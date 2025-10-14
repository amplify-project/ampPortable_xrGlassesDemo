using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.XR.Interaction.Toolkit;

public class DynamicAttachPointHandler : MonoBehaviour
{
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable; // Reference to the XR Grab Interactable component
    public List<Transform> attachPoints = new List<Transform>(); // Attach points around the edges of the quad

    private void Awake()
    {
        if (grabInteractable == null)
        {
            grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        }

        grabInteractable.selectEntered.AddListener(OnGrab);
        RefreshAttachPoints();
    }

    private void OnEnable()
    {
        ScoreLoader.ImageBoardInstantiated.AddListener(OnImageBoardInstantiated);
        RefreshAttachPoints();
    }

    private void OnDisable()
    {
        ScoreLoader.ImageBoardInstantiated.RemoveListener(OnImageBoardInstantiated);
    }

    private void OnDestroy()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrab);
        ScoreLoader.ImageBoardInstantiated.RemoveListener(OnImageBoardInstantiated);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        Vector3 interactorPosition = args.interactorObject.transform.position;

        Transform closestAttachPoint = attachPoints
            .OrderBy(point => Vector3.Distance(point.position, interactorPosition))
            .FirstOrDefault();

        if (closestAttachPoint != null)
        {
            grabInteractable.attachTransform = closestAttachPoint;
        }
    }

    private void OnImageBoardInstantiated()
    {
        RefreshAttachPoints();
    }

    private void RefreshAttachPoints()
    {
        attachPoints.Clear();
        var childTransforms = GetComponentsInChildren<Transform>(includeInactive: false);
        foreach (var child in childTransforms)
        {
            if (child == null || child == transform)
            {
                continue;
            }

            if (child.name.StartsWith("AttachPoint_", System.StringComparison.OrdinalIgnoreCase))
            {
                attachPoints.Add(child);
            }
        }

        Debug.Log($"DynamicAttachPointHandler: Found {attachPoints.Count} attach points.");
    }
}
