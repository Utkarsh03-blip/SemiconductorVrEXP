using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace PNJunctionLab
{
    /// <summary>
    /// Controls the NPC scientist's NavMesh movement, player-facing rotation, placeholder capsule + NPCModelSlot,
    /// station teleport escort, click/ray-to-explain interaction, optional Animator hooks, and pointing gestures.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class NPCController : MonoBehaviour
    {
        [Serializable]
        public struct StationGuideEntry
        {
            public string stationId;
            public TeleportationAnchor teleportAnchor;
            public Transform standPoint;
            public DialogueLine guideDialogue;
            public GlossaryTopic[] askTopics;
        }

        [SerializeField] private string npcName = "Dr. Vera";
        [SerializeField] private NPCProceduralAnimator proceduralAnimator;
        [SerializeField] private NPCNarratorAudio narratorAudio;
        [SerializeField] private Transform npcModelSlot;
        [SerializeField] private GameObject placeholderRig;
        [SerializeField] private Transform pointerArmPivot;
        [SerializeField] private NavMeshAgent navMeshAgent;
        [SerializeField] private DialogueSystem dialogueSystem;
        [SerializeField] private NPCSpeechBubble speechBubble;
        [SerializeField] private LessonManager lessonManager;
        [SerializeField] private XRSimpleInteractable npcInteractable;
        [SerializeField] private Button npcClickButton;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private Transform playerHead;
        [SerializeField] private float turnSpeed = 6f;
        [SerializeField] private StationGuideEntry[] stationGuides;

        public string NpcName => npcName;
        public Transform NpcModelSlot => npcModelSlot;
        public bool IsWalking { get; private set; }
        public bool IsPointing => Time.time < pointUntilTime;
        public bool IsNodding => Time.time < nodUntilTime;
        public Transform CurrentPointTarget => pointTarget;
        public Transform PlayerLookTarget => playerHead;

        private Animator cachedAnimator;
        private readonly HashSet<int> boolParams = new HashSet<int>();
        private readonly HashSet<int> triggerParams = new HashSet<int>();

        private static readonly int IdleHash = Animator.StringToHash("Idle");
        private static readonly int TalkHash = Animator.StringToHash("Talk");
        private static readonly int WalkHash = Animator.StringToHash("Walk");
        private static readonly int PointHash = Animator.StringToHash("Point");
        private static readonly int NodHash = Animator.StringToHash("Nod");

        private Transform pointTarget;
        private float pointUntilTime;
        private float nodUntilTime;
        private Quaternion defaultArmRotation;
        private bool hasDefaultArmRot;
        private string currentContextStationId;
        private string lastProximityStationId;
        private float lastClickTime = -10f;
        private float nextProximityCheckTime;
        private readonly List<(TeleportationAnchor anchor, UnityAction<TeleportingEventArgs> callback)> anchorSubscriptions =
            new List<(TeleportationAnchor, UnityAction<TeleportingEventArgs>)>();

        private void Awake()
        {
            if (navMeshAgent == null)
            {
                navMeshAgent = GetComponent<NavMeshAgent>();
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (narratorAudio == null)
            {
                narratorAudio = GetComponent<NPCNarratorAudio>();
            }

            if (dialogueSystem != null)
            {
                dialogueSystem.SetSpeakerName(npcName);
            }

            if (pointerArmPivot != null)
            {
                defaultArmRotation = pointerArmPivot.localRotation;
                hasDefaultArmRot = true;
            }

            RefreshAnimatorCache();
        }

        private void OnEnable()
        {
            SubscribeGuideInteractions(true);
        }

        private void OnDisable()
        {
            SubscribeGuideInteractions(false);
        }

        private void Start()
        {
            if (playerHead == null && Camera.main != null)
            {
                playerHead = Camera.main.transform;
            }

            if (navMeshAgent != null && !navMeshAgent.enabled)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    transform.position = hit.position;
                }

                navMeshAgent.enabled = true;
            }

            if (TryResolveActiveGuide(out StationGuideEntry initialEntry))
            {
                currentContextStationId = initialEntry.stationId;
                ApplyStationAskTopics(initialEntry);
            }
        }

        private void Update()
        {
            UpdateStationProximityContext();
            UpdateMovementAndFacing();
            UpdatePlaceholderPointAndNod();
            UpdateOptionalAnimator();
        }

        /// <summary>
        /// Sets the configurable NPC display name and propagates it to DialogueSystem.
        /// </summary>
        public void SetNpcName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
            {
                return;
            }

            npcName = displayName;
            dialogueSystem?.SetSpeakerName(npcName);
        }

        /// <summary>
        /// Scans NPCModelSlot (or children) for an Animator and caches supported optional parameters.
        /// </summary>
        public void RefreshAnimatorCache()
        {
            boolParams.Clear();
            triggerParams.Clear();
            cachedAnimator = npcModelSlot != null
                ? npcModelSlot.GetComponentInChildren<Animator>(true)
                : GetComponentInChildren<Animator>(true);

            if (cachedAnimator == null || !cachedAnimator.isInitialized)
            {
                return;
            }

            AnimatorControllerParameter[] parameters = cachedAnimator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter p = parameters[i];
                if (p.type == AnimatorControllerParameterType.Bool)
                {
                    boolParams.Add(p.nameHash);
                }
                else if (p.type == AnimatorControllerParameterType.Trigger)
                {
                    triggerParams.Add(p.nameHash);
                }
            }
        }

        /// <summary>
        /// Walks the NPC via NavMeshAgent to the target station stand marker.
        /// </summary>
        public void MoveToStation(Transform standPoint)
        {
            if (standPoint == null)
            {
                return;
            }

            pointUntilTime = 0f;
            if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh)
            {
                if (!navMeshAgent.hasPath && !navMeshAgent.pathPending)
                {
                    Vector3 flatDelta = navMeshAgent.transform.position - standPoint.position;
                    flatDelta.y = 0f;
                    if (flatDelta.sqrMagnitude < 0.04f)
                    {
                        return;
                    }
                }

                if ((navMeshAgent.destination - standPoint.position).sqrMagnitude < 0.01f
                    && (navMeshAgent.hasPath || navMeshAgent.pathPending))
                {
                    return;
                }

                if (navMeshAgent.hasPath || navMeshAgent.pathPending)
                {
                    navMeshAgent.ResetPath();
                }

                navMeshAgent.SetDestination(standPoint.position);
            }
            else
            {
                transform.position = standPoint.position;
            }
        }

        /// <summary>
        /// Simulates or triggers a player arrival at the named station ID (moves Dr. Vera to its NPCStandPoint
        /// and switches her click-to-guide explanation context).
        /// </summary>
        public void EscortToStation(string stationId)
        {
            if (!TryFindGuideEntry(stationId, out StationGuideEntry entry))
            {
                return;
            }

            currentContextStationId = entry.stationId;
            lastProximityStationId = entry.stationId;
            MoveToStation(entry.standPoint);
            ApplyStationAskTopics(entry);
        }

        /// <summary>
        /// Opens or advances Dr. Vera's station-specific physics and step guide when clicked or ray-selected.
        /// </summary>
        public void OnGuideInteracted()
        {
            if (Time.time - lastClickTime < 0.20f)
            {
                return;
            }

            lastClickTime = Time.time;
            TriggerNod();
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.50f);

            if (dialogueSystem == null)
            {
                return;
            }

            if (TryResolveActiveGuide(out StationGuideEntry entry) && entry.guideDialogue != null)
            {
                ApplyStationAskTopics(entry);
                if (dialogueSystem.IsDialogueActive && IsLineInChain(dialogueSystem.CurrentLine, entry.guideDialogue))
                {
                    dialogueSystem.Advance();
                }
                else
                {
                    dialogueSystem.ShowLine(entry.guideDialogue);
                }

                return;
            }

            if (dialogueSystem.IsDialogueActive)
            {
                dialogueSystem.Advance();
            }
        }

        private void SubscribeGuideInteractions(bool subscribe)
        {
            for (int i = 0; i < anchorSubscriptions.Count; i++)
            {
                var sub = anchorSubscriptions[i];
                if (sub.anchor != null && sub.callback != null)
                {
                    sub.anchor.teleporting.RemoveListener(sub.callback);
                }
            }

            anchorSubscriptions.Clear();

            if (lessonManager != null)
            {
                lessonManager.ActiveStationChanged -= HandleActiveStationChanged;
                if (subscribe)
                {
                    lessonManager.ActiveStationChanged += HandleActiveStationChanged;
                }
            }

            if (npcInteractable != null)
            {
                npcInteractable.selectEntered.RemoveListener(HandleNpcSelected);
                if (subscribe)
                {
                    npcInteractable.selectEntered.AddListener(HandleNpcSelected);
                }
            }

            if (npcClickButton != null)
            {
                npcClickButton.onClick.RemoveListener(OnGuideInteracted);
                if (subscribe)
                {
                    npcClickButton.onClick.AddListener(OnGuideInteracted);
                }
            }

            if (!subscribe || stationGuides == null)
            {
                return;
            }

            for (int i = 0; i < stationGuides.Length; i++)
            {
                StationGuideEntry captured = stationGuides[i];
                if (captured.teleportAnchor == null)
                {
                    continue;
                }

                UnityAction<TeleportingEventArgs> cb = _ => HandleStationTeleport(captured);
                captured.teleportAnchor.teleporting.AddListener(cb);
                anchorSubscriptions.Add((captured.teleportAnchor, cb));
            }
        }

        private void HandleStationTeleport(StationGuideEntry entry)
        {
            currentContextStationId = entry.stationId;
            lastProximityStationId = entry.stationId;
            MoveToStation(entry.standPoint);
            ApplyStationAskTopics(entry);
        }

        private void HandleActiveStationChanged(StationController station)
        {
            if (station != null && !string.IsNullOrEmpty(station.StationId))
            {
                currentContextStationId = station.StationId;
                if (TryFindGuideEntry(station.StationId, out StationGuideEntry entry))
                {
                    ApplyStationAskTopics(entry);
                }
            }
        }

        private void ApplyStationAskTopics(StationGuideEntry entry)
        {
            if (dialogueSystem != null && entry.askTopics != null && entry.askTopics.Length > 0)
            {
                dialogueSystem.SetStationAskTopics(entry.askTopics);
            }
        }

        private void UpdateStationProximityContext()
        {
            if (Time.unscaledTime < nextProximityCheckTime || stationGuides == null || stationGuides.Length == 0)
            {
                return;
            }

            nextProximityCheckTime = Time.unscaledTime + 0.35f;
            if (playerHead == null)
            {
                if (Camera.main != null)
                {
                    playerHead = Camera.main.transform;
                }
                else
                {
                    return;
                }
            }

            Vector3 headPos = playerHead.position;
            const float maxEnterRadiusSqr = 1.75f * 1.75f;
            const float maxExitRadiusSqr = 2.10f * 2.10f;
            float bestDistSqr = float.MaxValue;
            int bestIndex = -1;

            for (int i = 0; i < stationGuides.Length; i++)
            {
                Vector3 zoneCenter;
                if (stationGuides[i].teleportAnchor != null)
                {
                    Transform anchorTf = stationGuides[i].teleportAnchor.transform;
                    zoneCenter = anchorTf.position + anchorTf.forward * 0.25f;
                }
                else if (stationGuides[i].standPoint != null)
                {
                    zoneCenter = stationGuides[i].standPoint.position;
                }
                else
                {
                    continue;
                }

                float dx = headPos.x - zoneCenter.x;
                float dz = headPos.z - zoneCenter.z;
                float dSqr = (dx * dx) + (dz * dz);
                if (dSqr < bestDistSqr)
                {
                    bestDistSqr = dSqr;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0 && bestDistSqr <= maxEnterRadiusSqr)
            {
                StationGuideEntry nearest = stationGuides[bestIndex];
                if (!string.Equals(lastProximityStationId, nearest.stationId, StringComparison.OrdinalIgnoreCase))
                {
                    lastProximityStationId = nearest.stationId;
                    currentContextStationId = nearest.stationId;
                    MoveToStation(nearest.standPoint);
                    ApplyStationAskTopics(nearest);
                }
            }
            else if (bestDistSqr > maxExitRadiusSqr)
            {
                lastProximityStationId = null;
            }
        }

        private void HandleNpcSelected(SelectEnterEventArgs args)
        {
            OnGuideInteracted();
        }

        private bool TryResolveActiveGuide(out StationGuideEntry resolved)
        {
            if (!string.IsNullOrEmpty(currentContextStationId) && TryFindGuideEntry(currentContextStationId, out resolved))
            {
                return true;
            }

            if (lessonManager != null && lessonManager.ActiveStation != null
                && TryFindGuideEntry(lessonManager.ActiveStation.StationId, out resolved))
            {
                return true;
            }

            // Spatial fallback to nearest station stand point with a configured guide.
            resolved = default;
            if (stationGuides == null)
            {
                return false;
            }

            float bestDistSqr = float.MaxValue;
            bool found = false;
            for (int i = 0; i < stationGuides.Length; i++)
            {
                StationGuideEntry candidate = stationGuides[i];
                if (candidate.guideDialogue == null || candidate.standPoint == null)
                {
                    continue;
                }

                float dSqr = (candidate.standPoint.position - transform.position).sqrMagnitude;
                if (dSqr < bestDistSqr)
                {
                    bestDistSqr = dSqr;
                    resolved = candidate;
                    found = true;
                }
            }

            return found;
        }

        private bool TryFindGuideEntry(string stationId, out StationGuideEntry match)
        {
            match = default;
            if (string.IsNullOrEmpty(stationId) || stationGuides == null)
            {
                return false;
            }

            for (int i = 0; i < stationGuides.Length; i++)
            {
                if (string.Equals(stationGuides[i].stationId, stationId, StringComparison.OrdinalIgnoreCase))
                {
                    match = stationGuides[i];
                    return true;
                }
            }

            return false;
        }

        private static bool IsLineInChain(DialogueLine current, DialogueLine chainRoot)
        {
            if (current == null || chainRoot == null)
            {
                return false;
            }

            DialogueLine cursor = chainRoot;
            int guard = 0;
            while (cursor != null && guard < 16)
            {
                if (cursor == current)
                {
                    return true;
                }

                cursor = cursor.NextLine;
                guard++;
            }

            return false;
        }

        /// <summary>
        /// Points toward the relevant object (used by 45s strong hint) with Animator or placeholder arm fallback.
        /// </summary>
        public void PointAt(Transform target, float duration = 4f)
        {
            if (target == null)
            {
                return;
            }

            pointTarget = target;
            pointUntilTime = Time.time + Mathf.Max(0.5f, duration);
            TryTrigger(PointHash);
        }

        /// <summary>
        /// Plays a subtle confirmation nod on the Animator or placeholder rig.
        /// </summary>
        public void TriggerNod()
        {
            nodUntilTime = Time.time + 0.65f;
            TryTrigger(NodHash);
        }

        private void UpdateMovementAndFacing()
        {
            bool moving = false;
            if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh)
            {
                moving = navMeshAgent.pathPending
                    || (navMeshAgent.hasPath && navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance + 0.08f);
            }

            IsWalking = moving;

            if (!moving)
            {
                Transform lookAtTarget = IsPointing && pointTarget != null ? pointTarget : playerHead;
                if (lookAtTarget == null && Camera.main != null)
                {
                    playerHead = Camera.main.transform;
                    lookAtTarget = playerHead;
                }

                if (lookAtTarget != null)
                {
                    Vector3 flatDir = lookAtTarget.position - transform.position;
                    flatDir.y = 0f;
                    if (flatDir.sqrMagnitude > 0.001f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * turnSpeed);
                    }
                }
            }
        }

        private void UpdatePlaceholderPointAndNod()
        {
            bool usingProceduralRig = proceduralAnimator != null && proceduralAnimator.isActiveAndEnabled && cachedAnimator == null;
            if (usingProceduralRig)
            {
                return;
            }

            if (pointerArmPivot != null && hasDefaultArmRot)
            {
                if (IsPointing && pointTarget != null)
                {
                    Vector3 dir = pointTarget.position - pointerArmPivot.position;
                    if (dir.sqrMagnitude > 0.001f)
                    {
                        Quaternion worldAim = Quaternion.LookRotation(dir.normalized, Vector3.up);
                        pointerArmPivot.rotation = Quaternion.Slerp(pointerArmPivot.rotation, worldAim, Time.deltaTime * 10f);
                    }
                }
                else
                {
                    pointerArmPivot.localRotation = Quaternion.Slerp(pointerArmPivot.localRotation, defaultArmRotation, Time.deltaTime * 8f);
                }
            }

            if (npcModelSlot != null)
            {
                float nodPitch = Time.time < nodUntilTime
                    ? Mathf.Sin((nodUntilTime - Time.time) * Mathf.PI * 3f) * 8f
                    : 0f;
                Vector3 localEuler = npcModelSlot.localEulerAngles;
                localEuler.x = nodPitch;
                npcModelSlot.localEulerAngles = localEuler;
            }
        }

        private void UpdateOptionalAnimator()
        {
            if (cachedAnimator == null || !cachedAnimator.isInitialized)
            {
                return;
            }

            bool isSpeaking = narratorAudio != null
                ? narratorAudio.IsSpeaking
                : (dialogueSystem != null && dialogueSystem.IsDialogueActive);
            bool idle = !IsWalking && !isSpeaking;

            TrySetBool(WalkHash, IsWalking);
            TrySetBool(TalkHash, isSpeaking && !IsWalking);
            TrySetBool(IdleHash, idle);
        }

        private void TrySetBool(int hash, bool value)
        {
            if (cachedAnimator != null && boolParams.Contains(hash))
            {
                cachedAnimator.SetBool(hash, value);
            }
        }

        private void TryTrigger(int hash)
        {
            if (cachedAnimator != null && triggerParams.Contains(hash))
            {
                cachedAnimator.SetTrigger(hash);
            }
        }
    }
}
