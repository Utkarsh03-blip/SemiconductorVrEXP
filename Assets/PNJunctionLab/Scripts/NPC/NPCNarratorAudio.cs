using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System.Diagnostics;
using System.Threading.Tasks;
#endif

namespace PNJunctionLab
{
    /// <summary>
    /// Converts compact physics notation in dialogue captions into natural spoken English
    /// and drives 3D spatial voice narration playback + Mute persistence for Dr. Vera.
    /// </summary>
    public class NPCNarratorAudio : MonoBehaviour
    {
        public const string MutePrefKey = "PNLab_NPCVoiceMuted";

        [Header("Core References")]
        [SerializeField] private DialogueSystem dialogueSystem;
        [SerializeField] private AudioSource voiceAudioSource;
        [SerializeField] private Transform headAnchor;

        [Header("3D Spatial Audio Configuration")]
        [SerializeField] private float spatialBlend = 1.0f;
        [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
        [SerializeField] private float minDistance = 1.2f;
        [SerializeField] private float maxDistance = 14.0f;
        [SerializeField] private float dopplerLevel = 0f;
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.95f;

        public event Action<bool> MuteChanged;
        public event Action<bool> SpeakingStateChanged;

        public bool IsMuted { get; private set; }
        public bool IsSpeaking { get; private set; }
        public AudioSource VoiceAudioSource => voiceAudioSource;

        private readonly Dictionary<int, AudioClip> runtimeAdHocCache = new Dictionary<int, AudioClip>();
        private int activeRequestGeneration;
        private float androidTtsSpeakUntilTime = -1f;
        private bool wasSpeakingLastFrame;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private Task<byte[]> pendingSynthTask;
        private string pendingTempWavPath;
        private int pendingHash;
        private int pendingGeneration;
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject androidTts;
        private volatile bool androidTtsReady;
#endif

        private void Awake()
        {
            ResolveReferences();
            ConfigureSpatialAudioSource();

            IsMuted = PlayerPrefs.GetInt(MutePrefKey, 0) == 1;
#if UNITY_ANDROID && !UNITY_EDITOR
            InitializeAndroidTts();
#endif
        }

        private void OnEnable()
        {
            if (dialogueSystem != null)
            {
                dialogueSystem.StateChanged += HandleDialogueStateChanged;
            }
        }

        private void OnDisable()
        {
            if (dialogueSystem != null)
            {
                dialogueSystem.StateChanged -= HandleDialogueStateChanged;
            }

            StopNarration();
        }

        private void OnDestroy()
        {
            foreach (AudioClip cached in runtimeAdHocCache.Values)
            {
                if (cached != null)
                {
                    Destroy(cached);
                }
            }
            runtimeAdHocCache.Clear();

#if UNITY_ANDROID && !UNITY_EDITOR
            ShutdownAndroidTts();
#endif
        }

        private void Update()
        {
            PositionVoiceEmitterAtHead();

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            PollPendingWindowsSynthesis();
#endif

            bool playingClip = voiceAudioSource != null && voiceAudioSource.isPlaying;
            bool playingAndroidTts = Time.unscaledTime < androidTtsSpeakUntilTime;
            bool pendingSynth =
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                pendingSynthTask != null && !IsMuted;
#else
                false;
#endif
            bool speakingNow = !IsMuted && (playingClip || playingAndroidTts || pendingSynth);
            if (speakingNow != IsSpeaking)
            {
                IsSpeaking = speakingNow;
                if (speakingNow != wasSpeakingLastFrame)
                {
                    wasSpeakingLastFrame = speakingNow;
                    SpeakingStateChanged?.Invoke(IsSpeaking);
                }
            }
        }

        /// <summary>
        /// Toggles Dr. Vera's spoken voice narration and persists the choice in PlayerPrefs.
        /// </summary>
        public void ToggleMuted()
        {
            SetMuted(!IsMuted);
        }

        /// <summary>
        /// Enables or mutes Dr. Vera's spoken voice narration, immediately stopping active playback when muted.
        /// </summary>
        public void SetMuted(bool muted)
        {
            if (IsMuted == muted)
            {
                if (muted)
                {
                    StopNarration();
                }
                return;
            }

            IsMuted = muted;
            PlayerPrefs.SetInt(MutePrefKey, IsMuted ? 1 : 0);
            PlayerPrefs.Save();

            if (IsMuted)
            {
                StopNarration();
            }

            MuteChanged?.Invoke(IsMuted);
        }

        /// <summary>
        /// Immediately interrupts and stops any playing or queued voice narration.
        /// </summary>
        public void StopNarration()
        {
            activeRequestGeneration++;
            androidTtsSpeakUntilTime = -1f;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            pendingSynthTask = null;
#endif

            if (voiceAudioSource != null)
            {
                voiceAudioSource.Stop();
                voiceAudioSource.clip = null;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            StopAndroidTts();
#endif

            if (IsSpeaking)
            {
                IsSpeaking = false;
                wasSpeakingLastFrame = false;
                SpeakingStateChanged?.Invoke(false);
            }
        }

        /// <summary>
        /// Formats raw scientific dialogue text into natural spoken wording for offline or ad-hoc TTS.
        /// </summary>
        public static string SanitizeForSpeech(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return string.Empty;
            }

            string text = Regex.Replace(rawText, "<.*?>", " ");
            text = text.Replace("\u2014", ", ").Replace("\u2013", " to ").Replace("--", ", ");
            text = text.Replace("->", " to ").Replace("<-", " from ");
            text = text.Replace(">=", " greater than or equal to ").Replace("<=", " less than or equal to ");
            text = text.Replace("~", "approximately ");
            text = Regex.Replace(text, @"e-/h\+|e\^-/h\^\+", "electron-hole");
            text = Regex.Replace(text, @"\(hole\s+h\+\)", "(a hole)");
            text = Regex.Replace(text, @"e\^-|e-", "electrons");
            text = Regex.Replace(text, @"h\^\+|h\+", "holes");
            text = Regex.Replace(text, @"fixed\s+orange\s*\+", "fixed orange positive");
            text = Regex.Replace(text, @"fixed\s+teal\s*-", "fixed teal negative");
            text = Regex.Replace(text, @"fixed\s*\+", "fixed positive");
            text = Regex.Replace(text, @"fixed\s*-", "fixed negative");
            text = Regex.Replace(text, @"cm\^-3|cm⁻³", "per cubic centimeter");
            text = text.Replace("S/cm", "Siemens per centimeter");
            text = text.Replace("mV/C", "millivolts per degree Celsius");
            text = Regex.Replace(text, @"\b([0-9]+(?:\.[0-9]+)?)\s*um\b", "$1 micrometers");
            text = Regex.Replace(text, @"\b([0-9]+(?:\.[0-9]+)?)\s*eV\b", "$1 electron volts");
            text = text.Replace("10^14", "10 to the 14").Replace("10^18", "10 to the 18");
            text = Regex.Replace(text, @"\bSi\b", "Silicon");
            text = Regex.Replace(text, @"\bGe\b", "Germanium");
            text = Regex.Replace(text, @"\bV0\b", "V zero");
            text = Regex.Replace(text, @"\bVd\b", "V d");
            text = Regex.Replace(text, @"\bVk\b", "V knee");
            text = Regex.Replace(text, @"\bni\b", "n i");
            text = Regex.Replace(text, @"\bNA\b", "N A");
            text = Regex.Replace(text, @"\bND\b", "N D");
            text = Regex.Replace(text, @"\bEF\b", "E F");
            text = Regex.Replace(text, @"\bEc\b", "E c");
            text = Regex.Replace(text, @"\bEv\b", "E v");
            text = Regex.Replace(text, @"\bEd\b", "E d");
            text = Regex.Replace(text, @"\bEa\b", "E a");
            text = Regex.Replace(text, @"\bEi\b", "E i");
            text = Regex.Replace(text, @"\bEg\b", "E g");
            text = Regex.Replace(text, @"\bIs\b", "I s");
            text = Regex.Replace(text, @"\bEbi\b", "built-in electric field");
            text = text.Replace("I-V", "current versus voltage");
            text = text.Replace("P-N", "P N");
            text = text.Replace("N-Type", "N type").Replace("P-Type", "P type");
            text = Regex.Replace(text, @"\s+", " ").Trim();
            return text;
        }

        private void HandleDialogueStateChanged(DialogueState state)
        {
            StopNarration();

            if (!state.IsActive || IsMuted)
            {
                return;
            }

            if (state.VoiceClip != null)
            {
                PlaySpatialClip(state.VoiceClip);
                return;
            }

            if (!string.IsNullOrWhiteSpace(state.Text))
            {
                SpeakDynamicFallback(state.Text);
            }
        }

        private void PlaySpatialClip(AudioClip clip)
        {
            if (voiceAudioSource == null || clip == null || IsMuted)
            {
                return;
            }

            PositionVoiceEmitterAtHead();
            ConfigureSpatialAudioSource();
            voiceAudioSource.clip = clip;
            voiceAudioSource.Play();

            IsSpeaking = true;
            wasSpeakingLastFrame = true;
            SpeakingStateChanged?.Invoke(true);
        }

        private void SpeakDynamicFallback(string rawText)
        {
            string spoken = SanitizeForSpeech(rawText);
            if (string.IsNullOrEmpty(spoken))
            {
                return;
            }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            int hash = spoken.GetHashCode();
            if (runtimeAdHocCache.TryGetValue(hash, out AudioClip cached) && cached != null)
            {
                PlaySpatialClip(cached);
                return;
            }

            StartWindowsOfflineFallback(spoken, hash);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SpeakAndroidTts(spoken);
#endif
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private void StartWindowsOfflineFallback(string spokenText, int textHash)
        {
            int gen = ++activeRequestGeneration;
            string tempPath = Path.Combine(Application.temporaryCachePath, "dr_vera_adhoc_" + unchecked((uint)textHash) + ".wav");
            pendingTempWavPath = tempPath;
            pendingHash = textHash;
            pendingGeneration = gen;

            string safeSpeech = spokenText.Replace("'", "''");
            string safeOut = tempPath.Replace("'", "''");
            string script =
                "Add-Type -AssemblyName System.Speech; " +
                "$s = New-Object System.Speech.Synthesis.SpeechSynthesizer; " +
                "try { $s.SelectVoice('Microsoft Zira Desktop') } catch {}; " +
                "$s.Rate = 0; $s.Volume = 100; " +
                "$fmt = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(22050, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono); " +
                "$s.SetOutputToWaveFile('" + safeOut + "', $fmt); " +
                "$s.Speak('" + safeSpeech + "'); " +
                "$s.Dispose()";

            pendingSynthTask = Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo("powershell.exe")
                    {
                        Arguments = "-NoProfile -NonInteractive -WindowStyle Hidden -Command \"" + script.Replace("\"", "\\\"") + "\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using (Process proc = Process.Start(psi))
                    {
                        proc?.WaitForExit(8000);
                    }

                    if (File.Exists(tempPath))
                    {
                        return File.ReadAllBytes(tempPath);
                    }
                }
                catch
                {
                    // Silent fallback if Windows PowerShell synthesis is unavailable.
                }

                return null;
            });
        }

