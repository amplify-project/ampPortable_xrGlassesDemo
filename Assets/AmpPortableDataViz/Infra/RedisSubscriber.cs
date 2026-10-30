using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using UnityEngine;

public static class RedisSubscriber
{
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<RedisMessage>> _channelQueues = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, int> _channelRefCounts = new(StringComparer.Ordinal);
    private static readonly SemaphoreSlim _connectionLock = new(1, 1);
    private static readonly string[] EngagementAggregatePropertyNames =
    {
        "value",
        "engagement",
        "engagement_score",
        "engagementScore",
        "avg_engagement",
        "average_engagement",
        "mean_engagement",
        "overall_engagement",
        "average",
        "mean",
        "score"
    };
    private static readonly string[] EngagementScorePropertyNames =
    {
        "value",
        "score",
        "engagement",
        "engagement_score",
        "engagementScore"
    };
    private static readonly string[] EngagementScoreContainerPropertyNames =
    {
        "scores",
        "engagement_scores",
        "engagementScores",
        "devices",
        "participants",
        "audience",
        "values"
    };
    private static readonly string[] NumericMetadataPropertyNames =
    {
        "timestamp",
        "time",
        "sequence",
        "seq",
        "count",
        "sample_count",
        "score_count",
        "device",
        "device_id",
        "deviceId",
        "id",
        "source",
        "channel"
    };
    private static ConnectionMultiplexer? _redis;
    private static ISubscriber? _subscriber;
    private static bool _isInitialized;
    private static string _host = "192.168.0.6";
    private static int _port = 6379;
    private static string _defaultChannel = RedisEmotionChannels.BroadcastChannel;

