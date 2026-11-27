using System.Text;
using TMPro;
using UnityEngine;

namespace PNJunctionLab
{
    /// <summary>
    /// Interactive 3D Diamond-Cubic crystal lattice hologram ("Not to scale") supporting XRI ray-hover
    /// inspection on atoms/bonds/carriers and pooled thermal bond-breaking, generation, and recombination.
    /// </summary>
    public class LatticeHologramController : MonoBehaviour
    {
        [Header("Core Hierarchy")]
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private TMP_Text scaleNoticeText;
        [SerializeField] private TMP_Text hoverTooltipText;
        [SerializeField] private GameObject hoverTooltipRoot;

        [Header("Lattice Elements (Fixed Crystal & Pooled Carriers)")]
        [SerializeField] private LatticeElementTarget[] atomTargets;
        [SerializeField] private LatticeElementTarget[] bondTargets;
        [SerializeField] private Vector3[] bondLocalPositions;
        [SerializeField] private LatticeElementTarget[] pooledElectrons;
        [SerializeField] private LatticeElementTarget[] pooledHoles;
        [SerializeField] private Transform[] intactBondVisuals;
        [SerializeField] private GameObject[] brokenBondMarkers;

        [Header("Visual Colors & Bounds")]
        [SerializeField] private Color intactBondColor = new Color(0.20f, 0.88f, 1.0f, 0.90f);
        [SerializeField] private Color intactBondEmission = new Color(0.06f, 0.34f, 0.48f, 1f);
        [SerializeField] private Color brokenBondColor = new Color(0.95f, 0.45f, 0.18f, 0.40f);
        [SerializeField] private Color brokenBondEmission = new Color(0.28f, 0.08f, 0.02f, 1f);
        [SerializeField] private Color electronColor = new Color(0.18f, 0.58f, 1.0f, 1f);
        [SerializeField] private Color electronEmission = new Color(0.08f, 0.28f, 0.65f, 1f);
        [SerializeField] private Color holeColor = new Color(1.0f, 0.28f, 0.32f, 1f);
        [SerializeField] private Color holeEmission = new Color(0.55f, 0.08f, 0.12f, 1f);
        [SerializeField] private Vector3 carrierBoundsMin = new Vector3(-0.20f, -0.18f, -0.20f);
        [SerializeField] private Vector3 carrierBoundsMax = new Vector3(0.20f, 0.22f, 0.20f);
        [SerializeField] private float intactBondThickness = 0.0055f;
        [SerializeField] private float brokenBondThickness = 0.0022f;
        [SerializeField] private float maxAtomVibration = 0.0055f;
        [SerializeField] private Vector3 hoverTooltipLocalOffset = new Vector3(0.32f, 0.055f, -0.535f);
        [SerializeField] private bool billboardTooltip = false;
        [SerializeField] private AudioSource audioSource;

        private struct CarrierPair
        {
            public bool Active;
            public int BondIndex;
            public int PoolIndex;
            public Vector3 ElectronPos;
            public Vector3 HolePos;
            public Vector3 ElectronVel;
            public Vector3 HoleVel;
            public float Age;
            public float Lifetime;
        }

        public bool IsRevealed { get; private set; }
        public int ActiveCarrierPairs { get; private set; }
        public int TotalBondsCount => bondTargets != null ? bondTargets.Length : 0;

        /// <summary>
        /// Raised whenever the active hover element description changes (used by Microscope HUD readout).
        /// </summary>
        public event System.Action<string> HoverInfoChanged;

        private SemiconductorMaterial activeMaterial = SemiconductorMaterial.Silicon;
        private double activeTemperatureK;
        private int targetPairCount;
        private bool[] bondBroken;
        private Vector3[] atomRestLocalPos;
        private CarrierPair[] pairs;
        private LatticeElementTarget hoveredTarget;
        private Camera cachedCam;
        private float cycleTimer;

        private void Awake()
        {
            EnsureInitialized();
            ClearAllCarriers();
            HideTooltip();
            SetRevealed(false);
        }

