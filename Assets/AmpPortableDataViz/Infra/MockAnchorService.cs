using System.Collections.Generic;
using System.Threading.Tasks;
using AmpPortableDataViz.Core;
using UnityEngine;

namespace AmpPortableDataViz.Infra
{
    public sealed class MockAnchorService : IAnchorService
    {
        private readonly Dictionary<string, Transform> _anchorTransformsById = new Dictionary<string, Transform>();
        private int _createdAnchorCounter = 0;

        public async Task<string> CreateAsync(string label, Vector3 position, Quaternion rotation)
        {
            await Task.Yield();
            var anchorGameObject = new GameObject($"Anchor_{label}_{_createdAnchorCounter++}");
            anchorGameObject.transform.SetPositionAndRotation(position, rotation);
            var anchorId = anchorGameObject.GetInstanceID().ToString();
            _anchorTransformsById[anchorId] = anchorGameObject.transform;
            return anchorId;
        }

        public async Task<bool> LoadAsync(string anchorId)
        {
            await Task.Yield();
            return _anchorTransformsById.ContainsKey(anchorId);
        }

        public async Task<AnchorDescriptor> ExportDescriptorAsync(string anchorId)
        {
            await Task.Yield();
            if (!_anchorTransformsById.TryGetValue(anchorId, out var anchorTransform))
                return null;
            return new AnchorDescriptor
            {
                AnchorId = anchorId,
                Position = anchorTransform.position,
                Rotation = anchorTransform.rotation,
                Label = anchorTransform.name
            };
        }

        public async Task<string> ImportAndResolveAsync(AnchorDescriptor descriptor)
        {
            await Task.Yield();
            if (descriptor == null) return null;
            if (_anchorTransformsById.TryGetValue(descriptor.AnchorId, out var existingTransform))
            {
                existingTransform.SetPositionAndRotation(descriptor.Position, descriptor.Rotation);
                return descriptor.AnchorId;
            }
            var anchorGameObject = new GameObject(descriptor.Label ?? $"Anchor_{descriptor.AnchorId}");
            anchorGameObject.transform.SetPositionAndRotation(descriptor.Position, descriptor.Rotation);
            _anchorTransformsById[descriptor.AnchorId] = anchorGameObject.transform;
            return descriptor.AnchorId;
        }

        public Transform GetTransform(string anchorId)
        {
            _anchorTransformsById.TryGetValue(anchorId, out var anchorTransform);
            return anchorTransform;
        }
    }
}