        private void PollPendingWindowsSynthesis()
        {
            if (pendingSynthTask == null || !pendingSynthTask.IsCompleted)
            {
                return;
            }

            byte[] wavBytes = pendingSynthTask.Status == TaskStatus.RanToCompletion ? pendingSynthTask.Result : null;
            int hash = pendingHash;
            int gen = pendingGeneration;
            pendingSynthTask = null;

            if (IsMuted || gen != activeRequestGeneration || wavBytes == null || wavBytes.Length <= 44)
            {
                return;
            }

            AudioClip clip = BuildMono16WavClip("AdHoc_" + unchecked((uint)hash), wavBytes);
            if (clip != null)
            {
                runtimeAdHocCache[hash] = clip;
                PlaySpatialClip(clip);
            }
        }

        private static AudioClip BuildMono16WavClip(string clipName, byte[] wav)
        {
            int pos = 12;
            int sampleRate = 22050;
            short channels = 1;
            int dataOffset = -1;
            int dataSize = 0;

            while (pos + 8 <= wav.Length)
            {
                string chunkId = System.Text.Encoding.ASCII.GetString(wav, pos, 4);
                int chunkSize = BitConverter.ToInt32(wav, pos + 4);
                int payload = pos + 8;
                if (chunkId == "fmt " && payload + 16 <= wav.Length)
                {
                    channels = BitConverter.ToInt16(wav, payload + 2);
                    sampleRate = BitConverter.ToInt32(wav, payload + 4);
                }
                else if (chunkId == "data" && chunkSize > 0 && payload + chunkSize <= wav.Length)
                {
                    dataOffset = payload;
                    dataSize = chunkSize;
                    break;
                }

                pos = payload + chunkSize + (chunkSize & 1);
            }

            if (dataOffset < 0 || dataSize <= 0 || channels <= 0)
            {
                return null;
            }

            int totalSamples = dataSize / (2 * channels);
            if (totalSamples <= 0)
            {
                return null;
            }

            float[] samples = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                int byteIndex = dataOffset + i * 2 * channels;
                short pcm = BitConverter.ToInt16(wav, byteIndex);
                samples[i] = pcm / 32768f;
            }

