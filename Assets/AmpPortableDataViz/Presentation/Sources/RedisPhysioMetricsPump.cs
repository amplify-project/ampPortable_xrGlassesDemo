using System;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Infra;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Sources
{
    /// <summary>
    /// Bridges a labeled Redis physio_metrics JSON payload into a typed physiological metrics source.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RedisPhysioMetricsPump : MonoBehaviour, IDataSource<PhysioMetricsSample>
    {
        [Header("Source Identity")]
        [SerializeField] private string sourceId;

        [Header("Connection")]
        [SerializeField] private string redisHost = "192.168.0.6";
        [SerializeField] private int redisPort = 6379;
        [SerializeField] private string redisChannel = "device:MD-V5-0000448:physio_metrics";

        [Header("Device Channel Template")]
        [SerializeField] private bool useDeviceChannelTemplate = true;
        [SerializeField] private string deviceId = "MD-V5-0000448";

        [Header("Diagnostics")]
        [SerializeField] private bool logValues;
        [SerializeField] private bool logParseFailures = true;

        private IClock _clock;
        private int _sequenceId;
        private bool _isConnected;
        private bool _connectionInProgress;
        private string _activeChannelName = string.Empty;
        private string _cachedSourceName = string.Empty;

        public string SourceId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(sourceId))
                {
                    return sourceId.Trim();
                }

                if (this == null)
                {
                    return string.IsNullOrEmpty(_cachedSourceName) ? nameof(RedisPhysioMetricsPump) : _cachedSourceName;
                }

                var resolvedName = TryResolveGameObjectName();
                if (!string.IsNullOrEmpty(resolvedName))
                {
                    _cachedSourceName = resolvedName;
                    return resolvedName;
                }

                return string.IsNullOrEmpty(_cachedSourceName) ? nameof(RedisPhysioMetricsPump) : _cachedSourceName;
            }
        }

        public event Action<DataFrame<PhysioMetricsSample>> OnFrame;

        private void Awake()
        {
            _clock = new UnityClock();
            _cachedSourceName = TryResolveGameObjectName();
        }

        private void OnEnable()
        {
            _ = EnsureConnectionAsync();
        }

        private void Update()
        {
            if (!TryGetLatestMessage(out var latestMessage))
            {
                return;
            }

            if (!TryParseSample(latestMessage.RawPayload, out var sample))
            {
                if (logParseFailures)
                {
                    Debug.LogWarning($"RedisPhysioMetricsPump[{SourceId}] could not parse physio_metrics payload from {latestMessage.Channel}: {latestMessage.RawPayload}", this);
                }

                return;
            }

            var frame = new DataFrame<PhysioMetricsSample>(_clock.UtcNowTicks, _sequenceId++, sample);
            OnFrame?.Invoke(frame);

            if (logValues)
            {
                string channelLabel = string.IsNullOrWhiteSpace(latestMessage.Channel) ? _activeChannelName : latestMessage.Channel;
                Debug.Log($"RedisPhysioMetricsPump[{SourceId}] channel={channelLabel} {sample} seq={latestMessage.Sequence} ts={latestMessage.Timestamp}", this);
            }
        }

        private void OnDisable()
        {
            _ = TeardownConnectionAsync();
        }

        public void ConfigureConnection(string host, int port, string channel)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                Debug.LogWarning($"RedisPhysioMetricsPump[{SourceId}] cannot configure connection because host is empty.", this);
                return;
            }

            redisHost = host;
            redisPort = port;
            redisChannel = channel;
            useDeviceChannelTemplate = false;

            if (isActiveAndEnabled)
            {
                _ = RestartConnectionAsync();
            }
        }

        public void ConfigureDeviceChannel(string host, int port, string newDeviceId)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                Debug.LogWarning($"RedisPhysioMetricsPump[{SourceId}] cannot configure device channel because host is empty.", this);
                return;
            }

            redisHost = host;
            redisPort = port;
            deviceId = string.IsNullOrWhiteSpace(newDeviceId) ? string.Empty : newDeviceId.Trim();
            useDeviceChannelTemplate = true;

            if (isActiveAndEnabled)
            {
                _ = RestartConnectionAsync();
            }
        }

        private bool TryGetLatestMessage(out RedisSubscriber.RedisMessage message)
        {
            message = default;

            if (!TryGetSubscribedChannel(out var channelName))
            {
                return false;
            }

            bool hasMessage = false;
            RedisSubscriber.RedisMessage latestMessage = default;

            while (RedisSubscriber.TryDequeue(channelName, out var dequeuedMessage))
            {
                latestMessage = dequeuedMessage;
                hasMessage = true;
            }

            message = latestMessage;
            return hasMessage;
        }

        private bool TryParseSample(string payload, out PhysioMetricsSample sample)
        {
            return TryParsePayload(payload, ResolveDeviceId(), out sample);
        }

        internal static bool TryParsePayload(string payload, string deviceId, out PhysioMetricsSample sample)
        {
            sample = default;

            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            try
            {
                using var jsonDoc = JsonDocument.Parse(payload);
                var root = jsonDoc.RootElement;

                if (TryParseZScoreSample(root, deviceId, out sample))
                {
                    return true;
                }

                return TryParseLegacyStdDevSample(root, deviceId, out sample);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool TryParseZScoreSample(JsonElement root, string deviceId, out PhysioMetricsSample sample)
        {
            sample = default;

            bool hasTonicEda = TryFindFloat(root, "eda_z", out var tonicEda);
            bool hasTemperatureRate = TryFindFloat(root, "temperature_roc_z", out var temperatureRate);
            bool hasScrFrequency = TryFindFloat(root, "scr_frequency_z", out var scrFrequency);
            bool hasHeartRate = TryFindFloat(root, "hr_z", out var heartRate);
            bool hasInterBeatInterval = TryFindFloat(root, "ibi_z", out var interBeatInterval);

            if (!hasTonicEda || !hasTemperatureRate || !hasScrFrequency || !hasHeartRate || !hasInterBeatInterval)
            {
                return false;
            }

            sample = new PhysioMetricsSample(
                deviceId,
                PhysioMetricsEncoding.ZScore,
                ClampZScore(tonicEda),
                ClampZScore(temperatureRate),
                ClampZScore(scrFrequency),
                ClampZScore(heartRate),
                ClampZScore(interBeatInterval));
            return true;
        }

        private static bool TryParseLegacyStdDevSample(JsonElement root, string deviceId, out PhysioMetricsSample sample)
        {
            sample = default;

            bool hasTonicEda = TryFindAnyFloat(root, out var tonicEda, "edl_sd", "eda_sd");
            bool hasTemperatureRate = TryFindFloat(root, "temperature_roc_sd", out var temperatureRate);
            bool hasScrFrequency = TryFindFloat(root, "scr_frequency_sd", out var scrFrequency);
            bool hasHeartRate = TryFindFloat(root, "hr_sd", out var heartRate);
            bool hasInterBeatInterval = TryFindFloat(root, "ibi_sd", out var interBeatInterval);

            if (!hasTonicEda || !hasTemperatureRate || !hasScrFrequency || !hasHeartRate || !hasInterBeatInterval)
            {
                return false;
            }

            sample = new PhysioMetricsSample(
                deviceId,
                PhysioMetricsEncoding.LegacyStdDev,
                tonicEda,
                temperatureRate,
                scrFrequency,
                heartRate,
                interBeatInterval);
            return true;
        }

        private static bool TryFindAnyFloat(JsonElement element, out float value, params string[] propertyNames)
        {
            value = 0f;
            if (propertyNames == null)
            {
                return false;
            }

            for (int i = 0; i < propertyNames.Length; i++)
            {
                if (TryFindFloat(element, propertyNames[i], out value))
                {
                    return true;
                }
            }

            return false;
        }

        private static float ClampZScore(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return Mathf.Clamp(value, -3f, 3f);
        }

        private static bool TryFindFloat(JsonElement element, string propertyName, out float value)
        {
            value = 0f;

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase) &&
                        TryReadFloat(property.Value, out value))
                    {
                        return true;
                    }

                    if ((property.Value.ValueKind == JsonValueKind.Object || property.Value.ValueKind == JsonValueKind.Array) &&
                        TryFindFloat(property.Value, propertyName, out value))
                    {
                        return true;
                    }
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    if (TryFindFloat(item, propertyName, out value))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryReadFloat(JsonElement element, out float value)
        {
            value = 0f;

            try
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.Number:
                        value = (float)element.GetDouble();
                        return true;
                    case JsonValueKind.String:
                        return float.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
                    case JsonValueKind.True:
                        value = 1f;
                        return true;
                    case JsonValueKind.False:
                        value = 0f;
                        return true;
                    default:
                        return false;
                }
            }
            catch
            {
                value = 0f;
                return false;
            }
        }

        private async Task StartConnectionAsync()
        {
            if (_isConnected || _connectionInProgress)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(redisHost))
            {
                Debug.LogWarning($"RedisPhysioMetricsPump[{SourceId}] cannot start because redisHost is empty.", this);
                return;
            }

            _connectionInProgress = true;
            string channelName = string.Empty;
            try
            {
                if (!TryResolveConfiguredChannel(out channelName))
                {
                    return;
                }

                await RedisSubscriber.RegisterChannelAsync(redisHost, redisPort, channelName);
                _activeChannelName = channelName;
                _isConnected = true;
            }
            catch (Exception ex)
            {
                var label = string.IsNullOrWhiteSpace(channelName) ? redisChannel : channelName;
                Debug.LogError($"RedisPhysioMetricsPump: Failed to connect to Redis at {redisHost}:{redisPort} ({label}): {ex.Message}", this);
            }
            finally
            {
                _connectionInProgress = false;
            }
        }

        private async Task StopConnectionAsync()
        {
            if (!_isConnected || _connectionInProgress)
            {
                return;
            }

            _connectionInProgress = true;
            string channelName = string.Empty;
            try
            {
                if (!TryGetSubscribedChannel(out channelName))
                {
                    _isConnected = false;
                    return;
                }

                await RedisSubscriber.UnregisterChannelAsync(channelName);
                _activeChannelName = string.Empty;
                _isConnected = false;
            }
            catch (Exception ex)
            {
                var label = string.IsNullOrWhiteSpace(channelName) ? redisChannel : channelName;
                Debug.LogError($"RedisPhysioMetricsPump: Failed to clean up Redis connection for channel '{label}': {ex.Message}", this);
            }
            finally
            {
                _connectionInProgress = false;
            }
        }

        private async Task RestartConnectionAsync()
        {
            await WaitForIdleAsync();

            if (_isConnected)
            {
                await StopConnectionAsync();
            }

            await StartConnectionAsync();
        }

        private async Task EnsureConnectionAsync()
        {
            await WaitForIdleAsync();
            await StartConnectionAsync();
        }

        private async Task TeardownConnectionAsync()
        {
            await WaitForIdleAsync();
            await StopConnectionAsync();
        }

        private async Task WaitForIdleAsync()
        {
            while (_connectionInProgress)
            {
                await Task.Yield();
            }
        }

        private bool TryResolveConfiguredChannel(out string channelName)
        {
            if (useDeviceChannelTemplate)
            {
                channelName = FormatDeviceChannel(deviceId);
                if (!string.IsNullOrWhiteSpace(channelName))
                {
                    return true;
                }

                Debug.LogWarning($"RedisPhysioMetricsPump[{SourceId}] has an invalid device channel configuration.", this);
                return false;
            }

            channelName = string.IsNullOrWhiteSpace(redisChannel) ? string.Empty : redisChannel.Trim();
            if (string.IsNullOrWhiteSpace(channelName))
            {
                Debug.LogWarning($"RedisPhysioMetricsPump[{SourceId}] has no Redis channel configured.", this);
                return false;
            }

            return true;
        }

        private bool TryGetSubscribedChannel(out string channelName)
        {
            if (!string.IsNullOrWhiteSpace(_activeChannelName))
            {
                channelName = _activeChannelName;
                return true;
            }

            return TryResolveConfiguredChannel(out channelName);
        }

        private string ResolveDeviceId()
        {
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                return deviceId.Trim();
            }

            if (TryGetSubscribedChannel(out var channelName) && TryParseDeviceId(channelName, out var parsedDeviceId))
            {
                return parsedDeviceId;
            }

            return SourceId;
        }

        private static string FormatDeviceChannel(string value)
        {
            return RedisAudienceChannels.FormatPhysioMetricsChannel(value);
        }

        private static bool TryParseDeviceId(string channelName, out string parsedDeviceId)
        {
            return RedisAudienceChannels.TryParsePhysioMetricsDeviceChannel(channelName, out parsedDeviceId);
        }

        private string TryResolveGameObjectName()
        {
            try
            {
                if (gameObject != null && !string.IsNullOrEmpty(gameObject.name))
                {
                    return gameObject.name;
                }

                return string.IsNullOrEmpty(name) ? string.Empty : name;
            }
            catch (MissingReferenceException)
            {
                return string.Empty;
            }
            catch (NullReferenceException)
            {
                return string.Empty;
            }
        }
    }
}
