using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace PNJunctionLab
{
    /// <summary>
    /// Self-contained StationController for Station 4: Junction Formation.
    /// Supports socketing carried-over (Stations 2 & 3) or spare P-Type & N-Type Silicon blocks,
    /// pressing Join to launch the 5-stage time-lapse simulation (t=0 -> Diffusion -> Recombination
    /// -> Depletion & E-Field/Drift -> Equilibrium), Play/Pause/Step/Speed controls, asymmetric NA & ND
    /// sliders (10^14..10^18 cm^-3) reading strictly from SemiconductorModel, 5 layer toggles, and 4 live graphs
    /// plus Energy Band-Bending Diagram.
    /// </summary>
    public class JunctionStationController : StationController
    {
        private static readonly float[] SpeedOptions = { 0.5f, 1.0f, 2.0f };
        private static readonly float[] StageStops = { 0.0f, 0.25f, 0.52f, 0.78f, 1.0f };

        [Header("Navigation & Physics Defaults")]
        [SerializeField] private TeleportationAnchor stationAnchor;
        [SerializeField] private SemiconductorMaterial material = SemiconductorMaterial.Silicon;
        [SerializeField] private double temperatureK = 300.0;
        [SerializeField] private double defaultExponentP = 16.0;
        [SerializeField] private double defaultExponentN = 16.0;
        [SerializeField] private float baseFormationDurationSeconds = 9.0f;
        [SerializeField] private bool startUnlockedForStandalone;

        [Header("Junction Jig & Blocks")]
        [SerializeField] private XRSocketInteractor pSideSocket;
        [SerializeField] private SocketRingFeedback pSocketFeedback;
        [SerializeField] private XRSocketInteractor nSideSocket;
        [SerializeField] private SocketRingFeedback nSocketFeedback;
        [SerializeField] private RespawnableItem sparePBlockRespawn;
        [SerializeField] private RespawnableItem spareNBlockRespawn;
        [SerializeField] private PhysicalPushButton joinButton;
        [SerializeField] private RespawnZone respawnZone;
        [SerializeField] private AudioSource audioSource;

        [Header("Playback Controls")]
        [SerializeField] private PhysicalPushButton playPauseButton;
        [SerializeField] private TMP_Text playPauseButtonLabel;
        [SerializeField] private PhysicalPushButton stepButton;
        [SerializeField] private PhysicalPushButton speedButton;
        [SerializeField] private TMP_Text speedButtonLabel;
        [SerializeField] private PhysicalPushButton resetStationButton;
        [SerializeField] private PhysicalPushButton returnPropsButton;

        [Header("Asymmetric Doping Controls (NA & ND)")]
        [SerializeField] private Slider naExponentSlider;
        [SerializeField] private Slider ndExponentSlider;
        [SerializeField] private PhysicalPushButton naDownButton;
        [SerializeField] private PhysicalPushButton naUpButton;
        [SerializeField] private PhysicalPushButton ndDownButton;
        [SerializeField] private PhysicalPushButton ndUpButton;

        [Header("Layer Toggle Buttons")]
        [SerializeField] private PhysicalPushButton toggleHolesButton;
        [SerializeField] private TMP_Text toggleHolesLabel;
        [SerializeField] private PhysicalPushButton toggleElectronsButton;
        [SerializeField] private TMP_Text toggleElectronsLabel;
        [SerializeField] private PhysicalPushButton toggleIonsButton;
        [SerializeField] private TMP_Text toggleIonsLabel;
        [SerializeField] private PhysicalPushButton toggleFieldButton;
        [SerializeField] private TMP_Text toggleFieldLabel;
        [SerializeField] private PhysicalPushButton toggleCurrentsButton;
        [SerializeField] private TMP_Text toggleCurrentsLabel;

        [Header("Visualizers & Readouts")]
        [SerializeField] private JunctionSimulationView simulationView;
        [SerializeField] private JunctionGraphsAndBandsView graphsAndBandsView;
        [SerializeField] private TMP_Text jigStatusText;
        [SerializeField] private TMP_Text electrostaticsReadoutText;
        [SerializeField] private TMP_Text asymmetricSummaryText;
        [SerializeField] private TMP_Text naValueText;
        [SerializeField] private TMP_Text ndValueText;

        public bool HasPSideBlock => socketedPBlock != null;
        public bool HasNSideBlock => socketedNBlock != null;
        public bool IsJoined { get; private set; }
        public bool IsPlaying { get; private set; }
        public float FormationProgress01 { get; private set; }
        public int CurrentStageIndex { get; private set; }
        public int SpeedIndex { get; private set; } = 1;
        public double CurrentExponentNA { get; private set; } = 16.0;
        public double CurrentExponentND { get; private set; } = 16.0;
        public double CurrentNaCm3 => Math.Pow(10.0, CurrentExponentNA);
        public double CurrentNdCm3 => Math.Pow(10.0, CurrentExponentND);
        public JunctionElectrostatics CurrentElectrostatics { get; private set; }

        public bool ShowHoles { get; private set; } = true;
        public bool ShowElectrons { get; private set; } = true;
        public bool ShowIons { get; private set; } = true;
        public bool ShowEField { get; private set; } = true;
        public bool ShowCurrents { get; private set; } = true;

        private ExtrinsicSemiconductorBlock socketedPBlock;
        private ExtrinsicSemiconductorBlock socketedNBlock;
        private bool reportedTeleport;
        private bool reportedJoin;
        private bool reportedEquilibrium;
        private bool reportedAsymmetric;

        protected override void Awake()
        {
            base.Awake();
            CurrentExponentNA = ClampDouble(defaultExponentP, 14.0, 18.0);
            CurrentExponentND = ClampDouble(defaultExponentN, 14.0, 18.0);

            if (naExponentSlider != null)
            {
                naExponentSlider.minValue = 14f;
                naExponentSlider.maxValue = 18f;
                naExponentSlider.SetValueWithoutNotify((float)CurrentExponentNA);
            }

            if (ndExponentSlider != null)
            {
                ndExponentSlider.minValue = 14f;
                ndExponentSlider.maxValue = 18f;
                ndExponentSlider.SetValueWithoutNotify((float)CurrentExponentND);
            }

            if (pSocketFeedback != null)
            {
                pSocketFeedback.CustomValidityPredicate = IsSelectablePBlock;
            }

            if (nSocketFeedback != null)
            {
                nSocketFeedback.CustomValidityPredicate = IsSelectableNBlock;
            }

            RefreshAllViews();
            SetUnlocked(startUnlockedForStandalone);
        }

        private void OnEnable()
        {
            if (stationAnchor != null) stationAnchor.teleporting.AddListener(HandleTeleporting);
            if (pSideSocket != null)
            {
                pSideSocket.selectEntered.AddListener(HandlePSocketEntered);
                pSideSocket.selectExited.AddListener(HandlePSocketExited);
            }

            if (nSideSocket != null)
            {
                nSideSocket.selectEntered.AddListener(HandleNSocketEntered);
                nSideSocket.selectExited.AddListener(HandleNSocketExited);
            }

            if (joinButton != null) joinButton.Pressed += PressJoin;
            if (playPauseButton != null) playPauseButton.Pressed += TogglePlayPause;
            if (stepButton != null) stepButton.Pressed += StepFormationStage;
            if (speedButton != null) speedButton.Pressed += CyclePlaybackSpeed;
            if (resetStationButton != null) resetStationButton.Pressed += ResetStation;
            if (returnPropsButton != null) returnPropsButton.Pressed += ReturnAllBlocksHome;

            if (naExponentSlider != null) naExponentSlider.onValueChanged.AddListener(HandleNaSliderChanged);
            if (ndExponentSlider != null) ndExponentSlider.onValueChanged.AddListener(HandleNdSliderChanged);
            if (naDownButton != null) naDownButton.Pressed += HandleNaDown;
            if (naUpButton != null) naUpButton.Pressed += HandleNaUp;
            if (ndDownButton != null) ndDownButton.Pressed += HandleNdDown;
            if (ndUpButton != null) ndUpButton.Pressed += HandleNdUp;

            if (toggleHolesButton != null) toggleHolesButton.Pressed += ToggleHolesLayer;
            if (toggleElectronsButton != null) toggleElectronsButton.Pressed += ToggleElectronsLayer;
            if (toggleIonsButton != null) toggleIonsButton.Pressed += ToggleIonsLayer;
            if (toggleFieldButton != null) toggleFieldButton.Pressed += ToggleEFieldLayer;
            if (toggleCurrentsButton != null) toggleCurrentsButton.Pressed += ToggleCurrentsLayer;
        }

        private void OnDisable()
        {
            if (stationAnchor != null) stationAnchor.teleporting.RemoveListener(HandleTeleporting);
            if (pSideSocket != null)
            {
                pSideSocket.selectEntered.RemoveListener(HandlePSocketEntered);
                pSideSocket.selectExited.RemoveListener(HandlePSocketExited);
            }

            if (nSideSocket != null)
            {
                nSideSocket.selectEntered.RemoveListener(HandleNSocketEntered);
                nSideSocket.selectExited.RemoveListener(HandleNSocketExited);
            }

            if (joinButton != null) joinButton.Pressed -= PressJoin;
            if (playPauseButton != null) playPauseButton.Pressed -= TogglePlayPause;
            if (stepButton != null) stepButton.Pressed -= StepFormationStage;
            if (speedButton != null) speedButton.Pressed -= CyclePlaybackSpeed;
            if (resetStationButton != null) resetStationButton.Pressed -= ResetStation;
            if (returnPropsButton != null) returnPropsButton.Pressed -= ReturnAllBlocksHome;

            if (naExponentSlider != null) naExponentSlider.onValueChanged.RemoveListener(HandleNaSliderChanged);
            if (ndExponentSlider != null) ndExponentSlider.onValueChanged.RemoveListener(HandleNdSliderChanged);
            if (naDownButton != null) naDownButton.Pressed -= HandleNaDown;
            if (naUpButton != null) naUpButton.Pressed -= HandleNaUp;
            if (ndDownButton != null) ndDownButton.Pressed -= HandleNdDown;
            if (ndUpButton != null) ndUpButton.Pressed -= HandleNdUp;

            if (toggleHolesButton != null) toggleHolesButton.Pressed -= ToggleHolesLayer;
            if (toggleElectronsButton != null) toggleElectronsButton.Pressed -= ToggleElectronsLayer;
            if (toggleIonsButton != null) toggleIonsButton.Pressed -= ToggleIonsLayer;
            if (toggleFieldButton != null) toggleFieldButton.Pressed -= ToggleEFieldLayer;
            if (toggleCurrentsButton != null) toggleCurrentsButton.Pressed -= ToggleCurrentsLayer;
        }

        private void Update()
        {
            if (!IsUnlocked || !IsJoined || !IsPlaying)
            {
                return;
            }

            float speed = SpeedOptions[Mathf.Clamp(SpeedIndex, 0, SpeedOptions.Length - 1)];
            float next = FormationProgress01 + (Time.deltaTime * speed) / Mathf.Max(1f, baseFormationDurationSeconds);
            if (next >= 1.0f)
            {
                SetFormationProgress(1.0f, true);
                IsPlaying = false;
                RefreshPlaybackLabels();
            }
            else
            {
                SetFormationProgress(next, true);
            }
        }

        public override void SetUnlocked(bool unlocked)
        {
            base.SetUnlocked(unlocked);

            if (pSideSocket != null) pSideSocket.enabled = unlocked;
            if (nSideSocket != null) nSideSocket.enabled = unlocked;
            if (joinButton != null) joinButton.SetButtonEnabled(unlocked);
            if (playPauseButton != null) playPauseButton.SetButtonEnabled(unlocked);
            if (stepButton != null) stepButton.SetButtonEnabled(unlocked);
            if (speedButton != null) speedButton.SetButtonEnabled(unlocked);
            if (resetStationButton != null) resetStationButton.SetButtonEnabled(unlocked);
            if (returnPropsButton != null) returnPropsButton.SetButtonEnabled(unlocked);

            if (naExponentSlider != null) naExponentSlider.interactable = unlocked && IsJoined;
            if (ndExponentSlider != null) ndExponentSlider.interactable = unlocked && IsJoined;
            if (naDownButton != null) naDownButton.SetButtonEnabled(unlocked);
            if (naUpButton != null) naUpButton.SetButtonEnabled(unlocked);
            if (ndDownButton != null) ndDownButton.SetButtonEnabled(unlocked);
            if (ndUpButton != null) ndUpButton.SetButtonEnabled(unlocked);

            if (toggleHolesButton != null) toggleHolesButton.SetButtonEnabled(unlocked);
            if (toggleElectronsButton != null) toggleElectronsButton.SetButtonEnabled(unlocked);
            if (toggleIonsButton != null) toggleIonsButton.SetButtonEnabled(unlocked);
            if (toggleFieldButton != null) toggleFieldButton.SetButtonEnabled(unlocked);
            if (toggleCurrentsButton != null) toggleCurrentsButton.SetButtonEnabled(unlocked);

            SetBlockGrabEnabled(sparePBlockRespawn, unlocked);
            SetBlockGrabEnabled(spareNBlockRespawn, unlocked);
        }

        public override void ResetStation()
        {
            base.ResetStation();

            StopAllCoroutines();
            socketedPBlock = null;
            socketedNBlock = null;
            IsJoined = false;
            IsPlaying = false;
            FormationProgress01 = 0f;
            CurrentStageIndex = 0;
            SpeedIndex = 1;
            reportedJoin = false;
            reportedEquilibrium = false;
            reportedAsymmetric = false;

            CurrentExponentNA = ClampDouble(defaultExponentP, 14.0, 18.0);
            CurrentExponentND = ClampDouble(defaultExponentN, 14.0, 18.0);
            if (naExponentSlider != null)
            {
                naExponentSlider.SetValueWithoutNotify((float)CurrentExponentNA);
                naExponentSlider.interactable = false;
            }

            if (ndExponentSlider != null)
            {
                ndExponentSlider.SetValueWithoutNotify((float)CurrentExponentND);
                ndExponentSlider.interactable = false;
            }

            ShowHoles = true;
            ShowElectrons = true;
            ShowIons = true;
            ShowEField = true;
            ShowCurrents = true;

            ReturnAllBlocksHome();
            RefreshAllViews();
        }

        /// <summary>
        /// Programmatic / UI test helper to dock P and N blocks and optionally trigger Join directly.
        /// </summary>
        public bool TrySocketBlocks(ExtrinsicSemiconductorBlock pBlock, ExtrinsicSemiconductorBlock nBlock)
        {
            if (!IsUnlocked || pBlock == null || nBlock == null)
            {
                return false;
            }

            if (pBlock.DopingKind != ExtrinsicDopingKind.PType)
            {
                RejectWrongSocketBlock(pSideSocket, pSocketFeedback, pBlock, "Left bay expects a P-Type Silicon block!");
                return false;
            }

            if (nBlock.DopingKind != ExtrinsicDopingKind.NType)
            {
                RejectWrongSocketBlock(nSideSocket, nSocketFeedback, nBlock, "Right bay expects an N-Type Silicon block!");
                return false;
            }

            socketedPBlock = pBlock;
            socketedNBlock = nBlock;
            AdoptBlockDoping(pBlock, true);
            AdoptBlockDoping(nBlock, false);
            RefreshAllViews();
            return true;
        }

        /// <summary>
        /// Forms the P-N metallurgical junction once both blocks are socketed and starts the time-lapse animation.
        /// </summary>
        public void PressJoin()
        {
            if (!IsUnlocked)
            {
                return;
            }

            if (!HasPSideBlock || !HasNSideBlock)
            {
                ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.SocketInvalid, 0.55f);
                NotifyWrongAction("station4.missing_block",
                    "Place both a P-Type block (left bay) and an N-Type block (right bay) into the Junction Jig before pressing JOIN!");
                return;
            }

            IsJoined = true;
            IsPlaying = true;
            SetFormationProgress(0.02f, false);

            if (naExponentSlider != null) naExponentSlider.interactable = true;
            if (ndExponentSlider != null) ndExponentSlider.interactable = true;

            RefreshAllViews();

            if (!reportedJoin)
            {
                reportedJoin = true;
                ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Success, 0.72f);
                NotifyStepAction("station4.join");
            }
        }

        public void TogglePlayPause()
        {
            if (!IsUnlocked || !IsJoined)
            {
                return;
            }

            if (FormationProgress01 >= 0.999f && !IsPlaying)
            {
                FormationProgress01 = 0f;
            }

            IsPlaying = !IsPlaying;
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.45f);
            RefreshAllViews();
        }

        public void StepFormationStage()
        {
            if (!IsUnlocked || !IsJoined)
            {
                return;
            }

            IsPlaying = false;
            int nextStage = (CurrentStageIndex + 1) % StageStops.Length;
            SetFormationProgress(StageStops[nextStage], true);
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.50f);
            RefreshPlaybackLabels();
        }

        public void CyclePlaybackSpeed()
        {
            if (!IsUnlocked)
            {
                return;
            }

            SpeedIndex = (SpeedIndex + 1) % SpeedOptions.Length;
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.45f);
            RefreshPlaybackLabels();
        }

        public void SetDopingExponents(double expNa, double expNd, bool notifyLesson = true)
        {
            if (!IsUnlocked)
            {
                return;
            }

            CurrentExponentNA = ClampDouble(expNa, 14.0, 18.0);
            CurrentExponentND = ClampDouble(expNd, 14.0, 18.0);

            if (naExponentSlider != null) naExponentSlider.SetValueWithoutNotify((float)CurrentExponentNA);
            if (ndExponentSlider != null) ndExponentSlider.SetValueWithoutNotify((float)CurrentExponentND);

            RefreshAllViews();

            if (notifyLesson && IsJoined && !reportedAsymmetric && Math.Abs(CurrentExponentNA - CurrentExponentND) >= 0.45)
            {
                reportedAsymmetric = true;
                ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Success, 0.60f);
                NotifyStepAction("station4.asymmetric");
            }
        }

        public void ToggleHolesLayer() { ShowHoles = !ShowHoles; ApplyLayerAndRefresh(); }
        public void ToggleElectronsLayer() { ShowElectrons = !ShowElectrons; ApplyLayerAndRefresh(); }
        public void ToggleIonsLayer() { ShowIons = !ShowIons; ApplyLayerAndRefresh(); }
        public void ToggleEFieldLayer() { ShowEField = !ShowEField; ApplyLayerAndRefresh(); }
        public void ToggleCurrentsLayer() { ShowCurrents = !ShowCurrents; ApplyLayerAndRefresh(); }

        private void ApplyLayerAndRefresh()
        {
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.40f);
            simulationView?.SetLayerVisibility(ShowHoles, ShowElectrons, ShowIons, ShowEField, ShowCurrents);
            RefreshLayerToggleLabels();
        }

        private void SetFormationProgress(float progress01, bool checkEquilibriumStep)
        {
            FormationProgress01 = Mathf.Clamp01(progress01);
            CurrentStageIndex = ComputeStageIndex(FormationProgress01);
            RefreshAllViews();

            if (checkEquilibriumStep && IsJoined && !reportedEquilibrium && FormationProgress01 >= 0.96f)
            {
                reportedEquilibrium = true;
                ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Success, 0.68f);
                NotifyStepAction("station4.equilibrium");
            }
        }

        private void HandleTeleporting(TeleportingEventArgs args)
        {
            if (!IsUnlocked || reportedTeleport)
            {
                return;
            }

            reportedTeleport = true;
            NotifyStepAction("station4.teleport");
        }

        private void HandlePSocketEntered(SelectEnterEventArgs args)
        {
            ExtrinsicSemiconductorBlock block = args.interactableObject?.transform.GetComponentInParent<ExtrinsicSemiconductorBlock>();
            if (block == null || block.DopingKind != ExtrinsicDopingKind.PType)
            {
                RejectWrongSocketBlock(pSideSocket, pSocketFeedback, block,
                    "Left bay is for P-Type Silicon (B/Al/Ga/In doped)! Put N-Type Silicon into the right N-Type bay.");
                return;
            }

            socketedPBlock = block;
            AdoptBlockDoping(block, true);
            RefreshAllViews();
        }

        private void HandlePSocketExited(SelectExitEventArgs args)
        {
            socketedPBlock = null;
            RefreshAllViews();
        }

        private void HandleNSocketEntered(SelectEnterEventArgs args)
        {
            ExtrinsicSemiconductorBlock block = args.interactableObject?.transform.GetComponentInParent<ExtrinsicSemiconductorBlock>();
            if (block == null || block.DopingKind != ExtrinsicDopingKind.NType)
            {
                RejectWrongSocketBlock(nSideSocket, nSocketFeedback, block,
                    "Right bay is for N-Type Silicon (P/As/Sb doped)! Put P-Type Silicon into the left P-Type bay.");
                return;
            }

            socketedNBlock = block;
            AdoptBlockDoping(block, false);
            RefreshAllViews();
        }

        private void HandleNSocketExited(SelectExitEventArgs args)
        {
            socketedNBlock = null;
            RefreshAllViews();
        }

        private void AdoptBlockDoping(ExtrinsicSemiconductorBlock block, bool isPSide)
        {
            if (block == null || block.DopingConcentrationCm3 <= 0.0) return;
            double exp = ClampDouble(Math.Log10(block.DopingConcentrationCm3), 14.0, 18.0);
            if (isPSide) CurrentExponentNA = exp;
            else CurrentExponentND = exp;

            if (naExponentSlider != null) naExponentSlider.SetValueWithoutNotify((float)CurrentExponentNA);
            if (ndExponentSlider != null) ndExponentSlider.SetValueWithoutNotify((float)CurrentExponentND);
        }

        private void RejectWrongSocketBlock(XRSocketInteractor socket, SocketRingFeedback fb, ExtrinsicSemiconductorBlock wrongBlock, string msg)
        {
            fb?.FlashInvalid(0.85f);
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.SocketInvalid, 0.72f);
            NotifyWrongAction("station4.wrong_block", msg);
            if (wrongBlock != null)
            {
                StartCoroutine(ReturnWrongBlockRoutine(wrongBlock));
            }
        }

        private IEnumerator ReturnWrongBlockRoutine(ExtrinsicSemiconductorBlock block)
        {
            yield return new WaitForSeconds(0.50f);
            if (block != null)
            {
                RespawnableItem rp = block.GetComponent<RespawnableItem>();
                if (rp != null) rp.Respawn();
            }

            RefreshAllViews();
        }

        private void ReturnAllBlocksHome()
        {
            if (sparePBlockRespawn != null) sparePBlockRespawn.Respawn();
            if (spareNBlockRespawn != null) spareNBlockRespawn.Respawn();
            if (socketedPBlock != null)
            {
                RespawnableItem rp = socketedPBlock.GetComponent<RespawnableItem>();
                if (rp != null && rp != sparePBlockRespawn) rp.Respawn();
            }

            if (socketedNBlock != null)
            {
                RespawnableItem rp = socketedNBlock.GetComponent<RespawnableItem>();
                if (rp != null && rp != spareNBlockRespawn) rp.Respawn();
            }
        }

        private void HandleNaSliderChanged(float v) => SetDopingExponents(v, CurrentExponentND, true);
        private void HandleNdSliderChanged(float v) => SetDopingExponents(CurrentExponentNA, v, true);
        private void HandleNaDown() => SetDopingExponents(CurrentExponentNA - 0.5, CurrentExponentND, true);
        private void HandleNaUp() => SetDopingExponents(CurrentExponentNA + 0.5, CurrentExponentND, true);
        private void HandleNdDown() => SetDopingExponents(CurrentExponentNA, CurrentExponentND - 0.5, true);
        private void HandleNdUp() => SetDopingExponents(CurrentExponentNA, CurrentExponentND + 0.5, true);

        private void RefreshAllViews()
        {
            CurrentElectrostatics = SemiconductorModel.ComputeJunctionElectrostatics(
                material, CurrentNaCm3, CurrentNdCm3, 0.0, temperatureK);

            if (simulationView != null)
            {
                simulationView.SetSimulationActive(IsJoined);
                simulationView.SetLayerVisibility(ShowHoles, ShowElectrons, ShowIons, ShowEField, ShowCurrents);
                simulationView.UpdateSimulationState(
                    CurrentNaCm3, CurrentNdCm3, CurrentElectrostatics, FormationProgress01, CurrentStageIndex);
            }

            if (graphsAndBandsView != null)
            {
                graphsAndBandsView.RefreshFromModel(
                    material, CurrentNaCm3, CurrentNdCm3, 0.0, temperatureK,
                    IsJoined ? FormationProgress01 : 0f, CurrentElectrostatics);
            }

            RefreshPlaybackLabels();
            RefreshLayerToggleLabels();
            RefreshReadoutTexts();
        }

        private void RefreshPlaybackLabels()
        {
            if (playPauseButtonLabel != null)
            {
                playPauseButtonLabel.text = IsPlaying ? "PAUSE" : (FormationProgress01 >= 0.99f ? "REPLAY" : "PLAY");
            }

            if (speedButtonLabel != null)
            {
                speedButtonLabel.text = $"SPEED {SpeedOptions[Mathf.Clamp(SpeedIndex, 0, SpeedOptions.Length - 1)]:0.#}x";
            }
        }

        private void RefreshLayerToggleLabels()
        {
            SetToggleText(toggleHolesLabel, "HOLES", ShowHoles);
            SetToggleText(toggleElectronsLabel, "e-", ShowElectrons);
            SetToggleText(toggleIonsLabel, "IONS", ShowIons);
            SetToggleText(toggleFieldLabel, "E-FIELD", ShowEField);
            SetToggleText(toggleCurrentsLabel, "CURR", ShowCurrents);
        }

        private static void SetToggleText(TMP_Text label, string prefix, bool active)
        {
            if (label != null)
            {
                label.text = $"{prefix}\n{(active ? "[ON]" : "[OFF]")}";
                label.color = active ? Color.white : new Color(0.62f, 0.66f, 0.72f, 1f);
            }
        }

        private void RefreshReadoutTexts()
        {
            if (jigStatusText != null)
            {
                if (!IsJoined)
                {
                    string pTxt = HasPSideBlock ? "P-Block READY" : "Need P-Block (Left)";
                    string nTxt = HasNSideBlock ? "N-Block READY" : "Need N-Block (Right)";
                    jigStatusText.text = $"JUNCTION JIG: {pTxt} | {nTxt} -> Press JOIN";
                }
                else
                {
                    jigStatusText.text = $"FORMED | Stage {CurrentStageIndex + 1}/5 ({FormationProgress01 * 100f:F0}%)";
                }
            }

            if (naValueText != null) naValueText.text = $"P NA = {CurrentNaCm3:0.0E+0}";
            if (ndValueText != null) ndValueText.text = $"N ND = {CurrentNdCm3:0.0E+0}";

            if (electrostaticsReadoutText != null)
            {
                electrostaticsReadoutText.text =
                    $"V0 = {CurrentElectrostatics.BuiltInPotentialV:F3} V | Total W = {CurrentElectrostatics.DepletionWidthMicrometers:F3} um\n" +
                    $"xp (P) = {CurrentElectrostatics.PWidthMicrometers:F3} um | xn (N) = {CurrentElectrostatics.NWidthMicrometers:F3} um | |Emax| = {CurrentElectrostatics.PeakElectricFieldVPerCm * 1e-3:F1} kV/cm";
            }

            if (asymmetricSummaryText != null)
            {
                double ratio = CurrentNaCm3 / Math.Max(1.0, CurrentNdCm3);
                if (ratio > 1.25)
                {
                    asymmetricSummaryText.text = $"NA > ND: Lighter N-side depletes deeper (xn / xp = {ratio:F1}x)";
                }
                else if (ratio < 0.80)
                {
                    asymmetricSummaryText.text = $"ND > NA: Lighter P-side depletes deeper (xp / xn = {1.0 / ratio:F1}x)";
                }
                else
                {
                    asymmetricSummaryText.text = "Symmetric Doping (NA ~ ND): xp ~ xn equally across junction";
                }
            }
        }

        private static int ComputeStageIndex(float f)
        {
            if (f < 0.12f) return 0;
            if (f < 0.38f) return 1;
            if (f < 0.65f) return 2;
            if (f < 0.96f) return 3;
            return 4;
        }

        private static bool IsSelectablePBlock(IXRSelectInteractable selectable)
        {
            if (selectable == null) return false;
            ExtrinsicSemiconductorBlock blk = selectable.transform.GetComponentInParent<ExtrinsicSemiconductorBlock>();
            return blk != null && blk.DopingKind == ExtrinsicDopingKind.PType;
        }

        private static bool IsSelectableNBlock(IXRSelectInteractable selectable)
        {
            if (selectable == null) return false;
            ExtrinsicSemiconductorBlock blk = selectable.transform.GetComponentInParent<ExtrinsicSemiconductorBlock>();
            return blk != null && blk.DopingKind == ExtrinsicDopingKind.NType;
        }

        private static void SetBlockGrabEnabled(RespawnableItem item, bool unlocked)
        {
            if (item == null) return;
            XRGrabInteractable grab = item.GetComponent<XRGrabInteractable>();
            if (grab != null) grab.enabled = unlocked;
        }

        private static double ClampDouble(double v, double min, double max)
        {
            if (v < min) return min;
            return v > max ? max : v;
        }
    }
}
