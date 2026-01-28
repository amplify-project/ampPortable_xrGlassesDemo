using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmpPortableDataViz.Application;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Infra;
using AmpPortableDataViz.Presentation.Anchors;
using AmpPortableDataViz.Presentation.Mapping;
using AmpPortableDataViz.Presentation.Sources;
using AmpPortableDataViz.Presentation.Utility;
using AmpPortableDataViz.Presentation.Visualization;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

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

        [Header("Graph Visual Settings")]
        public bool AutoSpawnGraphVisualsFromChannels = true;
        public GameObject GraphPrefab;
        public Transform GraphGroupParent;
        public Vector3 GraphGroupOriginOffset = Vector3.zero;
        public Vector3 GraphGroupSpacing = new Vector3(1.6f, 0f, 0.75f);
        [Min(1)]
        public int GraphGroupsPerRow = 2;

        public enum GraphChannelLayoutMode
        {
            InlineOffsets,
            Stacked
        }

        [Header("Graph Channel Stacking")]
        public GraphChannelLayoutMode GraphChannelLayout = GraphChannelLayoutMode.InlineOffsets;
        public Vector3 GraphStackedOriginOffset = Vector3.zero;
        [Min(0.01f)]
        public float GraphStackedVerticalSpacing = 0.6f;

        [Header("Graph Channel Offsets")]
        public Vector3 GraphValenceOffset = new Vector3(-0.6f, 0f, 0f);
        public Vector3 GraphArousalOffset = Vector3.zero;
        public Vector3 GraphHeartRateOffset = new Vector3(0.6f, 0f, 0f);
        public Vector3 GraphEdaOffset = new Vector3(0.6f, -0.6f, 0f);

        [Header("Graph Stream Settings")]
        public Vector2 GraphValenceRange = new Vector2(0f, 1f);
        public Vector2 GraphArousalRange = new Vector2(0f, 1f);
        public Vector2 GraphHeartRateRange = new Vector2(40f, 200f);
        public Vector2 GraphEdaRange = new Vector2(0f, 10f);
        public Color GraphValenceColor = new Color(0.2f, 0.9f, 0.4f, 1f);
        public Color GraphArousalColor = new Color(0.95f, 0.65f, 0.15f, 1f);
        public Color GraphHeartRateColor = new Color(0.95f, 0.2f, 0.2f, 1f);
        public Color GraphEdaColor = new Color(0.2f, 0.8f, 0.95f, 1f);
        [Range(0.0001f, 0.05f)]
        public float GraphLineWidth = 0.01f;

        [Header("Graph Group Interaction")]
        public bool GraphGroupsGrabbable = true;
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
        private readonly List<EmotionDeviceInstance> _emotionDeviceInstances = new List<EmotionDeviceInstance>();
        private readonly List<GraphDeviceGroupInstance> _graphDeviceGroups = new List<GraphDeviceGroupInstance>();
        private readonly Dictionary<string, SharedPumpHandle> _sharedPumpsByChannel = new Dictionary<string, SharedPumpHandle>(StringComparer.Ordinal);
        private bool _redisEndpointReady;

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
            ApplySignalConfiguration();
            if (!ShouldInitializeSessionController())
            {
                return;
            }

            _initializationRoutine = StartCoroutine(BeginSession());
        }

        public void BeginAfterEndpoint()
        {
            var settings = RedisRuntimeSettings.Load();
            RedisHost = settings.Host;
            RedisPort = settings.Port;
            _redisEndpointReady = true;

            ApplySignalConfiguration();

            if (_initializationRoutine != null)
            {
                StopCoroutine(_initializationRoutine);
                _initializationRoutine = null;
            }

            if (!ShouldInitializeSessionController())
            {
                return;
            }

            _initializationRoutine = StartCoroutine(BeginSession());
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

            _sessionController?.Shutdown();
            ClearEmotionDeviceVisuals();
            ClearGraphDeviceVisuals();
        }

        private void OnDestroy()
        {
            _sessionController?.Dispose();
            _sessionController = null;
            ClearEmotionDeviceVisuals();
            ClearGraphDeviceVisuals();
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
            // If the inspector host is blank, fall back to the persisted runtime settings so we never configure an empty endpoint.
            if (string.IsNullOrWhiteSpace(RedisHost))
            {
                var runtimeSettings = RedisRuntimeSettings.Load();
                if (!string.IsNullOrWhiteSpace(runtimeSettings.Host))
                {
                    RedisHost = runtimeSettings.Host;
                    RedisPort = runtimeSettings.Port;
                    Debug.Log($"Bootstrapper: Loaded Redis endpoint from runtime settings -> {RedisHost}:{RedisPort}");
                }
            }

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

            if (LiveKitSource != null)
            {
                LiveKitSource.tokenEndpoint = LiveKitTokenEndpoint;
                LiveKitSource.sandboxId = LiveKitSandboxId;
                LiveKitSource.roomName = LiveKitRoomName;
                LiveKitSource.identity = LiveKitIdentity;
                LiveKitSource.enabled = useLiveKit;
            }

            bool shouldSpawnEmotionVisuals = useRedis && hasRedisEndpoint && redisReady && AutoSpawnEmotionVisualsFromChannels;
            if (shouldSpawnEmotionVisuals)
            {
                EnsureEmotionDeviceVisuals(redisChannels);
            }
            else
            {
                ClearEmotionDeviceVisuals();
            }

            bool shouldSpawnGraphVisuals = useRedis && hasRedisEndpoint && redisReady && AutoSpawnGraphVisualsFromChannels;
            if (shouldSpawnGraphVisuals)
            {
                EnsureGraphDeviceVisuals(redisChannels);
            }
            else
            {
                ClearGraphDeviceVisuals();
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

                var offsetsInUse = new List<Vector3>(4);
                var instance = new GraphDeviceGroupInstance
                {
                    DeviceId = definition.DeviceId,
                    GroupRoot = groupRoot
                };

                string deviceLabel = FormatDeviceLabel(definition.DeviceId, deviceIndex);
                Vector3 valenceOffset = ResolveGraphChannelOffset(EmotionChannelKind.Valence);
                Vector3 arousalOffset = ResolveGraphChannelOffset(EmotionChannelKind.Arousal);
                Vector3 heartRateOffset = ResolveGraphChannelOffset(EmotionChannelKind.HeartRate);
                Vector3 edaOffset = ResolveGraphChannelOffset(EmotionChannelKind.EdaFiltered);

                if (!string.IsNullOrWhiteSpace(definition.ValenceChannel))
                {
                    string valenceChannel = definition.ValenceChannel;
                    instance.ValenceGraph = SpawnGraphForChannel(
                        groupRoot.transform,
                        definition.DeviceId,
                        EmotionChannelKind.Valence,
                        valenceChannel,
                        valenceOffset,
                        GraphValenceRange,
                        GraphValenceColor,
                        GraphLineWidth,
                        $"{deviceLabel} - Valence",
                        out instance.ValencePump);
                    if (instance.ValenceGraph != null)
                    {
                        instance.ValenceChannel = valenceChannel;
                        offsetsInUse.Add(valenceOffset);
                    }
                }

                if (!string.IsNullOrWhiteSpace(definition.ArousalChannel))
                {
                    string arousalChannel = definition.ArousalChannel;
                    instance.ArousalGraph = SpawnGraphForChannel(
                        groupRoot.transform,
                        definition.DeviceId,
                        EmotionChannelKind.Arousal,
                        arousalChannel,
                        arousalOffset,
                        GraphArousalRange,
                        GraphArousalColor,
                        GraphLineWidth,
                        $"{deviceLabel} - Arousal",
                        out instance.ArousalPump);
                    if (instance.ArousalGraph != null)
                    {
                        instance.ArousalChannel = arousalChannel;
                        offsetsInUse.Add(arousalOffset);
                    }
                }

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
                    }
                }

                if (instance.ValenceGraph == null && instance.ArousalGraph == null && instance.HeartRateGraph == null && instance.EdaGraph == null)
                {
                    Debug.LogWarning($"Bootstrapper: No graph channels found for device '{definition.DeviceId}', skipping group spawn.");
                    Destroy(groupRoot);
                    ReleaseRedisPump(instance.ValenceChannel);
                    ReleaseRedisPump(instance.ArousalChannel);
                    ReleaseRedisPump(instance.HeartRateChannel);
                    ReleaseRedisPump(instance.EdaChannel);
                    continue;
                }

                if (GraphGroupsGrabbable)
                {
                    ConfigureGraphGroupGrab(groupRoot, offsetsInUse);
                }

                _graphDeviceGroups.Add(instance);
                deviceIndex++;
            }
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
                    if (instance.ValenceGraph != null) Destroy(instance.ValenceGraph);
                    if (instance.ArousalGraph != null) Destroy(instance.ArousalGraph);
                    if (instance.HeartRateGraph != null) Destroy(instance.HeartRateGraph);
                    if (instance.EdaGraph != null) Destroy(instance.EdaGraph);
                }

                ReleaseRedisPump(instance.ValenceChannel);
                ReleaseRedisPump(instance.ArousalChannel);
                ReleaseRedisPump(instance.HeartRateChannel);
                ReleaseRedisPump(instance.EdaChannel);
            }

            _graphDeviceGroups.Clear();
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
            ClearGraphDeviceVisuals();
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
                    EmotionChannelKind.Valence => GraphValenceOffset,
                    EmotionChannelKind.Arousal => GraphArousalOffset,
                    EmotionChannelKind.HeartRate => GraphHeartRateOffset,
                    EmotionChannelKind.EdaFiltered => GraphEdaOffset,
                    _ => Vector3.zero
                };
            }

            float spacing = Mathf.Max(0.01f, GraphStackedVerticalSpacing);
            float yOffset = channelKind switch
            {
                EmotionChannelKind.Valence => spacing,
                EmotionChannelKind.Arousal => 0f,
                EmotionChannelKind.HeartRate => -spacing,
                EmotionChannelKind.EdaFiltered => -spacing * 2f,
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

        private void ConfigureGraphGroupGrab(GameObject groupRoot, IReadOnlyList<Vector3> channelOffsets)
        {
            if (groupRoot == null)
            {
                return;
            }

            var rigidbody = groupRoot.GetComponent<Rigidbody>();
            if (rigidbody == null)
            {
                rigidbody = groupRoot.AddComponent<Rigidbody>();
            }

            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;

            var collider = groupRoot.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = groupRoot.AddComponent<BoxCollider>();
            }

            if (TryCalculateGraphGroupBounds(channelOffsets, out var center, out var size))
            {
                collider.center = center;
                collider.size = size;
            }

            var grab = groupRoot.GetComponent<XRGrabInteractable>();
            if (grab == null)
            {
                grab = groupRoot.AddComponent<XRGrabInteractable>();
            }

            grab.distanceCalculationMode = XRBaseInteractable.DistanceCalculationMode.ColliderPosition;
            grab.selectMode = InteractableSelectMode.Multiple;
            grab.focusMode = InteractableFocusMode.Multiple;
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            grab.trackPosition = true;
            grab.smoothPosition = true;
            grab.smoothPositionAmount = 14f;
            grab.tightenPosition = 0.1f;
            grab.useDynamicAttach = true;
            grab.matchAttachPosition = true;
            grab.matchAttachRotation = true;
            grab.snapToColliderVolume = true;
            grab.reinitializeDynamicAttachEverySingleGrab = true;
            grab.attachEaseInTime = 0.15f;

            if (grab.colliders != null)
            {
                grab.colliders.Clear();
                grab.colliders.Add(collider);
            }
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
                if (!TryParseDeviceChannel(channel, out var deviceId, out var kind))
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

        private static bool TryParseDeviceChannel(string channelName, out string deviceId, out EmotionChannelKind channelKind)
        {
            deviceId = string.Empty;
            channelKind = EmotionChannelKind.Broadcast;

            if (string.IsNullOrWhiteSpace(channelName))
            {
                return false;
            }

            const string prefix = "device:";
            if (!channelName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int finalColon = channelName.LastIndexOf(':');
            if (finalColon <= prefix.Length || finalColon >= channelName.Length - 1)
            {
                return false;
            }

            string suffix = channelName.Substring(finalColon + 1);
            if (suffix.Equals("valence_cont", StringComparison.OrdinalIgnoreCase) ||
                suffix.Equals("valence", StringComparison.OrdinalIgnoreCase)) // allow legacy channel names
            {
                channelKind = EmotionChannelKind.Valence;
            }
            else if (suffix.Equals("arousal_cont", StringComparison.OrdinalIgnoreCase) ||
                     suffix.Equals("arousal", StringComparison.OrdinalIgnoreCase)) // allow legacy channel names
            {
                channelKind = EmotionChannelKind.Arousal;
            }
            else if (suffix.Equals("hr_filtered", StringComparison.OrdinalIgnoreCase) ||
                     suffix.Equals("hr", StringComparison.OrdinalIgnoreCase))
            {
                channelKind = EmotionChannelKind.HeartRate;
            }
            else if (suffix.Equals("eda_filtered", StringComparison.OrdinalIgnoreCase) ||
                     suffix.Equals("eda", StringComparison.OrdinalIgnoreCase))
            {
                channelKind = EmotionChannelKind.EdaFiltered;
            }
            else
            {
                return false;
            }

            deviceId = channelName.Substring(prefix.Length, finalColon - prefix.Length).Trim();
            return deviceId.Length > 0;
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

            Transform labelParent = ResolveVisualRoot(visualInstance);
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

        private sealed class GraphDeviceGroupInstance
        {
            public string DeviceId;
            public GameObject GroupRoot;
            public GameObject ValenceGraph;
            public GameObject ArousalGraph;
            public GameObject HeartRateGraph;
            public GameObject EdaGraph;
            public string ValenceChannel;
            public string ArousalChannel;
            public string HeartRateChannel;
            public string EdaChannel;
            public RedisDataPump ValencePump;
            public RedisDataPump ArousalPump;
            public RedisDataPump HeartRatePump;
            public RedisDataPump EdaPump;
        }

        private sealed class SharedPumpHandle
        {
            public string ChannelName;
            public string Host;
            public int Port;
            public RedisDataPump Pump;
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

        private bool ShouldInitializeSessionController()
        {
            if (_sessionController == null)
            {
                return false;
            }

            bool redisEmotionOnly = AutoSpawnEmotionVisualsFromChannels && ActiveSignalSource == SignalSourceType.Redis;
            return !redisEmotionOnly;
        }
    }
}
