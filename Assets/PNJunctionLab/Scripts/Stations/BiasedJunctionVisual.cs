using System;
using TMPro;
using UnityEngine;

namespace PNJunctionLab
{
    /// <summary>
    /// Operating mode of the Station 5 biased P-N junction circuit.
    /// </summary>
    public enum DiodeBiasMode
    {
        OpenCircuit = 0,
        ForwardBias = 1,
        ReverseBias = 2,
        ReverseBreakdown = 3
    }

    /// <summary>
    /// Renders the Station 5 3D biased P-N junction cutaway ("Not to scale") with dynamic depletion width,
    /// ionized space-charge cores, pooled electron/hole carriers, breakdown avalanche glow, and LED indicator.
    /// </summary>
    public class BiasedJunctionVisual : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private const int MaxIonCountPerSide = 8;
        private const int MaxCarrierCountPerType = 16;

        [Header("3D Junction Structure (All Within 0.80m x 0.24m Box)")]
        [SerializeField] private Transform pBulkBlock;
        [SerializeField] private Transform nBulkBlock;
        [SerializeField] private Transform depletionBox;
        [SerializeField] private Renderer depletionBoxRenderer;
        [SerializeField] private Transform pBoundaryBar;
        [SerializeField] private Transform nBoundaryBar;
        [SerializeField] private Renderer breakdownGlowRenderer;

        [Header("Pooled Ions & Carriers (< 300 per view)")]
        [SerializeField] private Transform[] negativeIonMarkers;
        [SerializeField] private Transform[] positiveIonMarkers;
        [SerializeField] private Transform[] electronMarkers;
        [SerializeField] private Transform[] holeMarkers;

        [Header("Indicator LED")]
        [SerializeField] private Renderer ledBulbRenderer;
        [SerializeField] private Renderer ledHaloRenderer;

        [Header("Cutaway World-Space Labels")]
        [SerializeField] private TMP_Text cutawayHeaderText;
        [SerializeField] private TMP_Text barrierInfoText;
        [SerializeField] private TMP_Text depletionWidthText;

        private const float HalfBoxSpanX = 0.36f;
        private const float BoxDepthZ = 0.11f;
        private const float BoxHalfHeightY = 0.085f;

        private readonly Vector3[] electronPositions = new Vector3[MaxCarrierCountPerType];
        private readonly Vector3[] holePositions = new Vector3[MaxCarrierCountPerType];

        private MaterialPropertyBlock mpb;
        private DiodeBiasMode currentMode = DiodeBiasMode.OpenCircuit;
        private float currentDepletionHalfX = 0.075f;
        private float visualCurrentStrength01;
        private float visualLeakageRate;
        private bool isInitialized;

        private void Awake()
        {
            mpb = new MaterialPropertyBlock();
            SeedInitialPositions();
            isInitialized = true;
        }

        private void Update()
        {
            if (!isInitialized)
            {
                return;
            }

            float dt = Mathf.Min(0.05f, Time.deltaTime);
            AnimateCarriers(dt);
            UpdateBreakdownGlow();
        }

