using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public sealed class ColliderAttacher : MonoBehaviour
{
    [SerializeField] private XRGrabInteractable grabInteractable;

    private readonly HashSet<Collider> trackedColliders = new HashSet<Collider>();
    private readonly List<Collider> activeCollidersBuffer = new List<Collider>();
    private readonly List<Collider> collidersToAddBuffer = new List<Collider>();
    private readonly List<Collider> collidersToRemoveBuffer = new List<Collider>();

    private void Awake()
    {
        if (grabInteractable == null)
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
        }
    }

    private void OnEnable()
    {
        ScoreLoader.ImageBoardInstantiated.AddListener(OnImageBoardInstantiated);
        RefreshGrabInteractableColliders();
    }

    private void OnDisable()
    {
        ScoreLoader.ImageBoardInstantiated.RemoveListener(OnImageBoardInstantiated);
        RemoveTrackedColliders();
    }

    private void OnImageBoardInstantiated()
    {
        RefreshGrabInteractableColliders();
    }

    private void RefreshGrabInteractableColliders()
    {
        if (grabInteractable == null)
        {
            Debug.LogWarning($"{nameof(ColliderAttacher)} on {name} requires an {nameof(XRGrabInteractable)} on the same GameObject.");
            return;
        }

        var interactableColliders = grabInteractable.colliders;
        if (interactableColliders == null)
        {
            return;
        }

        activeCollidersBuffer.Clear();
        collidersToAddBuffer.Clear();
        collidersToRemoveBuffer.Clear();

        var childColliders = GetComponentsInChildren<BoxCollider>(includeInactive: false);
        foreach (var collider in childColliders)
        {
            if (collider == null || collider.transform == transform)
            {
                continue;
            }

            if (!collider.enabled || !collider.gameObject.activeInHierarchy)
            {
                continue;
            }

            activeCollidersBuffer.Add(collider);

            if (!interactableColliders.Contains(collider))
            {
                collidersToAddBuffer.Add(collider);
            }
        }

        foreach (var collider in trackedColliders)
        {
            if (collider == null || !activeCollidersBuffer.Contains(collider))
            {
                collidersToRemoveBuffer.Add(collider);
            }
        }

        if (collidersToAddBuffer.Count == 0 && collidersToRemoveBuffer.Count == 0)
        {
            return;
        }

        var interactionManager = grabInteractable.interactionManager;
        if (interactionManager != null)
        {
            interactionManager.UnregisterInteractable((IXRInteractable)grabInteractable);
        }

        foreach (var collider in collidersToRemoveBuffer)
        {
            if (collider != null)
            {
                interactableColliders.Remove(collider);
            }

            trackedColliders.Remove(collider);
        }

        foreach (var collider in collidersToAddBuffer)
        {
            interactableColliders.Add(collider);
            trackedColliders.Add(collider);
        }

        if (interactionManager != null && grabInteractable.isActiveAndEnabled)
        {
            interactionManager.RegisterInteractable((IXRInteractable)grabInteractable);
        }

        Debug.Log($"Updated {nameof(XRGrabInteractable)} colliders on {name}. Now tracking {trackedColliders.Count} colliders.");
    }

    private void RemoveTrackedColliders()
    {
        if (grabInteractable == null)
        {
            return;
        }

        var interactableColliders = grabInteractable.colliders;
        if (interactableColliders == null)
        {
            trackedColliders.Clear();
            return;
        }

        collidersToRemoveBuffer.Clear();
        foreach (var collider in trackedColliders)
        {
            if (collider == null || interactableColliders.Contains(collider))
            {
                collidersToRemoveBuffer.Add(collider);
            }
        }

        if (collidersToRemoveBuffer.Count == 0)
        {
            trackedColliders.Clear();
            return;
        }

        var interactionManager = grabInteractable.interactionManager;
        if (interactionManager != null)
        {
            interactionManager.UnregisterInteractable((IXRInteractable)grabInteractable);
        }

        foreach (var collider in collidersToRemoveBuffer)
        {
            if (collider != null)
            {
                interactableColliders.Remove(collider);
            }
        }

        trackedColliders.Clear();

        if (interactionManager != null && grabInteractable.isActiveAndEnabled)
        {
            interactionManager.RegisterInteractable((IXRInteractable)grabInteractable);
        }
    }
}
