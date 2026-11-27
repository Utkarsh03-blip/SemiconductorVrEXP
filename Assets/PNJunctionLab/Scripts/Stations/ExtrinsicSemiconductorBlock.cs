using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PNJunctionLab
{
    /// <summary>
    /// Output grabbable extrinsic semiconductor block (N-Type or P-Type Silicon) produced by Stations 2 & 3
    /// with physics properties ready for Phase 5's P-N Junction Assembly socket.
    /// </summary>
    public class ExtrinsicSemiconductorBlock : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private ExtrinsicDopingKind dopingKind = ExtrinsicDopingKind.NType;
        [SerializeField] private SemiconductorMaterial material = SemiconductorMaterial.Silicon;
        [SerializeField] private string dopantSymbol = "P";
        [SerializeField] private double dopingConcentrationCm3 = 1e16;
        [SerializeField] private double referenceTemperatureK = 300.0;
        [SerializeField] private XRGrabInteractable grabInteractable;
        [SerializeField] private RespawnableItem respawnableItem;
        [SerializeField] private Renderer blockRenderer;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Color nTypeColor = new Color(0.16f, 0.56f, 0.96f, 1f);
        [SerializeField] private Color pTypeColor = new Color(0.92f, 0.28f, 0.30f, 1f);

        public ExtrinsicDopingKind DopingKind => dopingKind;
        public SemiconductorMaterial Material => material;
        public string DopantSymbol => dopantSymbol;
        public double DopingConcentrationCm3 => dopingConcentrationCm3;
        public double ReferenceTemperatureK => referenceTemperatureK;
        public XRGrabInteractable GrabInteractable => grabInteractable;
        public bool IsDispensed => gameObject.activeSelf;

        private MaterialPropertyBlock propertyBlock;

        private void Awake()
        {
            if (grabInteractable == null)
            {
                grabInteractable = GetComponent<XRGrabInteractable>();
            }

            if (respawnableItem == null)
            {
                respawnableItem = GetComponent<RespawnableItem>();
            }

            propertyBlock = new MaterialPropertyBlock();
            RefreshVisuals();
        }

        /// <summary>
        /// Updates stored extrinsic doping metadata and refreshes the block's color and printed label.
        /// </summary>
        public void SetBlockState(
            ExtrinsicDopingKind kind,
            SemiconductorMaterial mat,
            string symbol,
            double concentrationCm3,
            double tempK)
        {
            dopingKind = kind;
            material = mat;
            dopantSymbol = string.IsNullOrEmpty(symbol) ? (kind == ExtrinsicDopingKind.NType ? "P" : "B") : symbol;
            dopingConcentrationCm3 = concentrationCm3;
            referenceTemperatureK = tempK;
            RefreshVisuals();
        }

        /// <summary>
        /// Computes live equilibrium carrier densities strictly from SemiconductorModel.
        /// </summary>
        public CarrierConcentrations GetConcentrations()
        {
            return dopingKind == ExtrinsicDopingKind.PType
                ? SemiconductorModel.ComputePType(material, dopingConcentrationCm3, referenceTemperatureK)
                : SemiconductorModel.ComputeNType(material, dopingConcentrationCm3, referenceTemperatureK);
        }

        /// <summary>
        /// Shows or hides the block and returns it cleanly to its output cradle when dispensed or reset.
        /// </summary>
        public void SetDispensed(bool visible, bool resetPose = true)
        {
            if (visible && !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (visible && resetPose && respawnableItem != null)
            {
                respawnableItem.Respawn();
            }
            else if (!visible)
            {
                if (respawnableItem != null && gameObject.activeSelf)
                {
                    respawnableItem.Respawn();
                }

                gameObject.SetActive(false);
            }

            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            bool isN = dopingKind == ExtrinsicDopingKind.NType;
            Color tint = isN ? nTypeColor : pTypeColor;

            if (blockRenderer != null)
            {
                if (propertyBlock == null)
                {
                    propertyBlock = new MaterialPropertyBlock();
                }

                blockRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, tint);
                propertyBlock.SetColor(EmissionColorId, tint * 0.24f);
                blockRenderer.SetPropertyBlock(propertyBlock);
            }

            if (labelText != null)
            {
                string matShort = material == SemiconductorMaterial.Silicon ? "Si" : "Ge";
                string typeWord = isN ? "N-TYPE" : "P-TYPE";
                string dopSymbol = isN ? "ND" : "NA";
                labelText.text = $"{typeWord} {matShort} ({dopantSymbol})\n{dopSymbol} = {dopingConcentrationCm3:0.0E+0} cm^-3";
            }
        }
    }
}
