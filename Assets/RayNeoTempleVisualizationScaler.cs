using System.Collections.Generic;
using AmpPortableDataViz.Presentation.Visualization;
using RayNeo;
using UnityEngine;

/// <summary>
/// Scales the currently active visualization when the RayNeo right-temple touchpad is swiped
/// while the user's gaze is on that visualization.
/// </summary>
public sealed class RayNeoTempleVisualizationScaler : MonoBehaviour
{
    [Header("Visual Roots")]
    [SerializeField] private Transform[] visualizationRoots = new Transform[0];

    [Header("Gaze")]
    [SerializeField] private Camera gazeCamera;
    [SerializeField] private float maxGazeDistance = 10f;
    [SerializeField] private LayerMask gazeLayerMask = ~0;

    [Header("Scale")]
    [SerializeField] private float scaleStep = 0.15f;
    [SerializeField] private float minScale = 0.35f;
    [SerializeField] private float maxScale = 2.5f;

    [Header("Swipe Mapping")]
    [SerializeField] private bool forwardSwipeIsRightSwipe = true;

    [Header("Diagnostics")]
    [SerializeField] private bool logDiagnostics = true;

    private readonly Dictionary<Transform, Vector3> _initialScales = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<ParticleMeshVisualizer, float> _initialParticleMeshVisualScales = new Dictionary<ParticleMeshVisualizer, float>();
    private bool _subscribed;
    private bool _gestureScaleApplied;

    private void Awake()
    {
        if (gazeCamera == null)
        {
            gazeCamera = Camera.main;
        }

        CacheInitialScales();
    }

    private void OnEnable()
    {
        SubscribeToTempleSwipes();
    }

    private void OnDisable()
    {
        UnsubscribeFromTempleSwipes();
    }

    private void OnDestroy()
    {
        UnsubscribeFromTempleSwipes();
    }

    private void OnValidate()
    {
        scaleStep = Mathf.Clamp(scaleStep, 0.01f, 0.95f);
        minScale = Mathf.Max(0.01f, minScale);
        maxScale = Mathf.Max(minScale, maxScale);
        maxGazeDistance = Mathf.Max(0.01f, maxGazeDistance);
    }

    public void ReduceActiveVisualizationScale()
    {
        TryScaleActiveVisualization(1f - scaleStep);
    }

    public void IncreaseActiveVisualizationScale()
    {
        TryScaleActiveVisualization(1f + scaleStep);
    }

    private void SubscribeToTempleSwipes()
    {
        if (_subscribed)
        {
            return;
        }

        SimpleTouchForLite.Instance.OnTouchStart.AddListener(OnTempleTouchStart);
        SimpleTouchForLite.Instance.OnTouchMove.AddListener(OnTempleTouchMove);
        SimpleTouchForLite.Instance.OnSwipEnd.AddListener(OnTempleSwipeEnd);
        _subscribed = true;

        LogDiagnostic("Subscribed to RayNeo swipe step and raw swipe-end events.");
    }

    private void UnsubscribeFromTempleSwipes()
    {
        if (!_subscribed || !SimpleTouchForLite.SingletonExist)
        {
            _subscribed = false;
            return;
        }

        SimpleTouchForLite.Instance.OnTouchStart.RemoveListener(OnTempleTouchStart);
        SimpleTouchForLite.Instance.OnTouchMove.RemoveListener(OnTempleTouchMove);
        SimpleTouchForLite.Instance.OnSwipEnd.RemoveListener(OnTempleSwipeEnd);
        _subscribed = false;
    }

    private void OnTempleTouchStart(Vector2 position)
    {
        _gestureScaleApplied = false;
        LogDiagnostic($"Touch started at {position}; scale gesture latch reset.");
    }

    private void OnTempleTouchMove(TouchActionType actionType, Vector2 position)
    {
        if (!IsHorizontalSwipeStep(actionType))
        {
            return;
        }

        LogDiagnostic($"Received swipe-step event {actionType} at {position}.");
        TryScaleForSwipeAction(actionType);
    }

    private void OnTempleSwipeEnd(TouchActionType actionType, Vector2 position)
    {
        LogDiagnostic($"Received raw swipe-end event {actionType} at {position}.");
        TryScaleForSwipeAction(actionType);
    }

