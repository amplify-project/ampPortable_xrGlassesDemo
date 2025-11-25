using UnityEngine;

namespace AmpPortableDataViz.Presentation.Visualization
{
    /// <summary>
    /// Rotates the attached object to face the main camera, keeping the label legible.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BillboardLabel : MonoBehaviour
    {
        [SerializeField] private bool yawOnly = true;
        [SerializeField] private bool flipForward = true;

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            Vector3 toCamera = cam.transform.position - transform.position;
            if (toCamera.sqrMagnitude < 1e-6f)
            {
                return;
            }

            if (yawOnly)
            {
                toCamera.y = 0f;
                if (toCamera.sqrMagnitude < 1e-6f)
                {
                    toCamera = cam.transform.forward;
                }
                var rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
                if (flipForward)
                {
                    rotation *= Quaternion.Euler(0f, 180f, 0f);
                }
                transform.rotation = rotation;
            }
            else
            {
                var rotation = Quaternion.LookRotation(toCamera.normalized, cam.transform.up);
                if (flipForward)
                {
                    rotation *= Quaternion.Euler(0f, 180f, 0f);
                }
                transform.rotation = rotation;
            }
        }
    }
}
