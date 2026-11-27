using UnityEngine;
using UnityEngine.AI;

namespace PNJunctionLab
{
    /// <summary>
    /// Procedural character animation controller for the multi-part stylized Dr. Vera rig inside NPCModelSlot.
    /// Drives smooth Idle breathing/head-tracking, Talk hand gestures, NavMesh-synced Walk strides,
    /// station Pointing poses, and step-completion Head Nods while yielding gracefully if an external Animator is added.
    /// </summary>
    public class NPCProceduralAnimator : MonoBehaviour
    {
        [Header("Core References")]
        [SerializeField] private NPCController npcController;
        [SerializeField] private DialogueSystem dialogueSystem;
        [SerializeField] private NPCNarratorAudio narratorAudio;
        [SerializeField] private NavMeshAgent navMeshAgent;
        [SerializeField] private Animator optionalExternalAnimator;

        [Header("Articulated Joint Pivots")]
        [SerializeField] private Transform chestPivot;
        [SerializeField] private Transform headPivot;
        [SerializeField] private Transform leftShoulderPivot;
        [SerializeField] private Transform leftElbowPivot;
        [SerializeField] private Transform rightShoulderPivot;
        [SerializeField] private Transform rightElbowPivot;
        [SerializeField] private Transform leftLegPivot;
        [SerializeField] private Transform rightLegPivot;

        [Header("Animation Tuning")]
        [SerializeField] private float breathingRate = 1.65f;
        [SerializeField] private float talkGestureRate = 2.15f;
        [SerializeField] private float walkStrideSpeed = 2.75f;
        [SerializeField] private float maxWalkLegSwingDeg = 28f;
        [SerializeField] private float maxHeadYawDeg = 36f;
        [SerializeField] private float maxHeadPitchDeg = 18f;
        [SerializeField] private float jointSmoothSpeed = 9f;

        private float walkCyclePhase;
        private float walkBlend;
        private float talkBlend;
        private float pointBlend;
        private float nodPhaseTime;
        private bool wasNoddingLastFrame;
        private Vector3 chestBaseLocalPos;
        private bool hasChestBase;

        private void Awake()
        {
            if (npcController == null)
            {
                npcController = GetComponentInParent<NPCController>();
            }

            if (narratorAudio == null)
            {
                narratorAudio = GetComponentInParent<NPCNarratorAudio>();
            }

            if (navMeshAgent == null && npcController != null)
            {
                navMeshAgent = npcController.GetComponent<NavMeshAgent>();
            }

            if (chestPivot != null)
            {
                chestBaseLocalPos = chestPivot.localPosition;
                hasChestBase = true;
            }
        }

        private void Update()
        {
            // Yield gracefully whenever a user-supplied FBX/GLB model brings an active Animator Controller.
            if (optionalExternalAnimator != null
                && optionalExternalAnimator.isActiveAndEnabled
                && optionalExternalAnimator.runtimeAnimatorController != null)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0.0001f)
            {
                return;
            }

            float moveSpeed = (navMeshAgent != null && navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh)
                ? navMeshAgent.velocity.magnitude
                : 0f;

            bool isWalking = (npcController != null && npcController.IsWalking) || moveSpeed > 0.08f;
            bool isPointing = npcController != null && npcController.IsPointing && npcController.CurrentPointTarget != null && !isWalking;
            bool isSpeaking = narratorAudio != null
                ? narratorAudio.IsSpeaking
                : (dialogueSystem != null && dialogueSystem.IsDialogueActive);
            bool isTalking = !isWalking && !isPointing && isSpeaking;
            bool isNodding = npcController != null && npcController.IsNodding;

            if (isNodding && !wasNoddingLastFrame)
            {
                nodPhaseTime = 0f;
            }
            wasNoddingLastFrame = isNodding;

            if (isNodding)
            {
                nodPhaseTime += dt;
            }

            walkBlend = Mathf.MoveTowards(walkBlend, isWalking ? 1f : 0f, dt * 6f);
            talkBlend = Mathf.MoveTowards(talkBlend, isTalking ? 1f : 0f, dt * 5f);
            pointBlend = Mathf.MoveTowards(pointBlend, isPointing ? 1f : 0f, dt * 5.5f);

            if (isWalking)
            {
                walkCyclePhase += dt * Mathf.Max(0.85f, moveSpeed) * walkStrideSpeed;
            }
            else if (walkBlend < 0.01f)
            {
                walkCyclePhase = 0f;
            }

