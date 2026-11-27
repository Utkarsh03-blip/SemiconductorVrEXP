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
    /// Shared self-contained StationController for Station 2 (N-Type Doping) and Station 3 (P-Type Doping).
    /// Handles wafer + dopant Diffusion Furnace sockets, periodic group validation with wrong-action rejection &
    /// Dr. Vera explanations, logarithmic doping concentration slider (10^14..10^18 cm^-3), live SemiconductorModel
    /// telemetry, extrinsic 3D crystal lattice + energy band diagram, and N/P-Type output block dispensing.
    /// </summary>
    public class DopingStationController : StationController
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("Station Mode & Navigation")]
        [SerializeField] private ExtrinsicDopingKind stationMode = ExtrinsicDopingKind.NType;
        [SerializeField] private TeleportationAnchor stationAnchor;
        [SerializeField] private SemiconductorMaterial semiconductorMaterial = SemiconductorMaterial.Silicon;
        [SerializeField] private double referenceTemperatureK = 300.0;
        [SerializeField] private double defaultDopingExponent = 16.0;
        [SerializeField] private bool startUnlockedForStandalone;

        [Header("Diffusion Furnace Sockets & Props")]
        [SerializeField] private XRSocketInteractor waferSocket;
        [SerializeField] private SocketRingFeedback waferSocketFeedback;
        [SerializeField] private XRSocketInteractor dopantSocket;
        [SerializeField] private SocketRingFeedback dopantSocketFeedback;
        [SerializeField] private DopantVialItem[] shelfVials;
        [SerializeField] private RespawnableItem waferRespawnItem;
        [SerializeField] private Renderer furnaceCoreRenderer;
        [SerializeField] private RespawnZone respawnZone;
        [SerializeField] private AudioSource audioSource;

        [Header("Doping Controls & Visualizers")]
        [SerializeField] private Slider dopingExponentSlider;
        [SerializeField] private PhysicalPushButton concentrationDownButton;
        [SerializeField] private PhysicalPushButton concentrationUpButton;
        [SerializeField] private PhysicalPushButton resetStationButton;
        [SerializeField] private PhysicalPushButton returnPropsButton;
        [SerializeField] private ExtrinsicLatticeVisualizer latticeVisualizer;
        [SerializeField] private ExtrinsicBandDiagramView bandDiagramView;
        [SerializeField] private ExtrinsicSemiconductorBlock outputBlock;

        [Header("World-Space Readout Displays")]
        [SerializeField] private TMP_Text statusHeaderText;
        [SerializeField] private TMP_Text concentrationReadoutText;
        [SerializeField] private TMP_Text carrierReadoutText;
        [SerializeField] private TMP_Text fermiNeutralityText;
        [SerializeField] private TMP_Text conductivityReadoutText;
        [SerializeField] private TMP_Text outputTrayText;

        public ExtrinsicDopingKind StationMode => stationMode;
        public bool HasWaferInFurnace { get; private set; }
        public DopantVialItem ActiveDopant { get; private set; }
        public bool IsDopingCompleted { get; private set; }
        public double CurrentDopingExponent { get; private set; } = 16.0;
        public double CurrentDopingCm3 => Math.Pow(10.0, CurrentDopingExponent);
        public CarrierConcentrations CurrentConcentrations { get; private set; }

        private MaterialPropertyBlock furnacePropBlock;
        private double initialAdjustBaselineExp = 16.0;
        private bool reportedTeleport;
        private bool reportedDopeSuccess;
        private bool reportedConcentrationAdjust;
        private Coroutine ejectRoutine;

        private DopantValenceGroup ExpectedGroup => stationMode == ExtrinsicDopingKind.NType
            ? DopantValenceGroup.GroupV_Donor
            : DopantValenceGroup.GroupIII_Acceptor;

        private string DopeActionId => stationMode == ExtrinsicDopingKind.NType ? "station2.dope_n" : "station3.dope_p";
        private string AdjustActionId => stationMode == ExtrinsicDopingKind.NType ? "station2.adjust_nd" : "station3.adjust_na";
        private string WrongDopantActionId => stationMode == ExtrinsicDopingKind.NType ? "station2.wrong_dopant" : "station3.wrong_dopant";
        private string TeleportActionId => stationMode == ExtrinsicDopingKind.NType ? "station2.teleport" : "station3.teleport";

        protected override void Awake()
        {
            base.Awake();
            furnacePropBlock = new MaterialPropertyBlock();
            CurrentDopingExponent = ClampDouble(defaultDopingExponent, 14.0, 18.0);
            initialAdjustBaselineExp = CurrentDopingExponent;

            if (dopantSocketFeedback != null)
            {
                dopantSocketFeedback.CustomValidityPredicate = IsSelectableValidDopant;
            }

            if (dopingExponentSlider != null)
            {
                dopingExponentSlider.minValue = 14f;
                dopingExponentSlider.maxValue = 18f;
                dopingExponentSlider.SetValueWithoutNotify((float)CurrentDopingExponent);
            }

            if (outputBlock != null)
            {
                outputBlock.SetDispensed(false, false);
            }

            RefreshPhysicsAndDisplays();
            SetUnlocked(startUnlockedForStandalone);
        }

        private void OnEnable()
        {
            if (stationAnchor != null)
            {
                stationAnchor.teleporting.AddListener(HandleTeleporting);
            }

            if (waferSocket != null)
            {
                waferSocket.selectEntered.AddListener(HandleWaferEntered);
                waferSocket.selectExited.AddListener(HandleWaferExited);
            }

            if (dopantSocket != null)
            {
                dopantSocket.selectEntered.AddListener(HandleDopantEntered);
                dopantSocket.selectExited.AddListener(HandleDopantExited);
            }

            if (dopingExponentSlider != null)
            {
                dopingExponentSlider.onValueChanged.AddListener(HandleSliderChanged);
            }

            if (concentrationDownButton != null)
            {
                concentrationDownButton.Pressed += HandleDecadeDown;
            }

            if (concentrationUpButton != null)
            {
                concentrationUpButton.Pressed += HandleDecadeUp;
            }

            if (resetStationButton != null)
            {
                resetStationButton.Pressed += HandleResetPressed;
            }

            if (returnPropsButton != null)
            {
                returnPropsButton.Pressed += HandleReturnPropsPressed;
            }
        }

        private void OnDisable()
        {
            if (stationAnchor != null)
            {
                stationAnchor.teleporting.RemoveListener(HandleTeleporting);
            }

            if (waferSocket != null)
            {
                waferSocket.selectEntered.RemoveListener(HandleWaferEntered);
                waferSocket.selectExited.RemoveListener(HandleWaferExited);
            }

            if (dopantSocket != null)
            {
                dopantSocket.selectEntered.RemoveListener(HandleDopantEntered);
                dopantSocket.selectExited.RemoveListener(HandleDopantExited);
            }

            if (dopingExponentSlider != null)
            {
                dopingExponentSlider.onValueChanged.RemoveListener(HandleSliderChanged);
            }

            if (concentrationDownButton != null)
            {
                concentrationDownButton.Pressed -= HandleDecadeDown;
            }

            if (concentrationUpButton != null)
            {
                concentrationUpButton.Pressed -= HandleDecadeUp;
            }

            if (resetStationButton != null)
            {
                resetStationButton.Pressed -= HandleResetPressed;
            }

            if (returnPropsButton != null)
            {
                returnPropsButton.Pressed -= HandleReturnPropsPressed;
            }
        }

        public override void SetUnlocked(bool unlocked)
        {
            base.SetUnlocked(unlocked);

            if (waferSocket != null) waferSocket.enabled = unlocked;
            if (dopantSocket != null) dopantSocket.enabled = unlocked;
            if (dopingExponentSlider != null) dopingExponentSlider.interactable = unlocked && IsDopingCompleted;
            if (concentrationDownButton != null) concentrationDownButton.SetButtonEnabled(unlocked);
            if (concentrationUpButton != null) concentrationUpButton.SetButtonEnabled(unlocked);
            if (resetStationButton != null) resetStationButton.SetButtonEnabled(unlocked);
            if (returnPropsButton != null) returnPropsButton.SetButtonEnabled(unlocked);

            if (shelfVials != null)
            {
                for (int i = 0; i < shelfVials.Length; i++)
                {
                    if (shelfVials[i] != null && shelfVials[i].GrabInteractable != null)
                    {
                        shelfVials[i].GrabInteractable.enabled = unlocked;
                    }
                }
            }

            if (waferRespawnItem != null)
            {
                XRGrabInteractable waferGrab = waferRespawnItem.GetComponent<XRGrabInteractable>();
                if (waferGrab != null)
                {
                    waferGrab.enabled = unlocked;
                }
            }
        }

        public override void ResetStation()
        {
            base.ResetStation();

            if (ejectRoutine != null)
            {
                StopCoroutine(ejectRoutine);
                ejectRoutine = null;
            }

            HasWaferInFurnace = false;
            ActiveDopant = null;
            IsDopingCompleted = false;
            reportedDopeSuccess = false;
            reportedConcentrationAdjust = false;
            CurrentDopingExponent = ClampDouble(defaultDopingExponent, 14.0, 18.0);
            initialAdjustBaselineExp = CurrentDopingExponent;

            if (dopingExponentSlider != null)
            {
                dopingExponentSlider.SetValueWithoutNotify((float)CurrentDopingExponent);
                dopingExponentSlider.interactable = false;
            }

            if (outputBlock != null)
            {
                outputBlock.SetDispensed(false, false);
            }

            ReturnAllShelfProps();
            RefreshPhysicsAndDisplays();
        }

        /// <summary>
        /// Programmatic / UI test helper to test docking a vial directly or simulate socket entry.
        /// </summary>
        public bool TryProcessVialDrop(DopantVialItem vial, bool includeWafer)
        {
            if (!IsUnlocked || vial == null)
            {
                return false;
            }

            if (includeWafer)
            {
                HasWaferInFurnace = true;
            }

            if (vial.ValenceGroup != ExpectedGroup)
            {
                RejectWrongDopant(vial);
                return false;
            }

            ActiveDopant = vial;
            EvaluateFurnaceReaction();
            return true;
        }

        /// <summary>
        /// Sets the logarithmic doping exponent (14.0 = 10^14 cm^-3 to 18.0 = 10^18 cm^-3).
        /// </summary>
        public void SetDopingExponent(double exponent, bool notifyLesson = true)
        {
            if (!IsUnlocked)
            {
                return;
            }

            double clamped = ClampDouble(exponent, 14.0, 18.0);
            CurrentDopingExponent = clamped;
            if (dopingExponentSlider != null)
            {
                dopingExponentSlider.SetValueWithoutNotify((float)clamped);
            }

            RefreshPhysicsAndDisplays();

            if (notifyLesson && IsDopingCompleted && !reportedConcentrationAdjust
                && Math.Abs(CurrentDopingExponent - initialAdjustBaselineExp) >= 0.25)
            {
                reportedConcentrationAdjust = true;
                ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Success, 0.55f);
                NotifyStepAction(AdjustActionId);
            }
        }

        private bool IsSelectableValidDopant(IXRSelectInteractable selectable)
        {
            if (selectable == null) return false;
            DopantVialItem vial = selectable.transform.GetComponentInParent<DopantVialItem>();
            return vial != null && vial.ValenceGroup == ExpectedGroup;
        }

        private static double ClampDouble(double val, double min, double max)
        {
            return val < min ? min : (val > max ? max : val);
        }

        private void HandleTeleporting(TeleportingEventArgs args)
        {
            if (!IsUnlocked || reportedTeleport)
            {
                return;
            }

            reportedTeleport = true;
            NotifyStepAction(TeleportActionId);
        }

        private void HandleWaferEntered(SelectEnterEventArgs args)
        {
            if (args.interactableObject == null)
            {
                return;
            }

            DopantVialItem wrongVialInWaferBay = args.interactableObject.transform.GetComponentInParent<DopantVialItem>();
            if (wrongVialInWaferBay != null)
            {
                waferSocketFeedback?.FlashInvalid(0.75f);
                ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.SocketInvalid, 0.65f);
                NotifyWrongAction(WrongDopantActionId,
                    $"Place dopant vial {wrongVialInWaferBay.ElementSymbol} into the right Dopant socket, and place the Silicon Wafer into the left Wafer socket.");
                StartCoroutine(ReturnWrongObjectNextFrame(wrongVialInWaferBay));
                return;
            }

            HasWaferInFurnace = true;
            EvaluateFurnaceReaction();
        }

        private IEnumerator ReturnWrongObjectNextFrame(DopantVialItem vial)
        {
            yield return new WaitForSeconds(0.45f);
            if (vial != null)
            {
                vial.ReturnToShelf();
            }

            RefreshPhysicsAndDisplays();
        }

        private void HandleWaferExited(SelectExitEventArgs args)
        {
            HasWaferInFurnace = false;
            RefreshPhysicsAndDisplays();
        }

        private void HandleDopantEntered(SelectEnterEventArgs args)
        {
            if (args.interactableObject == null)
            {
                return;
            }

            DopantVialItem vial = args.interactableObject.transform.GetComponentInParent<DopantVialItem>();
            if (vial == null)
            {
                if (waferRespawnItem != null && args.interactableObject.transform.GetComponentInParent<RespawnableItem>() == waferRespawnItem)
                {
                    dopantSocketFeedback?.FlashInvalid(0.75f);
                    ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.SocketInvalid, 0.65f);
                    NotifyWrongAction(WrongDopantActionId,
                        "That Silicon Wafer goes in the left Wafer bay! Drop a chemical dopant vial in this right Dopant bay.");
                    StartCoroutine(ReturnWaferNextFrame());
                }

                return;
            }

            if (vial.ValenceGroup != ExpectedGroup)
            {
                RejectWrongDopant(vial);
                return;
            }

            ActiveDopant = vial;
            EvaluateFurnaceReaction();
        }

        private void HandleDopantExited(SelectExitEventArgs args)
        {
            if (ActiveDopant != null && dopantSocket != null && !dopantSocket.hasSelection)
            {
                ActiveDopant = null;
                RefreshPhysicsAndDisplays();
            }
        }

        private void RejectWrongDopant(DopantVialItem wrongVial)
        {
            ActiveDopant = null;
            dopantSocketFeedback?.FlashInvalid(0.85f);
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.SocketInvalid, 0.72f);

            string explanation = wrongVial.BuildWrongGroupMessage(stationMode);
            NotifyWrongAction(WrongDopantActionId, explanation);

            if (ejectRoutine != null)
            {
                StopCoroutine(ejectRoutine);
            }

            ejectRoutine = StartCoroutine(EjectWrongVialRoutine(wrongVial));
        }

        private IEnumerator EjectWrongVialRoutine(DopantVialItem wrongVial)
        {
            yield return new WaitForSeconds(0.55f);
            if (wrongVial != null)
            {
                wrongVial.ReturnToShelf();
            }

            RefreshPhysicsAndDisplays();
            ejectRoutine = null;
        }

        private IEnumerator ReturnWaferNextFrame()
        {
            yield return new WaitForSeconds(0.45f);
            if (waferRespawnItem != null)
            {
                waferRespawnItem.Respawn();
            }

            RefreshPhysicsAndDisplays();
        }

        private void EvaluateFurnaceReaction()
        {
            RefreshPhysicsAndDisplays();

            if (!IsUnlocked || !HasWaferInFurnace || ActiveDopant == null || ActiveDopant.ValenceGroup != ExpectedGroup)
            {
                return;
            }

            bool newlyDoped = !IsDopingCompleted;
            IsDopingCompleted = true;
            if (dopingExponentSlider != null)
            {
                dopingExponentSlider.interactable = true;
            }

            if (outputBlock != null)
            {
                outputBlock.SetBlockState(stationMode, semiconductorMaterial, ActiveDopant.ElementSymbol, CurrentDopingCm3, referenceTemperatureK);
                if (newlyDoped || !outputBlock.IsDispensed)
                {
                    outputBlock.SetDispensed(true, true);
                }
            }

            RefreshPhysicsAndDisplays();

            if (!reportedDopeSuccess)
            {
                reportedDopeSuccess = true;
                initialAdjustBaselineExp = CurrentDopingExponent;
                ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Success, 0.72f);
                NotifyStepAction(DopeActionId);
            }
        }

        private void HandleSliderChanged(float value)
        {
            SetDopingExponent(value, true);
        }

        private void HandleDecadeDown()
        {
            SetDopingExponent(CurrentDopingExponent - 0.5, true);
        }

        private void HandleDecadeUp()
        {
            SetDopingExponent(CurrentDopingExponent + 0.5, true);
        }

        private void HandleResetPressed()
        {
            ResetStation();
        }

        private void HandleReturnPropsPressed()
        {
            ReturnAllShelfProps();
        }

        private void ReturnAllShelfProps()
        {
            if (waferRespawnItem != null)
            {
                waferRespawnItem.Respawn();
            }

            if (shelfVials != null)
            {
                for (int i = 0; i < shelfVials.Length; i++)
                {
                    if (shelfVials[i] != null)
                    {
                        shelfVials[i].ReturnToShelf();
                    }
                }
            }
        }

        private void RefreshPhysicsAndDisplays()
        {
            double conc = CurrentDopingCm3;
            CurrentConcentrations = stationMode == ExtrinsicDopingKind.PType
                ? SemiconductorModel.ComputePType(semiconductorMaterial, conc, referenceTemperatureK)
                : SemiconductorModel.ComputeNType(semiconductorMaterial, conc, referenceTemperatureK);

            string sym = ActiveDopant != null
                ? ActiveDopant.ElementSymbol
                : (stationMode == ExtrinsicDopingKind.NType ? "P" : "B");

            bool showExtrinsic = IsDopingCompleted && ActiveDopant != null && HasWaferInFurnace;

            if (latticeVisualizer != null)
            {
                latticeVisualizer.SetLatticeActive(showExtrinsic);
                if (showExtrinsic)
                {
                    latticeVisualizer.SetExtrinsicState(stationMode, sym, conc, CurrentConcentrations);
                }
            }

            if (bandDiagramView != null)
            {
                bandDiagramView.SetBandState(stationMode, semiconductorMaterial, sym, conc, CurrentConcentrations);
            }

            if (outputBlock != null && outputBlock.IsDispensed)
            {
                outputBlock.SetBlockState(stationMode, semiconductorMaterial, sym, conc, referenceTemperatureK);
            }

            UpdateFurnaceVisualGlow(showExtrinsic);
            UpdateMetersText(showExtrinsic, sym);
        }

        private void UpdateFurnaceVisualGlow(bool activeReaction)
        {
            if (furnaceCoreRenderer == null)
            {
                return;
            }

            if (furnacePropBlock == null)
            {
                furnacePropBlock = new MaterialPropertyBlock();
            }

            Color coreCol = activeReaction
                ? (stationMode == ExtrinsicDopingKind.NType ? new Color(0.18f, 0.72f, 1.0f, 1f) : new Color(0.95f, 0.34f, 0.36f, 1f))
                : new Color(0.24f, 0.30f, 0.38f, 1f);

            furnaceCoreRenderer.GetPropertyBlock(furnacePropBlock);
            furnacePropBlock.SetColor(BaseColorId, coreCol);
            furnacePropBlock.SetColor(EmissionColorId, activeReaction ? coreCol * 0.65f : Color.black);
            furnaceCoreRenderer.SetPropertyBlock(furnacePropBlock);
        }

        private void UpdateMetersText(bool showExtrinsic, string dopantSym)
        {
            bool isN = stationMode == ExtrinsicDopingKind.NType;
            string expectedLabel = isN ? "Group V Donor (P, As, Sb)" : "Group III Acceptor (B, Al, Ga, In)";
            string symSym = isN ? "ND" : "NA";

            if (statusHeaderText != null)
            {
                if (!showExtrinsic)
                {
                    string waferTxt = HasWaferInFurnace ? "Si Wafer READY" : "Insert Si Wafer";
                    string dopTxt = ActiveDopant != null ? $"Dopant {ActiveDopant.ElementSymbol} READY" : $"Insert {expectedLabel}";
                    statusHeaderText.text = $"DIFFUSION FURNACE STANDBY\n{waferTxt} | {dopTxt}";
                }
                else
                {
                    string modeWord = isN ? "N-TYPE DOPED" : "P-TYPE DOPED";
                    statusHeaderText.text = $"{modeWord} SILICON ({dopantSym}) @ {referenceTemperatureK:0} K\nAdjust {symSym} concentration (10^14 - 10^18 cm^-3)";
                }
            }

            if (concentrationReadoutText != null)
            {
                concentrationReadoutText.text = $"{symSym} Doping: {CurrentDopingCm3:0.00E+0} cm^-3 (10^{CurrentDopingExponent:F1})";
            }

            if (carrierReadoutText != null)
            {
                if (!showExtrinsic)
                {
                    carrierReadoutText.text = "Insert Si Wafer + valid dopant vial into Furnace sockets";
                }
                else
                {
                    carrierReadoutText.text = isN
                        ? $"Majority e-: n = {CurrentConcentrations.ElectronsCm3:0.00E+0} cm^-3\nMinority h+: p = ni^2/n = {CurrentConcentrations.HolesCm3:0.00E+0} cm^-3"
                        : $"Majority h+: p = {CurrentConcentrations.HolesCm3:0.00E+0} cm^-3\nMinority e-: n = ni^2/p = {CurrentConcentrations.ElectronsCm3:0.00E+0} cm^-3";
                }
            }

            if (fermiNeutralityText != null)
            {
                if (!showExtrinsic)
                {
                    fermiNeutralityText.text = "EF = Ei (Intrinsic midgap prior to doping)";
                }
                else
                {
                    string shiftDir = isN ? "UP toward Ec" : "DOWN toward Ev";
                    string neutralityEq = isN ? "Net Charge: p + ND+ - n = 0 (Neutral)" : "Net Charge: p - n - NA- = 0 (Neutral)";
                    fermiNeutralityText.text = $"EF Shift: {CurrentConcentrations.FermiShiftFromMidgapEv:+0.000;-0.000;0.000} eV ({shiftDir})\n{neutralityEq}";
                }
            }

            if (conductivityReadoutText != null)
            {
                conductivityReadoutText.text = showExtrinsic
                    ? $"Conductivity (sigma): {CurrentConcentrations.ConductivitySPerCm:0.00E+0} S/cm | ni(300K) = {CurrentConcentrations.IntrinsicNiCm3:0.00E+0}"
                    : "Conductivity (sigma): pending doping";
            }

            if (outputTrayText != null)
            {
                outputTrayText.text = showExtrinsic
                    ? (isN
                        ? $"OUTPUT READY: N-Type Si ({dopantSym})\nKeep for Station 4 (P-N Junction)"
                        : $"OUTPUT READY: P-Type Si ({dopantSym})\nKeep for Station 4 (P-N Junction)")
                    : "OUTPUT TRAY: Empty (Dope wafer to fabricate block)";
            }
        }
    }
}
