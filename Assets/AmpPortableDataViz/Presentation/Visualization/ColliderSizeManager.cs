using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class ColliderSizeManager : MonoBehaviour
{
    [SerializeField] private BoxCollider targetCollider;
    [SerializeField] private Vector3 padding = Vector3.zero;
    [SerializeField] private float minimumDepth = 0.01f;

    private static readonly Vector3[] CornerMultipliers =
    {
        new Vector3(-1f, -1f, -1f),
        new Vector3(-1f, -1f,  1f),
        new Vector3(-1f,  1f, -1f),
        new Vector3(-1f,  1f,  1f),
        new Vector3( 1f, -1f, -1f),
        new Vector3( 1f, -1f,  1f),
        new Vector3( 1f,  1f, -1f),
        new Vector3( 1f,  1f,  1f)
    };

    private Vector3 initialCenter;
    private Vector3 initialSize;
    private bool hasInitialValues;
    private bool sizeDirty = true;

    private void Awake()
    {
        if (!EnsureColliderReference())
        {
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (!EnsureColliderReference())
        {
            enabled = false;
            return;
        }

        ScoreLoader.ImageBoardInstantiated.AddListener(OnBoardsChanged);
        ScoreLoader.ImageBoardRemoved.AddListener(OnBoardsChanged);
        MarkDirty();
    }

    private void OnDisable()
    {
        ScoreLoader.ImageBoardInstantiated.RemoveListener(OnBoardsChanged);
        ScoreLoader.ImageBoardRemoved.RemoveListener(OnBoardsChanged);
    }

    private void LateUpdate()
    {
        if (!sizeDirty)
        {
            return;
        }

        sizeDirty = false;
        UpdateColliderNow();
    }

    private void OnTransformChildrenChanged()
    {
        MarkDirty();
    }

    private void OnBoardsChanged()
    {
        MarkDirty();
    }

    private void MarkDirty()
    {
        sizeDirty = true;
    }

    private void UpdateColliderNow()
    {
        if (targetCollider == null)
        {
            return;
        }

        var reference = targetCollider.transform;
        var childRenderers = GetComponentsInChildren<Renderer>(includeInactive: false);

        bool hasBounds = false;
        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        foreach (var childRenderer in childRenderers)
        {
            if (childRenderer == null ||
                childRenderer.transform == reference ||
                !childRenderer.enabled ||
                !childRenderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            EncapsulateBounds(childRenderer.bounds, reference, ref min, ref max, ref hasBounds);
        }

        if (!hasBounds)
        {
            var childColliders = GetComponentsInChildren<Collider>(includeInactive: false);
            foreach (var childCollider in childColliders)
            {
                if (childCollider == null ||
                    childCollider == targetCollider ||
                    childCollider.transform == reference ||
                    !childCollider.enabled ||
                    !childCollider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                EncapsulateBounds(childCollider.bounds, reference, ref min, ref max, ref hasBounds);
            }
        }

        if (!hasBounds)
        {
            if (hasInitialValues)
            {
                targetCollider.center = initialCenter;
                targetCollider.size = initialSize;
            }
            return;
        }

        var paddedMin = min - padding;
        var paddedMax = max + padding;

        var newCenter = (paddedMax + paddedMin) * 0.5f;
        var newSize = paddedMax - paddedMin;
        newSize.x = Mathf.Max(newSize.x, 0f);
        newSize.y = Mathf.Max(newSize.y, 0f);
        newSize.z = Mathf.Max(newSize.z, minimumDepth);

        targetCollider.center = newCenter;
        targetCollider.size = newSize;
    }

    private bool EnsureColliderReference()
    {
        if (targetCollider == null)
        {
            targetCollider = GetComponent<BoxCollider>();
        }

        if (targetCollider == null)
        {
            Debug.LogWarning($"{nameof(ColliderSizeManager)} on {name} requires a {nameof(BoxCollider)} to operate.");
            return false;
        }

        if (!hasInitialValues)
        {
            initialCenter = targetCollider.center;
            initialSize = targetCollider.size;
            hasInitialValues = true;
        }

        return true;
    }

    private void EncapsulateBounds(Bounds worldBounds, Transform reference, ref Vector3 min, ref Vector3 max, ref bool hasBounds)
    {
        var center = worldBounds.center;
        var extents = worldBounds.extents;

        for (int i = 0; i < CornerMultipliers.Length; i++)
        {
            var multiplier = CornerMultipliers[i];
            var corner = center + Vector3.Scale(extents, multiplier);
            var localCorner = reference.InverseTransformPoint(corner);

            if (!hasBounds)
            {
                min = localCorner;
                max = localCorner;
                hasBounds = true;
            }
            else
            {
                min = Vector3.Min(min, localCorner);
                max = Vector3.Max(max, localCorner);
            }
        }
    }
}