        private void EnsureInitialized()
        {
            if (cachedCam == null)
            {
                cachedCam = Camera.main;
            }

            if (atomRestLocalPos == null && atomTargets != null)
            {
                atomRestLocalPos = new Vector3[atomTargets.Length];
                for (int i = 0; i < atomTargets.Length; i++)
                {
                    atomRestLocalPos[i] = atomTargets[i] != null ? atomTargets[i].transform.localPosition : Vector3.zero;
                }
            }

            if (bondBroken == null)
            {
                int bondCount = bondTargets != null ? bondTargets.Length : 0;
                bondBroken = new bool[bondCount];
            }

            if (pairs == null)
            {
                int poolCap = Mathf.Min(
                    pooledElectrons != null ? pooledElectrons.Length : 0,
                    pooledHoles != null ? pooledHoles.Length : 0);
                pairs = new CarrierPair[poolCap];
            }

            if (scaleNoticeText != null)
            {
                scaleNoticeText.text = "Schematic 3D Diamond Lattice (Not to scale)";
            }
        }

        private void OnEnable()
        {
            SubscribeTargets(true);
        }

        private void OnDisable()
        {
            SubscribeTargets(false);
            HideTooltip();
        }

        private void Update()
        {
            if (!IsRevealed)
            {
                return;
            }

            UpdateAtomVibration();
            UpdateCarrierDynamics(Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (!billboardTooltip || hoverTooltipRoot == null || !hoverTooltipRoot.activeSelf)
            {
                return;
            }

            if (cachedCam == null)
            {
                cachedCam = Camera.main;
            }

            if (cachedCam != null)
            {
                Vector3 dir = hoverTooltipRoot.transform.position - cachedCam.transform.position;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    hoverTooltipRoot.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                }
            }
        }

        private void UpdateAtomVibration()
        {
            if (atomTargets == null || atomRestLocalPos == null)
            {
                return;
            }

            float tempNorm = Mathf.Clamp01((float)(activeTemperatureK / 500.0));
            float vibAmp = tempNorm <= 0.001f ? 0f : maxAtomVibration * Mathf.Sqrt(tempNorm);
            float tNow = Time.time * Mathf.Lerp(6f, 24f, tempNorm);

            int count = Mathf.Min(atomTargets.Length, atomRestLocalPos.Length);
            for (int i = 0; i < count; i++)
            {
                LatticeElementTarget atom = atomTargets[i];
                if (atom == null) continue;

                if (vibAmp <= 0.00001f)
                {
                    atom.transform.localPosition = atomRestLocalPos[i];
                }
                else
                {
                    Vector3 jitter = new Vector3(
                        Mathf.Sin(tNow + i * 1.37f),
                        Mathf.Cos(tNow * 1.15f + i * 2.11f),
                        Mathf.Sin(tNow * 0.93f + i * 0.79f)) * vibAmp;
                    atom.transform.localPosition = atomRestLocalPos[i] + jitter;
                }
            }
        }

        public void ConfigureReferences(
            GameObject root,
            TMP_Text scaleLabel,
            GameObject tipRoot,
            TMP_Text tipText,
            LatticeElementTarget[] atoms,
            LatticeElementTarget[] bonds,
            Vector3[] bondPos,
            Transform[] intactVisuals,
            GameObject[] brokenMarkers,
            LatticeElementTarget[] electrons,
            LatticeElementTarget[] holes,
            AudioSource source)
        {
            visualRoot = root;
            scaleNoticeText = scaleLabel;
            hoverTooltipRoot = tipRoot;
            hoverTooltipText = tipText;
            atomTargets = atoms;
            bondTargets = bonds;
            bondLocalPositions = bondPos;
            intactBondVisuals = intactVisuals;
            brokenBondMarkers = brokenMarkers;
            pooledElectrons = electrons;
            pooledHoles = holes;
            audioSource = source;
        }

        /// <summary>
        /// Shows or hides the holographic lattice visual (when scanned with the Atomic Lens or reset).
        /// </summary>
        public void SetRevealed(bool revealed)
        {
            EnsureInitialized();
            IsRevealed = revealed;
            if (visualRoot != null)
            {
                visualRoot.SetActive(revealed);
            }

            if (!revealed)
            {
                ClearAllCarriers();
                HideTooltip();
            }
            else
            {
                SyncPairCountImmediate();
            }
        }