    public static Task Begin(string host = "192.168.0.6", int port = 6379, params string[] channelNames)
    {
        if (channelNames == null || channelNames.Length == 0)
        {
            channelNames = new[] { _defaultChannel };
        }

        return Task.WhenAll(channelNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => RegisterChannelAsync(host, port, name)));
    }

    public static Task BeginEmotionChannelsAsync(string host, int port, IEnumerable<string>? deviceIds = null, bool includeBroadcast = true)
    {
        var channels = RedisEmotionChannels
            .BuildChannelList(deviceIds, includeBroadcast)
            .ToArray();

        return Begin(host, port, channels);
    }

    public static bool TryDequeue(string channelName, out RedisMessage message)
    {
        message = default;

        if (string.IsNullOrWhiteSpace(channelName))
        {
            return false;
        }

        if (!_channelQueues.TryGetValue(channelName, out var queue))
        {
            return false;
        }

        return queue.TryDequeue(out message);
    }

    internal static bool TryParsePayload(string channelName, string payload, out RedisMessage message)
    {
        message = default;

        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        try
        {
            var root = JToken.Parse(payload);
            var normalizedChannel = NormalizeChannelName(channelName) ?? string.Empty;
            message = ParseRedisMessage(normalizedChannel, root, payload);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static async Task RegisterChannelAsync(string host, int port, string channelName)
    {
        var normalizedChannel = NormalizeChannelName(channelName);
        if (normalizedChannel == null)
        {
            throw new ArgumentException("Channel name must be provided.", nameof(channelName));
        }

        await _connectionLock.WaitAsync();
        try
        {
            await EnsureConnectionAsync(host, port);

            if (_channelRefCounts.AddOrUpdate(normalizedChannel, 1, (_, count) => count + 1) == 1)
            {
                await SubscribeToChannel(normalizedChannel);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"RedisSubscriber failed to register channel '{normalizedChannel}': {ex.Message}");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public static async Task UnregisterChannelAsync(string channelName)
    {
        var normalizedChannel = NormalizeChannelName(channelName);
        if (normalizedChannel == null)
        {
            return;
        }

        await _connectionLock.WaitAsync();
        try
        {
            if (!_channelRefCounts.TryGetValue(normalizedChannel, out var count))
            {
                return;
            }

            if (count <= 1)
            {
                _channelRefCounts.TryRemove(normalizedChannel, out _);
                await UnsubscribeFromChannel(normalizedChannel);
                _channelQueues.TryRemove(normalizedChannel, out _);
            }
            else
            {
                _channelRefCounts[normalizedChannel] = count - 1;
            }

            if (_channelRefCounts.IsEmpty)
            {
                await CleanupInternalAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"RedisSubscriber failed to unregister channel '{normalizedChannel}': {ex.Message}");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public static async Task CleanupAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            await CleanupInternalAsync();
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private static async Task ConnectToRedis()
    {
        var config = new ConfigurationOptions
        {
            ConnectTimeout = 5000,
            SyncTimeout = 5000,
            AbortOnConnectFail = false
        };
        config.EndPoints.Add(_host, _port);

        Debug.Log($"Connecting to Redis at {_host}:{_port}...");
        _redis = await ConnectionMultiplexer.ConnectAsync(config);
        _subscriber = _redis.GetSubscriber();
        Debug.Log("Connected to Redis.");
    }

    private static async Task EnsureConnectionAsync(string host, int port)
    {
        if (_isInitialized)
        {
            bool sameEndpoint = string.Equals(_host, host, StringComparison.OrdinalIgnoreCase) && _port == port;
            if (sameEndpoint)
            {
                return;
            }

            // Host changed; tear down and reconnect.
            await CleanupInternalAsync();
        }

        _host = host;
        _port = port;

        await ConnectToRedis();
        _isInitialized = true;
    }

    private static async Task SubscribeToChannel(string channelName)
    {
        if (_subscriber == null)
        {
            throw new InvalidOperationException("Redis subscriber not initialized.");
        }

        var queue = _channelQueues.GetOrAdd(channelName, _ => new ConcurrentQueue<RedisMessage>());

        await _subscriber.SubscribeAsync(channelName, (redisChannel, message) =>
        {
            try
            {
                ProcessMessage(redisChannel, message, queue);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Redis message processing failed for channel '{redisChannel}': {ex.Message}");
            }
        });

        Debug.Log($"Subscribed to Redis channel '{channelName}'.");
    }

    private static async Task UnsubscribeFromChannel(string channelName)
    {
        if (_subscriber == null)
        {
            return;
        }

        await _subscriber.UnsubscribeAsync(channelName);
        Debug.Log($"Unsubscribed from Redis channel '{channelName}'.");
    }

    private static void ProcessMessage(RedisChannel channel, RedisValue message, ConcurrentQueue<RedisMessage> targetQueue)
    {
        if (message.IsNullOrEmpty)
        {
            return;
        }

        var messageText = message.ToString();
        var channelName = channel.ToString();

        try
        {
            if (!TryParsePayload(channelName, messageText, out var parsedMessage))
            {
                Debug.LogError($"Invalid Redis JSON payload.\nPayload: {messageText}");
                return;
            }

            targetQueue.Enqueue(parsedMessage);

            bool isHeartRateChannel = channelName.EndsWith(":hr_filtered", StringComparison.OrdinalIgnoreCase);
            bool isArousalChannel =
                channelName.EndsWith(":arousal_cont", StringComparison.OrdinalIgnoreCase) ||
                channelName.EndsWith(":arousal", StringComparison.OrdinalIgnoreCase);
            bool isEdaChannel = channelName.EndsWith(":eda_filtered", StringComparison.OrdinalIgnoreCase);

            if (isHeartRateChannel || isArousalChannel || isEdaChannel)
            {
                Debug.Log($"RedisSubscriber[{channelName}] value={parsedMessage.Value:F4} seq={parsedMessage.Sequence} ts={parsedMessage.Timestamp}");
            }
        }
        catch (JsonException ex)
        {
            Debug.LogError($"Invalid Redis JSON payload: {ex.Message}\nPayload: {messageText}");
        }
    }

    private static RedisMessage ParseRedisMessage(string channelName, JToken rootElement, string rawPayload)
    {
        if (rootElement.Type == JTokenType.Object)
        {
            var rootObject = (JObject)rootElement;
            double value;
            if (rootObject.TryGetValue("value", StringComparison.Ordinal, out var valueProp))
            {
                value = ReadAsDouble(valueProp);
            }
            else if (IsEngagementScoresChannel(channelName) && TryExtractEngagementScore(rootElement, out var engagementValue))
            {
                value = engagementValue;
            }
            else
            {
                value = ExtractFirstNumericValue(rootElement);
            }

            var sequence = rootObject.TryGetValue("sequence", StringComparison.Ordinal, out var sequenceProp)
                ? ReadAsInt(sequenceProp)
                : -1;

            var timestamp = rootObject.TryGetValue("timestamp", StringComparison.Ordinal, out var timestampProp)
                ? ReadAsString(timestampProp)
                : string.Empty;

            return new RedisMessage(channelName, value, sequence, timestamp, rawPayload);
        }

        // Support payloads that are just a primitive (e.g., raw numbers or numeric strings)
        var primitiveValue = ReadAsDouble(rootElement);
        return new RedisMessage(channelName, primitiveValue, -1, string.Empty, rawPayload);
    }

    private static bool TryExtractEngagementScore(JToken rootElement, out double value)
    {
        if (TryReadNamedNumericProperty(rootElement, EngagementAggregatePropertyNames, out value))
        {
            return true;
        }

        if (TryAverageNumericInNamedContainers(rootElement, out value))
        {
            return true;
        }

        if (TryAverageDirectNumericProperties(rootElement, out value))
        {
            return true;
        }

        if (TryAverageNamedNumericLeaves(rootElement, EngagementScorePropertyNames, out value))
        {
            return true;
        }

        return TryAverageNumericLeaves(rootElement, out value);
    }

    private static bool TryAverageNumericInNamedContainers(JToken element, out double value)
    {
        value = 0d;

        if (element.Type == JTokenType.Object)
        {
            foreach (var property in element.Children<JProperty>())
            {
                if (MatchesAny(property.Name, EngagementScoreContainerPropertyNames))
                {
                    if (TryAverageNamedNumericLeaves(property.Value, EngagementScorePropertyNames, out value) ||
                        TryAverageNumericLeaves(property.Value, out value))
                    {
                        return true;
                    }
                }

                if (TryAverageNumericInNamedContainers(property.Value, out value))
                {
                    return true;
                }
            }
        }
        else if (element.Type == JTokenType.Array)
        {
            foreach (var item in element.Children())
            {
                if (TryAverageNumericInNamedContainers(item, out value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryAverageDirectNumericProperties(JToken element, out double value)
    {
        value = 0d;

        if (element.Type != JTokenType.Object)
        {
            return false;
        }

        double sum = 0d;
        int count = 0;
        foreach (var property in element.Children<JProperty>())
        {
            if (IsNumericMetadataProperty(property.Name))
            {
                continue;
            }

            var numeric = ReadAsDouble(property.Value, double.NaN);
            if (IsUsableNumeric(numeric))
            {
                sum += numeric;
                count++;
            }
        }

        if (count == 0)
        {
            return false;
        }

        value = sum / count;
        return true;
    }

    private static bool TryAverageNamedNumericLeaves(JToken element, string[] propertyNames, out double value)
    {
        double sum = 0d;
        int count = 0;
        AccumulateNamedNumericLeaves(element, propertyNames, ref sum, ref count);

        if (count == 0)
        {
            value = 0d;
            return false;
        }

        value = sum / count;
        return true;
    }

    private static void AccumulateNamedNumericLeaves(JToken element, string[] propertyNames, ref double sum, ref int count)
    {
        if (element.Type == JTokenType.Object)
        {
            foreach (var property in element.Children<JProperty>())
            {
                if (MatchesAny(property.Name, propertyNames))
                {
                    var numeric = ReadAsDouble(property.Value, double.NaN);
                    if (IsUsableNumeric(numeric))
                    {
                        sum += numeric;
                        count++;
                        continue;
                    }
                }

                AccumulateNamedNumericLeaves(property.Value, propertyNames, ref sum, ref count);
            }
        }
        else if (element.Type == JTokenType.Array)
        {
            foreach (var item in element.Children())
            {
                AccumulateNamedNumericLeaves(item, propertyNames, ref sum, ref count);
            }
        }
    }

    private static bool TryAverageNumericLeaves(JToken element, out double value)
    {
        double sum = 0d;
        int count = 0;
        AccumulateNumericLeaves(element, ref sum, ref count);

        if (count == 0)
        {
            value = 0d;
            return false;
        }

        value = sum / count;
        return true;
    }

    private static void AccumulateNumericLeaves(JToken element, ref double sum, ref int count)
    {
        switch (element.Type)
        {
            case JTokenType.Object:
                foreach (var property in element.Children<JProperty>())
                {
                    if (IsNumericMetadataProperty(property.Name))
                    {
                        continue;
                    }

                    AccumulateNumericLeaves(property.Value, ref sum, ref count);
                }
                break;
            case JTokenType.Array:
                foreach (var item in element.Children())
                {
                    AccumulateNumericLeaves(item, ref sum, ref count);
                }
                break;
            default:
                var numeric = ReadAsDouble(element, double.NaN);
                if (IsUsableNumeric(numeric))
                {
                    sum += numeric;
                    count++;
                }
                break;
        }
    }

    private static bool TryReadNamedNumericProperty(JToken element, string[] propertyNames, out double value)
    {
        value = 0d;

        if (element.Type != JTokenType.Object)
        {
            return false;
        }

        foreach (var property in element.Children<JProperty>())
        {
            if (!MatchesAny(property.Name, propertyNames))
            {
                continue;
            }

            var numeric = ReadAsDouble(property.Value, double.NaN);
            if (IsUsableNumeric(numeric))
            {
                value = numeric;
                return true;
            }
        }

        return false;
    }

    private static double ExtractFirstNumericValue(JToken rootElement)
    {
        foreach (var property in rootElement.Children<JProperty>())
        {
            if (property.Value.Type == JTokenType.Object ||
                property.Value.Type == JTokenType.Array)
            {
                continue;
            }

            var numeric = ReadAsDouble(property.Value, double.NaN);
            if (!double.IsNaN(numeric))
            {
                return numeric;
            }
        }

        return 0d;
    }

    private static double ReadAsDouble(JToken element, double fallback = 0d)
    {
        try
        {
            switch (element.Type)
            {
                case JTokenType.Integer:
                case JTokenType.Float:
                    return element.Value<double>();
                case JTokenType.String:
                    if (double.TryParse(element.Value<string>(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                    {
                        return parsed;
                    }
                    break;
                case JTokenType.Boolean:
                    return element.Value<bool>() ? 1d : 0d;
            }
        }
        catch
        {
            // ignored on purpose
        }

        return fallback;
    }

    private static bool IsEngagementScoresChannel(string channelName)
    {
        return RedisAudienceChannels.IsEngagementScoresChannel(channelName);
    }

    private static bool IsNumericMetadataProperty(string propertyName)
    {
        return MatchesAny(propertyName, NumericMetadataPropertyNames);
    }

    private static bool MatchesAny(string value, string[] candidates)
    {
        for (int i = 0; i < candidates.Length; i++)
        {
            if (string.Equals(value, candidates[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUsableNumeric(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static int ReadAsInt(JToken element, int fallback = -1)
    {
        try
        {
            switch (element.Type)
            {
                case JTokenType.Integer:
                    return element.Value<int>();
                case JTokenType.Float:
                    return (int)Math.Round(element.Value<double>());
                case JTokenType.String:
                    if (int.TryParse(element.Value<string>(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    {
                        return parsed;
                    }
                    break;
            }
        }
        catch
        {
            // ignored on purpose
        }

        return fallback;
    }

    private static string ReadAsString(JToken element)
    {
        try
        {
            switch (element.Type)
            {
                case JTokenType.String:
                    return element.Value<string>() ?? string.Empty;
                case JTokenType.Integer:
                case JTokenType.Float:
                case JTokenType.Boolean:
                    return element.ToString(Formatting.None);
            }
        }
        catch
        {
            // ignored on purpose
        }

        return string.Empty;
    }

    private static string? NormalizeChannelName(string channelName)
    {
        if (string.IsNullOrWhiteSpace(channelName))
        {
            return null;
        }

        return channelName.Trim();
    }

    private static async Task CleanupInternalAsync()
    {
        _isInitialized = false;

        if (_subscriber != null)
        {
            await _subscriber.UnsubscribeAllAsync();
            _subscriber = null;
        }

        if (_redis != null)
        {
            await _redis.CloseAsync();
            _redis.Dispose();
            _redis = null;
        }

        _channelQueues.Clear();
        _channelRefCounts.Clear();
    }

    public readonly struct RedisMessage
    {
        public RedisMessage(string channel, double value, int sequence, string timestamp, string rawPayload = "")
        {
            Channel = channel;
            Value = value;
            Sequence = sequence;
            Timestamp = timestamp;
            RawPayload = rawPayload ?? string.Empty;
        }

        public string Channel { get; }
        public double Value { get; }
        public int Sequence { get; }
        public string Timestamp { get; }
        public string RawPayload { get; }
    }
}

public enum EmotionChannelKind
{
    Broadcast,
    Valence,
    Arousal,
    HeartRate,
    EdaFiltered,
    PhysioMetrics
}

public static class RedisAudienceChannels
{
    public const string EngagementScoresChannel = "engagement:scores";
    public const string LegacyEngagementScoreChannel = "engagement_score";
    public const string AmplifyEngagementChannel = "amplify.engagement.engagement";
    public const string PhysioMetricsTemplate = "device:{0}:physio_metrics";
    public const string SensorEngagementTemplate = "device:{serial}:engagement";

    public static bool IsEngagementScoresChannel(string channelName)
    {
        if (string.IsNullOrWhiteSpace(channelName))
        {
            return false;
        }

        var normalized = channelName.Trim();
        return string.Equals(normalized, EngagementScoresChannel, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, LegacyEngagementScoreChannel, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, AmplifyEngagementChannel, StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatPhysioMetricsChannel(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return string.Empty;
        }

        return string.Format(CultureInfo.InvariantCulture, PhysioMetricsTemplate, deviceId.Trim());
    }

    public static string FormatSensorEngagementChannel(string? sensorSerial)
    {
        if (string.IsNullOrWhiteSpace(sensorSerial))
        {
            return string.Empty;
        }

        return SensorEngagementTemplate.Replace("{serial}", sensorSerial.Trim());
    }

    public static bool TryParsePhysioMetricsDeviceChannel(string channelName, out string deviceId)
    {
        deviceId = string.Empty;

        if (string.IsNullOrWhiteSpace(channelName))
        {
            return false;
        }

        const string prefix = "device:";
        const string suffix = ":physio_metrics";
        if (!channelName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !channelName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int start = prefix.Length;
        int length = channelName.Length - prefix.Length - suffix.Length;
        if (length <= 0)
        {
            return false;
        }

        deviceId = channelName.Substring(start, length).Trim();
        return deviceId.Length > 0;
    }
}

public static class RedisEmotionChannels
{
    public const string BroadcastChannel = "emotion_scores";
    public const string ValenceTemplate = "device:{0}:valence_cont";
    public const string ArousalTemplate = "device:{0}:arousal_cont";
    public const string HeartRateTemplate = "device:{0}:hr_filtered";
    public const string EdaFilteredTemplate = "device:{0}:eda_filtered";

    private static readonly string[] _defaultDeviceIds =
    {
        "MD-V5-0000448",
        "MD-V5-0000334"
    };

    public static IReadOnlyList<string> DefaultDeviceIds => _defaultDeviceIds;

    public static string[] GetDefaultDeviceIds()
    {
        return (string[])_defaultDeviceIds.Clone();
    }

    public static IReadOnlyList<string> BuildChannelList(IEnumerable<string>? deviceIds = null, bool includeBroadcast = true)
    {
        var orderedChannels = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddChannel(string? channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
            {
                return;
            }

            if (seen.Add(channel))
            {
                orderedChannels.Add(channel);
            }
        }

        if (includeBroadcast)
        {
            AddChannel(BroadcastChannel);
        }

        var normalizedIds = (deviceIds ?? _defaultDeviceIds)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var deviceId in normalizedIds)
        {
            if (TryFormatChannel(EmotionChannelKind.Valence, deviceId, out var valenceChannel))
            {
                AddChannel(valenceChannel);
            }

            if (TryFormatChannel(EmotionChannelKind.Arousal, deviceId, out var arousalChannel))
            {
                AddChannel(arousalChannel);
            }

            if (TryFormatChannel(EmotionChannelKind.HeartRate, deviceId, out var heartRateChannel))
            {
                AddChannel(heartRateChannel);
            }

            if (TryFormatChannel(EmotionChannelKind.EdaFiltered, deviceId, out var edaFilteredChannel))
            {
                AddChannel(edaFilteredChannel);
            }
        }

        return orderedChannels;
    }

    public static bool TryFormatChannel(EmotionChannelKind channelKind, string? deviceId, out string channelName)
    {
        switch (channelKind)
        {
            case EmotionChannelKind.Broadcast:
                channelName = BroadcastChannel;
                return true;
            case EmotionChannelKind.Valence:
                channelName = FormatDeviceChannel(ValenceTemplate, deviceId);
                return !string.IsNullOrWhiteSpace(channelName);
            case EmotionChannelKind.Arousal:
                channelName = FormatDeviceChannel(ArousalTemplate, deviceId);
                return !string.IsNullOrWhiteSpace(channelName);
            case EmotionChannelKind.HeartRate:
                channelName = FormatDeviceChannel(HeartRateTemplate, deviceId);
                return !string.IsNullOrWhiteSpace(channelName);
            case EmotionChannelKind.EdaFiltered:
                channelName = FormatDeviceChannel(EdaFilteredTemplate, deviceId);
                return !string.IsNullOrWhiteSpace(channelName);
            default:
                channelName = string.Empty;
                return false;
        }
    }

    public static bool TryParseDeviceChannel(string channelName, out string deviceId, out EmotionChannelKind channelKind)
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
            suffix.Equals("valence", StringComparison.OrdinalIgnoreCase))
        {
            channelKind = EmotionChannelKind.Valence;
        }
        else if (suffix.Equals("arousal_cont", StringComparison.OrdinalIgnoreCase) ||
                 suffix.Equals("arousal", StringComparison.OrdinalIgnoreCase))
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

    private static string FormatDeviceChannel(string template, string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return string.Empty;
        }

        return string.Format(CultureInfo.InvariantCulture, template, deviceId.Trim());
    }
}
