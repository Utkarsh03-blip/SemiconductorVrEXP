using System;
using System.Collections.Generic;
using UnityEngine;

namespace PNJunctionLab
{
    /// <summary>
    /// Runs ScriptableObject LessonSteps in sequence, drives station unlocking, NPC navigation/hints,
    /// and notifies ProgressHUD and LabTablet via events.
    /// </summary>
    public class LessonManager : MonoBehaviour
    {
        [SerializeField] private List<LessonStep> steps = new List<LessonStep>();
        [SerializeField] private StationController[] stations;
        [SerializeField] private DialogueSystem dialogueSystem;
        [SerializeField] private NPCController npcController;
        [SerializeField] private float firstHintDelaySeconds = 20f;
        [SerializeField] private float strongHintDelaySeconds = 45f;

        public event Action<LessonStep, int, int> StepChanged;
        public event Action<int, int> ProgressChanged;
        public event Action<StationController> ActiveStationChanged;
        public event Action<LessonStep, bool> HintTriggered;
        public event Action LabCompleted;

        public int CurrentStepIndex { get; private set; } = -1;
        public int TotalSteps => steps != null ? steps.Count : 0;
        public int CompletedStepsCount { get; private set; }
        public LessonStep CurrentStep =>
            (steps != null && CurrentStepIndex >= 0 && CurrentStepIndex < steps.Count) ? steps[CurrentStepIndex] : null;
        public StationController ActiveStation { get; private set; }
        public IReadOnlyList<LessonStep> Steps => steps;
        public IReadOnlyList<StationController> Stations => stations;
        public float IdleElapsedSeconds => idleElapsedSeconds;

        private readonly Dictionary<string, StationController> stationLookup =
            new Dictionary<string, StationController>(StringComparer.OrdinalIgnoreCase);

        private float idleElapsedSeconds;
        private bool sentFirstHint;
        private bool sentStrongHint;

        private void Awake()
        {
            BuildStationLookup();
        }

        private void OnEnable()
        {
            SubscribeStations(true);
        }

        private void OnDisable()
        {
            SubscribeStations(false);
        }

        private void Start()
        {
            if (steps != null && steps.Count > 0)
            {
                ActivateStep(0, true);
            }
            else
            {
                UnlockSingleStation("T");
            }
        }

        private void Update()
        {
            LessonStep step = CurrentStep;
            if (step == null)
            {
                return;
            }

            idleElapsedSeconds += Time.deltaTime;

            if (!sentFirstHint && idleElapsedSeconds >= firstHintDelaySeconds)
            {
                sentFirstHint = true;
                ShowHint(step, false);
            }

            if (!sentStrongHint && idleElapsedSeconds >= strongHintDelaySeconds)
            {
                sentStrongHint = true;
                ShowHint(step, true);
            }
        }

        /// <summary>
        /// Immediately surfaces a hint for the active step (called by LabTablet Hint button).
        /// </summary>
        public void RequestHintNow()
        {
            LessonStep step = CurrentStep;
            if (step == null)
            {
                return;
            }

            bool useStrong = sentFirstHint && !sentStrongHint;
            sentFirstHint = true;
            if (useStrong)
            {
                sentStrongHint = true;
            }

            ShowHint(step, useStrong);
        }

        /// <summary>
        /// Advances immediately to the next step (for DebugPanel testing).
        /// </summary>
        public void SkipCurrentStep()
        {
            if (steps == null || steps.Count == 0)
            {
                return;
            }

            int next = CurrentStepIndex + 1;
            CompletedStepsCount = Mathf.Min(steps.Count, Mathf.Max(CompletedStepsCount, next));
            if (next < steps.Count)
            {
                ActivateStep(next, true);
            }
            else
            {
                CompleteLab();
            }
        }

        /// <summary>
        /// Jumps directly to the first step of the given station ID (for DebugPanel).
        /// </summary>
        public void JumpToStation(string stationId)
        {
            if (string.IsNullOrEmpty(stationId))
            {
                return;
            }

            if (stationLookup.TryGetValue(stationId, out StationController targetStation))
            {
                targetStation.SetUnlocked(true);
            }

            if (steps != null)
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    if (steps[i] != null && string.Equals(steps[i].StationId, stationId, StringComparison.OrdinalIgnoreCase))
                    {
                        ActivateStep(i, true);
                        return;
                    }
                }
            }

