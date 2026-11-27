using System.Collections.Generic;
using UnityEngine;

namespace PNJunctionLab
{
    /// <summary>
    /// Generates and caches lightweight procedural AudioClips so no external sound files are required.
    /// </summary>
    public static class ProceduralAudio
    {
        public enum SoundKind
        {
            Hover,
            Grab,
            Release,
            SocketSnap,
            SocketInvalid,
            ButtonPress,
            Teleport,
            Success,
            TabletSummon,
            TabletDismiss
        }

        private const int SampleRate = 44100;
        private static readonly Dictionary<SoundKind, AudioClip> ClipCache = new Dictionary<SoundKind, AudioClip>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            ClipCache.Clear();
        }

        /// <summary>
        /// Returns a cached procedural AudioClip for the requested interaction sound kind.
        /// </summary>
        public static AudioClip GetClip(SoundKind kind)
        {
            if (ClipCache.TryGetValue(kind, out AudioClip cached) && cached != null)
            {
                return cached;
            }

            AudioClip clip = BuildClip(kind);
            ClipCache[kind] = clip;
            return clip;
        }

        /// <summary>
        /// Plays the requested procedural sound one-shot if the provided AudioSource is valid.
        /// </summary>
        public static void PlayOneShot(AudioSource source, SoundKind kind, float volume = 0.65f)
        {
            if (source == null || !source.isActiveAndEnabled)
            {
                return;
            }

            AudioClip clip = GetClip(kind);
            if (clip != null)
            {
                source.PlayOneShot(clip, Mathf.Clamp01(volume));
            }
        }

        private static AudioClip BuildClip(SoundKind kind)
        {
            switch (kind)
            {
                case SoundKind.Hover:
                    return CreateSweepClip("Proc_Hover", 620f, 760f, 0.045f, 0.16f);
                case SoundKind.Grab:
                    return CreateSweepClip("Proc_Grab", 320f, 480f, 0.075f, 0.30f);
                case SoundKind.Release:
                    return CreateSweepClip("Proc_Release", 380f, 220f, 0.07f, 0.25f);
                case SoundKind.SocketSnap:
                    return CreateTwoToneClip("Proc_SocketSnap", 520f, 780f, 0.065f, 0.09f, 0.36f);
                case SoundKind.SocketInvalid:
                    return CreateTwoToneClip("Proc_SocketInvalid", 240f, 175f, 0.08f, 0.12f, 0.35f);
                case SoundKind.ButtonPress:
                    return CreateSweepClip("Proc_ButtonPress", 740f, 460f, 0.055f, 0.34f);
                case SoundKind.Teleport:
                    return CreateSweepClip("Proc_Teleport", 280f, 640f, 0.14f, 0.28f);
                case SoundKind.Success:
                    return CreateArpeggioClip("Proc_Success", new[] { 523.25f, 659.25f, 783.99f }, 0.11f, 0.35f);
                case SoundKind.TabletSummon:
                    return CreateTwoToneClip("Proc_TabletSummon", 440f, 660f, 0.055f, 0.085f, 0.24f);
                case SoundKind.TabletDismiss:
                    return CreateTwoToneClip("Proc_TabletDismiss", 587.33f, 392f, 0.05f, 0.08f, 0.22f);
                default:
                    return CreateSweepClip("Proc_Default", 440f, 440f, 0.05f, 0.2f);
            }
        }

        private static AudioClip CreateSweepClip(string clipName, float startFreq, float endFreq, float duration, float gain)
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            float[] samples = new float[sampleCount];
            double phase = 0.0;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleCount;
                float freq = Mathf.Lerp(startFreq, endFreq, t);
                phase += (2.0 * System.Math.PI * freq) / SampleRate;

                float attack = Mathf.Clamp01(t / 0.08f);
                float decay = Mathf.Exp(-4.5f * t);
                samples[i] = Mathf.Sin((float)phase) * attack * decay * gain;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateTwoToneClip(string clipName, float firstFreq, float secondFreq, float firstDur, float secondDur, float gain)
        {
            int count1 = Mathf.Max(1, Mathf.RoundToInt(SampleRate * firstDur));
            int count2 = Mathf.Max(1, Mathf.RoundToInt(SampleRate * secondDur));
            float[] samples = new float[count1 + count2];

            WriteToneSegment(samples, 0, count1, firstFreq, gain);
            WriteToneSegment(samples, count1, count2, secondFreq, gain);

            AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateArpeggioClip(string clipName, float[] frequencies, float noteDuration, float gain)
        {
            int noteSamples = Mathf.Max(1, Mathf.RoundToInt(SampleRate * noteDuration));
            float[] samples = new float[noteSamples * frequencies.Length];

            for (int n = 0; n < frequencies.Length; n++)
            {
                WriteToneSegment(samples, n * noteSamples, noteSamples, frequencies[n], gain);
            }

            AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static void WriteToneSegment(float[] buffer, int offset, int length, float freq, float gain)
        {
            double phase = 0.0;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)length;
                phase += (2.0 * System.Math.PI * freq) / SampleRate;
                float attack = Mathf.Clamp01(t / 0.06f);
                float decay = Mathf.Exp(-3.8f * t);
                buffer[offset + i] = Mathf.Sin((float)phase) * attack * decay * gain;
            }
        }
    }
}
