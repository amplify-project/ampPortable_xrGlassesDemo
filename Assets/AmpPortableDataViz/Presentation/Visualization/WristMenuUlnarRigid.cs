using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

public class WristMenuUlnarRigid : MonoBehaviour
{
    [Header("Hand")]
    public bool leftHand = true;          // set true for left wrist, false for right

    [Header("Placement (meters)")]
    public float sideDistance = 0.055f;   // + toward little finger (ulnar side)
    public float alongPalmDistance = -0.020f; // + toward fingers, - toward forearm
    public float liftDistance = 0.008f;   // lift off the palm plane (along palmUp)

    [Header("Orientation")]
    public bool invertNormal = false;     // flip if your Canvas front/back is reversed
    public bool invertUlnar = false;      // flip if offset goes to the thumb side

    [Header("Smoothing")]
    public float followSmoothing = 18f;   // 0 = no smoothing; increase to damp jitter

    XRHandSubsystem _hands;

    void Awake()
    {
        var subs = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subs);
        if (subs.Count > 0) _hands = subs[0];
    }

    void LateUpdate()
    {
        if (_hands == null || !_hands.running) return;

        XRHand hand = leftHand ? _hands.leftHand : _hands.rightHand;
        if (!hand.isTracked) return;

        if (!hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out var wrist)) return;

        // Palm up / ulnar direction
        Vector3 palmUp = wrist.rotation * Vector3.up;
        if (hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var palmPose))
            palmUp = palmPose.up;

        Vector3 ulnarRaw = wrist.rotation * Vector3.right; // fallback
        if (hand.GetJoint(XRHandJointID.IndexMetacarpal).TryGetPose(out var indexMcp) &&
            hand.GetJoint(XRHandJointID.LittleMetacarpal).TryGetPose(out var littleMcp))
        {
            // Across the palm toward the little finger (opposite the thumb)
            ulnarRaw = (littleMcp.position - indexMcp.position);
        }

        // Desired basis: forward = palmUp (menu faces same way as palm); right = ulnar in palm plane
        Vector3 forward = palmUp.normalized;
        if (invertNormal) forward = -forward;

        // Project ulnar onto palm plane to kill any component along palmUp
        Vector3 right = Vector3.ProjectOnPlane(ulnarRaw, forward).normalized;
        if (invertUlnar) right = -right;
        if (right.sqrMagnitude < 1e-6f) right = Vector3.Cross(Vector3.up, forward).normalized; // degenerate fallback

        // Make it orthonormal and get the third axis (up lies within palm plane, roughly along fingers)
        Vector3.OrthoNormalize(ref forward, ref right);                 // right ⟂ forward
        Vector3 up = Vector3.Cross(forward, right).normalized;          // completes RHS frame

        // Position rigidly on ulnar edge, with slight fore/aft and lift
        Vector3 targetPos = wrist.position
                          + right * sideDistance
                          + up * alongPalmDistance
                          + forward * liftDistance;

        Quaternion targetRot = Quaternion.LookRotation(forward, up);

        float t = (followSmoothing <= 0f) ? 1f : 1f - Mathf.Exp(-followSmoothing * Time.deltaTime);
        transform.SetPositionAndRotation(
            Vector3.Lerp(transform.position, targetPos, t),
            Quaternion.Slerp(transform.rotation, targetRot, t)
        );
    }
}
