using RayNeo;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Interaction
{
    /// <summary>
    /// Moves the current RayNeo visualization up or down when the right-temple touchpad
    /// receives a vertical swipe.
    /// </summary>
    [RequireComponent(typeof(RayNeoTempleVisualizationToggle))]
    public sealed class RayNeoTempleVisualizationVerticalMover : MonoBehaviour
    {
        [Header("Visualization Source")]
        [SerializeField] private RayNeoTempleVisualizationToggle visualizationToggle;

        [Header("Movement")]
        [SerializeField] private float yStep = 0.1f;
        [SerializeField] private float minLocalY = -1f;
        [SerializeField] private float maxLocalY = 1f;

        [Header("Diagnostics")]
        [SerializeField] private bool logDiagnostics = true;

        private bool _subscribed;
        private bool _gestureMoveApplied;

        private void Awake()
        {
            ResolveVisualizationToggle();
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
            yStep = Mathf.Max(0.001f, yStep);
            maxLocalY = Mathf.Max(minLocalY, maxLocalY);
        }

        public void MoveActiveVisualizationUp()
        {
            TryMoveActiveVisualization(yStep);
        }

        public void MoveActiveVisualizationDown()
        {
            TryMoveActiveVisualization(-yStep);
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

            LogDiagnostic("Subscribed to RayNeo vertical swipe events.");
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
            _gestureMoveApplied = false;
            LogDiagnostic($"Touch started at {position}; vertical move gesture latch reset.");
        }

        private void OnTempleTouchMove(TouchActionType actionType, Vector2 position)
        {
            if (!IsVerticalSwipeStep(actionType))
            {
                return;
            }

            LogDiagnostic($"Received vertical swipe-step event {actionType} at {position}.");
            TryMoveForSwipeAction(actionType);
        }

        private void OnTempleSwipeEnd(TouchActionType actionType, Vector2 position)
        {
            LogDiagnostic($"Received raw swipe-end event {actionType}.");
            TryMoveForSwipeAction(actionType);
        }

        private void TryMoveForSwipeAction(TouchActionType actionType)
        {
            if (_gestureMoveApplied)
            {
                LogDiagnostic($"Swipe action {actionType} ignored because this touch gesture already moved.");
                return;
            }

            var moved = false;
            if (IsUpSwipe(actionType))
            {
                moved = TryMoveActiveVisualization(yStep);
            }
            else if (IsDownSwipe(actionType))
            {
                moved = TryMoveActiveVisualization(-yStep);
            }

            if (moved)
            {
                _gestureMoveApplied = true;
            }
        }

        private bool TryMoveActiveVisualization(float yDelta)
        {
            var activeRoot = FindActiveRoot();
            if (activeRoot == null)
            {
                LogDiagnostic("Swipe ignored because no current visualization root is active.");
                return false;
            }

            var localPosition = activeRoot.localPosition;
            var previousY = localPosition.y;
            localPosition.y = Mathf.Clamp(localPosition.y + yDelta, minLocalY, maxLocalY);

            if (Mathf.Approximately(previousY, localPosition.y))
            {
                LogDiagnostic($"{activeRoot.name} is already at the local Y limit ({localPosition.y:0.00}).");
                return false;
            }

            activeRoot.localPosition = localPosition;

            LogDiagnostic($"Moved {activeRoot.name} local Y from {previousY:0.00} to {localPosition.y:0.00}.");
            return true;
        }

        private Transform FindActiveRoot()
        {
            ResolveVisualizationToggle();

            if (visualizationToggle == null)
            {
                Debug.LogWarning("RayNeoTempleVisualizationVerticalMover: VisualizationToggle is not assigned and no RayNeoTempleVisualizationToggle was found on this GameObject.");
                return null;
            }

            if (!visualizationToggle.TryGetCurrentVisualizationRoot(out var root) || root == null || !root.gameObject.activeInHierarchy)
            {
                return null;
            }

            return root;
        }

        private void ResolveVisualizationToggle()
        {
            if (visualizationToggle == null)
            {
                visualizationToggle = GetComponent<RayNeoTempleVisualizationToggle>();
            }
        }

        private static bool IsVerticalSwipeStep(TouchActionType actionType)
        {
            return actionType == TouchActionType.UP_SWIPE_STEP || actionType == TouchActionType.DOWN_SWIPE_STEP;
        }

        private static bool IsUpSwipe(TouchActionType actionType)
        {
            return actionType == TouchActionType.UP_SWIPE_STEP
                || actionType == TouchActionType.UP_SWIPE_END;
        }

        private static bool IsDownSwipe(TouchActionType actionType)
        {
            return actionType == TouchActionType.DOWN_SWIPE_STEP
                || actionType == TouchActionType.DOWN_SWPIE_END;
        }

        private void LogDiagnostic(string message)
        {
            if (logDiagnostics)
            {
                Debug.Log($"RayNeoTempleVisualizationVerticalMover: {message}");
            }
        }
    }
}
