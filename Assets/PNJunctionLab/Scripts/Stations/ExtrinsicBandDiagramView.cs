using System;
using TMPro;
using UnityEngine;

namespace PNJunctionLab
{
    /// <summary>
    /// Live Energy Band Diagram for N-Type and P-Type doping stations (Stations 2 & 3).
    /// Renders Ec, Ev, Eg, intrinsic midgap Ei, Donor Level (Ed ~0.045 eV below Ec) or
    /// Acceptor Level (Ea ~0.045 eV above Ev), dynamic Fermi level (EF) shifting upward (N) or downward (P),
    /// and animated majority carrier dots driven strictly by SemiconductorModel data.
    /// </summary>
    public class ExtrinsicBandDiagramView : MonoBehaviour
    {
        [Header("Band Rects & Label Backings")]
        [SerializeField] private RectTransform conductionBandRect;
        [SerializeField] private RectTransform valenceBandRect;
        [SerializeField] private RectTransform intrinsicEiLineRect;
        [SerializeField] private RectTransform dopantLevelLineRect;
        [SerializeField] private RectTransform fermiLevelLineRect;
        [SerializeField] private RectTransform dopantBadgeRect;
        [SerializeField] private RectTransform fermiBadgeRect;
        [SerializeField] private RectTransform eiBadgeRect;

        [Header("Labels")]
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text ecLabel;
        [SerializeField] private TMP_Text evLabel;
        [SerializeField] private TMP_Text dopantLevelLabel;
        [SerializeField] private TMP_Text eiLabel;
        [SerializeField] private TMP_Text fermiLabel;
        [SerializeField] private TMP_Text regimeFooterLabel;

        [Header("Carrier Dots")]
        [SerializeField] private RectTransform[] conductionDots;
        [SerializeField] private RectTransform[] valenceDots;

        [Header("Diagram Geometry (Canvas Units)")]
        [SerializeField] private float diagramTopY = 148f;
        [SerializeField] private float diagramBottomY = -148f;
        [SerializeField] private float minBandThickness = 46f;
        [SerializeField] private float referenceMaxGapEv = 1.40f;
        [SerializeField] private float maxGapHeightPx = 196f;
        [SerializeField] private float dopantOffsetEv = 0.045f;

        private float currentEcBottomY = 60f;
        private float currentEvTopY = -60f;
        private float[] dotPhases;
        private int activeConductionDotCount;
        private int activeValenceDotCount;

        private void Awake()
        {
            EnsureInit();
        }