    private void TryScaleForSwipeAction(TouchActionType actionType)
    {
        if (_gestureScaleApplied)
        {
            LogDiagnostic($"Swipe action {actionType} ignored because this touch gesture already scaled.");
            return;
        }

        var scaled = false;
        if (IsForwardSwipe(actionType))
        {
            scaled = TryScaleActiveVisualization(1f - scaleStep);
        }
        else if (IsBackwardSwipe(actionType))
        {
            scaled = TryScaleActiveVisualization(1f + scaleStep);
        }

        if (scaled)
        {
            _gestureScaleApplied = true;
        }
    }

    private bool TryScaleActiveVisualization(float scaleMultiplier)
    {
        var activeRoot = FindGazedActiveRoot();
        if (activeRoot == null)
        {
            LogDiagnostic("Swipe ignored because gaze did not resolve to an active visualization root.");
            return false;
        }

        var particleMeshVisualizer = FindParticleMeshVisualizer(activeRoot);
        if (particleMeshVisualizer != null)
        {
            return TryScaleParticleMeshVisualizer(particleMeshVisualizer, scaleMultiplier);
        }

        CacheInitialScale(activeRoot);
        var initialScale = _initialScales[activeRoot];
        var currentRelativeScale = GetCurrentRelativeScale(activeRoot, initialScale);
        var targetRelativeScale = Mathf.Clamp(currentRelativeScale * scaleMultiplier, minScale, maxScale);

        activeRoot.localScale = initialScale * targetRelativeScale;

        LogDiagnostic($"Scaled {activeRoot.name} from {currentRelativeScale:0.00}x to {targetRelativeScale:0.00}x.");
        return true;
    }

    private bool TryScaleParticleMeshVisualizer(ParticleMeshVisualizer visualizer, float scaleMultiplier)
    {
        CacheInitialParticleMeshVisualScale(visualizer);

        var initialVisualScale = _initialParticleMeshVisualScales[visualizer];
        var currentRelativeScale = GetAxisScaleRatio(visualizer.VisualScale, initialVisualScale);
        var targetRelativeScale = Mathf.Clamp(currentRelativeScale * scaleMultiplier, minScale, maxScale);
        var targetVisualScale = initialVisualScale * targetRelativeScale;

        visualizer.SetVisualScale(targetVisualScale);

        LogDiagnostic($"Set {visualizer.name} VisualScale from {currentRelativeScale:0.00}x to {targetRelativeScale:0.00}x ({targetVisualScale:0.00}).");
        return true;
    }

    private Transform FindGazedActiveRoot()
    {
        if (gazeCamera == null)
        {
            Debug.LogWarning("RayNeoTempleVisualizationScaler: GazeCamera is not assigned and Camera.main was not found.");
            return null;
        }

        if (visualizationRoots == null || visualizationRoots.Length == 0)
        {
            Debug.LogWarning("RayNeoTempleVisualizationScaler: No Visualization Roots are assigned.");
            return null;
        }

        var gazeRay = new Ray(gazeCamera.transform.position, gazeCamera.transform.forward);
        var hitRoot = FindPhysicsHitRoot(gazeRay);
        if (hitRoot != null)
        {
            return hitRoot;
        }

        for (var i = 0; i < visualizationRoots.Length; i++)
        {
            var root = visualizationRoots[i];
            if (IsUsableActiveRoot(root) && RayIntersectsRenderedBounds(root, gazeRay))
            {
                LogDiagnostic($"Gaze matched {root.name} by rendered bounds.");
                return root;
            }
        }

        return null;
    }

    private Transform FindPhysicsHitRoot(Ray gazeRay)
    {
        var hits = Physics.RaycastAll(gazeRay, maxGazeDistance, gazeLayerMask, QueryTriggerInteraction.Collide);
        var closestDistance = float.PositiveInfinity;
        Transform closestRoot = null;

        for (var hitIndex = 0; hitIndex < hits.Length; hitIndex++)
        {
            var hitTransform = hits[hitIndex].transform;
            for (var rootIndex = 0; rootIndex < visualizationRoots.Length; rootIndex++)
            {
                var root = visualizationRoots[rootIndex];
                if (!IsUsableActiveRoot(root) || !IsSameOrChildOf(hitTransform, root))
                {
                    continue;
                }

                if (hits[hitIndex].distance < closestDistance)
                {
                    closestDistance = hits[hitIndex].distance;
                    closestRoot = root;
                }
            }
        }

        if (closestRoot != null)
        {
            LogDiagnostic($"Gaze physics hit matched {closestRoot.name} at {closestDistance:0.00}m.");
        }

        return closestRoot;
    }

