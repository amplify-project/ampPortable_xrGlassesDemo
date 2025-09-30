using UnityEngine;
using UnityEngine.XR.Hands;

public class PinchToggle : MonoBehaviour
{
    public WristMenuFollower follower;
    public MonoBehaviour poseProviderBehaviour; // same provider as follower
    public float pinchThreshold = 0.03f; // meters
    public float unshowPalmUpDot = 0.2f; // hide if palm faces downward (dot with world up < 0.2)

    IHandPoseProvider _provider;
    bool _visible;

    void Awake()
    {
        _provider = poseProviderBehaviour as IHandPoseProvider;
        SetVisible(false);
    }

    void Update()
    {
        if (_provider == null) return;

        // If XR Hands is the provider, we can read tips directly for robust pinch;
        // otherwise simplify to "show when palm up".
        var xr = _provider as XRHandsPoseProvider;
        if (xr != null)
        {
            // Fetch tips via XR Hands quickly:
            if (TryGetTipDistance(follower.leftHand, out float d))
            {
                if (d < pinchThreshold) SetVisible(true);
            }
            if (TryGetPalmUpDot(follower.leftHand, out float dotUp) && dotUp < unshowPalmUpDot)
                SetVisible(false);
        }
        else
        {
            if (_provider.TryGetWristPose(follower.leftHand, out _, out var palmValid, out var palmUp))
            {
                if (palmValid)
                {
                    float dot = Vector3.Dot(palmUp, Vector3.up);
                    if (dot > 0.6f) SetVisible(true);
                    if (dot < unshowPalmUpDot) SetVisible(false);
                }
            }
        }
    }

    bool TryGetTipDistance(bool left, out float dist)
    {
        dist = 1f;
        var list = new System.Collections.Generic.List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(list);
        if (list.Count == 0 || !list[0].running) return false;
        var hand = left ? list[0].leftHand : list[0].rightHand;
        if (!hand.isTracked) return false;
        if (hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumb) &&
            hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var index))
        {
            dist = Vector3.Distance(thumb.position, index.position);
            return true;
        }
        return false;
    }

    bool TryGetPalmUpDot(bool left, out float dot)
    {
        dot = 0f;
        var list = new System.Collections.Generic.List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(list);
        if (list.Count == 0 || !list[0].running) return false;
        var hand = left ? list[0].leftHand : list[0].rightHand;
        if (!hand.isTracked) return false;
        if (hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var palm))
        {
            dot = Vector3.Dot(palm.up, Vector3.up);
            return true;
        }
        return false;
    }

    void SetVisible(bool on)
    {
        if (_visible == on) return;
        _visible = on;
        follower.gameObject.SetActive(on);
    }
}
