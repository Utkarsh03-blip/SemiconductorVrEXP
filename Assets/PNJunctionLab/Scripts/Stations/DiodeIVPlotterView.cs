using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace PNJunctionLab
{
    /// <summary>
    /// Recorded operating-point measurement for the Station 6 I-V data table.
    /// </summary>
    public struct DiodeIVDataPoint
    {
        public string DiodeShortName;
        public double TemperatureK;
        public double SourceVoltageV;
        public double DiodeVoltageV;
        public double CircuitCurrentA;
    }

    /// <summary>
    /// Renders the live Station 6 I-V Characteristic Curve (with series resistor vs optional Ideal Shockley overlay),
    /// dynamic Knee Voltage marker, Reverse Leakage Micro-Current Inset Zoom, and 10-row Data Table strictly from
    /// SemiconductorModel with zero per-frame allocations.
    /// </summary>
    public class DiodeIVPlotterView : MonoBehaviour
    {
        public const int MainSampleCount = 41;
        public const int InsetSampleCount = 21;
        public const float MinSweepV = -6.5f;
        public const float MaxSweepV = 3.5f;
        public const float MaxCurrentMa = 35.0f;

        [Header("Main I-V Curve Plot (Canvas Units)")]
        [SerializeField] private RectTransform[] actualCurveSegments;
        [SerializeField] private RectTransform[] idealOverlaySegments;
        [SerializeField] private RectTransform operatingDot;
        [SerializeField] private RectTransform kneeMarkerLine;
        [SerializeField] private TMP_Text kneeMarkerLabel;
        [SerializeField] private float mainPlotWidth = 640f;
        [SerializeField] private float mainPlotHalfHeight = 165f;

        [Header("Reverse Leakage Inset Zoom ([-6.5 V .. 0 V])")]
        [SerializeField] private RectTransform[] zoomCurveSegments;
        [SerializeField] private TMP_Text zoomTitleLabel;
        [SerializeField] private TMP_Text zoomReadoutLabel;
        [SerializeField] private float zoomPlotWidth = 340f;
        [SerializeField] private float zoomPlotHalfHeight = 62f;

        [Header("On-Screen Data Table")]
        [SerializeField] private TMP_Text tableHeaderText;
        [SerializeField] private TMP_Text tableRowsText;

        private readonly Vector2[] actualPointsBuffer = new Vector2[MainSampleCount];
        private readonly Vector2[] idealPointsBuffer = new Vector2[MainSampleCount];
        private readonly Vector2[] zoomPointsBuffer = new Vector2[InsetSampleCount];
        private readonly StringBuilder tableBuilder = new StringBuilder(640);

        public double LatestKneeVoltageV { get; private set; }
        public double LatestSaturationCurrentA { get; private set; }

        /// <summary>
        /// Re-samples both the main I-V plot and reverse-leakage inset directly from SemiconductorModel.
        /// </summary>
        public void RefreshPlot(
            bool hasDiode,
            DiodeType diodeType,
            double sourceVoltageV,
            double seriesResistanceOhms,
            double temperatureK,
            bool showIdealOverlay)
        {
            if (!hasDiode)
            {
                SetCurveActive(actualCurveSegments, false);
                SetCurveActive(idealOverlaySegments, false);
                SetCurveActive(zoomCurveSegments, false);
                if (operatingDot != null) operatingDot.gameObject.SetActive(false);
                if (kneeMarkerLine != null) kneeMarkerLine.gameObject.SetActive(false);
                if (kneeMarkerLabel != null) kneeMarkerLabel.text = "Insert a diode (Si, Ge, LED, or Zener)";
                if (zoomTitleLabel != null) zoomTitleLabel.text = "REVERSE INSET ZOOM (No Diode)";
                if (zoomReadoutLabel != null) zoomReadoutLabel.text = "Is(T): --";
                return;
            }

            SetCurveActive(actualCurveSegments, true);
            SetCurveActive(idealOverlaySegments, showIdealOverlay);
            SetCurveActive(zoomCurveSegments, true);
            if (operatingDot != null) operatingDot.gameObject.SetActive(true);
            if (kneeMarkerLine != null) kneeMarkerLine.gameObject.SetActive(true);

            DiodeParameters p = SemiconductorModel.GetDiodeParameters(diodeType);
            LatestSaturationCurrentA = SemiconductorModel.DiodeSaturationCurrentA(diodeType, temperatureK);
            LatestKneeVoltageV = SemiconductorModel.ForwardVoltageAtCurrentV(diodeType, 0.002, temperatureK);

            // Sample Main Curve: Actual Diode + Series R vs Ideal Shockley (with R=0).
            for (int i = 0; i < MainSampleCount; i++)
            {
                float t = i / (float)(MainSampleCount - 1);
                double vSrc = Mathf.Lerp(MinSweepV, MaxSweepV, t);

                SemiconductorModel.SolveSeriesDiodeCircuit(
                    diodeType, vSrc, seriesResistanceOhms, temperatureK,
                    out double _, out double iActualA);

                float xCanvas = VoltageToCanvasX((float)vSrc, MinSweepV, MaxSweepV, mainPlotWidth);
                float yActual = CurrentMaToCanvasY((float)(iActualA * 1000.0), MaxCurrentMa, mainPlotHalfHeight);
                actualPointsBuffer[i] = new Vector2(xCanvas, yActual);

                if (showIdealOverlay)
                {
                    double iIdealA = vSrc >= 0.0
                        ? SemiconductorModel.ShockleyCurrentA(vSrc, LatestSaturationCurrentA, p.IdealityFactor, temperatureK)
                        : SemiconductorModel.DiodeCurrentWithBreakdownA(diodeType, vSrc, temperatureK);
                    float yIdeal = CurrentMaToCanvasY((float)(iIdealA * 1000.0), MaxCurrentMa, mainPlotHalfHeight);
                    idealPointsBuffer[i] = new Vector2(xCanvas, yIdeal);
                }
            }

            ApplySegments(actualCurveSegments, actualPointsBuffer);
            if (showIdealOverlay)
            {
                ApplySegments(idealOverlaySegments, idealPointsBuffer);
            }

            // Active operating point dot.
            SemiconductorModel.SolveSeriesDiodeCircuit(
                diodeType, sourceVoltageV, seriesResistanceOhms, temperatureK,
                out double activeVd, out double activeI);

            if (operatingDot != null)
            {
                operatingDot.anchoredPosition = new Vector2(
                    VoltageToCanvasX((float)sourceVoltageV, MinSweepV, MaxSweepV, mainPlotWidth),
                    CurrentMaToCanvasY((float)(activeI * 1000.0), MaxCurrentMa, mainPlotHalfHeight));
            }

            // Dynamic forward knee marker (V at 2 mA).
            float kneeX = VoltageToCanvasX((float)LatestKneeVoltageV, MinSweepV, MaxSweepV, mainPlotWidth);
            if (kneeMarkerLine != null)
            {
                kneeMarkerLine.anchoredPosition = new Vector2(kneeX, 0f);
            }

            if (kneeMarkerLabel != null)
            {
                kneeMarkerLabel.rectTransform.anchoredPosition = new Vector2(Mathf.Clamp(kneeX + 95f, -150f, 220f), mainPlotHalfHeight - 24f);
                kneeMarkerLabel.text = $"V_knee({temperatureK:F0}K) = {LatestKneeVoltageV:F2} V";
            }

            // Sample Reverse-Leakage Inset Zoom: [-6.5 V .. 0 V] magnified to show Is(T) and Zener/LED reverse knees.
            // Reference span chosen so nA..uA changes across temperature remain clearly visible, and breakdown dives cleanly to bottom.
            double insetMaxLeakageMa = diodeType == DiodeType.Germanium ? 0.25 : 0.025;
            for (int i = 0; i < InsetSampleCount; i++)
            {
                float t = i / (float)(InsetSampleCount - 1);
                double vRev = Mathf.Lerp(MinSweepV, 0.0f, t);
                double iRevA = SemiconductorModel.DiodeCurrentWithBreakdownA(diodeType, vRev, temperatureK);
                double normalizedDown = Math.Min(1.0, Math.Abs(Math.Min(0.0, iRevA * 1000.0)) / insetMaxLeakageMa);
                // Add subtle log floor exaggeration ONLY inside normalized display indicator while keeping numeric readout exact?
                // Wait: better to scale purely monotonically by log10(|I| / 1e-12) so 2.5 nA at 300 K vs uA at 400 K shifts visibly!
                double absA = Math.Max(1e-13, Math.Abs(iRevA));
                float logNorm = Mathf.InverseLerp(-12.5f, -2.0f, (float)Math.Log10(absA));
                float xZ = VoltageToCanvasX((float)vRev, MinSweepV, 0.0f, zoomPlotWidth);
                float yZ = Mathf.Lerp(zoomPlotHalfHeight * 0.78f, -zoomPlotHalfHeight * 0.85f, logNorm);
                zoomPointsBuffer[i] = new Vector2(xZ, yZ);
            }

            ApplySegments(zoomCurveSegments, zoomPointsBuffer);

            if (zoomTitleLabel != null)
            {
                zoomTitleLabel.text = $"REVERSE INSET [-6.5..0 V] (log scale | Vbr=-{p.BreakdownVoltageV:F1}V)";
            }

            if (zoomReadoutLabel != null)
            {
                zoomReadoutLabel.text =
                    $"Is({temperatureK:F0} K) = {FormatCurrentShort(LatestSaturationCurrentA)}  |  " +
                    $"Current @ {sourceVoltageV:F1}V: {FormatCurrentShort(activeI)}";
            }
        }

        /// <summary>
        /// Renders the recorded data points into the World-Space Data Table panel.
        /// </summary>
        public void RefreshDataTable(IReadOnlyList<DiodeIVDataPoint> points)
        {
            int count = points != null ? points.Count : 0;
            if (tableHeaderText != null)
            {
                tableHeaderText.text = $"<mspace=0.56em>TABLE ({count,2}/8+)   #|Type|  T |  Vsrc |  Vd   |   I</mspace>";
            }

            if (tableRowsText == null)
            {
                return;
            }

            if (count == 0)
            {
                tableRowsText.text = "No points recorded yet.\nInsert a diode, sweep Voltage & T, then press RECORD POINT (at least 8 pts).";
                return;
            }

            tableBuilder.Clear();
            tableBuilder.Append("<mspace=0.56em>");
            int startIdx = Mathf.Max(0, count - 10);
            for (int i = startIdx; i < count; i++)
            {
                DiodeIVDataPoint pt = points[i];
                tableBuilder.AppendFormat("{0,2}|{1,-4}|{2,4:F0}|{3,6:+0.00;-0.00; 0.00}V|{4,6:+0.00;-0.00; 0.00}V|{5}\n",
                    i + 1, pt.DiodeShortName, pt.TemperatureK, pt.SourceVoltageV, pt.DiodeVoltageV,
                    FormatCurrentShort(pt.CircuitCurrentA));
            }

            tableBuilder.Append("</mspace>");
            tableRowsText.text = tableBuilder.ToString().TrimEnd('\n');
        }

        private static float VoltageToCanvasX(float v, float minV, float maxV, float width)
        {
            float t = Mathf.InverseLerp(minV, maxV, Mathf.Clamp(v, minV, maxV));
            return (t - 0.5f) * width;
        }

        private static float CurrentMaToCanvasY(float ma, float maxMa, float halfH)
        {
            float norm = Mathf.Clamp(ma / maxMa, -1f, 1f);
            return norm * (halfH * 0.88f);
        }

        private static void ApplySegments(RectTransform[] segments, Vector2[] pts)
        {
            if (segments == null || pts == null || pts.Length < 2)
            {
                return;
            }

            int segCount = Math.Min(segments.Length, pts.Length - 1);
            for (int i = 0; i < segCount; i++)
            {
                RectTransform seg = segments[i];
                if (seg == null) continue;

                Vector2 p0 = pts[i];
                Vector2 p1 = pts[i + 1];
                Vector2 diff = p1 - p0;
                float len = Mathf.Max(2f, diff.magnitude);
                float angleDeg = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

                seg.anchoredPosition = (p0 + p1) * 0.5f;
                seg.sizeDelta = new Vector2(len + 1.5f, seg.sizeDelta.y);
                seg.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
            }
        }

        private static void SetCurveActive(RectTransform[] segments, bool active)
        {
            if (segments == null) return;
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] != null && segments[i].gameObject.activeSelf != active)
                {
                    segments[i].gameObject.SetActive(active);
                }
            }
        }

        private static string FormatCurrentShort(double amps)
        {
            double abs = Math.Abs(amps);
            if (abs >= 0.001) return $"{amps * 1000.0:+0.00;-0.00;0.00} mA";
            if (abs >= 1e-6) return $"{amps * 1e6:+0.0;-0.0;0.0} uA";
            if (abs >= 1e-9) return $"{amps * 1e9:+0.0;-0.0;0.0} nA";
            return $"{amps:0.#E+0} A";
        }
    }
}
