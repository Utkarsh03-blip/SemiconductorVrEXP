using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PNJunctionLab
{
    /// <summary>
    /// Floating, button-summoned world-space Lab Tablet containing the active station's step checklist,
    /// objective instruction, glossary, controls help, hint trigger, and comfort/text-size settings.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class LabTablet : MonoBehaviour
    {
        public enum TabKind
        {
            Objective,
            Glossary,
            Controls,
            Settings
        }

        [Header("Core References")]
        [SerializeField] private LessonManager lessonManager;
        [SerializeField] private DialogueSystem dialogueSystem;
        [SerializeField] private LocomotionModeController locomotionController;
        [SerializeField] private NPCSpeechBubble speechBubble;
        [SerializeField] private ProgressHUD progressHud;
        [SerializeField] private AudioSource audioSource;

        [Header("Floating Summon & XR Physics")]
        [SerializeField] private XRGrabInteractable grabInteractable;
        [SerializeField] private InteractionFeedback interactionFeedback;
        [SerializeField] private Rigidbody tabletBody;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform playerHead;
        [SerializeField] private InputActionProperty toggleTabletAction;
        [SerializeField] private float summonDistanceMeters = 0.65f;
        [SerializeField] private float summonVerticalOffsetMeters = -0.05f;
        [SerializeField] private float summonUpwardTiltDegrees = 20f;
        [SerializeField] private float toggleAnimDurationSeconds = 0.15f;
        [SerializeField] private bool hideOnStart = true;

        [Header("Startup Welcome")]
        [SerializeField] private bool showWelcomeOnStart = true;
        [SerializeField] private string startupWelcomeTitle = "Welcome to Semiconductor Lab Simulation";
        [SerializeField] private string startupWelcomeSubtitle = "Interactive P-N Junction Physics VR Lab";
        [SerializeField] private float startupWelcomeSeconds = 3.5f;
        [SerializeField] private float startupObjectivePreviewSeconds = 2.5f;

        [Header("Tabs & Panels")]
        [SerializeField] private Button objectiveTabButton;
        [SerializeField] private Button glossaryTabButton;
        [SerializeField] private Button controlsTabButton;
        [SerializeField] private Button settingsTabButton;
        [SerializeField] private GameObject objectivePanel;
        [SerializeField] private GameObject glossaryPanel;
        [SerializeField] private GameObject controlsPanel;
        [SerializeField] private GameObject settingsPanel;

        [Header("Objective Tab")]
        [SerializeField] private TMP_Text stepListText;
        [SerializeField] private Button hintButton;

        [Header("Glossary Tab")]
        [SerializeField] private GlossaryTopic[] glossaryTopics;
        [SerializeField] private Button[] glossaryButtons;
        [SerializeField] private TMP_Text[] glossaryButtonLabels;
        [SerializeField] private TMP_Text glossaryDetailText;
        [SerializeField] private Button askNpcTopicButton;

        [Header("Controls Tab")]
        [SerializeField] private TMP_Text controlsBodyText;

        [Header("Menu Tab")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button volumeDownButton;
        [SerializeField] private Button volumeUpButton;
        [SerializeField] private TMP_Text volumeLabel;
        [SerializeField] private Button exitButton;
        [SerializeField] private Button textSizeButton;
        [SerializeField] private TMP_Text textSizeLabel;
        [SerializeField] private Button locomotionToggleButton;
        [SerializeField] private TMP_Text locomotionLabel;
        [SerializeField] private Button postureToggleButton;
        [SerializeField] private TMP_Text postureLabel;
        [SerializeField] private Button snapTurnToggleButton;
        [SerializeField] private TMP_Text snapTurnLabel;
        [SerializeField] private NPCNarratorAudio narratorAudio;
        [SerializeField] private Button npcVoiceToggleButton;
        [SerializeField] private TMP_Text npcVoiceLabel;
        [SerializeField] private Image npcVoiceButtonBackground;
        [SerializeField] private InputActionProperty openMenuAction;

        [Header("Scalable Body Labels")]
        [SerializeField] private TMP_Text[] scalableTexts;

        public event Action<bool> TabletVisibilityChanged;
        public event Action TabletSummoned;
        public event Action TabletDismissed;

        public TabKind ActiveTab { get; private set; } = TabKind.Objective;
        public int TextSizeIndex { get; private set; } = 1; // 0=Small, 1=Medium, 2=Large
        public bool IsSeatedMode { get; private set; }
        public bool IsVisible { get; private set; } = true;

        private static readonly float[] TextScales = { 0.85f, 1.0f, 1.2f };
        private static readonly string[] TextSizeNames = { "Small", "Medium", "Large" };
        private static readonly Color VoiceOnButtonColor = new Color(0.14f, 0.45f, 0.44f, 1f);
        private static readonly Color VoiceMutedButtonColor = new Color(0.54f, 0.22f, 0.22f, 1f);

        private float[] baseFontSizes;
        private float[] baseFontMinSizes;
        private float[] baseFontMaxSizes;
        private bool[] wasAutoSizing;
        private int selectedTopicIndex;
        private const string MasterVolumePrefKey = "PNLab_MasterVolume";
        private Coroutine visibilityAnimCoroutine;
        private Coroutine restoreKinematicCoroutine;
        private Coroutine startupWelcomeCoroutine;
        private InputAction runtimeFallbackAction;
        private InputAction runtimeMenuFallbackAction;
        private bool enabledToggleActionSelf;
        private bool enabledMenuActionSelf;
        private bool showingStartupWelcome;
        private bool startupAutoDismissArmed;
        private bool showingSimulationComplete;
        private float masterVolume = 1.0f;

        private void Awake()
        {
            ResolveCoreReferences();
            ConfigureFloatingRigidbodyAndGrab();
            EnsureToggleInputConfigured();
            LoadAndApplyMasterVolume();
            CacheBaseFontSizes();
            PopulateGlossaryButtons();
            PopulateControlsText();

            if (hideOnStart)
            {
                SetVisibleImmediate(false);
            }
            else
            {
                SetVisibleImmediate(true);
            }
        }

        private void Start()
        {
            if (showWelcomeOnStart)
            {
                startupWelcomeCoroutine = StartCoroutine(StartupWelcomeSequenceRoutine());
            }
        }

        private void OnEnable()
        {
            if (lessonManager != null)
            {
                lessonManager.StepChanged += HandleLessonStepChanged;
                lessonManager.ProgressChanged += HandleLessonProgressChanged;
                lessonManager.ActiveStationChanged += HandleActiveStationChanged;
                lessonManager.LabCompleted += HandleLabCompleted;
            }

            if (locomotionController != null)
            {
                locomotionController.ContinuousMoveChanged += HandleContinuousMoveChanged;
            }

            if (narratorAudio != null)
            {
                narratorAudio.MuteChanged += HandleNpcVoiceMuteChanged;
            }

            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.AddListener(HandleGrabEntered);
                grabInteractable.selectExited.AddListener(HandleGrabExited);
            }

            InputAction action = toggleTabletAction.action;
            if (action != null)
            {
                action.performed += HandleToggleInputPerformed;
                if (!action.enabled)
                {
                    action.Enable();
                    enabledToggleActionSelf = true;
                }
            }

            InputAction menuAct = openMenuAction.action;
            if (menuAct != null)
            {
                menuAct.performed += HandleOpenMenuInputPerformed;
                if (!menuAct.enabled)
                {
                    menuAct.Enable();
                    enabledMenuActionSelf = true;
                }
            }

            objectiveTabButton?.onClick.AddListener(() => SelectTab(TabKind.Objective));
            glossaryTabButton?.onClick.AddListener(() => SelectTab(TabKind.Glossary));
            controlsTabButton?.onClick.AddListener(() => SelectTab(TabKind.Controls));
            settingsTabButton?.onClick.AddListener(() => SelectTab(TabKind.Settings));

            hintButton?.onClick.AddListener(HandleHintPressed);
            askNpcTopicButton?.onClick.AddListener(HandleAskNpcPressed);
            resumeButton?.onClick.AddListener(ResumeSimulation);
            restartButton?.onClick.AddListener(RestartSimulation);
            volumeDownButton?.onClick.AddListener(HandleVolumeDownPressed);
            volumeUpButton?.onClick.AddListener(HandleVolumeUpPressed);
            exitButton?.onClick.AddListener(ExitSimulation);
            textSizeButton?.onClick.AddListener(CycleTextSize);
            locomotionToggleButton?.onClick.AddListener(HandleLocomotionToggled);
            postureToggleButton?.onClick.AddListener(HandlePostureToggled);
            snapTurnToggleButton?.onClick.AddListener(HandleTurnModeToggled);
            npcVoiceToggleButton?.onClick.AddListener(HandleNpcVoiceToggled);

            if (glossaryButtons != null)
            {
                for (int i = 0; i < glossaryButtons.Length; i++)
                {
                    int idx = i;
                    if (glossaryButtons[i] != null)
                    {
                        glossaryButtons[i].onClick.AddListener(() => SelectGlossaryTopic(idx));
                    }
                }
            }

            SelectTabInternal(ActiveTab, false);
            RefreshStepList();
            SelectGlossaryTopic(0);
            RefreshSettingsLabels();
        }

        private void OnDisable()
        {
            if (lessonManager != null)
            {
                lessonManager.StepChanged -= HandleLessonStepChanged;
                lessonManager.ProgressChanged -= HandleLessonProgressChanged;
                lessonManager.ActiveStationChanged -= HandleActiveStationChanged;
                lessonManager.LabCompleted -= HandleLabCompleted;
            }

            if (locomotionController != null)
            {
                locomotionController.ContinuousMoveChanged -= HandleContinuousMoveChanged;
            }

            if (narratorAudio != null)
            {
                narratorAudio.MuteChanged -= HandleNpcVoiceMuteChanged;
            }

            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.RemoveListener(HandleGrabEntered);
                grabInteractable.selectExited.RemoveListener(HandleGrabExited);
            }

            InputAction action = toggleTabletAction.action;
            if (action != null)
            {
                action.performed -= HandleToggleInputPerformed;
                if (enabledToggleActionSelf)
                {
                    action.Disable();
                    enabledToggleActionSelf = false;
                }
            }

            InputAction menuAct = openMenuAction.action;
            if (menuAct != null)
            {
                menuAct.performed -= HandleOpenMenuInputPerformed;
                if (enabledMenuActionSelf)
                {
                    menuAct.Disable();
                    enabledMenuActionSelf = false;
                }
            }

            objectiveTabButton?.onClick.RemoveAllListeners();
            glossaryTabButton?.onClick.RemoveAllListeners();
            controlsTabButton?.onClick.RemoveAllListeners();
            settingsTabButton?.onClick.RemoveAllListeners();
            hintButton?.onClick.RemoveAllListeners();
            askNpcTopicButton?.onClick.RemoveAllListeners();
            resumeButton?.onClick.RemoveAllListeners();
            restartButton?.onClick.RemoveAllListeners();
            volumeDownButton?.onClick.RemoveAllListeners();
            volumeUpButton?.onClick.RemoveAllListeners();
            exitButton?.onClick.RemoveAllListeners();
            textSizeButton?.onClick.RemoveAllListeners();
            locomotionToggleButton?.onClick.RemoveAllListeners();
            postureToggleButton?.onClick.RemoveAllListeners();
            snapTurnToggleButton?.onClick.RemoveAllListeners();
            npcVoiceToggleButton?.onClick.RemoveAllListeners();

            if (glossaryButtons != null)
            {
                for (int i = 0; i < glossaryButtons.Length; i++)
                {
                    glossaryButtons[i]?.onClick.RemoveAllListeners();
                }
            }
        }

        private void OnDestroy()
        {
            if (runtimeFallbackAction != null)
            {
                runtimeFallbackAction.Disable();
                runtimeFallbackAction.Dispose();
                runtimeFallbackAction = null;
            }

            if (runtimeMenuFallbackAction != null)
            {
                runtimeMenuFallbackAction.Disable();
                runtimeMenuFallbackAction.Dispose();
                runtimeMenuFallbackAction = null;
            }
        }

        /// <summary>
        /// Opens the Menu tab directly (or resumes/dismisses if already showing Menu).
        /// </summary>
        public void OpenMenu()
        {
            CancelStartupWelcomeSequence(restoreObjectives: true);
            showingSimulationComplete = false;
            if (IsVisible && ActiveTab == TabKind.Settings)
            {
                DismissTabletInternal(false);
                return;
            }

            ActiveTab = TabKind.Settings;
            if (!IsVisible)
            {
                SummonTabletInternal(false);
            }
            else
            {
                SelectTabInternal(TabKind.Settings, true);
            }
        }

        public void ResumeSimulation()
        {
            CancelStartupWelcomeSequence(restoreObjectives: true);
            showingSimulationComplete = false;
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.45f);
            DismissTabletInternal(false);
        }

        public void RestartSimulation()
        {
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.45f);
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ExitSimulation()
        {
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.45f);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Toggles between summoning the floating tablet in front of the player and dismissing it.
        /// </summary>
        public void ToggleTablet()
        {
            CancelStartupWelcomeSequence(restoreObjectives: true);
            if (IsVisible)
            {
                DismissTablet();
            }
            else
            {
                SummonTablet();
            }
        }

        /// <summary>
        /// Positions the tablet stationary in mid-air in front of the camera and plays the summon scale-in transition.
        /// </summary>
        public void SummonTablet()
        {
            SummonTabletInternal(true);
        }

        /// <summary>
        /// Plays the dismiss scale-down transition and hides the tablet until summoned again.
        /// </summary>
        public void DismissTablet()
        {
            DismissTabletInternal(true);
        }

        private void SummonTabletInternal(bool raiseVisibilityEvents)
        {
            StopAnimationCoroutine();
            PositionInFrontOfPlayer();
            ConfigureFloatingRigidbodyAndGrab();

            IsVisible = true;
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(true);
                visualRoot.localScale = Vector3.one * 0.01f;
            }

            if (grabInteractable != null)
            {
                grabInteractable.enabled = true;
            }

            RefreshStepList();
            SelectTabInternal(ActiveTab, false);
            interactionFeedback?.ResetVisualState();
            interactionFeedback?.PlaySummonSound();

            visibilityAnimCoroutine = StartCoroutine(AnimateScaleRoutine(0.01f, 1f, true));
            if (raiseVisibilityEvents)
            {
                TabletSummoned?.Invoke();
                TabletVisibilityChanged?.Invoke(true);
            }
        }

        private void DismissTabletInternal(bool raiseVisibilityEvents)
        {
            if (!IsVisible && (visualRoot == null || !visualRoot.gameObject.activeSelf))
            {
                return;
            }

            ReleaseGrabIfSelected();
            StopAnimationCoroutine();

            IsVisible = false;
            if (grabInteractable != null)
            {
                grabInteractable.enabled = false;
            }

            interactionFeedback?.ResetVisualState();
            interactionFeedback?.PlayDismissSound();

            float startScale = visualRoot != null ? Mathf.Clamp(visualRoot.localScale.x, 0.01f, 1f) : 1f;
            visibilityAnimCoroutine = StartCoroutine(AnimateScaleRoutine(startScale, 0.01f, false));
            if (raiseVisibilityEvents)
            {
                TabletDismissed?.Invoke();
                TabletVisibilityChanged?.Invoke(false);
            }
        }

        public void SelectTab(TabKind tab)
        {
            CancelStartupWelcomeSequence(restoreObjectives: true);
            SelectTabInternal(tab, true);
        }

        public void CycleTextSize()
        {
            TextSizeIndex = (TextSizeIndex + 1) % TextScales.Length;
            ApplyTextScale(TextScales[TextSizeIndex]);
            RefreshSettingsLabels();
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.4f);
        }

        public void SelectGlossaryTopic(int index)
        {
            if (glossaryTopics == null || glossaryTopics.Length == 0 || glossaryDetailText == null)
            {
                return;
            }

            selectedTopicIndex = Mathf.Clamp(index, 0, glossaryTopics.Length - 1);
            GlossaryTopic topic = glossaryTopics[selectedTopicIndex];
            if (topic != null)
            {
                glossaryDetailText.text = "<b>" + topic.Title + "</b>\n\n" + topic.Summary;
            }
        }

        private void SelectTabInternal(TabKind tab, bool playSound)
        {
            ActiveTab = tab;
            if (objectivePanel != null) objectivePanel.SetActive(tab == TabKind.Objective);
            if (glossaryPanel != null) glossaryPanel.SetActive(tab == TabKind.Glossary);
            if (controlsPanel != null) controlsPanel.SetActive(tab == TabKind.Controls);
            if (settingsPanel != null) settingsPanel.SetActive(tab == TabKind.Settings);
            if (playSound)
            {
                ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.35f);
            }
        }

        private void HandleToggleInputPerformed(InputAction.CallbackContext ctx)
        {
            ToggleTablet();
        }

        private void HandleGrabEntered(SelectEnterEventArgs args)
        {
            // Keep the tablet open in the player's hand if they grab it during the startup banner.
            startupAutoDismissArmed = false;
        }

        private void HandleGrabExited(SelectExitEventArgs args)
        {
            ConfigureFloatingRigidbodyAndGrab();
            if (restoreKinematicCoroutine != null)
            {
                StopCoroutine(restoreKinematicCoroutine);
            }

            restoreKinematicCoroutine = StartCoroutine(EnforceFloatingNextFrame());
        }

        private IEnumerator EnforceFloatingNextFrame()
        {
            yield return null;
            ConfigureFloatingRigidbodyAndGrab();
            restoreKinematicCoroutine = null;
        }

        private IEnumerator AnimateScaleRoutine(float fromScale, float toScale, bool keepVisibleAtEnd)
        {
            if (visualRoot == null)
            {
                visibilityAnimCoroutine = null;
                yield break;
            }

            float duration = Mathf.Max(0.01f, toggleAnimDurationSeconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(elapsed / duration);
                float eased = keepVisibleAtEnd
                    ? 1f - Mathf.Pow(1f - u, 3f)
                    : u * u;
                float s = Mathf.Lerp(fromScale, toScale, eased);
                visualRoot.localScale = Vector3.one * s;
                yield return null;
            }

            visualRoot.localScale = Vector3.one * toScale;
            if (!keepVisibleAtEnd)
            {
                visualRoot.gameObject.SetActive(false);
                visualRoot.localScale = Vector3.one;
            }

            visibilityAnimCoroutine = null;
        }

        private void StopAnimationCoroutine()
        {
            if (visibilityAnimCoroutine != null)
            {
                StopCoroutine(visibilityAnimCoroutine);
                visibilityAnimCoroutine = null;
            }
        }

        private void SetVisibleImmediate(bool visible)
        {
            StopAnimationCoroutine();
            IsVisible = visible;

            if (visualRoot != null)
            {
                visualRoot.localScale = Vector3.one;
                visualRoot.gameObject.SetActive(visible);
            }

            if (grabInteractable != null)
            {
                grabInteractable.enabled = visible;
            }

            interactionFeedback?.ResetVisualState();
        }

        private void PositionInFrontOfPlayer()
        {
            Transform head = ResolvePlayerHead();
            if (head == null)
            {
                return;
            }

            if (transform.parent != null)
            {
                transform.SetParent(null, true);
            }

            Vector3 flatForward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.0001f)
            {
                flatForward = Vector3.forward;
            }
            flatForward.Normalize();

            Vector3 targetPos = head.position
                + flatForward * summonDistanceMeters
                + Vector3.up * summonVerticalOffsetMeters;
            // Screen canvas faces local -Z; Y away (+flatForward) and +X pitch tilts the screen upward toward the eyes.
            Quaternion targetRot = Quaternion.LookRotation(flatForward, Vector3.up)
                * Quaternion.Euler(summonUpwardTiltDegrees, 0f, 0f);

            if (tabletBody != null)
            {
                if (!tabletBody.isKinematic)
                {
                    tabletBody.linearVelocity = Vector3.zero;
                    tabletBody.angularVelocity = Vector3.zero;
                }
                tabletBody.useGravity = false;
                tabletBody.isKinematic = true;
                tabletBody.position = targetPos;
                tabletBody.rotation = targetRot;
            }

            transform.SetPositionAndRotation(targetPos, targetRot);
        }

        private void ConfigureFloatingRigidbodyAndGrab()
        {
            if (tabletBody == null)
            {
                tabletBody = GetComponent<Rigidbody>();
            }

            if (tabletBody != null)
            {
                if (!tabletBody.isKinematic)
                {
                    tabletBody.linearVelocity = Vector3.zero;
                    tabletBody.angularVelocity = Vector3.zero;
                }
                tabletBody.useGravity = false;
                tabletBody.isKinematic = true;
            }

            if (grabInteractable == null)
            {
                grabInteractable = GetComponent<XRGrabInteractable>();
            }

            if (grabInteractable != null)
            {
                grabInteractable.throwOnDetach = false;
            }
        }

        private void ReleaseGrabIfSelected()
        {
            if (grabInteractable != null && grabInteractable.isSelected && grabInteractable.interactionManager != null)
            {
                var selecting = grabInteractable.firstInteractorSelecting;
                if (selecting != null)
                {
                    grabInteractable.interactionManager.SelectExit(selecting, grabInteractable);
                }
            }
        }

        private Transform ResolvePlayerHead()
        {
            if (playerHead == null && Camera.main != null)
            {
                playerHead = Camera.main.transform;
            }

            return playerHead;
        }

        private void ResolveCoreReferences()
        {
            if (grabInteractable == null) grabInteractable = GetComponent<XRGrabInteractable>();
            if (interactionFeedback == null) interactionFeedback = GetComponent<InteractionFeedback>();
            if (tabletBody == null) tabletBody = GetComponent<Rigidbody>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (progressHud == null) progressHud = GetComponent<ProgressHUD>();
            if (narratorAudio == null) narratorAudio = FindAnyObjectByType<NPCNarratorAudio>();
            if (visualRoot == null)
            {
                Transform foundRoot = transform.Find("ContentRoot");
                visualRoot = foundRoot != null ? foundRoot : transform;
            }
        }

        private void EnsureToggleInputConfigured()
        {
            InputAction current = toggleTabletAction.action;
            if (current == null || current.bindings.Count == 0)
            {
                runtimeFallbackAction = new InputAction("ToggleFloatingLabTablet", InputActionType.Button);
                runtimeFallbackAction.AddBinding("<XRController>{LeftHand}/secondaryButton");
                runtimeFallbackAction.AddBinding("<Keyboard>/f");
                toggleTabletAction = new InputActionProperty(runtimeFallbackAction);
            }

            InputAction menuAct = openMenuAction.action;
            if (menuAct == null || menuAct.bindings.Count == 0)
            {
                runtimeMenuFallbackAction = new InputAction("OpenLabMenu", InputActionType.Button);
                runtimeMenuFallbackAction.AddBinding("<XRController>{LeftHand}/primaryButton");
                runtimeMenuFallbackAction.AddBinding("<Keyboard>/escape");
                openMenuAction = new InputActionProperty(runtimeMenuFallbackAction);
            }
        }

        private void HandleOpenMenuInputPerformed(InputAction.CallbackContext ctx)
        {
            OpenMenu();
        }

        private void LoadAndApplyMasterVolume()
        {
            masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumePrefKey, 1.0f));
            AudioListener.volume = masterVolume;
        }

        private void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(Mathf.Round(volume * 5f) / 5f);
            AudioListener.volume = masterVolume;
            PlayerPrefs.SetFloat(MasterVolumePrefKey, masterVolume);
            PlayerPrefs.Save();
            RefreshSettingsLabels();
        }

        private void HandleVolumeDownPressed()
        {
            SetMasterVolume(masterVolume - 0.2f);
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.45f);
        }

        private void HandleVolumeUpPressed()
        {
            SetMasterVolume(masterVolume + 0.2f);
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.45f);
        }

        private void HandleHintPressed()
        {
            CancelStartupWelcomeSequence(restoreObjectives: true);
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.45f);
            lessonManager?.RequestHintNow();
        }

        private void HandleAskNpcPressed()
        {
            if (glossaryTopics == null || selectedTopicIndex < 0 || selectedTopicIndex >= glossaryTopics.Length)
            {
                return;
            }

            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.45f);
            dialogueSystem?.AskAboutTopic(glossaryTopics[selectedTopicIndex]);
        }

        private void HandleLocomotionToggled()
        {
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.45f);
            locomotionController?.ToggleContinuousMove();
            RefreshSettingsLabels();
        }

        private void HandlePostureToggled()
        {
            IsSeatedMode = !IsSeatedMode;
            locomotionController?.SetSeatedMode(IsSeatedMode);
            RefreshSettingsLabels();
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.45f);
        }

        private void HandleTurnModeToggled()
        {
            if (locomotionController != null)
            {
                locomotionController.SetSmoothTurnEnabled(!locomotionController.IsSmoothTurnEnabled);
            }

            RefreshSettingsLabels();
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.45f);
        }

        private void HandleNpcVoiceToggled()
        {
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.45f);
            narratorAudio?.ToggleMuted();
            RefreshSettingsLabels();
        }

        private void HandleNpcVoiceMuteChanged(bool muted)
        {
            RefreshSettingsLabels();
        }

        private void HandleLessonStepChanged(LessonStep step, int index, int total)
        {
            RefreshStepList();
        }

        private void HandleLessonProgressChanged(int completed, int total)
        {
            RefreshStepList();
        }

        private void HandleActiveStationChanged(StationController station)
        {
            RefreshStepList();
        }

        private void HandleContinuousMoveChanged(bool enabled)
        {
            RefreshSettingsLabels();
        }

        private void HandleLabCompleted()
        {
            CancelStartupWelcomeSequence(restoreObjectives: false);
            showingStartupWelcome = false;
            showingSimulationComplete = true;
            ActiveTab = TabKind.Objective;
            SummonTabletInternal(false);
        }

        private IEnumerator StartupWelcomeSequenceRoutine()
        {
            // Wait briefly so XR Origin / Main Camera tracking pose and LessonManager.Start() settle.
            yield return null;
            yield return new WaitForSecondsRealtime(0.15f);

            showingStartupWelcome = true;
            startupAutoDismissArmed = true;
            ActiveTab = TabKind.Objective;
            progressHud?.SetStartupWelcome(true, startupWelcomeTitle, startupWelcomeSubtitle);
            SummonTabletInternal(false);

            yield return new WaitForSecondsRealtime(Mathf.Max(0.5f, startupWelcomeSeconds));

            showingStartupWelcome = false;
            progressHud?.SetStartupWelcome(false, null, null);
            RefreshStepList();

            if (startupObjectivePreviewSeconds > 0f && startupAutoDismissArmed)
            {
                yield return new WaitForSecondsRealtime(startupObjectivePreviewSeconds);
            }

            if (startupAutoDismissArmed && IsVisible)
            {
                DismissTabletInternal(false);
            }

            startupAutoDismissArmed = false;
            startupWelcomeCoroutine = null;
        }

        private void CancelStartupWelcomeSequence(bool restoreObjectives)
        {
            if (startupWelcomeCoroutine != null)
            {
                StopCoroutine(startupWelcomeCoroutine);
                startupWelcomeCoroutine = null;
            }

            startupAutoDismissArmed = false;
            if (showingStartupWelcome && restoreObjectives)
            {
                showingStartupWelcome = false;
                progressHud?.SetStartupWelcome(false, null, null);
                RefreshStepList();
            }
        }

        private void RefreshStepList()
        {
            if (stepListText == null || lessonManager == null || lessonManager.Steps == null)
            {
                return;
            }

            if (showingStartupWelcome)
            {
                progressHud?.SetStartupWelcome(true, startupWelcomeTitle, startupWelcomeSubtitle);
                stepListText.text =
                    "<color=#FFE07D><b>WELCOME TO THE CLEANROOM LAB</b></color>\n" +
                    "<color=#D0DCE8>Continuing with your active station objectives in a moment...</color>\n" +
                    "<color=#B8C6D4>Press <b>Y</b> (Quest Left) or <b>F</b> (Desktop) anytime to summon/dismiss your tablet.</color>";
                return;
            }

            if (showingSimulationComplete)
            {
                progressHud?.RefreshAll();
                stepListText.text =
                    "<color=#5CFFA0><b>SIMULATION COMPLETE!</b></color>\n" +
                    "<color=#D0DCE8>You completed all 7 lab stations and the 10-question Theory Assessment.</color>\n\n" +
                    "<color=#FFE07D>Open the <b>Menu</b> tab (or press Left <b>X</b>) to Resume, Restart, or Exit Simulation.</color>";
                return;
            }

            var list = lessonManager.Steps;
            int current = lessonManager.CurrentStepIndex;
            bool labComplete = current >= list.Count;

            string activeStationId = lessonManager.ActiveStation != null
                ? lessonManager.ActiveStation.StationId
                : (lessonManager.CurrentStep != null ? lessonManager.CurrentStep.StationId : "A");
            string activeStationTitle = lessonManager.ActiveStation != null
                ? lessonManager.ActiveStation.StationTitle
                : "Lab Workbench";

            int stationTotal = 0;
            int stationDone = 0;
            for (int i = 0; i < list.Count; i++)
            {
                LessonStep s = list[i];
                if (s != null && string.Equals(s.StationId, activeStationId, StringComparison.OrdinalIgnoreCase))
                {
                    stationTotal++;
                    if (labComplete || i < current)
                    {
                        stationDone++;
                    }
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.Append("<color=#FFE07D><b>STATION ")
              .Append(activeStationId)
              .Append(": ")
              .Append(activeStationTitle.ToUpperInvariant())
              .Append(" (")
              .Append(stationDone)
              .Append("/")
              .Append(stationTotal)
              .Append(")</b></color>\n");

            for (int i = 0; i < list.Count; i++)
            {
                LessonStep s = list[i];
                if (s == null || !string.Equals(s.StationId, activeStationId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (labComplete || i < current)
                {
                    sb.Append("<color=#5CFFA0>[OK] ").Append(s.Title).Append("</color>\n");
                }
                else if (i == current)
                {
                    sb.Append("<color=#38D8FF><b>> ").Append(s.Title).Append("</b></color>\n");
                }
                else
                {
                    sb.Append("<color=#B8C6D4>[ ] ").Append(s.Title).Append("</color>\n");
                }
            }

            stepListText.text = sb.ToString();
            progressHud?.RefreshAll();
        }

        private void PopulateGlossaryButtons()
        {
            if (glossaryButtons == null)
            {
                return;
            }

            for (int i = 0; i < glossaryButtons.Length; i++)
            {
                bool active = glossaryTopics != null && i < glossaryTopics.Length && glossaryTopics[i] != null;
                if (glossaryButtons[i] != null)
                {
                    glossaryButtons[i].gameObject.SetActive(active);
                }

                if (active && glossaryButtonLabels != null && i < glossaryButtonLabels.Length && glossaryButtonLabels[i] != null)
                {
                    glossaryButtonLabels[i].text = glossaryTopics[i].Title;
                }
            }
        }

        private void PopulateControlsText()
        {
            if (controlsBodyText == null)
            {
                return;
            }

            controlsBodyText.text =
                "<b>META QUEST 3 CONTROLLER MAP</b>\n" +
                "- <b>Walk / Strafe:</b> Left Thumbstick\n" +
                "- <b>Teleport / Snap Turn:</b> Right Stick Up (Teleport) | Right Stick Left/Right (Turn)\n" +
                "- <b>Grab & Spin / Pull:</b> Grip (Grab), then Holding Stick spins & pulls item\n" +
                "- <b>Buttons / UI / Sliders:</b> Trigger or Direct Index Poke\n" +
                "- <b>Tablet / Menu:</b> Left <b>Y</b> (Tablet) | Left <b>X</b> or <b>Esc</b> (Resume/Restart/Exit)";
        }

        private void RefreshSettingsLabels()
        {
            if (volumeLabel != null)
            {
                volumeLabel.text = "Volume: " + Mathf.RoundToInt(masterVolume * 100f) + "%";
            }

            if (textSizeLabel != null)
            {
                textSizeLabel.text = "Text Size: " + TextSizeNames[TextSizeIndex];
            }

            if (locomotionLabel != null && locomotionController != null)
            {
                locomotionLabel.text = locomotionController.IsContinuousMoveEnabled
                    ? "Locomotion: Continuous + Vignette"
                    : "Locomotion: Teleport (Default)";
            }

            if (postureLabel != null)
            {
                postureLabel.text = IsSeatedMode ? "Posture: Seated" : "Posture: Standing";
            }

            if (snapTurnLabel != null && locomotionController != null)
            {
                snapTurnLabel.text = locomotionController.IsSmoothTurnEnabled
                    ? "Turn: Smooth"
                    : "Turn: Snap (Default)";
            }

            bool voiceMuted = narratorAudio != null && narratorAudio.IsMuted;
            if (npcVoiceLabel != null)
            {
                npcVoiceLabel.text = voiceMuted
                    ? "NPC Voice Narration: MUTED"
                    : "NPC Voice Narration: ON";
            }

            if (npcVoiceButtonBackground != null)
            {
                npcVoiceButtonBackground.color = voiceMuted ? VoiceMutedButtonColor : VoiceOnButtonColor;
            }
        }

        private void CacheBaseFontSizes()
        {
            if (scalableTexts == null)
            {
                return;
            }

            int n = scalableTexts.Length;
            baseFontSizes = new float[n];
            baseFontMinSizes = new float[n];
            baseFontMaxSizes = new float[n];
            wasAutoSizing = new bool[n];
            for (int i = 0; i < n; i++)
            {
                TMP_Text t = scalableTexts[i];
                if (t != null)
                {
                    baseFontSizes[i] = t.fontSize;
                    baseFontMinSizes[i] = t.fontSizeMin;
                    baseFontMaxSizes[i] = t.fontSizeMax;
                    wasAutoSizing[i] = t.enableAutoSizing;
                }
                else
                {
                    baseFontSizes[i] = 20f;
                    baseFontMinSizes[i] = 16f;
                    baseFontMaxSizes[i] = 24f;
                }
            }
        }

        private void ApplyTextScale(float scale)
        {
            if (scalableTexts != null && baseFontSizes != null)
            {
                for (int i = 0; i < scalableTexts.Length; i++)
                {
                    TMP_Text t = scalableTexts[i];
                    if (t == null)
                    {
                        continue;
                    }

                    t.fontSize = baseFontSizes[i] * scale;
                    if (wasAutoSizing != null && i < wasAutoSizing.Length && wasAutoSizing[i])
                    {
                        t.fontSizeMin = baseFontMinSizes[i] * scale;
                        t.fontSizeMax = baseFontMaxSizes[i] * scale;
                    }
                }
            }

            speechBubble?.SetTextScale(scale);
        }
    }
}
