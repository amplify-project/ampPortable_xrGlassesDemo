using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmpPortableDataViz.Application;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Infra;
using AmpPortableDataViz.Presentation.Anchors;
using AmpPortableDataViz.Presentation.Interaction;
using AmpPortableDataViz.Presentation.Mapping;
using AmpPortableDataViz.Presentation.Sources;
using AmpPortableDataViz.Presentation.Utility;
using AmpPortableDataViz.Presentation.Visualization;
using TMPro;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Bootstrap
{
    /// <summary>
    /// Composition root for wiring the visualization session using Ports and Adapters.
    /// </summary>
    public sealed class Bootstrapper : MonoBehaviour
    {
        [Header("Scene Dependencies")]
        public AnchorRegistry AnchorRegistry;
        public SineWaveSource SineSource;
        public RedisDataPump RedisSource;
        public LiveKitTelemetryReceiver LiveKitSource;
        public RedisManager RedisManager;
        public GameObject VisualPrefab;

        public enum SignalSourceType
        {
            Sine,
            Redis,
            LiveKit
        }

        [Header("Session Settings")]
        public bool IsHost = true;
        public string VisualId = "SimpleDemo";
        public string ProfileId = "Default";
        public int Seed = 12345;

        [Header("Signal Source Selection")]
        public SignalSourceType ActiveSignalSource = SignalSourceType.Sine;

        [Header("Manual Visualization Testing")]
        public bool UseManualVisualizationDrivers;

        [Header("Redis Settings")]
        public string RedisHost = "192.168.0.6";
        public int RedisPort = 6379;
        public string RedisChannel = RedisEmotionChannels.BroadcastChannel;
        public string[] RedisChannels = Array.Empty<string>();
        [Header("Redis Prompt")]
        public bool RequireRedisEndpointConfirmation = true;

        [Header("Emotion Channel Settings")]
        public bool IncludeEmotionChannels = true;
        public string[] EmotionDeviceIds = RedisEmotionChannels.GetDefaultDeviceIds();
        public bool AutoDetectEmotionDeviceIds = true;
        public int RedisDiscoveryTimeoutMs = 2000;

        [Header("Emotion Visual Settings")]
        public bool AutoSpawnEmotionVisualsFromChannels = true;
        public Transform EmotionVisualParent;
        public Vector3 EmotionVisualOriginOffset = Vector3.zero;
        public Vector3 EmotionVisualSpacing = new Vector3(0.75f, 0f, 0.75f);
        [Min(1)]
        public int EmotionVisualsPerRow = 2;
        [Header("Emotion Visual Labels")]
        public bool ShowDeviceLabels = true;
        public Vector3 DeviceLabelOffset = new Vector3(0f, -0.35f, 0f);
        public float DeviceLabelFontSize = 0.22f;
        public Color DeviceLabelColor = Color.white;

        [Header("Audience Signal Visual Settings")]
        public bool AutoSpawnAudienceVisualsFromChannels = true;

        [Header("Individual Sensor Engagement")]
        public bool UseIndividualSensorEngagement = true;
        [Tooltip("Use {sensorId} or {0} where the sensor ID belongs in the Redis channel name.")]
        public string SensorEngagementChannelTemplate = string.Empty;
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

        [Header("Graph Panels")]
        public bool ShowGraphPanels = true;
        public Color GraphPanelColor = new Color(0f, 0f, 0f, 0.2f);
        public Vector2 GraphPanelPadding = new Vector2(0.08f, 0.08f);
        public float GraphPanelDepthOffset = 0.01f;
        public Vector3 GraphPanelOffset = Vector3.zero;

        [Header("Graph Group Panels")]
        public bool ShowGraphGroupPanels = true;
        public Color GraphGroupPanelColor = new Color(0f, 0f, 0f, 0.12f);
        public Vector2 GraphGroupPanelPadding = new Vector2(0.12f, 0.12f);
        public float GraphGroupPanelDepthOffset = 0.02f;
        public Vector3 GraphGroupPanelOffset = Vector3.zero;

        [Header("Graph Labels")]
        public bool ShowGraphLabels = true;
        [Min(0f)]
        public float GraphLabelHorizontalPadding = 0.08f;
        public float GraphLabelVerticalOffset = 0f;
        public float GraphLabelDepthOffset = 0f;
        public Vector3 GraphLabelOffset = Vector3.zero;
        public float GraphLabelFontSize = 0.18f;
        public Color GraphLabelColor = Color.white;

        [Header("Graph Group Labels")]
        public bool ShowGraphGroupLabels = true;
        public Vector3 GraphGroupLabelOffset = Vector3.zero;
        [Min(0f)]
        public float GraphGroupLabelVerticalOffset = 0.2f;
        public float GraphGroupLabelFontSize = 0.24f;
        public Color GraphGroupLabelColor = Color.white;

        public enum GraphChannelLayoutMode
        {
            InlineOffsets,
            Stacked
        }

        [Header("Legacy Graph Channel Stacking")]
        public GraphChannelLayoutMode GraphChannelLayout = GraphChannelLayoutMode.InlineOffsets;
        public Vector3 GraphStackedOriginOffset = Vector3.zero;
        [Min(0.01f)]
        public float GraphStackedVerticalSpacing = 0.6f;
        [Range(0.1f, 1f)]
        public float GraphStackedHeightScale = 0.6f;

        [Header("Legacy Graph Channel Offsets")]
        public Vector3 GraphHeartRateOffset = new Vector3(0.6f, 0f, 0f);
        public Vector3 GraphEdaOffset = new Vector3(0.6f, -0.6f, 0f);

        [Header("Legacy Graph Stream Settings")]
        public Vector2 GraphHeartRateRange = new Vector2(40f, 200f);
        public Vector2 GraphEdaRange = new Vector2(0f, 10f);
        public Color GraphHeartRateColor = new Color(0.95f, 0.2f, 0.2f, 1f);
        public Color GraphEdaColor = new Color(0.2f, 0.8f, 0.95f, 1f);
        [Range(0.0001f, 0.05f)]
        public float GraphLineWidth = 0.01f;

        [Header("Graph Group Layout")]
        public Vector3 GraphItemBoundsSize = new Vector3(1f, 1f, 0.02f);
        public Vector3 GraphGroupBoundsPadding = new Vector3(0.05f, 0.05f, 0.05f);

        [Header("LiveKit Settings")]
        public string LiveKitTokenEndpoint = "https://cloud-api.livekit.io/api/sandbox/connection-details";
        public string LiveKitSandboxId = "amp-portable-viz-16o67i";
        public string LiveKitRoomName = "xr-room";
        public string LiveKitIdentity = "xreal-receiver-1";

        [Header("Anchor Settings")]
        public string AnchorLabel = "Demo";
        public Vector3 AnchorPosition = new Vector3(0f, 1.2f, 2f);
        public Vector3 AnchorRotationEuler = Vector3.zero;

        [Header("Network Topics")]
        public string SpawnTopic = "session/visual/spawn";
        public string ParamTopic = "session/visual/param";

        [Header("Mapper Configuration")]
        public FloatToSimpleParams.Settings MapperSettings = new FloatToSimpleParams.Settings
        {
            Scale = 1f,
            ClampRange = new Vector2(0f, 1f),
            HueMin = 0.55f,
            HueMax = 0.85f
        };

        private VisualizationSessionController _sessionController;
        private FloatToSimpleParams _floatToParamsMapper;
        private Coroutine _initializationRoutine;
        private Coroutine _deviceDiscoveryRoutine;
        private Coroutine _graphLayoutRefreshRoutine;
        private readonly List<EmotionDeviceInstance> _emotionDeviceInstances = new List<EmotionDeviceInstance>();
        private readonly List<AudienceDeviceInstance> _audienceDeviceInstances = new List<AudienceDeviceInstance>();
        private readonly List<GraphDeviceGroupInstance> _graphDeviceGroups = new List<GraphDeviceGroupInstance>();
        private readonly List<AudienceGraphDeviceGroupInstance> _audienceGraphDeviceGroups = new List<AudienceGraphDeviceGroupInstance>();
        private readonly List<SelectedSensorSourceInstance> _selectedSensorSources = new List<SelectedSensorSourceInstance>();
        private readonly List<GameObject> _manualDriverInstances = new List<GameObject>();
        private readonly Dictionary<string, SharedPumpHandle> _sharedPumpsByChannel = new Dictionary<string, SharedPumpHandle>(StringComparer.Ordinal);
        private readonly Dictionary<string, SharedPhysioPumpHandle> _sharedPhysioPumpsByChannel = new Dictionary<string, SharedPhysioPumpHandle>(StringComparer.Ordinal);
        private RedisDataPump _engagementHudPump;
        private string _engagementHudChannel = string.Empty;
        private GameObject _sensorEngagementBackendObject;
        private RedisSensorEngagementPump _sensorEngagementPump;
        private SensorEngagementCoordinator _sensorEngagementCoordinator;
        private string _sensorEngagementConfigurationKey = string.Empty;
        private bool _redisEndpointReady;
        private Material _graphPanelMaterial;
        private Material _graphGroupPanelMaterial;
        private SensorStreamCoordinator _sensorStreamCoordinator;
        private RayNeoTempleSensorStreamCycler _sensorStreamCycler;
        private GameObject _selectedParticleVisual;
        private GameObject _selectedGraphVisual;
        private GameObject _sensorHudObject;

        private void Awake()
        {
            _redisEndpointReady = !RequireRedisEndpointConfirmation;
            EnsureAnchorRegistry();
            EnsureSources();
            EnsureVisualPrefab();
            ApplySignalConfiguration();

            var clock = new UnityClock();
            var anchorService = new MockAnchorService();
            var networkSync = new LocalLoopbackNetworkSync();
            var visualizationFactory = new UnityVisualizationFactory(VisualPrefab);

            _floatToParamsMapper = new FloatToSimpleParams(MapperSettings);
            _sessionController = new VisualizationSessionController(
                clock,
                anchorService,
                networkSync,
                AnchorRegistry,
                visualizationFactory);
        }

        private void OnEnable()
        {
            if (ShouldAutoDetectEmotionDeviceIds())
            {
                StartDeviceDiscovery();
                return;
            }

            ApplySignalConfiguration();
            BeginSessionIfReady();
        }

        public void BeginAfterEndpoint()
        {
            Debug.Log("Bootstrapper: BeginAfterEndpoint invoked from Redis endpoint prompt.");

            var settings = RedisRuntimeSettings.Load();
            RedisHost = settings.Host;
            RedisPort = settings.Port;
            _redisEndpointReady = true;

            Debug.Log($"Bootstrapper: Redis endpoint confirmed -> {RedisHost}:{RedisPort}");

            if (_initializationRoutine != null)
            {
                StopCoroutine(_initializationRoutine);
                _initializationRoutine = null;
            }

            if (_deviceDiscoveryRoutine != null)
            {
                StopCoroutine(_deviceDiscoveryRoutine);
                _deviceDiscoveryRoutine = null;
            }

            if (ShouldAutoDetectEmotionDeviceIds())
            {
                Debug.Log("Bootstrapper: Auto-detecting Redis emotion device ids after endpoint confirmation.");
                StartDeviceDiscovery();
                return;
            }

            Debug.Log("Bootstrapper: Applying signal configuration after endpoint confirmation.");
            ApplySignalConfiguration();
            Debug.Log("Bootstrapper: Beginning session after endpoint confirmation.");
            BeginSessionIfReady();
        }

        private void OnDisable()
        {
            if (RedisSource != null)
            {
                RedisSource.enabled = false;
            }

            if (RedisManager != null)
            {
                RedisManager.enabled = false;
            }

            if (LiveKitSource != null)
            {
                LiveKitSource.enabled = false;
            }

            if (_initializationRoutine != null)
            {
                StopCoroutine(_initializationRoutine);
                _initializationRoutine = null;
            }

            if (_deviceDiscoveryRoutine != null)
            {
                StopCoroutine(_deviceDiscoveryRoutine);
                _deviceDiscoveryRoutine = null;
            }

            if (_graphLayoutRefreshRoutine != null)
            {
                StopCoroutine(_graphLayoutRefreshRoutine);
                _graphLayoutRefreshRoutine = null;
            }

            _sessionController?.Shutdown();
            ClearEngagementHudBindings();
            ClearEmotionDeviceVisuals();
            ClearAudienceDeviceVisuals();
            ClearGraphDeviceVisuals();
            ClearAudienceGraphDeviceVisuals();
            ClearSelectedSensorStreamMode();
            ClearSensorEngagementBackend();
            ClearManualDriverVisuals();
        }

        private void OnDestroy()
        {
            if (_graphLayoutRefreshRoutine != null)
            {
                StopCoroutine(_graphLayoutRefreshRoutine);
                _graphLayoutRefreshRoutine = null;
            }

            _sessionController?.Dispose();
            _sessionController = null;
            ClearEngagementHudBindings();
            ClearEmotionDeviceVisuals();
            ClearAudienceDeviceVisuals();
            ClearGraphDeviceVisuals();
            ClearAudienceGraphDeviceVisuals();
            ClearSelectedSensorStreamMode();
            ClearSensorEngagementBackend();
            ClearManualDriverVisuals();
        }

        private void EnsureAnchorRegistry()
        {
            if (AnchorRegistry != null) return;
            AnchorRegistry = FindFirstObjectByType<AnchorRegistry>();
            if (AnchorRegistry != null) return;

            var registryGameObject = new GameObject("AnchorRegistry");
            AnchorRegistry = registryGameObject.AddComponent<AnchorRegistry>();
        }

        private void EnsureSources()
        {
            if (SineSource == null)
            {
                SineSource = FindFirstObjectByType<SineWaveSource>();
                if (SineSource == null && IsHost)
                {
                    var sourceGameObject = new GameObject("SineWaveSource");
                    SineSource = sourceGameObject.AddComponent<SineWaveSource>();
                }
            }

            if (LiveKitSource == null)
            {
                LiveKitSource = FindFirstObjectByType<LiveKitTelemetryReceiver>();
            }

            if (RedisSource == null)
            {
                RedisSource = FindFirstObjectByType<RedisDataPump>();
            }

            if (RedisManager == null)
            {
                RedisManager = FindFirstObjectByType<RedisManager>();
            }
        }

        private void EnsureVisualPrefab()
        {
            if (VisualPrefab != null) return;

            VisualPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            VisualPrefab.name = "SimpleVisualizerPrefab";

            if (VisualPrefab.GetComponent<SimpleVisualizer>() == null)
            {
                VisualPrefab.AddComponent<SimpleVisualizer>();
            }

            if (VisualPrefab.GetComponent<AnchoredVisualization>() == null)
            {
                VisualPrefab.AddComponent<AnchoredVisualization>();
            }
        }

        private IEnumerator BeginSession()
        {
            var options = new VisualizationSessionOptions
            {
                IsHost = IsHost,
                VisualId = VisualId,
                ProfileId = ProfileId,
                Seed = Seed,
                AnchorLabel = AnchorLabel,
                AnchorPosition = AnchorPosition,
                AnchorRotation = Quaternion.Euler(AnchorRotationEuler),
                SpawnTopic = SpawnTopic,
                ParamTopic = ParamTopic
            };

            yield return _sessionController.InitializeAsync(options).AsIEnumerator();

            if (IsHost && SineSource != null)
            {
                _sessionController.BindSignalSource(SineSource, _floatToParamsMapper);
            }

            switch (ActiveSignalSource)
            {
                case SignalSourceType.Sine:
                    if (SineSource != null)
                    {
                        _sessionController.BindSignalSource(SineSource, _floatToParamsMapper);
                    }
                    break;
                case SignalSourceType.Redis:
                    // If we're auto-spawning per-device emotion visuals, skip spawning the primary Redis visual to avoid duplicates.
                    bool shouldBindPrimaryRedis = !AutoSpawnEmotionVisualsFromChannels;
                    if (shouldBindPrimaryRedis && RedisSource != null && RedisSource.enabled)
                    {
                        _sessionController.BindExternalSignalSource(RedisSource, _floatToParamsMapper);
                    }
                    else if (shouldBindPrimaryRedis)
                    {
                        Debug.LogWarning("Bootstrapper: Remote server set to Redis but RedisDataPump not found.");
                    }
                    break;
                case SignalSourceType.LiveKit:
                    if (LiveKitSource != null)
                    {
                        _sessionController.BindTelemetrySource(LiveKitSource);
                    }
                    else
                    {
                        Debug.LogWarning("Bootstrapper: Remote server set to LiveKit but LiveKitTelemetryReceiver not found.");
                    }
                    break;
                default:
                    break;
            }
        }

        private void ApplySignalConfiguration()
        {
            if (UseManualVisualizationDrivers)
            {
                ApplyManualDriverConfiguration();
                return;
            }

            ClearManualDriverVisuals();
            EnsureRedisEndpointFromRuntime();

            bool hasRedisEndpoint = !string.IsNullOrWhiteSpace(RedisHost);
            bool useSine = ActiveSignalSource == SignalSourceType.Sine;
            bool useRedis = ActiveSignalSource == SignalSourceType.Redis;
            bool useLiveKit = ActiveSignalSource == SignalSourceType.LiveKit;

            if (SineSource != null)
            {
                SineSource.enabled = useSine;
            }

            var redisChannels = GetRedisChannels();
            var primaryRedisChannel = redisChannels[0];

            if (useRedis && !hasRedisEndpoint)
            {
                Debug.LogWarning("Bootstrapper: Redis host is empty; skipping Redis configuration. Enter a host via the endpoint prompt.");
                DisableRedisComponents();
            }

            if (useRedis && RequireRedisEndpointConfirmation && !_redisEndpointReady)
            {
                Debug.Log("Bootstrapper: Waiting for Redis endpoint confirmation before connecting.");
                DisableRedisComponents();
            }

            bool redisReady = !RequireRedisEndpointConfirmation || _redisEndpointReady;

            if (RedisSource != null && hasRedisEndpoint && redisReady)
            {
                Debug.Log($"Bootstrapper: Configuring RedisSource for {RedisHost}:{RedisPort} ({primaryRedisChannel})");
                RedisSource.ConfigureConnection(RedisHost, RedisPort, primaryRedisChannel);
                RedisSource.enabled = useRedis;
            }

            if (RedisManager != null && hasRedisEndpoint && redisReady)
            {
                RedisManager.SetEmotionDeviceIds(EmotionDeviceIds);
                RedisManager.Configure(RedisHost, RedisPort, redisChannels);
                RedisManager.enabled = useRedis;
            }

            ConfigureEngagementHudBindings(useRedis, hasRedisEndpoint, redisReady);

            if (LiveKitSource != null)
            {
                LiveKitSource.tokenEndpoint = LiveKitTokenEndpoint;
                LiveKitSource.sandboxId = LiveKitSandboxId;
                LiveKitSource.roomName = LiveKitRoomName;
                LiveKitSource.identity = LiveKitIdentity;
                LiveKitSource.enabled = useLiveKit;
            }

            bool visualPrefabHasAudienceBinding = VisualPrefab != null && VisualPrefab.GetComponentInChildren<AudienceSignalBinding>() != null;
            bool shouldSpawnAudienceVisuals = useRedis && hasRedisEndpoint && redisReady && AutoSpawnAudienceVisualsFromChannels && visualPrefabHasAudienceBinding;
            bool graphPrefabHasAudienceBinding = GraphPrefab != null && GraphPrefab.GetComponentInChildren<AudienceSignalBinding>() != null;
            bool shouldSpawnAudienceGraphVisuals = useRedis && hasRedisEndpoint && redisReady && AutoSpawnGraphVisualsFromChannels && graphPrefabHasAudienceBinding;

            if (shouldSpawnAudienceVisuals || shouldSpawnAudienceGraphVisuals)
            {
                var sensorDefinitions = BuildAudienceDeviceChannelDefinitions(redisChannels);
                sensorDefinitions = FilterAudienceToConfiguredDevices(sensorDefinitions, EmotionDeviceIds);
                EnsureSensorEngagementBackend(sensorDefinitions.Select(definition => definition.DeviceId));
            }
            else
            {
                ClearSensorEngagementBackend();
            }

            bool shouldUseSelectedSensorStream = UseSingleSelectedSensorStream &&
                (shouldSpawnAudienceVisuals || shouldSpawnAudienceGraphVisuals);
            if (shouldUseSelectedSensorStream)
            {
                ClearAudienceDeviceVisuals();
                ClearAudienceGraphDeviceVisuals();
                ClearEmotionDeviceVisuals();
                ClearGraphDeviceVisuals();
                EnsureSelectedSensorStreamMode(redisChannels, shouldSpawnAudienceVisuals, shouldSpawnAudienceGraphVisuals);
                return;
            }

            ClearSelectedSensorStreamMode();

            if (shouldSpawnAudienceVisuals)
            {
                EnsureAudienceDeviceVisuals(redisChannels);
                ClearEmotionDeviceVisuals();
            }
            else
            {
                ClearAudienceDeviceVisuals();
            }

            bool shouldSpawnEmotionVisuals = useRedis && hasRedisEndpoint && redisReady && AutoSpawnEmotionVisualsFromChannels && !shouldSpawnAudienceVisuals;
            if (shouldSpawnEmotionVisuals)
            {
                EnsureEmotionDeviceVisuals(redisChannels);
            }
            else
            {
                ClearEmotionDeviceVisuals();
            }

            if (shouldSpawnAudienceGraphVisuals)
            {
                EnsureAudienceGraphDeviceVisuals(redisChannels);
                ClearGraphDeviceVisuals();
            }
            else
            {
                ClearAudienceGraphDeviceVisuals();
            }

            bool shouldSpawnGraphVisuals = useRedis && hasRedisEndpoint && redisReady && AutoSpawnGraphVisualsFromChannels && !shouldSpawnAudienceGraphVisuals;
            if (shouldSpawnGraphVisuals)
            {
                EnsureGraphDeviceVisuals(redisChannels);
            }
            else
            {
                ClearGraphDeviceVisuals();
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

        private bool ShouldAutoDetectEmotionDeviceIds()
        {
            if (UseManualVisualizationDrivers)
            {
                return false;
            }

            if (!AutoDetectEmotionDeviceIds || !IncludeEmotionChannels)
            {
                return false;
            }

            if (ActiveSignalSource != SignalSourceType.Redis)
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
            if (_deviceDiscoveryRoutine != null)
            {
                StopCoroutine(_deviceDiscoveryRoutine);
                _deviceDiscoveryRoutine = null;
            }

            _deviceDiscoveryRoutine = StartCoroutine(DiscoverEmotionDevicesThenApply());
        }

        private IEnumerator DiscoverEmotionDevicesThenApply()
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
                    EmotionDeviceIds = discovered.ToArray();
                    Debug.Log($"Bootstrapper: Discovered {discovered.Count} emotion devices from Redis.");
                }
            }

            ApplySignalConfiguration();
            BeginSessionIfReady();
            _deviceDiscoveryRoutine = null;
        }

        private void BeginSessionIfReady()
        {
            if (!ShouldInitializeSessionController())
            {
                return;
            }

            if (_initializationRoutine != null)
            {
                StopCoroutine(_initializationRoutine);
                _initializationRoutine = null;
            }

            _initializationRoutine = StartCoroutine(BeginSession());
        }

        private void ApplyManualDriverConfiguration()
        {
            if (SineSource != null)
            {
                SineSource.enabled = false;
            }

            if (RedisSource != null)
            {
                RedisSource.enabled = false;
            }

            if (RedisManager != null)
            {
                RedisManager.enabled = false;
            }

            if (LiveKitSource != null)
            {
                LiveKitSource.enabled = false;
            }

            ClearEngagementHudBindings();
            ClearEmotionDeviceVisuals();
            ClearAudienceDeviceVisuals();
            ClearGraphDeviceVisuals();
            ClearAudienceGraphDeviceVisuals();
            ClearSelectedSensorStreamMode();
            ClearSensorEngagementBackend();
            EnsureManualDriverVisuals();
        }

        private void EnsureManualDriverVisuals()
        {
            if (_manualDriverInstances.Count > 0)
            {
                return;
            }

            SpawnManualDriverPrefab(VisualPrefab, EmotionVisualParent, "ManualVisual", 0, isGraphPrefab: false);
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
                PositionEmotionVisual(instance.transform, index, hasCustomParent);
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

            var graphBindings = instance.GetComponentsInChildren<GraphBinding>(true);
            foreach (var binding in graphBindings)
            {
                binding.enabled = false;
            }

            var emotionPlasmaBindings = instance.GetComponentsInChildren<EmotionPlasmaBinding>(true);
            foreach (var binding in emotionPlasmaBindings)
            {
                binding.enabled = false;
            }

            var emotionRayBindings = instance.GetComponentsInChildren<EmotionRayPlasmaBinding>(true);
            foreach (var binding in emotionRayBindings)
            {
                binding.enabled = false;
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
            if (!UnityEngine.Application.isPlaying)
            {
                return;
            }

            ApplySignalConfiguration();
        }
#endif

        private void EnsureEmotionDeviceVisuals(string[] redisChannels)
        {
            if (!AutoSpawnEmotionVisualsFromChannels)
            {
                return;
            }

            if (VisualPrefab == null)
            {
                Debug.LogWarning("Bootstrapper: VisualPrefab is missing, cannot spawn EmotionPlasma instances.");
                ClearEmotionDeviceVisuals();
                return;
            }

            if (redisChannels == null || redisChannels.Length == 0)
            {
                ClearEmotionDeviceVisuals();
                return;
            }

            bool hasSupportedBinding =
                VisualPrefab.GetComponentInChildren<EmotionPlasmaBinding>() != null ||
                VisualPrefab.GetComponentInChildren<EmotionRayPlasmaBinding>() != null;

            if (!hasSupportedBinding)
            {
                Debug.LogWarning("Bootstrapper: VisualPrefab does not contain an EmotionPlasmaBinding or EmotionRayPlasmaBinding component.");
                ClearEmotionDeviceVisuals();
                return;
            }

            var deviceDefinitions = BuildDeviceChannelDefinitions(redisChannels);
            deviceDefinitions = FilterToConfiguredDevices(deviceDefinitions, EmotionDeviceIds);
            ClearEmotionDeviceVisuals();

            if (deviceDefinitions.Count == 0)
            {
                Debug.LogWarning("Bootstrapper: No device-specific valence/arousal channels were found.");
                return;
            }

            Debug.Log($"Bootstrapper: Spawning emotion visuals for {deviceDefinitions.Count} devices: {string.Join(", ", deviceDefinitions.Select(d => d.DeviceId))}");

            bool hasCustomParent = EmotionVisualParent != null;
            int deviceIndex = 0;
            foreach (var definition in deviceDefinitions)
            {
                if (string.IsNullOrEmpty(definition.DeviceId) ||
                    string.IsNullOrEmpty(definition.ValenceChannel) ||
                    string.IsNullOrEmpty(definition.ArousalChannel))
                {
                    Debug.LogWarning($"Bootstrapper: Incomplete channel set for device '{definition.DeviceId}', skipping visual spawn.");
                    continue;
                }

                var instance = hasCustomParent
                    ? Instantiate(VisualPrefab, EmotionVisualParent)
                    : Instantiate(VisualPrefab);

                instance.name = $"{VisualPrefab.name}_{definition.DeviceId}";
                PositionEmotionVisual(instance.transform, deviceIndex, hasCustomParent);

                DisableManualDrivers(instance);

                var bindingComponent = FindEmotionBindingComponent(instance) ?? instance.AddComponent<EmotionPlasmaBinding>();

                string valenceChannel = definition.ValenceChannel;
                string arousalChannel = definition.ArousalChannel;
                string heartRateChannel = definition.HeartRateChannel;

                var valencePump = AcquireRedisPumpForChannel(definition.DeviceId, EmotionChannelKind.Valence, valenceChannel);
                var arousalPump = AcquireRedisPumpForChannel(definition.DeviceId, EmotionChannelKind.Arousal, arousalChannel);
                RedisDataPump heartRatePump = null;
                if (!string.IsNullOrWhiteSpace(heartRateChannel))
                {
                    heartRatePump = AcquireRedisPumpForChannel(definition.DeviceId, EmotionChannelKind.HeartRate, heartRateChannel);
                }

                if (!TryConfigureEmotionBinding(bindingComponent, valencePump, arousalPump, heartRatePump, definition.DeviceId))
                {
                    Debug.LogWarning($"Bootstrapper: Unable to configure emotion binding on '{instance.name}', skipping visual spawn.");
                    Destroy(instance);
                    ReleaseRedisPump(valenceChannel);
                    ReleaseRedisPump(arousalChannel);
                    ReleaseRedisPump(heartRateChannel);
                    continue;
                }

                _emotionDeviceInstances.Add(new EmotionDeviceInstance
                {
                    DeviceId = definition.DeviceId,
                    VisualInstance = instance,
                    BindingComponent = bindingComponent,
                    ValenceChannel = valenceChannel,
                    ArousalChannel = arousalChannel,
                    HeartRateChannel = heartRateChannel,
                    ValencePump = valencePump,
                    ArousalPump = arousalPump,
                    HeartRatePump = heartRatePump
                });

                AttachDeviceLabel(instance, definition.DeviceId, deviceIndex);
                deviceIndex++;
            }

            if (deviceIndex == 0)
            {
                Debug.LogWarning("Bootstrapper: Unable to spawn emotion visuals because no device had both valence and arousal channels.");
            }
        }

        private void EnsureSelectedSensorStreamMode(
            string[] redisChannels,
            bool spawnParticleVisual,
            bool spawnGraphVisual)
        {
            ClearSelectedSensorStreamMode();

            var deviceDefinitions = BuildAudienceDeviceChannelDefinitions(redisChannels);
            deviceDefinitions = FilterAudienceToConfiguredDevices(deviceDefinitions, EmotionDeviceIds);
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
                    VisualPrefab,
                    EmotionVisualParent,
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

            var visualizerBindings = new List<AudienceSignalVisualizerBinding>();
            AddVisualizerBindings(_selectedParticleVisual, visualizerBindings);
            AddVisualizerBindings(_selectedGraphVisual, visualizerBindings);

            EnsureSensorStreamPresentation(
                _selectedSensorSources.Select(source => source.Binding),
                visualizerBindings);

            Debug.Log($"Bootstrapper: Configured one persistent visualization target for {_selectedSensorSources.Count} selectable sensor streams.");
        }

        private GameObject SpawnSelectedSensorVisual(
            GameObject prefab,
            Transform parent,
            string suffix,
            bool isGraphVisual)
        {
            if (prefab == null)
            {
                return null;
            }

            bool hasCustomParent = parent != null;
            var instance = hasCustomParent ? Instantiate(prefab, parent) : Instantiate(prefab);
            instance.name = $"{prefab.name}_{suffix}";

            if (isGraphVisual)
            {
                PositionGraphGroup(instance.transform, 0, hasCustomParent);
            }
            else
            {
                PositionEmotionVisual(instance.transform, 0, hasCustomParent);
            }

            DisableManualDrivers(instance);

            foreach (var sourceBinding in instance.GetComponentsInChildren<AudienceSignalBinding>(true))
            {
                sourceBinding.enabled = false;
                sourceBinding.ConfigureParticleTarget(null);
            }

            foreach (var legacyGraphBinding in instance.GetComponentsInChildren<GraphBinding>(true))
            {
                legacyGraphBinding.enabled = false;
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
            if (!AutoSpawnAudienceVisualsFromChannels)
            {
                return;
            }

            if (VisualPrefab == null)
            {
                Debug.LogWarning("Bootstrapper: VisualPrefab is missing, cannot spawn audience signal instances.");
                ClearAudienceDeviceVisuals();
                return;
            }

            if (redisChannels == null || redisChannels.Length == 0)
            {
                ClearAudienceDeviceVisuals();
                return;
            }

            bool hasAudienceBinding = VisualPrefab.GetComponentInChildren<AudienceSignalBinding>() != null;
            if (!hasAudienceBinding)
            {
                ClearAudienceDeviceVisuals();
                return;
            }

            var deviceDefinitions = BuildAudienceDeviceChannelDefinitions(redisChannels);
            deviceDefinitions = FilterAudienceToConfiguredDevices(deviceDefinitions, EmotionDeviceIds);
            ClearAudienceDeviceVisuals();

            if (deviceDefinitions.Count == 0)
            {
                Debug.LogWarning("Bootstrapper: No device-specific audience signal channels were found.");
                return;
            }

            Debug.Log($"Bootstrapper: Spawning audience visuals for {deviceDefinitions.Count} devices: {string.Join(", ", deviceDefinitions.Select(d => d.DeviceId))}");

            bool hasCustomParent = EmotionVisualParent != null;
            int deviceIndex = 0;
            foreach (var definition in deviceDefinitions)
            {
                if (!definition.HasVisualizationChannels)
                {
                    Debug.LogWarning($"Bootstrapper: Incomplete audience channel set for device '{definition.DeviceId}', skipping visual spawn.");
                    continue;
                }

                var instance = hasCustomParent
                    ? Instantiate(VisualPrefab, EmotionVisualParent)
                    : Instantiate(VisualPrefab);

                instance.name = $"{VisualPrefab.name}_{definition.DeviceId}";
                PositionEmotionVisual(instance.transform, deviceIndex, hasCustomParent);

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
                    DeviceId = definition.DeviceId,
                    VisualInstance = instance,
                    Binding = binding,
                    PhysioMetricsChannel = definition.PhysioMetricsChannel,
                    PhysioPump = physioPump
                });

                AttachDeviceLabel(instance, definition.DeviceId, deviceIndex);
                deviceIndex++;
            }

            if (deviceIndex == 0)
            {
                Debug.LogWarning("Bootstrapper: Unable to spawn audience visuals because no device had the required channels.");
            }
        }

        private void EnsureGraphDeviceVisuals(string[] redisChannels)
        {
            if (!AutoSpawnGraphVisualsFromChannels)
            {
                return;
            }

            if (GraphPrefab == null)
            {
                Debug.LogWarning("Bootstrapper: GraphPrefab is missing, cannot spawn graph instances.");
                ClearGraphDeviceVisuals();
                return;
            }

            if (redisChannels == null || redisChannels.Length == 0)
            {
                ClearGraphDeviceVisuals();
                return;
            }

            bool hasGraphBinding = GraphPrefab.GetComponentInChildren<GraphBinding>() != null;
            bool hasGraphVisualizer = GraphPrefab.GetComponentInChildren<GraphVisualizer>() != null;
            if (!hasGraphBinding || !hasGraphVisualizer)
            {
                Debug.LogWarning("Bootstrapper: GraphPrefab must include GraphBinding and GraphVisualizer components.");
                ClearGraphDeviceVisuals();
                return;
            }

            var deviceDefinitions = BuildDeviceChannelDefinitions(redisChannels);
            deviceDefinitions = FilterToConfiguredDevices(deviceDefinitions, EmotionDeviceIds);
            ClearGraphDeviceVisuals();

            if (deviceDefinitions.Count == 0)
            {
                Debug.LogWarning("Bootstrapper: No device-specific channels were found for graphs.");
                return;
            }

            Debug.Log($"Bootstrapper: Spawning graph groups for {deviceDefinitions.Count} devices: {string.Join(", ", deviceDefinitions.Select(d => d.DeviceId))}");

            bool hasCustomParent = GraphGroupParent != null;
            int deviceIndex = 0;
            foreach (var definition in deviceDefinitions)
            {
                if (string.IsNullOrWhiteSpace(definition.DeviceId))
                {
                    Debug.LogWarning("Bootstrapper: Encountered device definition with empty id, skipping graph spawn.");
                    continue;
                }

                var groupRoot = new GameObject($"GraphGroup_{definition.DeviceId}");
                if (hasCustomParent)
                {
                    groupRoot.transform.SetParent(GraphGroupParent, false);
                }

                PositionGraphGroup(groupRoot.transform, deviceIndex, hasCustomParent);

                var offsetsInUse = new List<Vector3>(2);
                var instance = new GraphDeviceGroupInstance
                {
                    DeviceId = definition.DeviceId,
                    DeviceIndex = deviceIndex,
                    GroupRoot = groupRoot
                };

                string deviceLabel = FormatDeviceLabel(definition.DeviceId, deviceIndex);
                Vector3 heartRateOffset = ResolveGraphChannelOffset(EmotionChannelKind.HeartRate);
                Vector3 edaOffset = ResolveGraphChannelOffset(EmotionChannelKind.EdaFiltered);

                if (!string.IsNullOrWhiteSpace(definition.HeartRateChannel))
                {
                    string heartRateChannel = definition.HeartRateChannel;
                    instance.HeartRateGraph = SpawnGraphForChannel(
                        groupRoot.transform,
                        definition.DeviceId,
                        EmotionChannelKind.HeartRate,
                        heartRateChannel,
                        heartRateOffset,
                        GraphHeartRateRange,
                        GraphHeartRateColor,
                        GraphLineWidth,
                        $"{deviceLabel} - Heart Rate",
                        out instance.HeartRatePump);
                    if (instance.HeartRateGraph != null)
                    {
                        instance.HeartRateChannel = heartRateChannel;
                        offsetsInUse.Add(heartRateOffset);
                        ApplyGraphHeightScale(instance.HeartRateGraph);
                        AttachGraphPanel(instance.HeartRateGraph);
                        AttachGraphLabel(groupRoot, instance.HeartRateGraph, heartRateOffset, ResolveGraphChannelLabel(EmotionChannelKind.HeartRate));
                    }
                }

                if (!string.IsNullOrWhiteSpace(definition.EdaChannel))
                {
                    string edaChannel = definition.EdaChannel;
                    instance.EdaGraph = SpawnGraphForChannel(
                        groupRoot.transform,
                        definition.DeviceId,
                        EmotionChannelKind.EdaFiltered,
                        edaChannel,
                        edaOffset,
                        GraphEdaRange,
                        GraphEdaColor,
                        GraphLineWidth,
                        $"{deviceLabel} - EDA",
                        out instance.EdaPump);
                    if (instance.EdaGraph != null)
                    {
                        instance.EdaChannel = edaChannel;
                        offsetsInUse.Add(edaOffset);
                        ApplyGraphHeightScale(instance.EdaGraph);
                        AttachGraphPanel(instance.EdaGraph);
                        AttachGraphLabel(groupRoot, instance.EdaGraph, edaOffset, ResolveGraphChannelLabel(EmotionChannelKind.EdaFiltered));
                    }
                }

                if (instance.HeartRateGraph == null && instance.EdaGraph == null)
                {
                    Debug.LogWarning($"Bootstrapper: No graph channels found for device '{definition.DeviceId}', skipping group spawn.");
                    Destroy(groupRoot);
                    ReleaseRedisPump(instance.HeartRateChannel);
                    ReleaseRedisPump(instance.EdaChannel);
                    continue;
                }

                AttachGraphGroupPanel(groupRoot, offsetsInUse);
                AttachGraphGroupLabel(groupRoot, definition.DeviceId, deviceIndex, offsetsInUse);
                _graphDeviceGroups.Add(instance);
                deviceIndex++;
            }

            RequestGraphLayoutRefresh();
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
            deviceDefinitions = FilterAudienceToConfiguredDevices(deviceDefinitions, EmotionDeviceIds);
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

                var legacyGraphBindings = instance.GetComponentsInChildren<GraphBinding>(true);
                foreach (var legacyBinding in legacyGraphBindings)
                {
                    legacyBinding.enabled = false;
                }

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
                    DeviceId = definition.DeviceId,
                    DeviceIndex = deviceIndex,
                    GroupRoot = instance,
                    Binding = binding,
                    PhysioMetricsChannel = definition.PhysioMetricsChannel,
                    PhysioPump = physioPump
                });

                AttachGraphGroupLabel(instance, definition.DeviceId, deviceIndex, Array.Empty<Vector3>());
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
                $"score={SensorEngagementPayloadFormat.EngagementPropertyName};confirmed={SensorEngagementPayloadFormat.ConfirmationPropertyName}";
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

        private RedisDataPump AcquireRedisPumpForChannel(string deviceId, EmotionChannelKind channelKind, string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
            {
                return null;
            }

            if (_sharedPumpsByChannel.TryGetValue(channelName, out var handle) && handle.Pump != null)
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

            var pumpObject = new GameObject($"RedisPump_{deviceId}_{channelKind}");
            pumpObject.transform.SetParent(transform, false);
            pumpObject.SetActive(false);

            var pump = pumpObject.AddComponent<RedisDataPump>();
            pump.ConfigureConnection(RedisHost, RedisPort, channelName);

            pumpObject.SetActive(true);

            _sharedPumpsByChannel[channelName] = new SharedPumpHandle
            {
                ChannelName = channelName,
                Host = RedisHost,
                Port = RedisPort,
                Pump = pump,
                ReferenceCount = 1
            };

            return pump;
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
                ChannelName = channelName,
                Host = RedisHost,
                Port = RedisPort,
                Pump = pump,
                ReferenceCount = 1
            };

            return pump;
        }

        private void ReleaseRedisPump(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
            {
                return;
            }

            if (!_sharedPumpsByChannel.TryGetValue(channelName, out var handle))
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

            _sharedPumpsByChannel.Remove(channelName);
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

        private Component FindEmotionBindingComponent(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            var plasmaBinding = target.GetComponentInChildren<EmotionPlasmaBinding>();
            if (plasmaBinding != null)
            {
                return plasmaBinding;
            }

            return target.GetComponentInChildren<EmotionRayPlasmaBinding>();
        }

        private static bool TryConfigureEmotionBinding(Component bindingComponent, RedisDataPump valencePump, RedisDataPump arousalPump, RedisDataPump heartRatePump, string deviceId)
        {
            if (bindingComponent == null)
            {
                return false;
            }

            if (bindingComponent is EmotionPlasmaBinding plasmaBinding)
            {
                plasmaBinding.ConfigureSources(valencePump, arousalPump, deviceId);
                return true;
            }

            if (bindingComponent is EmotionRayPlasmaBinding rayBinding)
            {
                rayBinding.ConfigureSources(valencePump, arousalPump, heartRatePump, deviceId);
                return true;
            }

            return false;
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

        private void ClearEmotionDeviceVisuals()
        {
            if (_emotionDeviceInstances.Count == 0)
            {
                return;
            }

            foreach (var instance in _emotionDeviceInstances)
            {
                if (instance.VisualInstance != null)
                {
                    Destroy(instance.VisualInstance);
                }
                else if (instance.BindingComponent != null)
                {
                    Destroy(instance.BindingComponent.gameObject);
                }

                ReleaseRedisPump(instance.ValenceChannel);
                ReleaseRedisPump(instance.ArousalChannel);
                ReleaseRedisPump(instance.HeartRateChannel);
            }

            _emotionDeviceInstances.Clear();
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

        private void ClearGraphDeviceVisuals()
        {
            if (_graphDeviceGroups.Count == 0)
            {
                return;
            }

            foreach (var instance in _graphDeviceGroups)
            {
                if (instance.GroupRoot != null)
                {
                    Destroy(instance.GroupRoot);
                }
                else
                {
                    if (instance.HeartRateGraph != null) Destroy(instance.HeartRateGraph);
                    if (instance.EdaGraph != null) Destroy(instance.EdaGraph);
                }

                ReleaseRedisPump(instance.HeartRateChannel);
                ReleaseRedisPump(instance.EdaChannel);
            }

            _graphDeviceGroups.Clear();
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

        private void DisableRedisComponents()
        {
            if (RedisSource != null)
            {
                RedisSource.enabled = false;
            }

            if (RedisManager != null)
            {
                RedisManager.enabled = false;
            }

            ClearEmotionDeviceVisuals();
            ClearAudienceDeviceVisuals();
            ClearGraphDeviceVisuals();
            ClearAudienceGraphDeviceVisuals();
            ClearSelectedSensorStreamMode();
            ClearSensorEngagementBackend();
            ClearManualDriverVisuals();
        }

        private void ConfigureEngagementHudBindings(bool useRedis, bool hasRedisEndpoint, bool redisReady)
        {
            if (!useRedis || !hasRedisEndpoint || !redisReady)
            {
                ClearEngagementHudBindings();
                return;
            }

            string channel = ResolveEngagementScoresChannel();
            if (_engagementHudPump == null || !string.Equals(_engagementHudChannel, channel, StringComparison.Ordinal))
            {
                ClearEngagementHudBindings();
                _engagementHudPump = AcquireRedisPumpForChannel("global", EmotionChannelKind.Broadcast, channel);
                _engagementHudChannel = channel;
            }
            else
            {
                _engagementHudPump.ConfigureConnection(RedisHost, RedisPort, channel);
            }

            if (_engagementHudPump == null)
            {
                return;
            }

            var hudBindings = FindObjectsByType<EngagementHudBinding>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var binding in hudBindings)
            {
                if (binding != null)
                {
                    binding.ConfigureSource(_engagementHudPump);
                }
            }
        }

        private void ClearEngagementHudBindings()
        {
            if (_engagementHudPump == null)
            {
                return;
            }

            var hudBindings = FindObjectsByType<EngagementHudBinding>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var binding in hudBindings)
            {
                if (binding != null)
                {
                    binding.ConfigureSource(null);
                }
            }

            ReleaseRedisPump(_engagementHudChannel);
            _engagementHudPump = null;
            _engagementHudChannel = string.Empty;
        }

        private void PositionEmotionVisual(Transform target, int index, bool useLocalSpace)
        {
            if (target == null)
            {
                return;
            }

            int perRow = Mathf.Max(1, EmotionVisualsPerRow);
            int row = index / perRow;
            int column = index % perRow;

            Vector3 offset = EmotionVisualOriginOffset + new Vector3(
                column * EmotionVisualSpacing.x,
                row * EmotionVisualSpacing.y,
                row * EmotionVisualSpacing.z);

            if (useLocalSpace)
            {
                target.localPosition = offset;
                target.localRotation = Quaternion.identity;
            }
            else
            {
                target.position = AnchorPosition + offset;
                target.rotation = Quaternion.Euler(AnchorRotationEuler);
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
                target.position = AnchorPosition + offset;
                target.rotation = Quaternion.Euler(AnchorRotationEuler);
            }
        }

        private Vector3 ResolveGraphChannelOffset(EmotionChannelKind channelKind)
        {
            if (GraphChannelLayout != GraphChannelLayoutMode.Stacked)
            {
                return channelKind switch
                {
                    EmotionChannelKind.HeartRate => GraphHeartRateOffset,
                    EmotionChannelKind.EdaFiltered => GraphEdaOffset,
                    _ => Vector3.zero
                };
            }

            float spacing = Mathf.Max(0.01f, GraphStackedVerticalSpacing);
            float yOffset = channelKind switch
            {
                EmotionChannelKind.HeartRate => spacing * 0.5f,
                EmotionChannelKind.EdaFiltered => -spacing * 0.5f,
                _ => 0f
            };

            return GraphStackedOriginOffset + new Vector3(0f, yOffset, 0f);
        }

        private GameObject SpawnGraphForChannel(
            Transform parent,
            string deviceId,
            EmotionChannelKind channelKind,
            string channelName,
            Vector3 localOffset,
            Vector2 yRange,
            Color lineColor,
            float lineWidth,
            string label,
            out RedisDataPump pump)
        {
            pump = null;

            if (GraphPrefab == null || parent == null || string.IsNullOrWhiteSpace(channelName))
            {
                return null;
            }

            var instance = Instantiate(GraphPrefab, parent);
            instance.name = $"{GraphPrefab.name}_{deviceId}_{channelKind}";
            instance.transform.localPosition = localOffset;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            var binding = instance.GetComponentInChildren<GraphBinding>();
            var visualizer = instance.GetComponentInChildren<GraphVisualizer>();

            if (binding == null || visualizer == null)
            {
                Debug.LogWarning($"Bootstrapper: GraphPrefab instance missing GraphBinding or GraphVisualizer for {deviceId} {channelKind}.");
                Destroy(instance);
                return null;
            }

            var manualDriver = instance.GetComponentInChildren<GraphManualDriver>();
            if (manualDriver != null)
            {
                manualDriver.enabled = false;
            }

            pump = AcquireRedisPumpForChannel(deviceId, channelKind, channelName);
            if (pump == null)
            {
                Destroy(instance);
                return null;
            }
            binding.enabled = true;
            binding.ConfigureSource(pump, visualizer, label, yRange, lineColor, lineWidth);

            // GraphBinding.ApplyLayout() can reset the transform to its internal layout positions.
            // Re-apply the channel offset so Bootstrapper-controlled layouts remain in effect.
            instance.transform.localPosition = localOffset;
            return instance;
        }

        private bool TryCalculateGraphGroupBounds(IReadOnlyList<Vector3> channelOffsets, out Vector3 center, out Vector3 size)
        {
            center = Vector3.zero;
            size = GraphItemBoundsSize;

            if (GraphItemBoundsSize.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            if (channelOffsets == null || channelOffsets.Count == 0)
            {
                size += GraphGroupBoundsPadding * 2f;
                return true;
            }

            Vector3 halfSize = GraphItemBoundsSize * 0.5f;
            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            foreach (var offset in channelOffsets)
            {
                Vector3 localMin = offset - halfSize;
                Vector3 localMax = offset + halfSize;
                min = Vector3.Min(min, localMin);
                max = Vector3.Max(max, localMax);
            }

            size = (max - min) + GraphGroupBoundsPadding * 2f;
            center = (min + max) * 0.5f;
            return true;
        }

        private static List<DeviceChannelDefinition> BuildDeviceChannelDefinitions(IEnumerable<string> redisChannels)
        {
            var orderedDeviceIds = new List<string>();
            var map = new Dictionary<string, DeviceChannelDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (var channel in redisChannels)
            {
                if (!RedisEmotionChannels.TryParseDeviceChannel(channel, out var deviceId, out var kind))
                {
                    continue;
                }

                if (!map.TryGetValue(deviceId, out var definition))
                {
                    definition = new DeviceChannelDefinition { DeviceId = deviceId };
                    orderedDeviceIds.Add(deviceId);
                }

                if (kind == EmotionChannelKind.Valence)
                {
                    definition.ValenceChannel = channel;
                }
                else if (kind == EmotionChannelKind.Arousal)
                {
                    definition.ArousalChannel = channel;
                }
                else if (kind == EmotionChannelKind.HeartRate)
                {
                    definition.HeartRateChannel = channel;
                }
                else if (kind == EmotionChannelKind.EdaFiltered)
                {
                    definition.EdaChannel = channel;
                }

                map[deviceId] = definition;
            }

            var ordered = new List<DeviceChannelDefinition>();
            foreach (var deviceId in orderedDeviceIds)
            {
                if (map.TryGetValue(deviceId, out var definition))
                {
                    ordered.Add(definition);
                }
            }

            return ordered;
        }

        private static List<DeviceChannelDefinition> FilterToConfiguredDevices(IEnumerable<DeviceChannelDefinition> definitions, IEnumerable<string> configuredDeviceIds)
        {
            if (configuredDeviceIds == null)
            {
                return new List<DeviceChannelDefinition>(definitions);
            }

            var allowed = new HashSet<string>(configuredDeviceIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()), StringComparer.OrdinalIgnoreCase);
            if (allowed.Count == 0)
            {
                return new List<DeviceChannelDefinition>(definitions);
            }

            var filtered = new List<DeviceChannelDefinition>();
            foreach (var definition in definitions)
            {
                if (!string.IsNullOrWhiteSpace(definition.DeviceId) && allowed.Contains(definition.DeviceId))
                {
                    filtered.Add(definition);
                }
            }

            return filtered;
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

                FillAudienceChannelDefaults(ref definition);
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

        private static void FillAudienceChannelDefaults(ref AudienceDeviceChannelDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(definition.PhysioMetricsChannel))
            {
                definition.PhysioMetricsChannel = RedisAudienceChannels.FormatPhysioMetricsChannel(definition.DeviceId);
            }
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

            if (IncludeEmotionChannels)
            {
                foreach (var channel in RedisEmotionChannels.BuildChannelList(EmotionDeviceIds, includeBroadcast: false))
                {
                    AddChannel(channel);
                }

                if (EmotionDeviceIds != null)
                {
                    foreach (var deviceId in EmotionDeviceIds)
                    {
                        AddChannel(RedisAudienceChannels.FormatPhysioMetricsChannel(deviceId));
                    }
                }
            }

            AddChannel(ResolveEngagementScoresChannel());

            if (channels.Count == 0)
            {
                AddChannel(RedisEmotionChannels.BroadcastChannel);
            }

            return channels.ToArray();
        }

        private string ResolveEngagementScoresChannel()
        {
            if (RedisAudienceChannels.IsEngagementScoresChannel(RedisChannel))
            {
                return RedisChannel.Trim();
            }

            if (RedisChannels != null)
            {
                foreach (var channel in RedisChannels)
                {
                    if (RedisAudienceChannels.IsEngagementScoresChannel(channel))
                    {
                        return channel.Trim();
                    }
                }
            }

            return RedisAudienceChannels.EngagementScoresChannel;
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

        private void AttachGraphLabel(GameObject groupRoot, GameObject graphInstance, Vector3 channelOffset, string labelText)
        {
            if (!ShowGraphLabels || graphInstance == null || string.IsNullOrWhiteSpace(labelText))
            {
                return;
            }

            float halfWidth = GraphItemBoundsSize.x * 0.5f;
            var graphVisualizer = graphInstance.GetComponentInChildren<GraphVisualizer>();
            if (graphVisualizer != null)
            {
                halfWidth = graphVisualizer.GraphSize.x * 0.5f;
            }

            float xOffset = -halfWidth - Mathf.Max(0f, GraphLabelHorizontalPadding);
            Vector3 baseOffset = new Vector3(xOffset, GraphLabelVerticalOffset, GraphLabelDepthOffset);

            var labelObject = new GameObject("GraphLabel");
            var parent = groupRoot != null ? groupRoot.transform : graphInstance.transform;
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = channelOffset + baseOffset + GraphLabelOffset;
            labelObject.transform.localRotation = Quaternion.identity;
            labelObject.transform.localScale = Vector3.one;
            labelObject.layer = graphInstance.layer;

            var text = labelObject.AddComponent<TextMeshPro>();
            text.text = labelText.Trim();
            text.fontSize = Mathf.Max(0.01f, GraphLabelFontSize);
            text.color = GraphLabelColor;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.richText = false;

            labelObject.AddComponent<BillboardLabel>();
        }

        private void RequestGraphLayoutRefresh()
        {
            if (_graphLayoutRefreshRoutine != null)
            {
                StopCoroutine(_graphLayoutRefreshRoutine);
            }

            _graphLayoutRefreshRoutine = StartCoroutine(RefreshGraphLayoutForFrames(3));
        }

        private IEnumerator RefreshGraphLayoutForFrames(int frameCount)
        {
            int remaining = Mathf.Max(1, frameCount);
            while (remaining-- > 0)
            {
                yield return null;
                ApplyGraphLayoutToGroups();
            }
            _graphLayoutRefreshRoutine = null;
        }

        private void ApplyGraphLayoutToGroups()
        {
            if (_graphDeviceGroups.Count == 0)
            {
                return;
            }

            foreach (var instance in _graphDeviceGroups)
            {
                if (instance == null || instance.GroupRoot == null)
                {
                    continue;
                }

                Vector3 heartRateOffset = ResolveGraphChannelOffset(EmotionChannelKind.HeartRate);
                Vector3 edaOffset = ResolveGraphChannelOffset(EmotionChannelKind.EdaFiltered);

                var offsetsInUse = new List<Vector3>(2);

                if (instance.HeartRateGraph != null)
                {
                    instance.HeartRateGraph.transform.localPosition = heartRateOffset;
                    offsetsInUse.Add(heartRateOffset);
                    ApplyGraphHeightScale(instance.HeartRateGraph);
                    UpdateGraphPanelSize(instance.HeartRateGraph);
                }

                if (instance.EdaGraph != null)
                {
                    instance.EdaGraph.transform.localPosition = edaOffset;
                    offsetsInUse.Add(edaOffset);
                    ApplyGraphHeightScale(instance.EdaGraph);
                    UpdateGraphPanelSize(instance.EdaGraph);
                }

                ClearGraphGroupDecorations(instance.GroupRoot.transform);

                if (instance.HeartRateGraph != null)
                {
                    AttachGraphLabel(instance.GroupRoot, instance.HeartRateGraph, heartRateOffset, ResolveGraphChannelLabel(EmotionChannelKind.HeartRate));
                }

                if (instance.EdaGraph != null)
                {
                    AttachGraphLabel(instance.GroupRoot, instance.EdaGraph, edaOffset, ResolveGraphChannelLabel(EmotionChannelKind.EdaFiltered));
                }

                AttachGraphGroupPanel(instance.GroupRoot, offsetsInUse);
                AttachGraphGroupLabel(instance.GroupRoot, instance.DeviceId, instance.DeviceIndex, offsetsInUse);
            }
        }

        private static void ClearGraphGroupDecorations(Transform groupRoot)
        {
            if (groupRoot == null)
            {
                return;
            }

            var toRemove = new List<GameObject>();
            for (int i = 0; i < groupRoot.childCount; i++)
            {
                var child = groupRoot.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                string name = child.name;
                if (name == "GraphLabel" || name == "GraphGroupLabel" || name == "GraphGroupPanel")
                {
                    toRemove.Add(child.gameObject);
                }
            }

            foreach (var child in toRemove)
            {
                if (child != null)
                {
                    Destroy(child);
                }
            }
        }

        private void AttachGraphPanel(GameObject graphInstance)
        {
            if (!ShowGraphPanels || graphInstance == null)
            {
                return;
            }

            Vector2 panelSize = new Vector2(GraphItemBoundsSize.x, GraphItemBoundsSize.y);
            var graphVisualizer = graphInstance.GetComponentInChildren<GraphVisualizer>();
            if (graphVisualizer != null)
            {
                panelSize = graphVisualizer.GraphSize;
            }

            panelSize += GraphPanelPadding;
            Vector3 localPosition = new Vector3(0f, 0f, GraphPanelDepthOffset) + GraphPanelOffset;
            var panel = CreatePanelQuad("GraphPanel", graphInstance.transform, panelSize, localPosition, GetGraphPanelMaterial());
            if (panel != null)
            {
                panel.layer = graphInstance.layer;
            }
            UpdateGraphPanelSize(graphInstance);
        }

        private void UpdateGraphPanelSize(GameObject graphInstance)
        {
            if (graphInstance == null)
            {
                return;
            }

            Transform panelTransform = graphInstance.transform.Find("GraphPanel");
            if (panelTransform == null)
            {
                return;
            }

            Vector2 panelSize = new Vector2(GraphItemBoundsSize.x, GraphItemBoundsSize.y);
            var graphVisualizer = graphInstance.GetComponentInChildren<GraphVisualizer>();
            if (graphVisualizer != null)
            {
                panelSize = graphVisualizer.GraphSize;
            }

            float fallbackY = GraphItemBoundsSize.y * (GraphChannelLayout == GraphChannelLayoutMode.Stacked ? GraphStackedHeightScale : 1f);
            if (panelSize.x < 0.01f)
            {
                panelSize.x = GraphItemBoundsSize.x;
            }
            if (panelSize.y < 0.01f)
            {
                panelSize.y = fallbackY;
            }

            panelSize += GraphPanelPadding;
            panelTransform.localScale = new Vector3(panelSize.x, panelSize.y, 1f);
        }

        private void ApplyGraphHeightScale(GameObject graphInstance)
        {
            if (graphInstance == null)
            {
                return;
            }

            var graphVisualizer = graphInstance.GetComponentInChildren<GraphVisualizer>();
            if (graphVisualizer == null)
            {
                return;
            }

            float scale = GraphChannelLayout == GraphChannelLayoutMode.Stacked
                ? GraphStackedHeightScale
                : 1f;

            graphVisualizer.SetGraphSizeYScale(scale);
        }

        private void AttachGraphGroupPanel(GameObject groupRoot, IReadOnlyList<Vector3> channelOffsets)
        {
            if (!ShowGraphGroupPanels || groupRoot == null)
            {
                return;
            }

            Vector3 center = Vector3.zero;
            Vector3 size = GraphItemBoundsSize;
            if (TryCalculateGraphGroupBounds(channelOffsets, out var boundsCenter, out var boundsSize))
            {
                center = boundsCenter;
                size = boundsSize;
            }

            float heightScale = GraphChannelLayout == GraphChannelLayoutMode.Stacked
                ? GraphStackedHeightScale
                : 1f;
            float graphHeight = GraphItemBoundsSize.y * heightScale;
            float minY = center.y - graphHeight * 0.5f;
            float maxY = center.y + graphHeight * 0.5f;
            if (channelOffsets != null && channelOffsets.Count > 0)
            {
                minY = float.PositiveInfinity;
                maxY = float.NegativeInfinity;
                float halfHeight = graphHeight * 0.5f;
                foreach (var offset in channelOffsets)
                {
                    float localMin = offset.y - halfHeight;
                    float localMax = offset.y + halfHeight;
                    if (localMin < minY) minY = localMin;
                    if (localMax > maxY) maxY = localMax;
                }
            }

            float totalHeight = Mathf.Max(graphHeight, maxY - minY);
            size.y = totalHeight;
            center.y = (minY + maxY) * 0.5f;

            Vector2 panelSize = new Vector2(size.x, size.y) + GraphGroupPanelPadding;
            Vector3 localPosition = center + new Vector3(0f, 0f, GraphGroupPanelDepthOffset) + GraphGroupPanelOffset;
            var panel = CreatePanelQuad("GraphGroupPanel", groupRoot.transform, panelSize, localPosition, GetGraphGroupPanelMaterial());
            if (panel != null)
            {
                panel.layer = groupRoot.layer;
            }
        }

        private void AttachGraphGroupLabel(GameObject groupRoot, string deviceId, int deviceIndex, IReadOnlyList<Vector3> channelOffsets)
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
            if (TryCalculateGraphGroupBounds(channelOffsets, out var center, out var size))
            {
                float yOffset = center.y + size.y * 0.5f + extraYOffset;
                localPosition = new Vector3(center.x, yOffset, center.z) + GraphGroupLabelOffset;
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

        private Material GetGraphPanelMaterial()
        {
            return GetOrCreatePanelMaterial(ref _graphPanelMaterial, GraphPanelColor);
        }

        private Material GetGraphGroupPanelMaterial()
        {
            return GetOrCreatePanelMaterial(ref _graphGroupPanelMaterial, GraphGroupPanelColor);
        }

        private static Material GetOrCreatePanelMaterial(ref Material material, Color color)
        {
            if (material == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }

                if (shader == null)
                {
                    return null;
                }

                material = new Material(shader);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            return material;
        }

        private static GameObject CreatePanelQuad(string name, Transform parent, Vector2 size, Vector3 localPosition, Material material)
        {
            if (parent == null || material == null)
            {
                return null;
            }

            var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = name;
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = localPosition;
            panel.transform.localRotation = Quaternion.identity;
            panel.transform.localScale = new Vector3(size.x, size.y, 1f);

            var collider = panel.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = panel.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return panel;
        }

        private static string ResolveGraphChannelLabel(EmotionChannelKind channelKind)
        {
            return channelKind switch
            {
                EmotionChannelKind.HeartRate => "Heart Rate",
                EmotionChannelKind.EdaFiltered => "EDA",
                _ => channelKind.ToString()
            };
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

            return ResolveVisualRoot(instance);
        }

        private static Transform ResolveVisualRoot(GameObject instance)
        {
            if (instance == null)
            {
                return null;
            }

            var anchored = instance.GetComponent<AnchoredVisualization>();
            if (anchored != null && anchored.VisualRoot != null)
            {
                return anchored.VisualRoot;
            }

            return instance.transform;
        }

        private sealed class EmotionDeviceInstance
        {
            public string DeviceId;
            public GameObject VisualInstance;
            public Component BindingComponent;
            public string ValenceChannel;
            public string ArousalChannel;
            public string HeartRateChannel;
            public RedisDataPump ValencePump;
            public RedisDataPump ArousalPump;
            public RedisDataPump HeartRatePump;
        }

        private sealed class AudienceDeviceInstance
        {
            public string DeviceId;
            public GameObject VisualInstance;
            public AudienceSignalBinding Binding;
            public string PhysioMetricsChannel;
            public RedisPhysioMetricsPump PhysioPump;
        }

        private sealed class GraphDeviceGroupInstance
        {
            public string DeviceId;
            public int DeviceIndex;
            public GameObject GroupRoot;
            public GameObject HeartRateGraph;
            public GameObject EdaGraph;
            public string HeartRateChannel;
            public string EdaChannel;
            public RedisDataPump HeartRatePump;
            public RedisDataPump EdaPump;
        }

        private sealed class AudienceGraphDeviceGroupInstance
        {
            public string DeviceId;
            public int DeviceIndex;
            public GameObject GroupRoot;
            public AudienceSignalBinding Binding;
            public string PhysioMetricsChannel;
            public RedisPhysioMetricsPump PhysioPump;
        }

        private sealed class SelectedSensorSourceInstance
        {
            public GameObject SourceObject;
            public AudienceSignalBinding Binding;
            public string PhysioMetricsChannel;
        }

        private sealed class SharedPumpHandle
        {
            public string ChannelName;
            public string Host;
            public int Port;
            public RedisDataPump Pump;
            public int ReferenceCount;
        }

        private sealed class SharedPhysioPumpHandle
        {
            public string ChannelName;
            public string Host;
            public int Port;
            public RedisPhysioMetricsPump Pump;
            public int ReferenceCount;
        }

        private struct DeviceChannelDefinition
        {
            public string DeviceId;
            public string ValenceChannel;
            public string ArousalChannel;
            public string HeartRateChannel;
            public string EdaChannel;
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

        private bool ShouldInitializeSessionController()
        {
            if (_sessionController == null)
            {
                return false;
            }

            if (UseManualVisualizationDrivers)
            {
                return false;
            }

            bool redisAutoSpawnOnly = ActiveSignalSource == SignalSourceType.Redis &&
                (AutoSpawnEmotionVisualsFromChannels || AutoSpawnAudienceVisualsFromChannels);
            return !redisAutoSpawnOnly;
        }
    }
}
