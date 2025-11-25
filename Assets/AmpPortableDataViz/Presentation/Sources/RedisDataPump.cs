using System;
using System.Threading.Tasks;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Infra;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Sources
{
    /// <summary>
    /// Bridges Redis queue messages into the visualization pipeline as a float data source.
    /// Optionally drives a renderer emission to mirror legacy behaviour.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RedisDataPump : MonoBehaviour, IDataSource<float>
    {
        [Header("Source Identity")]
        [SerializeField]
        private string sourceId;

        [Header("Connection")]
        [SerializeField] private string redisHost = "192.168.0.6";
        [SerializeField] private int redisPort = 6379;
        [SerializeField] private string redisChannel = RedisEmotionChannels.BroadcastChannel;

        [Header("Emotion Channel Template (optional)")]
        [SerializeField] private bool useEmotionChannelTemplate;
        [SerializeField] private EmotionChannelKind emotionChannelKind = EmotionChannelKind.Broadcast;
        [SerializeField] private string emotionDeviceId = RedisEmotionChannels.DefaultDeviceIds[0];

        [Header("Renderer Feedback (optional)")]
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private string emissionProperty = "_Emission";
        [SerializeField] private Vector2 emissionRange = new Vector2(0.5f, 5f);
        [SerializeField] private bool logValues;

        [Header("Value Mapping")]
        [SerializeField]
        private bool clampToZeroOne = false;

        [SerializeField]
        private bool normalizeEmission = true;

        private MaterialPropertyBlock _propertyBlock;
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
                if (!string.IsNullOrEmpty(sourceId))
                {
                    return sourceId;
                }

                if (this == null)
                {
                    return string.IsNullOrEmpty(_cachedSourceName) ? nameof(RedisDataPump) : _cachedSourceName;
                }

                var resolvedName = TryResolveGameObjectName();
                if (!string.IsNullOrEmpty(resolvedName))
                {
                    _cachedSourceName = resolvedName;
                    return resolvedName;
                }

                return string.IsNullOrEmpty(_cachedSourceName) ? nameof(RedisDataPump) : _cachedSourceName;
            }
        }
        public event Action<DataFrame<float>> OnFrame;

        private void Awake()
        {
            _clock = new UnityClock();
            _cachedSourceName = TryResolveGameObjectName();

            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            _propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            _ = EnsureConnectionAsync();
        }

        private void OnDisable()
        {
            _ = TeardownConnectionAsync();
        }

        private void Update()
        {
            if (!TryGetLatestMessage(out var latestMessage))
            {
                return;
            }

            float rawValue = (float)latestMessage.Value;
            float frameValue = clampToZeroOne ? Mathf.Clamp01(rawValue) : rawValue;

            EmitFrame(frameValue);
            ApplyRendererFeedback(frameValue);

            if (logValues)
            {
                string channelLabel = string.IsNullOrWhiteSpace(latestMessage.Channel) ? _activeChannelName : latestMessage.Channel;
                Debug.Log($"RedisDataPump[{SourceId}] channel={channelLabel} value {rawValue:F4} frameValue {frameValue:F4} seq={latestMessage.Sequence} ts={latestMessage.Timestamp}");
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

        private void EmitFrame(float value)
        {
            var dataFrame = new DataFrame<float>(_clock.UtcNowTicks, _sequenceId++, value);
            OnFrame?.Invoke(dataFrame);
        }

        private void ApplyRendererFeedback(float value)
        {
            if (targetRenderer == null)
            {
                return;
            }

            float emissionValue = normalizeEmission
                ? Mathf.Lerp(emissionRange.x, emissionRange.y, Mathf.Clamp01(value))
                : value;

            targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(emissionProperty, emissionValue);
            targetRenderer.SetPropertyBlock(_propertyBlock);
        }

        public void ConfigureConnection(string host, int port, string channel)
        {
            redisHost = host;
            redisPort = port;
            redisChannel = channel;
            useEmotionChannelTemplate = false;
            emotionDeviceId = string.Empty;
            clampToZeroOne = ShouldClampChannel(channel);

            if (isActiveAndEnabled)
            {
                _ = RestartConnectionAsync();
            }
        }

        private async Task StartConnectionAsync()
        {
            if (_isConnected || _connectionInProgress)
            {
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
                Debug.LogError($"RedisDataPump: Failed to connect to Redis at {redisHost}:{redisPort} ({label}): {ex.Message}");
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
                Debug.LogError($"RedisDataPump: Failed to clean up Redis connection for channel '{label}': {ex.Message}");
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

        public void ConfigureEmotionChannel(string host, int port, EmotionChannelKind channelKind, string? deviceId = null)
        {
            redisHost = host;
            redisPort = port;
            useEmotionChannelTemplate = true;
            emotionChannelKind = channelKind;
            emotionDeviceId = deviceId ?? string.Empty;
            clampToZeroOne = channelKind == EmotionChannelKind.Broadcast;

            if (isActiveAndEnabled)
            {
                _ = RestartConnectionAsync();
            }
        }

        private static bool IsHeartRateChannel(string? channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
            {
                return false;
            }

            return channelName.IndexOf("hr_filtered", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   channelName.IndexOf(":hr", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsValenceOrArousalChannel(string? channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
            {
                return false;
            }

            return channelName.IndexOf("valence", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   channelName.IndexOf("arousal", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ShouldClampChannel(string channelName)
        {
            // Do not clamp valence or arousal; only clamp non-emotion channels by default.
            if (IsHeartRateChannel(channelName))
            {
                return false;
            }

            if (IsValenceOrArousalChannel(channelName))
            {
                return false;
            }

            return true;
        }

        private bool TryResolveConfiguredChannel(out string channelName)
        {
            if (useEmotionChannelTemplate)
            {
                if (RedisEmotionChannels.TryFormatChannel(emotionChannelKind, emotionDeviceId, out channelName))
                {
                    return true;
                }

                Debug.LogWarning($"RedisDataPump[{SourceId}] has an invalid emotion channel configuration.");
                channelName = string.Empty;
                return false;
            }

            channelName = string.IsNullOrWhiteSpace(redisChannel) ? string.Empty : redisChannel.Trim();
            if (string.IsNullOrWhiteSpace(channelName))
            {
                Debug.LogWarning($"RedisDataPump[{SourceId}] has no Redis channel configured.");
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
    }
}
