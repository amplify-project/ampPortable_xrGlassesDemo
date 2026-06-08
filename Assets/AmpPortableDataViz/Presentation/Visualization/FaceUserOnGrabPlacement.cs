using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace AmpPortableDataViz.Presentation.Visualization
{
    /// <summary>
    /// Keeps a grabbable visualization oriented toward the user's camera while preserving its placed position.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable))]
    [AddComponentMenu("Amp Portable Data Viz/Visualization/Face User On Grab Placement")]
    public sealed class FaceUserOnGrabPlacement : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform userCamera;

        [Header("Facing")]
        [SerializeField] private bool faceWhileGrabbed = true;
        [SerializeField] private bool faceWhenPlaced = true;
        [SerializeField] private bool yawOnly = true;
        [SerializeField] private bool flipForward;
        [SerializeField, Min(0f)] private float rotationSpeed = 18f;

        private XRGrabInteractable _grabInteractable;

        public void Configure(
            Transform cameraTransform,
            bool faceDuringGrab,
            bool keepFacingWhenPlaced,
            bool constrainToYaw,
            bool invertForward,
            float speed)
        {
            userCamera = cameraTransform;
            faceWhileGrabbed = faceDuringGrab;
            faceWhenPlaced = keepFacingWhenPlaced;
            yawOnly = constrainToYaw;
            flipForward = invertForward;
            rotationSpeed = Mathf.Max(0f, speed);
        }

        public void FaceNow()
        {
            ApplyFacing(instant: true);
        }

        private void Awake()
        {
            ResolveGrabInteractable();
            ResolveCamera();
        }

        private void OnEnable()
        {
            ResolveGrabInteractable();
            if (_grabInteractable == null)
            {
                return;
            }

            _grabInteractable.selectEntered.AddListener(OnSelectEntered);
            _grabInteractable.lastSelectExited.AddListener(OnLastSelectExited);
        }

        private void OnDisable()
        {
            if (_grabInteractable == null)
            {
                return;
            }

            _grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            _grabInteractable.lastSelectExited.RemoveListener(OnLastSelectExited);
        }

        private void LateUpdate()
        {
            if (_grabInteractable == null)
            {
                ResolveGrabInteractable();
            }

            bool isGrabbed = _grabInteractable != null && _grabInteractable.isSelected;
            if ((isGrabbed && faceWhileGrabbed) || (!isGrabbed && faceWhenPlaced))
            {
                ApplyFacing(instant: false);
            }
        }

        private void OnValidate()
        {
            rotationSpeed = Mathf.Max(0f, rotationSpeed);
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (faceWhileGrabbed)
            {
                ApplyFacing(instant: true);
            }
        }

        private void OnLastSelectExited(SelectExitEventArgs args)
        {
            if (faceWhenPlaced)
            {
                ApplyFacing(instant: true);
            }
        }

        private void ResolveGrabInteractable()
        {
            if (_grabInteractable == null)
            {
                _grabInteractable = GetComponent<XRGrabInteractable>();
            }
        }

        private void ResolveCamera()
        {
            if (userCamera != null)
            {
                return;
            }

            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                userCamera = mainCamera.transform;
            }
        }

        private void ApplyFacing(bool instant)
        {
            ResolveCamera();
            if (userCamera == null)
            {
                return;
            }

            Vector3 toCamera = userCamera.position - transform.position;
            if (yawOnly)
            {
                toCamera = Vector3.ProjectOnPlane(toCamera, Vector3.up);
            }

            if (toCamera.sqrMagnitude < 1e-6f)
            {
                toCamera = yawOnly
                    ? Vector3.ProjectOnPlane(-userCamera.forward, Vector3.up)
                    : -userCamera.forward;
            }

            if (toCamera.sqrMagnitude < 1e-6f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(toCamera.normalized, yawOnly ? Vector3.up : userCamera.up);
            if (flipForward)
            {
                targetRotation *= Quaternion.Euler(0f, 180f, 0f);
            }

            if (instant || rotationSpeed <= 0f || !UnityEngine.Application.isPlaying)
            {
                transform.rotation = targetRotation;
                return;
            }

            float t = 1f - Mathf.Exp(-rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
        }
    }
}
