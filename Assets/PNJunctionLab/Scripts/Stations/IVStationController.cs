using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace PNJunctionLab
{
    /// <summary>
    /// Self-contained StationController for Station 6: Diode I-V Characteristics Analyzer.
    /// Supports 4 swappable diodes (Si, Ge, LED, Zener) in an XRSocketInteractor, source voltage sweep (-6.5..+3.5 V),
    /// Auto-Sweep toggle, temperature control (220..420 K), Ideal Shockley curve overlay toggle,
    /// live Reverse-Leakage inset zoom, and recording >= 8 operating points into an on-screen Data Table.
    /// </summary>
    public class IVStationController : StationController
    {
        private const float MinVoltageV = DiodeIVPlotterView.MinSweepV;
        private const float MaxVoltageV = DiodeIVPlotterView.MaxSweepV;
        private const float MinTempK = 220f;
        private const float MaxTempK = 420f;

        [Header("Navigation & Circuit Constants")]
        [SerializeField] private TeleportationAnchor stationAnchor;
        [SerializeField] private double seriesResistanceOhms = 100.0;
        [SerializeField] private float autoSweepSpeedVoltsPerSec = 3.2f;
        [SerializeField] private bool startUnlockedForStandalone;

        [Header("Diode Socket & Sample Modules")]
        [SerializeField] private XRSocketInteractor diodeSocket;
        [SerializeField] private SocketRingFeedback diodeSocketFeedback;
        [SerializeField] private DiodeSampleModule[] sampleModules;
        [SerializeField] private RespawnZone respawnZone;
        [SerializeField] private AudioSource audioSource;

        [Header("Sweep, Temperature & Table Controls")]
        [SerializeField] private RotaryKnobControl voltageKnob;
        [SerializeField] private Slider voltageSlider;
        [SerializeField] private PhysicalPushButton voltDownButton;
        [SerializeField] private PhysicalPushButton voltUpButton;
        [SerializeField] private PhysicalPushButton autoSweepButton;
        [SerializeField] private TMP_Text autoSweepButtonLabel;

        [SerializeField] private RotaryKnobControl temperatureKnob;
        [SerializeField] private Slider temperatureSlider;
        [SerializeField] private PhysicalPushButton tempDownButton;
        [SerializeField] private PhysicalPushButton tempUpButton;

        [SerializeField] private PhysicalPushButton idealOverlayButton;
        [SerializeField] private TMP_Text idealOverlayButtonLabel;
        [SerializeField] private PhysicalPushButton recordPointButton;
        [SerializeField] private PhysicalPushButton clearTableButton;
        [SerializeField] private PhysicalPushButton returnSamplesButton;
        [SerializeField] private PhysicalPushButton resetStationButton;

        [Header("Displays & Plotter")]
        [SerializeField] private DiodeIVPlotterView plotterView;
        [SerializeField] private TMP_Text analyzerStatusText;
        [SerializeField] private TMP_Text diodeSpecsText;
        [SerializeField] private TMP_Text liveReadingText;
        [SerializeField] private TMP_Text tempValueText;
        [SerializeField] private TMP_Text voltageValueText;

        public bool HasSocketedDiode => socketedDiode != null;
        public DiodeType ActiveDiodeType => socketedDiode != null ? socketedDiode.DiodeType : DiodeType.Silicon;
        public float CurrentSourceVoltageV { get; private set; } = 0.9f;
        public float CurrentTemperatureK { get; private set; } = 300f;
        public bool IsAutoSweepActive { get; private set; }
        public bool ShowIdealOverlay { get; private set; }
        public double CurrentDiodeVoltageV { get; private set; }
        public double CurrentCircuitCurrentA { get; private set; }
        public int RecordedPointsCount => recordedPoints.Count;
        public IReadOnlyList<DiodeIVDataPoint> RecordedPoints => recordedPoints;

        private readonly List<DiodeIVDataPoint> recordedPoints = new List<DiodeIVDataPoint>(16);
        private DiodeSampleModule socketedDiode;
        private bool suppressControlEvents;
        private float autoSweepDirection = 1f;
        private bool wasSweepTempMet;
        private bool voltageAdjustedSinceSocket;
        private bool tempAdjustedSinceSocket;

        protected override void Awake()
        {
            base.Awake();

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (diodeSocketFeedback != null)
            {
                diodeSocketFeedback.CustomValidityPredicate = selectable =>
                    selectable != null && selectable.transform.GetComponentInParent<DiodeSampleModule>() != null;
            }

            SetSourceVoltageInternal(0.9f, true);
            SetTemperatureInternal(300f, true);
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

            RefreshAnalyzer();
            if (plotterView != null)
            {
                plotterView.RefreshDataTable(recordedPoints);
            }
        }

        private void OnEnable()
        {
            if (stationAnchor != null) stationAnchor.teleporting.AddListener(HandleTeleportedHere);

            if (diodeSocket != null)
            {
                diodeSocket.selectEntered.AddListener(HandleDiodeSocketEntered);
                diodeSocket.selectExited.AddListener(HandleDiodeSocketExited);
            }

            if (voltageKnob != null) voltageKnob.ValueChanged += HandleVoltageKnobChanged;
            if (voltageSlider != null) voltageSlider.onValueChanged.AddListener(HandleVoltageSliderChanged);
            if (voltDownButton != null) voltDownButton.Pressed += HandleVoltDownPressed;
            if (voltUpButton != null) voltUpButton.Pressed += HandleVoltUpPressed;
            if (autoSweepButton != null) autoSweepButton.Pressed += ToggleAutoSweep;

            if (temperatureKnob != null) temperatureKnob.ValueChanged += HandleTemperatureKnobChanged;
            if (temperatureSlider != null) temperatureSlider.onValueChanged.AddListener(HandleTemperatureSliderChanged);
            if (tempDownButton != null) tempDownButton.Pressed += HandleTempDownPressed;
            if (tempUpButton != null) tempUpButton.Pressed += HandleTempUpPressed;

            if (idealOverlayButton != null) idealOverlayButton.Pressed += ToggleIdealOverlay;
            if (recordPointButton != null) recordPointButton.Pressed += RecordCurrentPoint;
            if (clearTableButton != null) clearTableButton.Pressed += ClearRecordedTable;
            if (returnSamplesButton != null) returnSamplesButton.Pressed += ReturnAllSamples;
            if (resetStationButton != null) resetStationButton.Pressed += ResetStation;
        }

        private void OnDisable()
        {
            if (stationAnchor != null) stationAnchor.teleporting.RemoveListener(HandleTeleportedHere);

            if (diodeSocket != null)
            {
                diodeSocket.selectEntered.RemoveListener(HandleDiodeSocketEntered);
                diodeSocket.selectExited.RemoveListener(HandleDiodeSocketExited);
            }

            if (voltageKnob != null) voltageKnob.ValueChanged -= HandleVoltageKnobChanged;
            if (voltageSlider != null) voltageSlider.onValueChanged.RemoveListener(HandleVoltageSliderChanged);
            if (voltDownButton != null) voltDownButton.Pressed -= HandleVoltDownPressed;
            if (voltUpButton != null) voltUpButton.Pressed -= HandleVoltUpPressed;
            if (autoSweepButton != null) autoSweepButton.Pressed -= ToggleAutoSweep;

            if (temperatureKnob != null) temperatureKnob.ValueChanged -= HandleTemperatureKnobChanged;
            if (temperatureSlider != null) temperatureSlider.onValueChanged.RemoveListener(HandleTemperatureSliderChanged);
            if (tempDownButton != null) tempDownButton.Pressed -= HandleTempDownPressed;
            if (tempUpButton != null) tempUpButton.Pressed -= HandleTempUpPressed;

            if (idealOverlayButton != null) idealOverlayButton.Pressed -= ToggleIdealOverlay;
            if (recordPointButton != null) recordPointButton.Pressed -= RecordCurrentPoint;
            if (clearTableButton != null) clearTableButton.Pressed -= ClearRecordedTable;
            if (returnSamplesButton != null) returnSamplesButton.Pressed -= ReturnAllSamples;
            if (resetStationButton != null) resetStationButton.Pressed -= ResetStation;
        }

        private void Update()
        {
            if (!IsUnlocked || !IsAutoSweepActive || socketedDiode == null)
            {
                return;
            }

            float nextV = CurrentSourceVoltageV + (autoSweepDirection * autoSweepSpeedVoltsPerSec * Time.deltaTime);
            if (nextV >= MaxVoltageV)
            {
                nextV = MaxVoltageV;
                autoSweepDirection = -1f;
            }
            else if (nextV <= MinVoltageV)
            {
                nextV = MinVoltageV;
                autoSweepDirection = 1f;
            }

            voltageAdjustedSinceSocket = true;
            SetSourceVoltageInternal(nextV, true);
            RefreshAnalyzer();
        }

        public override void SetUnlocked(bool unlocked)
        {
            base.SetUnlocked(unlocked);
            ApplyControlLockState();
        }

        public override void ResetStation()
        {
            base.ResetStation();
            IsAutoSweepActive = false;
            ShowIdealOverlay = false;
            wasSweepTempMet = false;
            voltageAdjustedSinceSocket = false;
            tempAdjustedSinceSocket = false;

            recordedPoints.Clear();
            ReturnAllSamplesInternal();
            SetSourceVoltageInternal(0.9f, true);
            SetTemperatureInternal(300f, true);
            ApplyControlLockState();
            RefreshAnalyzer();
            if (plotterView != null)
            {
                plotterView.RefreshDataTable(recordedPoints);
            }

            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.45f);
        }

        /// <summary>
        /// Sets the source voltage sweep value (-6.5..+3.5 V) directly.
        /// </summary>
        public void SetSourceVoltage(float volts)
        {
            if (!IsUnlocked) return;
            voltageAdjustedSinceSocket = true;
            SetSourceVoltageInternal(volts, true);
            RefreshAnalyzer();
        }

        /// <summary>
        /// Sets the analyzer chamber temperature (220..420 K) directly.
        /// </summary>
        public void SetTemperature(float kelvin)
        {
            if (!IsUnlocked) return;
            if (Mathf.Abs(Mathf.Clamp(kelvin, MinTempK, MaxTempK) - 300f) >= 20f)
            {
                tempAdjustedSinceSocket = true;
            }

            SetTemperatureInternal(kelvin, true);
            RefreshAnalyzer();
        }

        /// <summary>
        /// Toggles continuous Auto-Sweep across [-6.5 V .. +3.5 V].
        /// </summary>
        public void ToggleAutoSweep()
        {
            if (!IsUnlocked) return;
            IsAutoSweepActive = !IsAutoSweepActive;
            if (IsAutoSweepActive)
            {
                voltageAdjustedSinceSocket = true;
            }

            RefreshAnalyzer();
        }

        /// <summary>
        /// Toggles the Ideal Shockley (zero-series-resistance) comparison curve overlay.
        /// </summary>
        public void ToggleIdealOverlay()
        {
            if (!IsUnlocked) return;
            ShowIdealOverlay = !ShowIdealOverlay;
            RefreshAnalyzer();
        }

        /// <summary>
        /// Records the current diode (V_src, V_d, I) reading into the Station 6 Data Table.
        /// </summary>
        public void RecordCurrentPoint()
        {
            if (!IsUnlocked || socketedDiode == null)
            {
                if (IsUnlocked && socketedDiode == null)
                {
                    NotifyWrongAction("no_diode", "Insert a diode module (Si, Ge, LED, or Zener) into the test socket before recording points!");
                    ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.SocketInvalid, 0.55f);
                }
                return;
            }

            recordedPoints.Add(new DiodeIVDataPoint
            {
                DiodeShortName = socketedDiode.ShortLabel,
                TemperatureK = CurrentTemperatureK,
                SourceVoltageV = CurrentSourceVoltageV,
                DiodeVoltageV = CurrentDiodeVoltageV,
                CircuitCurrentA = CurrentCircuitCurrentA
            });

            if (plotterView != null)
            {
                plotterView.RefreshDataTable(recordedPoints);
            }

            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Success, 0.35f);

            if (recordedPoints.Count >= 8)
            {
                NotifyStepAction("station6.record_points");
                MarkCompleted();
            }
        }

        /// <summary>
        /// Clears all logged measurements from the Data Table.
        /// </summary>
        public void ClearRecordedTable()
        {
            if (!IsUnlocked) return;
            recordedPoints.Clear();
            if (plotterView != null)
            {
                plotterView.RefreshDataTable(recordedPoints);
            }
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.35f);
        }

        /// <summary>
        /// Returns all 4 diode sample modules safely onto their sample tray.
        /// </summary>
        public void ReturnAllSamples()
        {
            if (!IsUnlocked) return;
            ReturnAllSamplesInternal();
            RefreshAnalyzer();
        }

        private void HandleTeleportedHere(TeleportingEventArgs args)
        {
            if (IsUnlocked)
            {
                NotifyStepAction("station6.teleport");
            }
        }

        private void HandleDiodeSocketEntered(SelectEnterEventArgs args)
        {
            socketedDiode = args.interactableObject != null
                ? args.interactableObject.transform.GetComponentInParent<DiodeSampleModule>()
                : null;
            voltageAdjustedSinceSocket = false;
            tempAdjustedSinceSocket = false;
            wasSweepTempMet = false;

            if (IsUnlocked && socketedDiode != null)
            {
                NotifyStepAction("station6.socket_diode");
            }

            RefreshAnalyzer();
        }

        private void HandleDiodeSocketExited(SelectExitEventArgs args)
        {
            if (args.interactableObject != null
                && args.interactableObject.transform.GetComponentInParent<DiodeSampleModule>() == socketedDiode)
            {
                socketedDiode = null;
                RefreshAnalyzer();
            }
        }

        private void HandleVoltageKnobChanged(float knobPercent)
        {
            if (suppressControlEvents || !IsUnlocked) return;
            voltageAdjustedSinceSocket = true;
            float v = Mathf.Lerp(MinVoltageV, MaxVoltageV, Mathf.Clamp01(knobPercent / 100f));
            SetSourceVoltageInternal(v, false, syncSlider: true, syncKnob: false);
            RefreshAnalyzer();
        }

        private void HandleVoltageSliderChanged(float norm01)
        {
            if (suppressControlEvents || !IsUnlocked) return;
            voltageAdjustedSinceSocket = true;
            float v = Mathf.Lerp(MinVoltageV, MaxVoltageV, Mathf.Clamp01(norm01));
            SetSourceVoltageInternal(v, false, syncSlider: false, syncKnob: true);
            RefreshAnalyzer();
        }

        private void HandleVoltDownPressed() => SetSourceVoltage(CurrentSourceVoltageV - 0.5f);
        private void HandleVoltUpPressed() => SetSourceVoltage(CurrentSourceVoltageV + 0.5f);

        private void HandleTemperatureKnobChanged(float kelvin)
        {
            if (suppressControlEvents || !IsUnlocked) return;
            if (Mathf.Abs(kelvin - 300f) >= 20f) tempAdjustedSinceSocket = true;
            SetTemperatureInternal(kelvin, false, syncSlider: true, syncKnob: false);
            RefreshAnalyzer();
        }

        private void HandleTemperatureSliderChanged(float kelvin)
        {
            if (suppressControlEvents || !IsUnlocked) return;
            if (Mathf.Abs(kelvin - 300f) >= 20f) tempAdjustedSinceSocket = true;
            SetTemperatureInternal(kelvin, false, syncSlider: false, syncKnob: true);
            RefreshAnalyzer();
        }

        private void HandleTempDownPressed() => SetTemperature(CurrentTemperatureK - 25f);
        private void HandleTempUpPressed() => SetTemperature(CurrentTemperatureK + 25f);

        private void SetSourceVoltageInternal(float volts, bool syncBoth, bool syncSlider = true, bool syncKnob = true)
        {
            CurrentSourceVoltageV = Mathf.Clamp(volts, MinVoltageV, MaxVoltageV);
            float norm = Mathf.InverseLerp(MinVoltageV, MaxVoltageV, CurrentSourceVoltageV);

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

        private void SetTemperatureInternal(float kelvin, bool syncBoth, bool syncSlider = true, bool syncKnob = true)
        {
            CurrentTemperatureK = Mathf.Clamp(kelvin, MinTempK, MaxTempK);
            suppressControlEvents = true;
            if ((syncBoth || syncSlider) && temperatureSlider != null)
            {
                temperatureSlider.SetValueWithoutNotify(CurrentTemperatureK);
            }
            if ((syncBoth || syncKnob) && temperatureKnob != null)
            {
                temperatureKnob.SetValue(CurrentTemperatureK, false);
            }
            suppressControlEvents = false;
        }

        private void RefreshAnalyzer()
        {
            bool hasDiode = socketedDiode != null;
            DiodeType type = ActiveDiodeType;

            if (hasDiode)
            {
                SemiconductorModel.SolveSeriesDiodeCircuit(
                    type, CurrentSourceVoltageV, seriesResistanceOhms, CurrentTemperatureK,
                    out double vd, out double iA);
                CurrentDiodeVoltageV = vd;
                CurrentCircuitCurrentA = iA;
            }
            else
            {
                CurrentDiodeVoltageV = 0.0;
                CurrentCircuitCurrentA = 0.0;
            }

            if (plotterView != null)
            {
                plotterView.RefreshPlot(
                    hasDiode, type, CurrentSourceVoltageV, seriesResistanceOhms,
                    CurrentTemperatureK, ShowIdealOverlay);
            }

            if (voltageValueText != null)
            {
                voltageValueText.text = $"Sweep V_src: {CurrentSourceVoltageV:+0.00;-0.00;0.00} V";
            }

            if (tempValueText != null)
            {
                tempValueText.text = $"Temp: {CurrentTemperatureK:F0} K ({CurrentTemperatureK - 273.15f:+0;-0;0} C)";
            }

            if (autoSweepButtonLabel != null)
            {
                autoSweepButtonLabel.text = IsAutoSweepActive ? "AUTO-SWEEP: ON" : "AUTO-SWEEP: OFF";
            }

            if (idealOverlayButtonLabel != null)
            {
                idealOverlayButtonLabel.text = ShowIdealOverlay ? "SHOCKLEY: ON" : "SHOCKLEY: OFF";
            }

            if (!hasDiode)
            {
                if (analyzerStatusText != null)
                {
                    analyzerStatusText.text = "Drop a sample diode (Si, Ge, LED, or Zener) into the Analyzer Socket to plot I-V!";
                }
                if (diodeSpecsText != null)
                {
                    diodeSpecsText.text = "ACTIVE DIODE: None inserted (Test socket empty)";
                }
                if (liveReadingText != null)
                {
                    liveReadingText.text = $"V_src = {CurrentSourceVoltageV:+0.00;-0.00;0.00} V  |  Vd = --  |  I = --";
                }
                return;
            }

            DiodeParameters p = SemiconductorModel.GetDiodeParameters(type);
            double vKnee = plotterView != null
                ? plotterView.LatestKneeVoltageV
                : SemiconductorModel.ForwardVoltageAtCurrentV(type, 0.002, CurrentTemperatureK);
            double iSat = plotterView != null
                ? plotterView.LatestSaturationCurrentA
                : SemiconductorModel.DiodeSaturationCurrentA(type, CurrentTemperatureK);

            if (analyzerStatusText != null)
            {
                analyzerStatusText.text =
                    $"ANALYZING {socketedDiode.DisplayName} | Series R = {seriesResistanceOhms:F0} ohm | " +
                    $"{recordedPoints.Count}/8+ pts recorded";
            }

            if (diodeSpecsText != null)
            {
                diodeSpecsText.text =
                    $"Diode: {socketedDiode.ShortLabel} | Eg={p.EffectiveGapEv:F2} eV | n={p.IdealityFactor:F2} | " +
                    $"V_knee(T)={vKnee:F2} V | V_br=-{p.BreakdownVoltageV:F1} V | Is(T)={iSat:0.##E+0} A";
            }

            if (liveReadingText != null)
            {
                string iStr = Math.Abs(CurrentCircuitCurrentA) >= 0.001
                    ? $"{CurrentCircuitCurrentA * 1000.0:+0.00;-0.00;0.00} mA"
                    : Math.Abs(CurrentCircuitCurrentA) >= 1e-6
                        ? $"{CurrentCircuitCurrentA * 1e6:+0.00;-0.00;0.00} uA"
                        : $"{CurrentCircuitCurrentA * 1e9:+0.00;-0.00;0.00} nA";

                liveReadingText.text =
                    $"V_src = {CurrentSourceVoltageV:+0.00;-0.00;0.00} V  |  " +
                    $"Vd = {CurrentDiodeVoltageV:+0.000;-0.000;0.000} V  |  I = {iStr}";
            }

            bool sweepTempMetNow = IsUnlocked && hasDiode && voltageAdjustedSinceSocket && tempAdjustedSinceSocket;
            if (sweepTempMetNow && !wasSweepTempMet)
            {
                NotifyStepAction("station6.sweep_temp");
            }
            wasSweepTempMet = sweepTempMetNow;
        }

        private void ReturnAllSamplesInternal()
        {
            if (diodeSocket != null && diodeSocket.hasSelection && diodeSocket.interactionManager != null
                && diodeSocket.interactablesSelected.Count > 0)
            {
                var sel = diodeSocket.interactablesSelected[0];
                if (sel != null)
                {
                    diodeSocket.interactionManager.SelectExit(diodeSocket, sel);
                }
            }

            socketedDiode = null;
            if (respawnZone != null)
            {
                respawnZone.RespawnAllManagedItems();
            }
            else if (sampleModules != null)
            {
                for (int i = 0; i < sampleModules.Length; i++)
                {
                    if (sampleModules[i] != null) sampleModules[i].ReturnToTray();
                }
            }
        }

        private void ApplyControlLockState()
        {
            bool unlocked = IsUnlocked;

            if (voltageKnob != null) voltageKnob.SetInteractive(unlocked);
            if (voltageSlider != null) voltageSlider.interactable = unlocked;
            if (voltDownButton != null) voltDownButton.SetButtonEnabled(unlocked);
            if (voltUpButton != null) voltUpButton.SetButtonEnabled(unlocked);
            if (autoSweepButton != null) autoSweepButton.SetButtonEnabled(unlocked);

            if (temperatureKnob != null) temperatureKnob.SetInteractive(unlocked);
            if (temperatureSlider != null) temperatureSlider.interactable = unlocked;
            if (tempDownButton != null) tempDownButton.SetButtonEnabled(unlocked);
            if (tempUpButton != null) tempUpButton.SetButtonEnabled(unlocked);

            if (idealOverlayButton != null) idealOverlayButton.SetButtonEnabled(unlocked);
            if (recordPointButton != null) recordPointButton.SetButtonEnabled(unlocked);
            if (clearTableButton != null) clearTableButton.SetButtonEnabled(unlocked);
            if (returnSamplesButton != null) returnSamplesButton.SetButtonEnabled(unlocked);
            if (resetStationButton != null) resetStationButton.SetButtonEnabled(unlocked);

            if (diodeSocket != null) diodeSocket.enabled = unlocked;
            if (sampleModules != null)
            {
                for (int i = 0; i < sampleModules.Length; i++)
                {
                    if (sampleModules[i] != null) sampleModules[i].SetInteractive(unlocked);
                }
            }
        }
    }
}
