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
    private bool _isGrabbed;
    private Vector3 _targetLocalOffset;
    private Quaternion _localRotOffset = Quaternion.identity;

    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _rb   = GetComponent<Rigidbody>();

        _grab.selectEntered.AddListener(OnGrabbed);
        _grab.lastSelectExited.AddListener(OnReleased);

        Debug.Log("PaperPlacementMode: Awake complete.");
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
        _grab.lastSelectExited.RemoveListener(OnReleased);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        Debug.Log("PaperPlacementMode: Grabbed, disabling follow.");
        // While held, stop following so the user can place it anywhere.
        _isGrabbed = true;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        Debug.Log("PaperPlacementMode: Released, setting follow mode: " + followMode);
        _isGrabbed = false;

        if (_camera == null) return;

        // if (followMode == FollowMode.WorldLocked)
        // {
        //     // Do nothing: paper stays in world where released.
        //     return;
        // }

        // For follow modes, compute the desired offset relative to the current camera.
        if (captureOffsetOnRelease)
        {
            Debug.Log("PaperPlacementMode: Capturing offset on release. Transform pos: " + transform.position   + " Camera pos: " + _camera.position);
            _targetLocalOffset = _camera.InverseTransformPoint(transform.position);
            Debug.Log($"PaperPlacementMode: Captured offset on release: {_targetLocalOffset}");

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
        if (_isGrabbed) return;

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
        Vector3 camPos = _camera.position;
        Vector3 desiredLocal = _camera.TransformDirection(_targetLocalOffset);
        float desiredRadius = desiredLocal.magnitude;

        if (desiredRadius < 1e-4f)
        {
            // Fallback: keep a small forward offset if the configured radius collapses.
            desiredLocal = _camera.forward * 0.5f;
            desiredRadius = desiredLocal.magnitude;
        }

        Vector3 currentLocal = transform.position - camPos;
        if (currentLocal.sqrMagnitude < 1e-6f)
        {
            currentLocal = desiredLocal;
        }

        Vector3 currentDir = currentLocal.normalized;
        Vector3 desiredDir = desiredLocal.normalized;

        float slerpT = 1f;
        if (positionSmoothTime > 0f)
        {
            slerpT = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(positionSmoothTime, 1e-4f));
        }

        Vector3 candidateDir = Vector3.Slerp(currentDir, desiredDir, slerpT);

        if (maxFollowSpeed > 0f && desiredRadius > 1e-4f)
        {
            float candidateRadians = Vector3.Angle(currentDir, candidateDir) * Mathf.Deg2Rad;
            float maxRadians = (maxFollowSpeed * Time.deltaTime) / desiredRadius;

            if (candidateRadians > maxRadians && maxRadians > 0f)
            {
                candidateDir = Vector3.RotateTowards(currentDir, desiredDir, maxRadians, 0f);
            }
        }

        Vector3 targetPos = camPos + candidateDir.normalized * desiredRadius;

        // Compute desired rotation
        Quaternion targetRot;
        if (faceUser)
        {
            if (yawOnly)
            {
                // Keep upright: rotate only around world up to face camera
                Vector3 toCamOnPlane = Vector3.ProjectOnPlane(transform.position - _camera.position, Vector3.up);
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
        _rb.isKinematic = true;
        transform.position = targetPos;

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
