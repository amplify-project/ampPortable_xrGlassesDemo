using UnityEngine;

public interface IHandPoseProvider
{
    // Returns true if a reliable wrist pose is available this frame.
    bool TryGetWristPose(bool leftHand, out Pose wristPose, out bool palmUpValid, out Vector3 palmUp);
}
