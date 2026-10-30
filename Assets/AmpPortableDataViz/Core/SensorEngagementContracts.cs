using System;
using UnityEngine;

namespace AmpPortableDataViz.Core
{
    public interface ISensorEngagementPayloadDecoder
    {
        bool TryDecode(
            string channelName,
            string expectedSensorId,
            string rawPayload,
            out SensorEngagementObservation[] observations);
    }

    public readonly struct SensorEngagementChannelRoute
    {
        public SensorEngagementChannelRoute(string sensorId, string channelName)
        {
            SensorId = string.IsNullOrWhiteSpace(sensorId) ? string.Empty : sensorId.Trim();
            ChannelName = string.IsNullOrWhiteSpace(channelName) ? string.Empty : channelName.Trim();
        }

        public string SensorId { get; }
        public string ChannelName { get; }
    }

    public enum SensorEngagementStatus
    {
        WaitingForConfirmedScore,
        Confirmed,
        Stale
    }

    /// <summary>
    /// One decoded sensor-specific engagement observation before confidence filtering.
    /// </summary>
    [Serializable]
    public readonly struct SensorEngagementObservation
    {
        public readonly string SensorId;
        public readonly float Engagement;
        public readonly bool IsConfirmed;
        public readonly long SourceTimestampTicksUtc;

        public SensorEngagementObservation(
            string sensorId,
            float engagement,
            bool isConfirmed,
            long sourceTimestampTicksUtc = 0)
        {
            SensorId = string.IsNullOrWhiteSpace(sensorId) ? string.Empty : sensorId.Trim();
            Engagement = Mathf.Clamp01(Sanitize(engagement));
            IsConfirmed = isConfirmed;
            SourceTimestampTicksUtc = Math.Max(0L, sourceTimestampTicksUtc);
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }
    }

    /// <summary>
    /// The trusted engagement state exposed to one sensor visualization.
    /// </summary>
    [Serializable]
    public readonly struct SensorEngagementState
    {
        public readonly string SensorId;
        public readonly float Engagement;
        public readonly float LastConfirmedEngagement;
        public readonly long LastConfirmedReceivedTicksUtc;
        public readonly SensorEngagementStatus Status;

        public SensorEngagementState(
            string sensorId,
            float engagement,
            float lastConfirmedEngagement,
            long lastConfirmedReceivedTicksUtc,
            SensorEngagementStatus status)
        {
            SensorId = string.IsNullOrWhiteSpace(sensorId) ? string.Empty : sensorId.Trim();
            Engagement = Mathf.Clamp01(Sanitize(engagement));
            LastConfirmedEngagement = Mathf.Clamp01(Sanitize(lastConfirmedEngagement));
            LastConfirmedReceivedTicksUtc = Math.Max(0L, lastConfirmedReceivedTicksUtc);
            Status = status;
        }

        public bool IsCurrent => Status == SensorEngagementStatus.Confirmed;

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }
    }
}
