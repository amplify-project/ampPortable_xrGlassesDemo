using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

public class WristMenuUlnarFollower : MonoBehaviour
{
    [Header("Hand")]
    public bool leftHand = true;          // choose the hand you want to follow

    [Header("Offsets (meters)")]
    public float sideDistance = 0.055f;   // towards ulnar side (opposite the thumb)
    public float backDistance = 0.02f;    // slightly back toward forearm
    public float liftDistance = 0.00f;    // small lift off the palm plane

    [Header("Orientation & smoothing")]
    public Transform head;                // XR camera
    [Range(0,1)] public float faceBlend = 1.0f;   // 1 = always face head
    public float followSmoothing = 16f;   // exponential smoothing

    XRHandSubsystem _hands;

    void Awake()
    {
        if (!head && Camera.main) head = Camera.main.transform;

        var subs = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subs);
        if (subs.Count > 0) _hands = subs[0];
    }

    void LateUpdate()
    {
        if (_hands == null || !_hands.running || head == null) return;

        XRHand hand = leftHand ? _hands.leftHand : _hands.rightHand;
        if (!hand.isTracked) return;

        // Required joints
        if (!hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out var wristPose)) return;

        // Nice-to-have joints (for ulnar direction + palm up)
        bool haveIndex = hand.GetJoint(XRHandJointID.IndexMetacarpal).TryGetPose(out var indexMcp);
        bool haveLittle = hand.GetJoint(XRHandJointID.LittleMetacarpal).TryGetPose(out var littleMcp);
        bool havePalm = hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var palmPose);

        Vector3 ulnarDir;
        Vector3 palmUp;

        if (haveIndex && haveLittle)
            ulnarDir = (littleMcp.position - indexMcp.position).normalized; // across the palm, toward pinky = opposite thumb
        else
            ulnarDir = wristPose.rotation * Vector3.right; // fallback

        palmUp = havePalm ? palmPose.up : wristPose.rotation * Vector3.up;

        // Derive an approximate "fingers forward" on the hand plane
        Vector3 palmForward = Vector3.Cross(palmUp, ulnarDir).normalized;

        // Build target position on the ulnar side, slightly toward the forearm, with a tiny lift
        Vector3 targetPos = wristPose.position
                            + ulnarDir * sideDistance
                            - palmForward * backDistance
                            + palmUp * liftDistance;

        // Face the player’s head, but keep the hand’s up for stability
        Quaternion wristAligned = wristPose.rotation;
        Quaternion faceHead = Quaternion.LookRotation((head.position - targetPos).normalized, palmUp);
        Quaternion targetRot = Quaternion.Slerp(wristAligned, faceHead, faceBlend);

        // after you compute targetRot
        targetRot = targetRot * Quaternion.AngleAxis(180f, palmUp); // flip around up


        // Smooth follow (exp smoothing)
        float t = 1f - Mathf.Exp(-followSmoothing * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPos, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
    }
}