        /// <summary>
        /// Updates cutaway geometry, LED brightness, ion visibility, and scientific captions from SemiconductorModel outputs.
        /// </summary>
        public void ApplyState(
            DiodeBiasMode mode,
            double signedSourceVoltageV,
            double diodeVoltageV,
            double circuitCurrentA,
            JunctionElectrostatics electrostatics,
            double equilibriumDepletionUm,
            double breakdownVoltageV)
        {
            if (!isInitialized)
            {
                mpb = new MaterialPropertyBlock();
                SeedInitialPositions();
                isInitialized = true;
            }

            currentMode = mode;

            double ratio = equilibriumDepletionUm > 1e-6
                ? electrostatics.DepletionWidthMicrometers / equilibriumDepletionUm
                : 1.0;

            float visualW = Mathf.Clamp((float)(0.15 * Math.Max(0.28, Math.Min(2.45, ratio))), 0.042f, 0.36f);
            currentDepletionHalfX = visualW * 0.5f;

            UpdateBlocksAndBoundaries(currentDepletionHalfX);
            UpdateIonVisibility(currentDepletionHalfX);
            UpdateLedIndicator(mode, circuitCurrentA);

            visualCurrentStrength01 = Mathf.Clamp01((float)(Math.Abs(circuitCurrentA) / 0.025));
            visualLeakageRate = mode == DiodeBiasMode.ReverseBias ? 0.15f : 0f;

            if (cutawayHeaderText != null)
            {
                string modeLabel = mode switch
                {
                    DiodeBiasMode.ForwardBias => "FORWARD BIAS (Red -> P, Black -> N)",
                    DiodeBiasMode.ReverseBias => "REVERSE BIAS (Red -> N, Black -> P)",
                    DiodeBiasMode.ReverseBreakdown => "REVERSE BREAKDOWN (AVALANCHE ACTIVE!)",
                    _ => "OPEN CIRCUIT (Connect Red + and Black - Leads)"
                };
                cutawayHeaderText.text = $"P-N JUNCTION CUTAWAY (Not to scale) — {modeLabel}";
            }

            if (barrierInfoText != null)
            {
                if (mode == DiodeBiasMode.ReverseBreakdown)
                {
                    barrierInfoText.text =
                        $"V_br = -{breakdownVoltageV:F1} V | Impact ionization floods junction!";
                }
                else
                {
                    barrierInfoText.text =
                        $"Barrier qV_eff = {electrostatics.EffectiveBarrierV:F3} eV " +
                        $"(V0 = {electrostatics.BuiltInPotentialV:F3} V, V_applied = {diodeVoltageV:F2} V)";
                }
            }

            if (depletionWidthText != null)
            {
                string trend = mode == DiodeBiasMode.ForwardBias
                    ? "NARROWING (majority carriers cross)"
                    : mode == DiodeBiasMode.ReverseBias
                        ? "WIDENING (majority carriers blocked)"
                        : mode == DiodeBiasMode.ReverseBreakdown
                            ? "AVALANCHE BREAKDOWN (220 ohm limits I)"
                            : "EQUILIBRIUM WIDTH (I = 0)";

                depletionWidthText.text =
                    $"W = {electrostatics.DepletionWidthMicrometers:F3} um | " +
                    $"E_max = {electrostatics.PeakElectricFieldVPerCm:0.##E+0} V/cm | {trend}";
            }
        }

        private void SeedInitialPositions()
        {
            for (int i = 0; i < MaxCarrierCountPerType; i++)
            {
                float rowY = (((i % 4) - 1.5f) / 1.5f) * (BoxHalfHeightY * 0.72f);
                float colZ = (((i / 4) % 2 == 0) ? -0.028f : 0.028f);
                float tX = (i / (float)Mathf.Max(1, MaxCarrierCountPerType - 1));

                // +X is Player LEFT (P side, aligned with P-Terminal); -X is Player RIGHT (N side, aligned with N-Terminal).
                holePositions[i] = new Vector3(Mathf.Lerp(0.09f, HalfBoxSpanX - 0.03f, tX), rowY, colZ);
                electronPositions[i] = new Vector3(Mathf.Lerp(-HalfBoxSpanX + 0.03f, -0.09f, tX), rowY, -colZ);
            }
        }

