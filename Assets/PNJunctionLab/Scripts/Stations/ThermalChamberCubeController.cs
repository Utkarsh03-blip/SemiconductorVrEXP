using TMPro;
using UnityEngine;

namespace PNJunctionLab
{
    /// <summary>
    /// Translucent 3D Thermal Agitation & Carrier Chamber Cube mounted on Station 1's workbench.
    /// Visually demonstrates lattice phonon vibration (~sqrt(T)) and free electron/hole density + speed
    /// directly driven by SemiconductorModel as temperature moves between 0 K and 500 K.
    /// </summary>
    public class ThermalChamberCubeController : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("Chamber Geometry")]
        [SerializeField] private Transform[] latticeAtoms;
        [SerializeField] private Vector3[] atomRestLocalPos;
        [SerializeField] private Transform[] pooledElectrons;
        [SerializeField] private Transform[] pooledHoles;
        [SerializeField] private Renderer[] thermalGlowRenderers;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Vector3 interiorHalfExtents = new Vector3(0.115f, 0.110f, 0.115f);

        [Header("Thermal Visual Tuning")]
        [SerializeField] private float maxVibrationAmplitude = 0.011f;
        [SerializeField] private float minSpeedMps = 0.07f;
        [SerializeField] private float maxSpeedMps = 0.42f;
        [SerializeField] private Color coldGlowColor = new Color(0.20f, 0.56f, 0.90f, 1f);
        [SerializeField] private Color midGlowColor = new Color(0.22f, 0.90f, 0.96f, 1f);
        [SerializeField] private Color hotGlowColor = new Color(1.0f, 0.52f, 0.16f, 1f);
        [SerializeField] private Color lockedGlowColor = new Color(0.25f, 0.28f, 0.34f, 1f);

        private Vector3[] ePos;
        private Vector3[] eVel;
        private Vector3[] hPos;
        private Vector3[] hVel;
        private float[] atomPhaseX;
        private float[] atomPhaseY;
        private float[] atomPhaseZ;
        private MaterialPropertyBlock glowBlock;

        private SemiconductorMaterial activeMaterial = SemiconductorMaterial.Silicon;
        private double activeTempK;
        private int activeCarrierCount;
        private bool isUnlocked = true;
        private bool initialized;

        public int ActiveCarrierPairs => activeCarrierCount;

        private void Awake()
        {
            EnsureInitialized();
            ApplyVisualState();
        }

        private void Update()
        {
            if (!isUnlocked)
            {
                return;
            }

            float dt = Time.deltaTime;
            float tempNorm = Mathf.Clamp01((float)(activeTempK / 500.0));
            float sqrtNorm = Mathf.Sqrt(tempNorm);

            // 1. Vibrate crystal lattice cores inside the cube proportionally to sqrt(T) (0 at 0 K).
            if (latticeAtoms != null && atomRestLocalPos != null)
            {
                float vibAmp = tempNorm <= 0.001f ? 0f : maxVibrationAmplitude * sqrtNorm;
                float freq = Mathf.Lerp(5f, 22f, tempNorm);
                float tNow = Time.time * freq;

                int count = Mathf.Min(latticeAtoms.Length, atomRestLocalPos.Length);
                for (int i = 0; i < count; i++)
                {
                    Transform atom = latticeAtoms[i];
                    if (atom == null) continue;

                    if (vibAmp <= 0.00001f)
                    {
                        atom.localPosition = atomRestLocalPos[i];
                    }
                    else
                    {
                        Vector3 jitter = new Vector3(
                            Mathf.Sin(tNow + atomPhaseX[i]),
                            Mathf.Cos(tNow * 1.13f + atomPhaseY[i]),
                            Mathf.Sin(tNow * 0.91f + atomPhaseZ[i])) * vibAmp;
                        atom.localPosition = atomRestLocalPos[i] + jitter;
                    }
                }
            }

            // 2. Move free electrons (blue) & holes (red) inside cube with speed increasing with T.
            float speed = Mathf.Lerp(minSpeedMps, maxSpeedMps, sqrtNorm);
            UpdateCarrierPool(pooledElectrons, ePos, eVel, activeCarrierCount, speed, dt);
            UpdateCarrierPool(pooledHoles, hPos, hVel, activeCarrierCount, speed * 0.72f, dt);
        }

        public void ConfigureReferences(
            Transform[] atoms,
            Vector3[] restPositions,
            Transform[] electrons,
            Transform[] holes,
            Renderer[] glowRenderers,
            TMP_Text label)
        {
            latticeAtoms = atoms;
            atomRestLocalPos = restPositions;
            pooledElectrons = electrons;
            pooledHoles = holes;
            thermalGlowRenderers = glowRenderers;
            statusLabel = label;
            initialized = false;
            EnsureInitialized();
        }

        /// <summary>
        /// Locks/dims or unlocks the 3D Thermal Chamber Cube.
        /// </summary>
        public void SetChamberUnlocked(bool unlocked)
        {
            EnsureInitialized();
            isUnlocked = unlocked;
            if (!unlocked)
            {
                ResetAtomsAndHideCarriers();
            }

            ApplyVisualState();
        }

        /// <summary>
        /// Syncs the cube's carrier count, vibration intensity, and thermal accent from SemiconductorModel.
        /// </summary>
        public void SetThermalState(SemiconductorMaterial material, double temperatureK, CarrierConcentrations concentrations)
        {
            EnsureInitialized();
            activeMaterial = material;
            activeTempK = temperatureK;

            int poolCap = Mathf.Min(
                pooledElectrons != null ? pooledElectrons.Length : 0,
                pooledHoles != null ? pooledHoles.Length : 0);

            if (poolCap <= 0 || temperatureK <= 0.01 || concentrations.IntrinsicNiCm3 <= 0.0)
            {
                activeCarrierCount = 0;
            }
            else
            {
                double logNi = System.Math.Log10(System.Math.Max(1.0, concentrations.IntrinsicNiCm3));
                float norm = Mathf.Clamp01((float)(logNi / 16.0));
                activeCarrierCount = Mathf.Clamp(Mathf.RoundToInt(norm * poolCap), 1, poolCap);
            }

            ApplyVisualState();
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            glowBlock = new MaterialPropertyBlock();

            int aCount = latticeAtoms != null ? latticeAtoms.Length : 0;
            atomPhaseX = new float[aCount];
            atomPhaseY = new float[aCount];
            atomPhaseZ = new float[aCount];
            for (int i = 0; i < aCount; i++)
            {
                atomPhaseX[i] = i * 1.37f;
                atomPhaseY[i] = i * 2.19f;
                atomPhaseZ[i] = i * 3.07f;
            }

            InitCarrierArray(pooledElectrons, out ePos, out eVel, 1.0f);
            InitCarrierArray(pooledHoles, out hPos, out hVel, -1.0f);
        }

        private void InitCarrierArray(Transform[] pool, out Vector3[] positions, out Vector3[] velocities, float sign)
        {
            int count = pool != null ? pool.Length : 0;
            positions = new Vector3[count];
            velocities = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                positions[i] = new Vector3(
                    Random.Range(-interiorHalfExtents.x * 0.8f, interiorHalfExtents.x * 0.8f),
                    Random.Range(-interiorHalfExtents.y * 0.8f, interiorHalfExtents.y * 0.8f),
                    Random.Range(-interiorHalfExtents.z * 0.8f, interiorHalfExtents.z * 0.8f));

                Vector3 dir = Random.onUnitSphere * sign;
                if (dir.sqrMagnitude < 0.01f) dir = Vector3.right * sign;
                velocities[i] = dir.normalized;
            }
        }

        private void UpdateCarrierPool(Transform[] pool, Vector3[] posArr, Vector3[] velArr, int countActive, float speed, float dt)
        {
            if (pool == null || posArr == null || velArr == null)
            {
                return;
            }

            int cap = Mathf.Min(pool.Length, posArr.Length);
            for (int i = 0; i < cap; i++)
            {
                Transform tr = pool[i];
                if (tr == null) continue;

                bool show = isUnlocked && i < countActive;
                if (tr.gameObject.activeSelf != show)
                {
                    tr.gameObject.SetActive(show);
                }

                if (!show)
                {
                    continue;
                }

                Vector3 p = posArr[i] + (velArr[i] * (speed * dt));
                Vector3 v = velArr[i];

                if (p.x < -interiorHalfExtents.x) { p.x = -interiorHalfExtents.x; v.x = Mathf.Abs(v.x); }
                else if (p.x > interiorHalfExtents.x) { p.x = interiorHalfExtents.x; v.x = -Mathf.Abs(v.x); }

                if (p.y < -interiorHalfExtents.y) { p.y = -interiorHalfExtents.y; v.y = Mathf.Abs(v.y); }
                else if (p.y > interiorHalfExtents.y) { p.y = interiorHalfExtents.y; v.y = -Mathf.Abs(v.y); }

                if (p.z < -interiorHalfExtents.z) { p.z = -interiorHalfExtents.z; v.z = Mathf.Abs(v.z); }
                else if (p.z > interiorHalfExtents.z) { p.z = interiorHalfExtents.z; v.z = -Mathf.Abs(v.z); }

                posArr[i] = p;
                velArr[i] = v;
                tr.localPosition = p;
            }
        }

        private void ResetAtomsAndHideCarriers()
        {
            if (latticeAtoms != null && atomRestLocalPos != null)
            {
                int count = Mathf.Min(latticeAtoms.Length, atomRestLocalPos.Length);
                for (int i = 0; i < count; i++)
                {
                    if (latticeAtoms[i] != null)
                    {
                        latticeAtoms[i].localPosition = atomRestLocalPos[i];
                    }
                }
            }

            HideAll(pooledElectrons);
            HideAll(pooledHoles);
        }

        private static void HideAll(Transform[] pool)
        {
            if (pool == null) return;
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] != null && pool[i].gameObject.activeSelf)
                {
                    pool[i].gameObject.SetActive(false);
                }
            }
        }

        private void ApplyVisualState()
        {
            UpdateCarrierPool(pooledElectrons, ePos, eVel, isUnlocked ? activeCarrierCount : 0, 0f, 0f);
            UpdateCarrierPool(pooledHoles, hPos, hVel, isUnlocked ? activeCarrierCount : 0, 0f, 0f);

            float norm = Mathf.Clamp01((float)(activeTempK / 500.0));
            Color railCol = !isUnlocked
                ? lockedGlowColor
                : (norm < 0.5f ? Color.Lerp(coldGlowColor, midGlowColor, norm * 2f) : Color.Lerp(midGlowColor, hotGlowColor, (norm - 0.5f) * 2f));
            float emissionMul = !isUnlocked ? 0.1f : Mathf.Lerp(0.35f, 0.95f, norm);

            if (thermalGlowRenderers != null)
            {
                if (glowBlock == null) glowBlock = new MaterialPropertyBlock();
                for (int i = 0; i < thermalGlowRenderers.Length; i++)
                {
                    Renderer r = thermalGlowRenderers[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(glowBlock);
                    glowBlock.SetColor(BaseColorId, railCol);
                    glowBlock.SetColor(EmissionColorId, railCol * emissionMul);
                    r.SetPropertyBlock(glowBlock);
                }
            }

            if (statusLabel != null)
            {
                if (!isUnlocked)
                {
                    statusLabel.text = "THERMAL CUBE (Locked)";
                }
                else
                {
                    string sym = SemiconductorModel.GetMaterialProperties(activeMaterial).Symbol;
                    statusLabel.text = activeTempK <= 0.01
                        ? sym + " | 0 K: Frozen (0 carriers) — Not to scale"
                        : sym + " | " + activeTempK.ToString("F0") + " K: " + activeCarrierCount + " e-/h+ pairs (Not to scale)";
                }
            }
        }
    }
}
