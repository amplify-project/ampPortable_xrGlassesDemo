using System.Collections.Generic;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Anchors
{
    public sealed class AnchorRegistry : MonoBehaviour
    {
        public static AnchorRegistry Instance { get; private set; }
        private readonly Dictionary<string, Transform> _anchorIdToTransformMap = new Dictionary<string, Transform>();

        private void Awake()
        {
            Instance = this;
        }

        public void Register(string anchorId, Transform anchorTransform)
        {
            if (string.IsNullOrEmpty(anchorId) || anchorTransform == null) return;
            _anchorIdToTransformMap[anchorId] = anchorTransform;
        }

        public Transform Get(string anchorId)
        {
            _anchorIdToTransformMap.TryGetValue(anchorId, out var transformForAnchor);
            return transformForAnchor;
        }
    }
}
