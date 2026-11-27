using System.Text;
using TMPro;
using UnityEngine;

namespace PNJunctionLab
{
    /// <summary>
    /// Displays live station progress, current step objective, and station unlock status driven by LessonManager events.
    /// </summary>
    public class ProgressHUD : MonoBehaviour
    {
        [SerializeField] private LessonManager lessonManager;
        [SerializeField] private TMP_Text stationTitleText;
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private TMP_Text stepCounterText;
        [SerializeField] private TMP_Text stationStripText;

        private bool startupWelcomeActive;
        private string startupWelcomeTitleText;
        private string startupWelcomeSubtitleText;

        private void OnEnable()
        {
            if (lessonManager != null)
            {
                lessonManager.StepChanged += HandleStepChanged;
                lessonManager.ProgressChanged += HandleProgressChanged;
                lessonManager.ActiveStationChanged += HandleActiveStationChanged;
            }

            RefreshAll();
        }

        private void OnDisable()
        {
            if (lessonManager != null)
            {
                lessonManager.StepChanged -= HandleStepChanged;
                lessonManager.ProgressChanged -= HandleProgressChanged;
                lessonManager.ActiveStationChanged -= HandleActiveStationChanged;
            }
        }

        /// <summary>
        /// Temporarily displays a startup welcome message before resuming normal objective updates.
        /// </summary>
        public void SetStartupWelcome(bool active, string welcomeTitle, string welcomeSubtitle = null)
        {
            startupWelcomeActive = active;
            startupWelcomeTitleText = welcomeTitle;
            startupWelcomeSubtitleText = welcomeSubtitle;
            RefreshAll();
        }

        /// <summary>
        /// Refreshes all HUD labels immediately from LessonManager state.
        /// </summary>
        public void RefreshAll()
        {
            if (lessonManager == null)
            {
                return;
            }

            HandleStepChanged(lessonManager.CurrentStep, lessonManager.CurrentStepIndex, lessonManager.TotalSteps);
            HandleProgressChanged(lessonManager.CompletedStepsCount, lessonManager.TotalSteps);
        }

        private void HandleStepChanged(LessonStep step, int index, int total)
        {
            if (stationTitleText != null)
            {
                stationTitleText.text = startupWelcomeActive
                    ? "P-N JUNCTION VR LAB"
                    : (lessonManager.ActiveStation != null
                        ? lessonManager.ActiveStation.StationId + ". " + lessonManager.ActiveStation.StationTitle
                        : (step != null ? "Station " + step.StationId : "Lab Complete"));
            }

            if (objectiveText != null)
            {
                if (startupWelcomeActive)
                {
                    objectiveText.text = string.IsNullOrEmpty(startupWelcomeSubtitleText)
                        ? "<color=#38D8FF><b>" + startupWelcomeTitleText + "</b></color>"
                        : "<color=#38D8FF><b>" + startupWelcomeTitleText + "</b></color>\n" + startupWelcomeSubtitleText;
                }
                else if (step != null)
                {
                    objectiveText.text = "<b>" + step.Title + "</b>\n" + step.ObjectiveText;
                }
                else
                {
                    objectiveText.text = "<color=#5CFFA0><b>Simulation Complete!</b></color>\nAll lab stations & theory quiz completed!";
                }
            }

            RefreshStationStrip();
        }

        private void HandleProgressChanged(int completed, int total)
        {
            if (stepCounterText != null)
            {
                stepCounterText.text = "PROGRESS: " + completed + " / " + total + " STEPS";
            }

            RefreshStationStrip();
        }

        private void HandleActiveStationChanged(StationController station)
        {
            RefreshStationStrip();
        }

        private void RefreshStationStrip()
        {
            if (stationStripText == null || lessonManager == null || lessonManager.Stations == null)
            {
                return;
            }

            StringBuilder sb = new StringBuilder();
            var list = lessonManager.Stations;
            for (int i = 0; i < list.Count; i++)
            {
                StationController s = list[i];
                if (s == null)
                {
                    continue;
                }

                if (i > 0)
                {
                    sb.Append("  ");
                }

                if (s.IsCompleted)
                {
                    sb.Append("<color=#5CFFA0>[").Append(s.StationId).Append("]</color>");
                }
                else if (s.IsUnlocked)
                {
                    sb.Append("<color=#38D8FF><b>[").Append(s.StationId).Append("]</b></color>");
                }
                else
                {
                    sb.Append("<color=#6D7C8C>").Append(s.StationId).Append("</color>");
                }
            }

            stationStripText.text = sb.ToString();
        }
    }
}
