using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class GrabManager : MonoBehaviour
{
    public XRGrabInteractable grabInteractable;

/// <summary>
/// Toggles the lock state of the object. When locked, the object cannot be grabbed or moved. When unlocked, it can be interacted with as normal.
/// </summary>
    public void OnLockPositionClicked(bool isOn)
    {
        Debug.Log($"Lock Position Toggle: {isOn}");
        if (isOn)
        {
            Debug.Log("Position Locked");
            grabInteractable.enabled = false;
        }
        else if (!isOn)
        {
            Debug.Log("Position Unlocked");
            grabInteractable.enabled = true;
        }
        else
        {
            Debug.LogError("Invalid input for lock state. Expected a boolean value.");
        }
    }
}
