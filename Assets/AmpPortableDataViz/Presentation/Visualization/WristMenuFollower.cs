using UnityEngine;

public class WristMenuFollower : MonoBehaviour
{
    [Header("Hand Source")]
    public MonoBehaviour poseProviderBehaviour; // assign XRHandsPoseProvider OR XRealPoseProvider
    public bool leftHand = true;

    IHandPoseProvider _provider;

    [Header("Offsets (in wrist space)")]
    public Vector3 localOffset = new Vector3(0.055f, 0.02f, 0.00f); // +X = ulnar side
    public Vector3 localEulerOffset = new Vector3(0f, 90f, 0f);     // face along forearm

    [Header("Readability")]
    public Transform head;               // HMD camera
    [Range(0f, 1f)] public float faceBlend = 0.35f; // 0=wrist aligned, 1=faces camera
    public float followSmoothing = 20f;  // exponential smoothing
    public float minHeadDistance = 0.25f;// meters; fade when too close
    public CanvasGroup canvasGroup;      // optional for proximity fade

    [Header("Clamp")]
    public float maxSwingAngle = 65f;    // keep menu within a cone around wrist "up"

    [Header("Coordinate space (use if menu is offset)")]
    public bool jointsAreInSessionSpace = false;       // toggle this on if you see a fixed offset
    public Transform sessionToWorld;                   // assign XR Origin's "Origin"/root transform



    void Awake()
    {
        _provider = poseProviderBehaviour as IHandPoseProvider;
        if (!head && Camera.main) head = Camera.main.transform;
        if (!canvasGroup) canvasGroup = GetComponentInChildren<CanvasGroup>();
    }

    void LateUpdate()
    {
        if (_provider == null) return;

        if (_provider.TryGetWristPose(leftHand, out var wrist, out var palmUpValid, out var palmUp))
        {
            //var baseRot = wrist.rotation;
            var targetPos = Vector3.zero;// = wrist.position + baseRot * localOffset;
            var targetRot = Quaternion.identity;// = baseRot * Quaternion.Euler(localEulerOffset);

            var rawPos = wrist.position;
            var rawRot = wrist.rotation;

            if (jointsAreInSessionSpace && sessionToWorld)
            {
                var worldPos = sessionToWorld.TransformPoint(rawPos);
                var worldRot = sessionToWorld.rotation * rawRot;

                targetPos = worldPos + worldRot * localOffset;
                targetRot = worldRot * Quaternion.Euler(localEulerOffset);
            }
            else
            {
                targetPos = rawPos + rawRot * localOffset;
                targetRot = rawRot * Quaternion.Euler(localEulerOffset);
            }

            // Face-to-head blend for readability
            if (head)
            {
                var toHead = (head.position - targetPos).normalized;
                var faceRot = Quaternion.LookRotation(toHead, targetRot * Vector3.up);
                targetRot = Quaternion.Slerp(targetRot, faceRot, faceBlend);
            }

            // Clamp swing so the menu doesn't pass under the forearm
            if (palmUpValid)
            {
                var currentUp = targetRot * Vector3.up;
                float ang = Vector3.Angle(currentUp, palmUp);
                if (ang > maxSwingAngle)
                {
                    var axis = Vector3.Cross(currentUp, palmUp).normalized;
                    var clampRot = Quaternion.AngleAxis(ang - maxSwingAngle, axis);
                    targetRot = clampRot * targetRot;
                }
            }

            float t = 1f - Mathf.Exp(-followSmoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPos, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);

            // Proximity fade
            if (canvasGroup && head)
            {
                float d = Vector3.Distance(transform.position, head.position);
                canvasGroup.alpha = Mathf.InverseLerp(minHeadDistance, minHeadDistance + 0.2f, d);
            }
        }
    }
}
