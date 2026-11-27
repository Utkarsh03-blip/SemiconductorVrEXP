using UnityEngine;

namespace PNJunctionLab
{
    /// <summary>
    /// Catches items falling into a catch volume and provides a table-level method to return managed items.
    /// </summary>
    public class RespawnZone : MonoBehaviour
    {
        [SerializeField] private RespawnableItem[] managedItems;

        private void OnTriggerEnter(Collider other)
        {
            RespawnableItem item = other.GetComponentInParent<RespawnableItem>();
            if (item != null)
            {
                item.Respawn();
            }
        }

        /// <summary>
        /// Returns all items registered to this station/zone back to their home spots.
        /// </summary>
        public void RespawnAllManagedItems()
        {
            if (managedItems == null)
            {
                return;
            }

            for (int i = 0; i < managedItems.Length; i++)
            {
                if (managedItems[i] != null)
                {
                    managedItems[i].Respawn();
                }
            }
        }
    }
}
