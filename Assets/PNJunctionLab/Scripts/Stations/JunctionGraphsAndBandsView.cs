using System;
using TMPro;
using UnityEngine;

namespace PNJunctionLab
{
    /// <summary>
    /// Updates the 4 live spatial graphs — Carrier Concentration (n/p), Space Charge Density (rho),
    /// Electric Field (E), Electrostatic Potential (V) — and the Energy Band-Bending Diagram (Ec, Ev, flat EF)
    /// strictly by sampling SemiconductorModel.SampleJunctionProfile with zero per-frame allocations.
    /// </summary>
    public class JunctionGraphsAndBandsView : MonoBehaviour
    {
        public const int SampleCount = 33;

        [Header("Plot 1: Carrier Concentration (n & p log10)")]
        [SerializeField] private RectTransform[] electronCurveSegments;
        [SerializeField] private RectTransform[] holeCurveSegments;
        [SerializeField] private TMP_Text carrierAxisLabel;

        [Header("Plot 2: Space Charge Density rho(x)")]
        [SerializeField] private RectTransform[] chargeDensityBars;
        [SerializeField] private TMP_Text chargeAxisLabel;

        [Header("Plot 3: Electric Field E(x)")]
        [SerializeField] private RectTransform[] fieldCurveSegments;
        [SerializeField] private TMP_Text fieldAxisLabel;

        [Header("Plot 4: Electrostatic Potential V(x)")]
        [SerializeField] private RectTransform[] potentialCurveSegments;
        [SerializeField] private TMP_Text potentialAxisLabel;

        [Header("Plot 5: Energy Band-Bending Diagram")]
        [SerializeField] private RectTransform[] ecBandSegments;
        [SerializeField] private RectTransform[] evBandSegments;
        [SerializeField] private RectTransform[] efBandSegments;
        [SerializeField] private TMP_Text bandDiagramFooterLabel;
        [SerializeField] private TMP_Text bandBarrierLabel;

        [Header("Plot Dimensions (Canvas Units)")]
        [SerializeField] private float smallPlotWidth = 376f;
        [SerializeField] private float smallPlotHalfHeight = 68f;
        [SerializeField] private float bandPlotWidth = 860f;
        [SerializeField] private float bandPlotHalfHeight = 96f;

        private readonly JunctionSpatialSample[] sampleBuffer = new JunctionSpatialSample[SampleCount];

        /// <summary>
        /// Exposes latest sampled endpoints/peak values cleanly for tests and displays.
        /// </summary>
        public double LatestPotentialStepV { get; private set; }
        public double LatestPeakFieldVPerCm { get; private set; }
        public double LatestBandDropEv { get; private set; }

        /// <summary>
        /// Re-samples all 4 plots and the Energy Band-Bending Diagram directly from SemiconductorModel.
        /// </summary>
        public void RefreshFromModel(
            SemiconductorMaterial material,
            double acceptorNaCm3,
            double donorNdCm3,
            double appliedVoltageV,
            double temperatureK,
            float formationProgress01,
            JunctionElectrostatics eq)
        {
            double halfSpanCm = Math.Max(1.0e-4, Math.Max(eq.PWidthCm, eq.NWidthCm) * 1.35);
            SemiconductorModel.SampleJunctionProfile(
                material, acceptorNaCm3, donorNdCm3, appliedVoltageV, temperatureK,
                formationProgress01, halfSpanCm, sampleBuffer);

            LatestPotentialStepV = sampleBuffer[SampleCount - 1].PotentialV;
            LatestPeakFieldVPerCm = Math.Abs(sampleBuffer[SampleCount / 2].ElectricFieldVPerCm);
            LatestBandDropEv = sampleBuffer[0].EcEv - sampleBuffer[SampleCount - 1].EcEv;

            PlotCarriers(sampleBuffer);
            PlotChargeDensity(sampleBuffer, acceptorNaCm3, donorNdCm3);
            PlotElectricField(sampleBuffer, eq.PeakElectricFieldVPerCm);
            PlotPotential(sampleBuffer, eq.BuiltInPotentialV);
            PlotEnergyBands(sampleBuffer, eq, formationProgress01);
        }

