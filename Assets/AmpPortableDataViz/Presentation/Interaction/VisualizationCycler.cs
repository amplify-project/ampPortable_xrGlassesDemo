using UnityEngine;
namespace AmpPortableDataViz.Presentation.Interaction
{
    public sealed class VisualizationCycler : MonoBehaviour
    {
        [Header("Visual Parents (exactly one active at a time)")]
        [SerializeField] private GameObject imageBoardParent;
        [SerializeField] private GameObject emotionVisualParent;
        [SerializeField] private GameObject graphVizParent;

        [Header("Startup")]
        [SerializeField] private int startIndex = 0; // 0=ImageBoard, 1=Emotion, 2=Graph

        private int _currentIndex;

        private void Awake()
        {
            _currentIndex = Mathf.Clamp(startIndex, 0, 2);
            if (imageBoardParent == null)
            {
                Debug.LogWarning("VisualizationCycler: ImageBoardParent is not assigned.");
            }
            if (emotionVisualParent == null)
            {
                Debug.LogWarning("VisualizationCycler: EmotionVisualParent is not assigned.");
            }
            if (graphVizParent == null)
            {
                Debug.LogWarning("VisualizationCycler: GraphVizParent is not assigned.");
            }
            ApplyState(_currentIndex);
        }

        // public void CycleVisualization()
        // {
        //     _currentIndex = (_currentIndex + 1) % 3;
        //     ApplyState(_currentIndex);
        // }

        public void ApplyState(int index)
        {
            Debug.Log($"VisualizationCycler: Applying visualization index {index}.");
            
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
