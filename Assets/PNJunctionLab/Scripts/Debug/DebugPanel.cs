using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PNJunctionLab
{
    /// <summary>
    /// Editor/Development-build-only world-space debug console for jumping stations/steps,
    /// adjusting time scale, skipping dialogue, and inspecting live SemiconductorModel values.
    /// </summary>
    public class DebugPanel : MonoBehaviour
    {
        [SerializeField] private LessonManager lessonManager;
        [SerializeField] private DialogueSystem dialogueSystem;
        [SerializeField] private string[] stationIds = { "T", "1", "2", "3", "4", "5", "6", "A" };
        [SerializeField] private Button[] jumpStationButtons;
        [SerializeField] private Button nextStepButton;
        [SerializeField] private Button skipDialogueButton;
        [SerializeField] private Button resetStationButton;
        [SerializeField] private Slider timeScaleSlider;
        [SerializeField] private Button resetTimeScaleButton;
        [SerializeField] private TMP_Text timeScaleText;
        [SerializeField] private TMP_Text modelReadoutText;
        [SerializeField] private float telemetryRefreshInterval = 0.25f;

        private float nextTelemetryAt;

        private static bool IsDevEnabled => Application.isEditor || UnityEngine.Debug.isDebugBuild;

        private void Awake()
        {
            if (!IsDevEnabled)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (!IsDevEnabled)
            {
                return;
            }

            if (jumpStationButtons != null)
            {
                for (int i = 0; i < jumpStationButtons.Length; i++)
                {
                    if (jumpStationButtons[i] != null && stationIds != null && i < stationIds.Length)
                    {
                        string id = stationIds[i];
                        jumpStationButtons[i].onClick.AddListener(() => JumpToStation(id));
                    }
                }
            }

            if (timeScaleSlider != null)
            {
                timeScaleSlider.minValue = 0.25f;
                timeScaleSlider.maxValue = 3f;
                timeScaleSlider.SetValueWithoutNotify(Time.timeScale);
                timeScaleSlider.onValueChanged.AddListener(SetTimeScale);
            }

            nextStepButton?.onClick.AddListener(SkipStep);
            skipDialogueButton?.onClick.AddListener(SkipDialogue);
            resetStationButton?.onClick.AddListener(ResetActiveStation);
            resetTimeScaleButton?.onClick.AddListener(() => SetTimeScale(1f));

            UpdateTimeScaleLabel();
            RefreshModelReadout();
        }

        private void OnDisable()
        {
            if (jumpStationButtons != null)
            {
                for (int i = 0; i < jumpStationButtons.Length; i++)
                {
                    jumpStationButtons[i]?.onClick.RemoveAllListeners();
                }
            }

            nextStepButton?.onClick.RemoveAllListeners();
            skipDialogueButton?.onClick.RemoveAllListeners();
            resetStationButton?.onClick.RemoveAllListeners();
            resetTimeScaleButton?.onClick.RemoveAllListeners();
            timeScaleSlider?.onValueChanged.RemoveAllListeners();
        }

        private void Update()
        {
            if (!IsDevEnabled)
            {
                return;
            }

            if (Time.unscaledTime >= nextTelemetryAt)
            {
                nextTelemetryAt = Time.unscaledTime + telemetryRefreshInterval;
                RefreshModelReadout();
            }
        }

        public void JumpToStation(string stationId)
        {
            lessonManager?.JumpToStation(stationId);
            RefreshModelReadout();
        }

        public void SkipStep()
        {
            lessonManager?.SkipCurrentStep();
            RefreshModelReadout();
        }

        public void SkipDialogue()
        {
            dialogueSystem?.SkipSequence();
        }

        public void ResetActiveStation()
        {
            lessonManager?.ActiveStation?.ResetStation();
            RefreshModelReadout();
        }

        public void SetTimeScale(float value)
        {
            Time.timeScale = Mathf.Clamp(value, 0.25f, 3f);
            if (timeScaleSlider != null && Mathf.Abs(timeScaleSlider.value - Time.timeScale) > 0.01f)
            {
                timeScaleSlider.SetValueWithoutNotify(Time.timeScale);
            }

            UpdateTimeScaleLabel();
        }

        public void RefreshModelReadout()
        {
            if (modelReadoutText == null)
            {
                return;
            }

            const double t = SemiconductorModel.ReferenceTemperatureK;
            double vtMv = SemiconductorModel.ThermalVoltageV(t) * 1000.0;
            double siNi = SemiconductorModel.IntrinsicConcentrationCm3(SemiconductorMaterial.Silicon, t);
            double geNi = SemiconductorModel.IntrinsicConcentrationCm3(SemiconductorMaterial.Germanium, t);
            JunctionElectrostatics eq = SemiconductorModel.ComputeJunctionElectrostatics(
                SemiconductorMaterial.Silicon, 1e16, 1e16, 0.0, t);
            DiodeParameters siParams = SemiconductorModel.GetDiodeParameters(DiodeType.Silicon);
            double isSi = SemiconductorModel.DiodeSaturationCurrentA(DiodeType.Silicon, t);
            double iFwd = SemiconductorModel.ShockleyCurrentA(0.7, isSi, siParams.IdealityFactor, t);

            string activeStep = lessonManager != null && lessonManager.CurrentStep != null
                ? lessonManager.CurrentStep.StepId + " (Idle " + lessonManager.IdleElapsedSeconds.ToString("F0") + "s)"
                : "None";

            modelReadoutText.text = string.Format(
                "<b>DEBUG / MODEL TELEMETRY (300 K)</b>\n" +
                "Step: {0}\n" +
                "VT = {1:F2} mV | Si ni = {2:E2} | Ge ni = {3:E2} cm^-3\n" +
                "Si (1e16): V0 = {4:F3} V, W = {5:F3} um\n" +
                "Shockley Si @ 0.7 V: I = {6:E2} A",
                activeStep,
                vtMv,
                siNi,
                geNi,
                eq.BuiltInPotentialV,
                eq.DepletionWidthMicrometers,
                iFwd);
        }

        private void UpdateTimeScaleLabel()
        {
            if (timeScaleText != null)
            {
                timeScaleText.text = "Time Scale: " + Time.timeScale.ToString("F2") + "x";
            }
        }
    }
}
