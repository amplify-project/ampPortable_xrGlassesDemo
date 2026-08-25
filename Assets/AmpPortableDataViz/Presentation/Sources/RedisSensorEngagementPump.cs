using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Infra;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Sources
{
    /// <summary>
    /// Subscribes once to each configured Redis channel and emits decoded sensor observations.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RedisSensorEngagementPump : MonoBehaviour, IDataSource<SensorEngagementObservation>
    {
        [Header("Diagnostics")]
        [SerializeField] private bool logValues;
        [SerializeField] private bool logDecodeFailures = true;

        private readonly Dictionary<string, string> _expectedSensorIdByChannel =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _activeChannels = new HashSet<string>(StringComparer.Ordinal);

        private string _redisHost = string.Empty;
        private int _redisPort = 6379;
        private ISensorEngagementPayloadDecoder _decoder;
        private IClock _clock;
        private int _sequenceId;
        private bool _connectionInProgress;

        public string SourceId => nameof(RedisSensorEngagementPump);

        public event Action<DataFrame<SensorEngagementObservation>> OnFrame;

        private void Awake()
        {
            _clock = new UnityClock();
        }

        private void OnEnable()
        {
            _ = EnsureConnectionAsync();
        }

        private void Update()
        {
            if (_decoder == null || _activeChannels.Count == 0)
            {
                return;
            }

            foreach (string channelName in _activeChannels)
            {
                while (RedisSubscriber.TryDequeue(channelName, out RedisSubscriber.RedisMessage message))
                {
                    ProcessMessage(channelName, message.RawPayload);
                }
            }
        }

        private void OnDisable()
        {
            _ = TeardownConnectionAsync();
        }

        public void ConfigureConnection(
            string host,
            int port,
            IEnumerable<SensorEngagementChannelRoute> routes,
            ISensorEngagementPayloadDecoder decoder)
        {
            bool wasEnabled = isActiveAndEnabled;
            _redisHost = string.IsNullOrWhiteSpace(host) ? string.Empty : host.Trim();
            _redisPort = port;
            _decoder = decoder;
            BuildChannelMap(routes);

            if (wasEnabled)
            {
                _ = RestartConnectionAsync();
            }
        }

        internal void ProcessMessage(string channelName, string rawPayload)
        {
            if (_decoder == null ||
                !_expectedSensorIdByChannel.TryGetValue(channelName, out string expectedSensorId) ||
                !_decoder.TryDecode(channelName, expectedSensorId, rawPayload, out SensorEngagementObservation[] observations))
            {
                if (logDecodeFailures)
                {
                    Debug.LogWarning($"RedisSensorEngagementPump could not decode payload from '{channelName}'.", this);
                }

                return;
            }

            long receivedTicksUtc = _clock.UtcNowTicks;
            for (int i = 0; i < observations.Length; i++)
            {
                var frame = new DataFrame<SensorEngagementObservation>(
                    receivedTicksUtc,
                    _sequenceId++,
                    observations[i]);
                OnFrame?.Invoke(frame);

                if (logValues)
                {
                    Debug.Log(
                        $"RedisSensorEngagementPump[{observations[i].SensorId}] " +
                        $"engagement={observations[i].Engagement:F4} confirmed={observations[i].IsConfirmed} channel={channelName}",
                        this);
                }
            }
        }

        private void BuildChannelMap(IEnumerable<SensorEngagementChannelRoute> routes)
        {
            _expectedSensorIdByChannel.Clear();
            if (routes == null)
            {
                return;
            }

            foreach (SensorEngagementChannelRoute route in routes)
            {
                if (string.IsNullOrWhiteSpace(route.ChannelName))
                {
                    continue;
                }

                string channelName = route.ChannelName.Trim();
                string sensorId = string.IsNullOrWhiteSpace(route.SensorId) ? string.Empty : route.SensorId.Trim();
                if (_expectedSensorIdByChannel.TryGetValue(channelName, out string existingSensorId) &&
                    !string.Equals(existingSensorId, sensorId, StringComparison.OrdinalIgnoreCase))
                {
                    _expectedSensorIdByChannel[channelName] = string.Empty;
                }
                else
                {
                    _expectedSensorIdByChannel[channelName] = sensorId;
                }
            }
        }

        private async Task StartConnectionAsync()
        {
            if (_connectionInProgress || _activeChannels.Count > 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_redisHost) || _decoder == null || _expectedSensorIdByChannel.Count == 0)
            {
                return;
            }

            _connectionInProgress = true;
            try
            {
                foreach (string channelName in _expectedSensorIdByChannel.Keys)
                {
                    await RedisSubscriber.RegisterChannelAsync(_redisHost, _redisPort, channelName);
                    _activeChannels.Add(channelName);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"RedisSensorEngagementPump failed to subscribe at {_redisHost}:{_redisPort}: {ex.Message}", this);
            }
            finally
            {
                _connectionInProgress = false;
            }
        }

        private async Task StopConnectionAsync()
        {
            if (_connectionInProgress || _activeChannels.Count == 0)
            {
                return;
            }

            _connectionInProgress = true;
            try
            {
                string[] channels = _activeChannels.ToArray();
                for (int i = 0; i < channels.Length; i++)
                {
                    await RedisSubscriber.UnregisterChannelAsync(channels[i]);
                }

                _activeChannels.Clear();
            }
            catch (Exception ex)
            {
                Debug.LogError($"RedisSensorEngagementPump failed to unsubscribe: {ex.Message}", this);
            }
            finally
            {
                _connectionInProgress = false;
            }
        }

        private async Task RestartConnectionAsync()
        {
            await WaitForIdleAsync();
            await StopConnectionAsync();
            await WaitForIdleAsync();
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
