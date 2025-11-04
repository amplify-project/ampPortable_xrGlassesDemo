using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;
using StackExchange.Redis;
using UnityEngine;

public static class RedisSubscriber
{
    private static readonly ConcurrentQueue<RedisMessage> _messageQueue = new();
    private static ConnectionMultiplexer? _redis;
    private static ISubscriber? _subscriber;
    private static bool _isInitialized;
    private static string _host = "192.168.0.6";
    private static int _port = 6379;
    private static string _channel = "sensor_data";

    public static async Task Begin(string host = "192.168.0.6", int port = 6379, string channelName = "sensor_data")
    {
        if (_isInitialized &&
            string.Equals(_host, host, StringComparison.OrdinalIgnoreCase) &&
            _port == port &&
            string.Equals(_channel, channelName, StringComparison.Ordinal))
        {
            return;
        }

        if (_isInitialized)
        {
            await CleanupAsync();
        }

        _host = host;
        _port = port;
        _channel = channelName;

        try
        {
            await ConnectToRedis();
            await SubscribeToChannel(_channel);
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"RedisSubscriber failed to begin: {ex.Message}");
        }
    }

    public static bool TryDequeue(out RedisMessage message) => _messageQueue.TryDequeue(out message);

    public static async Task CleanupAsync()
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
    }

    private static async Task ConnectToRedis()
    {
        var config = new ConfigurationOptions
        {
            EndPoints = { $"{_host}:{_port}" },
            ConnectTimeout = 5000,
            SyncTimeout = 5000
        };

        _redis = await ConnectionMultiplexer.ConnectAsync(config);
        _subscriber = _redis.GetSubscriber();
        Debug.Log("Connected to Redis.");
    }

    private static async Task SubscribeToChannel(string channelName)
    {
        if (_subscriber == null)
        {
            throw new InvalidOperationException("Redis subscriber not initialized.");
        }

        await _subscriber.SubscribeAsync(channelName, (_, message) =>
        {
            try
            {
                ProcessMessage(message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Redis message processing failed: {ex.Message}");
            }
        });

        Debug.Log($"Subscribed to Redis channel '{channelName}'.");
    }

    private static void ProcessMessage(RedisValue message)
    {
        if (message.IsNullOrEmpty)
        {
            return;
        }

        var messageText = message.ToString();

        try
        {
            using var jsonDoc = JsonDocument.Parse(messageText);
            var root = jsonDoc.RootElement;

            var value = root.TryGetProperty("value", out var valueProp) ? valueProp.GetDouble() : 0d;
            var sequence = root.TryGetProperty("sequence", out var sequenceProp) ? sequenceProp.GetInt32() : -1;
            var timestamp = root.TryGetProperty("timestamp", out var timestampProp)
                ? timestampProp.GetString() ?? string.Empty
                : string.Empty;

            _messageQueue.Enqueue(new RedisMessage(value, sequence, timestamp));
        }
        catch (JsonException ex)
        {
            Debug.LogError($"Invalid Redis JSON payload: {ex.Message}\nPayload: {messageText}");
        }
    }

    public readonly struct RedisMessage
    {
        public RedisMessage(double value, int sequence, string timestamp)
        {
            Value = value;
            Sequence = sequence;
            Timestamp = timestamp;
        }

        public double Value { get; }
        public int Sequence { get; }
        public string Timestamp { get; }
    }
}