        private void UpdateBlocksAndBoundaries(float halfDepX)
        {
            float bulkSpanEach = Mathf.Max(0.05f, HalfBoxSpanX - halfDepX);
            if (pBulkBlock != null)
            {
                pBulkBlock.localPosition = new Vector3(halfDepX + bulkSpanEach * 0.5f, 0f, 0f);
                pBulkBlock.localScale = new Vector3(bulkSpanEach, 0.21f, 0.13f);
            }

            if (nBulkBlock != null)
            {
                nBulkBlock.localPosition = new Vector3(-(halfDepX + bulkSpanEach * 0.5f), 0f, 0f);
                nBulkBlock.localScale = new Vector3(bulkSpanEach, 0.21f, 0.13f);
            }

            if (depletionBox != null)
            {
                depletionBox.localPosition = Vector3.zero;
                depletionBox.localScale = new Vector3(halfDepX * 2f, 0.212f, 0.132f);
            }

            if (pBoundaryBar != null)
            {
                pBoundaryBar.localPosition = new Vector3(halfDepX, 0f, -0.068f);
            }

            if (nBoundaryBar != null)
            {
                nBoundaryBar.localPosition = new Vector3(-halfDepX, 0f, -0.068f);
            }

            if (depletionBoxRenderer != null && mpb != null)
            {
                Color depColor = currentMode switch
                {
                    DiodeBiasMode.ForwardBias => new Color(0.24f, 0.72f, 0.65f, 0.34f),
                    DiodeBiasMode.ReverseBias => new Color(0.90f, 0.55f, 0.18f, 0.44f),
                    DiodeBiasMode.ReverseBreakdown => new Color(1.0f, 0.22f, 0.18f, 0.58f),
                    _ => new Color(0.55f, 0.62f, 0.74f, 0.36f)
                };
                depletionBoxRenderer.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, depColor);
                mpb.SetColor(EmissionColorId, depColor * (currentMode == DiodeBiasMode.ReverseBreakdown ? 1.2f : 0.22f));
                depletionBoxRenderer.SetPropertyBlock(mpb);
            }
        }

        private void UpdateIonVisibility(float halfDepX)
        {
            if (negativeIonMarkers != null)
            {
                int count = Mathf.Min(MaxIonCountPerSide, negativeIonMarkers.Length);
                for (int i = 0; i < count; i++)
                {
                    Transform ion = negativeIonMarkers[i];
                    if (ion == null) continue;
                    float frac = (i % 4 + 0.5f) / 4f;
                    float x = halfDepX * Mathf.Clamp(frac, 0.18f, 0.88f);
                    float y = ((i / 4) == 0 ? 0.048f : -0.048f);
                    ion.localPosition = new Vector3(x, y, -0.02f);
                    bool visible = Mathf.Abs(x) < halfDepX - 0.008f;
                    ion.gameObject.SetActive(visible);
                }
            }

            if (positiveIonMarkers != null)
            {
                int count = Mathf.Min(MaxIonCountPerSide, positiveIonMarkers.Length);
                for (int i = 0; i < count; i++)
                {
                    Transform ion = positiveIonMarkers[i];
                    if (ion == null) continue;
                    float frac = (i % 4 + 0.5f) / 4f;
                    float x = -halfDepX * Mathf.Clamp(frac, 0.18f, 0.88f);
                    float y = ((i / 4) == 0 ? 0.048f : -0.048f);
                    ion.localPosition = new Vector3(x, y, -0.02f);
                    bool visible = Mathf.Abs(x) < halfDepX - 0.008f;
                    ion.gameObject.SetActive(visible);
                }
            }
        }

        private void UpdateLedIndicator(DiodeBiasMode mode, double currentA)
        {
            Color baseColor;
            Color emissionColor;

            if (mode == DiodeBiasMode.ForwardBias && currentA >= 0.0004)
            {
                float glow = Mathf.Clamp01((float)(currentA / 0.015));
                float boost = Mathf.Lerp(0.8f, 3.2f, glow);
                baseColor = Color.Lerp(new Color(0.15f, 0.55f, 0.25f), new Color(0.28f, 1.0f, 0.45f), glow);
                emissionColor = baseColor * boost;
            }
            else if (mode == DiodeBiasMode.ReverseBreakdown)
            {
                baseColor = new Color(1.0f, 0.18f, 0.12f, 1f);
                emissionColor = baseColor * 2.8f;
            }
            else
            {
                baseColor = new Color(0.16f, 0.20f, 0.24f, 1f);
                emissionColor = Color.black;
            }

            if (ledBulbRenderer != null)
            {
                ledBulbRenderer.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, baseColor);
                mpb.SetColor(EmissionColorId, emissionColor);
                ledBulbRenderer.SetPropertyBlock(mpb);
            }

            if (ledHaloRenderer != null)
            {
                ledHaloRenderer.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, baseColor);
                mpb.SetColor(EmissionColorId, emissionColor * 0.65f);
                ledHaloRenderer.SetPropertyBlock(mpb);
            }
        }

        private void AnimateCarriers(float dt)
        {
            float halfDep = currentDepletionHalfX;
            float fwdSpeed = Mathf.Lerp(0.06f, 0.42f, visualCurrentStrength01);
            float t = Time.time;

            if (holeMarkers != null)
            {
                int count = Mathf.Min(MaxCarrierCountPerType, holeMarkers.Length);
                for (int i = 0; i < count; i++)
                {
                    Transform marker = holeMarkers[i];
                    if (marker == null) continue;
                    Vector3 p = holePositions[i];

                    if (currentMode == DiodeBiasMode.ForwardBias && visualCurrentStrength01 > 0.01f)
                    {
                        // P (+X) -> N (-X) forward injection of holes.
                        p.x -= fwdSpeed * dt;
                        if (p.x < -HalfBoxSpanX)
                        {
                            p.x = HalfBoxSpanX;
                        }
                    }
                    else if (currentMode == DiodeBiasMode.ReverseBreakdown)
                    {
                        p.x += (i % 3 == 0 ? -0.35f : 0.35f) * dt;
                        if (p.x > HalfBoxSpanX) p.x = -HalfBoxSpanX;
                        if (p.x < -HalfBoxSpanX) p.x = HalfBoxSpanX;
                    }
                    else if (currentMode == DiodeBiasMode.ReverseBias)
                    {
                        // Minority hole leakage from N (-X) sweeps toward P (+X); majority holes pulled to P outer edge (+X).
                        if (i >= count - 2)
                        {
                            p.x += 0.14f * dt;
                            if (p.x > HalfBoxSpanX) p.x = -HalfBoxSpanX;
                        }
                        else
                        {
                            float targetX = HalfBoxSpanX - 0.04f - ((i % 7) / 7f) * Mathf.Max(0.04f, (HalfBoxSpanX - halfDep) * 0.65f);
                            p.x = Mathf.Lerp(p.x, targetX + Mathf.Sin(t * 1.9f + i) * 0.01f, dt * 4f);
                        }
                    }
                    else
                    {
                        float pSpan = Mathf.Max(0.04f, HalfBoxSpanX - halfDep - 0.03f);
                        float homeX = halfDep + 0.025f + ((i % 8) / 7f) * pSpan;
                        p.x = Mathf.Lerp(p.x, homeX + Mathf.Sin(t * 2.1f + i * 0.7f) * 0.014f, dt * 3.5f);
                    }

                    p.y = Mathf.Clamp(p.y + Mathf.Cos(t * 2.4f + i) * 0.008f * dt, -BoxHalfHeightY, BoxHalfHeightY);
                    holePositions[i] = p;
                    marker.localPosition = p;
                }
            }

            if (electronMarkers != null)
            {
                int count = Mathf.Min(MaxCarrierCountPerType, electronMarkers.Length);
                for (int i = 0; i < count; i++)
                {
                    Transform marker = electronMarkers[i];
                    if (marker == null) continue;
                    Vector3 p = electronPositions[i];

                    if (currentMode == DiodeBiasMode.ForwardBias && visualCurrentStrength01 > 0.01f)
                    {
                        // N (-X) -> P (+X) forward injection of electrons.
                        p.x += fwdSpeed * dt;
                        if (p.x > HalfBoxSpanX)
                        {
                            p.x = -HalfBoxSpanX;
                        }
                    }
                    else if (currentMode == DiodeBiasMode.ReverseBreakdown)
                    {
                        p.x -= (i % 3 == 0 ? 0.35f : -0.35f) * dt;
                        if (p.x > HalfBoxSpanX) p.x = -HalfBoxSpanX;
                        if (p.x < -HalfBoxSpanX) p.x = HalfBoxSpanX;
                    }
                    else if (currentMode == DiodeBiasMode.ReverseBias)
                    {
                        // Minority electron leakage on P (+X) sweeps toward N (-X); majority electrons pulled to N outer edge (-X).
                        if (i >= count - 2)
                        {
                            p.x -= 0.14f * dt;
                            if (p.x < -HalfBoxSpanX) p.x = HalfBoxSpanX;
                        }
                        else
                        {
                            float targetX = -HalfBoxSpanX + 0.04f + ((i % 7) / 7f) * Mathf.Max(0.04f, (HalfBoxSpanX - halfDep) * 0.65f);
                            p.x = Mathf.Lerp(p.x, targetX + Mathf.Cos(t * 1.9f + i) * 0.01f, dt * 4f);
                        }
                    }
                    else
                    {
                        float nSpan = Mathf.Max(0.04f, HalfBoxSpanX - halfDep - 0.03f);
                        float homeX = -halfDep - 0.025f - ((i % 8) / 7f) * nSpan;
                        p.x = Mathf.Lerp(p.x, homeX + Mathf.Cos(t * 2.1f + i * 0.7f) * 0.014f, dt * 3.5f);
                    }

                    p.y = Mathf.Clamp(p.y + Mathf.Sin(t * 2.4f + i) * 0.008f * dt, -BoxHalfHeightY, BoxHalfHeightY);
                    electronPositions[i] = p;
                    marker.localPosition = p;
                }
            }
        }

        private void UpdateBreakdownGlow()
        {
            if (breakdownGlowRenderer == null || mpb == null)
            {
                return;
            }

            bool active = currentMode == DiodeBiasMode.ReverseBreakdown;
            breakdownGlowRenderer.enabled = active;
            if (!active)
            {
                return;
            }

            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 10f);
            Color c = new Color(1.0f, 0.24f, 0.14f, 0.85f);
            breakdownGlowRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, c);
            mpb.SetColor(EmissionColorId, c * (1.2f + pulse * 1.8f));
            breakdownGlowRenderer.SetPropertyBlock(mpb);
        }
    }
}