            UpdateChestAndLegs(dt);
            UpdateArms(dt);
            UpdateHead(dt, isNodding);
        }

        private void UpdateChestAndLegs(float dt)
        {
            float legAngle = Mathf.Sin(walkCyclePhase) * maxWalkLegSwingDeg * walkBlend;
            if (leftLegPivot != null)
            {
                Quaternion target = Quaternion.Euler(-legAngle, 0f, 0f);
                leftLegPivot.localRotation = Quaternion.Slerp(leftLegPivot.localRotation, target, dt * 12f);
            }

            if (rightLegPivot != null)
            {
                Quaternion target = Quaternion.Euler(legAngle, 0f, 0f);
                rightLegPivot.localRotation = Quaternion.Slerp(rightLegPivot.localRotation, target, dt * 12f);
            }

            if (chestPivot != null)
            {
                float breath = Mathf.Sin(Time.time * breathingRate);
                float walkBob = Mathf.Abs(Mathf.Sin(walkCyclePhase)) * 0.018f * walkBlend;

                if (hasChestBase)
                {
                    chestPivot.localPosition = new Vector3(
                        chestBaseLocalPos.x,
                        chestBaseLocalPos.y + breath * 0.0035f + walkBob,
                        chestBaseLocalPos.z);
                }

                float talkLeanX = -2.2f * talkBlend + Mathf.Sin(Time.time * talkGestureRate * 0.65f) * 1.6f * talkBlend;
                float talkYawY = Mathf.Sin(Time.time * talkGestureRate * 0.5f) * 3.5f * talkBlend;
                float idlePitch = breath * 1.1f;
                Quaternion chestRot = Quaternion.Euler(idlePitch + talkLeanX, talkYawY, 0f);
                chestPivot.localRotation = Quaternion.Slerp(chestPivot.localRotation, chestRot, dt * jointSmoothSpeed);
            }
        }

        private void UpdateArms(float dt)
        {
            float t = Time.time;
            float idleSway = Mathf.Sin(t * 1.3f) * 1.8f;
            float walkArmAngle = Mathf.Sin(walkCyclePhase) * 20f * walkBlend;

            // Left arm: Idle sway + Walk counter-swing + Conversational explanatory gestures while talking.
            float leftTalkPitch = -26f + Mathf.Sin(t * talkGestureRate) * 11f;
            float leftTalkRoll = 8f + Mathf.Cos(t * talkGestureRate * 0.8f) * 4f;
            float leftElbowTalk = -58f + Mathf.Cos(t * talkGestureRate * 1.1f) * 16f;

            float leftPitch = Mathf.Lerp(idleSway + walkArmAngle, leftTalkPitch, talkBlend);
            float leftRoll = Mathf.Lerp(4.5f, leftTalkRoll, talkBlend);
            float leftElbowX = Mathf.Lerp(-10f - 8f * walkBlend, leftElbowTalk, talkBlend);

            if (leftShoulderPivot != null)
            {
                leftShoulderPivot.localRotation = Quaternion.Slerp(
                    leftShoulderPivot.localRotation,
                    Quaternion.Euler(leftPitch, 0f, -leftRoll),
                    dt * jointSmoothSpeed);
            }

            if (leftElbowPivot != null)
            {
                leftElbowPivot.localRotation = Quaternion.Slerp(
                    leftElbowPivot.localRotation,
                    Quaternion.Euler(leftElbowX, 0f, 0f),
                    dt * (jointSmoothSpeed + 2f));
            }

            // Right arm: blends cleanly between Idle/Walk, Talk gesture, and full Point-At pose.
            float rightTalkPitch = -28f + Mathf.Cos(t * talkGestureRate * 0.92f) * 12f;
            float rightTalkRoll = 9f + Mathf.Sin(t * talkGestureRate * 0.75f) * 4.5f;
            float rightElbowTalk = -62f + Mathf.Sin(t * talkGestureRate * 1.15f) * 18f;

            float baseRightPitch = Mathf.Lerp(-idleSway - walkArmAngle, rightTalkPitch, talkBlend);
            float baseRightRoll = Mathf.Lerp(4.5f, rightTalkRoll, talkBlend);
            Quaternion baseShoulderRot = Quaternion.Euler(baseRightPitch, 0f, baseRightRoll);

            float rightElbowAngle = Mathf.Lerp(-10f - 8f * walkBlend, rightElbowTalk, talkBlend);

            if (rightShoulderPivot != null)
            {
                Quaternion targetShoulder = baseShoulderRot;
                if (pointBlend > 0.01f && npcController != null && npcController.CurrentPointTarget != null)
                {
                    Vector3 targetWorld = npcController.CurrentPointTarget.position;
                    // Aim comfortably at torso/tabletop height if target root is on floor (y < 0.35m).
                    if (targetWorld.y < 0.35f)
                    {
                        targetWorld.y = 1.05f;
                    }

                    Vector3 toTarget = targetWorld - rightShoulderPivot.position;
                    if (toTarget.sqrMagnitude > 0.001f && rightShoulderPivot.parent != null)
                    {
                        Vector3 localDir = rightShoulderPivot.parent.InverseTransformDirection(toTarget.normalized);
                        // Prevent pointing arm from clipping across her own torso: keep right side outward (x >= 0.05) and forward (z >= 0.25).
                        localDir.x = Mathf.Max(0.05f, localDir.x);
                        localDir.z = Mathf.Max(0.25f, localDir.z);
                        localDir.y = Mathf.Clamp(localDir.y, -0.45f, 0.55f);
                        localDir.Normalize();

                        Quaternion aimLocal = Quaternion.FromToRotation(Vector3.down, localDir);
                        targetShoulder = Quaternion.Slerp(baseShoulderRot, aimLocal, pointBlend);
                    }
                    rightElbowAngle = Mathf.Lerp(rightElbowAngle, -10f, pointBlend);
                }

                rightShoulderPivot.localRotation = Quaternion.Slerp(rightShoulderPivot.localRotation, targetShoulder, dt * 10f);
            }

            if (rightElbowPivot != null)
            {
                rightElbowPivot.localRotation = Quaternion.Slerp(
                    rightElbowPivot.localRotation,
                    Quaternion.Euler(rightElbowAngle, 0f, 0f),
                    dt * 12f);
            }
        }

