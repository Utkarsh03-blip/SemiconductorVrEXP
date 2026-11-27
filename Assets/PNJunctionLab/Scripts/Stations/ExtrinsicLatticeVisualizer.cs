using System;
using TMPro;
using UnityEngine;

namespace PNJunctionLab
{
    /// <summary>
    /// 3D Extrinsic Silicon Crystal Lattice Visualizer (explicitly labeled "Not to scale") for Stations 2 & 3.
    /// N-Type shows pentavalent donor atoms forming 4 bonds with fixed orange + ions & mobile blue free electrons.
    /// P-Type shows trivalent acceptor atoms with an incomplete covalent bond, fixed teal - ions,
    /// mobile red holes, and animated opposite-direction valence-electron hopping.
    /// Keeps total rendered primitives well under Quest's <=300 budget.
    /// </summary>
    public class ExtrinsicLatticeVisualizer : MonoBehaviour
    {
        [Header("Mode & Scene Roots")]
        [SerializeField] private ExtrinsicDopingKind dopingMode = ExtrinsicDopingKind.NType;
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private GameObject standbyPromptRoot;
        [SerializeField] private TMP_Text scaleHeaderText;
        [SerializeField] private TMP_Text mechanismCaptionText;
        [SerializeField] private TMP_Text hoverTitleText;
        [SerializeField] private TMP_Text hoverDetailText;
        [SerializeField] private AudioSource audioSource;

        [Header("Lattice Elements & Donor/Acceptor Sites")]
        [SerializeField] private LatticeElementTarget[] hostAtoms;
        [SerializeField] private LatticeElementTarget[] donorOrAcceptorIons;
        [SerializeField] private Vector3[] donorIonSitePositions;
        [SerializeField] private LatticeElementTarget[] covalentBonds;
        [SerializeField] private LatticeElementTarget incompleteBondTarget;
        [SerializeField] private GameObject incompleteBondGapMarker;

        [Header("Mobile Carrier Pools (Blue e- & Red h+)")]
        [SerializeField] private LatticeElementTarget[] freeElectrons;
        [SerializeField] private LatticeElementTarget[] mobileHoles;

        [Header("P-Type Hole-Hopping Valence Animation")]
        [SerializeField] private Vector3[] hopSitePositions;
        [SerializeField] private Transform[] hoppingValenceElectrons;
        [SerializeField] private Transform hoppingVacancyHole;
        [SerializeField] private LatticeElementTarget hoppingHoleTarget;
        [SerializeField] private LatticeElementTarget hoppingElectronTarget;
        [SerializeField] private float hopCycleSeconds = 1.65f;

        [Header("Colors (Matches Docs/DESIGN.md)")]
        [SerializeField] private Color hostAtomColor = new Color(0.74f, 0.80f, 0.90f, 1f);
        [SerializeField] private Color donorIonOrange = new Color(0.98f, 0.56f, 0.14f, 1f);
        [SerializeField] private Color acceptorIonTeal = new Color(0.12f, 0.80f, 0.78f, 1f);
        [SerializeField] private Color bondIntactCyan = new Color(0.26f, 0.76f, 0.94f, 1f);
        [SerializeField] private Color incompleteBondWarning = new Color(0.95f, 0.34f, 0.30f, 1f);
        [SerializeField] private Color electronBlue = new Color(0.18f, 0.68f, 1.0f, 1f);
        [SerializeField] private Color holeRed = new Color(0.96f, 0.25f, 0.28f, 1f);

        public bool IsActive { get; private set; }
        public int ActiveMajorityCarrierCount { get; private set; }
        public float LastHopPhaseNormalized { get; private set; }

        private string activeDopantSymbol = "P";
        private double activeConcentrationCm3 = 1e16;
        private float driftSpeed = 0.22f;
        private Vector3[] ePositions;
        private Vector3[] eVelocities;
        private Vector3[] hPositions;
        private Vector3[] hVelocities;
        private float hopClock;
        private int currentHoleSiteIndex;
        private LatticeElementTarget activeHovered;

