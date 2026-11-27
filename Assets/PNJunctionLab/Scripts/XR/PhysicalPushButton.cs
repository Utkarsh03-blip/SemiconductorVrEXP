using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PNJunctionLab
{
    /// <summary>
    /// Reusable physical push button supporting XRI direct poke, pinch/grip select,
    /// and world-space UI Trigger/Left-Click via TrackedDeviceGraphicRaycaster.
    /// </summary>
    public class PhysicalPushButton : MonoBehaviour
    {
        [SerializeField] private XRSimpleInteractable interactable;
        [SerializeField] private Button uiRelayButton;
        [SerializeField] private Button uiLabelRelayButton;
        [SerializeField] private Transform capTransform;
        [SerializeField] private float pressDepth = 0.014f;
        [SerializeField] private float holdDuration = 0.14f;
        [SerializeField] private float pressCooldown = 0.22f;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private UnityEvent onPressed;

        /// <summary>
        /// Raised whenever the button is pressed via poke or ray/direct select.
        /// </summary>
        public event Action Pressed;

        private Vector3 capRestLocalPosition;
        private float lastPressTime = -10f;
        private float releaseAtTime = -1f;
        private bool hasRestPos;

        private void Awake()
        {
            if (interactable == null)
            {
                interactable = GetComponent<XRSimpleInteractable>();
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (capTransform != null)
            {
                capRestLocalPosition = capTransform.localPosition;
                hasRestPos = true;
            }
        }

        private void OnEnable()
        {
            if (interactable != null)
            {
                interactable.selectEntered.AddListener(HandleSelectEntered);
            }

            if (uiRelayButton != null)
            {
                uiRelayButton.onClick.AddListener(Press);
            }

            if (uiLabelRelayButton != null)
            {
                uiLabelRelayButton.onClick.AddListener(Press);
            }
        }

        private void OnDisable()
        {
            if (interactable != null)
            {
                interactable.selectEntered.RemoveListener(HandleSelectEntered);
            }

            if (uiRelayButton != null)
            {
                uiRelayButton.onClick.RemoveListener(Press);
            }

            if (uiLabelRelayButton != null)
            {
                uiLabelRelayButton.onClick.RemoveListener(Press);
            }

            ResetCapPosition();
        }

        /// <summary>
        /// Enables or disables all XRI and UI relay click paths for this physical button.
        /// </summary>
        public void SetButtonEnabled(bool enabledState)
        {
            enabled = enabledState;
            if (interactable != null) interactable.enabled = enabledState;
            if (uiRelayButton != null) uiRelayButton.interactable = enabledState;
            if (uiLabelRelayButton != null) uiLabelRelayButton.interactable = enabledState;
        }

        private void Update()
        {
            if (!hasRestPos || capTransform == null)
            {
                return;
            }

            bool isPressed = Time.time < releaseAtTime;
            Vector3 target = isPressed
                ? capRestLocalPosition + new Vector3(0f, -pressDepth, 0f)
                : capRestLocalPosition;
            capTransform.localPosition = Vector3.Lerp(capTransform.localPosition, target, Time.deltaTime * 18f);
        }

        /// <summary>
        /// Triggers the button press programmatically or from XRI selection.
        /// </summary>
        public void Press()
        {
            if (Time.time - lastPressTime < pressCooldown)
            {
                return;
            }

            lastPressTime = Time.time;
            releaseAtTime = Time.time + holdDuration;
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.7f);
            Pressed?.Invoke();
            onPressed?.Invoke();
        }

        private void HandleSelectEntered(SelectEnterEventArgs args)
        {
            if (XRSettings.isDeviceActive && args.interactorObject is XRBaseInputInteractor inputInteractor)
            {
                inputInteractor.SendHapticImpulse(0.35f, 0.05f);
            }

            Press();
        }

        private void ResetCapPosition()
        {
            if (hasRestPos && capTransform != null)
            {
                capTransform.localPosition = capRestLocalPosition;
            }
        }
    }
}