        private void PlotCarriers(JunctionSpatialSample[] s)
        {
            // Log10 range [1..18] covers minority electrons/holes (~10^2) through heavy doping (10^18).
            const double minLog = 1.0;
            const double maxLog = 18.5;

            PlotPolyline(electronCurveSegments, s, smallPlotWidth, smallPlotHalfHeight,
                sample => NormalizedToY(Mathf.InverseLerp((float)minLog, (float)maxLog, (float)Math.Log10(Math.Max(10.0, sample.ElectronConcentrationCm3))), smallPlotHalfHeight));

            PlotPolyline(holeCurveSegments, s, smallPlotWidth, smallPlotHalfHeight,
                sample => NormalizedToY(Mathf.InverseLerp((float)minLog, (float)maxLog, (float)Math.Log10(Math.Max(10.0, sample.HoleConcentrationCm3))), smallPlotHalfHeight));

            if (carrierAxisLabel != null)
            {
                carrierAxisLabel.text = $"log n(x) blue, p(x) red | x in +/-{s[SampleCount - 1].PositionCm * 1e4:F2} um";
            }
        }

        private void PlotChargeDensity(JunctionSpatialSample[] s, double na, double nd)
        {
            if (chargeDensityBars == null) return;
            double maxDop = Math.Max(1e14, Math.Max(na, nd));
            double maxCharge = SemiconductorModel.ElementaryChargeQ * maxDop;

            int barCount = chargeDensityBars.Length;
            for (int i = 0; i < barCount && i < s.Length; i++)
            {
                RectTransform bar = chargeDensityBars[i];
                if (bar == null) continue;

                bool hasCharge = Math.Abs(s[i].ChargeDensityCPerCm3) > 1e-18;
                float norm = Mathf.Clamp((float)(s[i].ChargeDensityCPerCm3 / maxCharge), -1f, 1f);
                float h = Mathf.Abs(norm) * (smallPlotHalfHeight * 0.92f);
                if (!hasCharge)
                {
                    bar.gameObject.SetActive(false);
                    continue;
                }

                h = Mathf.Max(3.5f, h);

                bar.gameObject.SetActive(true);
                float x = ((i / (float)Math.Max(1, barCount - 1)) - 0.5f) * smallPlotWidth;
                float y = norm >= 0f ? h * 0.5f : -h * 0.5f;
                bar.anchoredPosition = new Vector2(x, y);
                bar.sizeDelta = new Vector2(bar.sizeDelta.x, h);

                var img = bar.GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                {
                    img.color = norm >= 0f
                        ? new Color(0.98f, 0.58f, 0.14f, 0.92f)
                        : new Color(0.14f, 0.84f, 0.82f, 0.92f);
                }
            }

            if (chargeAxisLabel != null)
            {
                chargeAxisLabel.text = $"rho: Teal -qNA ({na:0.0E+0}) | Orange +qND ({nd:0.0E+0})";
            }
        }

        private void PlotElectricField(JunctionSpatialSample[] s, double eqPeakVPerCm)
        {
            double peakScale = Math.Max(1e3, eqPeakVPerCm);
            // Electric field goes from 0 (top baseline) down to -Emax (bottom); place 0 line near top (+0.75 H).
            float zeroY = smallPlotHalfHeight * 0.68f;
            float spanDown = smallPlotHalfHeight * 1.50f;

            PlotPolyline(fieldCurveSegments, s, smallPlotWidth, smallPlotHalfHeight,
                sample => zeroY + (Mathf.Clamp((float)(sample.ElectricFieldVPerCm / peakScale), -1f, 0f) * spanDown));

            if (fieldAxisLabel != null)
            {
                fieldAxisLabel.text = $"Peak |Emax| = {LatestPeakFieldVPerCm * 1e-3:F1} kV/cm (points N -> P)";
            }
        }