        private static readonly Vector3 LatticeHalfExtents = new Vector3(0.24f, 0.14f, 0.14f);

        private void Awake()
        {
            InitializePools();
            RefreshHoverBanner(null);
            SetLatticeActive(false);
        }

        private void OnEnable()
        {
            SubscribeTargetEvents(true);
        }

        private void OnDisable()
        {
            SubscribeTargetEvents(false);
        }

        private void Update()
        {
            if (!IsActive)
            {
                return;
            }

            float dt = Time.deltaTime;
            StepCarrierSwarm(freeElectrons, ePositions, eVelocities, dt);
            StepCarrierSwarm(mobileHoles, hPositions, hVelocities, dt);

            if (dopingMode == ExtrinsicDopingKind.PType)
            {
                UpdateHoleHoppingAnimation(dt);
            }
        }

        /// <summary>
        /// Shows or hides the 3D crystal hologram vs standby furnace placeholder.
        /// </summary>
        public void SetLatticeActive(bool active)
        {
            IsActive = active;
            if (visualRoot != null)
            {
                visualRoot.SetActive(active);
            }

            if (standbyPromptRoot != null)
            {
                standbyPromptRoot.SetActive(!active);
            }

            if (!active)
            {
                RefreshHoverBanner(null);
            }
        }

