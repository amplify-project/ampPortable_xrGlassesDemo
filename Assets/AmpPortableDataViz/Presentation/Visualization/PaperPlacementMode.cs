using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class PaperPlacementMode : MonoBehaviour
{
    public enum FollowMode { WorldLocked, CameraFollow, ElasticFollow }

    [Header("Mode")]
    public FollowMode followMode = FollowMode.WorldLocked;

    [Header("Follow Pose (relative to camera)")]
    [Tooltip("Default offset from the camera when follow begins (meters in camera local space).")]
    public Vector3 defaultLocalOffset = new Vector3(0.35f, -0.10f, 0.75f); // right, slightly down, in front
    [Tooltip("If true, we recapture the offset from how the user placed it (camera-relative). If false, we use defaultLocalOffset.")]
    public bool captureOffsetOnRelease = true;

    [Header("Smoothing")]
    [Range(0.01f, 1.0f)] public float positionSmoothTime = 0.12f;
    [Range(1f, 20f)] public float rotationLerpSpeed = 8f;
    [Tooltip("Max linear follow speed (m/s). Set 0 for unlimited.")]
    public float maxFollowSpeed = 2.0f;

    [Header("Facing / Billboarding")]
    [Tooltip("If true, the paper will face the user while following.")]
    public bool faceUser = true;
    [Tooltip("If true, yaw only (keeps paper upright). If false, full face.")]
    public bool yawOnly = true;

    [Header("Elastic Follow Triggers")]
    [Tooltip("Max distance from the camera before the paper re-centers to the follow pose.")]
    public float maxDistance = 1.25f;
    [Tooltip("Max off-angle (degrees) between where you're looking and the paper before it re-centers.")]
    public float maxOffAxisAngle = 40f;

    private XRGrabInteractable _grab;
    private Rigidbody _rb;
    private Transform _camera;

    // Follow state
    private Vector3 _targetLocalOffset;
    private Quaternion _localRotOffset = Quaternion.identity;
    private Vector3 _vel; // for SmoothDamp

    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _rb   = GetComponent<Rigidbody>();

        _grab.selectEntered.AddListener(OnGrabbed);
        _grab.selectExited.AddListener(OnReleased);
    }

    private void Start()
    {
        // Find the XR camera. With XREAL/AR rigs this is typically the center-eye camera on your XR Origin.
        Camera cam = Camera.main;
        if (cam != null) _camera = cam.transform;
        else Debug.LogWarning("PaperPlacementMode: No Camera.main found. Assign _camera manually.");

        _targetLocalOffset = defaultLocalOffset;
    }

    private void OnDestroy()
    {
        _grab.selectEntered.RemoveListener(OnGrabbed);
        _grab.selectExited.RemoveListener(OnReleased);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        // While held, stop following so the user can place it anywhere.
        // Let XRIT drive pose during grab.
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (_camera == null) return;

        if (followMode == FollowMode.WorldLocked)
        {
            // Do nothing: paper stays in world where released.
            return;
        }

        // For follow modes, compute the desired offset relative to the current camera.
        if (captureOffsetOnRelease)
        {
            _targetLocalOffset = _camera.InverseTransformPoint(transform.position);

            if (!faceUser)
            {
                // Preserve current rotation relative to camera
                _localRotOffset = Quaternion.Inverse(_camera.rotation) * transform.rotation;
            }
            else
            {
                // We'll compute rotation each frame to face the user; no fixed localRotOffset needed
                _localRotOffset = Quaternion.identity;
            }
        }
        else
        {
            // Use the defaultLocalOffset and a friendly forward-facing rotation
            _localRotOffset = Quaternion.identity;
        }
    }

    private void LateUpdate()
    {
        if (_camera == null) return;

        switch (followMode)
        {
            case FollowMode.WorldLocked:
                // No work required.
                break;

            case FollowMode.CameraFollow:
                FollowCamera();
                break;

            case FollowMode.ElasticFollow:
                if (ShouldRecenter())
                {
                    FollowCamera();
                }
                // else remain where it is (world-locked) until thresholds are exceeded
                break;
        }
    }

    private void FollowCamera()
    {
        // Target position based on stored offset
        Vector3 targetPos = _camera.TransformPoint(_targetLocalOffset);

        // Compute desired rotation
        Quaternion targetRot;
        if (faceUser)
        {
            if (yawOnly)
            {
                // Keep upright: rotate only around world up to face camera
                Vector3 toCamOnPlane = Vector3.ProjectOnPlane(_camera.position - transform.position, Vector3.up);
                if (toCamOnPlane.sqrMagnitude < 1e-4f) toCamOnPlane = Vector3.forward;
                targetRot = Quaternion.LookRotation(toCamOnPlane.normalized, Vector3.up);
            }
            else
            {
                Vector3 toCam = (_camera.position - transform.position).normalized;
                targetRot = Quaternion.LookRotation(toCam, _camera.up);
            }
        }
        else
        {
            targetRot = _camera.rotation * _localRotOffset;
        }

        // Temporarily kinematic for smooth placement (prevents physics jitter when following)
        bool wasKinematic = _rb.isKinematic;
        _rb.isKinematic = true;

        // Smooth position
        float maxSpeed = (maxFollowSpeed > 0f) ? maxFollowSpeed : Mathf.Infinity;
        Vector3 current = transform.position;
        Vector3 next = Vector3.SmoothDamp(current, targetPos, ref _vel, positionSmoothTime, maxSpeed);
        transform.position = next;

        // Smooth rotation
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationLerpSpeed);

        // Restore kinematic flag for next frame (kept true while following, re-enabled physics only when grabbed)
        _rb.isKinematic = true;
    }

    private bool ShouldRecenter()
    {
        // If too far…
        float dist = Vector3.Distance(transform.position, _camera.position);
        if (dist > maxDistance) return true;

        // If too far off the user’s forward axis…
        Vector3 toPaper = (transform.position - _camera.position).normalized;
        float angle = Vector3.Angle(_camera.forward, toPaper);
        return angle > maxOffAxisAngle;
    }

    // Optional: expose these to UI buttons/toggles
    public void SetWorldLocked()   => followMode = FollowMode.WorldLocked;
    public void SetCameraFollow()  => followMode = FollowMode.CameraFollow;
    public void SetElasticFollow() => followMode = FollowMode.ElasticFollow;
}
