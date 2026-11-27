using System;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace PNJunctionLab
{
    /// <summary>
    /// Self-contained controller for Tutorial Pad (Station T): verifies teleport, cube grab,
    /// socket insertion, physical button press, and ray-pointing at the station screen.
    /// </summary>
    public class TutorialPadController : StationController
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private TeleportationAnchor tutorialAnchor;
        [SerializeField] private XRGrabInteractable testCube;
        [SerializeField] private RespawnableItem testCubeRespawn;
        [SerializeField] private XRSocketInteractor testSocket;
        [SerializeField] private PhysicalPushButton testButton;
        [SerializeField] private PhysicalPushButton returnCubeButton;
        [SerializeField] private XRSimpleInteractable screenInteractable;
        [SerializeField] private LabTablet labTablet;
        [SerializeField] private TMP_Text checklistText;
        [SerializeField] private TMP_Text statusBannerText;
        [SerializeField] private Renderer screenStatusLed;
        [SerializeField] private AudioSource audioSource;

        /// <summary>
        /// Raised once all six Tutorial Pad skills are completed.
        /// </summary>
        public event Action AllSkillsCompleted;

        public bool Teleported => teleported;
        public bool GrabbedCube => grabbedCube;
        public bool SocketedCube => socketedCube;
        public bool PressedButton => pressedButton;
        public bool RayPointedScreen => rayPointedScreen;
        public bool UsedLabTablet => usedLabTablet;
        public bool IsComplete => teleported && grabbedCube && socketedCube && pressedButton && rayPointedScreen && usedLabTablet;

        private bool teleported;
        private bool grabbedCube;
        private bool socketedCube;
        private bool pressedButton;
        private bool rayPointedScreen;
        private bool usedLabTablet;
        private bool sawTabletSummon;
        private MaterialPropertyBlock ledBlock;
        private bool completionBroadcasted;

        protected override void Awake()
        {
            base.Awake();
            ledBlock = new MaterialPropertyBlock();
            if (labTablet == null)
            {
                labTablet = FindAnyObjectByType<LabTablet>(FindObjectsInactive.Include);
            }

            if (stationBeacon != null)
            {
                stationBeacon.SetState(true, false);
            }

            RefreshChecklistUI();
        }

        private void OnEnable()
        {
            if (tutorialAnchor != null)
            {
                tutorialAnchor.teleporting.AddListener(HandleTeleporting);
            }

            if (testCube != null)
            {
                testCube.selectEntered.AddListener(HandleCubeSelected);
            }

            if (testSocket != null)
            {
                testSocket.selectEntered.AddListener(HandleSocketSelected);
            }

            if (testButton != null)
            {
                testButton.Pressed += HandleTestButtonPressed;
            }

            if (returnCubeButton != null)
            {
                returnCubeButton.Pressed += HandleReturnCubePressed;
            }

            if (screenInteractable != null)
            {
                screenInteractable.hoverEntered.AddListener(HandleScreenHovered);
                screenInteractable.selectEntered.AddListener(HandleScreenSelected);
            }

            if (labTablet != null)
            {
                labTablet.TabletVisibilityChanged += HandleLabTabletVisibilityChanged;
            }
        }

        private void OnDisable()
        {
            if (tutorialAnchor != null)
            {
                tutorialAnchor.teleporting.RemoveListener(HandleTeleporting);
            }

            if (testCube != null)
            {
                testCube.selectEntered.RemoveListener(HandleCubeSelected);
            }

            if (testSocket != null)
            {
                testSocket.selectEntered.RemoveListener(HandleSocketSelected);
            }

            if (testButton != null)
            {
                testButton.Pressed -= HandleTestButtonPressed;
            }

            if (returnCubeButton != null)
            {
                returnCubeButton.Pressed -= HandleReturnCubePressed;
            }

            if (screenInteractable != null)
            {
                screenInteractable.hoverEntered.RemoveListener(HandleScreenHovered);
                screenInteractable.selectEntered.RemoveListener(HandleScreenSelected);
            }

            if (labTablet != null)
            {
                labTablet.TabletVisibilityChanged -= HandleLabTabletVisibilityChanged;
            }
        }

        private void HandleTeleporting(TeleportingEventArgs args)
        {
            MarkCompleted(ref teleported, "tutorial.teleport");
        }

        private void HandleCubeSelected(SelectEnterEventArgs args)
        {
            if (args.interactorObject is XRSocketInteractor)
            {
                return;
            }

            MarkCompleted(ref grabbedCube, "tutorial.grab");
        }

        private void HandleSocketSelected(SelectEnterEventArgs args)
        {
            MarkCompleted(ref socketedCube, "tutorial.socket");
        }

        private void HandleTestButtonPressed()
        {
            MarkCompleted(ref pressedButton, "tutorial.button");
        }

        private void HandleReturnCubePressed()
        {
            if (testCubeRespawn != null)
            {
                testCubeRespawn.Respawn();
            }
        }

        private void HandleScreenHovered(HoverEnterEventArgs args)
        {
            if (args.interactorObject is XRRayInteractor || args.interactorObject is NearFarInteractor)
            {
                MarkCompleted(ref rayPointedScreen, "tutorial.screen");
            }
        }

        private void HandleScreenSelected(SelectEnterEventArgs args)
        {
            MarkCompleted(ref rayPointedScreen, "tutorial.screen");
        }

        private void HandleLabTabletVisibilityChanged(bool visible)
        {
            bool readyForTabletStep = teleported && grabbedCube && socketedCube && pressedButton && rayPointedScreen;
            if (!readyForTabletStep)
            {
                return;
            }

            if (visible)
            {
                sawTabletSummon = true;
                RefreshChecklistUI();
                return;
            }

            if (!sawTabletSummon)
            {
                return;
            }

            if (!usedLabTablet)
            {
                MarkCompleted(ref usedLabTablet, "tutorial.tablet");
            }
            else if (!IsComplete)
            {
                NotifyStepAction("tutorial.tablet");
            }
        }

        private void MarkCompleted(ref bool flag, string actionId)
        {
            if (flag)
            {
                return;
            }

            flag = true;
            if (!sawTabletSummon && teleported && grabbedCube && socketedCube && pressedButton && rayPointedScreen
                && labTablet != null && labTablet.IsVisible)
            {
                sawTabletSummon = true;
            }

            RefreshChecklistUI();
            BroadcastCompletedActionsInOrder();

            if (IsComplete && !completionBroadcasted)
            {
                completionBroadcasted = true;
                ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Success, 0.7f);
                MarkCompleted();
                AllSkillsCompleted?.Invoke();
            }
        }

        /// <summary>
        /// Fires every completed tutorial step action in canonical LessonManager order so
        /// LessonManager immediately catches up even when the player finishes skills out of
        /// sequence (e.g. ray-pointing the screen before pressing the button).
        /// </summary>
        private void BroadcastCompletedActionsInOrder()
        {
            if (teleported) NotifyStepAction("tutorial.teleport");
            if (grabbedCube) NotifyStepAction("tutorial.grab");
            if (socketedCube) NotifyStepAction("tutorial.socket");
            if (pressedButton) NotifyStepAction("tutorial.button");
            if (rayPointedScreen) NotifyStepAction("tutorial.screen");
            if (usedLabTablet) NotifyStepAction("tutorial.tablet");
        }

        public override void ResetStation()
        {
            teleported = false;
            grabbedCube = false;
            socketedCube = false;
            pressedButton = false;
            rayPointedScreen = false;
            usedLabTablet = false;
            sawTabletSummon = false;
            completionBroadcasted = false;
            if (testCubeRespawn != null)
            {
                testCubeRespawn.Respawn();
            }

            base.ResetStation();
            RefreshChecklistUI();
        }

        public override Transform GetHintTarget(int hintTargetIndex)
        {
            switch (hintTargetIndex)
            {
                case 0:
                    return tutorialAnchor != null ? tutorialAnchor.transform : base.GetHintTarget(hintTargetIndex);
                case 1:
                    return testCube != null ? testCube.transform : base.GetHintTarget(hintTargetIndex);
                case 2:
                    return testSocket != null ? testSocket.transform : base.GetHintTarget(hintTargetIndex);
                case 3:
                    return testButton != null ? testButton.transform : base.GetHintTarget(hintTargetIndex);
                case 4:
                    return screenInteractable != null ? screenInteractable.transform : base.GetHintTarget(hintTargetIndex);
                case 5:
                    if (labTablet != null && labTablet.IsVisible)
                    {
                        return labTablet.transform;
                    }
                    return tutorialAnchor != null ? tutorialAnchor.transform : base.GetHintTarget(hintTargetIndex);
                default:
                    return base.GetHintTarget(hintTargetIndex);
            }
        }

        private void RefreshChecklistUI()
        {
            int count = (Teleported ? 1 : 0)
                      + (GrabbedCube ? 1 : 0)
                      + (SocketedCube ? 1 : 0)
                      + (PressedButton ? 1 : 0)
                      + (RayPointedScreen ? 1 : 0)
                      + (UsedLabTablet ? 1 : 0);

            string tabletPrompt = (!UsedLabTablet && sawTabletSummon)
                ? "<color=#38D8FF>[..] 6. Read Steps & Press Y (or F) to Dismiss Tablet</color>"
                : FormatStep(UsedLabTablet, "6. Summon Floating Lab Tablet (Y / F)");

            if (checklistText != null)
            {
                checklistText.text =
                    FormatStep(Teleported, "1. Teleport onto Tutorial Anchor") + "\n" +
                    FormatStep(GrabbedCube, "2. Grab the Test Cube") + "\n" +
                    FormatStep(SocketedCube, "3. Drop Cube into Socket") + "\n" +
                    FormatStep(PressedButton, "4. Press the Green Lab Button") + "\n" +
                    FormatStep(RayPointedScreen, "5. Ray-Point at this Screen") + "\n" +
                    tabletPrompt;
            }

            if (statusBannerText != null)
            {
                statusBannerText.text = IsComplete
                    ? "TUTORIAL COMPLETE (6 / 6)"
                    : "SKILLS VERIFIED: " + count + " / 6";
            }

            if (screenStatusLed != null)
            {
                Color ledColor = IsComplete
                    ? new Color(0.20f, 0.95f, 0.45f, 1f)
                    : new Color(0.18f, 0.75f, 1.0f, 1f);
                if (ledBlock == null)
                {
                    ledBlock = new MaterialPropertyBlock();
                }

                screenStatusLed.GetPropertyBlock(ledBlock);
                ledBlock.SetColor(BaseColorId, ledColor);
                ledBlock.SetColor(EmissionColorId, ledColor * 0.8f);
                screenStatusLed.SetPropertyBlock(ledBlock);
            }
        }

        private static string FormatStep(bool done, string label)
        {
            return done
                ? "<color=#5CFFA0>[OK] " + label + "</color>"
                : "<color=#D0DCE8>[ ] " + label + "</color>";
        }
    }
}
