using System;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PNJunctionLab
{
    /// <summary>
    /// XRI-driven physical rotary dial usable via ray-drag, direct grab, or +/- step buttons.
    /// Reads horizontal displacement or wrist twist while selected without detaching the dial from its console.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RotaryKnobControl : MonoBehaviour
    {
        [SerializeField] private XRSimpleInteractable interactable;
        [SerializeField] private Transform dialVisual;
        [SerializeField] private Transform orientationReference;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private float minValue = 0f;
        [SerializeField] private float maxValue = 500f;
        [SerializeField] private float initialValue = 0f;
        [SerializeField] private float minAngleDeg = -135f;
        [SerializeField] private float maxAngleDeg = 135f;
        [SerializeField] private float dragRangePerUnit = 1600f;
        [SerializeField] private float rayAngleSensitivity = 3.2f;
        [SerializeField] private float audioStepInterval = 25f;

        /// <summary>
        /// Raised whenever the knob's value changes.
        /// </summary>
        public event Action<float> ValueChanged;

        public float Value { get; private set; }
        public float MinValue => minValue;
        public float MaxValue => maxValue;

        private IXRSelectInteractor activeInteractor;
        private Vector3 startAttachWorldPos;
        private Vector3 startInteractorForward;
        private float startValue;
        private float lastAudioValue;
        private bool controlsEnabled = true;

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

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            Value = Mathf.Clamp(initialValue, minValue, maxValue);
            lastAudioValue = Value;
            UpdateVisualAngle();
        }

        private void OnEnable()
        {
            if (interactable != null)
            {
                interactable.selectEntered.AddListener(HandleSelectEntered);
                interactable.selectExited.AddListener(HandleSelectExited);
            }
        }

        private void OnDisable()
        {
            if (interactable != null)
            {
                interactable.selectEntered.RemoveListener(HandleSelectEntered);
                interactable.selectExited.RemoveListener(HandleSelectExited);
            }

            activeInteractor = null;
        }

        private void Update()
        {
            if (!controlsEnabled || activeInteractor == null || interactable == null)
            {
                return;
            }

            Transform attach = activeInteractor.GetAttachTransform(interactable);
            if (attach == null)
            {
                return;
            }

            Transform basis = orientationReference != null ? orientationReference : transform;
            Vector3 posDelta = attach.position - startAttachWorldPos;
            float lateralMove = Vector3.Dot(posDelta, basis.right) + (Vector3.Dot(posDelta, basis.up) * 0.5f);

            // Also respond smoothly to pointing/wrist yaw changes during XR Device Simulator ray drags.
            Vector3 flatStart = Vector3.ProjectOnPlane(startInteractorForward, Vector3.up);
            Vector3 flatNow = Vector3.ProjectOnPlane(attach.forward, Vector3.up);
            float yawDeltaDeg = (flatStart.sqrMagnitude > 0.001f && flatNow.sqrMagnitude > 0.001f)
                ? Vector3.SignedAngle(flatStart.normalized, flatNow.normalized, Vector3.up)
                : 0f;

            float newValue = startValue
                + (lateralMove * dragRangePerUnit)
                + (yawDeltaDeg * rayAngleSensitivity);

            SetValue(newValue, true);
        }

        /// <summary>
        /// Sets the knob value directly (for step buttons, Reset, and test verification).
        /// </summary>
        public void SetValue(float value, bool notify = true)
        {
            float clamped = Mathf.Clamp(value, minValue, maxValue);
            bool changed = Mathf.Abs(clamped - Value) > 0.01f;
            Value = clamped;
            UpdateVisualAngle();

            if (!changed)
            {
                return;
            }

            if (notify && Mathf.Abs(Value - lastAudioValue) >= Mathf.Max(1f, audioStepInterval))
            {
                lastAudioValue = Value;
                ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Hover, 0.25f);
                if (XRSettings.isDeviceActive && activeInteractor is XRBaseInputInteractor inputInteractor)
                {
                    inputInteractor.SendHapticImpulse(0.10f, 0.02f);
                }
            }

            if (notify)
            {
                ValueChanged?.Invoke(Value);
            }
        }

        /// <summary>
        /// Offsets the knob value by a signed delta (used by physical -/+ buttons).
        /// </summary>
        public void StepBy(float delta)
        {
            if (!controlsEnabled)
            {
                return;
            }

            SetValue(Value + delta, true);
        }

        /// <summary>
        /// Enables or disables interaction when its parent station locks/unlocks.
        /// </summary>
        public void SetInteractive(bool interactive)
        {
            controlsEnabled = interactive;
            if (!interactive)
            {
                activeInteractor = null;
            }

            if (interactable != null)
            {
                interactable.enabled = interactive;
            }
        }

        private void HandleSelectEntered(SelectEnterEventArgs args)
        {
            if (!controlsEnabled || args.interactorObject == null)
            {
                return;
            }

            activeInteractor = args.interactorObject;
            Transform attach = activeInteractor.GetAttachTransform(interactable);
            if (attach != null)
            {
                startAttachWorldPos = attach.position;
                startInteractorForward = attach.forward;
            }
            else
            {
                startAttachWorldPos = transform.position;
                startInteractorForward = transform.forward;
            }

            startValue = Value;
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Grab, 0.35f);
        }

        private void HandleSelectExited(SelectExitEventArgs args)
        {
            if (args.interactorObject == activeInteractor)
            {
                activeInteractor = null;
                ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Release, 0.28f);
            }
        }

        private void UpdateVisualAngle()
        {
            if (dialVisual == null)
            {
                return;
            }

            float t = Mathf.InverseLerp(minValue, maxValue, Value);
            float angle = Mathf.Lerp(minAngleDeg, maxAngleDeg, t);
            dialVisual.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
    }
}
