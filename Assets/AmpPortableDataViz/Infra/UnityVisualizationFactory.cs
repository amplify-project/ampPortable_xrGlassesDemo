using System;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Presentation.Visualization;
using UnityEngine;

namespace AmpPortableDataViz.Infra
{
    public sealed class UnityVisualizationFactory : IVisualizationFactory
    {
        private readonly GameObject _visualPrefab;

        public UnityVisualizationFactory(GameObject visualPrefab)
        {
            _visualPrefab = visualPrefab;
        }

        public IVisualizationInstance Create(VisualizationSpawnRequest request)
        {
            if (_visualPrefab == null)
            {
                Debug.LogWarning("UnityVisualizationFactory: visual prefab is not assigned.");
                return null;
            }

            var instanceGo = UnityEngine.Object.Instantiate(_visualPrefab);
            instanceGo.name = $"Viz_{request.VisualId}";

            return new UnityVisualizationInstance(request.VisualId, instanceGo);
        }
    }

    internal sealed class UnityVisualizationInstance : IVisualizationInstance
    {
        private readonly GameObject _gameObject;
        private readonly SimpleVisualizer _visualizer;
        private readonly AnchoredVisualization _anchoredVisualization;
        private Transform _visualRoot;

        public string VisualId { get; }

        public UnityVisualizationInstance(string visualId, GameObject gameObject)
        {
            VisualId = visualId ?? throw new ArgumentNullException(nameof(visualId));
            _gameObject = gameObject ?? throw new ArgumentNullException(nameof(gameObject));

            _visualizer = _gameObject.GetComponentInChildren<SimpleVisualizer>();
            _anchoredVisualization = _gameObject.GetComponent<AnchoredVisualization>();

            _visualRoot = _anchoredVisualization?.VisualRoot != null
                ? _anchoredVisualization.VisualRoot
                : _gameObject.transform;

            if (_visualizer == null)
            {
                Debug.LogWarning($"UnityVisualizationInstance: SimpleVisualizer component not found for visual '{visualId}'.");
            }
        }

        public void BindToAnchor(string anchorId, Transform anchorTransform)
        {
            if (anchorTransform == null)
            {
                Debug.LogWarning($"UnityVisualizationInstance: anchor transform is null for visual '{VisualId}'.");
                return;
            }

            if (_anchoredVisualization != null)
            {
                _anchoredVisualization.AnchorId = anchorId;
                _anchoredVisualization.BindToAnchor(anchorTransform);
                _visualRoot = _anchoredVisualization.VisualRoot != null
                    ? _anchoredVisualization.VisualRoot
                    : _anchoredVisualization.transform;
            }
            else
            {
                var transform = _gameObject.transform;
                transform.SetParent(anchorTransform, worldPositionStays: false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                _visualRoot = transform;
            }
        }

        public void Apply(SimpleVisualParams parameters, long timestampTicksUtc)
        {
            _visualizer?.Apply(parameters, timestampTicksUtc);
        }

        public void ApplyPose(in PoseSample pose)
        {
            if (_visualRoot == null) return;
            _visualRoot.localPosition = pose.Position;
            _visualRoot.localRotation = pose.Rotation;
        }
    }
}