        private void PlotPotential(JunctionSpatialSample[] s, double eqV0)
        {
            double vMax = Math.Max(0.25, eqV0);
            PlotPolyline(potentialCurveSegments, s, smallPlotWidth, smallPlotHalfHeight,
                sample => NormalizedToY(Mathf.Clamp01((float)(sample.PotentialV / vMax)), smallPlotHalfHeight));

            if (potentialAxisLabel != null)
            {
                potentialAxisLabel.text = $"Barrier step V(x) = {LatestPotentialStepV:F3} V (V0 = {eqV0:F3} V)";
            }
        }

        private void PlotEnergyBands(JunctionSpatialSample[] s, JunctionElectrostatics eq, float f)
        {
            double minE = double.MaxValue;
            double maxE = double.MinValue;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i].EvEv < minE) minE = s[i].EvEv;
                if (s[i].EcEv > maxE) maxE = s[i].EcEv;
                if (s[i].QuasiFermiElectronEv > maxE) maxE = s[i].QuasiFermiElectronEv;
            }

            double pad = 0.20;
            minE -= pad;
            maxE += pad;
            double eRange = Math.Max(0.5, maxE - minE);

            PlotPolyline(ecBandSegments, s, bandPlotWidth, bandPlotHalfHeight,
                sample => NormalizedToY((float)((sample.EcEv - minE) / eRange), bandPlotHalfHeight));

            PlotPolyline(evBandSegments, s, bandPlotWidth, bandPlotHalfHeight,
                sample => NormalizedToY((float)((sample.EvEv - minE) / eRange), bandPlotHalfHeight));

            PlotPolyline(efBandSegments, s, bandPlotWidth, bandPlotHalfHeight,
                sample => NormalizedToY((float)((sample.QuasiFermiElectronEv - minE) / eRange), bandPlotHalfHeight));

            if (bandBarrierLabel != null)
            {
                bandBarrierLabel.text = $"qV0 = {LatestBandDropEv:F3} eV (bands bend N down by V0)";
            }

            if (bandDiagramFooterLabel != null)
            {
                bandDiagramFooterLabel.text = f >= 0.99f
                    ? $"Equilibrium: EF is FLAT across P & N | xp={eq.PWidthMicrometers:F2} um, xn={eq.NWidthMicrometers:F2} um"
                    : $"Forming ({f * 100f:F0}%): Ec/Ev bending toward qV0 = {eq.BuiltInPotentialV:F3} eV as EF aligns";
            }
        }

        private static float NormalizedToY(float t01, float halfH)
        {
            return Mathf.Lerp(-halfH * 0.84f, halfH * 0.84f, Mathf.Clamp01(t01));
        }

        private static void PlotPolyline(
            RectTransform[] segments,
            JunctionSpatialSample[] samples,
            float plotW,
            float plotHalfH,
            Func<JunctionSpatialSample, float> ySelector)
        {
            if (segments == null || samples == null || samples.Length < 2)
            {
                return;
            }

            int segCount = Math.Min(segments.Length, samples.Length - 1);
            for (int i = 0; i < segCount; i++)
            {
                RectTransform seg = segments[i];
                if (seg == null) continue;

                float x0 = ((i / (float)(samples.Length - 1)) - 0.5f) * plotW;
                float x1 = (((i + 1) / (float)(samples.Length - 1)) - 0.5f) * plotW;
                float y0 = Mathf.Clamp(ySelector(samples[i]), -plotHalfH, plotHalfH);
                float y1 = Mathf.Clamp(ySelector(samples[i + 1]), -plotHalfH, plotHalfH);

                Vector2 p0 = new Vector2(x0, y0);
                Vector2 p1 = new Vector2(x1, y1);
                Vector2 diff = p1 - p0;
                float len = Mathf.Max(2f, diff.magnitude);
                float angleDeg = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

                seg.anchoredPosition = (p0 + p1) * 0.5f;
                seg.sizeDelta = new Vector2(len + 1.5f, seg.sizeDelta.y);
                seg.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
            }
        }
    }
}
