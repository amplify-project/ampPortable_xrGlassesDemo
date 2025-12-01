using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;
using UnityEngine;

public static class RedisSubscriber
{
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<RedisMessage>> _channelQueues = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, int> _channelRefCounts = new(StringComparer.Ordinal);
    private static readonly SemaphoreSlim _connectionLock = new(1, 1);
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
            EndPoints = { $"{_host}:{_port}" },
            ConnectTimeout = 5000,
            SyncTimeout = 5000,
            AbortOnConnectFail = false
        };

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
            using var jsonDoc = JsonDocument.Parse(messageText);
            var root = jsonDoc.RootElement;

            var parsedMessage = ParseRedisMessage(channelName, root);
            targetQueue.Enqueue(parsedMessage);

            if (channelName.EndsWith(":hr_filtered", StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"RedisSubscriber[{channelName}] value={parsedMessage.Value:F4} seq={parsedMessage.Sequence} ts={parsedMessage.Timestamp}");
            }
        }
        catch (JsonException ex)
        {
            Debug.LogError($"Invalid Redis JSON payload: {ex.Message}\nPayload: {messageText}");
        }
    }

    private static RedisMessage ParseRedisMessage(string channelName, JsonElement rootElement)
    {
        if (rootElement.ValueKind == JsonValueKind.Object)
        {
            var value = rootElement.TryGetProperty("value", out var valueProp)
                ? ReadAsDouble(valueProp)
                : ExtractFirstNumericValue(rootElement);

            var sequence = rootElement.TryGetProperty("sequence", out var sequenceProp)
                ? ReadAsInt(sequenceProp)
                : -1;

            var timestamp = rootElement.TryGetProperty("timestamp", out var timestampProp)
                ? ReadAsString(timestampProp)
                : string.Empty;

            return new RedisMessage(channelName, value, sequence, timestamp);
        }

        // Support payloads that are just a primitive (e.g., raw numbers or numeric strings)
        var primitiveValue = ReadAsDouble(rootElement);
        return new RedisMessage(channelName, primitiveValue, -1, string.Empty);
    }

    private static double ExtractFirstNumericValue(JsonElement rootElement)
    {
        foreach (var property in rootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object ||
                property.Value.ValueKind == JsonValueKind.Array)
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

    private static double ReadAsDouble(JsonElement element, double fallback = 0d)
    {
        try
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    return element.GetDouble();
                case JsonValueKind.String:
                    if (double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                    {
                        return parsed;
                    }
                    break;
                case JsonValueKind.True:
                    return 1d;
                case JsonValueKind.False:
                    return 0d;
            }
        }
        catch
        {
            // ignored on purpose
        }

        return fallback;
    }

    private static int ReadAsInt(JsonElement element, int fallback = -1)
    {
        try
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out var intVal))
                    {
                        return intVal;
                    }

                    var asDouble = element.GetDouble();
                    return (int)Math.Round(asDouble);
                case JsonValueKind.String:
                    if (int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
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

    private static string ReadAsString(JsonElement element)
    {
        try
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString() ?? string.Empty;
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return element.GetRawText();
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
        public RedisMessage(string channel, double value, int sequence, string timestamp)
        {
            Channel = channel;
            Value = value;
            Sequence = sequence;
            Timestamp = timestamp;
        }

        public string Channel { get; }
        public double Value { get; }
        public int Sequence { get; }
        public string Timestamp { get; }
    }
}

public enum EmotionChannelKind
{
    Broadcast,
    Valence,
    Arousal,
    HeartRate
}

public static class RedisEmotionChannels
{
    public const string BroadcastChannel = "emotion_scores";
    public const string ValenceTemplate = "device:{0}:valence_cont";
    public const string ArousalTemplate = "device:{0}:arousal_cont";
    public const string HeartRateTemplate = "device:{0}:hr_filtered";

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
            default:
                channelName = string.Empty;
                return false;
        }
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
