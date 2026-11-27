using System;
using UnityEngine;

namespace PNJunctionLab
{
    /// <summary>
    /// Base controller for every self-contained lab station (Tutorial Pad, Stations 1-6, Assessment).
    /// </summary>
    public abstract class StationController : MonoBehaviour
    {
        [SerializeField] protected StationBeacon stationBeacon;
        [SerializeField] private Transform npcStandPoint;
        [SerializeField] private Transform[] hintTargets;
        [SerializeField] private GameObject lockedOverlay;

        public string StationId => stationBeacon != null ? stationBeacon.StationId : string.Empty;
        public string StationTitle => stationBeacon != null ? stationBeacon.StationTitle : gameObject.name;
        public bool IsUnlocked => isUnlocked;
        public bool IsCompleted => isCompleted;
        public Transform NpcStandPoint => npcStandPoint != null ? npcStandPoint : transform;

        public event Action<StationController> UnlockedChanged;
        public event Action<StationController> Completed;
        public event Action<StationController, string> StepActionCompleted;
        public event Action<StationController, string, string> WrongActionReported;

        private bool isUnlocked;
        private bool isCompleted;

        protected virtual void Awake()
        {
            if (stationBeacon == null)
            {
                stationBeacon = GetComponent<StationBeacon>();
            }
        }

        /// <summary>
        /// Unlocks or locks (dims) this station and refreshes its beacon/overlay.
        /// </summary>
        public virtual void SetUnlocked(bool unlocked)
        {
            isUnlocked = unlocked;
            if (stationBeacon != null)
            {
                stationBeacon.SetState(unlocked, isCompleted);
            }

            if (lockedOverlay != null)
            {
                lockedOverlay.SetActive(!unlocked && !isCompleted);
            }

            UnlockedChanged?.Invoke(this);
        }

        /// <summary>
        /// Marks the station completed and notifies listeners such as LessonManager.
        /// </summary>
        public virtual void MarkCompleted()
        {
            if (isCompleted)
            {
                return;
            }

            isCompleted = true;
            isUnlocked = true;
            if (stationBeacon != null)
            {
                stationBeacon.SetState(true, true);
            }

            if (lockedOverlay != null)
            {
                lockedOverlay.SetActive(false);
            }

            Completed?.Invoke(this);
        }

        /// <summary>
        /// Resets this station's runtime progress back to its initial state.
        /// </summary>
        public virtual void ResetStation()
        {
            isCompleted = false;
            if (stationBeacon != null)
            {
                stationBeacon.SetState(isUnlocked, false);
            }

            if (lockedOverlay != null)
            {
                lockedOverlay.SetActive(!isUnlocked);
            }
        }

        /// <summary>
        /// Returns the world target Transform the NPC should point toward for the given step hint index.
        /// </summary>
        public virtual Transform GetHintTarget(int hintTargetIndex)
        {
            if (hintTargets != null && hintTargetIndex >= 0 && hintTargetIndex < hintTargets.Length && hintTargets[hintTargetIndex] != null)
            {
                return hintTargets[hintTargetIndex];
            }

            return transform;
        }

        protected void NotifyStepAction(string actionId)
        {
            if (!string.IsNullOrEmpty(actionId))
            {
                StepActionCompleted?.Invoke(this, actionId);
            }
        }

        protected void NotifyWrongAction(string actionId, string feedbackMessage)
        {
            WrongActionReported?.Invoke(this, actionId, feedbackMessage);
        }
    }
}
