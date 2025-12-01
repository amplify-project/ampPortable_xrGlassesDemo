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

        private void Awake()
        {
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
        }

        private void OnDestroy()
        {
            _sessionController?.Dispose();
            _sessionController = null;
            ClearEmotionDeviceVisuals();
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
                if (RedisSource != null)
                {
                    RedisSource.enabled = false;
                }

                if (RedisManager != null)
                {
                    RedisManager.enabled = false;
                }

                ClearEmotionDeviceVisuals();
            }

            if (RedisSource != null && hasRedisEndpoint)
            {
                Debug.Log($"Bootstrapper: Configuring RedisSource for {RedisHost}:{RedisPort} ({primaryRedisChannel})");
                RedisSource.ConfigureConnection(RedisHost, RedisPort, primaryRedisChannel);
                RedisSource.enabled = useRedis;
            }

            if (RedisManager != null && hasRedisEndpoint)
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

            if (useRedis && hasRedisEndpoint && AutoSpawnEmotionVisualsFromChannels)
            {
                EnsureEmotionDeviceVisuals(redisChannels);
            }
            else
            {
                ClearEmotionDeviceVisuals();
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

                var valencePump = CreateRedisPumpForChannel(definition.DeviceId, EmotionChannelKind.Valence, definition.ValenceChannel);
                var arousalPump = CreateRedisPumpForChannel(definition.DeviceId, EmotionChannelKind.Arousal, definition.ArousalChannel);
                RedisDataPump heartRatePump = null;
                if (!string.IsNullOrWhiteSpace(definition.HeartRateChannel))
                {
                    heartRatePump = CreateRedisPumpForChannel(definition.DeviceId, EmotionChannelKind.HeartRate, definition.HeartRateChannel);
                }

                if (!TryConfigureEmotionBinding(bindingComponent, valencePump, arousalPump, heartRatePump, definition.DeviceId))
                {
                    Debug.LogWarning($"Bootstrapper: Unable to configure emotion binding on '{instance.name}', skipping visual spawn.");
                    Destroy(instance);
                    Destroy(valencePump.gameObject);
                    Destroy(arousalPump.gameObject);
                    if (heartRatePump != null)
                    {
                        Destroy(heartRatePump.gameObject);
                    }
                    continue;
                }

                _emotionDeviceInstances.Add(new EmotionDeviceInstance
                {
                    DeviceId = definition.DeviceId,
                    VisualInstance = instance,
                    BindingComponent = bindingComponent,
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

        private RedisDataPump CreateRedisPumpForChannel(string deviceId, EmotionChannelKind channelKind, string channelName)
        {
            var pumpObject = new GameObject($"RedisPump_{deviceId}_{channelKind}");
            pumpObject.transform.SetParent(transform, false);
            pumpObject.SetActive(false);

            var pump = pumpObject.AddComponent<RedisDataPump>();
            pump.ConfigureConnection(RedisHost, RedisPort, channelName);

            pumpObject.SetActive(true);
            return pump;
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

                if (instance.ValencePump != null)
                {
                    Destroy(instance.ValencePump.gameObject);
                }

                if (instance.ArousalPump != null)
                {
                    Destroy(instance.ArousalPump.gameObject);
                }

                if (instance.HeartRatePump != null)
                {
                    Destroy(instance.HeartRatePump.gameObject);
                }
            }

            _emotionDeviceInstances.Clear();
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
            public RedisDataPump ValencePump;
            public RedisDataPump ArousalPump;
            public RedisDataPump HeartRatePump;
        }

        private struct DeviceChannelDefinition
        {
            public string DeviceId;
            public string ValenceChannel;
            public string ArousalChannel;
            public string HeartRateChannel;
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