            if (targetStation != null)
            {
                ActiveStation = targetStation;
                npcController?.MoveToStation(targetStation.NpcStandPoint);
                ActiveStationChanged?.Invoke(targetStation);
            }
        }

        /// <summary>
        /// Jumps to an arbitrary step index (for DebugPanel).
        /// </summary>
        public void JumpToStep(int stepIndex)
        {
            if (steps == null || stepIndex < 0 || stepIndex >= steps.Count)
            {
                return;
            }

            ActivateStep(stepIndex, true);
        }

        /// <summary>
        /// Skips active dialogue (for DebugPanel).
        /// </summary>
        public void SkipDialogue()
        {
            dialogueSystem?.SkipSequence();
        }

        private void BuildStationLookup()
        {
            stationLookup.Clear();
            if (stations == null)
            {
                return;
            }

            for (int i = 0; i < stations.Length; i++)
            {
                StationController s = stations[i];
                if (s != null && !string.IsNullOrEmpty(s.StationId))
                {
                    stationLookup[s.StationId] = s;
                }
            }
        }

        private void SubscribeStations(bool subscribe)
        {
            if (stations == null)
            {
                return;
            }

            for (int i = 0; i < stations.Length; i++)
            {
                StationController s = stations[i];
                if (s == null)
                {
                    continue;
                }

                if (subscribe)
                {
                    s.StepActionCompleted += HandleStepActionCompleted;
                    s.WrongActionReported += HandleWrongActionReported;
                }
                else
                {
                    s.StepActionCompleted -= HandleStepActionCompleted;
                    s.WrongActionReported -= HandleWrongActionReported;
                }
            }
        }

        private void HandleStepActionCompleted(StationController station, string actionId)
        {
            LessonStep step = CurrentStep;
            if (step == null)
            {
                return;
            }

            if (!string.Equals(step.StationId, station.StationId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!string.Equals(step.RequiredActionId, actionId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            idleElapsedSeconds = 0f;
            sentFirstHint = false;
            sentStrongHint = false;

            npcController?.TriggerNod();
            if (step.CompletionDialogue != null)
            {
                dialogueSystem?.ShowLine(step.CompletionDialogue);
            }

            int nextIndex = CurrentStepIndex + 1;
            CompletedStepsCount = Mathf.Max(CompletedStepsCount, nextIndex);

            if (nextIndex < steps.Count)
            {
                bool changingStation = !string.Equals(steps[nextIndex].StationId, step.StationId, StringComparison.OrdinalIgnoreCase);
                if (changingStation)
                {
                    station.MarkCompleted();
                }

                ActivateStep(nextIndex, step.CompletionDialogue == null);
            }
            else
            {
                station.MarkCompleted();
                CompleteLab();
            }
        }

        private void HandleWrongActionReported(StationController station, string actionId, string feedbackMessage)
        {
            idleElapsedSeconds = 0f;
            if (!string.IsNullOrEmpty(feedbackMessage))
            {
                dialogueSystem?.ShowAdHoc(feedbackMessage, NPCEmotion.Warning);
            }
        }

        private void ActivateStep(int index, bool playIntroDialogue)
        {
            CurrentStepIndex = index;
            idleElapsedSeconds = 0f;
            sentFirstHint = false;
            sentStrongHint = false;

            LessonStep step = CurrentStep;
            if (step == null)
            {
                return;
            }

            EnsureStationUnlockedUpTo(step.StationId);
            if (stationLookup.TryGetValue(step.StationId, out StationController station))
            {
                if (ActiveStation != station)
                {
                    ActiveStation = station;
                    npcController?.MoveToStation(station.NpcStandPoint);
                    ActiveStationChanged?.Invoke(station);
                }
            }

            if (playIntroDialogue && step.IntroDialogue != null)
            {
                dialogueSystem?.ShowLine(step.IntroDialogue);
            }

            StepChanged?.Invoke(step, CurrentStepIndex, TotalSteps);
            ProgressChanged?.Invoke(CompletedStepsCount, TotalSteps);
        }

        private void CompleteLab()
        {
            CurrentStepIndex = TotalSteps;
            CompletedStepsCount = TotalSteps;
            StepChanged?.Invoke(null, TotalSteps, TotalSteps);
            ProgressChanged?.Invoke(TotalSteps, TotalSteps);
            LabCompleted?.Invoke();
        }

        private void ShowHint(LessonStep step, bool strong)
        {
            DialogueLine hintLine = strong && step.Hint45sDialogue != null
                ? step.Hint45sDialogue
                : step.Hint20sDialogue;

            if (hintLine != null)
            {
                dialogueSystem?.ShowLine(hintLine);
            }

            if (strong && npcController != null && ActiveStation != null)
            {
                Transform target = ActiveStation.GetHintTarget(step.HintTargetIndex);
                npcController.PointAt(target);
            }

            HintTriggered?.Invoke(step, strong);
        }

        private void EnsureStationUnlockedUpTo(string activeStationId)
        {
            if (stations == null)
            {
                return;
            }

            bool foundActive = false;
            for (int i = 0; i < stations.Length; i++)
            {
                StationController s = stations[i];
                if (s == null)
                {
                    continue;
                }

                if (!foundActive)
                {
                    s.SetUnlocked(true);
                    if (string.Equals(s.StationId, activeStationId, StringComparison.OrdinalIgnoreCase))
                    {
                        foundActive = true;
                    }
                }
                else
                {
                    if (!s.IsCompleted)
                    {
                        s.SetUnlocked(false);
                    }
                }
            }
        }

        private void UnlockSingleStation(string stationId)
        {
            if (stations == null)
            {
                return;
            }

            for (int i = 0; i < stations.Length; i++)
            {
                if (stations[i] != null)
                {
                    stations[i].SetUnlocked(string.Equals(stations[i].StationId, stationId, StringComparison.OrdinalIgnoreCase));
                }
            }
        }
    }
}
