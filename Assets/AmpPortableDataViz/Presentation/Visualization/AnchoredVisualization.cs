using AmpPortableDataViz.Presentation.Anchors;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Visualization
{
    public sealed class AnchoredVisualization : MonoBehaviour
    {
        public string AnchorId;
        public Transform VisualRoot;

        public void BindTransform()
        {
            var targetAnchorTransform = AnchorRegistry.Instance?.Get(AnchorId);
            var transformToBind = VisualRoot == null ? transform : VisualRoot;
            transformToBind.SetParent(targetAnchorTransform, worldPositionStays: false);
            transformToBind.localPosition = Vector3.zero;
            transformToBind.localRotation = Quaternion.identity;
        }
    }
}
