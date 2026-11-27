using TMPro;
using UnityEngine;

namespace PNJunctionLab
{
    /// <summary>
    /// Controls the 3D atomic & carrier time-lapse simulation stage ("Not to scale") on Station 4.
    /// Animates pooled blue electrons, red holes, orange fixed + donor ions, teal fixed - acceptor ions,
    /// asymmetric depletion region bounds (-xp on P left, +xn on N right), N->P electric field arrows,
    /// diffusion vs drift current balance, and layer visibility toggles. Zero per-frame heap allocations.
    /// </summary>
    public class JunctionSimulationView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("Headers & Stage Status")]
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private GameObject standbyRoot;
        [SerializeField] private TMP_Text scaleHeaderText;
        [SerializeField] private TMP_Text stageNarrativeText;

        [Header("Depletion Brackets & Layer Roots")]
        [SerializeField] private Transform depletionRegionBox;
        [SerializeField] private Transform depletionBoundaryLineP;
        [SerializeField] private Transform depletionBoundaryLineN;
        [SerializeField] private GameObject holesLayerRoot;
        [SerializeField] private GameObject electronsLayerRoot;
        [SerializeField] private GameObject fixedIonsLayerRoot;
        [SerializeField] private GameObject eFieldLayerRoot;
        [SerializeField] private GameObject currentsLayerRoot;

        [Header("Pooled Carriers & Fixed Ions")]
        [SerializeField] private Transform[] holeTransforms;
        [SerializeField] private Transform[] electronTransforms;
        [SerializeField] private Renderer[] acceptorIonRenderers;
        [SerializeField] private Renderer[] donorIonRenderers;

        [Header("E-Field & Current Arrows")]
        [SerializeField] private Transform[] eFieldArrowRoots;
        [SerializeField] private Transform diffusionArrowRoot;
        [SerializeField] private Transform driftArrowRoot;
        [SerializeField] private TMP_Text currentBalanceLabel;

        [Header("Simulation Bounds (Local X: +Left=P, -Right=N)")]
        [SerializeField] private float chamberHalfWidth = 0.34f;
        [SerializeField] private float chamberHalfHeight = 0.11f;
        [SerializeField] private float chamberHalfDepth = 0.08f;

        [Header("Palette Colors")]
        [SerializeField] private Color acceptorTealCovered = new Color(0.08f, 0.34f, 0.36f, 1f);
        [SerializeField] private Color acceptorTealUncovered = new Color(0.12f, 0.86f, 0.84f, 1f);
        [SerializeField] private Color donorOrangeCovered = new Color(0.40f, 0.22f, 0.08f, 1f);
        [SerializeField] private Color donorOrangeUncovered = new Color(1.0f, 0.58f, 0.14f, 1f);

        public bool IsActive { get; private set; }
        public float LeftDepletionLocalX { get; private set; }
        public float RightDepletionLocalX { get; private set; }

        private MaterialPropertyBlock ionPropBlock;
        private Vector3[] holePos;
        private Vector3[] holeVel;
        private Vector3[] elecPos;
        private Vector3[] elecVel;
        private double currentNaCm3 = 1e16;
        private double currentNdCm3 = 1e16;
        private float currentProgress01;
        private float leftXpMeters;
        private float rightXnMeters;

        private bool showHoles = true;
        private bool showElectrons = true;
        private bool showIons = true;
        private bool showEField = true;
        private bool showCurrents = true;

        private void Awake()
        {
            ionPropBlock = new MaterialPropertyBlock();
            InitPools();
            SetSimulationActive(false);
        }

        private void Update()
        {
            if (!IsActive)
            {
                return;
            }

            float dt = Time.deltaTime;
            StepCarriers(holeTransforms, holePos, holeVel, true, dt);
            StepCarriers(electronTransforms, elecPos, elecVel, false, dt);
        }

        /// <summary>
        /// Shows or hides the active 3D junction lattice vs standby empty chamber.
        /// </summary>
        public void SetSimulationActive(bool active)
        {
            IsActive = active;
            if (visualRoot != null) visualRoot.SetActive(active);
            if (standbyRoot != null) standbyRoot.SetActive(!active);
        }

        /// <summary>
        /// Toggles visibility of each simulation layer cleanly.
        /// </summary>
        public void SetLayerVisibility(bool holes, bool electrons, bool ions, bool eField, bool currents)
        {
            showHoles = holes;
            showElectrons = electrons;
            showIons = ions;
            showEField = eField;
            showCurrents = currents;

            if (holesLayerRoot != null) holesLayerRoot.SetActive(holes);
            if (electronsLayerRoot != null) electronsLayerRoot.SetActive(electrons);
            if (fixedIonsLayerRoot != null) fixedIonsLayerRoot.SetActive(ions);
            if (eFieldLayerRoot != null) eFieldLayerRoot.SetActive(eField);
            if (currentsLayerRoot != null) currentsLayerRoot.SetActive(currents);
        }

        /// <summary>
        /// Updates asymmetric depletion boundaries, uncovered ion illumination, E-field/drift arrows,
        /// and stage narration strictly from SemiconductorModel JunctionElectrostatics.
        /// </summary>
        public void UpdateSimulationState(
            double acceptorNaCm3,
            double donorNdCm3,
            JunctionElectrostatics eq,
            float formationProgress01,
            int stageIndex)
        {
            InitPools();
            currentNaCm3 = acceptorNaCm3;
            currentNdCm3 = donorNdCm3;
            currentProgress01 = Mathf.Clamp01(formationProgress01);

            // Asymmetric visual extent matching W = xp + xn from SemiconductorModel (normalized cleanly to chamber width).
            double halfDomainCm = System.Math.Max(1.0e-4, System.Math.Max(eq.PWidthCm, eq.NWidthCm) * 1.35);
            double normP = System.Math.Min(0.88, (eq.PWidthCm * currentProgress01) / halfDomainCm);
            double normN = System.Math.Min(0.88, (eq.NWidthCm * currentProgress01) / halfDomainCm);

            // Note: Station 4 player looks -Z -> player LEFT (P-side) is +X, player RIGHT (N-side) is -X.
            leftXpMeters = (float)(normP * chamberHalfWidth);
            rightXnMeters = (float)(normN * chamberHalfWidth);
            LeftDepletionLocalX = leftXpMeters;
            RightDepletionLocalX = -rightXnMeters;

            float totalWVisual = leftXpMeters + rightXnMeters;
            float centerLocalX = (leftXpMeters - rightXnMeters) * 0.5f;

            if (depletionRegionBox != null)
            {
                bool showZone = totalWVisual > 0.004f;
                depletionRegionBox.gameObject.SetActive(showZone);
                depletionRegionBox.localPosition = new Vector3(centerLocalX, 0f, 0f);
                depletionRegionBox.localScale = new Vector3(Mathf.Max(0.004f, totalWVisual), chamberHalfHeight * 2f, chamberHalfDepth * 2f);
            }

            if (depletionBoundaryLineP != null)
            {
                depletionBoundaryLineP.localPosition = new Vector3(leftXpMeters, 0f, 0f);
            }

            if (depletionBoundaryLineN != null)
            {
                depletionBoundaryLineN.localPosition = new Vector3(-rightXnMeters, 0f, 0f);
            }

            UpdateUncoveredIons();
            UpdateFieldAndCurrentArrows();
            UpdateStageTexts(stageIndex, eq);
        }

        private void UpdateUncoveredIons()
        {
            if (acceptorIonRenderers != null)
            {
                for (int i = 0; i < acceptorIonRenderers.Length; i++)
                {
                    Renderer r = acceptorIonRenderers[i];
                    if (r == null) continue;
                    // On P-side (+X > 0): uncovered when 0 < localPosition.x <= leftXpMeters.
                    float x = r.transform.localPosition.x;
                    bool uncovered = leftXpMeters > 0.01f && x >= 0f && x <= leftXpMeters;
                    Color col = uncovered ? acceptorTealUncovered : acceptorTealCovered;
                    ApplyRendererColor(r, col, uncovered ? col * 0.55f : Color.black);
                    r.transform.localScale = Vector3.one * (uncovered ? 0.025f : 0.016f);
                }
            }

            if (donorIonRenderers != null)
            {
                for (int i = 0; i < donorIonRenderers.Length; i++)
                {
                    Renderer r = donorIonRenderers[i];
                    if (r == null) continue;
                    // On N-side (-X < 0): uncovered when 0 > localPosition.x >= -rightXnMeters.
                    float x = r.transform.localPosition.x;
                    bool uncovered = rightXnMeters > 0.01f && x <= 0f && x >= -rightXnMeters;
                    Color col = uncovered ? donorOrangeUncovered : donorOrangeCovered;
                    ApplyRendererColor(r, col, uncovered ? col * 0.55f : Color.black);
                    r.transform.localScale = Vector3.one * (uncovered ? 0.025f : 0.016f);
                }
            }
        }

        private void UpdateFieldAndCurrentArrows()
        {
            float fieldScale = Mathf.Clamp01((currentProgress01 - 0.30f) / 0.55f);
            if (eFieldArrowRoots != null)
            {
                float fieldCenter = (leftXpMeters - rightXnMeters) * 0.5f;
                for (int i = 0; i < eFieldArrowRoots.Length; i++)
                {
                    Transform a = eFieldArrowRoots[i];
                    if (a == null) continue;
                    bool visible = fieldScale > 0.05f;
                    a.gameObject.SetActive(visible);
                    if (!visible) continue;
                    float y = -0.055f + (i * 0.055f);
                    a.localPosition = new Vector3(fieldCenter, y, -chamberHalfDepth - 0.015f);
                    a.localScale = new Vector3(Mathf.Lerp(0.32f, 1.0f, fieldScale), 1f, 1f);
                }
            }

            float diffMag = currentProgress01 < 0.05f ? 0.15f : Mathf.Lerp(1.0f, 0.72f, currentProgress01);
            float driftMag = Mathf.Clamp01((currentProgress01 - 0.35f) / 0.65f) * 0.72f;

            if (diffusionArrowRoot != null)
            {
                diffusionArrowRoot.gameObject.SetActive(currentProgress01 > 0.02f);
                diffusionArrowRoot.localScale = new Vector3(diffMag, 1f, 1f);
            }

            if (driftArrowRoot != null)
            {
                driftArrowRoot.gameObject.SetActive(driftMag > 0.05f);
                driftArrowRoot.localScale = new Vector3(driftMag, 1f, 1f);
            }

            if (currentBalanceLabel != null)
            {
                if (currentProgress01 < 0.12f)
                {
                    currentBalanceLabel.text = "t=0: Sharp P|N Concentration Step";
                }
                else if (currentProgress01 < 0.45f)
                {
                    currentBalanceLabel.text = "Diffusion (e- N->P, h+ P->N) | Drift ~ 0";
                }
                else if (currentProgress01 < 0.96f)
                {
                    currentBalanceLabel.text = $"Ebi N->P grows | Drift builds ({driftMag / diffMag * 100f:F0}%)";
                }
                else
                {
                    currentBalanceLabel.text = "Equilibrium: Drift = Diffusion (Net I = 0)";
                }
            }
        }

        private void UpdateStageTexts(int stageIndex, JunctionElectrostatics eq)
        {
            if (scaleHeaderText != null)
            {
                scaleHeaderText.text = $"P-N JUNCTION SIMULATION (Not to scale) | V0={eq.BuiltInPotentialV:F3} V, W={eq.DepletionWidthMicrometers:F2} um";
            }

            if (stageNarrativeText != null)
            {
                switch (stageIndex)
                {
                    case 0:
                        stageNarrativeText.text = "1. t = 0: Sharp gradient — Holes on P (left), Electrons on N (right).";
                        break;
                    case 1:
                        stageNarrativeText.text = "2. Diffusion: Electrons diffuse N -> P and Holes diffuse P -> N.";
                        break;
                    case 2:
                        stageNarrativeText.text = "3. Recombination: Crossing carriers annihilate, uncovering fixed + / - ions.";
                        break;
                    case 3:
                        stageNarrativeText.text = "4. Depletion & E-Field (Ebi): Field N -> P creates opposing Drift current.";
                        break;
                    default:
                        stageNarrativeText.text = "5. Equilibrium: Drift balances Diffusion (Net I = 0), stable W & V0.";
                        break;
                }
            }
        }

        private void InitPools()
        {
            if (holePos == null && holeTransforms != null)
            {
                InitCarrierArray(holeTransforms, true, 71, out holePos, out holeVel);
            }

            if (electronPosNull())
            {
                InitCarrierArray(electronTransforms, false, 193, out elecPos, out elecVel);
            }
        }

        private bool electronPosNull() => elecPos == null && electronTransforms != null;

        private void InitCarrierArray(Transform[] pool, bool isHole, int seedBase, out Vector3[] pos, out Vector3[] vel)
        {
            pos = new Vector3[pool.Length];
            vel = new Vector3[pool.Length];
            for (int i = 0; i < pool.Length; i++)
            {
                int s = seedBase + i * 29;
                // Keep 90% in home side (+X for holes, -X for electrons) and 10% as rare thermal minority carriers.
                float sideSign = isHole ? 1f : -1f;
                float u = ((i + 0.5f) / pool.Length);
                float x = sideSign * Mathf.Lerp(0.03f, chamberHalfWidth * 0.92f, u);
                float y = Mathf.Sin(s * 1.13f) * chamberHalfHeight * 0.85f;
                float z = Mathf.Cos(s * 1.71f) * chamberHalfDepth * 0.85f;
                pos[i] = new Vector3(x, y, z);
                vel[i] = new Vector3(Mathf.Cos(s * 0.8f), Mathf.Sin(s * 1.4f), Mathf.Cos(s * 2.1f)).normalized;
                if (pool[i] != null) pool[i].localPosition = pos[i];
            }
        }

        private void StepCarriers(Transform[] pool, Vector3[] pos, Vector3[] vel, bool isHole, float dt)
        {
            if (pool == null || pos == null || vel == null) return;

            // Scale visible active carrier density smoothly with log10 doping (4..24 carriers per side).
            double conc = isHole ? currentNaCm3 : currentNdCm3;
            float concT = Mathf.Clamp01((float)((System.Math.Log10(System.Math.Max(1e14, conc)) - 14.0) / 4.0));
            int activeCount = Mathf.RoundToInt(Mathf.Lerp(6f, pool.Length, concT));

            float speed = 0.14f;
            float barrierX = isHole ? leftXpMeters : -rightXnMeters;

            for (int i = 0; i < pool.Length; i++)
            {
                Transform tf = pool[i];
                if (tf == null) continue;
                bool active = i < activeCount;
                if (tf.gameObject.activeSelf != active)
                {
                    tf.gameObject.SetActive(active);
                }

                if (!active) continue;

                Vector3 p = pos[i] + vel[i] * (speed * dt);
                Vector3 v = vel[i];

                // During Diffusion stage (0.1..0.4), let top carriers stream toward x=0; at equilibrium bounce off depletion edge!
                if (isHole)
                {
                    float minX = currentProgress01 < 0.32f ? -0.04f * currentProgress01 * 4f : Mathf.Max(0.015f, barrierX + 0.012f);
                    minX = Mathf.Min(minX, chamberHalfWidth - 0.02f);
                    if (p.x < minX) { p.x = minX; v.x = Mathf.Abs(v.x); }
                    if (p.x > chamberHalfWidth) { p.x = chamberHalfWidth; v.x = -Mathf.Abs(v.x); }
                }
                else
                {
                    float maxX = currentProgress01 < 0.32f ? 0.04f * currentProgress01 * 4f : -Mathf.Max(0.015f, -barrierX + 0.012f);
                    maxX = Mathf.Max(maxX, -chamberHalfWidth + 0.02f);
                    if (p.x > maxX) { p.x = maxX; v.x = -Mathf.Abs(v.x); }
                    if (p.x < -chamberHalfWidth) { p.x = -chamberHalfWidth; v.x = Mathf.Abs(v.x); }
                }

                if (p.y > chamberHalfHeight || p.y < -chamberHalfHeight)
                {
                    v.y = -v.y;
                    p.y = Mathf.Clamp(p.y, -chamberHalfHeight, chamberHalfHeight);
                }

                if (p.z > chamberHalfDepth || p.z < -chamberHalfDepth)
                {
                    v.z = -v.z;
                    p.z = Mathf.Clamp(p.z, -chamberHalfDepth, chamberHalfDepth);
                }

                pos[i] = p;
                vel[i] = v;
                tf.localPosition = p;
            }
        }

        private void ApplyRendererColor(Renderer r, Color baseCol, Color emissionCol)
        {
            if (r == null) return;
            if (ionPropBlock == null) ionPropBlock = new MaterialPropertyBlock();
            r.GetPropertyBlock(ionPropBlock);
            ionPropBlock.SetColor(BaseColorId, baseCol);
            ionPropBlock.SetColor(EmissionColorId, emissionCol);
            r.SetPropertyBlock(ionPropBlock);
        }
    }
}
