using System;
using System.Collections.Generic;
using System.Linq;
using AmpPortableDataViz.Application;
using AmpPortableDataViz.Core;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Visualization
{
    /// <summary>
    /// Bridges sensor selection to persistent visualizer bindings.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Amp Portable Data Viz/Visualization/Sensor Stream Coordinator")]
    public sealed class SensorStreamCoordinator : MonoBehaviour
    {
        [Header("Persistent Visualization Targets")]
        [SerializeField] private AudienceSignalVisualizerBinding[] visualizerBindings =
            Array.Empty<AudienceSignalVisualizerBinding>();

        [Header("Diagnostics")]
        [SerializeField] private bool logSelectionChanges = true;

        private SensorStreamSelectionController _selection;
        private SelectedSensorDataSource<AudienceSignalSample> _selectedSource;

        public int SensorCount => _selection?.SensorCount ?? 0;
        public int ActiveSensorIndex => _selection?.ActiveIndex ?? -1;
        public string ActiveSensorId => _selection?.ActiveSensorId ?? string.Empty;
        public bool HasActiveSensor => _selection != null && _selection.HasActiveSensor;
        public ILatestDataSource<AudienceSignalSample> SelectedSource => _selectedSource;

        public event Action<SensorStreamSelectionChanged> SelectionChanged;

        private void Awake()
        {
            EnsureState();
            BindVisualizerTargets();
        }

        private void OnDestroy()
        {
            if (_selection != null)
            {
                _selection.SelectionChanged -= OnSelectionChanged;
            }

            _selectedSource?.Dispose();
            _selectedSource = null;
            _selection = null;
        }

        public void ConfigureSources(IEnumerable<AudienceSignalBinding> sources)
        {
            ConfigureSources(sources?.Cast<IDataSource<AudienceSignalSample>>());
        }

        public void ConfigureSources(IEnumerable<IDataSource<AudienceSignalSample>> sources)
        {
            EnsureState();
            _selectedSource.ReplaceSources(sources);
            BindVisualizerTargets();
        }

        public void ConfigureVisualizerBindings(IEnumerable<AudienceSignalVisualizerBinding> bindings)
        {
            visualizerBindings = bindings == null
                ? Array.Empty<AudienceSignalVisualizerBinding>()
                : bindings.Where(binding => binding != null).Distinct().ToArray();

            BindVisualizerTargets();
        }

        public bool SelectNextSensor()
        {
            EnsureState();
            return _selection.SelectNext();
        }

        public bool TrySelectSensor(string sensorId)
        {
            EnsureState();
            return _selection.TrySelect(sensorId);
        }

        private void EnsureState()
        {
            if (_selection != null && _selectedSource != null)
            {
                return;
            }

            _selection = new SensorStreamSelectionController();
            _selectedSource = new SelectedSensorDataSource<AudienceSignalSample>(_selection);
            _selection.SelectionChanged += OnSelectionChanged;
        }

        private void BindVisualizerTargets()
        {
            if (_selectedSource == null || visualizerBindings == null)
            {
                return;
            }

            foreach (AudienceSignalVisualizerBinding binding in visualizerBindings)
            {
                if (binding != null)
                {
                    binding.ConfigureLiveSource(_selectedSource);
                }
            }
        }

        private void OnSelectionChanged(SensorStreamSelectionChanged change)
        {
            if (logSelectionChanges)
            {
                string label = change.HasActiveSensor
                    ? $"{change.ActiveSensorId} ({change.ActiveIndex + 1}/{change.SensorCount})"
                    : "no available sensor";
                Debug.Log($"SensorStreamCoordinator: Active stream is {label}.", this);
            }

            SelectionChanged?.Invoke(change);
        }
    }
}
