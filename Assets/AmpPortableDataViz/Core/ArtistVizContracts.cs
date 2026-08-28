using System;
using UnityEngine;

namespace AmpPortableDataViz.Core
{
    /// <summary>
    /// Normalized audience signals available to the artist visualization.
    /// Physiological channels are signed -1..1 values; engagement is 0..1.
    /// </summary>
    [Serializable]
    public readonly struct ArtistVizParams
    {
        public readonly string DeviceId;
        public readonly float TonicElectrodermalActivity;
        public readonly float TemperatureRateOfChange;
        public readonly float SkinConductanceResponseFrequency;
        public readonly float HeartRate;
        public readonly float Engagement;

        public ArtistVizParams(
            string deviceId,
            float tonicElectrodermalActivity,
            float temperatureRateOfChange,
            float skinConductanceResponseFrequency,
            float heartRate,
            float engagement)
        {
            DeviceId = deviceId ?? string.Empty;
            TonicElectrodermalActivity = ClampSigned(tonicElectrodermalActivity);
            TemperatureRateOfChange = ClampSigned(temperatureRateOfChange);
            SkinConductanceResponseFrequency = ClampSigned(skinConductanceResponseFrequency);
            HeartRate = ClampSigned(heartRate);
            Engagement = Sanitize01(engagement);
        }

        private static float ClampSigned(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return Mathf.Clamp(value, -1f, 1f);
        }

        private static float Sanitize01(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return Mathf.Clamp01(value);
        }
    }
}
