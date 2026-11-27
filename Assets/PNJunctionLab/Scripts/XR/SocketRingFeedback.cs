using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PNJunctionLab
{
    /// <summary>
    /// Updates a socket's indicator ring color (idle / valid hover / invalid hover / snapped)
    /// and plays a procedural snap sound when an object locks into the socket.
    /// </summary>
    public class SocketRingFeedback : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private XRSocketInteractor socket;
        [SerializeField] private Renderer ringRenderer;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private Color idleColor = new Color(0.15f, 0.72f, 0.95f, 1f);
        [SerializeField] private Color validHoverColor = new Color(0.20f, 0.95f, 0.42f, 1f);
        [SerializeField] private Color invalidHoverColor = new Color(0.95f, 0.24f, 0.20f, 1f);
        [SerializeField] private Color occupiedColor = new Color(0.15f, 0.88f, 0.55f, 1f);

        public System.Func<IXRSelectInteractable, bool> CustomValidityPredicate { get; set; }

        private MaterialPropertyBlock propertyBlock;
        private float invalidFlashUntilTime = -1f;

        private void Awake()
        {
            if (socket == null)
            {
                socket = GetComponent<XRSocketInteractor>();
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            propertyBlock = new MaterialPropertyBlock();
            ApplyRingColor(idleColor);
        }

        private void Update()
        {
            if (invalidFlashUntilTime > 0f && Time.time >= invalidFlashUntilTime)
            {
                invalidFlashUntilTime = -1f;
                ApplyRingColor(socket != null && socket.hasSelection ? occupiedColor : idleColor);
            }
        }

        /// <summary>
        /// Briefly holds the invalid red indicator color on the socket ring (used when rejecting wrong-group dopants).
        /// </summary>
        public void FlashInvalid(float duration = 0.75f)
        {
            invalidFlashUntilTime = Time.time + Mathf.Max(0.1f, duration);
            ApplyRingColor(invalidHoverColor);
        }

        private void OnEnable()
        {
            if (socket == null)
            {
                return;
            }

            socket.hoverEntered.AddListener(HandleHoverEntered);
            socket.hoverExited.AddListener(HandleHoverExited);
            socket.selectEntered.AddListener(HandleSelectEntered);
            socket.selectExited.AddListener(HandleSelectExited);
        }

        private void OnDisable()
        {
            if (socket == null)
            {
                return;
            }

            socket.hoverEntered.RemoveListener(HandleHoverEntered);
            socket.hoverExited.RemoveListener(HandleHoverExited);
            socket.selectEntered.RemoveListener(HandleSelectEntered);
            socket.selectExited.RemoveListener(HandleSelectExited);
        }

        private void HandleHoverEntered(HoverEnterEventArgs args)
        {
            if (socket.hasSelection)
            {
                return;
            }

            bool isValid = args.interactableObject is IXRSelectInteractable selectable
                && socket.CanSelect(selectable)
                && (CustomValidityPredicate == null || CustomValidityPredicate(selectable));
            ApplyRingColor(isValid ? validHoverColor : invalidHoverColor);
            if (!isValid)
            {
                ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.SocketInvalid, 0.45f);
            }
        }

        private void HandleHoverExited(HoverExitEventArgs args)
        {
            if (Time.time < invalidFlashUntilTime) return;
            ApplyRingColor(socket.hasSelection ? occupiedColor : idleColor);
        }

        private void HandleSelectEntered(SelectEnterEventArgs args)
        {
            if (Time.time < invalidFlashUntilTime) return;
            ApplyRingColor(occupiedColor);
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.SocketSnap, 0.65f);
        }

        private void HandleSelectExited(SelectExitEventArgs args)
        {
            if (Time.time < invalidFlashUntilTime) return;
            ApplyRingColor(idleColor);
        }

        private void ApplyRingColor(Color color)
        {
            if (ringRenderer == null)
            {
                return;
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            ringRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(EmissionColorId, color * 0.65f);
            ringRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