        /// <summary>
        /// Applies dopant element, concentration decade (10^14..10^18 cm^-3), and live SemiconductorModel counts.
        /// </summary>
        public void SetExtrinsicState(
            ExtrinsicDopingKind mode,
            string dopantSymbol,
            double concentrationCm3,
            CarrierConcentrations concentrations)
        {
            InitializePools();
            dopingMode = mode;
            activeDopantSymbol = string.IsNullOrEmpty(dopantSymbol) ? (mode == ExtrinsicDopingKind.NType ? "P" : "B") : dopantSymbol;
            activeConcentrationCm3 = Math.Max(1e14, concentrationCm3);

            double log10Conc = Math.Log10(activeConcentrationCm3);
            float tConc = Mathf.Clamp01((float)((log10Conc - 14.0) / 4.0));
            driftSpeed = Mathf.Lerp(0.16f, 0.42f, tConc);
            hopCycleSeconds = Mathf.Lerp(2.10f, 1.05f, tConc);

            int desiredMajority = Mathf.RoundToInt(Mathf.Lerp(4f, 16f, tConc));
            ActiveMajorityCarrierCount = desiredMajority;
            int activeIons = Mathf.Clamp(1 + Mathf.FloorToInt(tConc * 2.4f), 1, donorOrAcceptorIons != null ? donorOrAcceptorIons.Length : 1);

            if (donorOrAcceptorIons != null)
            {
                bool isN = mode == ExtrinsicDopingKind.NType;
                Color ionColor = isN ? donorIonOrange : acceptorIonTeal;
                string title = isN
                    ? $"Donor Ion ({activeDopantSymbol}+): Fixed +"
                    : $"Acceptor Ion ({activeDopantSymbol}-): Fixed -";
                string detail = isN
                    ? "Group V atom bonded to 4 Si neighbors; fixed + ion after releasing 5th e-."
                    : "Group III atom missing 1 covalent e-; fixed - ion after capturing a neighbor e-.";

                for (int i = 0; i < donorOrAcceptorIons.Length; i++)
                {
                    if (donorOrAcceptorIons[i] == null) continue;
                    bool showIon = i < activeIons;
                    donorOrAcceptorIons[i].gameObject.SetActive(showIon);
                    donorOrAcceptorIons[i].SetRestingColor(ionColor, ionColor * 0.48f);
                    donorOrAcceptorIons[i].SetCustomTooltip(title, detail);
                }
            }

            if (hostAtoms != null)
            {
                for (int i = 0; i < hostAtoms.Length; i++)
                {
                    if (hostAtoms[i] == null) continue;
                    hostAtoms[i].SetRestingColor(hostAtomColor, Color.black);
                    hostAtoms[i].SetCustomTooltip("Silicon Atom (Si, Group IV)", "4 valence e- form tetrahedral covalent bonds.");
                }
            }

            if (covalentBonds != null)
            {
                for (int i = 0; i < covalentBonds.Length; i++)
                {
                    if (covalentBonds[i] == null) continue;
                    covalentBonds[i].SetRestingColor(bondIntactCyan, bondIntactCyan * 0.25f);
                    covalentBonds[i].SetCustomTooltip("Covalent Bond (Shared e- pair)", "2 valence electrons lock adjacent atoms.");
                }
            }

            if (incompleteBondTarget != null)
            {
                bool showGap = mode == ExtrinsicDopingKind.PType;
                incompleteBondTarget.gameObject.SetActive(showGap);
                incompleteBondTarget.SetRestingColor(incompleteBondWarning, incompleteBondWarning * 0.4f);
                incompleteBondTarget.SetCustomTooltip("Incomplete Bond (Vacancy)", "Only 3 valence e- from acceptor -> 1 hole (h+).");
            }

            if (incompleteBondGapMarker != null)
            {
                incompleteBondGapMarker.SetActive(mode == ExtrinsicDopingKind.PType);
            }

            if (mode == ExtrinsicDopingKind.NType)
            {
                ApplySwarmVisibility(freeElectrons, desiredMajority, electronBlue, "Free Electron (e-)", "Donated 5th valence e- moving freely in Conduction Band.");
                // Minority holes are tiny (< ni) in N-type at 300 K; show 0-1 at very light doping for intuition.
                int minHoles = log10Conc <= 14.3 ? 1 : 0;
                ApplySwarmVisibility(mobileHoles, minHoles, holeRed, "Minority Hole (h+)", "Rare thermally generated hole (p = ni^2 / ND).");
            }
            else
            {
                ApplySwarmVisibility(mobileHoles, desiredMajority, holeRed, "Mobile Hole (h+)", "Positive charge carrier: valence e- hop the opposite way!");
                int minElectrons = log10Conc <= 14.3 ? 1 : 0;
                ApplySwarmVisibility(freeElectrons, minElectrons, electronBlue, "Minority Electron (e-)", "Rare thermally generated electron (n = ni^2 / NA).");
            }

            if (hoppingHoleTarget != null)
            {
                hoppingHoleTarget.SetRestingColor(holeRed, holeRed * 0.55f);
                hoppingHoleTarget.SetCustomTooltip("Hole Hopping Site (h+)", "Vacancy moves right as neighboring valence e- hop left.");
            }

            if (hoppingElectronTarget != null)
            {
                hoppingElectronTarget.SetRestingColor(electronBlue, electronBlue * 0.55f);
                hoppingElectronTarget.SetCustomTooltip("Hopping Valence e-", "Valence electron jumps opposite to hole direction.");
            }

            if (scaleHeaderText != null)
            {
                string label = mode == ExtrinsicDopingKind.NType
                    ? $"N-Type Si ({activeDopantSymbol}, Group V) — Not to scale"
                    : $"P-Type Si ({activeDopantSymbol}, Group III) — Not to scale";
                scaleHeaderText.text = label;
            }

            if (mechanismCaptionText != null)
            {
                mechanismCaptionText.text = mode == ExtrinsicDopingKind.NType
                    ? $"Orange {activeDopantSymbol}+ fixed donor | Blue free e- (n ~ {concentrations.ElectronsCm3:0.0E+0})"
                    : $"Teal {activeDopantSymbol}- fixed acceptor | Valence e- hop <- | Hole h+ shifts ->";
            }
        }

