using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace PNJunctionLab
{
    /// <summary>
    /// Self-contained controller for Station 1: Intrinsic Silicon.
    /// Reads all constants and carrier physics strictly from SemiconductorModel, coordinates the
    /// grabbable Silicon Wafer + Atomic Lens lattice reveal, 0-500 K temperature knob, Si/Ge toggle,
    /// live meters, energy band diagram, and LessonManager steps.
    /// </summary>
    public class IntrinsicSiliconStationController : StationController
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("Station Navigation & Props")]
        [SerializeField] private TeleportationAnchor stationAnchor;
        [SerializeField] private Renderer stageWaferRenderer;
        [SerializeField] private MicroscopeViewController microscopeController;

        [Header("Controls")]
        [SerializeField] private RotaryKnobControl temperatureKnob;
        [SerializeField] private Slider temperatureSlider;
        [SerializeField] private PhysicalPushButton tempUpButton;
        [SerializeField] private PhysicalPushButton tempDownButton;
        [SerializeField] private PhysicalPushButton materialToggleButton;
        [SerializeField] private PhysicalPushButton resetStationButton;
        [SerializeField] private GameObject scanTargetRing;

        [Header("Visualizers & Displays")]
        [SerializeField] private LatticeHologramController latticeHologram;
        [SerializeField] private ThermalChamberCubeController thermalChamberCube;
        [SerializeField] private EnergyBandDiagramView bandDiagramView;
        [SerializeField] private TMP_Text materialHeaderText;
        [SerializeField] private TMP_Text temperatureReadoutText;
        [SerializeField] private TMP_Text carrierReadoutText;
        [SerializeField] private TMP_Text bandGapReadoutText;
        [SerializeField] private TMP_Text conductivityReadoutText;
        [SerializeField] private TMP_Text latticeSummaryText;
        [SerializeField] private TMP_Text waferPromptText;
        [SerializeField] private TMP_Text materialToggleLabel;
        [SerializeField] private TMP_Text groupIvChartText;
        [SerializeField] private AudioSource audioSource;

        [Header("Standalone / Runtime Defaults")]
        [SerializeField] private bool startUnlockedForStandalone;
        [SerializeField] private float initialTemperatureK = 0f;
        [SerializeField] private float stepTempDeltaK = 50f;
        [SerializeField] private float heatCompletionThresholdK = 250f;

        [Header("Appearance")]
        [SerializeField] private Color siliconWaferColor = new Color(0.24f, 0.30f, 0.39f, 1f);
        [SerializeField] private Color germaniumWaferColor = new Color(0.36f, 0.33f, 0.42f, 1f);

        public SemiconductorMaterial CurrentMaterial { get; private set; } = SemiconductorMaterial.Silicon;
        public float TemperatureK { get; private set; }
        public bool IsLatticeRevealed { get; private set; }
        public CarrierConcentrations CurrentConcentrations { get; private set; }

        private MaterialPropertyBlock waferPropBlock;
        private bool reportedTeleport;
        private bool reportedLensReveal;
        private float lastHeatReportTemp = -1f;

        protected override void Awake()
        {
            base.Awake();
            waferPropBlock = new MaterialPropertyBlock();
            TemperatureK = Mathf.Clamp(initialTemperatureK, 0f, 500f);
            CurrentMaterial = SemiconductorMaterial.Silicon;
            IsLatticeRevealed = false;

            if (temperatureKnob != null)
            {
                temperatureKnob.SetValue(TemperatureK, false);
            }

            if (temperatureSlider != null)
            {
                temperatureSlider.minValue = 0f;
                temperatureSlider.maxValue = 500f;
                temperatureSlider.SetValueWithoutNotify(TemperatureK);
            }

            ApplyPhysicsAndRefreshViews();
            SetUnlocked(startUnlockedForStandalone);
        }

        private void OnEnable()
        {
            if (stationAnchor != null)
            {
                stationAnchor.teleporting.AddListener(HandleTeleporting);
            }

            if (temperatureKnob != null)
            {
                temperatureKnob.ValueChanged += HandleKnobValueChanged;
            }

            if (temperatureSlider != null)
            {
                temperatureSlider.onValueChanged.AddListener(HandleSliderValueChanged);
            }

            if (tempUpButton != null) tempUpButton.Pressed += HandleTempUpPressed;
            if (tempDownButton != null) tempDownButton.Pressed += HandleTempDownPressed;
            if (materialToggleButton != null) materialToggleButton.Pressed += HandleMaterialTogglePressed;
            if (resetStationButton != null) resetStationButton.Pressed += ResetStation;

            if (microscopeController != null)
            {
                microscopeController.Entered += HandleMicroscopeEntered;
                microscopeController.TemperatureDeltaRequested += HandleMicroscopeTempDelta;
                microscopeController.MaterialToggleRequested += HandleMaterialTogglePressed;
            }
        }

        private void OnDisable()
        {
            if (stationAnchor != null)
            {
                stationAnchor.teleporting.RemoveListener(HandleTeleporting);
            }

            if (temperatureKnob != null)
            {
                temperatureKnob.ValueChanged -= HandleKnobValueChanged;
            }

            if (temperatureSlider != null)
            {
                temperatureSlider.onValueChanged.RemoveListener(HandleSliderValueChanged);
            }

            if (tempUpButton != null) tempUpButton.Pressed -= HandleTempUpPressed;
            if (tempDownButton != null) tempDownButton.Pressed -= HandleTempDownPressed;
            if (materialToggleButton != null) materialToggleButton.Pressed -= HandleMaterialTogglePressed;
            if (resetStationButton != null) resetStationButton.Pressed -= ResetStation;

            if (microscopeController != null)
            {
                microscopeController.Entered -= HandleMicroscopeEntered;
                microscopeController.TemperatureDeltaRequested -= HandleMicroscopeTempDelta;
                microscopeController.MaterialToggleRequested -= HandleMaterialTogglePressed;
            }
        }

        public override void SetUnlocked(bool unlocked)
        {
            base.SetUnlocked(unlocked);
            ApplyInteractiveStates(unlocked);
            RefreshMetersAndCharts();
        }

        public override void ResetStation()
        {
            if (microscopeController != null && microscopeController.IsInMicroscopeView)
            {
                microscopeController.ExitMicroscopeView();
            }

            TemperatureK = Mathf.Clamp(initialTemperatureK, 0f, 500f);
            CurrentMaterial = SemiconductorMaterial.Silicon;
            IsLatticeRevealed = false;
            reportedTeleport = false;
            reportedLensReveal = false;
            lastHeatReportTemp = -1f;

            if (temperatureKnob != null)
            {
                temperatureKnob.SetValue(TemperatureK, false);
            }

            if (temperatureSlider != null)
            {
                temperatureSlider.SetValueWithoutNotify(TemperatureK);
            }

            if (latticeHologram != null)
            {
                latticeHologram.SetRevealed(false);
            }

            base.ResetStation();
            ApplyPhysicsAndRefreshViews();
        }

        public override Transform GetHintTarget(int hintTargetIndex)
        {
            switch (hintTargetIndex)
            {
                case 0:
                    return stationAnchor != null ? stationAnchor.transform : base.GetHintTarget(hintTargetIndex);
                case 1:
                    return microscopeController != null ? microscopeController.transform : base.GetHintTarget(hintTargetIndex);
                case 2:
                    return temperatureKnob != null ? temperatureKnob.transform : base.GetHintTarget(hintTargetIndex);
                case 3:
                    return materialToggleButton != null ? materialToggleButton.transform : base.GetHintTarget(hintTargetIndex);
                default:
                    return base.GetHintTarget(hintTargetIndex);
            }
        }

        /// <summary>
        /// Sets temperature explicitly (used by knob, +/- step buttons, and tests).
        /// </summary>
        public void SetTemperatureK(float tempK)
        {
            if (!IsUnlocked)
            {
                return;
            }

            TemperatureK = Mathf.Clamp(tempK, 0f, 500f);
            if (temperatureKnob != null && Mathf.Abs(temperatureKnob.Value - TemperatureK) > 0.05f)
            {
                temperatureKnob.SetValue(TemperatureK, false);
            }

            if (temperatureSlider != null && Mathf.Abs(temperatureSlider.value - TemperatureK) > 0.05f)
            {
                temperatureSlider.SetValueWithoutNotify(TemperatureK);
            }

            ApplyPhysicsAndRefreshViews();
            EvaluateTemperatureMilestone();
        }

        /// <summary>
        /// Sets active intrinsic semiconductor material (Silicon vs Germanium) and updates all displays from SemiconductorModel.
        /// </summary>
        public void SetMaterial(SemiconductorMaterial material)
        {
            if (!IsUnlocked)
            {
                return;
            }

            CurrentMaterial = material;
            ApplyPhysicsAndRefreshViews();

            if (CurrentMaterial == SemiconductorMaterial.Germanium)
            {
                ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Success, 0.55f);
                NotifyStepAction("station1.toggle_ge");
            }
        }

        /// <summary>
        /// Reveals the crystal lattice inside the optical microscope chamber and marks step inspection.
        /// </summary>
        public void RevealLattice()
        {
            if (!IsUnlocked)
            {
                return;
            }

            bool firstReveal = !IsLatticeRevealed;
            IsLatticeRevealed = true;
            if (latticeHologram != null)
            {
                latticeHologram.SetRevealed(true);
                latticeHologram.SetLatticeState(CurrentMaterial, TemperatureK, CurrentConcentrations);
            }

            if (scanTargetRing != null)
            {
                scanTargetRing.SetActive(false);
            }

            if (firstReveal)
            {
                ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Success, 0.65f);
            }

            RefreshMetersAndCharts();

            if (!reportedLensReveal)
            {
                reportedLensReveal = true;
                NotifyStepAction("station1.microscope_enter");
            }

            EvaluateTemperatureMilestone();
        }

        private void HandleMicroscopeEntered()
        {
            // Allow students who walked up with WASD / Continuous Move (without teleporting onto Pad 1)
            // to advance past Step 6 smoothly as soon as they inspect the Microscope.
            if (IsUnlocked && !reportedTeleport)
            {
                reportedTeleport = true;
                NotifyStepAction("station1.teleport");
            }

            RevealLattice();
        }

        private void HandleMicroscopeTempDelta(float deltaK)
        {
            if (!IsUnlocked) return;
            SetTemperatureK(TemperatureK + deltaK);
        }

        private void HandleTeleporting(TeleportingEventArgs args)
        {
            if (!IsUnlocked || reportedTeleport)
            {
                return;
            }

            reportedTeleport = true;
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Success, 0.5f);
            NotifyStepAction("station1.teleport");
        }

        private void HandleKnobValueChanged(float value)
        {
            SetTemperatureK(value);
        }

        private void HandleSliderValueChanged(float value)
        {
            SetTemperatureK(value);
        }

        private void HandleTempUpPressed()
        {
            if (!IsUnlocked) return;
            SetTemperatureK(TemperatureK + stepTempDeltaK);
        }

        private void HandleTempDownPressed()
        {
            if (!IsUnlocked) return;
            SetTemperatureK(TemperatureK - stepTempDeltaK);
        }

        private void HandleMaterialTogglePressed()
        {
            if (!IsUnlocked) return;
            SemiconductorMaterial next = CurrentMaterial == SemiconductorMaterial.Silicon
                ? SemiconductorMaterial.Germanium
                : SemiconductorMaterial.Silicon;
            SetMaterial(next);
        }

        private void EvaluateTemperatureMilestone()
        {
            if (!IsUnlocked)
            {
                return;
            }

            if (IsLatticeRevealed && TemperatureK >= heatCompletionThresholdK && CurrentConcentrations.IntrinsicNiCm3 > 0.0)
            {
                if (lastHeatReportTemp < heatCompletionThresholdK)
                {
                    ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Success, 0.6f);
                }

                lastHeatReportTemp = TemperatureK;
                NotifyStepAction("station1.heat_carriers");
            }
        }

        private void ApplyInteractiveStates(bool unlocked)
        {
            if (microscopeController != null) microscopeController.SetMicroscopeUnlocked(unlocked);
            if (temperatureKnob != null) temperatureKnob.SetInteractive(unlocked);
            if (temperatureSlider != null) temperatureSlider.interactable = unlocked;
            if (thermalChamberCube != null) thermalChamberCube.SetChamberUnlocked(unlocked);

            SetButtonInteractable(tempUpButton, unlocked);
            SetButtonInteractable(tempDownButton, unlocked);
            SetButtonInteractable(materialToggleButton, unlocked);
            SetButtonInteractable(resetStationButton, unlocked);

            if (scanTargetRing != null)
            {
                scanTargetRing.SetActive(unlocked && !IsLatticeRevealed);
            }

            if (!unlocked && latticeHologram != null)
            {
                latticeHologram.SetRevealed(false);
            }
        }

        private static void SetButtonInteractable(PhysicalPushButton btn, bool enabledState)
        {
            if (btn == null) return;
            btn.SetButtonEnabled(enabledState);
        }

        private void ApplyPhysicsAndRefreshViews()
        {
            CurrentConcentrations = SemiconductorModel.ComputeIntrinsic(CurrentMaterial, TemperatureK);

            if (latticeHologram != null)
            {
                latticeHologram.SetLatticeState(CurrentMaterial, TemperatureK, CurrentConcentrations);
            }

            if (thermalChamberCube != null)
            {
                thermalChamberCube.SetThermalState(CurrentMaterial, TemperatureK, CurrentConcentrations);
            }

            if (bandDiagramView != null)
            {
                bandDiagramView.SetBandState(CurrentMaterial, TemperatureK, CurrentConcentrations);
            }

            UpdateWaferMaterialTint();
            RefreshMetersAndCharts();
        }

        private void UpdateWaferMaterialTint()
        {
            if (waferPropBlock == null)
            {
                waferPropBlock = new MaterialPropertyBlock();
            }

            Color c = CurrentMaterial == SemiconductorMaterial.Germanium ? germaniumWaferColor : siliconWaferColor;
            ApplyWaferRendererTint(stageWaferRenderer, c);
        }

        private void ApplyWaferRendererTint(Renderer r, Color c)
        {
            if (r == null) return;
            r.GetPropertyBlock(waferPropBlock);
            waferPropBlock.SetColor(BaseColorId, c);
            waferPropBlock.SetColor(EmissionColorId, c * (IsUnlocked ? 0.12f : 0.02f));
            r.SetPropertyBlock(waferPropBlock);
        }

        private void RefreshMetersAndCharts()
        {
            MaterialProperties props = SemiconductorModel.GetMaterialProperties(CurrentMaterial);
            MaterialProperties siProps = SemiconductorModel.GetMaterialProperties(SemiconductorMaterial.Silicon);
            MaterialProperties geProps = SemiconductorModel.GetMaterialProperties(SemiconductorMaterial.Germanium);

            if (materialHeaderText != null)
            {
                string matFull = CurrentMaterial == SemiconductorMaterial.Germanium ? "GERMANIUM (Ge)" : "SILICON (Si)";
                materialHeaderText.text = IsUnlocked
                    ? matFull + "  |  GROUP IV INTRINSIC"
                    : matFull + "  |  <color=#8896A6>[LOCKED - COMPLETE TUTORIAL]</color>";
            }

            if (temperatureReadoutText != null)
            {
                float celsius = TemperatureK - 273.15f;
                string stateTag = TemperatureK <= 0.01f
                    ? "<color=#8FA6B8>0 K Insulator</color>"
                    : "<color=#5CFFA0>Thermal Generation</color>";
                temperatureReadoutText.text = "<b>Temperature (T):</b>  " + TemperatureK.ToString("F0") + " K  (" + celsius.ToString("F0") + " C)  " + stateTag;
            }

            if (carrierReadoutText != null)
            {
                carrierReadoutText.text = CurrentConcentrations.IntrinsicNiCm3 <= 0.0
                    ? "<b>Carriers (n = p = ni):</b>  0 cm^-3  (All bonds intact)"
                    : "<b>Carriers (n = p = ni):</b>  " + CurrentConcentrations.IntrinsicNiCm3.ToString("E2") + " cm^-3";
            }

            if (bandGapReadoutText != null)
            {
                bandGapReadoutText.text = "<b>Bandgap (Eg):</b>  " + props.BandGapEv.ToString("F2") + " eV  |  4 valence e-/atom";
            }

            if (conductivityReadoutText != null)
            {
                conductivityReadoutText.text = "<b>Conductivity (sigma):</b>  " + CurrentConcentrations.ConductivitySPerCm.ToString("E2") + " S/cm";
            }

            if (latticeSummaryText != null)
            {
                int totalBonds = latticeHologram != null ? latticeHologram.TotalBondsCount : 16;
                int pairs = IsLatticeRevealed && latticeHologram != null ? latticeHologram.ActiveCarrierPairs : 0;
                int intact = Mathf.Max(0, totalBonds - pairs);
                latticeSummaryText.text = IsLatticeRevealed
                    ? "<b>Microscope Crystal:</b> " + intact + "/" + totalBonds + " bonds intact | Active pairs: " + pairs + " <color=#99B4C9>(Not to scale)</color>"
                    : "<b>Microscope:</b> <color=#FFCE66>Look through Microscope Eyepiece to inspect lattice & e-/h+</color>";

                if (microscopeController != null)
                {
                    string obj = "Objective: " + (!IsUnlocked
                        ? "Unlock Station 1 first"
                        : (TemperatureK < heatCompletionThresholdK
                            ? "Raise Temperature >= 250 K (+50 K button) to watch bonds break"
                            : (CurrentMaterial == SemiconductorMaterial.Silicon
                                ? "Toggle Material (Si -> Ge) to compare narrower 0.67 eV gap"
                                : "Observe e-/h+ pairs & ray-hover atoms — click EXIT MICROSCOPE anytime")));
                    microscopeController.SetTelemetry(CurrentMaterial, TemperatureK, CurrentConcentrations, intact, totalBonds, obj);
                }
            }

            if (waferPromptText != null)
            {
                if (!IsUnlocked)
                {
                    waferPromptText.text = "LOCKED: Finish Tutorial (or Debug 'Go 1')";
                }
                else
                {
                    waferPromptText.text = IsLatticeRevealed
                        ? "Look Into Microscope Anytime (Trigger / Click)"
                        : "Interact With Microscope To View Atomic Lattice";
                }
            }

            if (materialToggleLabel != null)
            {
                materialToggleLabel.text = CurrentMaterial == SemiconductorMaterial.Silicon
                    ? "MAT: Si -> Ge"
                    : "MAT: Ge -> Si";
            }

            if (groupIvChartText != null)
            {
                string siHighlight = CurrentMaterial == SemiconductorMaterial.Silicon ? "<color=#38D8FF><b>> " : "<color=#C8D6E4>";
                string geHighlight = CurrentMaterial == SemiconductorMaterial.Germanium ? "<color=#38D8FF><b>> " : "<color=#C8D6E4>";
                string siEnd = CurrentMaterial == SemiconductorMaterial.Silicon ? "</b></color>" : "</color>";
                string geEnd = CurrentMaterial == SemiconductorMaterial.Germanium ? "</b></color>" : "</color>";

                groupIvChartText.text =
                    "<b>PERIODIC TABLE — GROUP IV (4 VALENCE e-)</b>\n" +
                    "<color=#9CB0C4>  C (6)   Carbon   — Diamond insulator (wide gap)</color>\n" +
                    siHighlight + "Si (14)  Silicon  — Eg = " + siProps.BandGapEv.ToString("F2") + " eV | ni(300K) = " + siProps.IntrinsicConcentration300KCm3.ToString("E1") + siEnd + "\n" +
                    geHighlight + "Ge (32)  Germanium — Eg = " + geProps.BandGapEv.ToString("F2") + " eV | ni(300K) = " + geProps.IntrinsicConcentration300KCm3.ToString("E1") + siEnd;
            }
        }
    }
}
