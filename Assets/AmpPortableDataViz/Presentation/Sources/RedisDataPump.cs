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
        [SerializeField] private string redisChannel = "sensor_data";

        [Header("Renderer Feedback (optional)")]
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private string emissionProperty = "_Emission";
        [SerializeField] private Vector2 emissionRange = new Vector2(0.5f, 5f);
        [SerializeField] private bool logValues;

        [Header("Value Mapping")]
        [SerializeField]
        private bool clampToZeroOne = true;

        [SerializeField]
        private bool normalizeEmission = true;

        private MaterialPropertyBlock _propertyBlock;
        private IClock _clock;
        private int _sequenceId;
        private bool _isConnected;
        private bool _connectionInProgress;

        public string SourceId => string.IsNullOrEmpty(sourceId) ? gameObject.name : sourceId;
        public event Action<DataFrame<float>> OnFrame;

        private void Awake()
        {
            _clock = new UnityClock();

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
                Debug.Log($"RedisDataPump[{SourceId}] value {rawValue:F4} frameValue {frameValue:F4}");
            }
        }

        private bool TryGetLatestMessage(out RedisSubscriber.RedisMessage message)
        {
            bool hasMessage = false;
            RedisSubscriber.RedisMessage latestMessage = default;

            while (RedisSubscriber.TryDequeue(out var dequeuedMessage))
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
            try
            {
                await RedisSubscriber.Begin(redisHost, redisPort, redisChannel);
                _isConnected = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"RedisDataPump: Failed to connect to Redis at {redisHost}:{redisPort} ({redisChannel}): {ex.Message}");
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
            try
            {
                await RedisSubscriber.CleanupAsync();
                _isConnected = false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"RedisDataPump: Failed to clean up Redis connection: {ex.Message}");
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
    }
}
