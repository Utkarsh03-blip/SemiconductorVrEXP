using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PNJunctionLab
{
    /// <summary>
    /// World-space billboarded NPC speech bubble with typewriter text reveal, 3D voice mute toggle,
    /// ray/poke "Next" button, and branching choice / "Ask me about..." glossary buttons.
    /// </summary>
    public class NPCSpeechBubble : MonoBehaviour
    {
        [SerializeField] private DialogueSystem dialogueSystem;
        [SerializeField] private NPCNarratorAudio narratorAudio;
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text nextButtonLabel;
        [SerializeField] private Button voiceMuteButton;
        [SerializeField] private TMP_Text voiceMuteLabel;
        [SerializeField] private Image voiceMuteBackground;
        [SerializeField] private InteractionFeedback voiceMuteFeedback;
        [SerializeField] private XRSimpleInteractable bubbleInteractable;
        [SerializeField] private Button[] optionButtons;
        [SerializeField] private TMP_Text[] optionLabels;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private float charactersPerSecond = 48f;
        [SerializeField] private float baseMinFontSize = 36f;
        [SerializeField] private float baseMaxFontSize = 50f;

        private static readonly Color VoiceOnColor = new Color(0.14f, 0.52f, 0.46f, 1f);
        private static readonly Color VoiceMutedColor = new Color(0.60f, 0.22f, 0.22f, 1f);

        public bool IsTyping { get; private set; }
        public string FullText { get; private set; } = string.Empty;

        private Camera cachedCamera;
        private DialogueState currentState;
        private float revealProgress;
        private int lastBlipStep;
        private float textScale = 1f;
        private readonly List<GlossaryTopic> activeTopicBindings = new List<GlossaryTopic>();

        private void Awake()
        {
            cachedCamera = Camera.main;
            if (narratorAudio == null)
            {
                narratorAudio = GetComponentInParent<NPCNarratorAudio>();
            }

            if (visualRoot != null)
            {
                visualRoot.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (dialogueSystem != null)
            {
                dialogueSystem.StateChanged += HandleDialogueStateChanged;
                dialogueSystem.AskTopicsChanged += HandleAskTopicsChanged;
            }

            if (narratorAudio != null)
            {
                narratorAudio.MuteChanged += HandleMuteChanged;
            }

            if (nextButton != null)
            {
                nextButton.onClick.AddListener(HandleNextClicked);
            }

            if (voiceMuteButton != null)
            {
                voiceMuteButton.onClick.AddListener(HandleVoiceMuteClicked);
            }

            if (bubbleInteractable != null)
            {
                bubbleInteractable.selectEntered.AddListener(HandleBubbleSelected);
            }

            if (optionButtons != null)
            {
                for (int i = 0; i < optionButtons.Length; i++)
                {
                    int idx = i;
                    if (optionButtons[i] != null)
                    {
                        optionButtons[i].onClick.AddListener(() => HandleOptionClicked(idx));
                    }
                }
            }

            RefreshMuteButtonUI(narratorAudio != null && narratorAudio.IsMuted);
        }

        private void OnDisable()
        {
            if (dialogueSystem != null)
            {
                dialogueSystem.StateChanged -= HandleDialogueStateChanged;
                dialogueSystem.AskTopicsChanged -= HandleAskTopicsChanged;
            }

            if (narratorAudio != null)
            {
                narratorAudio.MuteChanged -= HandleMuteChanged;
            }

            if (nextButton != null)
            {
                nextButton.onClick.RemoveListener(HandleNextClicked);
            }

            if (voiceMuteButton != null)
            {
                voiceMuteButton.onClick.RemoveListener(HandleVoiceMuteClicked);
            }

            if (bubbleInteractable != null)
            {
                bubbleInteractable.selectEntered.RemoveListener(HandleBubbleSelected);
            }

            if (optionButtons != null)
            {
                for (int i = 0; i < optionButtons.Length; i++)
                {
                    if (optionButtons[i] != null)
                    {
                        optionButtons[i].onClick.RemoveAllListeners();
                    }
                }
            }
        }

        private void Update()
        {
            if (!IsTyping || bodyText == null)
            {
                return;
            }

            revealProgress += Time.unscaledDeltaTime * charactersPerSecond;
            int charCount = Mathf.Clamp(Mathf.FloorToInt(revealProgress), 0, FullText.Length);
            if (bodyText.maxVisibleCharacters != charCount)
            {
                bodyText.maxVisibleCharacters = charCount;
                bool hasNarrator = narratorAudio != null;
                if (!hasNarrator && currentState.VoiceClip == null && charCount / 6 > lastBlipStep)
                {
                    lastBlipStep = charCount / 6;
                    ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Hover, 0.12f);
                }
            }

            if (charCount >= FullText.Length)
            {
                FinishTyping();
            }
        }

        private void LateUpdate()
        {
            if (visualRoot == null || !visualRoot.activeSelf)
            {
                return;
            }

            if (cachedCamera == null)
            {
                cachedCamera = Camera.main;
            }

            if (cachedCamera == null)
            {
                return;
            }

            // World-space Canvas forward (+Z) faces away from the viewer so text reads left-to-right.
            Vector3 awayFromCam = transform.position - cachedCamera.transform.position;
            awayFromCam.y = Mathf.Clamp(awayFromCam.y, -0.35f * awayFromCam.magnitude, 0.35f * awayFromCam.magnitude);
            if (awayFromCam.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(awayFromCam.normalized, Vector3.up);
            }
        }

        /// <summary>
        /// Adjusts subtitle text size (called by LabTablet Settings).
        /// </summary>
        public void SetTextScale(float scale)
        {
            textScale = Mathf.Clamp(scale, 0.75f, 1.4f);
            if (bodyText != null)
            {
                bodyText.fontSizeMin = baseMinFontSize * textScale;
                bodyText.fontSizeMax = baseMaxFontSize * textScale;
            }
        }

        /// <summary>
        /// Simulates or executes clicking the Next button (complete typing first, then advance).
        /// </summary>
        public void HandleNextClicked()
        {
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.45f);
            if (IsTyping)
            {
                FinishTyping();
                return;
            }

            dialogueSystem?.Advance();
        }

        /// <summary>
        /// Toggles Dr. Vera's 3D voice narration directly from the speech bubble header button.
        /// </summary>
        public void HandleVoiceMuteClicked()
        {
            if (voiceMuteFeedback != null)
            {
                voiceMuteFeedback.PlayClickSound();
            }
            else
            {
                ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.45f);
            }

            narratorAudio?.ToggleMuted();
        }

        private void HandleMuteChanged(bool muted)
        {
            RefreshMuteButtonUI(muted);
        }

        private void HandleBubbleSelected(SelectEnterEventArgs args)
        {
            HandleNextClicked();
        }

        private void HandleOptionClicked(int index)
        {
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.5f);
            if (currentState.Choices != null && index < currentState.Choices.Count)
            {
                dialogueSystem?.SelectChoice(index);
                return;
            }

            if (index >= 0 && index < activeTopicBindings.Count && activeTopicBindings[index] != null)
            {
                dialogueSystem?.AskAboutTopic(activeTopicBindings[index]);
            }
        }

        private void HandleAskTopicsChanged()
        {
            if (visualRoot != null && visualRoot.activeSelf)
            {
                RefreshOptionButtons(currentState);
            }
        }

        private void HandleDialogueStateChanged(DialogueState state)
        {
            currentState = state;
            if (visualRoot != null)
            {
                visualRoot.SetActive(state.IsActive);
            }

            if (!state.IsActive)
            {
                IsTyping = false;
                FullText = string.Empty;
                return;
            }

            if (speakerText != null)
            {
                speakerText.text = state.Speaker + " (" + state.Emotion + ")";
            }

            FullText = state.Text ?? string.Empty;
            revealProgress = 0f;
            lastBlipStep = -1;
            IsTyping = !string.IsNullOrEmpty(FullText);

            if (bodyText != null)
            {
                bodyText.text = FullText;
                bodyText.maxVisibleCharacters = 0;
            }

            RefreshMuteButtonUI(narratorAudio != null && narratorAudio.IsMuted);
            UpdateNextLabel();
            RefreshOptionButtons(state);
        }

        private void FinishTyping()
        {
            IsTyping = false;
            if (bodyText != null)
            {
                bodyText.maxVisibleCharacters = int.MaxValue;
            }

            UpdateNextLabel();
        }

        private void UpdateNextLabel()
        {
            if (nextButtonLabel == null)
            {
                return;
            }

            if (IsTyping)
            {
                nextButtonLabel.text = "Reveal";
            }
            else
            {
                nextButtonLabel.text = currentState.HasNextLine ? "Next" : "Continue";
            }
        }

        private void RefreshMuteButtonUI(bool muted)
        {
            if (voiceMuteLabel != null)
            {
                voiceMuteLabel.text = muted ? "Voice: MUTED" : "Voice: ON";
            }

            if (voiceMuteBackground != null)
            {
                voiceMuteBackground.color = muted ? VoiceMutedColor : VoiceOnColor;
            }
        }

        private void RefreshOptionButtons(DialogueState state)
        {
            activeTopicBindings.Clear();
            if (optionButtons == null)
            {
                return;
            }

            bool hasChoices = state.Choices != null && state.Choices.Count > 0;
            IReadOnlyList<GlossaryTopic> topics = (!hasChoices && state.AllowAskTopics && dialogueSystem != null)
                ? dialogueSystem.GetEffectiveAskTopics()
                : null;

            for (int i = 0; i < optionButtons.Length; i++)
            {
                Button btn = optionButtons[i];
                TMP_Text lbl = (optionLabels != null && i < optionLabels.Length) ? optionLabels[i] : null;
                if (btn == null)
                {
                    continue;
                }

                if (hasChoices && i < state.Choices.Count && state.Choices[i] != null)
                {
                    btn.gameObject.SetActive(true);
                    if (lbl != null) lbl.text = state.Choices[i].Label;
                }
                else if (topics != null && i < topics.Count && topics[i] != null)
                {
                    btn.gameObject.SetActive(true);
                    activeTopicBindings.Add(topics[i]);
                    if (lbl != null) lbl.text = "Ask: " + topics[i].Title;
                }
                else
                {
                    btn.gameObject.SetActive(false);
                }
            }
        }
    }
}
