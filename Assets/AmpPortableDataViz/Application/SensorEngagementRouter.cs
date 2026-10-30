using System;
using System.Collections.Generic;
using AmpPortableDataViz.Core;
using UnityEngine;

namespace AmpPortableDataViz.Application
{
    /// <summary>
    /// Filters unconfirmed observations and exposes independent trusted state per sensor.
    /// </summary>
    public sealed class SensorEngagementRouter
    {
        private static readonly StringComparer SensorIdComparer = StringComparer.OrdinalIgnoreCase;

        private readonly Dictionary<string, SensorEngagementSource> _sourcesBySensorId =
            new Dictionary<string, SensorEngagementSource>(SensorIdComparer);
        private readonly long _timeoutTicks;
        private readonly float _neutralEngagement;

        public SensorEngagementRouter(TimeSpan timeout, float neutralEngagement = 0.5f)
        {
            _timeoutTicks = Math.Max(0L, timeout.Ticks);
            _neutralEngagement = Mathf.Clamp01(neutralEngagement);
        }

        public SensorEngagementSource RegisterSensor(string sensorId, long timestampTicksUtc = 0)
        {
            string normalizedSensorId = NormalizeSensorId(sensorId);
            if (string.IsNullOrEmpty(normalizedSensorId))
            {
                throw new ArgumentException("Sensor ID must be provided.", nameof(sensorId));
            }

            if (_sourcesBySensorId.TryGetValue(normalizedSensorId, out SensorEngagementSource existing))
            {
                return existing;
            }

            var source = new SensorEngagementSource(
                normalizedSensorId,
                _neutralEngagement,
                Math.Max(0L, timestampTicksUtc));
            _sourcesBySensorId.Add(normalizedSensorId, source);
            return source;
        }

        public bool TryGetSource(string sensorId, out SensorEngagementSource source)
        {
            return _sourcesBySensorId.TryGetValue(NormalizeSensorId(sensorId), out source);
        }

        public bool Process(in SensorEngagementObservation observation, long receivedTicksUtc)
        {
            return Process(observation, receivedTicksUtc, receivedTicksUtc);
        }

        public bool Process(
            in SensorEngagementObservation observation,
            long receivedTicksUtc,
            long timeoutClockTicks)
        {
            if (!observation.IsConfirmed ||
                !_sourcesBySensorId.TryGetValue(observation.SensorId, out SensorEngagementSource source))
            {
                return false;
            }

            return source.AcceptConfirmed(
                observation,
                Math.Max(0L, receivedTicksUtc),
                Math.Max(0L, timeoutClockTicks));
        }

        public void EvaluateTimeouts(long nowTicksUtc)
        {
            EvaluateTimeouts(nowTicksUtc, nowTicksUtc);
        }

        public void EvaluateTimeouts(long nowTicksUtc, long timeoutClockTicks)
        {
            long normalizedNow = Math.Max(0L, nowTicksUtc);
            long normalizedTimeoutClock = Math.Max(0L, timeoutClockTicks);
            foreach (SensorEngagementSource source in _sourcesBySensorId.Values)
            {
                source.EvaluateTimeout(
                    normalizedNow,
                    normalizedTimeoutClock,
                    _timeoutTicks,
                    _neutralEngagement);
            }
        }

        private static string NormalizeSensorId(string sensorId)
        {
            return string.IsNullOrWhiteSpace(sensorId) ? string.Empty : sensorId.Trim();
        }
    }

    public sealed class SensorEngagementSource : ILatestDataSource<SensorEngagementState>
    {
        private int _sequenceId;
        private long _lastSourceTimestampTicksUtc;
        private long _lastConfirmedTimeoutClockTicks;

        internal SensorEngagementSource(string sensorId, float neutralEngagement, long timestampTicksUtc)
        {
            SourceId = sensorId;
            LatestFrame = new DataFrame<SensorEngagementState>(
                timestampTicksUtc,
                _sequenceId++,
                new SensorEngagementState(
                    sensorId,
                    neutralEngagement,
                    neutralEngagement,
                    0L,
                    SensorEngagementStatus.WaitingForConfirmedScore));
            HasLatestFrame = true;
        }

        public string SourceId { get; }
        public bool HasLatestFrame { get; private set; }
        public DataFrame<SensorEngagementState> LatestFrame { get; private set; }

        public event Action<DataFrame<SensorEngagementState>> OnFrame;

        internal bool AcceptConfirmed(
            in SensorEngagementObservation observation,
            long receivedTicksUtc,
            long timeoutClockTicks)
        {
            if (observation.SourceTimestampTicksUtc > 0L &&
                _lastSourceTimestampTicksUtc > 0L &&
                observation.SourceTimestampTicksUtc <= _lastSourceTimestampTicksUtc)
            {
                return false;
            }

            if (observation.SourceTimestampTicksUtc > 0L)
            {
                _lastSourceTimestampTicksUtc = observation.SourceTimestampTicksUtc;
            }

            _lastConfirmedTimeoutClockTicks = timeoutClockTicks;

            Publish(
                receivedTicksUtc,
                new SensorEngagementState(
                    SourceId,
                    observation.Engagement,
                    observation.Engagement,
                    receivedTicksUtc,
                    SensorEngagementStatus.Confirmed));
            return true;
        }

        internal void EvaluateTimeout(
            long nowTicksUtc,
            long timeoutClockTicks,
            long timeoutTicks,
            float neutralEngagement)
        {
            SensorEngagementState current = LatestFrame.Payload;
            if (current.Status != SensorEngagementStatus.Confirmed || timeoutTicks <= 0L)
            {
                return;
            }

            long elapsedTicks = Math.Max(0L, timeoutClockTicks - _lastConfirmedTimeoutClockTicks);
            if (elapsedTicks < timeoutTicks)
            {
                return;
            }

            Publish(
                nowTicksUtc,
                new SensorEngagementState(
                    SourceId,
                    neutralEngagement,
                    current.LastConfirmedEngagement,
                    current.LastConfirmedReceivedTicksUtc,
                    SensorEngagementStatus.Stale));
        }

        private void Publish(long timestampTicksUtc, SensorEngagementState state)
        {
            LatestFrame = new DataFrame<SensorEngagementState>(timestampTicksUtc, _sequenceId++, state);
            HasLatestFrame = true;
            OnFrame?.Invoke(LatestFrame);
        }
    }
}
