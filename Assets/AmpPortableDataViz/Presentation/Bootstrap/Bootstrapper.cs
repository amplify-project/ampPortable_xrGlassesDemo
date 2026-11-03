using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Infra;
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

        private VisualizationPresenter _visuals;
        private VisualizationPresenter Visuals
        {
            get
            {
                if (_visuals == null)
                {
                    _visuals = new VisualizationPresenter(gameObject, Streams,
                        PositionAbstractViz, PositionGraphGroup, PositionAtParentOrigin,
                        AttachDeviceLabel, AttachGraphGroupLabel);
                }

                _visuals.Configure(new VisualizationPresenter.Settings
                {
                    AbstractVizPrefab = AbstractVizPrefab,
                    AbstractVizParent = AbstractVizParent,
                    GraphPrefab = GraphPrefab,
                    GraphGroupParent = GraphGroupParent,
                    ArtistVizPrefab = ArtistVizPrefab,
                    ArtistVizParent = ArtistVizParent,
                    AutoSpawnAbstractViz = AutoSpawnAbstractViz,
                    AutoSpawnGraphVisualsFromChannels = AutoSpawnGraphVisualsFromChannels,
                    SensorIds = SensorIds,
                    SensorHudCamera = SensorHudCamera,
                    SensorHudLocalPosition = SensorHudLocalPosition,
                    SensorHudLocalScale = SensorHudLocalScale,
                    SensorHudFontSize = SensorHudFontSize,
                    SensorHudColor = SensorHudColor
                });
                return _visuals;
            }
        }

        private RedisStreamRegistry _streams;
        private RedisStreamRegistry Streams => _streams ??= new RedisStreamRegistry(transform);

        private void ConfigureStreams()
        {
            var overrides = SensorEngagementChannelOverrides == null
                ? null
                : SensorEngagementChannelOverrides.Select(value =>
                    new SensorEngagementChannelRoute(value.SensorId, value.ChannelName)).ToArray();
            Streams.Configure(new RedisStreamRegistry.Settings
            {
                RedisHost = RedisHost,
                RedisPort = RedisPort,
                UseIndividualSensorEngagement = UseIndividualSensorEngagement,
                SensorEngagementChannelTemplate = SensorEngagementChannelTemplate,
                SensorEngagementChannelOverrides = overrides,
                SensorEngagementTimeoutSeconds = SensorEngagementTimeoutSeconds,
                SensorEngagementNeutralValue = SensorEngagementNeutralValue,
                SensorEngagementPayloadFormat = SensorEngagementPayloadFormat
            });
        }

        private Coroutine _deviceDiscoveryRoutine;
        private bool _redisEndpointReady;

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
            Visuals.ClearAudienceDeviceVisuals();
            Visuals.ClearAudienceGraphDeviceVisuals();
            Visuals.ClearSelectedSensorStreamMode();
            Streams.ClearSensorEngagementBackend();
        }

        private void ClearRuntimeVisuals()
        {
            ClearAutomaticVisuals();
            Visuals.ClearManualDriverVisuals();
        }

        private void ApplySignalConfiguration()
        {
            if (UseManualVisualizationDrivers)
            {
                ApplyManualDriverConfiguration();
                return;
            }

            EnsureRedisEndpointFromRuntime();
            ConfigureStreams();

            Visuals.ClearManualDriverVisuals();

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
                var sensorDefinitions = RedisStreamRegistry.BuildAudienceDeviceChannelDefinitions(redisChannels);
                sensorDefinitions = RedisStreamRegistry.FilterAudienceToConfiguredDevices(sensorDefinitions, SensorIds);
                Streams.EnsureSensorEngagementBackend(sensorDefinitions.Select(definition => definition.DeviceId));
            }
            else
            {
                Streams.ClearSensorEngagementBackend();
            }

            bool shouldUseSelectedSensorStream = UseSingleSelectedSensorStream &&
                (shouldSpawnAudienceVisuals || shouldSpawnAudienceGraphVisuals || shouldSpawnArtistViz);
            if (shouldUseSelectedSensorStream)
            {
                Visuals.ClearAudienceDeviceVisuals();
                Visuals.ClearAudienceGraphDeviceVisuals();
                Visuals.EnsureSelectedSensorStreamMode(
                    redisChannels,
                    shouldSpawnAudienceVisuals,
                    shouldSpawnAudienceGraphVisuals,
                    shouldSpawnArtistViz);
                return;
            }

            Visuals.ClearSelectedSensorStreamMode();

            if (shouldSpawnAudienceVisuals)
            {
                Visuals.EnsureAudienceDeviceVisuals(redisChannels);
            }
            else
            {
                Visuals.ClearAudienceDeviceVisuals();
            }

            if (shouldSpawnAudienceGraphVisuals)
            {
                Visuals.EnsureAudienceGraphDeviceVisuals(redisChannels);
            }
            else
            {
                Visuals.ClearAudienceGraphDeviceVisuals();
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
            Visuals.EnsureManualDriverVisuals();
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

        [Serializable]
        public struct SensorEngagementChannelOverride
        {
            public string SensorId;
            public string ChannelName;
        }
    }
}
