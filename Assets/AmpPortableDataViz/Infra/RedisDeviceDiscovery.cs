using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using StackExchange.Redis;
using UnityEngine;

namespace AmpPortableDataViz.Infra
{
    public static class RedisDeviceDiscovery
    {
        public static async Task<bool> CanConnectAsync(string host, int port, int timeoutMs = 2000)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return false;
            }

            ConnectionMultiplexer redis = null;
            try
            {
                redis = await ConnectionMultiplexer.ConnectAsync(CreateConfigurationOptions(host, port, timeoutMs));
                if (redis == null)
                {
                    Debug.LogWarning($"RedisDeviceDiscovery: Connection multiplexer was null for Redis at {host}:{port}.");
                    return false;
                }

                await redis.GetDatabase().PingAsync();
                Debug.Log($"RedisDeviceDiscovery: Verified Redis connection at {host}:{port}.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"RedisDeviceDiscovery: Failed to verify Redis at {host}:{port} ({ex.Message})");
                return false;
            }
            finally
            {
                await CloseConnectionAsync(redis);
            }
        }

        public static async Task<IReadOnlyList<string>> DiscoverDeviceIdsAsync(string host, int port, int timeoutMs = 2000)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return Array.Empty<string>();
            }

            ConnectionMultiplexer redis = null;
            try
            {
                redis = await ConnectionMultiplexer.ConnectAsync(CreateConfigurationOptions(host, port, timeoutMs));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"RedisDeviceDiscovery: Failed to connect to Redis at {host}:{port} ({ex.Message})");
                return Array.Empty<string>();
            }

            try
            {
                var subscriber = redis.GetSubscriber();
                if (subscriber == null)
                {
                    return Array.Empty<string>();
                }

                var channelPatterns = new[]
                {
                    "device:*:physio_metrics",
                    "device:*:valence_cont",
                    "device:*:arousal_cont",
                    "device:*:hr_filtered"
                };
                Debug.Log($"RedisDeviceDiscovery: Listening for device IDs on '{string.Join(", ", channelPatterns)}' at {host}:{port} for {timeoutMs}ms");
                var deviceIds = await DiscoverDeviceIdsFromPatternsAsync(subscriber, channelPatterns, timeoutMs);
                if (deviceIds.Count > 0)
                {
                    Debug.Log($"RedisDeviceDiscovery: Discovered device IDs: {string.Join(", ", deviceIds)}");
                }
                else
                {
                    Debug.Log("RedisDeviceDiscovery: No device IDs discovered.");
                }

                return deviceIds;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"RedisDeviceDiscovery: Failed to query channels from Redis at {host}:{port} ({ex.Message})");
                return Array.Empty<string>();
            }
            finally
            {
                await CloseConnectionAsync(redis);
            }
        }

        private static ConfigurationOptions CreateConfigurationOptions(string host, int port, int timeoutMs)
        {
            int effectiveTimeoutMs = Mathf.Max(1, timeoutMs);
            return new ConfigurationOptions
            {
                EndPoints = { $"{host}:{port}" },
                ConnectTimeout = effectiveTimeoutMs,
                SyncTimeout = effectiveTimeoutMs,
                AbortOnConnectFail = false
            };
        }

        private static async Task CloseConnectionAsync(ConnectionMultiplexer redis)
        {
            try
            {
                if (redis != null)
                {
                    await redis.CloseAsync();
                    redis.Dispose();
                }
            }
            catch
            {
                // ignored on purpose
            }
        }

        private static async Task<IReadOnlyList<string>> DiscoverDeviceIdsFromPatternsAsync(ISubscriber subscriber, IReadOnlyList<string> channelPatterns, int timeoutMs)
        {
            var ordered = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var gate = new object();

            if (subscriber == null || channelPatterns == null || channelPatterns.Count == 0)
            {
                return ordered;
            }

            var subscribedPatterns = new List<RedisChannel>();
            try
            {
                foreach (var channelPattern in channelPatterns)
                {
                    if (string.IsNullOrWhiteSpace(channelPattern))
                    {
                        continue;
                    }

                    var patternChannel = RedisChannel.Pattern(channelPattern);
                    subscribedPatterns.Add(patternChannel);
                    await subscriber.SubscribeAsync(patternChannel, (redisChannel, message) =>
                    {
                        var payload = message.ToString();
                        string channelName = redisChannel.ToString();
                        if (RedisEmotionChannels.TryParseDeviceChannel(channelName, out var deviceId, out _) ||
                            RedisAudienceChannels.TryParsePhysioMetricsDeviceChannel(channelName, out deviceId))
                        {
                            AddDeviceId(deviceId, ordered, seen, gate, requireHeuristic: false);
                        }

                        if (!string.IsNullOrWhiteSpace(payload))
                        {
                            ExtractDeviceIdsFromPayload(payload, ordered, seen, gate);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"RedisDeviceDiscovery: Failed to subscribe to device channel patterns ({ex.Message})");
                return ordered;
            }

            int waitMs = Mathf.Max(0, timeoutMs);
            if (waitMs > 0)
            {
                await Task.Delay(waitMs);
            }

            try
            {
                foreach (var patternChannel in subscribedPatterns)
                {
                    await subscriber.UnsubscribeAsync(patternChannel);
                }
            }
            catch
            {
                // ignored on purpose
            }

            return ordered;
        }

        private static void ExtractDeviceIdsFromPayload(string payload, List<string> ordered, HashSet<string> seen, object gate)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return;
            }

            try
            {
                using var jsonDoc = JsonDocument.Parse(payload);
                ExtractDeviceIdsFromJson(jsonDoc.RootElement, ordered, seen, gate, false);
                return;
            }
            catch (JsonException)
            {
                // Fall back to raw payload inspection below.
            }

            if (LooksLikeDeviceId(payload))
            {
                AddDeviceId(payload, ordered, seen, gate, requireHeuristic: false);
            }
        }

        private static void ExtractDeviceIdsFromJson(JsonElement element, List<string> ordered, HashSet<string> seen, object gate, bool deviceContext)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        string normalizedName = NormalizeKey(property.Name);
                        bool isDeviceProperty = IsDevicePropertyName(normalizedName);
                        bool isDeviceContext = deviceContext || isDeviceProperty;

                        if (isDeviceProperty)
                        {
                            ExtractDeviceIdsFromJson(property.Value, ordered, seen, gate, true);
                            continue;
                        }

                        if (deviceContext && normalizedName == "id")
                        {
                            ExtractDeviceIdsFromJson(property.Value, ordered, seen, gate, true);
                            continue;
                        }

                        if (property.Value.ValueKind == JsonValueKind.Object || property.Value.ValueKind == JsonValueKind.Array)
                        {
                            ExtractDeviceIdsFromJson(property.Value, ordered, seen, gate, isDeviceContext);
                        }
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        ExtractDeviceIdsFromJson(item, ordered, seen, gate, deviceContext);
                    }
                    break;
                case JsonValueKind.String:
                    AddDeviceId(element.GetString(), ordered, seen, gate, requireHeuristic: !deviceContext);
                    break;
            }
        }

        private static void AddDeviceId(string value, List<string> ordered, HashSet<string> seen, object gate, bool requireHeuristic)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var trimmed = value.Trim();
            if (requireHeuristic && !LooksLikeDeviceId(trimmed))
            {
                return;
            }

            lock (gate)
            {
                if (seen.Add(trimmed))
                {
                    ordered.Add(trimmed);
                }
            }
        }

        private static bool LooksLikeDeviceId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            bool hasLetter = false;
            bool hasDigit = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetter(c))
                {
                    hasLetter = true;
                }
                else if (char.IsDigit(c))
                {
                    hasDigit = true;
                }
            }

            return hasLetter && hasDigit;
        }

        private static string NormalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            return key.Replace("_", string.Empty).Replace("-", string.Empty).Trim().ToLowerInvariant();
        }

        private static bool IsDevicePropertyName(string normalizedName)
        {
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return false;
            }

            return normalizedName == "device" ||
                   normalizedName == "devices" ||
                   normalizedName == "deviceid" ||
                   normalizedName == "deviceids" ||
                   normalizedName.EndsWith("deviceid", StringComparison.Ordinal);
        }
    }
}
