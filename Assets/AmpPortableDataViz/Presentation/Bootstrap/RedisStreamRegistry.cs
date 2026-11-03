using System;
using System.Collections.Generic;
using System.Linq;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Infra;
using AmpPortableDataViz.Presentation.Sources;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Bootstrap
{
    /// <summary>Owns shared physiological pumps and the sensor engagement backend.</summary>
    internal sealed class RedisStreamRegistry
    {
        private readonly Transform _owner;
        private Settings _settings;
        private readonly Dictionary<string, SharedPhysioPumpHandle> _sharedPhysioPumpsByChannel = new Dictionary<string, SharedPhysioPumpHandle>(StringComparer.Ordinal);
        private GameObject _sensorEngagementBackendObject;
        private RedisSensorEngagementPump _sensorEngagementPump;
        private SensorEngagementCoordinator _sensorEngagementCoordinator;
        private string _sensorEngagementConfigurationKey = string.Empty;

        internal RedisStreamRegistry(Transform owner)
        {
            _owner = owner;
        }

        internal void Configure(Settings settings)
        {
            _settings = settings;
        }

        internal struct Settings
        {
            public string RedisHost;
            public int RedisPort;
            public bool UseIndividualSensorEngagement;
            public string SensorEngagementChannelTemplate;
            public SensorEngagementChannelRoute[] SensorEngagementChannelOverrides;
            public float SensorEngagementTimeoutSeconds;
            public float SensorEngagementNeutralValue;
            public SensorEngagementJsonFormat SensorEngagementPayloadFormat;
        }

        internal void EnsureSensorEngagementBackend(IEnumerable<string> sensorIds)
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
            _sensorEngagementBackendObject.transform.SetParent(_owner, false);
            _sensorEngagementBackendObject.SetActive(false);

            _sensorEngagementPump = _sensorEngagementBackendObject.AddComponent<RedisSensorEngagementPump>();
            _sensorEngagementCoordinator = _sensorEngagementBackendObject.AddComponent<SensorEngagementCoordinator>();

            var decoder = new SensorEngagementJsonDecoder(_settings.SensorEngagementPayloadFormat);
            _sensorEngagementPump.ConfigureConnection(
                _settings.RedisHost,
                _settings.RedisPort,
                _settings.UseIndividualSensorEngagement ? routes : Array.Empty<SensorEngagementChannelRoute>(),
                decoder);
            _sensorEngagementCoordinator.Configure(
                _sensorEngagementPump,
                normalizedSensorIds,
                _settings.SensorEngagementTimeoutSeconds,
                _settings.SensorEngagementNeutralValue);

            _sensorEngagementConfigurationKey = configurationKey;
            _sensorEngagementBackendObject.SetActive(true);

            if (_settings.UseIndividualSensorEngagement && routes.Length == 0)
            {
                Debug.LogWarning(
                    "Bootstrapper: Individual sensor engagement is enabled, but no channel template or overrides are configured. " +
                    "Sensor visualizations will use the neutral engagement value.",
                    _owner);
            }
        }

        private SensorEngagementChannelRoute[] BuildSensorEngagementRoutes(IEnumerable<string> sensorIds)
        {
            if (!_settings.UseIndividualSensorEngagement || sensorIds == null)
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
            if (_settings.SensorEngagementChannelOverrides != null)
            {
                for (int i = 0; i < _settings.SensorEngagementChannelOverrides.Length; i++)
                {
                    SensorEngagementChannelRoute channelOverride = _settings.SensorEngagementChannelOverrides[i];
                    if (string.Equals(channelOverride.SensorId?.Trim(), sensorId, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(channelOverride.ChannelName))
                    {
                        return channelOverride.ChannelName.Trim();
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(_settings.SensorEngagementChannelTemplate))
            {
                return string.Empty;
            }

            return _settings.SensorEngagementChannelTemplate
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
            return $"{_settings.RedisHost}:{_settings.RedisPort};enabled={_settings.UseIndividualSensorEngagement};timeout={_settings.SensorEngagementTimeoutSeconds:R};" +
                $"neutral={_settings.SensorEngagementNeutralValue:R};sensors={sensorKey};routes={routeKey};" +
                $"items={_settings.SensorEngagementPayloadFormat.ItemsPropertyName};sensor={_settings.SensorEngagementPayloadFormat.SensorIdPropertyName};" +
                $"score={_settings.SensorEngagementPayloadFormat.EngagementPropertyName};confirmed={_settings.SensorEngagementPayloadFormat.ConfirmationPropertyName};" +
                $"timestamp={_settings.SensorEngagementPayloadFormat.TimestampPropertyName}";
        }

        internal ILatestDataSource<SensorEngagementState> ResolveSensorEngagementSource(string sensorId)
        {
            return _sensorEngagementCoordinator?.GetSource(sensorId);
        }

        internal void ClearSensorEngagementBackend()
        {
            if (_sensorEngagementBackendObject != null)
            {
                UnityEngine.Object.Destroy(_sensorEngagementBackendObject);
            }

            _sensorEngagementBackendObject = null;
            _sensorEngagementPump = null;
            _sensorEngagementCoordinator = null;
            _sensorEngagementConfigurationKey = string.Empty;
        }

        internal RedisPhysioMetricsPump AcquirePhysioPumpForChannel(string deviceId, string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
            {
                return null;
            }

            if (_sharedPhysioPumpsByChannel.TryGetValue(channelName, out var handle) && handle.Pump != null)
            {
                handle.ReferenceCount++;
                if (!string.Equals(handle.Host, _settings.RedisHost, StringComparison.Ordinal) || handle.Port != _settings.RedisPort)
                {
                    handle.Pump.ConfigureConnection(_settings.RedisHost, _settings.RedisPort, channelName);
                    handle.Host = _settings.RedisHost;
                    handle.Port = _settings.RedisPort;
                }
                return handle.Pump;
            }

            var pumpObject = new GameObject($"RedisPump_{deviceId}_PhysioMetrics");
            pumpObject.transform.SetParent(_owner, false);
            pumpObject.SetActive(false);

            var pump = pumpObject.AddComponent<RedisPhysioMetricsPump>();
            pump.ConfigureConnection(_settings.RedisHost, _settings.RedisPort, channelName);

            pumpObject.SetActive(true);

            _sharedPhysioPumpsByChannel[channelName] = new SharedPhysioPumpHandle
            {
                Host = _settings.RedisHost,
                Port = _settings.RedisPort,
                Pump = pump,
                ReferenceCount = 1
            };

            return pump;
        }

        internal void ReleasePhysioPump(string channelName)
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
                UnityEngine.Object.Destroy(handle.Pump.gameObject);
            }

            _sharedPhysioPumpsByChannel.Remove(channelName);
        }

        internal static List<AudienceDeviceChannelDefinition> BuildAudienceDeviceChannelDefinitions(IEnumerable<string> redisChannels)
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

        internal static List<AudienceDeviceChannelDefinition> FilterAudienceToConfiguredDevices(IEnumerable<AudienceDeviceChannelDefinition> definitions, IEnumerable<string> configuredDeviceIds)
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

        private sealed class SharedPhysioPumpHandle
        {
            public string Host;
            public int Port;
            public RedisPhysioMetricsPump Pump;
            public int ReferenceCount;
        }

        internal struct AudienceDeviceChannelDefinition
        {
            public string DeviceId;
            public string PhysioMetricsChannel;

            public bool HasVisualizationChannels =>
                !string.IsNullOrWhiteSpace(DeviceId) &&
                !string.IsNullOrWhiteSpace(PhysioMetricsChannel);
        }
    }
}
