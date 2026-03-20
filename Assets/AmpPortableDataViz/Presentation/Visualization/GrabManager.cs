using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class GrabManager : MonoBehaviour
{
    public GameObject imageBoardParent;
    public GameObject emtionVisualParent;
    public GameObject graphVizParent;
    public Toggle toggle;


    private List<XRGrabInteractable> CheckVizParent()
    {

        List<XRGrabInteractable> grabInteractables = new List<XRGrabInteractable>();

        if (imageBoardParent != null && imageBoardParent.activeSelf)
        {
            grabInteractables.AddRange(imageBoardParent.GetComponentsInChildren<XRGrabInteractable>());
        }
        else if (emtionVisualParent != null && emtionVisualParent.activeSelf)
        {
            grabInteractables.AddRange(emtionVisualParent.GetComponentsInChildren<XRGrabInteractable>());
        }
        else if (graphVizParent != null && graphVizParent.activeSelf)
        {
            grabInteractables.AddRange(graphVizParent.GetComponentsInChildren<XRGrabInteractable>());
        }
        else
        {
            Debug.LogWarning("GrabManager: No active visualization parent found. Please ensure one of the parents is active.");
        }

        return grabInteractables;        
    }

/// <summary>
/// Toggles the lock state of the object. When locked, the object cannot be grabbed or moved. When unlocked, it can be interacted with as normal.
/// </summary>
    public void OnLockPositionClicked(bool isOn)
    {
        List<XRGrabInteractable> grabInteractables = CheckVizParent();

        Debug.Log($"Lock Position Toggle: {isOn}");
        if (isOn)
        {
            Debug.Log("Position Locked");
            foreach (var grabInteractable in grabInteractables)
            {
                grabInteractable.enabled = false;
            }
        }
        else if (!isOn)
        {
            Debug.Log("Position Unlocked");
            foreach (var grabInteractable in grabInteractables)
            {
                grabInteractable.enabled = true;
            }
        }
        else
        {
            Debug.LogError("Invalid input for lock state. Expected a boolean value.");
        }
    }

    public void ResetToggle()
    {
        if (toggle != null && toggle.isOn)
        {
            Debug.Log("Resetting lock toggle state to off.");
            toggle.isOn = false;
        }
        else
        {
            Debug.LogWarning("GrabManager: Toggle reference is not assigned. Cannot reset toggle state.");
        }
    }
}