            AudioClip clip = AudioClip.Create(clipName, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private sealed class AndroidTtsInitListener : AndroidJavaProxy
        {
            private readonly Action<int> onInitCallback;
            public AndroidTtsInitListener(Action<int> callback) : base("android.speech.tts.TextToSpeech$OnInitListener")
            {
                onInitCallback = callback;
            }

            public void onInit(int status)
            {
                onInitCallback?.Invoke(status);
            }
        }

        private void InitializeAndroidTts()
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    androidTts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity,
                        new AndroidTtsInitListener(status => { androidTtsReady = (status == 0); }));
                }
            }
            catch
            {
                androidTtsReady = false;
            }
        }

        private void SpeakAndroidTts(string text)
        {
            if (!androidTtsReady || androidTts == null || IsMuted)
            {
                return;
            }

            try
            {
                using (var bundle = new AndroidJavaObject("android.os.Bundle"))
                {
                    androidTts.Call<int>("speak", text, 0, bundle, "pn_npc_line");
                }
                float estSeconds = Mathf.Clamp(text.Length / 14.5f, 1.5f, 15f);
                androidTtsSpeakUntilTime = Time.unscaledTime + estSeconds;
                IsSpeaking = true;
                wasSpeakingLastFrame = true;
                SpeakingStateChanged?.Invoke(true);
            }
            catch
            {
                androidTtsSpeakUntilTime = -1f;
            }
        }

        private void StopAndroidTts()
        {
            if (androidTts != null)
            {
                try { androidTts.Call<int>("stop"); } catch { }
            }
        }

        private void ShutdownAndroidTts()
        {
            if (androidTts != null)
            {
                try { androidTts.Call("shutdown"); androidTts.Dispose(); } catch { }
                androidTts = null;
            }
        }
