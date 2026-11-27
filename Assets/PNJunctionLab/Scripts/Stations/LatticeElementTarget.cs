using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PNJunctionLab
{
    public enum LatticeElementKind
    {
        HostAtom,
        CovalentBond,
        FreeElectron,
        Hole,
        DonorIon,
        AcceptorIon,
        IncompleteBond
    }

    /// <summary>
    /// Individual ray-hoverable crystal atom, covalent bond, free electron, hole, or dopant ion inside a lattice hologram.
    /// Reports hover focus cleanly to a shared tooltip display without allocating per-atom Canvases.
    /// </summary>
    public class LatticeElementTarget : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private XRSimpleInteractable interactable;
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private LatticeElementKind elementKind;
        [SerializeField] private int elementIndex;
        [SerializeField] private string customTooltipTitle;
        [SerializeField] private string customTooltipDetail;

        public event Action<LatticeElementTarget> Hovered;
        public event Action<LatticeElementTarget> Unhovered;

        public LatticeElementKind ElementKind => elementKind;
        public int ElementIndex => elementIndex;
        public string CustomTooltipTitle => customTooltipTitle;
        public string CustomTooltipDetail => customTooltipDetail;
        public bool IsHovered => hoverCount > 0;

        private MaterialPropertyBlock propertyBlock;
        private Color baseColor = Color.white;
        private Color baseEmission = Color.black;
        private int hoverCount;

        private void Awake()
        {
            if (interactable == null)
            {
                interactable = GetComponent<XRSimpleInteractable>();
            }

            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            CacheBaseColors();
        }

        private void OnEnable()
        {
            if (interactable != null)
            {
                interactable.hoverEntered.AddListener(HandleHoverEntered);
                interactable.hoverExited.AddListener(HandleHoverExited);
            }
        }

        private void OnDisable()
        {
            if (interactable != null)
            {
                interactable.hoverEntered.RemoveListener(HandleHoverEntered);
                interactable.hoverExited.RemoveListener(HandleHoverExited);
            }

            hoverCount = 0;
            ApplyVisualColors(false);
        }

        public void Configure(XRSimpleInteractable simple, Renderer rendererRef, LatticeElementKind kind, int index)
        {
            interactable = simple;
            targetRenderer = rendererRef;
            elementKind = kind;
            elementIndex = index;
            CacheBaseColors();
        }

        /// <summary>
        /// Updates optional per-target tooltip title and detail for extrinsic ions, valence e-, or incomplete bonds.
        /// </summary>
        public void SetCustomTooltip(string title, string detail)
        {
            customTooltipTitle = title;
            customTooltipDetail = detail;
        }

        /// <summary>
        /// Updates the resting tint/emission (for example intact vs thermally broken covalent bonds).
        /// </summary>
        public void SetRestingColor(Color baseCol, Color emissionCol)
        {
            baseColor = baseCol;
            baseEmission = emissionCol;
            ApplyVisualColors(hoverCount > 0);
        }

        private void HandleHoverEntered(HoverEnterEventArgs args)
        {
            hoverCount++;
            if (hoverCount == 1)
            {
                ApplyVisualColors(true);
                Hovered?.Invoke(this);
            }
        }

        private void HandleHoverExited(HoverExitEventArgs args)
        {
            hoverCount = Mathf.Max(0, hoverCount - 1);
            if (hoverCount == 0)
            {
                ApplyVisualColors(false);
                Unhovered?.Invoke(this);
            }
        }

        private void CacheBaseColors()
        {
            if (targetRenderer != null && targetRenderer.sharedMaterial != null)
            {
                Material mat = targetRenderer.sharedMaterial;
                baseColor = mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;
                baseEmission = mat.HasProperty(EmissionColorId) ? mat.GetColor(EmissionColorId) : Color.black;
            }
        }

        private void ApplyVisualColors(bool hovered)
        {
            if (targetRenderer == null)
            {
                return;
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, hovered ? Color.Lerp(baseColor, Color.white, 0.45f) : baseColor);
            propertyBlock.SetColor(EmissionColorId, hovered ? baseEmission + new Color(0.28f, 0.42f, 0.50f, 1f) : baseEmission);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
