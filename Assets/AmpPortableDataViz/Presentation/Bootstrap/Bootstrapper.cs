using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmpPortableDataViz.Application;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Infra;
using AmpPortableDataViz.Presentation.Interaction;
using AmpPortableDataViz.Presentation.Sources;
using AmpPortableDataViz.Presentation.Visualization;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace AmpPortableDataViz.Presentation.Bootstrap
{
    /// <summary>
    /// Coordinates Redis sensor streams and visualization modes.
    /// </summary>
    public sealed class Bootstrapper : MonoBehaviour
    {
        [Header("Scene Dependencies")]
        [FormerlySerializedAs("VisualPrefab")]
        [InspectorName("AbstractViz Prefab")]
        public GameObject AbstractVizPrefab;

        [Header("Manual Visualization Testing")]
        public bool UseManualVisualizationDrivers;

        [Header("Redis Settings")]
        public string RedisHost = "192.168.0.6";
        public int RedisPort = 6379;
        public string RedisChannel = RedisEmotionChannels.BroadcastChannel;
        public string[] RedisChannels = Array.Empty<string>();
        [Header("Redis Prompt")]
        public bool RequireRedisEndpointConfirmation = true;

        [Header("Sensor Discovery")]
        [FormerlySerializedAs("IncludeEmotionChannels")]
        public bool IncludeSensorChannels = true;
        [FormerlySerializedAs("EmotionDeviceIds")]
        public string[] SensorIds = RedisEmotionChannels.GetDefaultDeviceIds();
        [FormerlySerializedAs("AutoDetectEmotionDeviceIds")]
        public bool AutoDetectSensorIds = true;
        public int RedisDiscoveryTimeoutMs = 2000;

        [Header("AbstractViz Layout")]
        [FormerlySerializedAs("EmotionVisualParent")]
        public Transform AbstractVizParent;
        [FormerlySerializedAs("EmotionVisualOriginOffset")]
        public Vector3 AbstractVizOriginOffset = Vector3.zero;
        [FormerlySerializedAs("EmotionVisualSpacing")]
        public Vector3 AbstractVizSpacing = new Vector3(0.75f, 0f, 0.75f);
        [Min(1)]
        [FormerlySerializedAs("EmotionVisualsPerRow")]
        public int AbstractVizPerRow = 2;
        [Header("Sensor Labels")]
        public bool ShowDeviceLabels = true;
        public Vector3 DeviceLabelOffset = new Vector3(0f, -0.35f, 0f);
        public float DeviceLabelFontSize = 0.22f;
        public Color DeviceLabelColor = Color.white;

        [Header("AbstractViz Settings")]
        [FormerlySerializedAs("AutoSpawnAudienceVisualsFromChannels")]
        [InspectorName("Auto Spawn AbstractViz")]
        public bool AutoSpawnAbstractViz = true;

        [Header("Artist Visual Settings")]
        public bool AutoSpawnArtistVizFromChannels = true;
        public GameObject ArtistVizPrefab;
        public Transform ArtistVizParent;

        [Header("Individual Sensor Engagement")]
        public bool UseIndividualSensorEngagement = true;
        [Tooltip("Use {serial}, {sensorId}, or {0} where the sensor serial belongs in the Redis channel name.")]
        public string SensorEngagementChannelTemplate = RedisAudienceChannels.SensorEngagementTemplate;
        public SensorEngagementChannelOverride[] SensorEngagementChannelOverrides =
            Array.Empty<SensorEngagementChannelOverride>();
        [Min(0f)]
        public float SensorEngagementTimeoutSeconds = 10f;
        [Range(0f, 1f)]
        public float SensorEngagementNeutralValue = 0.5f;
        public SensorEngagementJsonFormat SensorEngagementPayloadFormat =
            SensorEngagementJsonFormat.CreateDefault();

        [Header("RayNeo Selected Sensor Stream")]
        public bool UseSingleSelectedSensorStream = true;
        public Camera SensorHudCamera;
        public Vector3 SensorHudLocalPosition = new Vector3(0f, -0.22f, 1.2f);
        [Min(0.0001f)]
        public float SensorHudLocalScale = 0.01f;
        [Min(0.01f)]
        public float SensorHudFontSize = 2f;
        public Color SensorHudColor = Color.white;

        [Header("Graph Visual Settings")]
        public bool AutoSpawnGraphVisualsFromChannels = true;
        public GameObject GraphPrefab;
        public Transform GraphGroupParent;
        public Vector3 GraphGroupOriginOffset = Vector3.zero;
        public Vector3 GraphGroupSpacing = new Vector3(1.6f, 0f, 0.75f);
        [Min(1)]
        public int GraphGroupsPerRow = 2;

        [Header("Graph Group Labels")]
        public bool ShowGraphGroupLabels = true;
        public Vector3 GraphGroupLabelOffset = Vector3.zero;
        [Min(0f)]
        public float GraphGroupLabelVerticalOffset = 0.2f;
        public float GraphGroupLabelFontSize = 0.24f;
        public Color GraphGroupLabelColor = Color.white;

        [Header("Graph Group Layout")]
        public Vector3 GraphItemBoundsSize = new Vector3(1f, 1f, 0.02f);
        public Vector3 GraphGroupBoundsPadding = new Vector3(0.05f, 0.05f, 0.05f);

        [Header("Visualization Spawn Placement")]
        [FormerlySerializedAs("AnchorPosition")]
        [Tooltip("World-space spawn position used when no visualization parent is assigned.")]
        public Vector3 SpawnPosition = new Vector3(0f, 1.2f, 2f);
        [FormerlySerializedAs("AnchorRotationEuler")]
        [Tooltip("World-space spawn rotation in degrees used when no visualization parent is assigned.")]
        public Vector3 SpawnRotationEuler = Vector3.zero;

        private Coroutine _deviceDiscoveryRoutine;
        private readonly List<AudienceDeviceInstance> _audienceDeviceInstances = new List<AudienceDeviceInstance>();
        private readonly List<AudienceGraphDeviceGroupInstance> _audienceGraphDeviceGroups = new List<AudienceGraphDeviceGroupInstance>();
        private readonly List<SelectedSensorSourceInstance> _selectedSensorSources = new List<SelectedSensorSourceInstance>();
        private readonly List<GameObject> _manualDriverInstances = new List<GameObject>();
        private readonly Dictionary<string, SharedPhysioPumpHandle> _sharedPhysioPumpsByChannel = new Dictionary<string, SharedPhysioPumpHandle>(StringComparer.Ordinal);
        private GameObject _sensorEngagementBackendObject;
        private RedisSensorEngagementPump _sensorEngagementPump;
        private SensorEngagementCoordinator _sensorEngagementCoordinator;
        private string _sensorEngagementConfigurationKey = string.Empty;
        private bool _redisEndpointReady;
        private SensorStreamCoordinator _sensorStreamCoordinator;
        private RayNeoTempleSensorStreamCycler _sensorStreamCycler;
        private GameObject _selectedParticleVisual;
        private GameObject _selectedGraphVisual;
        private GameObject _selectedArtistVisual;
        private GameObject _sensorHudObject;

        private void Awake()
        {
            _redisEndpointReady = !RequireRedisEndpointConfirmation;
            ApplySignalConfiguration();
        }

        private void OnEnable()
        {
            if (ShouldAutoDetectSensorIds())
            {
                StartDeviceDiscovery();
                return;
            }

            ApplySignalConfiguration();
        }

        public void BeginAfterEndpoint()
        {
            Debug.Log("Bootstrapper: BeginAfterEndpoint invoked from Redis endpoint prompt.");

            var settings = RedisRuntimeSettings.Load();
            RedisHost = settings.Host;
            RedisPort = settings.Port;
            _redisEndpointReady = true;

            Debug.Log($"Bootstrapper: Redis endpoint confirmed -> {RedisHost}:{RedisPort}");

            StopRoutine(ref _deviceDiscoveryRoutine);

            if (ShouldAutoDetectSensorIds())
            {
                Debug.Log("Bootstrapper: Auto-detecting Redis sensor IDs after endpoint confirmation.");
                StartDeviceDiscovery();
                return;
            }

            Debug.Log("Bootstrapper: Applying signal configuration after endpoint confirmation.");
            ApplySignalConfiguration();
        }

        private void OnDisable()
        {
            StopRoutine(ref _deviceDiscoveryRoutine);
            ClearRuntimeVisuals();
        }

        private void OnDestroy()
        {
            StopRoutine(ref _deviceDiscoveryRoutine);
            ClearRuntimeVisuals();
        }

        private void StopRoutine(ref Coroutine routine)
        {
            if (routine == null) return;
            StopCoroutine(routine);
            routine = null;
        }

        private void ClearAutomaticVisuals()
        {
            ClearAudienceDeviceVisuals();
            ClearAudienceGraphDeviceVisuals();
            ClearSelectedSensorStreamMode();
            ClearSensorEngagementBackend();
        }

        private void ClearRuntimeVisuals()
        {
            ClearAutomaticVisuals();
            ClearManualDriverVisuals();
        }

        private void ApplySignalConfiguration()
        {
            if (UseManualVisualizationDrivers)
            {
                ApplyManualDriverConfiguration();
                return;
            }

            EnsureRedisEndpointFromRuntime();

            ClearManualDriverVisuals();

            bool hasRedisEndpoint = !string.IsNullOrWhiteSpace(RedisHost);

            var redisChannels = GetRedisChannels();

            if (!hasRedisEndpoint)
            {
                Debug.LogWarning("Bootstrapper: Redis host is empty; skipping Redis configuration. Enter a host via the endpoint prompt.");
                ClearRuntimeVisuals();
            }

            if (RequireRedisEndpointConfirmation && !_redisEndpointReady)
            {
                Debug.Log("Bootstrapper: Waiting for Redis endpoint confirmation before connecting.");
                ClearRuntimeVisuals();
            }

            bool redisReady = !RequireRedisEndpointConfirmation || _redisEndpointReady;

            bool visualPrefabHasAudienceBinding = AbstractVizPrefab != null && AbstractVizPrefab.GetComponentInChildren<AudienceSignalBinding>() != null;
            bool shouldSpawnAudienceVisuals = hasRedisEndpoint && redisReady && AutoSpawnAbstractViz && visualPrefabHasAudienceBinding;
            bool graphPrefabHasAudienceBinding = GraphPrefab != null && GraphPrefab.GetComponentInChildren<AudienceSignalBinding>() != null;
            bool shouldSpawnAudienceGraphVisuals = hasRedisEndpoint && redisReady && AutoSpawnGraphVisualsFromChannels && graphPrefabHasAudienceBinding;
            bool artistPrefabHasVisualizerBinding = ArtistVizPrefab != null &&
                ArtistVizPrefab.GetComponentInChildren<AudienceSignalVisualizerBinding>(true) != null &&
                ArtistVizPrefab.GetComponentInChildren<ArtistVizVisualizer>(true) != null;
            bool shouldSpawnArtistViz = hasRedisEndpoint && redisReady &&
                UseSingleSelectedSensorStream && AutoSpawnArtistVizFromChannels && artistPrefabHasVisualizerBinding;

            if (shouldSpawnAudienceVisuals || shouldSpawnAudienceGraphVisuals || shouldSpawnArtistViz)
            {
                var sensorDefinitions = BuildAudienceDeviceChannelDefinitions(redisChannels);
                sensorDefinitions = FilterAudienceToConfiguredDevices(sensorDefinitions, SensorIds);
                EnsureSensorEngagementBackend(sensorDefinitions.Select(definition => definition.DeviceId));
            }
            else
            {
                ClearSensorEngagementBackend();
            }

            bool shouldUseSelectedSensorStream = UseSingleSelectedSensorStream &&
                (shouldSpawnAudienceVisuals || shouldSpawnAudienceGraphVisuals || shouldSpawnArtistViz);
            if (shouldUseSelectedSensorStream)
            {
                ClearAudienceDeviceVisuals();
                ClearAudienceGraphDeviceVisuals();
                EnsureSelectedSensorStreamMode(
                    redisChannels,
                    shouldSpawnAudienceVisuals,
                    shouldSpawnAudienceGraphVisuals,
                    shouldSpawnArtistViz);
                return;
            }

            ClearSelectedSensorStreamMode();

            if (shouldSpawnAudienceVisuals)
            {
                EnsureAudienceDeviceVisuals(redisChannels);
            }
            else
            {
                ClearAudienceDeviceVisuals();
            }

            if (shouldSpawnAudienceGraphVisuals)
            {
                EnsureAudienceGraphDeviceVisuals(redisChannels);
            }
            else
            {
                ClearAudienceGraphDeviceVisuals();
            }
        }

        private void EnsureRedisEndpointFromRuntime()
        {
            if (!string.IsNullOrWhiteSpace(RedisHost))
            {
                return;
            }

            var runtimeSettings = RedisRuntimeSettings.Load();
            if (!string.IsNullOrWhiteSpace(runtimeSettings.Host))
            {
                RedisHost = runtimeSettings.Host;
                RedisPort = runtimeSettings.Port;
                Debug.Log($"Bootstrapper: Loaded Redis endpoint from runtime settings -> {RedisHost}:{RedisPort}");
            }
        }

        private bool ShouldAutoDetectSensorIds()
        {
            if (UseManualVisualizationDrivers)
            {
                return false;
            }

            if (!AutoDetectSensorIds || !IncludeSensorChannels)
            {
                return false;
            }

            EnsureRedisEndpointFromRuntime();

            if (string.IsNullOrWhiteSpace(RedisHost))
            {
                return false;
            }

            if (RequireRedisEndpointConfirmation && !_redisEndpointReady)
            {
                return false;
            }

            return true;
        }

        private void StartDeviceDiscovery()
        {
            StopRoutine(ref _deviceDiscoveryRoutine);
            _deviceDiscoveryRoutine = StartCoroutine(DiscoverSensorsThenApply());
        }

        private IEnumerator DiscoverSensorsThenApply()
        {
            EnsureRedisEndpointFromRuntime();

            var discoveryTask = RedisDeviceDiscovery.DiscoverDeviceIdsAsync(RedisHost, RedisPort, RedisDiscoveryTimeoutMs);
            while (!discoveryTask.IsCompleted)
            {
                yield return null;
            }

            if (discoveryTask.IsFaulted)
            {
                Debug.LogWarning($"Bootstrapper: Device discovery failed for {RedisHost}:{RedisPort} ({discoveryTask.Exception?.GetBaseException().Message})");
            }
            else if (!discoveryTask.IsCanceled)
            {
                var discovered = discoveryTask.Result;
                if (discovered != null && discovered.Count > 0)
                {
                    SensorIds = discovered.ToArray();
                    Debug.Log($"Bootstrapper: Discovered {discovered.Count} sensors from Redis.");
                }
            }

            ApplySignalConfiguration();
            _deviceDiscoveryRoutine = null;
        }

        private void ApplyManualDriverConfiguration()
        {
            ClearAutomaticVisuals();
            EnsureManualDriverVisuals();
        }

        private void EnsureManualDriverVisuals()
        {
            if (_manualDriverInstances.Count > 0)
            {
                return;
            }

            SpawnManualDriverPrefab(AbstractVizPrefab, AbstractVizParent, "ManualVisual", 0, isGraphPrefab: false);
            SpawnManualDriverPrefab(GraphPrefab, GraphGroupParent, "ManualGraph", 0, isGraphPrefab: true);
        }

        private void SpawnManualDriverPrefab(GameObject prefab, Transform parent, string suffix, int index, bool isGraphPrefab)
        {
            if (prefab == null)
            {
                return;
            }

            bool hasManualDriver =
                prefab.GetComponentInChildren<AudienceDataManualDriver>(true) != null ||
                prefab.GetComponentInChildren<ParticleMeshManualDriver>(true) != null ||
                prefab.GetComponentInChildren<GraphManualDriver>(true) != null;

            if (!hasManualDriver)
            {
                return;
            }

            bool hasCustomParent = parent != null;
            var instance = hasCustomParent
                ? Instantiate(prefab, parent)
                : Instantiate(prefab);

            instance.name = $"{prefab.name}_{suffix}";
            if (isGraphPrefab)
            {
                PositionGraphGroup(instance.transform, index, hasCustomParent);
            }
            else
            {
                PositionAbstractViz(instance.transform, index, hasCustomParent);
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

            var audienceBindings = instance.GetComponentsInChildren<AudienceSignalBinding>(true);
            foreach (var binding in audienceBindings)
            {
                binding.enabled = false;
            }

            var audienceManualDrivers = instance.GetComponentsInChildren<AudienceDataManualDriver>(true);
            bool hasAudienceManualDriver = audienceManualDrivers.Length > 0;
            foreach (var manualDriver in audienceManualDrivers)
            {
                manualDriver.enabled = true;
            }

            var audienceVisualizerBindings = instance.GetComponentsInChildren<AudienceSignalVisualizerBinding>(true);
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!UnityEngine.Application.isPlaying || !isActiveAndEnabled)
            {
                return;
            }

            ApplySignalConfiguration();
        }
#endif

        private void EnsureSelectedSensorStreamMode(
            string[] redisChannels,
            bool spawnParticleVisual,
            bool spawnGraphVisual,
            bool spawnArtistVisual)
        {
            ClearSelectedSensorStreamMode();

            var deviceDefinitions = BuildAudienceDeviceChannelDefinitions(redisChannels);
            deviceDefinitions = FilterAudienceToConfiguredDevices(deviceDefinitions, SensorIds);
            if (deviceDefinitions.Count == 0)
            {
                Debug.LogWarning("Bootstrapper: No complete sensor streams are available for selected-stream mode.");
                EnsureSensorStreamPresentation(Array.Empty<AudienceSignalBinding>(), Array.Empty<AudienceSignalVisualizerBinding>());
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
                sourceObject.transform.SetParent(transform, false);
                sourceObject.SetActive(false);

                var binding = sourceObject.AddComponent<AudienceSignalBinding>();
                var physioPump = AcquirePhysioPumpForChannel(definition.DeviceId, definition.PhysioMetricsChannel);
                var engagementSource = ResolveSensorEngagementSource(definition.DeviceId);

                if (physioPump == null || engagementSource == null)
                {
                    Debug.LogWarning($"Bootstrapper: Unable to create selectable stream for '{definition.DeviceId}'.");
                    Destroy(sourceObject);
                    ReleasePhysioPump(definition.PhysioMetricsChannel);
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
                EnsureSensorStreamPresentation(Array.Empty<AudienceSignalBinding>(), Array.Empty<AudienceSignalVisualizerBinding>());
                return;
            }

            if (spawnParticleVisual)
            {
                _selectedParticleVisual = SpawnSelectedSensorVisual(
                    AbstractVizPrefab,
                    AbstractVizParent,
                    "SelectedSensor",
                    isGraphVisual: false);
            }

            if (spawnGraphVisual)
            {
                _selectedGraphVisual = SpawnSelectedSensorVisual(
                    GraphPrefab,
                    GraphGroupParent,
                    "SelectedSensor",
                    isGraphVisual: true);
            }

            if (spawnArtistVisual)
            {
                _selectedArtistVisual = SpawnSelectedSensorVisual(
                    ArtistVizPrefab,
                    ArtistVizParent,
                    "SelectedSensor",
                    isGraphVisual: false,
                    positionAtParentOrigin: true);
            }

            var visualizerBindings = new List<AudienceSignalVisualizerBinding>();
            AddVisualizerBindings(_selectedParticleVisual, visualizerBindings);
            AddVisualizerBindings(_selectedGraphVisual, visualizerBindings);
            AddVisualizerBindings(_selectedArtistVisual, visualizerBindings);

            EnsureSensorStreamPresentation(
                _selectedSensorSources.Select(source => source.Binding),
                visualizerBindings);

            Debug.Log($"Bootstrapper: Configured one persistent visualization target for {_selectedSensorSources.Count} selectable sensor streams.");
        }

        private GameObject SpawnSelectedSensorVisual(
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
            var instance = hasCustomParent ? Instantiate(prefab, parent) : Instantiate(prefab);
            instance.name = $"{prefab.name}_{suffix}";

            if (positionAtParentOrigin)
            {
                PositionAtParentOrigin(instance.transform, hasCustomParent);
            }
            else if (isGraphVisual)
            {
                PositionGraphGroup(instance.transform, 0, hasCustomParent);
            }
            else
            {
                PositionAbstractViz(instance.transform, 0, hasCustomParent);
            }

            DisableManualDrivers(instance);

            foreach (var sourceBinding in instance.GetComponentsInChildren<AudienceSignalBinding>(true))
            {
                sourceBinding.enabled = false;
                sourceBinding.ConfigureParticleTarget(null);
            }

            var visualizerBindings = instance.GetComponentsInChildren<AudienceSignalVisualizerBinding>(true);
            if (visualizerBindings.Length == 0)
            {
                Debug.LogWarning($"Bootstrapper: Selected-stream visual '{instance.name}' has no AudienceSignalVisualizerBinding.");
                Destroy(instance);
                return null;
            }

            foreach (var visualizerBinding in visualizerBindings)
            {
                visualizerBinding.enabled = true;
            }

            return instance;
        }

        private void PositionAtParentOrigin(Transform target, bool useLocalSpace)
        {
            if (target == null)
            {
                return;
            }

            if (useLocalSpace)
            {
                target.localPosition = Vector3.zero;
                target.localRotation = Quaternion.identity;
                return;
            }

            target.position = SpawnPosition;
            target.rotation = Quaternion.Euler(SpawnRotationEuler);
        }

        private void EnsureSensorStreamPresentation(
            IEnumerable<AudienceSignalBinding> sources,
            IEnumerable<AudienceSignalVisualizerBinding> visualizerBindings)
        {
            if (_sensorStreamCoordinator == null)
            {
                _sensorStreamCoordinator = GetComponent<SensorStreamCoordinator>();
                if (_sensorStreamCoordinator == null)
                {
                    _sensorStreamCoordinator = gameObject.AddComponent<SensorStreamCoordinator>();
                }
            }

            _sensorStreamCoordinator.ConfigureVisualizerBindings(visualizerBindings);
            _sensorStreamCoordinator.ConfigureSources(sources);

            if (_sensorStreamCycler == null)
            {
                _sensorStreamCycler = GetComponent<RayNeoTempleSensorStreamCycler>();
                if (_sensorStreamCycler == null)
                {
                    _sensorStreamCycler = gameObject.AddComponent<RayNeoTempleSensorStreamCycler>();
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

            Camera hudCamera = SensorHudCamera != null ? SensorHudCamera : Camera.main;
            if (hudCamera == null)
            {
                hudCamera = FindFirstObjectByType<Camera>();
            }

            if (hudCamera == null)
            {
                Debug.LogWarning("Bootstrapper: No camera is available for the active sensor HUD.");
                return;
            }

            _sensorHudObject = new GameObject("ActiveSensorHud");
            _sensorHudObject.transform.SetParent(hudCamera.transform, false);
            _sensorHudObject.transform.localPosition = SensorHudLocalPosition;
            _sensorHudObject.transform.localRotation = Quaternion.identity;
            _sensorHudObject.transform.localScale = Vector3.one * Mathf.Max(0.0001f, SensorHudLocalScale);

            var label = _sensorHudObject.AddComponent<TextMeshPro>();
            label.fontSize = Mathf.Max(0.01f, SensorHudFontSize);
            label.color = SensorHudColor;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.richText = false;
            label.sortingOrder = 100;
            label.rectTransform.sizeDelta = new Vector2(60f, 8f);

            var hudBinding = _sensorHudObject.AddComponent<ActiveSensorHudBinding>();
            hudBinding.Configure(_sensorStreamCoordinator, label);
        }

        private static void AddVisualizerBindings(
            GameObject instance,
            ICollection<AudienceSignalVisualizerBinding> bindings)
        {
            if (instance == null || bindings == null)
            {
                return;
            }

            foreach (var binding in instance.GetComponentsInChildren<AudienceSignalVisualizerBinding>(true))
            {
                if (binding != null && !bindings.Contains(binding))
                {
                    bindings.Add(binding);
                }
            }
        }

        private void EnsureAudienceDeviceVisuals(string[] redisChannels)
        {
            if (!AutoSpawnAbstractViz)
            {
                return;
            }

            if (AbstractVizPrefab == null)
            {
                Debug.LogWarning("Bootstrapper: AbstractVizPrefab is missing, cannot spawn audience signal instances.");
                ClearAudienceDeviceVisuals();
                return;
            }

            if (redisChannels == null || redisChannels.Length == 0)
            {
                ClearAudienceDeviceVisuals();
                return;
            }

            bool hasAudienceBinding = AbstractVizPrefab.GetComponentInChildren<AudienceSignalBinding>() != null;
            if (!hasAudienceBinding)
            {
                ClearAudienceDeviceVisuals();
                return;
            }

            var deviceDefinitions = BuildAudienceDeviceChannelDefinitions(redisChannels);
            deviceDefinitions = FilterAudienceToConfiguredDevices(deviceDefinitions, SensorIds);
            ClearAudienceDeviceVisuals();

            if (deviceDefinitions.Count == 0)
            {
                Debug.LogWarning("Bootstrapper: No device-specific audience signal channels were found.");
                return;
            }

            Debug.Log($"Bootstrapper: Spawning audience visuals for {deviceDefinitions.Count} devices: {string.Join(", ", deviceDefinitions.Select(d => d.DeviceId))}");

            bool hasCustomParent = AbstractVizParent != null;
            int deviceIndex = 0;
            foreach (var definition in deviceDefinitions)
            {
                if (!definition.HasVisualizationChannels)
                {
                    Debug.LogWarning($"Bootstrapper: Incomplete audience channel set for device '{definition.DeviceId}', skipping visual spawn.");
                    continue;
                }

                var instance = hasCustomParent
                    ? Instantiate(AbstractVizPrefab, AbstractVizParent)
                    : Instantiate(AbstractVizPrefab);

                instance.name = $"{AbstractVizPrefab.name}_{definition.DeviceId}";
                PositionAbstractViz(instance.transform, deviceIndex, hasCustomParent);

                DisableManualDrivers(instance);

                var binding = instance.GetComponentInChildren<AudienceSignalBinding>();
                if (binding == null)
                {
                    Debug.LogWarning($"Bootstrapper: Audience visual '{instance.name}' is missing AudienceSignalBinding, skipping visual spawn.");
                    Destroy(instance);
                    continue;
                }

                var particleMeshVisualizer = instance.GetComponentInChildren<ParticleMeshVisualizer>();
                var physioPump = AcquirePhysioPumpForChannel(definition.DeviceId, definition.PhysioMetricsChannel);
                var engagementSource = ResolveSensorEngagementSource(definition.DeviceId);

                if (!TryConfigureAudienceBinding(binding, physioPump, engagementSource, particleMeshVisualizer, definition.DeviceId))
                {
                    Debug.LogWarning($"Bootstrapper: Unable to configure audience binding on '{instance.name}', skipping visual spawn.");
                    Destroy(instance);
                    ReleasePhysioPump(definition.PhysioMetricsChannel);
                    continue;
                }

                _audienceDeviceInstances.Add(new AudienceDeviceInstance
                {
                    VisualInstance = instance,
                    Binding = binding,
                    PhysioMetricsChannel = definition.PhysioMetricsChannel
                });

                AttachDeviceLabel(instance, definition.DeviceId, deviceIndex);
                deviceIndex++;
            }

            if (deviceIndex == 0)
            {
                Debug.LogWarning("Bootstrapper: Unable to spawn audience visuals because no device had the required channels.");
            }
        }

        private void EnsureAudienceGraphDeviceVisuals(string[] redisChannels)
        {
            if (!AutoSpawnGraphVisualsFromChannels)
            {
                return;
            }

            if (GraphPrefab == null)
            {
                Debug.LogWarning("Bootstrapper: GraphPrefab is missing, cannot spawn audience graph instances.");
                ClearAudienceGraphDeviceVisuals();
                return;
            }

            if (redisChannels == null || redisChannels.Length == 0)
            {
                ClearAudienceGraphDeviceVisuals();
                return;
            }

            bool hasAudienceBinding = GraphPrefab.GetComponentInChildren<AudienceSignalBinding>() != null;
            bool hasAudienceVisualizerBinding = GraphPrefab.GetComponentInChildren<AudienceSignalVisualizerBinding>() != null;
            bool hasGraphVisualizer = GraphPrefab.GetComponentInChildren<GraphVisualizer>() != null;
            if (!hasAudienceBinding || !hasAudienceVisualizerBinding || !hasGraphVisualizer)
            {
                Debug.LogWarning("Bootstrapper: Audience GraphPrefab must include AudienceSignalBinding, AudienceSignalVisualizerBinding, and at least one GraphVisualizer component. Audience graph metrics are configured on the prefab's AudienceSignalVisualizerBinding.");
                ClearAudienceGraphDeviceVisuals();
                return;
            }

            var deviceDefinitions = BuildAudienceDeviceChannelDefinitions(redisChannels);
            deviceDefinitions = FilterAudienceToConfiguredDevices(deviceDefinitions, SensorIds);
            ClearAudienceGraphDeviceVisuals();

            if (deviceDefinitions.Count == 0)
            {
                Debug.LogWarning("Bootstrapper: No device-specific audience channels were found for graphs.");
                return;
            }

            Debug.Log($"Bootstrapper: Spawning audience graph groups for {deviceDefinitions.Count} devices: {string.Join(", ", deviceDefinitions.Select(d => d.DeviceId))}");

            bool hasCustomParent = GraphGroupParent != null;
            int deviceIndex = 0;
            foreach (var definition in deviceDefinitions)
            {
                if (!definition.HasVisualizationChannels)
                {
                    Debug.LogWarning($"Bootstrapper: Incomplete audience channel set for device '{definition.DeviceId}', skipping graph spawn.");
                    continue;
                }

                var instance = hasCustomParent
                    ? Instantiate(GraphPrefab, GraphGroupParent)
                    : Instantiate(GraphPrefab);

                instance.name = $"{GraphPrefab.name}_{definition.DeviceId}";
                PositionGraphGroup(instance.transform, deviceIndex, hasCustomParent);
                DisableManualDrivers(instance);

                var binding = instance.GetComponentInChildren<AudienceSignalBinding>();
                if (binding == null)
                {
                    Debug.LogWarning($"Bootstrapper: Audience graph '{instance.name}' is missing AudienceSignalBinding, skipping graph spawn.");
                    Destroy(instance);
                    continue;
                }

                var physioPump = AcquirePhysioPumpForChannel(definition.DeviceId, definition.PhysioMetricsChannel);
                var engagementSource = ResolveSensorEngagementSource(definition.DeviceId);

                if (!TryConfigureAudienceBinding(binding, physioPump, engagementSource, null, definition.DeviceId))
                {
                    Debug.LogWarning($"Bootstrapper: Unable to configure audience graph binding on '{instance.name}', skipping graph spawn.");
                    Destroy(instance);
                    ReleasePhysioPump(definition.PhysioMetricsChannel);
                    continue;
                }

                _audienceGraphDeviceGroups.Add(new AudienceGraphDeviceGroupInstance
                {
                    GroupRoot = instance,
                    Binding = binding,
                    PhysioMetricsChannel = definition.PhysioMetricsChannel
                });

                AttachGraphGroupLabel(instance, definition.DeviceId, deviceIndex);
                deviceIndex++;
            }
        }

        private void EnsureSensorEngagementBackend(IEnumerable<string> sensorIds)
        {
            string[] normalizedSensorIds = sensorIds == null
                ? Array.Empty<string>()
                : sensorIds
                    .Where(sensorId => !string.IsNullOrWhiteSpace(sensorId))
                    .Select(sensorId => sensorId.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(sensorId => sensorId, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            if (normalizedSensorIds.Length == 0)
            {
                ClearSensorEngagementBackend();
                return;
            }

            SensorEngagementChannelRoute[] routes = BuildSensorEngagementRoutes(normalizedSensorIds);
            string configurationKey = BuildSensorEngagementConfigurationKey(normalizedSensorIds, routes);
            if (_sensorEngagementBackendObject != null &&
                _sensorEngagementCoordinator != null &&
                string.Equals(_sensorEngagementConfigurationKey, configurationKey, StringComparison.Ordinal))
            {
                return;
            }

            ClearSensorEngagementBackend();

            _sensorEngagementBackendObject = new GameObject("Sensor Engagement Backend");
            _sensorEngagementBackendObject.transform.SetParent(transform, false);
            _sensorEngagementBackendObject.SetActive(false);

            _sensorEngagementPump = _sensorEngagementBackendObject.AddComponent<RedisSensorEngagementPump>();
            _sensorEngagementCoordinator = _sensorEngagementBackendObject.AddComponent<SensorEngagementCoordinator>();

            var decoder = new SensorEngagementJsonDecoder(SensorEngagementPayloadFormat);
            _sensorEngagementPump.ConfigureConnection(
                RedisHost,
                RedisPort,
                UseIndividualSensorEngagement ? routes : Array.Empty<SensorEngagementChannelRoute>(),
                decoder);
            _sensorEngagementCoordinator.Configure(
                _sensorEngagementPump,
                normalizedSensorIds,
                SensorEngagementTimeoutSeconds,
                SensorEngagementNeutralValue);

            _sensorEngagementConfigurationKey = configurationKey;
            _sensorEngagementBackendObject.SetActive(true);

            if (UseIndividualSensorEngagement && routes.Length == 0)
            {
                Debug.LogWarning(
                    "Bootstrapper: Individual sensor engagement is enabled, but no channel template or overrides are configured. " +
                    "Sensor visualizations will use the neutral engagement value.",
                    this);
            }
        }

        private SensorEngagementChannelRoute[] BuildSensorEngagementRoutes(IEnumerable<string> sensorIds)
        {
            if (!UseIndividualSensorEngagement || sensorIds == null)
            {
                return Array.Empty<SensorEngagementChannelRoute>();
            }

            var routes = new List<SensorEngagementChannelRoute>();
            foreach (string sensorId in sensorIds)
            {
                string channelName = ResolveSensorEngagementChannel(sensorId);
                if (!string.IsNullOrWhiteSpace(channelName))
                {
                    routes.Add(new SensorEngagementChannelRoute(sensorId, channelName));
                }
            }

            return routes.ToArray();
        }

        private string ResolveSensorEngagementChannel(string sensorId)
        {
            if (SensorEngagementChannelOverrides != null)
            {
                for (int i = 0; i < SensorEngagementChannelOverrides.Length; i++)
                {
                    SensorEngagementChannelOverride channelOverride = SensorEngagementChannelOverrides[i];
                    if (string.Equals(channelOverride.SensorId?.Trim(), sensorId, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(channelOverride.ChannelName))
                    {
                        return channelOverride.ChannelName.Trim();
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(SensorEngagementChannelTemplate))
            {
                return string.Empty;
            }

            return SensorEngagementChannelTemplate
                .Trim()
                .Replace("{serial}", sensorId)
                .Replace("{sensorId}", sensorId)
                .Replace("{0}", sensorId);
        }

        private string BuildSensorEngagementConfigurationKey(
            IEnumerable<string> sensorIds,
            IEnumerable<SensorEngagementChannelRoute> routes)
        {
            string sensorKey = string.Join("|", sensorIds);
            string routeKey = string.Join("|", routes.Select(route => $"{route.SensorId}={route.ChannelName}"));
            return $"{RedisHost}:{RedisPort};enabled={UseIndividualSensorEngagement};timeout={SensorEngagementTimeoutSeconds:R};" +
                $"neutral={SensorEngagementNeutralValue:R};sensors={sensorKey};routes={routeKey};" +
                $"items={SensorEngagementPayloadFormat.ItemsPropertyName};sensor={SensorEngagementPayloadFormat.SensorIdPropertyName};" +
                $"score={SensorEngagementPayloadFormat.EngagementPropertyName};confirmed={SensorEngagementPayloadFormat.ConfirmationPropertyName};" +
                $"timestamp={SensorEngagementPayloadFormat.TimestampPropertyName}";
        }

        private ILatestDataSource<SensorEngagementState> ResolveSensorEngagementSource(string sensorId)
        {
            return _sensorEngagementCoordinator?.GetSource(sensorId);
        }

        private void ClearSensorEngagementBackend()
        {
            if (_sensorEngagementBackendObject != null)
            {
                Destroy(_sensorEngagementBackendObject);
            }

            _sensorEngagementBackendObject = null;
            _sensorEngagementPump = null;
            _sensorEngagementCoordinator = null;
            _sensorEngagementConfigurationKey = string.Empty;
        }

        private RedisPhysioMetricsPump AcquirePhysioPumpForChannel(string deviceId, string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
            {
                return null;
            }

            if (_sharedPhysioPumpsByChannel.TryGetValue(channelName, out var handle) && handle.Pump != null)
            {
                handle.ReferenceCount++;
                if (!string.Equals(handle.Host, RedisHost, StringComparison.Ordinal) || handle.Port != RedisPort)
                {
                    handle.Pump.ConfigureConnection(RedisHost, RedisPort, channelName);
                    handle.Host = RedisHost;
                    handle.Port = RedisPort;
                }
                return handle.Pump;
            }

            var pumpObject = new GameObject($"RedisPump_{deviceId}_PhysioMetrics");
            pumpObject.transform.SetParent(transform, false);
            pumpObject.SetActive(false);

            var pump = pumpObject.AddComponent<RedisPhysioMetricsPump>();
            pump.ConfigureConnection(RedisHost, RedisPort, channelName);

            pumpObject.SetActive(true);

            _sharedPhysioPumpsByChannel[channelName] = new SharedPhysioPumpHandle
            {
                Host = RedisHost,
                Port = RedisPort,
                Pump = pump,
                ReferenceCount = 1
            };

            return pump;
        }

        private void ReleasePhysioPump(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
            {
                return;
            }

            if (!_sharedPhysioPumpsByChannel.TryGetValue(channelName, out var handle))
            {
                return;
            }

            handle.ReferenceCount--;
            if (handle.ReferenceCount > 0)
            {
                return;
            }

            if (handle.Pump != null)
            {
                Destroy(handle.Pump.gameObject);
            }

            _sharedPhysioPumpsByChannel.Remove(channelName);
        }

        private static bool TryConfigureAudienceBinding(
            AudienceSignalBinding binding,
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

            var visualizerBinding = binding.GetComponentInChildren<AudienceSignalVisualizerBinding>();
            if (visualizerBinding == null && binding.gameObject != null)
            {
                visualizerBinding = binding.gameObject.GetComponentInParent<AudienceSignalVisualizerBinding>();
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

            var audienceManualDrivers = instance.GetComponentsInChildren<AudienceDataManualDriver>(true);
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

        private void ClearAudienceDeviceVisuals()
        {
            if (_audienceDeviceInstances.Count == 0)
            {
                return;
            }

            foreach (var instance in _audienceDeviceInstances)
            {
                if (instance.VisualInstance != null)
                {
                    Destroy(instance.VisualInstance);
                }
                else if (instance.Binding != null)
                {
                    Destroy(instance.Binding.gameObject);
                }

                ReleasePhysioPump(instance.PhysioMetricsChannel);
            }

            _audienceDeviceInstances.Clear();
        }

        private void ClearAudienceGraphDeviceVisuals()
        {
            if (_audienceGraphDeviceGroups.Count == 0)
            {
                return;
            }

            foreach (var instance in _audienceGraphDeviceGroups)
            {
                if (instance.GroupRoot != null)
                {
                    Destroy(instance.GroupRoot);
                }
                else if (instance.Binding != null)
                {
                    Destroy(instance.Binding.gameObject);
                }

                ReleasePhysioPump(instance.PhysioMetricsChannel);
            }

            _audienceGraphDeviceGroups.Clear();
        }

        private void ClearSelectedSensorStreamMode()
        {
            if (_sensorStreamCoordinator != null)
            {
                _sensorStreamCoordinator.ConfigureVisualizerBindings(Array.Empty<AudienceSignalVisualizerBinding>());
                _sensorStreamCoordinator.ConfigureSources(Array.Empty<AudienceSignalBinding>());
            }

            foreach (var source in _selectedSensorSources)
            {
                if (source.SourceObject != null)
                {
                    Destroy(source.SourceObject);
                }
                else if (source.Binding != null)
                {
                    Destroy(source.Binding.gameObject);
                }

                ReleasePhysioPump(source.PhysioMetricsChannel);
            }

            _selectedSensorSources.Clear();

            if (_selectedParticleVisual != null)
            {
                Destroy(_selectedParticleVisual);
                _selectedParticleVisual = null;
            }

            if (_selectedGraphVisual != null)
            {
                Destroy(_selectedGraphVisual);
                _selectedGraphVisual = null;
            }

            if (_selectedArtistVisual != null)
            {
                Destroy(_selectedArtistVisual);
                _selectedArtistVisual = null;
            }

            if (_sensorHudObject != null)
            {
                Destroy(_sensorHudObject);
                _sensorHudObject = null;
            }
        }

        private void ClearManualDriverVisuals()
        {
            if (_manualDriverInstances.Count == 0)
            {
                return;
            }

            foreach (var instance in _manualDriverInstances)
            {
                if (instance != null)
                {
                    Destroy(instance);
                }
            }

            _manualDriverInstances.Clear();
        }

        private void PositionAbstractViz(Transform target, int index, bool useLocalSpace)
        {
            if (target == null)
            {
                return;
            }

            int perRow = Mathf.Max(1, AbstractVizPerRow);
            int row = index / perRow;
            int column = index % perRow;

            Vector3 offset = AbstractVizOriginOffset + new Vector3(
                column * AbstractVizSpacing.x,
                row * AbstractVizSpacing.y,
                row * AbstractVizSpacing.z);

            if (useLocalSpace)
            {
                target.localPosition = offset;
                target.localRotation = Quaternion.identity;
            }
            else
            {
                target.position = SpawnPosition + offset;
                target.rotation = Quaternion.Euler(SpawnRotationEuler);
            }
        }

        private void PositionGraphGroup(Transform target, int index, bool useLocalSpace)
        {
            if (target == null)
            {
                return;
            }

            int perRow = Mathf.Max(1, GraphGroupsPerRow);
            int row = index / perRow;
            int column = index % perRow;

            Vector3 offset = GraphGroupOriginOffset + new Vector3(
                column * GraphGroupSpacing.x,
                row * GraphGroupSpacing.y,
                row * GraphGroupSpacing.z);

            if (useLocalSpace)
            {
                target.localPosition = offset;
                target.localRotation = Quaternion.identity;
            }
            else
            {
                target.position = SpawnPosition + offset;
                target.rotation = Quaternion.Euler(SpawnRotationEuler);
            }
        }

        private List<AudienceDeviceChannelDefinition> BuildAudienceDeviceChannelDefinitions(IEnumerable<string> redisChannels)
        {
            var orderedDeviceIds = new List<string>();
            var map = new Dictionary<string, AudienceDeviceChannelDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (var channel in redisChannels)
            {
                if (string.IsNullOrWhiteSpace(channel))
                {
                    continue;
                }

                string deviceId;
                AudienceDeviceChannelDefinition definition;

                if (RedisAudienceChannels.TryParsePhysioMetricsDeviceChannel(channel, out deviceId))
                {
                    definition = GetOrCreateAudienceDefinition(deviceId, orderedDeviceIds, map);
                    definition.PhysioMetricsChannel = channel;
                    map[deviceId] = definition;
                }
            }

            var ordered = new List<AudienceDeviceChannelDefinition>();
            foreach (var deviceId in orderedDeviceIds)
            {
                if (!map.TryGetValue(deviceId, out var definition))
                {
                    continue;
                }

                ordered.Add(definition);
            }

            return ordered;
        }

        private static AudienceDeviceChannelDefinition GetOrCreateAudienceDefinition(
            string deviceId,
            List<string> orderedDeviceIds,
            Dictionary<string, AudienceDeviceChannelDefinition> map)
        {
            if (!map.TryGetValue(deviceId, out var definition))
            {
                definition = new AudienceDeviceChannelDefinition { DeviceId = deviceId };
                orderedDeviceIds.Add(deviceId);
            }

            return definition;
        }

        private static List<AudienceDeviceChannelDefinition> FilterAudienceToConfiguredDevices(IEnumerable<AudienceDeviceChannelDefinition> definitions, IEnumerable<string> configuredDeviceIds)
        {
            if (configuredDeviceIds == null)
            {
                return new List<AudienceDeviceChannelDefinition>(definitions);
            }

            var allowed = new HashSet<string>(configuredDeviceIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()), StringComparer.OrdinalIgnoreCase);
            if (allowed.Count == 0)
            {
                return new List<AudienceDeviceChannelDefinition>(definitions);
            }

            var filtered = new List<AudienceDeviceChannelDefinition>();
            foreach (var definition in definitions)
            {
                if (!string.IsNullOrWhiteSpace(definition.DeviceId) && allowed.Contains(definition.DeviceId))
                {
                    filtered.Add(definition);
                }
            }

            return filtered;
        }

        private string[] GetRedisChannels()
        {
            var channels = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            void AddChannel(string channel)
            {
                if (string.IsNullOrWhiteSpace(channel))
                {
                    return;
                }

                if (seen.Add(channel))
                {
                    channels.Add(channel);
                }
            }

            if (!string.IsNullOrWhiteSpace(RedisChannel))
            {
                AddChannel(RedisChannel);
            }

            if (RedisChannels != null && RedisChannels.Length > 0)
            {
                foreach (var channel in RedisChannels)
                {
                    AddChannel(channel);
                }
            }

            if (IncludeSensorChannels)
            {
                if (SensorIds != null)
                {
                    foreach (var deviceId in SensorIds)
                    {
                        AddChannel(RedisAudienceChannels.FormatPhysioMetricsChannel(deviceId));
                    }
                }
            }

            if (channels.Count == 0)
            {
                AddChannel(RedisEmotionChannels.BroadcastChannel);
            }

            return channels.ToArray();
        }

        private void AttachDeviceLabel(GameObject visualInstance, string deviceId, int deviceIndex)
        {
            if (!ShowDeviceLabels || visualInstance == null)
            {
                return;
            }

            Transform labelParent = ResolveDeviceLabelParent(visualInstance);
            if (labelParent == null)
            {
                return;
            }

            var labelObject = new GameObject("DeviceLabel");
            labelObject.transform.SetParent(labelParent, false);
            labelObject.transform.localPosition = DeviceLabelOffset;
            labelObject.transform.localRotation = Quaternion.identity;
            labelObject.transform.localScale = Vector3.one;
            labelObject.layer = visualInstance.layer;

            var text = labelObject.AddComponent<TextMeshPro>();
            text.text = FormatDeviceLabel(deviceId, deviceIndex);
            text.fontSize = Mathf.Max(0.01f, DeviceLabelFontSize);
            text.color = DeviceLabelColor;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.richText = false;

            labelObject.AddComponent<BillboardLabel>();
        }

        private void AttachGraphGroupLabel(GameObject groupRoot, string deviceId, int deviceIndex)
        {
            if (!ShowGraphGroupLabels || groupRoot == null)
            {
                return;
            }

            string labelText = FormatGraphGroupLabel(deviceId, deviceIndex);
            if (string.IsNullOrWhiteSpace(labelText))
            {
                return;
            }

            var labelObject = new GameObject("GraphGroupLabel");
            labelObject.transform.SetParent(groupRoot.transform, false);
            labelObject.transform.localRotation = Quaternion.identity;
            labelObject.transform.localScale = Vector3.one;
            labelObject.layer = groupRoot.layer;

            float extraYOffset = Mathf.Max(0f, GraphGroupLabelVerticalOffset);
            Vector3 localPosition = GraphGroupLabelOffset + new Vector3(0f, extraYOffset, 0f);
            bool boundsTooSmall = GraphItemBoundsSize.sqrMagnitude < 1e-6f;
            if (!boundsTooSmall)
            {
                Vector3 size = GraphItemBoundsSize + GraphGroupBoundsPadding * 2f;
                float yOffset = size.y * 0.5f + extraYOffset;
                localPosition = new Vector3(0f, yOffset, 0f) + GraphGroupLabelOffset;
            }

            labelObject.transform.localPosition = localPosition;

            var text = labelObject.AddComponent<TextMeshPro>();
            text.text = labelText;
            text.fontSize = Mathf.Max(0.01f, GraphGroupLabelFontSize);
            text.color = GraphGroupLabelColor;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.richText = false;

            labelObject.AddComponent<BillboardLabel>();
        }

        private static string FormatGraphGroupLabel(string deviceId, int deviceIndex)
        {
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                return deviceId.Trim();
            }

            return $"Device {deviceIndex + 1}";
        }

        private static string FormatDeviceLabel(string deviceId, int deviceIndex)
        {
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                string numeric = ExtractTrailingDigits(deviceId);
                if (!string.IsNullOrEmpty(numeric))
                {
                    return $"Device {numeric}";
                }

                return $"Device {deviceId.Trim()}";
            }

            return $"Device {deviceIndex + 1}";
        }

        private static string ExtractTrailingDigits(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            int end = value.Length - 1;
            while (end >= 0 && !char.IsDigit(value[end]))
            {
                end--;
            }

            if (end < 0)
            {
                return string.Empty;
            }

            int start = end;
            while (start >= 0 && char.IsDigit(value[start]))
            {
                start--;
            }

            int length = end - start;
            return length > 0 ? value.Substring(start + 1, length) : string.Empty;
        }

        private static Transform ResolveDeviceLabelParent(GameObject instance)
        {
            if (instance == null)
            {
                return null;
            }

            var particleMeshVisualizer = instance.GetComponentInChildren<ParticleMeshVisualizer>(true);
            if (particleMeshVisualizer != null)
            {
                return particleMeshVisualizer.transform;
            }

            return instance.transform;
        }

        private sealed class AudienceDeviceInstance
        {
            public GameObject VisualInstance;
            public AudienceSignalBinding Binding;
            public string PhysioMetricsChannel;
        }

        private sealed class AudienceGraphDeviceGroupInstance
        {
            public GameObject GroupRoot;
            public AudienceSignalBinding Binding;
            public string PhysioMetricsChannel;
        }

        private sealed class SelectedSensorSourceInstance
        {
            public GameObject SourceObject;
            public AudienceSignalBinding Binding;
            public string PhysioMetricsChannel;
        }

        private sealed class SharedPhysioPumpHandle
        {
            public string Host;
            public int Port;
            public RedisPhysioMetricsPump Pump;
            public int ReferenceCount;
        }

        private struct AudienceDeviceChannelDefinition
        {
            public string DeviceId;
            public string PhysioMetricsChannel;

            public bool HasVisualizationChannels =>
                !string.IsNullOrWhiteSpace(DeviceId) &&
                !string.IsNullOrWhiteSpace(PhysioMetricsChannel);
        }

        [Serializable]
        public struct SensorEngagementChannelOverride
        {
            public string SensorId;
            public string ChannelName;
        }
    }
}
