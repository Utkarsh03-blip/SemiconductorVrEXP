using UnityEngine;
using UnityEngine.AI;

namespace PNJunctionLab
{
    /// <summary>
    /// Registers a pre-baked NavMeshData asset at runtime without requiring external AI navigation packages.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class LabNavMeshSurface : MonoBehaviour
    {
        [SerializeField] private NavMeshData navMeshData;

        private NavMeshDataInstance instance;

        public void SetNavMeshData(NavMeshData data)
        {
            navMeshData = data;
        }

        private void OnEnable()
        {
            if (navMeshData != null)
            {
                instance = NavMesh.AddNavMeshData(navMeshData, transform.position, transform.rotation);
            }
        }

        private void OnDisable()
        {
            if (instance.valid)
            {
                instance.Remove();
            }
        }
    }
}
