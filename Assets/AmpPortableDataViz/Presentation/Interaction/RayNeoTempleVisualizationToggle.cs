using RayNeo;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Interaction
{
    /// <summary>
    /// Cycles assigned visualization roots when the RayNeo right-temple touchpad receives a single tap.
    /// </summary>
    public sealed class RayNeoTempleVisualizationToggle : MonoBehaviour
    {
        [Header("Visual Roots (exactly one active at a time)")]
        [SerializeField] private GameObject[] visualizationRoots = new GameObject[0];

        [Header("Startup")]
        [SerializeField] private int startIndex;
        [SerializeField] private bool applyStartupState = true;

        private int _currentIndex;
        private bool _subscribed;

        public int CurrentIndex => _currentIndex;

        public bool TryGetCurrentVisualizationRoot(out Transform root)
        {
            root = null;

            if (!HasVisualizationRoots())
            {
                return false;
            }

            _currentIndex = ClampIndex(_currentIndex);
            var currentRoot = visualizationRoots[_currentIndex];
            if (currentRoot == null)
            {
                Debug.LogWarning($"RayNeoTempleVisualizationToggle: Current visualization root {_currentIndex} is not assigned.");
                return false;
            }

            root = currentRoot.transform;
            return true;
        }

        private void Awake()
        {
            _currentIndex = ClampIndex(startIndex);

            if (applyStartupState)
            {
                ApplyVisibility();
            }
        }

        private void OnEnable()
        {
            SubscribeToTempleTap();
        }

        private void OnDisable()
        {
            UnsubscribeFromTempleTap();
        }

        private void OnDestroy()
        {
            UnsubscribeFromTempleTap();
        }

        private void OnValidate()
        {
            startIndex = ClampIndex(startIndex);
        }

        public void ToggleVisualization()
        {
            ShowNext();
        }

        public void ShowNext()
        {
            if (!HasVisualizationRoots())
            {
                return;
            }

            Show((_currentIndex + 1) % visualizationRoots.Length);
        }

        public void ShowPrevious()
        {
            if (!HasVisualizationRoots())
            {
                return;
            }

            Show((_currentIndex + visualizationRoots.Length - 1) % visualizationRoots.Length);
        }

        public void Show(int index)
        {
            if (!HasVisualizationRoots())
            {
                return;
            }

            _currentIndex = Mathf.Clamp(index, 0, visualizationRoots.Length - 1);
            ApplyVisibility();
        }

        private void SubscribeToTempleTap()
        {
            if (_subscribed)
            {
                return;
            }

            SimpleTouchForLite.Instance.OnSimpleTap.AddListener(ToggleVisualization);
            _subscribed = true;
        }

        private void UnsubscribeFromTempleTap()
        {
            if (!_subscribed || !SimpleTouchForLite.SingletonExist)
            {
                _subscribed = false;
                return;
            }

            SimpleTouchForLite.Instance.OnSimpleTap.RemoveListener(ToggleVisualization);
            _subscribed = false;
        }

        private void ApplyVisibility()
        {
            if (!HasVisualizationRoots())
            {
                return;
            }

            for (var i = 0; i < visualizationRoots.Length; i++)
            {
                var root = visualizationRoots[i];
                if (root == null)
                {
                    Debug.LogWarning($"RayNeoTempleVisualizationToggle: Visualization root {i} is not assigned.");
                    continue;
                }

                root.SetActive(i == _currentIndex);
            }
        }

        private bool HasVisualizationRoots()
        {
            if (visualizationRoots != null && visualizationRoots.Length > 0)
            {
                return true;
            }

            Debug.LogWarning("RayNeoTempleVisualizationToggle: No visualization roots are assigned.");
            return false;
        }

        private int ClampIndex(int index)
        {
            if (visualizationRoots == null || visualizationRoots.Length == 0)
            {
                return 0;
            }

            return Mathf.Clamp(index, 0, visualizationRoots.Length - 1);
        }
    }
}
