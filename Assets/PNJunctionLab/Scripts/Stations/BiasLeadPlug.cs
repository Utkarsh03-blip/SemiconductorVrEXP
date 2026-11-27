using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PNJunctionLab
{
    /// <summary>
    /// Polarity of a Station 5 variable DC battery lead plug.
    /// </summary>
    public enum BiasLeadPolarity
    {
        PositiveRed = 0,
        NegativeBlack = 1
    }

    /// <summary>
    /// Grabbable battery cable plug with live zero-allocation LineRenderer wire drape
    /// from the battery binding post to the plug base.
    /// </summary>
    public class BiasLeadPlug : MonoBehaviour
    {
        [SerializeField] private BiasLeadPolarity polarity = BiasLeadPolarity.PositiveRed;
        [SerializeField] private Transform batteryPostOrigin;
        [SerializeField] private Transform wireAttachPoint;
        [SerializeField] private LineRenderer cableRenderer;
        [SerializeField] private XRGrabInteractable grabInteractable;
        [SerializeField] private RespawnableItem respawnableItem;
        [SerializeField] private int cableSegments = 10;
        [SerializeField] private float cableSagMeters = 0.08f;

        public BiasLeadPolarity Polarity => polarity;

        private Vector3[] pointBuffer;
        private bool controlsInteractive = true;

        private void Awake()
        {
            if (grabInteractable == null)
            {
                grabInteractable = GetComponent<XRGrabInteractable>();
            }

            if (respawnableItem == null)
            {
                respawnableItem = GetComponent<RespawnableItem>();
            }

            int count = Mathf.Max(2, cableSegments) + 1;
            pointBuffer = new Vector3[count];
            if (cableRenderer != null)
            {
                cableRenderer.positionCount = count;
                cableRenderer.useWorldSpace = true;
            }
        }

        private void Start()
        {
            UpdateCableVisual();
        }

        private void LateUpdate()
        {
            UpdateCableVisual();
        }

        /// <summary>
        /// Enables or disables grabbing when Station 5 locks or unlocks.
        /// </summary>
        public void SetInteractive(bool interactive)
        {
            controlsInteractive = interactive;
            if (grabInteractable != null)
            {
                grabInteractable.enabled = interactive;
            }
        }

        /// <summary>
        /// Returns this lead plug safely to its battery holster.
        /// </summary>
        public void ReturnToBattery()
        {
            if (respawnableItem != null)
            {
                respawnableItem.Respawn();
            }

            if (grabInteractable != null)
            {
                grabInteractable.enabled = controlsInteractive;
            }

            UpdateCableVisual();
        }

        private void UpdateCableVisual()
        {
            if (cableRenderer == null || batteryPostOrigin == null || pointBuffer == null)
            {
                return;
            }

            Vector3 start = batteryPostOrigin.position;
            Vector3 end = wireAttachPoint != null ? wireAttachPoint.position : transform.position;
            int count = pointBuffer.Length;
            float span = Vector3.Distance(start, end);
            float sag = Mathf.Clamp(cableSagMeters * (0.45f + span), 0.02f, 0.14f);

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                Vector3 p = Vector3.Lerp(start, end, t);
                p.y -= Mathf.Sin(t * Mathf.PI) * sag;
                pointBuffer[i] = p;
            }

            cableRenderer.SetPositions(pointBuffer);
        }
    }
}
