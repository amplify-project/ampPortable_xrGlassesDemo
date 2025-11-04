using AmpPortableDataViz.Presentation.Anchors;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Visualization
{
    public sealed class AnchoredVisualization : MonoBehaviour
    {
        public string AnchorId;
        public Transform VisualRoot;

        public void BindToAnchor(Transform anchorTransform)
        {
            if (anchorTransform == null) return;
            var transformToBind = VisualRoot == null ? transform : VisualRoot;
            transformToBind.SetParent(anchorTransform, worldPositionStays: false);
            transformToBind.localPosition = Vector3.zero;
            transformToBind.localRotation = Quaternion.identity;
        }
    }
}
