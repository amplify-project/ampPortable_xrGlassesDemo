using UnityEngine;
using UnityEngine.InputSystem;

namespace AmpPortableDataViz.Presentation.Interaction
{
    public sealed class VisualizationCycler : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionReference cycleAction;

        [Header("Visual Parents (exactly one active at a time)")]
        [SerializeField] private GameObject imageBoardParent;
        [SerializeField] private GameObject emotionVisualParent;
        [SerializeField] private GameObject graphVizParent;

        [Header("Startup")]
        [SerializeField] private int startIndex = 0; // 0=ImageBoard, 1=Emotion, 2=Graph

        private InputAction _boundAction;
        private int _currentIndex;

        private void Awake()
        {
            _currentIndex = Mathf.Clamp(startIndex, 0, 2);
            ApplyState(_currentIndex);
        }

        private void OnEnable()
        {
            if (cycleAction == null || cycleAction.action == null)
            {
                Debug.LogWarning("VisualizationCycler: No cycle action assigned.");
                return;
            }

            _boundAction = cycleAction.action;
            _boundAction.performed += OnCyclePerformed;

            if (!_boundAction.enabled)
            {
                _boundAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (_boundAction == null) return;

            _boundAction.performed -= OnCyclePerformed;
        }

        private void OnCyclePerformed(InputAction.CallbackContext context)
        {
            _currentIndex = (_currentIndex + 1) % 3;
            ApplyState(_currentIndex);
        }

        private void ApplyState(int index)
        {
            if (imageBoardParent != null)
            {
                imageBoardParent.SetActive(index == 0);
            }

            if (emotionVisualParent != null)
            {
                emotionVisualParent.SetActive(index == 1);
            }

            if (graphVizParent != null)
            {
                graphVizParent.SetActive(index == 2);
            }
        }
    }
}
