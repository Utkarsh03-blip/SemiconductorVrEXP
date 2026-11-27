using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PNJunctionLab
{
    /// <summary>
    /// Tracks a grabbable lab object's home pose and respawns it safely when dropped out of bounds
    /// or when requested by a table Return Item button.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RespawnableItem : MonoBehaviour
    {
        [SerializeField] private Transform homeAnchor;
        [SerializeField] private float minWorldY = -0.35f;
        [SerializeField] private Vector2 roomBoundsX = new Vector2(-8.2f, 8.2f);
        [SerializeField] private Vector2 roomBoundsZ = new Vector2(-5.7f, 5.7f);
        [SerializeField] private AudioSource audioSource;

        private Rigidbody body;
        private XRGrabInteractable grabInteractable;
        private Vector3 initialPosition;
        private Quaternion initialRotation;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            grabInteractable = GetComponent<XRGrabInteractable>();
            initialPosition = homeAnchor != null ? homeAnchor.position : transform.position;
            initialRotation = homeAnchor != null ? homeAnchor.rotation : transform.rotation;
        }

        private void Update()
        {
            if (grabInteractable != null && grabInteractable.isSelected)
            {
                return;
            }

            if (IsOutOfBounds(transform.position))
            {
                Respawn();
            }
        }

        /// <summary>
        /// Immediately returns this item to its home table spot and zeroes its Rigidbody velocities.
        /// </summary>
        public void Respawn()
        {
            if (grabInteractable != null && grabInteractable.isSelected && grabInteractable.interactionManager != null)
            {
                var selecting = grabInteractable.firstInteractorSelecting;
                if (selecting != null)
                {
                    grabInteractable.interactionManager.SelectExit(selecting, grabInteractable);
                }
            }

            Vector3 targetPos = homeAnchor != null ? homeAnchor.position : initialPosition;
            Quaternion targetRot = homeAnchor != null ? homeAnchor.rotation : initialRotation;

            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = targetPos;
                body.rotation = targetRot;
            }

            transform.SetPositionAndRotation(targetPos, targetRot);
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Teleport, 0.35f);
        }

        private bool IsOutOfBounds(Vector3 pos)
        {
            return pos.y < minWorldY
                || pos.x < roomBoundsX.x || pos.x > roomBoundsX.y
                || pos.z < roomBoundsZ.x || pos.z > roomBoundsZ.y;
        }
    }
}
