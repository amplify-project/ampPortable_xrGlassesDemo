using System;
using UnityEditor;
using UnityEngine;
using System.Linq;
using UnityEngine.XR.Interaction.Toolkit;

public class DynamicAttachPointHandler : MonoBehaviour
{
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable; // Reference to the XR Grab Interactable component
    public Transform[] attachPoints; // Array of attach points around the edges of the quad

    private void Awake()
    {
        if (grabInteractable == null)
        {
            grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        }

        // Subscribe to the select entered event
        grabInteractable.selectEntered.AddListener(OnGrab);
    }

    private void OnDestroy()
    {
        // Unsubscribe from the select entered event
        grabInteractable.selectEntered.RemoveListener(OnGrab);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        // Get the interactor's position (e.g., the hand/controller grabbing the object)
        Vector3 interactorPosition = args.interactorObject.transform.position;

        // Find the closest attach point to the interactor's position
        Transform closestAttachPoint = attachPoints
            .OrderBy(point => Vector3.Distance(point.position, interactorPosition))
            .FirstOrDefault();

        // Assign the closest attach point to the Attach Transform field
        if (closestAttachPoint != null)
        {
            grabInteractable.attachTransform = closestAttachPoint;
        }
    }
}
