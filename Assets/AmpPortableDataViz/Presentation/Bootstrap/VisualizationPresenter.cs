using System;
using System.Collections.Generic;
using System.Linq;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Presentation.Interaction;
using AmpPortableDataViz.Presentation.Sources;
using AmpPortableDataViz.Presentation.Visualization;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Bootstrap
{
    /// <summary>Creates, binds and owns automatic and manual visualization instances.</summary>
    internal sealed class VisualizationPresenter
    {
        private readonly GameObject _owner;
        private readonly RedisStreamRegistry _streams;
        private Settings _settings;
        private readonly VisualizationLayout _layout;

        internal VisualizationPresenter(GameObject owner, RedisStreamRegistry streams, VisualizationLayout layout)
        {
            _owner = owner;
            _streams = streams;
            _layout = layout;
        }

        internal void Configure(Settings settings)
        {
            _settings = settings;
        }

        private readonly List<AudienceDeviceInstance> _audienceDeviceInstances = new List<AudienceDeviceInstance>();
        private readonly List<AudienceGraphDeviceGroupInstance> _audienceGraphDeviceGroups = new List<AudienceGraphDeviceGroupInstance>();
        private readonly List<SelectedSensorSourceInstance> _selectedSensorSources = new List<SelectedSensorSourceInstance>();
        private readonly List<GameObject> _manualDriverInstances = new List<GameObject>();
        private SensorStreamCoordinator _sensorStreamCoordinator;
        private RayNeoTempleSensorStreamCycler _sensorStreamCycler;
        private GameObject _selectedParticleVisual;
        private GameObject _selectedGraphVisual;
        private GameObject _selectedArtistVisual;
        private GameObject _sensorHudObject;

        internal struct Settings
        {
            public GameObject AbstractVizPrefab;
            public Transform AbstractVizParent;
            public GameObject GraphPrefab;
            public Transform GraphGroupParent;
            public GameObject ArtistVizPrefab;
            public Transform ArtistVizParent;
            public bool AutoSpawnAbstractViz;
            public bool AutoSpawnGraphVisualsFromChannels;
            public string[] SensorIds;
        }

        internal void EnsureManualDriverVisuals()
        {
            if (_manualDriverInstances.Count > 0)
            {
                return;
            }

            SpawnManualDriverPrefab(_settings.AbstractVizPrefab, _settings.AbstractVizParent, "ManualVisual", 0, isGraphPrefab: false);
            SpawnManualDriverPrefab(_settings.GraphPrefab, _settings.GraphGroupParent, "ManualGraph", 0, isGraphPrefab: true);
        }

        private void SpawnManualDriverPrefab(GameObject prefab, Transform parent, string suffix, int index, bool isGraphPrefab)
        {
            if (prefab == null)
            {
                return;
            }

            bool hasManualDriver =
                prefab.GetComponentInChildren<SensorDataSimulator>(true) != null ||
                prefab.GetComponentInChildren<ParticleMeshManualDriver>(true) != null ||
                prefab.GetComponentInChildren<GraphManualDriver>(true) != null;

            if (!hasManualDriver)
            {
                return;
            }

            bool hasCustomParent = parent != null;
            var instance = hasCustomParent
                ? UnityEngine.Object.Instantiate(prefab, parent)
                : UnityEngine.Object.Instantiate(prefab);

            instance.name = $"{prefab.name}_{suffix}";
            if (isGraphPrefab)
            {
                _layout.PositionGraphGroup(instance.transform, index, hasCustomParent);
            }
            else
            {
                _layout.PositionAbstractViz(instance.transform, index, hasCustomParent);
            }

            ConfigureManualDriverInstance(instance);
            _manualDriverInstances.Add(instance);
        }

        private static void ConfigureManualDriverInstance(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            var audienceBindings = instance.GetComponentsInChildren<SensorSampleAssembler>(true);
            foreach (var binding in audienceBindings)
            {
                binding.enabled = false;
            }

            var audienceManualDrivers = instance.GetComponentsInChildren<SensorDataSimulator>(true);
            bool hasAudienceManualDriver = audienceManualDrivers.Length > 0;
            foreach (var manualDriver in audienceManualDrivers)
            {
                manualDriver.enabled = true;
            }

            var audienceVisualizerBindings = instance.GetComponentsInChildren<SensorVisualizationBinding>(true);
            foreach (var visualizerBinding in audienceVisualizerBindings)
            {
                if (hasAudienceManualDriver)
                {
                    visualizerBinding.ConfigureManualSource(audienceManualDrivers[0]);
                }

                var particleMeshVisualizer = visualizerBinding.GetComponentInChildren<ParticleMeshVisualizer>();
                if (particleMeshVisualizer == null)
                {
                    particleMeshVisualizer = instance.GetComponentInChildren<ParticleMeshVisualizer>(true);
                }

                if (particleMeshVisualizer != null)
                {
                    visualizerBinding.ConfigureParticleTarget(particleMeshVisualizer);
                }

                visualizerBinding.enabled = true;
            }

            var particleManualDrivers = instance.GetComponentsInChildren<ParticleMeshManualDriver>(true);
            foreach (var manualDriver in particleManualDrivers)
            {
                manualDriver.enabled = !hasAudienceManualDriver;
            }

            var graphManualDrivers = instance.GetComponentsInChildren<GraphManualDriver>(true);
            foreach (var manualDriver in graphManualDrivers)
            {
                manualDriver.enabled = !hasAudienceManualDriver;
            }
        }

        internal void EnsureSelectedSensorStreamMode(
            string[] redisChannels,
            bool spawnParticleVisual,
            bool spawnGraphVisual,
            bool spawnArtistVisual)
        {
            ClearSelectedSensorStreamMode();

            var deviceDefinitions = RedisStreamRegistry.BuildAudienceDeviceChannelDefinitions(redisChannels);
            deviceDefinitions = RedisStreamRegistry.FilterAudienceToConfiguredDevices(deviceDefinitions, _settings.SensorIds);
            if (deviceDefinitions.Count == 0)
            {
                Debug.LogWarning("Bootstrapper: No complete sensor streams are available for selected-stream mode.");
                EnsureSensorStreamPresentation(Array.Empty<SensorSampleAssembler>(), Array.Empty<SensorVisualizationBinding>());
                return;
            }

            foreach (var definition in deviceDefinitions)
            {
                if (!definition.HasVisualizationChannels)
                {
                    Debug.LogWarning($"Bootstrapper: Incomplete audience channel set for device '{definition.DeviceId}', skipping selectable stream.");
                    continue;
                }

                var sourceObject = new GameObject($"SensorStreamSource_{definition.DeviceId}");
                sourceObject.transform.SetParent(_owner.transform, false);
                sourceObject.SetActive(false);

                var binding = sourceObject.AddComponent<SensorSampleAssembler>();
                var physioPump = _streams.AcquirePhysioPumpForChannel(definition.DeviceId, definition.PhysioMetricsChannel);
                var engagementSource = _streams.ResolveSensorEngagementSource(definition.DeviceId);

                if (physioPump == null || engagementSource == null)
                {
                    Debug.LogWarning($"Bootstrapper: Unable to create selectable stream for '{definition.DeviceId}'.");
                    UnityEngine.Object.Destroy(sourceObject);
                    _streams.ReleasePhysioPump(definition.PhysioMetricsChannel);
                    continue;
                }

                binding.ConfigureSensorSources(
                    physioPump,
                    engagementSource,
                    definition.DeviceId);
                binding.ConfigureParticleTarget(null);
                sourceObject.SetActive(true);

                _selectedSensorSources.Add(new SelectedSensorSourceInstance
                {
                    SourceObject = sourceObject,
                    Binding = binding,
                    PhysioMetricsChannel = definition.PhysioMetricsChannel
                });
            }

            if (_selectedSensorSources.Count == 0)
            {
                Debug.LogWarning("Bootstrapper: No complete sensor streams could be configured.");
                EnsureSensorStreamPresentation(Array.Empty<SensorSampleAssembler>(), Array.Empty<SensorVisualizationBinding>());
                return;
            }

            if (spawnParticleVisual)
            {
                _selectedParticleVisual = SpawnSelectedSensorVisual(
                    _settings.AbstractVizPrefab,
                    _settings.AbstractVizParent,
                    "SelectedSensor",
                    isGraphVisual: false);
            }

            if (spawnGraphVisual)
            {
                _selectedGraphVisual = SpawnSelectedSensorVisual(
                    _settings.GraphPrefab,
                    _settings.GraphGroupParent,
                    "SelectedSensor",
                    isGraphVisual: true);
            }

            if (spawnArtistVisual)
            {
                _selectedArtistVisual = SpawnSelectedSensorVisual(
                    _settings.ArtistVizPrefab,
                    _settings.ArtistVizParent,
                    "SelectedSensor",
                    isGraphVisual: false,
                    positionAtParentOrigin: true);
            }

            var visualizerBindings = new List<SensorVisualizationBinding>();
            AddVisualizerBindings(_selectedParticleVisual, visualizerBindings);
            AddVisualizerBindings(_selectedGraphVisual, visualizerBindings);
            AddVisualizerBindings(_selectedArtistVisual, visualizerBindings);

            EnsureSensorStreamPresentation(
                _selectedSensorSources.Select(source => source.Binding),
                visualizerBindings);

            Debug.Log($"Bootstrapper: Configured one persistent visualization target for {_selectedSensorSources.Count} selectable sensor streams.");
        }

        internal GameObject SpawnSelectedSensorVisual(
            GameObject prefab,
            Transform parent,
            string suffix,
            bool isGraphVisual,
            bool positionAtParentOrigin = false)
        {
            if (prefab == null)
            {
                return null;
            }

            bool hasCustomParent = parent != null;
            var instance = hasCustomParent ? UnityEngine.Object.Instantiate(prefab, parent) : UnityEngine.Object.Instantiate(prefab);
            instance.name = $"{prefab.name}_{suffix}";

            if (positionAtParentOrigin)
            {
                _layout.PositionAtParentOrigin(instance.transform, hasCustomParent);
            }
            else if (isGraphVisual)
            {
                _layout.PositionGraphGroup(instance.transform, 0, hasCustomParent);
            }
            else
            {
                _layout.PositionAbstractViz(instance.transform, 0, hasCustomParent);
            }

            DisableManualDrivers(instance);

            foreach (var sourceBinding in instance.GetComponentsInChildren<SensorSampleAssembler>(true))
            {
                sourceBinding.enabled = false;
                sourceBinding.ConfigureParticleTarget(null);
            }

            var visualizerBindings = instance.GetComponentsInChildren<SensorVisualizationBinding>(true);
            if (visualizerBindings.Length == 0)
            {
                Debug.LogWarning($"Bootstrapper: Selected-stream visual '{instance.name}' has no SensorVisualizationBinding.");
                UnityEngine.Object.Destroy(instance);
                return null;
            }

            foreach (var visualizerBinding in visualizerBindings)
            {
                visualizerBinding.enabled = true;
            }

            return instance;
        }

        private void EnsureSensorStreamPresentation(
            IEnumerable<SensorSampleAssembler> sources,
            IEnumerable<SensorVisualizationBinding> visualizerBindings)
        {
            if (_sensorStreamCoordinator == null)
            {
                _sensorStreamCoordinator = _owner.GetComponent<SensorStreamCoordinator>();
                if (_sensorStreamCoordinator == null)
                {
                    _sensorStreamCoordinator = _owner.AddComponent<SensorStreamCoordinator>();
                }
            }

            _sensorStreamCoordinator.ConfigureVisualizerBindings(visualizerBindings);
            _sensorStreamCoordinator.ConfigureSources(sources);

            if (_sensorStreamCycler == null)
            {
                _sensorStreamCycler = _owner.GetComponent<RayNeoTempleSensorStreamCycler>();
                if (_sensorStreamCycler == null)
                {
                    _sensorStreamCycler = _owner.AddComponent<RayNeoTempleSensorStreamCycler>();
                }
            }

            _sensorStreamCycler.Configure(_sensorStreamCoordinator);
            EnsureSensorHud();
        }

        private void EnsureSensorHud()
        {
            if (_sensorHudObject != null || _sensorStreamCoordinator == null)
            {
                return;
            }

            _sensorHudObject = _layout.CreateSensorHud(_sensorStreamCoordinator);
        }

        internal static void AddVisualizerBindings(
            GameObject instance,
            ICollection<SensorVisualizationBinding> bindings)
        {
            if (instance == null || bindings == null)
            {
                return;
            }

            foreach (var binding in instance.GetComponentsInChildren<SensorVisualizationBinding>(true))
            {
                if (binding != null && !bindings.Contains(binding))
                {
                    bindings.Add(binding);
                }
            }
        }

        internal void EnsureAudienceDeviceVisuals(string[] redisChannels)
        {
            if (!_settings.AutoSpawnAbstractViz)
            {
                return;
            }

            if (_settings.AbstractVizPrefab == null)
            {
                Debug.LogWarning("Bootstrapper: AbstractViz Prefab is missing, cannot spawn audience signal instances.");
                ClearAudienceDeviceVisuals();
                return;
            }

            if (redisChannels == null || redisChannels.Length == 0)
            {
                ClearAudienceDeviceVisuals();
                return;
            }

            bool hasSampleAssembler = _settings.AbstractVizPrefab.GetComponentInChildren<SensorSampleAssembler>(true) != null;
            bool hasVisualizerBinding = _settings.AbstractVizPrefab.GetComponentInChildren<SensorVisualizationBinding>(true) != null;
            if (!hasSampleAssembler && !hasVisualizerBinding)
            {
                ClearAudienceDeviceVisuals();
                return;
            }

            var deviceDefinitions = RedisStreamRegistry.BuildAudienceDeviceChannelDefinitions(redisChannels);
            deviceDefinitions = RedisStreamRegistry.FilterAudienceToConfiguredDevices(deviceDefinitions, _settings.SensorIds);
            ClearAudienceDeviceVisuals();

            if (deviceDefinitions.Count == 0)
            {
                Debug.LogWarning("Bootstrapper: No device-specific audience signal channels were found.");
                return;
            }

            Debug.Log($"Bootstrapper: Spawning audience visuals for {deviceDefinitions.Count} devices: {string.Join(", ", deviceDefinitions.Select(d => d.DeviceId))}");

            bool hasCustomParent = _settings.AbstractVizParent != null;
            int deviceIndex = 0;
            foreach (var definition in deviceDefinitions)
            {
                if (!definition.HasVisualizationChannels)
                {
                    Debug.LogWarning($"Bootstrapper: Incomplete audience channel set for device '{definition.DeviceId}', skipping visual spawn.");
                    continue;
                }

                var instance = hasCustomParent
                    ? UnityEngine.Object.Instantiate(_settings.AbstractVizPrefab, _settings.AbstractVizParent)
                    : UnityEngine.Object.Instantiate(_settings.AbstractVizPrefab);

                instance.name = $"{_settings.AbstractVizPrefab.name}_{definition.DeviceId}";
                _layout.PositionAbstractViz(instance.transform, deviceIndex, hasCustomParent);

                DisableManualDrivers(instance);

                var binding = instance.GetComponentInChildren<SensorSampleAssembler>(true);
                bool reactivateAfterConfiguration = false;
                if (binding == null)
                {
                    // Display-only prefabs receive a source only when per-device mode needs it.
                    // Configure while inactive so OnEnable cannot subscribe to missing sources.
                    var visualizerBinding = instance.GetComponentInChildren<SensorVisualizationBinding>(true);
                    if (visualizerBinding == null)
                    {
                        Debug.LogWarning($"Bootstrapper: Audience visual '{instance.name}' has no sensor binding, skipping visual spawn.");
                        UnityEngine.Object.Destroy(instance);
                        continue;
                    }

                    reactivateAfterConfiguration = instance.activeSelf;
                    instance.SetActive(false);
                    binding = visualizerBinding.gameObject.AddComponent<SensorSampleAssembler>();
                }

                var particleMeshVisualizer = instance.GetComponentInChildren<ParticleMeshVisualizer>(true);
                var physioPump = _streams.AcquirePhysioPumpForChannel(definition.DeviceId, definition.PhysioMetricsChannel);
                var engagementSource = _streams.ResolveSensorEngagementSource(definition.DeviceId);

                if (!TryConfigureAudienceBinding(binding, physioPump, engagementSource, particleMeshVisualizer, definition.DeviceId))
                {
                    Debug.LogWarning($"Bootstrapper: Unable to configure audience binding on '{instance.name}', skipping visual spawn.");
                    UnityEngine.Object.Destroy(instance);
                    _streams.ReleasePhysioPump(definition.PhysioMetricsChannel);
                    continue;
                }

                if (reactivateAfterConfiguration)
                {
                    instance.SetActive(true);
                }

                _audienceDeviceInstances.Add(new AudienceDeviceInstance
                {
                    VisualInstance = instance,
                    Binding = binding,
                    PhysioMetricsChannel = definition.PhysioMetricsChannel
                });

                _layout.AttachDeviceLabel(instance, definition.DeviceId, deviceIndex);
                deviceIndex++;
            }

            if (deviceIndex == 0)
            {
                Debug.LogWarning("Bootstrapper: Unable to spawn audience visuals because no device had the required channels.");
            }
        }

        internal void EnsureAudienceGraphDeviceVisuals(string[] redisChannels)
        {
            if (!_settings.AutoSpawnGraphVisualsFromChannels)
            {
                return;
            }

            if (_settings.GraphPrefab == null)
            {
                Debug.LogWarning("Bootstrapper: Graph Prefab is missing, cannot spawn audience graph instances.");
                ClearAudienceGraphDeviceVisuals();
                return;
            }

            if (redisChannels == null || redisChannels.Length == 0)
            {
                ClearAudienceGraphDeviceVisuals();
                return;
            }

            bool hasVisualizerBinding = _settings.GraphPrefab.GetComponentInChildren<SensorVisualizationBinding>(true) != null;
            bool hasGraphVisualizer = _settings.GraphPrefab.GetComponentInChildren<GraphVisualizer>(true) != null;
            if (!hasVisualizerBinding || !hasGraphVisualizer)
            {
                Debug.LogWarning("Bootstrapper: Audience Graph Prefab must include SensorVisualizationBinding and at least one GraphVisualizer component. Audience graph metrics are configured on the prefab's SensorVisualizationBinding.");
                ClearAudienceGraphDeviceVisuals();
                return;
            }

            var deviceDefinitions = RedisStreamRegistry.BuildAudienceDeviceChannelDefinitions(redisChannels);
            deviceDefinitions = RedisStreamRegistry.FilterAudienceToConfiguredDevices(deviceDefinitions, _settings.SensorIds);
            ClearAudienceGraphDeviceVisuals();

            if (deviceDefinitions.Count == 0)
            {
                Debug.LogWarning("Bootstrapper: No device-specific audience channels were found for graphs.");
                return;
            }

            Debug.Log($"Bootstrapper: Spawning audience graph groups for {deviceDefinitions.Count} devices: {string.Join(", ", deviceDefinitions.Select(d => d.DeviceId))}");

            bool hasCustomParent = _settings.GraphGroupParent != null;
            int deviceIndex = 0;
            foreach (var definition in deviceDefinitions)
            {
                if (!definition.HasVisualizationChannels)
                {
                    Debug.LogWarning($"Bootstrapper: Incomplete audience channel set for device '{definition.DeviceId}', skipping graph spawn.");
                    continue;
                }

                var instance = hasCustomParent
                    ? UnityEngine.Object.Instantiate(_settings.GraphPrefab, _settings.GraphGroupParent)
                    : UnityEngine.Object.Instantiate(_settings.GraphPrefab);

                instance.name = $"{_settings.GraphPrefab.name}_{definition.DeviceId}";
                _layout.PositionGraphGroup(instance.transform, deviceIndex, hasCustomParent);
                DisableManualDrivers(instance);

                var binding = instance.GetComponentInChildren<SensorSampleAssembler>(true);
                bool reactivateAfterConfiguration = false;
                if (binding == null)
                {
                    var visualizerBinding = instance.GetComponentInChildren<SensorVisualizationBinding>(true);
                    if (visualizerBinding == null)
                    {
                        Debug.LogWarning($"Bootstrapper: Audience graph '{instance.name}' has no visualization binding, skipping graph spawn.");
                        UnityEngine.Object.Destroy(instance);
                        continue;
                    }

                    // Configure the per-device source before OnEnable subscribes to its streams.
                    reactivateAfterConfiguration = instance.activeSelf;
                    instance.SetActive(false);
                    binding = visualizerBinding.gameObject.AddComponent<SensorSampleAssembler>();
                }

                var physioPump = _streams.AcquirePhysioPumpForChannel(definition.DeviceId, definition.PhysioMetricsChannel);
                var engagementSource = _streams.ResolveSensorEngagementSource(definition.DeviceId);

                if (!TryConfigureAudienceBinding(binding, physioPump, engagementSource, null, definition.DeviceId))
                {
                    Debug.LogWarning($"Bootstrapper: Unable to configure audience graph binding on '{instance.name}', skipping graph spawn.");
                    UnityEngine.Object.Destroy(instance);
                    _streams.ReleasePhysioPump(definition.PhysioMetricsChannel);
                    continue;
                }

                if (reactivateAfterConfiguration)
                {
                    instance.SetActive(true);
                }

                _audienceGraphDeviceGroups.Add(new AudienceGraphDeviceGroupInstance
                {
                    GroupRoot = instance,
                    Binding = binding,
                    PhysioMetricsChannel = definition.PhysioMetricsChannel
                });

                _layout.AttachGraphGroupLabel(instance, definition.DeviceId, deviceIndex);
                deviceIndex++;
            }
        }

        private static bool TryConfigureAudienceBinding(
            SensorSampleAssembler binding,
            RedisPhysioMetricsPump physioPump,
            ILatestDataSource<SensorEngagementState> engagementSource,
            ParticleMeshVisualizer particleMeshVisualizer,
            string deviceId)
        {
            if (binding == null || physioPump == null || engagementSource == null)
            {
                return false;
            }

            binding.ConfigureSensorSources(physioPump, engagementSource, deviceId);
            if (particleMeshVisualizer != null)
            {
                binding.ConfigureParticleTarget(particleMeshVisualizer);
            }

            var visualizerBinding = binding.GetComponentInChildren<SensorVisualizationBinding>();
            if (visualizerBinding == null && binding.gameObject != null)
            {
                visualizerBinding = binding.gameObject.GetComponentInParent<SensorVisualizationBinding>();
            }

            if (visualizerBinding != null)
            {
                visualizerBinding.ConfigureLiveSource(binding, deviceId);
                if (particleMeshVisualizer != null)
                {
                    visualizerBinding.ConfigureParticleTarget(particleMeshVisualizer);
                }

                visualizerBinding.enabled = true;
            }

            binding.enabled = true;
            return true;
        }

        private static void DisableManualDrivers(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            var audienceManualDrivers = instance.GetComponentsInChildren<SensorDataSimulator>(true);
            foreach (var manualDriver in audienceManualDrivers)
            {
                manualDriver.enabled = false;
            }

            var particleManualDrivers = instance.GetComponentsInChildren<ParticleMeshManualDriver>(true);
            foreach (var manualDriver in particleManualDrivers)
            {
                manualDriver.enabled = false;
            }

            var graphManualDrivers = instance.GetComponentsInChildren<GraphManualDriver>(true);
            foreach (var manualDriver in graphManualDrivers)
            {
                manualDriver.enabled = false;
            }
        }

        internal void ClearAudienceDeviceVisuals()
        {
            if (_audienceDeviceInstances.Count == 0)
            {
                return;
            }

            foreach (var instance in _audienceDeviceInstances)
            {
                if (instance.VisualInstance != null)
                {
                    UnityEngine.Object.Destroy(instance.VisualInstance);
                }
                else if (instance.Binding != null)
                {
                    UnityEngine.Object.Destroy(instance.Binding.gameObject);
                }

                _streams.ReleasePhysioPump(instance.PhysioMetricsChannel);
            }

            _audienceDeviceInstances.Clear();
        }

        internal void ClearAudienceGraphDeviceVisuals()
        {
            if (_audienceGraphDeviceGroups.Count == 0)
            {
                return;
            }

            foreach (var instance in _audienceGraphDeviceGroups)
            {
                if (instance.GroupRoot != null)
                {
                    UnityEngine.Object.Destroy(instance.GroupRoot);
                }
                else if (instance.Binding != null)
                {
                    UnityEngine.Object.Destroy(instance.Binding.gameObject);
                }

                _streams.ReleasePhysioPump(instance.PhysioMetricsChannel);
            }

            _audienceGraphDeviceGroups.Clear();
        }

        internal void ClearSelectedSensorStreamMode()
        {
            if (_sensorStreamCoordinator != null)
            {
                _sensorStreamCoordinator.ConfigureVisualizerBindings(Array.Empty<SensorVisualizationBinding>());
                _sensorStreamCoordinator.ConfigureSources(Array.Empty<SensorSampleAssembler>());
            }

            foreach (var source in _selectedSensorSources)
            {
                if (source.SourceObject != null)
                {
                    UnityEngine.Object.Destroy(source.SourceObject);
                }
                else if (source.Binding != null)
                {
                    UnityEngine.Object.Destroy(source.Binding.gameObject);
                }

                _streams.ReleasePhysioPump(source.PhysioMetricsChannel);
            }

            _selectedSensorSources.Clear();

            if (_selectedParticleVisual != null)
            {
                UnityEngine.Object.Destroy(_selectedParticleVisual);
                _selectedParticleVisual = null;
            }

            if (_selectedGraphVisual != null)
            {
                UnityEngine.Object.Destroy(_selectedGraphVisual);
                _selectedGraphVisual = null;
            }

            if (_selectedArtistVisual != null)
            {
                UnityEngine.Object.Destroy(_selectedArtistVisual);
                _selectedArtistVisual = null;
            }

            if (_sensorHudObject != null)
            {
                UnityEngine.Object.Destroy(_sensorHudObject);
                _sensorHudObject = null;
            }
        }

        internal void ClearManualDriverVisuals()
        {
            if (_manualDriverInstances.Count == 0)
            {
                return;
            }

            foreach (var instance in _manualDriverInstances)
            {
                if (instance != null)
                {
                    UnityEngine.Object.Destroy(instance);
                }
            }

            _manualDriverInstances.Clear();
        }

        private sealed class AudienceDeviceInstance
        {
            public GameObject VisualInstance;
            public SensorSampleAssembler Binding;
            public string PhysioMetricsChannel;
        }

        private sealed class AudienceGraphDeviceGroupInstance
        {
            public GameObject GroupRoot;
            public SensorSampleAssembler Binding;
            public string PhysioMetricsChannel;
        }

        private sealed class SelectedSensorSourceInstance
        {
            public GameObject SourceObject;
            public SensorSampleAssembler Binding;
            public string PhysioMetricsChannel;
        }
    }
}
