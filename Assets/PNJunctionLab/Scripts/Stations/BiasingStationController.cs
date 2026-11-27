using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace PNJunctionLab
{
    /// <summary>
    /// Self-contained StationController for Station 5: Diode Biasing.
    /// Features pluggable Red (+) and Black (-) battery leads into labelled P & N diode terminals,
    /// variable DC supply (0..60 V piecewise fine/breakdown control) through a 220 ohm protective resistor,
    /// digital Voltmeter & Ammeter solved strictly via SemiconductorModel, indicator LED, live biased
    /// 3D junction cutaway, and latched Reverse Breakdown protection alarm until Reset.
    /// </summary>
    public class BiasingStationController : StationController
    {
        [Header("Navigation & Circuit Constants")]
        [SerializeField] private TeleportationAnchor stationAnchor;
        [SerializeField] private DiodeType diodeType = DiodeType.Silicon;
        [SerializeField] private SemiconductorMaterial junctionMaterial = SemiconductorMaterial.Silicon;
        [SerializeField] private double seriesResistanceOhms = 220.0;
        [SerializeField] private double temperatureK = 300.0;
        [SerializeField] private double defaultAcceptorNaCm3 = 1e16;
        [SerializeField] private double defaultDonorNdCm3 = 1e16;
        [SerializeField] private bool startUnlockedForStandalone;

        [Header("Battery Leads & Diode Sockets")]
        [SerializeField] private XRSocketInteractor pTerminalSocket;
        [SerializeField] private SocketRingFeedback pSocketFeedback;
        [SerializeField] private XRSocketInteractor nTerminalSocket;
        [SerializeField] private SocketRingFeedback nSocketFeedback;
        [SerializeField] private BiasLeadPlug redPositiveLead;
        [SerializeField] private BiasLeadPlug blackNegativeLead;
        [SerializeField] private RespawnZone respawnZone;
        [SerializeField] private AudioSource audioSource;

        [Header("Voltage & Station Controls")]
        [SerializeField] private RotaryKnobControl voltageKnob;
        [SerializeField] private Slider voltageSlider;
        [SerializeField] private PhysicalPushButton voltDownButton;
        [SerializeField] private PhysicalPushButton voltUpButton;
        [SerializeField] private PhysicalPushButton voltStep10Button;
        [SerializeField] private PhysicalPushButton returnLeadsButton;
        [SerializeField] private PhysicalPushButton resetStationButton;

        [Header("Displays & Visualizers")]
        [SerializeField] private BiasedJunctionVisual junctionVisual;
        [SerializeField] private TMP_Text circuitStatusText;
        [SerializeField] private TMP_Text voltmeterText;
        [SerializeField] private TMP_Text ammeterText;
        [SerializeField] private TMP_Text supplyVoltageText;
        [SerializeField] private GameObject breakdownWarningBanner;
        [SerializeField] private TMP_Text breakdownWarningText;

        public float SupplyVoltageV { get; private set; }
        public double DiodeVoltageV { get; private set; }
        public double CircuitCurrentA { get; private set; }
        public DiodeBiasMode BiasMode { get; private set; } = DiodeBiasMode.OpenCircuit;
        public bool IsBreakdownLatched { get; private set; }

        private BiasLeadPlug pluggedInP;
        private BiasLeadPlug pluggedInN;
        private bool suppressControlEvents;
        private bool wasForwardActive;
        private bool wasReverseActive;
        private bool breakdownTriggeredBeforeReset;
        private double equilibriumDepletionUm;

        protected override void Awake()
        {
            base.Awake();

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (pSocketFeedback != null)
            {
                pSocketFeedback.CustomValidityPredicate = selectable =>
                    selectable != null && selectable.transform.GetComponentInParent<BiasLeadPlug>() != null;
            }

            if (nSocketFeedback != null)
            {
                nSocketFeedback.CustomValidityPredicate = selectable =>
                    selectable != null && selectable.transform.GetComponentInParent<BiasLeadPlug>() != null;
            }

            JunctionElectrostatics eq = SemiconductorModel.ComputeJunctionElectrostatics(
                junctionMaterial, defaultAcceptorNaCm3, defaultDonorNdCm3, 0.0, temperatureK);
            equilibriumDepletionUm = eq.DepletionWidthMicrometers;

            SetSupplyVoltageInternal(0f, true);
        }

        private void Start()
        {
            if (startUnlockedForStandalone && !IsUnlocked)
            {
                SetUnlocked(true);
            }
            else
            {
                ApplyControlLockState();
            }

            RefreshCircuitAndDisplay();
        }

        private void OnEnable()
        {
            if (stationAnchor != null)
            {
                stationAnchor.teleporting.AddListener(HandlePlayerTeleportedHere);
            }

            if (pTerminalSocket != null)
            {
                pTerminalSocket.selectEntered.AddListener(HandlePSocketEntered);
                pTerminalSocket.selectExited.AddListener(HandlePSocketExited);
            }

            if (nTerminalSocket != null)
            {
                nTerminalSocket.selectEntered.AddListener(HandleNSocketEntered);
                nTerminalSocket.selectExited.AddListener(HandleNSocketExited);
            }

            if (voltageKnob != null)
            {
                voltageKnob.ValueChanged += HandleKnobChanged;
            }

            if (voltageSlider != null)
            {
                voltageSlider.onValueChanged.AddListener(HandleSliderChanged);
            }

            if (voltDownButton != null) voltDownButton.Pressed += HandleVoltDownPressed;
            if (voltUpButton != null) voltUpButton.Pressed += HandleVoltUpPressed;
            if (voltStep10Button != null) voltStep10Button.Pressed += HandleVoltStep10Pressed;
            if (returnLeadsButton != null) returnLeadsButton.Pressed += HandleReturnLeadsPressed;
            if (resetStationButton != null) resetStationButton.Pressed += HandleResetButtonPressed;
        }

        private void OnDisable()
        {
            if (stationAnchor != null)
            {
                stationAnchor.teleporting.RemoveListener(HandlePlayerTeleportedHere);
            }

            if (pTerminalSocket != null)
            {
                pTerminalSocket.selectEntered.RemoveListener(HandlePSocketEntered);
                pTerminalSocket.selectExited.RemoveListener(HandlePSocketExited);
            }

            if (nTerminalSocket != null)
            {
                nTerminalSocket.selectEntered.RemoveListener(HandleNSocketEntered);
                nTerminalSocket.selectExited.RemoveListener(HandleNSocketExited);
            }

            if (voltageKnob != null)
            {
                voltageKnob.ValueChanged -= HandleKnobChanged;
            }

            if (voltageSlider != null)
            {
                voltageSlider.onValueChanged.RemoveListener(HandleSliderChanged);
            }

            if (voltDownButton != null) voltDownButton.Pressed -= HandleVoltDownPressed;
            if (voltUpButton != null) voltUpButton.Pressed -= HandleVoltUpPressed;
            if (voltStep10Button != null) voltStep10Button.Pressed -= HandleVoltStep10Pressed;
            if (returnLeadsButton != null) returnLeadsButton.Pressed -= HandleReturnLeadsPressed;
            if (resetStationButton != null) resetStationButton.Pressed -= HandleResetButtonPressed;
        }

        public override void SetUnlocked(bool unlocked)
        {
            base.SetUnlocked(unlocked);
            ApplyControlLockState();
        }

        public override void ResetStation()
        {
            bool wasBreakdown = breakdownTriggeredBeforeReset;

            base.ResetStation();
            IsBreakdownLatched = false;
            breakdownTriggeredBeforeReset = false;
            wasForwardActive = false;
            wasReverseActive = false;

            ReturnAllLeadsInternal();
            SetSupplyVoltageInternal(0f, true);
            ApplyControlLockState();
            RefreshCircuitAndDisplay();
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.45f);

            if (wasBreakdown)
            {
                NotifyStepAction("station5.breakdown_reset");
                MarkCompleted();
            }
        }

        /// <summary>
        /// Sets the battery voltage magnitude (0..60 V) directly for buttons and test verification.
        /// </summary>
        public void SetSupplyVoltage(float volts)
        {
            if (!IsUnlocked || IsBreakdownLatched)
            {
                return;
            }

            SetSupplyVoltageInternal(volts, true);
            RefreshCircuitAndDisplay();
        }

        /// <summary>
        /// Converts normalized [0..1] slider position into piecewise voltage:
        /// [0..0.5] -> 0..5 V (fine knee control), [0.5..1.0] -> 5..60 V (breakdown sweep).
        /// </summary>
        public static float NormalizedToVoltage(float t01)
        {
            float t = Mathf.Clamp01(t01);
            return t <= 0.5f
                ? (t / 0.5f) * 5.0f
                : 5.0f + ((t - 0.5f) / 0.5f) * 55.0f;
        }

        /// <summary>
        /// Converts battery supply voltage (0..60 V) into normalized [0..1] slider position.
        /// </summary>
        public static float VoltageToNormalized(float volts)
        {
            float v = Mathf.Clamp(volts, 0f, 60f);
            return v <= 5.0f
                ? (v / 5.0f) * 0.5f
                : 0.5f + ((v - 5.0f) / 55.0f) * 0.5f;
        }

        private void HandlePlayerTeleportedHere(TeleportingEventArgs args)
        {
            if (IsUnlocked)
            {
                NotifyStepAction("station5.teleport");
            }
        }

        private void HandlePSocketEntered(SelectEnterEventArgs args)
        {
            BiasLeadPlug plug = args.interactableObject != null
                ? args.interactableObject.transform.GetComponentInParent<BiasLeadPlug>()
                : null;
            pluggedInP = plug;
            EvaluatePlugChange();
        }

        private void HandlePSocketExited(SelectExitEventArgs args)
        {
            if (args.interactableObject != null
                && args.interactableObject.transform.GetComponentInParent<BiasLeadPlug>() == pluggedInP)
            {
                pluggedInP = null;
                EvaluatePlugChange();
            }
        }

        private void HandleNSocketEntered(SelectEnterEventArgs args)
        {
            BiasLeadPlug plug = args.interactableObject != null
                ? args.interactableObject.transform.GetComponentInParent<BiasLeadPlug>()
                : null;
            pluggedInN = plug;
            EvaluatePlugChange();
        }

        private void HandleNSocketExited(SelectExitEventArgs args)
        {
            if (args.interactableObject != null
                && args.interactableObject.transform.GetComponentInParent<BiasLeadPlug>() == pluggedInN)
            {
                pluggedInN = null;
                EvaluatePlugChange();
            }
        }

        private void EvaluatePlugChange()
        {
            // Clear reverse breakdown latch cleanly if circuit was unplugged and reset will still be rewarded when pressed.
            RefreshCircuitAndDisplay();
        }

        private void HandleKnobChanged(float knobPercent)
        {
            if (suppressControlEvents || !IsUnlocked || IsBreakdownLatched)
            {
                return;
            }

            SetSupplyVoltageInternal(NormalizedToVoltage(knobPercent / 100f), false, syncSlider: true, syncKnob: false);
            RefreshCircuitAndDisplay();
        }

        private void HandleSliderChanged(float sliderValue)
        {
            if (suppressControlEvents || !IsUnlocked || IsBreakdownLatched)
            {
                return;
            }

            SetSupplyVoltageInternal(NormalizedToVoltage(sliderValue), false, syncSlider: false, syncKnob: true);
            RefreshCircuitAndDisplay();
        }

        private void HandleVoltDownPressed()
        {
            if (!IsUnlocked || IsBreakdownLatched) return;
            float delta = SupplyVoltageV > 5.2f ? -2.5f : -0.2f;
            SetSupplyVoltage(SupplyVoltageV + delta);
        }

        private void HandleVoltUpPressed()
        {
            if (!IsUnlocked || IsBreakdownLatched) return;
            float delta = SupplyVoltageV >= 5.0f ? 2.5f : 0.2f;
            SetSupplyVoltage(SupplyVoltageV + delta);
        }

        private void HandleVoltStep10Pressed()
        {
            if (!IsUnlocked || IsBreakdownLatched) return;
            SetSupplyVoltage(SupplyVoltageV + 10.0f);
        }

        private void HandleReturnLeadsPressed()
        {
            if (!IsUnlocked) return;
            ReturnAllLeadsInternal();
            RefreshCircuitAndDisplay();
        }

        private void HandleResetButtonPressed()
        {
            if (!IsUnlocked) return;
            ResetStation();
        }

        private void SetSupplyVoltageInternal(float volts, bool syncBoth, bool syncSlider = true, bool syncKnob = true)
        {
            SupplyVoltageV = Mathf.Clamp(volts, 0f, 60f);
            float norm = VoltageToNormalized(SupplyVoltageV);

            suppressControlEvents = true;
            if ((syncBoth || syncSlider) && voltageSlider != null)
            {
                voltageSlider.SetValueWithoutNotify(norm);
            }

            if ((syncBoth || syncKnob) && voltageKnob != null)
            {
                voltageKnob.SetValue(norm * 100f, false);
            }
            suppressControlEvents = false;
        }

        private void RefreshCircuitAndDisplay()
        {
            DiodeParameters diodeParams = SemiconductorModel.GetDiodeParameters(diodeType);

            bool hasRedInP = pluggedInP != null && pluggedInP.Polarity == BiasLeadPolarity.PositiveRed;
            bool hasBlackInN = pluggedInN != null && pluggedInN.Polarity == BiasLeadPolarity.NegativeBlack;
            bool hasRedInN = pluggedInN != null && pluggedInN.Polarity == BiasLeadPolarity.PositiveRed;
            bool hasBlackInP = pluggedInP != null && pluggedInP.Polarity == BiasLeadPolarity.NegativeBlack;

            bool isForward = hasRedInP && hasBlackInN;
            bool isReverse = hasRedInN && hasBlackInP;

            double signedSourceV = isForward ? SupplyVoltageV : (isReverse ? -SupplyVoltageV : 0.0);

            if (!isForward && !isReverse)
            {
                BiasMode = DiodeBiasMode.OpenCircuit;
                DiodeVoltageV = 0.0;
                CircuitCurrentA = 0.0;
            }
            else
            {
                SemiconductorModel.SolveSeriesDiodeCircuit(
                    diodeType,
                    signedSourceV,
                    seriesResistanceOhms,
                    temperatureK,
                    out double solvedVd,
                    out double solvedI);

                DiodeVoltageV = solvedVd;
                CircuitCurrentA = solvedI;

                if (isForward)
                {
                    BiasMode = DiodeBiasMode.ForwardBias;
                }
                else
                {
                    bool hitBreakdown = Math.Abs(DiodeVoltageV) >= diodeParams.BreakdownVoltageV
                        || SupplyVoltageV >= diodeParams.BreakdownVoltageV + 0.05;

                    if (hitBreakdown || IsBreakdownLatched)
                    {
                        BiasMode = DiodeBiasMode.ReverseBreakdown;
                        if (!IsBreakdownLatched)
                        {
                            IsBreakdownLatched = true;
                            breakdownTriggeredBeforeReset = true;
                            ApplyControlLockState();
                            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.SocketInvalid, 0.7f);
                            NotifyWrongAction("reverse_breakdown",
                                $"Reverse breakdown reached at -{diodeParams.BreakdownVoltageV:F0} V! " +
                                $"Notice our {seriesResistanceOhms:F0} ohm resistor caps current, whereas an unprotected diode " +
                                $"suffers thermal runaway. Press RESET STATION to clear.");
                        }
                    }
                    else
                    {
                        BiasMode = DiodeBiasMode.ReverseBias;
                    }
                }
            }

            // Clamp electrostatic barrier voltage magnitude below V0 for visual depletion scaling cleanly.
            double electroBiasV = BiasMode == DiodeBiasMode.OpenCircuit
                ? 0.0
                : isForward
                    ? Math.Min(DiodeVoltageV, 0.85)
                    : -Math.Min(Math.Abs(DiodeVoltageV), 50.0);

            JunctionElectrostatics electro = SemiconductorModel.ComputeJunctionElectrostatics(
                junctionMaterial, defaultAcceptorNaCm3, defaultDonorNdCm3, electroBiasV, temperatureK);

            if (junctionVisual != null)
            {
                junctionVisual.ApplyState(
                    BiasMode, signedSourceV, DiodeVoltageV, CircuitCurrentA,
                    electro, equilibriumDepletionUm, diodeParams.BreakdownVoltageV);
            }

            UpdateMetersText(diodeParams, isForward, isReverse);

            // Step completion progression while unlocked (re-triggers cleanly on state transitions):
            bool isForwardNow = isForward && SupplyVoltageV >= 0.85f && CircuitCurrentA >= 0.0005;
            bool isReverseNow = isReverse && !IsBreakdownLatched && SupplyVoltageV >= 2.0f;

            if (IsUnlocked)
            {
                if (isForwardNow && !wasForwardActive)
                {
                    NotifyStepAction("station5.forward_bias");
                }

                if (isReverseNow && !wasReverseActive)
                {
                    NotifyStepAction("station5.reverse_bias");
                }
            }

            wasForwardActive = isForwardNow;
            wasReverseActive = isReverseNow;
        }

        private void UpdateMetersText(DiodeParameters diodeParams, bool isForward, bool isReverse)
        {
            if (supplyVoltageText != null)
            {
                string signHint = isForward ? "+" : (isReverse ? "-" : "");
                supplyVoltageText.text = $"V_src: {signHint}{SupplyVoltageV:F2} V\n" +
                                         $"R_series: {seriesResistanceOhms:F0} ohm";
            }

            if (voltmeterText != null)
            {
                voltmeterText.text = $"VOLTMETER (Vd)\n{DiodeVoltageV:+0.000;-0.000;0.000} V";
            }

            if (ammeterText != null)
            {
                ammeterText.text = $"AMMETER (I)\n{FormatCurrent(CircuitCurrentA)}";
            }

            if (circuitStatusText != null)
            {
                if (!isForward && !isReverse)
                {
                    circuitStatusText.text =
                        "PLUG BATTERY LEADS: Red (+) and Black (-) into P-Terminal and N-Terminal sockets.\n" +
                        "Red -> P, Black -> N = Forward Bias  |  Red -> N, Black -> P = Reverse Bias";
                }
                else if (BiasMode == DiodeBiasMode.ForwardBias)
                {
                    string ledWord = CircuitCurrentA >= 0.0004 ? "LED ON (above 0.70 V Si knee)" : "LED OFF (below 0.70 V knee)";
                    circuitStatusText.text =
                        $"FORWARD BIAS: {ledWord} | Barrier reduced -> exponential current!";
                }
                else if (BiasMode == DiodeBiasMode.ReverseBias)
                {
                    circuitStatusText.text =
                        $"REVERSE BIAS: Majority carriers blocked, depletion widens! Sweep past -{diodeParams.BreakdownVoltageV:F0} V to test Breakdown.";
                }
                else
                {
                    circuitStatusText.text =
                        $"AVALANCHE BREAKDOWN (-{diodeParams.BreakdownVoltageV:F0} V)! Voltage controls locked — Press RESET STATION.";
                }
            }

            if (breakdownWarningBanner != null)
            {
                breakdownWarningBanner.SetActive(IsBreakdownLatched);
            }

            if (breakdownWarningText != null && IsBreakdownLatched)
            {
                breakdownWarningText.text =
                    $"BREAKDOWN ALERT!  |V_rev| >= {diodeParams.BreakdownVoltageV:F0} V\n" +
                    $"Protective {seriesResistanceOhms:F0} ohm resistor prevents burnout. Press RESET STATION.";
            }
        }

        private void ReturnAllLeadsInternal()
        {
            ForceReleaseFromSocket(pTerminalSocket);
            ForceReleaseFromSocket(nTerminalSocket);
            pluggedInP = null;
            pluggedInN = null;

            if (respawnZone != null)
            {
                respawnZone.RespawnAllManagedItems();
            }
            else
            {
                if (redPositiveLead != null) redPositiveLead.ReturnToBattery();
                if (blackNegativeLead != null) blackNegativeLead.ReturnToBattery();
            }
        }

        private static void ForceReleaseFromSocket(XRSocketInteractor socket)
        {
            if (socket != null && socket.hasSelection && socket.interactionManager != null && socket.interactablesSelected.Count > 0)
            {
                var selected = socket.interactablesSelected[0];
                if (selected != null)
                {
                    socket.interactionManager.SelectExit(socket, selected);
                }
            }
        }

        private void ApplyControlLockState()
        {
            bool unlocked = IsUnlocked;
            bool voltageAllowed = unlocked && !IsBreakdownLatched;

            if (voltageKnob != null) voltageKnob.SetInteractive(voltageAllowed);
            if (voltageSlider != null) voltageSlider.interactable = voltageAllowed;
            if (voltDownButton != null) voltDownButton.SetButtonEnabled(voltageAllowed);
            if (voltUpButton != null) voltUpButton.SetButtonEnabled(voltageAllowed);
            if (voltStep10Button != null) voltStep10Button.SetButtonEnabled(voltageAllowed);

            if (returnLeadsButton != null) returnLeadsButton.SetButtonEnabled(unlocked);
            if (resetStationButton != null) resetStationButton.SetButtonEnabled(unlocked);

            if (redPositiveLead != null) redPositiveLead.SetInteractive(unlocked);
            if (blackNegativeLead != null) blackNegativeLead.SetInteractive(unlocked);
            if (pTerminalSocket != null) pTerminalSocket.enabled = unlocked;
            if (nTerminalSocket != null) nTerminalSocket.enabled = unlocked;
        }

        private static string FormatCurrent(double amps)
        {
            double absA = Math.Abs(amps);
            if (absA >= 0.001)
            {
                return $"{amps * 1000.0:+0.00;-0.00;0.00} mA";
            }
            if (absA >= 1e-6)
            {
                return $"{amps * 1e6:+0.00;-0.00;0.00} uA";
            }
            if (absA >= 1e-12)
            {
                return $"{amps * 1e9:+0.00;-0.00;0.00} nA";
            }

            return "0.00 mA";
        }
    }
}
