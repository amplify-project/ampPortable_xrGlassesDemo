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

        public Task<string> CreateAsync(string label, Vector3 position, Quaternion rotation)
        {
            var anchorGameObject = new GameObject($"Anchor_{label}_{_createdAnchorCounter++}");
            anchorGameObject.transform.SetPositionAndRotation(position, rotation);
            var anchorId = anchorGameObject.GetInstanceID().ToString();
            _anchorTransformsById[anchorId] = anchorGameObject.transform;
            return Task.FromResult(anchorId);
        }

        public Task<bool> LoadAsync(string anchorId)
        {
            return Task.FromResult(_anchorTransformsById.ContainsKey(anchorId));
        }

        public Task<AnchorDescriptor> ExportDescriptorAsync(string anchorId)
        {
            if (!_anchorTransformsById.TryGetValue(anchorId, out var anchorTransform))
                return Task.FromResult<AnchorDescriptor>(null);

            var descriptor = new AnchorDescriptor
            {
                AnchorId = anchorId,
                Position = anchorTransform.position,
                Rotation = anchorTransform.rotation,
                Label = anchorTransform.name
            };
            return Task.FromResult(descriptor);
        }

        public Task<string> ImportAndResolveAsync(AnchorDescriptor descriptor)
        {
            if (descriptor == null) return Task.FromResult<string>(null);
            if (_anchorTransformsById.TryGetValue(descriptor.AnchorId, out var existingTransform))
            {
                existingTransform.SetPositionAndRotation(descriptor.Position, descriptor.Rotation);
                return Task.FromResult(descriptor.AnchorId);
            }
            var anchorGameObject = new GameObject(descriptor.Label ?? $"Anchor_{descriptor.AnchorId}");
            anchorGameObject.transform.SetPositionAndRotation(descriptor.Position, descriptor.Rotation);
            _anchorTransformsById[descriptor.AnchorId] = anchorGameObject.transform;
            return Task.FromResult(descriptor.AnchorId);
        }

        public Transform GetTransform(string anchorId)
        {
            _anchorTransformsById.TryGetValue(anchorId, out var anchorTransform);
            return anchorTransform;
        }
    }
}