        /// <summary>
        /// Updates lattice thermal state using SemiconductorModel intrinsic concentrations.
        /// </summary>
        public void SetLatticeState(SemiconductorMaterial material, double temperatureK, CarrierConcentrations concentrations)
        {
            EnsureInitialized();
            activeMaterial = material;
            activeTemperatureK = temperatureK;

            int maxPossible = Mathf.Min(pairs != null ? pairs.Length : 0, TotalBondsCount);
            if (maxPossible <= 0 || temperatureK <= 0.01 || concentrations.IntrinsicNiCm3 <= 0.0)
            {
                targetPairCount = 0;
            }
            else
            {
                // Monotonic log-scale visualization across ni [1..1e16 cm^-3]:
                // 0 K = 0 pairs, Si@300K (~1.5e10) -> ~6-7 pairs, Ge@300K (~2.4e13) -> ~11-12 pairs, 500K -> ~14-16 pairs.
                double logNi = System.Math.Log10(System.Math.Max(1.0, concentrations.IntrinsicNiCm3));
                float normalized = Mathf.Clamp01((float)(logNi / 16.0));
                targetPairCount = Mathf.Clamp(Mathf.RoundToInt(normalized * maxPossible), 1, maxPossible);
            }

            if (IsRevealed)
            {
                SyncPairCountImmediate();
            }

            if (hoveredTarget != null && hoveredTarget.IsHovered)
            {
                UpdateHoverTooltipContent(hoveredTarget);
            }
        }

        private void SyncPairCountImmediate()
        {
            if (pairs == null)
            {
                return;
            }

            int current = CountActivePairs();
            while (current > targetPairCount)
            {
                if (!RemoveOldestPair())
                {
                    break;
                }
                current--;
            }

            while (current < targetPairCount)
            {
                if (!SpawnPairAtRandomIntactBond())
                {
                    break;
                }
                current++;
            }

            ActiveCarrierPairs = current;
        }

        private void UpdateCarrierDynamics(float dt)
        {
            if (pairs == null || pairs.Length == 0)
            {
                return;
            }

            float speedMul = Mathf.Lerp(0.45f, 1.55f, Mathf.Clamp01((float)(activeTemperatureK / 500.0)));

            for (int i = 0; i < pairs.Length; i++)
            {
                if (!pairs[i].Active)
                {
                    continue;
                }

                pairs[i].Age += dt;
                pairs[i].ElectronPos += pairs[i].ElectronVel * (speedMul * dt);
                pairs[i].HolePos += pairs[i].HoleVel * (speedMul * dt);

                BounceInBounds(ref pairs[i].ElectronPos, ref pairs[i].ElectronVel);
                BounceInBounds(ref pairs[i].HolePos, ref pairs[i].HoleVel);

                int pIdx = pairs[i].PoolIndex;
                if (pooledElectrons != null && pIdx < pooledElectrons.Length && pooledElectrons[pIdx] != null)
                {
                    pooledElectrons[pIdx].transform.localPosition = pairs[i].ElectronPos;
                }

                if (pooledHoles != null && pIdx < pooledHoles.Length && pooledHoles[pIdx] != null)
                {
                    pooledHoles[pIdx].transform.localPosition = pairs[i].HolePos;
                }
            }

            if (targetPairCount > 0)
            {
                float interval = Mathf.Lerp(1.6f, 0.50f, Mathf.Clamp01((float)(activeTemperatureK / 500.0)));
                cycleTimer += dt;
                if (cycleTimer >= interval)
                {
                    cycleTimer = 0f;
                    // Recombine one aged pair and thermally generate a new e-/h+ pair so dynamic equilibrium is visible.
                    if (RemoveOldestPair())
                    {
                        SpawnPairAtRandomIntactBond();
                    }
                }
            }

            ActiveCarrierPairs = CountActivePairs();
        }

        private void BounceInBounds(ref Vector3 pos, ref Vector3 vel)
        {
            if (pos.x < carrierBoundsMin.x) { pos.x = carrierBoundsMin.x; vel.x = Mathf.Abs(vel.x); }
            else if (pos.x > carrierBoundsMax.x) { pos.x = carrierBoundsMax.x; vel.x = -Mathf.Abs(vel.x); }

            if (pos.y < carrierBoundsMin.y) { pos.y = carrierBoundsMin.y; vel.y = Mathf.Abs(vel.y); }
            else if (pos.y > carrierBoundsMax.y) { pos.y = carrierBoundsMax.y; vel.y = -Mathf.Abs(vel.y); }

            if (pos.z < carrierBoundsMin.z) { pos.z = carrierBoundsMin.z; vel.z = Mathf.Abs(vel.z); }
            else if (pos.z > carrierBoundsMax.z) { pos.z = carrierBoundsMax.z; vel.z = -Mathf.Abs(vel.z); }
        }

