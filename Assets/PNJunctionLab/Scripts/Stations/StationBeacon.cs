using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace PNJunctionLab
{
    /// <summary>
    /// Controls visual state (locked/dimmed vs unlocked vs completed) for a lab station's floor sign,
    /// hologram title, and teleport anchor pad.
    /// </summary>
    public class StationBeacon : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private string stationId = "T";
        [SerializeField] private string stationTitle = "Tutorial Pad";
        [SerializeField] private Renderer[] accentRenderers;
        [SerializeField] private TMP_Text floorNumberText;
        [SerializeField] private TMP_Text hologramTitleText;
        [SerializeField] private TeleportationAnchor teleportAnchor;
        [SerializeField] private bool startUnlocked;
        [SerializeField] private Color unlockedColor = new Color(0.14f, 0.82f, 1.0f, 1f);
        [SerializeField] private Color completedColor = new Color(0.22f, 0.95f, 0.52f, 1f);
        [SerializeField] private Color lockedColor = new Color(0.28f, 0.32f, 0.38f, 1f);

        private MaterialPropertyBlock propertyBlock;
        private bool isUnlocked;
        private bool isCompleted;

        public string StationId => stationId;
        public string StationTitle => stationTitle;
        public bool IsUnlocked => isUnlocked;
        public bool IsCompleted => isCompleted;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            ApplyLabels();
            SetState(startUnlocked, false);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                ApplyLabels();
                SetState(startUnlocked, false);
            }
        }
#endif

        /// <summary>
        /// Updates the station's visual unlocked/completed appearance without allocating new materials.
        /// </summary>
        public void SetState(bool unlocked, bool completed)
        {
            isUnlocked = unlocked;
            isCompleted = completed;

            Color accent = completed ? completedColor : (unlocked ? unlockedColor : lockedColor);
            float emissionMul = completed ? 0.75f : (unlocked ? 0.55f : 0.08f);

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            if (accentRenderers != null)
            {
                for (int i = 0; i < accentRenderers.Length; i++)
                {
                    Renderer r = accentRenderers[i];
                    if (r == null)
                    {
                        continue;
                    }

                    r.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor(BaseColorId, accent);
                    propertyBlock.SetColor(EmissionColorId, accent * emissionMul);
                    r.SetPropertyBlock(propertyBlock);
                }
            }

            if (floorNumberText != null)
            {
                floorNumberText.color = unlocked || completed ? Color.white : new Color(0.55f, 0.60f, 0.66f, 0.75f);
            }

            if (hologramTitleText != null)
            {
                hologramTitleText.color = unlocked || completed
                    ? accent
                    : new Color(0.52f, 0.56f, 0.62f, 0.65f);
            }

            if (teleportAnchor != null)
            {
                teleportAnchor.enabled = unlocked || completed;
            }
        }

        private void ApplyLabels()
        {
            if (floorNumberText != null && !string.IsNullOrEmpty(stationId))
            {
                floorNumberText.text = stationId;
            }

            if (hologramTitleText != null && !string.IsNullOrEmpty(stationTitle))
            {
                hologramTitleText.text = string.IsNullOrEmpty(stationId)
                    ? stationTitle
                    : stationId + ". " + stationTitle;
            }
        }
    }
}