    private void CacheInitialScales()
    {
        if (visualizationRoots == null)
        {
            return;
        }

        for (var i = 0; i < visualizationRoots.Length; i++)
        {
            CacheInitialScale(visualizationRoots[i]);
            CacheInitialParticleMeshVisualScale(FindParticleMeshVisualizer(visualizationRoots[i]));
        }
    }

    private void CacheInitialScale(Transform target)
    {
        if (target != null && !_initialScales.ContainsKey(target))
        {
            _initialScales.Add(target, target.localScale);
        }
    }

    private void CacheInitialParticleMeshVisualScale(ParticleMeshVisualizer visualizer)
    {
        if (visualizer != null && !_initialParticleMeshVisualScales.ContainsKey(visualizer))
        {
            _initialParticleMeshVisualScales.Add(visualizer, visualizer.VisualScale);
        }
    }

    private static bool IsUsableActiveRoot(Transform root)
    {
        return root != null && root.gameObject.activeInHierarchy;
    }

    private static ParticleMeshVisualizer FindParticleMeshVisualizer(Transform root)
    {
        return root == null ? null : root.GetComponentInChildren<ParticleMeshVisualizer>(false);
    }

    private bool IsForwardSwipe(TouchActionType actionType)
    {
        if (forwardSwipeIsRightSwipe)
        {
            return actionType == TouchActionType.LEFT_SWIPE_STEP
                || actionType == TouchActionType.LEFT_SWIPE_END
                || actionType == TouchActionType.FAST_LEFT_SWIPE_END;
        }

        return actionType == TouchActionType.RIGHT_SWIPE_STEP
            || actionType == TouchActionType.RIGHT_SWIPE_END
            || actionType == TouchActionType.FAST_RIGHT_SWIPE_END;
    }

    private bool IsBackwardSwipe(TouchActionType actionType)
    {
        if (forwardSwipeIsRightSwipe)
        {
            return actionType == TouchActionType.RIGHT_SWIPE_STEP
                || actionType == TouchActionType.RIGHT_SWIPE_END
                || actionType == TouchActionType.FAST_RIGHT_SWIPE_END;
        }

        return actionType == TouchActionType.LEFT_SWIPE_STEP
            || actionType == TouchActionType.LEFT_SWIPE_END
            || actionType == TouchActionType.FAST_LEFT_SWIPE_END;
    }

    private static bool IsHorizontalSwipeStep(TouchActionType actionType)
    {
        return actionType == TouchActionType.LEFT_SWIPE_STEP || actionType == TouchActionType.RIGHT_SWIPE_STEP;
    }

    private void LogDiagnostic(string message)
    {
        if (logDiagnostics)
        {
            Debug.Log($"RayNeoTempleVisualizationScaler: {message}");
        }
    }

    private static bool IsSameOrChildOf(Transform candidate, Transform root)
    {
        while (candidate != null)
        {
            if (candidate == root)
            {
                return true;
            }

            candidate = candidate.parent;
        }

        return false;
    }

    private static bool RayIntersectsRenderedBounds(Transform root, Ray ray)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(false);
        if (renderers.Length == 0)
        {
            return false;
        }

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds.IntersectRay(ray, out var distance) && distance >= 0f;
    }

    private static float GetCurrentRelativeScale(Transform target, Vector3 initialScale)
    {
        var x = GetAxisScaleRatio(target.localScale.x, initialScale.x);
        var y = GetAxisScaleRatio(target.localScale.y, initialScale.y);
        var z = GetAxisScaleRatio(target.localScale.z, initialScale.z);

        return Mathf.Max(x, y, z);
    }

    private static float GetAxisScaleRatio(float currentAxisScale, float initialAxisScale)
    {
        if (Mathf.Approximately(initialAxisScale, 0f))
        {
            return 1f;
        }

        return Mathf.Abs(currentAxisScale / initialAxisScale);
    }
}