        private bool SpawnPairAtRandomIntactBond()
        {
            int bondCount = TotalBondsCount;
            if (bondCount == 0 || pairs == null)
            {
                return false;
            }

            int freeSlot = -1;
            for (int i = 0; i < pairs.Length; i++)
            {
                if (!pairs[i].Active)
                {
                    freeSlot = i;
                    break;
                }
            }

            if (freeSlot < 0)
            {
                return false;
            }

            int start = Random.Range(0, bondCount);
            int chosenBond = -1;
            for (int k = 0; k < bondCount; k++)
            {
                int idx = (start + k) % bondCount;
                if (!bondBroken[idx])
                {
                    chosenBond = idx;
                    break;
                }
            }

            if (chosenBond < 0)
            {
                return false;
            }

            SetBondBrokenVisual(chosenBond, true);

            Vector3 spawnPos = (bondLocalPositions != null && chosenBond < bondLocalPositions.Length)
                ? bondLocalPositions[chosenBond]
                : Vector3.zero;

            Vector3 dir = Random.onUnitSphere;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.right;
            dir.Normalize();

            pairs[freeSlot] = new CarrierPair
            {
                Active = true,
                BondIndex = chosenBond,
                PoolIndex = freeSlot,
                ElectronPos = spawnPos + (dir * 0.025f),
                HolePos = spawnPos - (dir * 0.025f),
                ElectronVel = dir * 0.14f,
                HoleVel = -dir * 0.08f,
                Age = 0f,
                Lifetime = Random.Range(2.2f, 4.5f)
            };

            ActivateCarrierSlot(freeSlot, true, pairs[freeSlot].ElectronPos, pairs[freeSlot].HolePos);
            return true;
        }

        private bool RemoveOldestPair()
        {
            if (pairs == null)
            {
                return false;
            }

            int oldestIdx = -1;
            float oldestAge = -1f;
            for (int i = 0; i < pairs.Length; i++)
            {
                if (pairs[i].Active && pairs[i].Age > oldestAge)
                {
                    oldestAge = pairs[i].Age;
                    oldestIdx = i;
                }
            }

            if (oldestIdx < 0)
            {
                return false;
            }

            int bIdx = pairs[oldestIdx].BondIndex;
            if (bIdx >= 0 && bIdx < bondBroken.Length)
            {
                SetBondBrokenVisual(bIdx, false);
            }

            ActivateCarrierSlot(pairs[oldestIdx].PoolIndex, false, Vector3.zero, Vector3.zero);
            pairs[oldestIdx].Active = false;
            return true;
        }

        private void ClearAllCarriers()
        {
            if (pairs != null)
            {
                for (int i = 0; i < pairs.Length; i++)
                {
                    pairs[i].Active = false;
                    ActivateCarrierSlot(i, false, Vector3.zero, Vector3.zero);
                }
            }

            if (bondBroken != null)
            {
                for (int i = 0; i < bondBroken.Length; i++)
                {
                    SetBondBrokenVisual(i, false);
                }
            }

            ActiveCarrierPairs = 0;
        }

        private int CountActivePairs()
        {
            if (pairs == null) return 0;
            int count = 0;
            for (int i = 0; i < pairs.Length; i++)
            {
                if (pairs[i].Active) count++;
            }
            return count;
        }

        private void ActivateCarrierSlot(int poolIdx, bool active, Vector3 ePos, Vector3 hPos)
        {
            if (pooledElectrons != null && poolIdx >= 0 && poolIdx < pooledElectrons.Length && pooledElectrons[poolIdx] != null)
            {
                GameObject eGo = pooledElectrons[poolIdx].gameObject;
                eGo.transform.localPosition = ePos;
                eGo.SetActive(active);
                if (active)
                {
                    pooledElectrons[poolIdx].SetRestingColor(electronColor, electronEmission);
                }
            }

            if (pooledHoles != null && poolIdx >= 0 && poolIdx < pooledHoles.Length && pooledHoles[poolIdx] != null)
            {
                GameObject hGo = pooledHoles[poolIdx].gameObject;
                hGo.transform.localPosition = hPos;
                hGo.SetActive(active);
                if (active)
                {
                    pooledHoles[poolIdx].SetRestingColor(holeColor, holeEmission);
                }
            }
        }