#endif

        private void ResolveReferences()
        {
            if (dialogueSystem == null)
            {
                dialogueSystem = FindAnyObjectByType<DialogueSystem>();
            }

            if (headAnchor == null)
            {
                Transform existingHead = transform.Find("VoiceHeadEmitter");
                if (existingHead == null)
                {
                    var go = new GameObject("VoiceHeadEmitter");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(0f, 1.65f, 0.04f);
                    existingHead = go.transform;
                }
                headAnchor = existingHead;
            }

            if (voiceAudioSource == null && headAnchor != null)
            {
                voiceAudioSource = headAnchor.GetComponent<AudioSource>();
                if (voiceAudioSource == null)
                {
                    voiceAudioSource = headAnchor.gameObject.AddComponent<AudioSource>();
                }
            }
        }

        private void ConfigureSpatialAudioSource()
        {
            if (voiceAudioSource == null)
            {
                return;
            }

            voiceAudioSource.playOnAwake = false;
            voiceAudioSource.loop = false;
            voiceAudioSource.spatialBlend = Mathf.Clamp01(spatialBlend);
            voiceAudioSource.rolloffMode = rolloffMode;
            voiceAudioSource.minDistance = Mathf.Max(0.2f, minDistance);
            voiceAudioSource.maxDistance = Mathf.Max(voiceAudioSource.minDistance + 0.5f, maxDistance);
            voiceAudioSource.dopplerLevel = dopplerLevel;
            voiceAudioSource.volume = Mathf.Clamp01(voiceVolume);
        }

        private void PositionVoiceEmitterAtHead()
        {
            if (voiceAudioSource == null || headAnchor == null)
            {
                return;
            }

            if (voiceAudioSource.transform != headAnchor)
            {
                voiceAudioSource.transform.position = headAnchor.position;
            }
        }
    }
}
