using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PNJunctionLab
{
    /// <summary>
    /// Grabbable Station 6 semiconductor diode sample module (Si, Ge, LED, or Zener)
    /// that plugs into the I-V Characteristic Analyzer test socket.
    /// </summary>
    public class DiodeSampleModule : MonoBehaviour
    {
        [SerializeField] private DiodeType diodeType = DiodeType.Silicon;
        [SerializeField] private string shortLabel = "Si";
        [SerializeField] private string displayName = "Silicon Diode (1N4007-style)";
        [SerializeField] private XRGrabInteractable grabInteractable;
        [SerializeField] private RespawnableItem respawnableItem;

        public DiodeType DiodeType => diodeType;
        public string ShortLabel => shortLabel;
        public string DisplayName => displayName;

        private bool interactive = true;

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
        }

        /// <summary>
        /// Enables or disables grabbing when Station 6 locks or unlocks.
        /// </summary>
        public void SetInteractive(bool canInteract)
        {
            interactive = canInteract;
            if (grabInteractable != null)
            {
                grabInteractable.enabled = canInteract;
            }
        }

        /// <summary>
        /// Returns this sample module safely to the Station 6 sample tray.
        /// </summary>
        public void ReturnToTray()
        {
            if (respawnableItem != null)
            {
                respawnableItem.Respawn();
            }

            if (grabInteractable != null)
            {
                grabInteractable.enabled = interactive;
            }
        }
    }
}
