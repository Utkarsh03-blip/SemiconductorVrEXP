using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace PNJunctionLab
{
    /// <summary>
    /// Configures default XRI locomotion (Teleport + Snap Turn by default, Continuous Move toggleable)
    /// and ensures the comfort tunneling vignette activates during continuous movement.
    /// </summary>
    public class LocomotionModeController : MonoBehaviour
    {
        [SerializeField] private ControllerInputActionManager leftControllerActions;
        [SerializeField] private ControllerInputActionManager rightControllerActions;
        [SerializeField] private LocomotionProvider continuousMoveProvider;
        [SerializeField] private LocomotionProvider snapTurnProvider;
        [SerializeField] private LocomotionProvider teleportProvider;
        [SerializeField] private TunnelingVignetteController vignetteController;
        [SerializeField] private Transform cameraOffset;
        [SerializeField] private bool continuousMoveEnabled = false;
        [SerializeField] private bool smoothTurnEnabled = false;
        [SerializeField] private float standingOffsetY = 0f;
        [SerializeField] private float seatedOffsetY = 0.45f;

        private bool locomotionLocked;

        /// <summary>
        /// Raised whenever continuous movement mode changes.
        /// </summary>
        public event Action<bool> ContinuousMoveChanged;

        /// <summary>
        /// True when continuous smooth locomotion is active instead of teleport-only on the left stick.
        /// </summary>
        public bool IsContinuousMoveEnabled => continuousMoveEnabled;

        /// <summary>
        /// True when smooth continuous turning is active instead of snap turning.
        /// </summary>
        public bool IsSmoothTurnEnabled => smoothTurnEnabled;

        private void Awake()
        {
            ApplyLocomotionSettings();
            ConfigureVignetteProviders();
        }

        /// <summary>
        /// Enables or disables continuous move (with comfort vignette) while keeping teleport anchors usable.
        /// </summary>
        public void SetContinuousMoveEnabled(bool enabled)
        {
            if (continuousMoveEnabled == enabled)
            {
                return;
            }

            continuousMoveEnabled = enabled;
            ApplyLocomotionSettings();
            ContinuousMoveChanged?.Invoke(continuousMoveEnabled);
        }

        /// <summary>
        /// Toggles between Teleport-default mode and Continuous Move mode.
        /// </summary>
        public void ToggleContinuousMove()
        {
            SetContinuousMoveEnabled(!continuousMoveEnabled);
        }

        /// <summary>
        /// Switches between Snap Turn (false) and Smooth Turn (true).
        /// </summary>
        public void SetSmoothTurnEnabled(bool enabled)
        {
            smoothTurnEnabled = enabled;
            ApplyLocomotionSettings();
        }

        /// <summary>
        /// Adjusts Camera Offset height to support seated vs standing comfort.
        /// </summary>
        public void SetSeatedMode(bool seated)
        {
            if (cameraOffset == null)
            {
                return;
            }

            Vector3 localPos = cameraOffset.localPosition;
            localPos.y = seated ? seatedOffsetY : standingOffsetY;
            cameraOffset.localPosition = localPos;
        }

        /// <summary>
        /// Locks or unlocks player locomotion (Teleport, Continuous Move, Snap Turn) when inspecting the Microscope.
        /// </summary>
        public void SetLocomotionLocked(bool locked)
        {
            locomotionLocked = locked;
            ApplyLocomotionSettings();
        }

        private void ApplyLocomotionSettings()
        {
            if (locomotionLocked)
            {
                if (leftControllerActions != null)
                {
                    leftControllerActions.smoothMotionEnabled = false;
                    leftControllerActions.smoothTurnEnabled = false;
                }

                if (rightControllerActions != null)
                {
                    rightControllerActions.smoothMotionEnabled = false;
                    rightControllerActions.smoothTurnEnabled = false;
                }

                if (continuousMoveProvider != null) continuousMoveProvider.enabled = false;
                if (snapTurnProvider != null) snapTurnProvider.enabled = false;
                if (teleportProvider != null) teleportProvider.enabled = false;
                return;
            }

            if (leftControllerActions != null)
            {
                leftControllerActions.smoothMotionEnabled = continuousMoveEnabled;
                leftControllerActions.smoothTurnEnabled = smoothTurnEnabled;
            }

            if (rightControllerActions != null)
            {
                rightControllerActions.smoothMotionEnabled = false;
                rightControllerActions.smoothTurnEnabled = smoothTurnEnabled;
            }

            if (continuousMoveProvider != null)
            {
                continuousMoveProvider.enabled = continuousMoveEnabled;
            }

            if (snapTurnProvider != null)
            {
                snapTurnProvider.enabled = !smoothTurnEnabled;
            }

            if (teleportProvider != null)
            {
                teleportProvider.enabled = true;
            }
        }

        private void ConfigureVignetteProviders()
        {
            if (vignetteController == null)
            {
                return;
            }

            var providers = vignetteController.locomotionVignetteProviders;
            EnsureVignetteProvider(providers, continuousMoveProvider);
            EnsureVignetteProvider(providers, snapTurnProvider);
            EnsureVignetteProvider(providers, teleportProvider);
        }

        private static void EnsureVignetteProvider(
            System.Collections.Generic.List<LocomotionVignetteProvider> list,
            LocomotionProvider targetProvider)
        {
            if (list == null || targetProvider == null)
            {
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].locomotionProvider == targetProvider)
                {
                    list[i].enabled = true;
                    return;
                }
            }

            list.Add(new LocomotionVignetteProvider
            {
                enabled = true,
                locomotionProvider = targetProvider
            });
        }
    }
}
