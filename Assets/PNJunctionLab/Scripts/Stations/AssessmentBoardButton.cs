using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PNJunctionLab
{
    /// <summary>
    /// Whiteboard card button supporting XRI ray/direct/pinch selection via XRSimpleInteractable
    /// plus TrackedDeviceGraphicRaycaster UI.Button clicks with debounced activation and visual card states.
    /// </summary>
    public class AssessmentBoardButton : MonoBehaviour
    {
        [SerializeField] private XRSimpleInteractable interactable;
        [SerializeField] private Button uiRelayButton;
        [SerializeField] private Image cardBackground;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private float clickCooldown = 0.22f;

        public event Action<AssessmentBoardButton> Clicked;

        private float lastClickTime = -10f;
        private bool controlsEnabled = true;

        private void Awake()
        {
            if (interactable == null)
            {
                interactable = GetComponent<XRSimpleInteractable>();
            }

            if (audioSource == null)
            {
                audioSource = GetComponentInParent<AudioSource>();
            }
        }

        private void OnEnable()
        {
            if (interactable != null)
            {
                interactable.selectEntered.AddListener(HandleXriSelect);
            }

            if (uiRelayButton != null)
            {
                uiRelayButton.onClick.AddListener(TriggerClick);
            }
        }

        private void OnDisable()
        {
            if (interactable != null)
            {
                interactable.selectEntered.RemoveListener(HandleXriSelect);
            }

            if (uiRelayButton != null)
            {
                uiRelayButton.onClick.RemoveListener(TriggerClick);
            }
        }

        /// <summary>
        /// Enables or disables both XRI and World-Space UI click pathways for this whiteboard card.
        /// </summary>
        public void SetInteractable(bool active)
        {
            controlsEnabled = active;
            if (interactable != null)
            {
                interactable.enabled = active;
            }

            if (uiRelayButton != null)
            {
                uiRelayButton.interactable = active;
            }
        }

        /// <summary>
        /// Updates the UI card fill tint (used to mark default, correct green, or incorrect red states).
        /// </summary>
        public void SetCardColor(Color color)
        {
            if (cardBackground != null)
            {
                cardBackground.color = color;
            }
        }

        /// <summary>
        /// Programmatically or manually activates the button subject to cooldown protection.
        /// </summary>
        public void TriggerClick()
        {
            if (!controlsEnabled || !isActiveAndEnabled)
            {
                return;
            }

            if (Time.unscaledTime - lastClickTime < clickCooldown)
            {
                return;
            }

            lastClickTime = Time.unscaledTime;
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.35f);
            Clicked?.Invoke(this);
        }

        private void HandleXriSelect(SelectEnterEventArgs args)
        {
            TriggerClick();
        }
    }
}
