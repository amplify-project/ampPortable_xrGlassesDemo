using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

public class XRHandsPoseProvider : MonoBehaviour, IHandPoseProvider
{
    XRHandSubsystem _subsys;

    void Awake()
    {
        var list = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(list);
        if (list.Count > 0) _subsys = list[0];
    }

    public bool TryGetWristPose(bool leftHand, out Pose wristPose, out bool palmUpValid, out Vector3 palmUp)
    {
        wristPose = default;
        palmUpValid = false; palmUp = Vector3.up;

        if (_subsys == null || !_subsys.running) return false;

        XRHand hand = leftHand ? _subsys.leftHand : _subsys.rightHand;
        if (!hand.isTracked) return false;

        if (!hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out wristPose)) return false;

        // Optional: a rough "palm up" vector using the Palm joint, if present
        if (hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var palmPose))
        {
            palmUp = palmPose.up; // OpenXR convention
            palmUpValid = true;
        }
        return true;
    }
}
