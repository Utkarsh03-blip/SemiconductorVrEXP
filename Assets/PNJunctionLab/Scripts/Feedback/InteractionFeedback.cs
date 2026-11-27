using TMPro;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PNJunctionLab
{
    /// <summary>
    /// Provides hover tint/emission highlighting via MaterialPropertyBlock, optional world-space tooltip toggling,
    /// procedural interaction audio, and controller haptic feedback for any XRBaseInteractable.
    /// </summary>
    public class InteractionFeedback : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private XRBaseInteractable interactable;
        [SerializeField] private Renderer[] highlightRenderers;
        [SerializeField] private Color hoverTint = new Color(0.35f, 0.90f, 1.0f, 1f);
        [SerializeField] private Color hoverEmission = new Color(0.08f, 0.30f, 0.42f, 1f);
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private GameObject tooltipObject;
        [SerializeField] private TMP_Text tooltipText;
        [SerializeField] private string tooltipLabel;
        [SerializeField] private bool playHoverSound = true;
        [SerializeField] private bool playSelectSounds = true;
        [SerializeField] private float hoverHapticAmplitude = 0.08f;
        [SerializeField] private float hoverHapticDuration = 0.03f;
        [SerializeField] private float selectHapticAmplitude = 0.25f;
        [SerializeField] private float selectHapticDuration = 0.06f;

        private MaterialPropertyBlock propertyBlock;
        private Color[] originalBaseColors;
        private Color[] originalEmissions;
        private bool hasCachedColors;
        private int activeHoverCount;
        private Camera cachedCamera;

        private void Awake()
        {
            cachedCamera = Camera.main;
            if (interactable == null)
            {
                interactable = GetComponent<XRBaseInteractable>();
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            CacheOriginalColors();
            ApplyTooltipText();
            SetTooltipVisible(false);
        }

        private void OnEnable()
        {
            if (interactable == null)
            {
                return;
            }

            interactable.hoverEntered.AddListener(HandleHoverEntered);
            interactable.hoverExited.AddListener(HandleHoverExited);
            interactable.selectEntered.AddListener(HandleSelectEntered);
            interactable.selectExited.AddListener(HandleSelectExited);
        }

        private void OnDisable()
        {
            if (interactable != null)
            {
                interactable.hoverEntered.RemoveListener(HandleHoverEntered);
                interactable.hoverExited.RemoveListener(HandleHoverExited);
                interactable.selectEntered.RemoveListener(HandleSelectEntered);
                interactable.selectExited.RemoveListener(HandleSelectExited);
            }

            activeHoverCount = 0;
            SetHighlighted(false);
            SetTooltipVisible(false);
        }

        private void LateUpdate()
        {
            if (tooltipObject == null || !tooltipObject.activeSelf)
            {
                return;
            }

            if (cachedCamera == null)
            {
                cachedCamera = Camera.main;
            }

            if (cachedCamera != null)
            {
                Vector3 dir = tooltipObject.transform.position - cachedCamera.transform.position;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    tooltipObject.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                }
            }
        }

        /// <summary>
        /// Updates the displayed tooltip string at runtime.
        /// </summary>
        public void SetTooltipLabel(string label)
        {
            tooltipLabel = label;
            ApplyTooltipText();
        }

        /// <summary>
        /// Plays the subtle procedural cue when summoning an interactable such as LabTablet.
        /// </summary>
        public void PlaySummonSound()
        {
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.TabletSummon, 0.45f);
        }

        /// <summary>
        /// Plays the subtle procedural cue when dismissing an interactable such as LabTablet.
        /// </summary>
        public void PlayDismissSound()
        {
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.TabletDismiss, 0.42f);
        }

        /// <summary>
        /// Plays the procedural UI button click sound via this object's AudioSource.
        /// </summary>
        public void PlayClickSound()
        {
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.45f);
        }

        /// <summary>
        /// Clears active hover highlight and tooltip state cleanly when hiding the interactable.
        /// </summary>
        public void ResetVisualState()
        {
            activeHoverCount = 0;
            SetHighlighted(false);
            SetTooltipVisible(false);
        }

        private void HandleHoverEntered(HoverEnterEventArgs args)
        {
            if (args.interactorObject is XRSocketInteractor)
            {
                return;
            }

            activeHoverCount++;
            if (activeHoverCount == 1)
            {
                SetHighlighted(true);
                SetTooltipVisible(true);
                if (playHoverSound)
                {
                    ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Hover, 0.35f);
                }
            }

            TriggerHaptics(args.interactorObject, hoverHapticAmplitude, hoverHapticDuration);
        }

        private void HandleHoverExited(HoverExitEventArgs args)
        {
            if (args.interactorObject is XRSocketInteractor)
            {
                return;
            }

            activeHoverCount = Mathf.Max(0, activeHoverCount - 1);
            if (activeHoverCount == 0)
            {
                SetHighlighted(false);
                SetTooltipVisible(false);
            }
        }

        private void HandleSelectEntered(SelectEnterEventArgs args)
        {
            if (args.interactorObject is XRSocketInteractor)
            {
                return;
            }

            if (playSelectSounds)
            {
                ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Grab, 0.55f);
            }

            TriggerHaptics(args.interactorObject, selectHapticAmplitude, selectHapticDuration);
        }

        private void HandleSelectExited(SelectExitEventArgs args)
        {
            if (args.interactorObject is XRSocketInteractor)
            {
                return;
            }

            if (playSelectSounds)
            {
                ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Release, 0.45f);
            }
        }

        private void CacheOriginalColors()
        {
            if (highlightRenderers == null || highlightRenderers.Length == 0)
            {
                highlightRenderers = GetComponentsInChildren<Renderer>(true);
            }

            propertyBlock = new MaterialPropertyBlock();
            int count = highlightRenderers != null ? highlightRenderers.Length : 0;
            originalBaseColors = new Color[count];
            originalEmissions = new Color[count];

            for (int i = 0; i < count; i++)
            {
                Renderer r = highlightRenderers[i];
                if (r == null || r.sharedMaterial == null)
                {
                    originalBaseColors[i] = Color.white;
                    originalEmissions[i] = Color.black;
                    continue;
                }

                Material mat = r.sharedMaterial;
                originalBaseColors[i] = mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;
                originalEmissions[i] = mat.HasProperty(EmissionColorId) ? mat.GetColor(EmissionColorId) : Color.black;
            }

            hasCachedColors = true;
        }

        private void SetHighlighted(bool highlighted)
        {
            if (!hasCachedColors || highlightRenderers == null)
            {
                return;
            }

            for (int i = 0; i < highlightRenderers.Length; i++)
            {
                Renderer r = highlightRenderers[i];
                if (r == null)
                {
                    continue;
                }

                r.GetPropertyBlock(propertyBlock);
                if (highlighted)
                {
                    Color blended = Color.Lerp(originalBaseColors[i], hoverTint, 0.45f);
                    propertyBlock.SetColor(BaseColorId, blended);
                    propertyBlock.SetColor(EmissionColorId, originalEmissions[i] + hoverEmission);
                }
                else
                {
                    propertyBlock.SetColor(BaseColorId, originalBaseColors[i]);
                    propertyBlock.SetColor(EmissionColorId, originalEmissions[i]);
                }

                r.SetPropertyBlock(propertyBlock);
            }
        }

        private void ApplyTooltipText()
        {
            if (tooltipText != null && !string.IsNullOrEmpty(tooltipLabel))
            {
                tooltipText.text = tooltipLabel;
            }
        }

        private void SetTooltipVisible(bool visible)
        {
            if (tooltipObject != null)
            {
                tooltipObject.SetActive(visible);
            }
        }

        private static void TriggerHaptics(IXRInteractor interactor, float amplitude, float duration)
        {
            if (!XRSettings.isDeviceActive || amplitude <= 0f || duration <= 0f)
            {
                return;
            }

            if (interactor is XRBaseInputInteractor inputInteractor)
            {
                inputInteractor.SendHapticImpulse(amplitude, duration);
            }
        }
    }
}