        private void ApplySwarmVisibility(LatticeElementTarget[] pool, int activeCount, Color col, string tipTitle, string tipDetail)
        {
            if (pool == null) return;
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] == null) continue;
                bool show = i < activeCount;
                pool[i].gameObject.SetActive(show);
                pool[i].SetRestingColor(col, col * 0.50f);
                pool[i].SetCustomTooltip(tipTitle, tipDetail);
            }
        }

        private void InitializePools()
        {
            if (ePositions == null && freeElectrons != null)
            {
                InitSwarmArrays(freeElectrons.Length, 117, out ePositions, out eVelocities, freeElectrons);
            }

            if (hPositions == null && mobileHoles != null)
            {
                InitSwarmArrays(mobileHoles.Length, 313, out hPositions, out hVelocities, mobileHoles);
            }
        }

        private static void InitSwarmArrays(int count, int seedOffset, out Vector3[] positions, out Vector3[] velocities, LatticeElementTarget[] targets)
        {
            positions = new Vector3[count];
            velocities = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                int s = seedOffset + i * 37;
                Vector3 p = new Vector3(
                    Mathf.Sin(s * 1.13f) * LatticeHalfExtents.x * 0.88f,
                    Mathf.Cos(s * 1.71f) * LatticeHalfExtents.y * 0.85f,
                    Mathf.Sin(s * 2.29f) * LatticeHalfExtents.z * 0.85f);
                Vector3 dir = new Vector3(
                    Mathf.Cos(s * 0.87f),
                    Mathf.Sin(s * 1.43f),
                    Mathf.Cos(s * 1.91f)).normalized;
                positions[i] = p;
                velocities[i] = dir;
                if (targets != null && i < targets.Length && targets[i] != null)
                {
                    targets[i].transform.localPosition = p;
                }
            }
        }

        private void StepCarrierSwarm(LatticeElementTarget[] pool, Vector3[] positions, Vector3[] velocities, float dt)
        {
            if (pool == null || positions == null || velocities == null)
            {
                return;
            }

            int n = Mathf.Min(pool.Length, positions.Length);
            for (int i = 0; i < n; i++)
            {
                LatticeElementTarget target = pool[i];
                if (target == null || !target.gameObject.activeSelf)
                {
                    continue;
                }

                Vector3 p = positions[i] + velocities[i] * (driftSpeed * dt);
                Vector3 v = velocities[i];

                if (p.x > LatticeHalfExtents.x || p.x < -LatticeHalfExtents.x)
                {
                    v.x = -v.x;
                    p.x = Mathf.Clamp(p.x, -LatticeHalfExtents.x, LatticeHalfExtents.x);
                }

                if (p.y > LatticeHalfExtents.y || p.y < -LatticeHalfExtents.y)
                {
                    v.y = -v.y;
                    p.y = Mathf.Clamp(p.y, -LatticeHalfExtents.y, LatticeHalfExtents.y);
                }

                if (p.z > LatticeHalfExtents.z || p.z < -LatticeHalfExtents.z)
                {
                    v.z = -v.z;
                    p.z = Mathf.Clamp(p.z, -LatticeHalfExtents.z, LatticeHalfExtents.z);
                }

                positions[i] = p;
                velocities[i] = v;
                target.transform.localPosition = p;
            }
        }

        private void UpdateHoleHoppingAnimation(float dt)
        {
            if (hopSitePositions == null || hopSitePositions.Length < 2
                || hoppingValenceElectrons == null || hoppingValenceElectrons.Length < hopSitePositions.Length)
            {
                return;
            }

            int siteCount = hopSitePositions.Length;
            hopClock += dt / Mathf.Max(0.4f, hopCycleSeconds);
            if (hopClock >= 1f)
            {
                hopClock -= 1f;
                currentHoleSiteIndex = (currentHoleSiteIndex + 1) % siteCount;
            }

            LastHopPhaseNormalized = hopClock;
            int nextHoleSite = (currentHoleSiteIndex + 1) % siteCount;

            // Pause briefly (0..0.3), then neighbor electron at nextHoleSite hops in the OPPOSITE direction to currentHoleSite.
            float moveT = Mathf.Clamp01((hopClock - 0.25f) / 0.65f);
            float smoothT = moveT * moveT * (3f - 2f * moveT);

            if (hoppingVacancyHole != null)
            {
                Vector3 holePos = smoothT > 0.55f ? hopSitePositions[nextHoleSite] : hopSitePositions[currentHoleSiteIndex];
                hoppingVacancyHole.localPosition = holePos;
            }

            for (int i = 0; i < siteCount; i++)
            {
                Transform eTf = hoppingValenceElectrons[i];
                if (eTf == null) continue;

                if (i == currentHoleSiteIndex)
                {
                    // This electron corresponds to the slot being filled by nextHoleSite's hopping electron.
                    eTf.gameObject.SetActive(false);
                }
                else if (i == nextHoleSite)
                {
                    // Electron arcs in the opposite direction (nextHoleSite -> currentHoleSiteIndex).
                    eTf.gameObject.SetActive(true);
                    Vector3 arcPos = Vector3.Lerp(hopSitePositions[nextHoleSite], hopSitePositions[currentHoleSiteIndex], smoothT);
                    arcPos.y += Mathf.Sin(smoothT * Mathf.PI) * 0.036f;
                    eTf.localPosition = arcPos;
                }
                else
                {
                    eTf.gameObject.SetActive(true);
                    eTf.localPosition = hopSitePositions[i];
                }
            }
        }

        private void SubscribeTargetEvents(bool subscribe)
        {
            SubscribeArray(hostAtoms, subscribe);
            SubscribeArray(donorOrAcceptorIons, subscribe);
            SubscribeArray(covalentBonds, subscribe);
            SubscribeArray(freeElectrons, subscribe);
            SubscribeArray(mobileHoles, subscribe);
            if (incompleteBondTarget != null) SubscribeSingle(incompleteBondTarget, subscribe);
            if (hoppingHoleTarget != null) SubscribeSingle(hoppingHoleTarget, subscribe);
            if (hoppingElectronTarget != null) SubscribeSingle(hoppingElectronTarget, subscribe);
        }

        private void SubscribeArray(LatticeElementTarget[] arr, bool subscribe)
        {
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] != null) SubscribeSingle(arr[i], subscribe);
            }
        }

        private void SubscribeSingle(LatticeElementTarget target, bool subscribe)
        {
            target.Hovered -= HandleTargetHovered;
            target.Unhovered -= HandleTargetUnhovered;
            if (subscribe)
            {
                target.Hovered += HandleTargetHovered;
                target.Unhovered += HandleTargetUnhovered;
            }
        }

        private void HandleTargetHovered(LatticeElementTarget target)
        {
            activeHovered = target;
            RefreshHoverBanner(target);
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Hover, 0.25f);
        }

        private void HandleTargetUnhovered(LatticeElementTarget target)
        {
            if (activeHovered == target)
            {
                activeHovered = null;
                RefreshHoverBanner(null);
            }
        }

        private void RefreshHoverBanner(LatticeElementTarget target)
        {
            if (target == null)
            {
                if (hoverTitleText != null)
                {
                    hoverTitleText.text = "Ray-Point Any Atom, Bond, Ion, or Carrier";
                }

                if (hoverDetailText != null)
                {
                    hoverDetailText.text = "Hover any 3D particle in the crystal to inspect its charge and role.";
                }

                return;
            }

            if (hoverTitleText != null)
            {
                hoverTitleText.text = string.IsNullOrEmpty(target.CustomTooltipTitle)
                    ? target.ElementKind.ToString()
                    : target.CustomTooltipTitle;
            }

            if (hoverDetailText != null)
            {
                hoverDetailText.text = target.CustomTooltipDetail ?? string.Empty;
            }
        }
    }
}