        private void EnsureInit()
        {
            if (dotPhases == null)
            {
                int count = Mathf.Max(
                    conductionDots != null ? conductionDots.Length : 0,
                    valenceDots != null ? valenceDots.Length : 0);
                dotPhases = new float[Mathf.Max(1, count)];
                for (int i = 0; i < dotPhases.Length; i++)
                {
                    dotPhases[i] = (i * 0.31f) % 1f;
                }
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            AnimateDots(conductionDots, activeConductionDotCount, currentEcBottomY + 8f, currentEcBottomY + 24f, dt);
            AnimateDots(valenceDots, activeValenceDotCount, currentEvTopY - 24f, currentEvTopY - 8f, dt);
        }

        /// <summary>
        /// Refreshes energy band levels, Ed/Ea marker position, EF shift, and populated carriers from SemiconductorModel.
        /// </summary>
        public void SetBandState(
            ExtrinsicDopingKind mode,
            SemiconductorMaterial material,
            string dopantSymbol,
            double concentrationCm3,
            CarrierConcentrations concentrations)
        {
            EnsureInit();

            MaterialProperties props = SemiconductorModel.GetMaterialProperties(material);
            double egEv = props.BandGapEv;
            float gapFraction = Mathf.Clamp((float)(egEv / referenceMaxGapEv), 0.25f, 1f);
            float gapHeight = gapFraction * maxGapHeightPx;
            float halfGap = gapHeight * 0.5f;

            currentEcBottomY = halfGap;
            currentEvTopY = -halfGap;

            float ecHeight = Mathf.Max(minBandThickness, diagramTopY - currentEcBottomY);
            float evHeight = Mathf.Max(minBandThickness, currentEvTopY - diagramBottomY);

            if (conductionBandRect != null)
            {
                conductionBandRect.anchoredPosition = new Vector2(0f, currentEcBottomY + ecHeight * 0.5f);
                conductionBandRect.sizeDelta = new Vector2(conductionBandRect.sizeDelta.x, ecHeight);
            }

            if (valenceBandRect != null)
            {
                valenceBandRect.anchoredPosition = new Vector2(0f, currentEvTopY - evHeight * 0.5f);
                valenceBandRect.sizeDelta = new Vector2(valenceBandRect.sizeDelta.x, evHeight);
            }

            if (intrinsicEiLineRect != null)
            {
                intrinsicEiLineRect.anchoredPosition = Vector2.zero;
            }

            // Place Donor (Ed = Ec - 0.045 eV) or Acceptor (Ea = Ev + 0.045 eV) clearly separated from EF.
            float visualLevelMargin = Mathf.Clamp((float)(dopantOffsetEv / egEv) * gapHeight, 13f, 18f);
            float dopantY = mode == ExtrinsicDopingKind.NType
                ? currentEcBottomY - visualLevelMargin
                : currentEvTopY + visualLevelMargin;

            if (dopantLevelLineRect != null)
            {
                dopantLevelLineRect.anchoredPosition = new Vector2(0f, dopantY);
            }

            // Fermi level position EF strictly from SemiconductorModel's FermiShiftFromMidgapEv.
            double halfGapEv = Math.Max(0.1, egEv * 0.5);
            float efNormalized = Mathf.Clamp((float)(concentrations.FermiShiftFromMidgapEv / halfGapEv), -0.94f, 0.94f);
            float efY = efNormalized * (halfGap - 8f);

            if (fermiLevelLineRect != null)
            {
                fermiLevelLineRect.anchoredPosition = new Vector2(0f, efY);
            }

            // Position Ed/Ea badge away from both dopantY and efY, backed by an opaque dark pill above lines.
            float dopLblY = dopantY;
            if (Mathf.Abs(dopLblY - efY) < 22f)
            {
                dopLblY = mode == ExtrinsicDopingKind.NType ? efY - 22f : efY + 22f;
                dopLblY = Mathf.Clamp(dopLblY, currentEvTopY + 18f, currentEcBottomY - 18f);
                if (Mathf.Abs(dopLblY) < 18f)
                {
                    dopLblY = mode == ExtrinsicDopingKind.NType ? 20f : -20f;
                }
            }

            string dopTxt = mode == ExtrinsicDopingKind.NType
                ? $"Ed ({dopantOffsetEv:F3} eV below Ec)"
                : $"Ea ({dopantOffsetEv:F3} eV above Ev)";

            if (dopantBadgeRect != null)
            {
                dopantBadgeRect.anchoredPosition = new Vector2(-225f, dopLblY);
                if (dopantLevelLabel != null)
                {
                    dopantLevelLabel.rectTransform.anchoredPosition = Vector2.zero;
                    dopantLevelLabel.text = dopTxt;
                }
            }
            else if (dopantLevelLabel != null)
            {
                dopantLevelLabel.rectTransform.anchoredPosition = new Vector2(-215f, dopLblY);
                dopantLevelLabel.text = dopTxt;
            }

            float efLblY = Mathf.Clamp(efY + (mode == ExtrinsicDopingKind.NType ? -16f : 16f), currentEvTopY + 18f, currentEcBottomY - 18f);
            if (Mathf.Abs(efLblY) < 16f)
            {
                efLblY = mode == ExtrinsicDopingKind.NType ? 18f : -18f;
            }

            string efTxt = $"EF ({concentrations.FermiShiftFromMidgapEv:+0.000;-0.000;0.000} eV)";
            if (fermiBadgeRect != null)
            {
                fermiBadgeRect.anchoredPosition = new Vector2(225f, efLblY);
                if (fermiLabel != null)
                {
                    fermiLabel.rectTransform.anchoredPosition = Vector2.zero;
                    fermiLabel.text = efTxt;
                }
            }
            else if (fermiLabel != null)
            {
                fermiLabel.rectTransform.anchoredPosition = new Vector2(215f, efLblY);
                fermiLabel.text = efTxt;
            }

            if (eiBadgeRect != null)
            {
                eiBadgeRect.anchoredPosition = Vector2.zero;
            }

            float concT = Mathf.Clamp01((float)((Math.Log10(Math.Max(1e14, concentrationCm3)) - 14.0) / 4.0));
            if (mode == ExtrinsicDopingKind.NType)
            {
                activeConductionDotCount = Mathf.RoundToInt(Mathf.Lerp(3f, conductionDots != null ? conductionDots.Length : 8, concT));
                activeValenceDotCount = concT < 0.1f ? 1 : 0;
            }
            else
            {
                activeValenceDotCount = Mathf.RoundToInt(Mathf.Lerp(3f, valenceDots != null ? valenceDots.Length : 8, concT));
                activeConductionDotCount = concT < 0.1f ? 1 : 0;
            }

            if (titleLabel != null)
            {
                titleLabel.text = mode == ExtrinsicDopingKind.NType
                    ? "ENERGY BAND DIAGRAM — N-TYPE (Not to scale)"
                    : "ENERGY BAND DIAGRAM — P-TYPE (Not to scale)";
            }

            if (ecLabel != null)
            {
                ecLabel.rectTransform.anchoredPosition = new Vector2(0f, currentEcBottomY + ecHeight * 0.65f);
                ecLabel.text = "Ec (Conduction Band)";
            }

            if (evLabel != null)
            {
                evLabel.rectTransform.anchoredPosition = new Vector2(0f, currentEvTopY - evHeight * 0.65f);
                evLabel.text = "Ev (Valence Band)";
            }

            if (eiLabel != null)
            {
                eiLabel.text = "Ei Midgap";
            }

            if (regimeFooterLabel != null)
            {
                string sym = string.IsNullOrEmpty(dopantSymbol) ? (mode == ExtrinsicDopingKind.NType ? "P" : "B") : dopantSymbol;
                regimeFooterLabel.text = mode == ExtrinsicDopingKind.NType
                    ? $"Donor {sym}: EF shifts UP toward Ec | Eg = {egEv:F2} eV | n >> p"
                    : $"Acceptor {sym}: EF shifts DOWN toward Ev | Eg = {egEv:F2} eV | p >> n";
            }
        }

        private void AnimateDots(RectTransform[] dots, int activeCount, float minY, float maxY, float dt)
        {
            if (dots == null) return;
            for (int i = 0; i < dots.Length; i++)
            {
                RectTransform rt = dots[i];
                if (rt == null) continue;
                bool visible = i < activeCount;
                if (rt.gameObject.activeSelf != visible)
                {
                    rt.gameObject.SetActive(visible);
                }

                if (!visible) continue;

                dotPhases[i] = (dotPhases[i] + dt * (0.28f + (i % 3) * 0.08f)) % 1f;
                float x = Mathf.Lerp(-165f, 165f, dotPhases[i]);
                float y = Mathf.Lerp(minY, maxY, ((i * 37) % 100) / 100f);
                rt.anchoredPosition = new Vector2(x, y);
            }
        }
    }
}