        private void SetBondBrokenVisual(int bondIdx, bool broken)
        {
            if (bondBroken != null && bondIdx >= 0 && bondIdx < bondBroken.Length)
            {
                bondBroken[bondIdx] = broken;
            }

            if (bondTargets != null && bondIdx >= 0 && bondIdx < bondTargets.Length && bondTargets[bondIdx] != null)
            {
                if (broken)
                {
                    bondTargets[bondIdx].SetRestingColor(brokenBondColor, brokenBondEmission);
                }
                else
                {
                    bondTargets[bondIdx].SetRestingColor(intactBondColor, intactBondEmission);
                }
            }

            if (intactBondVisuals != null && bondIdx >= 0 && bondIdx < intactBondVisuals.Length && intactBondVisuals[bondIdx] != null)
            {
                Vector3 s = intactBondVisuals[bondIdx].localScale;
                s.x = broken ? brokenBondThickness : intactBondThickness;
                s.z = broken ? brokenBondThickness : intactBondThickness;
                intactBondVisuals[bondIdx].localScale = s;
            }

            if (brokenBondMarkers != null && bondIdx >= 0 && bondIdx < brokenBondMarkers.Length && brokenBondMarkers[bondIdx] != null)
            {
                brokenBondMarkers[bondIdx].SetActive(broken);
            }
        }

        private void SubscribeTargets(bool subscribe)
        {
            SubscribeArray(atomTargets, subscribe);
            SubscribeArray(bondTargets, subscribe);
            SubscribeArray(pooledElectrons, subscribe);
            SubscribeArray(pooledHoles, subscribe);
        }

        private void SubscribeArray(LatticeElementTarget[] arr, bool subscribe)
        {
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++)
            {
                LatticeElementTarget t = arr[i];
                if (t == null) continue;
                if (subscribe)
                {
                    t.Hovered += HandleElementHovered;
                    t.Unhovered += HandleElementUnhovered;
                }
                else
                {
                    t.Hovered -= HandleElementHovered;
                    t.Unhovered -= HandleElementUnhovered;
                }
            }
        }

        private void HandleElementHovered(LatticeElementTarget target)
        {
            hoveredTarget = target;
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Hover, 0.25f);
            UpdateHoverTooltipContent(target);
        }

        private void HandleElementUnhovered(LatticeElementTarget target)
        {
            if (hoveredTarget == target)
            {
                hoveredTarget = null;
                HideTooltip();
            }
        }

        /// <summary>
        /// Updates hover focus externally (for raycasting through the circular Microscope eyepiece RenderTexture).
        /// </summary>
        public void SetExternalHover(LatticeElementTarget target)
        {
            if (hoveredTarget == target)
            {
                return;
            }

            hoveredTarget = target;
            if (target != null)
            {
                ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Hover, 0.2f);
                UpdateHoverTooltipContent(target);
            }
            else
            {
                HideTooltip();
            }
        }

        private void UpdateHoverTooltipContent(LatticeElementTarget target)
        {
            if (target == null)
            {
                return;
            }

            MaterialProperties props = SemiconductorModel.GetMaterialProperties(activeMaterial);
            var sb = new StringBuilder();

            switch (target.ElementKind)
            {
                case LatticeElementKind.HostAtom:
                    sb.Append("<b>").Append(props.Symbol).Append(" Atom (Group IV)</b>\n");
                    sb.Append("4 valence e-, 4 covalent bonds | Eg = ").Append(props.BandGapEv.ToString("F2")).Append(" eV");
                    break;

                case LatticeElementKind.CovalentBond:
                    bool isBroken = bondBroken != null && target.ElementIndex >= 0 && target.ElementIndex < bondBroken.Length && bondBroken[target.ElementIndex];
                    if (isBroken)
                    {
                        sb.Append("<b>Broken Covalent Bond</b>\nThermal agitation freed e- / h+ pair");
                    }
                    else
                    {
                        sb.Append("<b>Covalent Bond (Shared e- Pair)</b>\n");
                        sb.Append(activeTemperatureK <= 0.01 ? "0 K: Fully intact insulator" : "Intact tetrahedral valence bond");
                    }
                    break;

                case LatticeElementKind.FreeElectron:
                    sb.Append("<b>Free Electron (e-, Charge -q)</b>\nMobile negative carrier in Conduction Band");
                    break;

                case LatticeElementKind.Hole:
                    sb.Append("<b>Electron Vacancy / Hole (h+, Charge +q)</b>\nMobile positive carrier in Valence Band");
                    break;
            }

            string formatted = sb.ToString();
            HoverInfoChanged?.Invoke(formatted);

            if (hoverTooltipRoot != null && hoverTooltipText != null)
            {
                hoverTooltipText.text = formatted;
                hoverTooltipRoot.transform.localPosition = hoverTooltipLocalOffset;
                hoverTooltipRoot.SetActive(true);
            }
        }

        private void HideTooltip()
        {
            HoverInfoChanged?.Invoke(string.Empty);
            if (hoverTooltipRoot != null)
            {
                hoverTooltipRoot.SetActive(false);
            }
        }
    }
}
