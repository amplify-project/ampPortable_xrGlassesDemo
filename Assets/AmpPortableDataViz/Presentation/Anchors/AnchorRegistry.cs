using System.Collections.Generic;
using AmpPortableDataViz.Core;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Anchors
{
    public sealed class AnchorRegistry : MonoBehaviour, IAnchorRegistry
    {
        private readonly Dictionary<string, Transform> _anchorIdToTransformMap = new Dictionary<string, Transform>();

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

        public bool TryGet(string anchorId, out Transform anchorTransform)
        {
            return _anchorIdToTransformMap.TryGetValue(anchorId, out anchorTransform);
        }
    }
}
