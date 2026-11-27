using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PNJunctionLab
{
    /// <summary>
    /// Controls the interactive Station 1 optical Microscope: opens an in-place one-eyed circular
    /// optical eyepiece viewer right in front of the player at Station 1 (without ever moving XR Origin
    /// off the lab floor), streaming live 3D lattice specimen visuals via a dedicated optical camera.
    /// </summary>
    public class MicroscopeViewController : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private const string DefaultInspectPrompt = "<b>Hover Specimen:</b> Point ray inside lens circle to inspect atoms, bonds, e- or h+";

        [Header("Bench Microscope Controls")]
        [SerializeField] private XRSimpleInteractable eyepieceInteractable;
        [SerializeField] private Button eyepieceUiButton;
        [SerializeField] private PhysicalPushButton inspectButton;
        [SerializeField] private Renderer[] stageLightRenderers;

        [Header("In-Place Optical Viewer & Specimen Chamber")]
        [SerializeField] private GameObject viewerHudRoot;
        [SerializeField] private GameObject specimenChamberRoot;
        [SerializeField] private Camera specimenCamera;
        [SerializeField] private XRSimpleInteractable lensViewportInteractable;
        [SerializeField] private Collider lensViewportCollider;
        [SerializeField] private float lensViewportRadius = 0.155f;
        [SerializeField] private LatticeHologramController latticeHologram;
        [SerializeField] private Renderer condenserBackdropRenderer;
        [SerializeField] private Renderer apertureRimRenderer;
        [SerializeField] private float viewerDistanceMeters = 0.56f;
        [SerializeField] private float maxRoomscaleStayDistance = 1.40f;
        [SerializeField] private float defaultSpecimenFov = 34f;
        [SerializeField] private float zoomedSpecimenFov = 22f;

        [Header("Eyepiece HUD Readouts & Buttons")]
        [SerializeField] private TMP_Text bezelTelemetryText;
        [SerializeField] private TMP_Text bezelObjectiveText;
        [SerializeField] private TMP_Text bezelHoverInfoText;
        [SerializeField] private TMP_Text bezelMaterialButtonText;
        [SerializeField] private TMP_Text bezelZoomButtonText;
        [SerializeField] private Button exitUiButton;
        [SerializeField] private XRSimpleInteractable exitInteractable;
        [SerializeField] private Button bezelTempDownButton;
        [SerializeField] private Button bezelTempUpButton;
        [SerializeField] private Button bezelMaterialButton;
        [SerializeField] private Button bezelZoomButton;
        [SerializeField] private AudioSource audioSource;

        [Header("Optical Backdrop Tints")]
        [SerializeField] private Color coldBackdrop = new Color(0.025f, 0.065f, 0.14f, 1f);
        [SerializeField] private Color midBackdrop = new Color(0.025f, 0.12f, 0.17f, 1f);
        [SerializeField] private Color hotBackdrop = new Color(0.18f, 0.065f, 0.025f, 1f);

        /// <summary>
        /// Raised when the player opens the one-eyed Microscope eyepiece view.
        /// </summary>
        public event Action Entered;

        /// <summary>
        /// Raised when the player closes the Microscope eyepiece view.
        /// </summary>
        public event Action Exited;

        /// <summary>
        /// Raised when temperature step (-50 / +50 K) or material toggle is clicked inside the eyepiece HUD.
        /// </summary>
        public event Action<float> TemperatureDeltaRequested;
        public event Action MaterialToggleRequested;

        public bool IsInMicroscopeView { get; private set; }
        public bool IsZoomedIn { get; private set; }

        private MaterialPropertyBlock propBlock;
        private LocomotionModeController cachedLocoController;
        private IXRHoverInteractor activeLensHoverInteractor;
        private Vector3 openedEyePosition;
        private bool isUnlocked = true;
        private float activeTempK;

        private void Awake()
        {
            propBlock = new MaterialPropertyBlock();
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            SetOpticalRigActive(false);
            ApplyZoomState();
            ApplyBackdropColor();
            UpdateHoverReadout(string.Empty);
        }

        private void OnEnable()
        {
            if (eyepieceInteractable != null)
            {
                eyepieceInteractable.selectEntered.AddListener(HandleEyepieceSelectEntered);
            }

            if (eyepieceUiButton != null)
            {
                eyepieceUiButton.onClick.AddListener(EnterMicroscopeView);
            }

            if (inspectButton != null)
            {
                inspectButton.Pressed += EnterMicroscopeView;
            }

            if (exitUiButton != null)
            {
                exitUiButton.onClick.AddListener(ExitMicroscopeView);
            }

            if (exitInteractable != null)
            {
                exitInteractable.selectEntered.AddListener(HandleExitSelectEntered);
            }

            if (bezelTempDownButton != null)
            {
                bezelTempDownButton.onClick.AddListener(HandleTempDownClicked);
            }

            if (bezelTempUpButton != null)
            {
                bezelTempUpButton.onClick.AddListener(HandleTempUpClicked);
            }

            if (bezelMaterialButton != null)
            {
                bezelMaterialButton.onClick.AddListener(HandleMaterialToggleClicked);
            }

            if (bezelZoomButton != null)
            {
                bezelZoomButton.onClick.AddListener(ToggleZoom);
            }

            if (lensViewportInteractable != null)
            {
                lensViewportInteractable.hoverEntered.AddListener(HandleLensHoverEntered);
                lensViewportInteractable.hoverExited.AddListener(HandleLensHoverExited);
            }

            if (latticeHologram != null)
            {
                latticeHologram.HoverInfoChanged += UpdateHoverReadout;
            }
        }

        private void OnDisable()
        {
            if (eyepieceInteractable != null)
            {
                eyepieceInteractable.selectEntered.RemoveListener(HandleEyepieceSelectEntered);
            }

            if (eyepieceUiButton != null)
            {
                eyepieceUiButton.onClick.RemoveListener(EnterMicroscopeView);
            }

            if (inspectButton != null)
            {
                inspectButton.Pressed -= EnterMicroscopeView;
            }

            if (exitUiButton != null)
            {
                exitUiButton.onClick.RemoveListener(ExitMicroscopeView);
            }

            if (exitInteractable != null)
            {
                exitInteractable.selectEntered.RemoveListener(HandleExitSelectEntered);
            }

            if (bezelTempDownButton != null)
            {
                bezelTempDownButton.onClick.RemoveListener(HandleTempDownClicked);
            }

            if (bezelTempUpButton != null)
            {
                bezelTempUpButton.onClick.RemoveListener(HandleTempUpClicked);
            }

            if (bezelMaterialButton != null)
            {
                bezelMaterialButton.onClick.RemoveListener(HandleMaterialToggleClicked);
            }

            if (bezelZoomButton != null)
            {
                bezelZoomButton.onClick.RemoveListener(ToggleZoom);
            }

            if (lensViewportInteractable != null)
            {
                lensViewportInteractable.hoverEntered.RemoveListener(HandleLensHoverEntered);
                lensViewportInteractable.hoverExited.RemoveListener(HandleLensHoverExited);
            }

            if (latticeHologram != null)
            {
                latticeHologram.HoverInfoChanged -= UpdateHoverReadout;
            }

            if (IsInMicroscopeView)
            {
                ExitMicroscopeView();
            }
        }

        private void Update()
        {
            if (!IsInMicroscopeView)
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam != null && Vector3.Distance(cam.transform.position, openedEyePosition) > maxRoomscaleStayDistance)
            {
                ExitMicroscopeView();
                return;
            }

            UpdateSpecimenRayHover();
        }

        /// <summary>
        /// Enables or disables bench microscope entry controls according to station lock state.
        /// </summary>
        public void SetMicroscopeUnlocked(bool unlocked)
        {
            isUnlocked = unlocked;

            if (eyepieceInteractable != null)
            {
                eyepieceInteractable.enabled = unlocked;
            }

            if (eyepieceUiButton != null)
            {
                eyepieceUiButton.interactable = unlocked;
            }

            if (inspectButton != null)
            {
                inspectButton.SetButtonEnabled(unlocked);
            }

            if (stageLightRenderers != null)
            {
                Color c = unlocked ? new Color(0.16f, 0.85f, 1.0f, 1f) : new Color(0.18f, 0.22f, 0.28f, 1f);
                Color e = unlocked ? new Color(0.12f, 0.58f, 0.80f, 1f) : Color.black;
                for (int i = 0; i < stageLightRenderers.Length; i++)
                {
                    Renderer r = stageLightRenderers[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(propBlock);
                    propBlock.SetColor(BaseColorId, c);
                    propBlock.SetColor(EmissionColorId, e);
                    r.SetPropertyBlock(propBlock);
                }
            }

            if (!unlocked && IsInMicroscopeView)
            {
                ExitMicroscopeView();
            }
        }

        /// <summary>
        /// Updates live crystal telemetry and thermal backdrop inside the microscope eyepiece view.
        /// </summary>
        public void SetTelemetry(
            SemiconductorMaterial material,
            float tempK,
            CarrierConcentrations concentrations,
            int intactBonds,
            int totalBonds,
            string currentObjective)
        {
            activeTempK = Mathf.Clamp(tempK, 0f, 500f);
            ApplyBackdropColor();

            MaterialProperties props = SemiconductorModel.GetMaterialProperties(material);

            if (bezelTelemetryText != null)
            {
                string niText = concentrations.IntrinsicNiCm3 <= 0.0
                    ? "0 cm^-3"
                    : concentrations.IntrinsicNiCm3.ToString("E2") + " cm^-3";
                bezelTelemetryText.text =
                    "<b>Material:</b> " + props.Symbol + " (Eg = " + props.BandGapEv.ToString("F2") + " eV)\n" +
                    "<b>Temperature:</b> " + activeTempK.ToString("F0") + " K\n" +
                    "<b>Carriers (n=p=ni):</b> " + niText + "\n" +
                    "<b>Conductivity:</b> " + concentrations.ConductivitySPerCm.ToString("E2") + " S/cm\n" +
                    "<b>Covalent Bonds:</b> " + intactBonds + "/" + totalBonds + " intact";
            }

            if (bezelObjectiveText != null && !string.IsNullOrEmpty(currentObjective))
            {
                bezelObjectiveText.text = currentObjective;
            }

            if (bezelMaterialButtonText != null)
            {
                bezelMaterialButtonText.text = material == SemiconductorMaterial.Silicon
                    ? "MAT: Si -> Ge"
                    : "MAT: Ge -> Si";
            }
        }

        /// <summary>
        /// Opens the in-place one-eyed optical eyepiece viewer right in front of the player at Station 1.
        /// </summary>
        public void EnterMicroscopeView()
        {
            if (!isUnlocked || IsInMicroscopeView)
            {
                return;
            }

            if (cachedLocoController == null)
            {
                cachedLocoController = FindAnyObjectByType<LocomotionModeController>();
            }

            if (cachedLocoController != null)
            {
                cachedLocoController.SetLocomotionLocked(true);
            }

            PositionViewerInFrontOfEye();
            SetOpticalRigActive(true);
            ApplyZoomState();

            IsInMicroscopeView = true;
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Success, 0.65f);
            Entered?.Invoke();
        }

        /// <summary>
        /// Exits the microscope eyepiece viewer cleanly back to the Station 1 workbench.
        /// </summary>
        public void ExitMicroscopeView()
        {
            if (!IsInMicroscopeView)
            {
                return;
            }

            IsInMicroscopeView = false;
            activeLensHoverInteractor = null;
            if (latticeHologram != null)
            {
                latticeHologram.SetExternalHover(null);
            }

            SetOpticalRigActive(false);

            if (cachedLocoController != null)
            {
                cachedLocoController.SetLocomotionLocked(false);
            }

            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.6f);
            Exited?.Invoke();
        }

        /// <summary>
        /// Toggles optical magnification inside the eyepiece between 1.0x wide view and 1.5x close-up.
        /// </summary>
        public void ToggleZoom()
        {
            IsZoomedIn = !IsZoomedIn;
            ApplyZoomState();
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Hover, 0.45f);
        }

        private void PositionViewerInFrontOfEye()
        {
            if (viewerHudRoot == null)
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                openedEyePosition = viewerHudRoot.transform.position;
                return;
            }

            openedEyePosition = cam.transform.position;
            Vector3 flatFwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (flatFwd.sqrMagnitude < 0.0001f)
            {
                flatFwd = Vector3.left;
            }
            flatFwd.Normalize();

            viewerHudRoot.transform.position = openedEyePosition + flatFwd * viewerDistanceMeters;
            viewerHudRoot.transform.rotation = Quaternion.LookRotation(flatFwd, Vector3.up);
        }

        private void SetOpticalRigActive(bool active)
        {
            if (specimenChamberRoot != null)
            {
                specimenChamberRoot.SetActive(active);
            }

            if (specimenCamera != null)
            {
                specimenCamera.enabled = active;
            }

            if (viewerHudRoot != null)
            {
                viewerHudRoot.SetActive(active);
            }
        }

        private void ApplyZoomState()
        {
            if (specimenCamera != null)
            {
                specimenCamera.fieldOfView = IsZoomedIn ? zoomedSpecimenFov : defaultSpecimenFov;
            }

            if (bezelZoomButtonText != null)
            {
                bezelZoomButtonText.text = IsZoomedIn ? "ZOOM: 1.5x" : "ZOOM: 1.0x";
            }
        }

        private void UpdateSpecimenRayHover()
        {
            if (latticeHologram == null || specimenCamera == null || lensViewportCollider == null || activeLensHoverInteractor == null)
            {
                return;
            }

            Transform rayOriginT = activeLensHoverInteractor.GetAttachTransform(lensViewportInteractable);
            if (rayOriginT == null)
            {
                rayOriginT = activeLensHoverInteractor.transform;
            }

            Ray ray = new Ray(rayOriginT.position, rayOriginT.forward);
            if (!lensViewportCollider.Raycast(ray, out RaycastHit lensHit, 4.0f))
            {
                latticeHologram.SetExternalHover(null);
                return;
            }

            Vector3 localPt = lensViewportCollider.transform.InverseTransformPoint(lensHit.point);
            float diameter = Mathf.Max(0.01f, lensViewportRadius * 2f);
            float u = Mathf.Clamp01(0.5f + (localPt.x / diameter));
            float v = Mathf.Clamp01(0.5f + (localPt.y / diameter));
            Ray specRay = specimenCamera.ViewportPointToRay(new Vector3(u, v, 0f));
            if (Physics.Raycast(specRay, out RaycastHit hit, 5.0f))
            {
                LatticeElementTarget target = hit.collider.GetComponent<LatticeElementTarget>();
                latticeHologram.SetExternalHover(target);
            }
            else
            {
                latticeHologram.SetExternalHover(null);
            }
        }

        private void HandleLensHoverEntered(HoverEnterEventArgs args)
        {
            activeLensHoverInteractor = args.interactorObject;
        }

        private void HandleLensHoverExited(HoverExitEventArgs args)
        {
            if (activeLensHoverInteractor == args.interactorObject)
            {
                activeLensHoverInteractor = null;
                latticeHologram?.SetExternalHover(null);
            }
        }

        private void UpdateHoverReadout(string info)
        {
            if (bezelHoverInfoText != null)
            {
                bezelHoverInfoText.text = string.IsNullOrEmpty(info) ? DefaultInspectPrompt : info;
            }
        }

        private void HandleEyepieceSelectEntered(SelectEnterEventArgs args)
        {
            SendSelectHaptic(args);
            EnterMicroscopeView();
        }

        private void HandleExitSelectEntered(SelectEnterEventArgs args)
        {
            SendSelectHaptic(args);
            ExitMicroscopeView();
        }

        private void HandleTempDownClicked()
        {
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.5f);
            TemperatureDeltaRequested?.Invoke(-50f);
        }

        private void HandleTempUpClicked()
        {
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.5f);
            TemperatureDeltaRequested?.Invoke(50f);
        }

        private void HandleMaterialToggleClicked()
        {
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.ButtonPress, 0.5f);
            MaterialToggleRequested?.Invoke();
        }

        private void ApplyBackdropColor()
        {
            if (propBlock == null)
            {
                propBlock = new MaterialPropertyBlock();
            }

            float t = Mathf.Clamp01(activeTempK / 500f);
            Color tint = t < 0.5f
                ? Color.Lerp(coldBackdrop, midBackdrop, t * 2f)
                : Color.Lerp(midBackdrop, hotBackdrop, (t - 0.5f) * 2f);

            if (condenserBackdropRenderer != null)
            {
                condenserBackdropRenderer.GetPropertyBlock(propBlock);
                propBlock.SetColor(BaseColorId, tint);
                propBlock.SetColor(EmissionColorId, tint * 0.9f);
                condenserBackdropRenderer.SetPropertyBlock(propBlock);
            }

            if (apertureRimRenderer != null)
            {
                Color rim = Color.Lerp(new Color(0.24f, 0.72f, 0.92f), new Color(1.0f, 0.58f, 0.22f), t);
                apertureRimRenderer.GetPropertyBlock(propBlock);
                propBlock.SetColor(BaseColorId, rim);
                propBlock.SetColor(EmissionColorId, rim * 0.45f);
                apertureRimRenderer.SetPropertyBlock(propBlock);
            }
        }

        private static void SendSelectHaptic(SelectEnterEventArgs args)
        {
            if (XRSettings.isDeviceActive && args.interactorObject is XRBaseInputInteractor inputInteractor)
            {
                inputInteractor.SendHapticImpulse(0.28f, 0.05f);
            }
        }
    }
}
