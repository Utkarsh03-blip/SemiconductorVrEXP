using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PNJunctionLab
{
    /// <summary>
    /// Live World-Space Energy Band Diagram showing Valence Band (Ev), Conduction Band (Ec),
    /// Bandgap (Eg), midgap Intrinsic Fermi Level (Ei), and animated thermal carrier excitation.
    /// Reads Eg directly from SemiconductorModel.GetMaterialProperties.
    /// </summary>
    public class EnergyBandDiagramView : MonoBehaviour
    {
        [Header("Band Rects & Labels")]
        [SerializeField] private RectTransform conductionBandRect;
        [SerializeField] private RectTransform valenceBandRect;
        [SerializeField] private RectTransform fermiLineRect;
        [SerializeField] private RectTransform gapRegionRect;
        [SerializeField] private TMP_Text ecLabel;
        [SerializeField] private TMP_Text evLabel;
        [SerializeField] private TMP_Text egLabel;
        [SerializeField] private TMP_Text eiLabel;
        [SerializeField] private TMP_Text regimeLabel;

        [Header("Pooled Excitation Dots")]
        [SerializeField] private RectTransform[] conductionElectrons;
        [SerializeField] private RectTransform[] valenceHoles;
        [SerializeField] private RectTransform[] transitionArrows;

        [Header("Diagram Bounds (Canvas Units)")]
        [SerializeField] private float diagramTopY = 128f;
        [SerializeField] private float diagramBottomY = -128f;
        [SerializeField] private float minBandThickness = 48f;
        [SerializeField] private float referenceMaxGapEv = 1.45f;
        [SerializeField] private float maxGapHeightPx = 152f;

        private float excitationLevel01;
        private float currentEcBottomY = 48f;
        private float currentEvTopY = -48f;
        private float[] arrowPhases;
        private float[] carrierPhases;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (arrowPhases == null)
            {
                int arrowCount = transitionArrows != null ? transitionArrows.Length : 0;
                arrowPhases = new float[arrowCount];
                for (int i = 0; i < arrowCount; i++)
                {
                    arrowPhases[i] = i / Mathf.Max(1f, (float)arrowCount);
                }
            }

            if (carrierPhases == null)
            {
                int electronCount = conductionElectrons != null ? conductionElectrons.Length : 0;
                carrierPhases = new float[electronCount];
                for (int i = 0; i < electronCount; i++)
                {
                    carrierPhases[i] = (i * 0.37f) % 1f;
                }
            }
        }

        private void Update()
        {
            AnimateDiagramCarriers(Time.deltaTime);
        }

        /// <summary>
        /// Refreshes band positions, gap energy readout, and thermal excitation fraction from SemiconductorModel data.
        /// </summary>
        public void SetBandState(SemiconductorMaterial material, double temperatureK, CarrierConcentrations concentrations)
        {
            EnsureInitialized();
            MaterialProperties props = SemiconductorModel.GetMaterialProperties(material);
            float gapFraction = Mathf.Clamp((float)(props.BandGapEv / referenceMaxGapEv), 0.25f, 1f);
            float gapHeight = gapFraction * maxGapHeightPx;
            float halfGap = gapHeight * 0.5f;

            currentEcBottomY = halfGap;
            currentEvTopY = -halfGap;

            float ecHeight = Mathf.Max(minBandThickness, diagramTopY - currentEcBottomY);
            float evHeight = Mathf.Max(minBandThickness, currentEvTopY - diagramBottomY);

            if (conductionBandRect != null)
            {
                conductionBandRect.anchoredPosition = new Vector2(0f, currentEcBottomY + (ecHeight * 0.5f));
                conductionBandRect.sizeDelta = new Vector2(conductionBandRect.sizeDelta.x, ecHeight);
            }

            if (valenceBandRect != null)
            {
                valenceBandRect.anchoredPosition = new Vector2(0f, currentEvTopY - (evHeight * 0.5f));
                valenceBandRect.sizeDelta = new Vector2(valenceBandRect.sizeDelta.x, evHeight);
            }

            if (gapRegionRect != null)
            {
                gapRegionRect.anchoredPosition = Vector2.zero;
                gapRegionRect.sizeDelta = new Vector2(gapRegionRect.sizeDelta.x, gapHeight);
            }

            if (fermiLineRect != null)
            {
                fermiLineRect.anchoredPosition = new Vector2(0f, (float)concentrations.FermiShiftFromMidgapEv);
            }

            if (ecLabel != null)
            {
                ecLabel.text = "<b>Ec</b> Conduction Band";
                ecLabel.rectTransform.anchoredPosition = new Vector2(-165f, currentEcBottomY + 24f);
            }

            if (evLabel != null)
            {
                evLabel.text = "<b>Ev</b> Valence Band";
                evLabel.rectTransform.anchoredPosition = new Vector2(-165f, currentEvTopY - 24f);
            }

            if (egLabel != null)
            {
                egLabel.text = "Eg = " + props.BandGapEv.ToString("F2") + " eV (" + props.Symbol + ")";
                egLabel.rectTransform.anchoredPosition = new Vector2(-165f, 18f);
            }

            if (eiLabel != null)
            {
                eiLabel.text = "Ei (Midgap Fermi)";
                eiLabel.rectTransform.anchoredPosition = new Vector2(-165f, -18f);
            }

            // Monotonic log normalization from ni so 0 K = 0 excitation, and Ge clearly exceeds Si at 300 K.
            if (temperatureK <= 0.01 || concentrations.IntrinsicNiCm3 <= 0.0)
            {
                excitationLevel01 = 0f;
            }
            else
            {
                double logNi = System.Math.Log10(System.Math.Max(1.0, concentrations.IntrinsicNiCm3));
                excitationLevel01 = Mathf.Clamp01((float)(logNi / 16.0));
            }

            if (regimeLabel != null)
            {
                if (temperatureK <= 0.01 || concentrations.IntrinsicNiCm3 <= 0.0)
                {
                    regimeLabel.text = "0 K: Frozen Insulator (0 e-/h+ pairs)";
                }
                else
                {
                    regimeLabel.text = "T = " + temperatureK.ToString("F0") + " K: Thermal e- -> Ec & h+ -> Ev";
                }
            }

            AnimateDiagramCarriers(0f);
        }

        private void AnimateDiagramCarriers(float dt)
        {
            int eLen = conductionElectrons != null ? conductionElectrons.Length : 0;
            int hLen = valenceHoles != null ? valenceHoles.Length : 0;
            int activePairs = excitationLevel01 <= 0.001f
                ? 0
                : Mathf.Clamp(Mathf.RoundToInt(excitationLevel01 * Mathf.Min(eLen, hLen)), 1, Mathf.Min(eLen, hLen));

            float xCenter = 165f;
            float xSpan = 125f;
            for (int i = 0; i < eLen; i++)
            {
                RectTransform eRect = conductionElectrons[i];
                if (eRect == null) continue;
                bool active = i < activePairs;
                if (eRect.gameObject.activeSelf != active)
                {
                    eRect.gameObject.SetActive(active);
                }

                if (active)
                {
                    carrierPhases[i] = (carrierPhases[i] + dt * (0.28f + 0.35f * excitationLevel01)) % 1f;
                    float x = xCenter + (Mathf.Sin((carrierPhases[i] * Mathf.PI * 2f) + (i * 1.1f)) * xSpan);
                    float y = currentEcBottomY + 18f + ((i % 3) * 14f);
                    eRect.anchoredPosition = new Vector2(x, y);
                }
            }

            for (int i = 0; i < hLen; i++)
            {
                RectTransform hRect = valenceHoles[i];
                if (hRect == null) continue;
                bool active = i < activePairs;
                if (hRect.gameObject.activeSelf != active)
                {
                    hRect.gameObject.SetActive(active);
                }

                if (active)
                {
                    float phase = (carrierPhases != null && i < carrierPhases.Length) ? carrierPhases[i] : 0f;
                    float x = xCenter - (Mathf.Sin((phase * Mathf.PI * 2f) + (i * 0.9f)) * xSpan);
                    float y = currentEvTopY - 18f - ((i % 3) * 14f);
                    hRect.anchoredPosition = new Vector2(x, y);
                }
            }

            int aLen = transitionArrows != null ? transitionArrows.Length : 0;
            int activeArrows = excitationLevel01 <= 0.02f ? 0 : Mathf.Clamp(Mathf.CeilToInt(excitationLevel01 * aLen), 1, aLen);
            for (int i = 0; i < aLen; i++)
            {
                RectTransform aRect = transitionArrows[i];
                if (aRect == null) continue;
                bool active = i < activeArrows;
                if (aRect.gameObject.activeSelf != active)
                {
                    aRect.gameObject.SetActive(active);
                }

                if (active)
                {
                    arrowPhases[i] = (arrowPhases[i] + dt * (0.35f + 0.65f * excitationLevel01)) % 1f;
                    float x = Mathf.Lerp(55f, 275f, (i + 0.5f) / Mathf.Max(1f, (float)aLen));
                    float y = Mathf.Lerp(currentEvTopY, currentEcBottomY, arrowPhases[i]);
                    aRect.anchoredPosition = new Vector2(x, y);
                }
            }
        }
    }
}