        private void UpdateHead(float dt, bool isNodding)
        {
            if (headPivot == null)
            {
                return;
            }

            float targetYaw = 0f;
            float targetPitch = 0f;

            Transform lookTarget = null;
            if (npcController != null && npcController.IsPointing && npcController.CurrentPointTarget != null)
            {
                // Alternate glance gently toward target and back toward student while pointing.
                float glance = Mathf.Sin(Time.time * 1.6f);
                lookTarget = glance > -0.25f
                    ? npcController.CurrentPointTarget
                    : (npcController.PlayerLookTarget != null ? npcController.PlayerLookTarget : Camera.main?.transform);
            }
            else if (npcController != null && !npcController.IsWalking)
            {
                lookTarget = npcController.PlayerLookTarget != null ? npcController.PlayerLookTarget : Camera.main?.transform;
            }

            if (lookTarget != null && headPivot.parent != null)
            {
                Vector3 localLookDir = headPivot.parent.InverseTransformDirection((lookTarget.position - headPivot.position).normalized);
                if (localLookDir.sqrMagnitude > 0.001f && localLookDir.z > 0.05f)
                {
                    targetYaw = Mathf.Clamp(Mathf.Atan2(localLookDir.x, localLookDir.z) * Mathf.Rad2Deg, -maxHeadYawDeg, maxHeadYawDeg);
                    targetPitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(localLookDir.y, -0.8f, 0.8f)) * Mathf.Rad2Deg, -maxHeadPitchDeg, maxHeadPitchDeg);
                }
            }

            float talkTilt = Mathf.Sin(Time.time * 1.9f) * 3.5f * talkBlend;
            float talkNodPitch = Mathf.Sin(Time.time * 2.8f) * 3.2f * talkBlend;
            float confirmNod = isNodding
                ? Mathf.Sin(nodPhaseTime * Mathf.PI * 3.2f) * 14f * Mathf.Clamp01(1f - nodPhaseTime / 0.65f)
                : 0f;

            Quaternion goalHead = Quaternion.Euler(targetPitch + talkNodPitch + confirmNod, targetYaw, talkTilt);
            headPivot.localRotation = Quaternion.Slerp(headPivot.localRotation, goalHead, dt * 8f);
        }
    }
}
