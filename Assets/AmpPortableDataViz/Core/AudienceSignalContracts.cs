using System;
using UnityEngine;

namespace AmpPortableDataViz.Core
{
    public enum AudienceMetricKind
    {
        TonicElectrodermalActivityStdDev,
        TemperatureRateOfChangeStdDev,
        SkinConductanceResponseFrequencyStdDev,
        HeartRateStdDev,
        InterBeatIntervalStdDev,
        Engagement,
        Arousal,
        Valence
    }

    /// <summary>
    /// Raw physiological metrics parsed from the device physio_metrics Redis channel.
    /// </summary>
    [Serializable]
    public readonly struct PhysioMetricsSample
    {
        public readonly string DeviceId;
        public readonly float TonicElectrodermalActivityStdDev;
        public readonly float TemperatureRateOfChangeStdDev;
        public readonly float SkinConductanceResponseFrequencyStdDev;
        public readonly float HeartRateStdDev;
        public readonly float InterBeatIntervalStdDev;

        public PhysioMetricsSample(
            string deviceId,
            float tonicElectrodermalActivityStdDev,
            float temperatureRateOfChangeStdDev,
            float skinConductanceResponseFrequencyStdDev,
            float heartRateStdDev,
            float interBeatIntervalStdDev)
        {
            DeviceId = deviceId ?? string.Empty;
            TonicElectrodermalActivityStdDev = Sanitize(tonicElectrodermalActivityStdDev);
            TemperatureRateOfChangeStdDev = Sanitize(temperatureRateOfChangeStdDev);
            SkinConductanceResponseFrequencyStdDev = Sanitize(skinConductanceResponseFrequencyStdDev);
            HeartRateStdDev = Sanitize(heartRateStdDev);
            InterBeatIntervalStdDev = Sanitize(interBeatIntervalStdDev);
        }

        public override string ToString()
        {
            return $"Device={DeviceId}, " +
                $"TonicEDA={TonicElectrodermalActivityStdDev:F3}, " +
                $"TempRate={TemperatureRateOfChangeStdDev:F3}, " +
                $"SCRFreq={SkinConductanceResponseFrequencyStdDev:F3}, " +
                $"HeartRate={HeartRateStdDev:F3}, " +
                $"IBI={InterBeatIntervalStdDev:F3}";
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }
    }

    /// <summary>
    /// Aggregated audience signal snapshot combining physiological, engagement, and emotion streams.
    /// </summary>
    [Serializable]
    public readonly struct AudienceSignalSample
    {
        public readonly string DeviceId;
        public readonly float TonicElectrodermalActivityStdDev;
        public readonly float TemperatureRateOfChangeStdDev;
        public readonly float SkinConductanceResponseFrequencyStdDev;
        public readonly float HeartRateStdDev;
        public readonly float InterBeatIntervalStdDev;
        public readonly float Engagement;
        public readonly float Arousal;
        public readonly float Valence;

        public AudienceSignalSample(
            string deviceId,
            float tonicElectrodermalActivityStdDev,
            float temperatureRateOfChangeStdDev,
            float skinConductanceResponseFrequencyStdDev,
            float heartRateStdDev,
            float interBeatIntervalStdDev,
            float engagement,
            float arousal,
            float valence)
        {
            DeviceId = deviceId ?? string.Empty;
            TonicElectrodermalActivityStdDev = Sanitize(tonicElectrodermalActivityStdDev);
            TemperatureRateOfChangeStdDev = Sanitize(temperatureRateOfChangeStdDev);
            SkinConductanceResponseFrequencyStdDev = Sanitize(skinConductanceResponseFrequencyStdDev);
            HeartRateStdDev = Sanitize(heartRateStdDev);
            InterBeatIntervalStdDev = Sanitize(interBeatIntervalStdDev);
            Engagement = Mathf.Clamp01(Sanitize(engagement));
            Arousal = Mathf.Clamp(Sanitize(arousal), 0f, 2f);
            Valence = Mathf.Clamp(Sanitize(valence), 0f, 2f);
        }

        public float GetMetricValue(AudienceMetricKind metricKind)
        {
            return metricKind switch
            {
                AudienceMetricKind.TonicElectrodermalActivityStdDev => TonicElectrodermalActivityStdDev,
                AudienceMetricKind.TemperatureRateOfChangeStdDev => TemperatureRateOfChangeStdDev,
                AudienceMetricKind.SkinConductanceResponseFrequencyStdDev => SkinConductanceResponseFrequencyStdDev,
                AudienceMetricKind.HeartRateStdDev => HeartRateStdDev,
                AudienceMetricKind.InterBeatIntervalStdDev => InterBeatIntervalStdDev,
                AudienceMetricKind.Engagement => Engagement,
                AudienceMetricKind.Arousal => Arousal,
                AudienceMetricKind.Valence => Valence,
                _ => 0f
            };
        }

        public override string ToString()
        {
            return $"Device={DeviceId}, " +
                $"TonicEDA={TonicElectrodermalActivityStdDev:F3}, " +
                $"TempRate={TemperatureRateOfChangeStdDev:F3}, " +
                $"SCRFreq={SkinConductanceResponseFrequencyStdDev:F3}, " +
                $"HeartRate={HeartRateStdDev:F3}, " +
                $"IBI={InterBeatIntervalStdDev:F3}, " +
                $"Engagement={Engagement:F3}, " +
                $"Arousal={Arousal:F3}, " +
                $"Valence={Valence:F3}";
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }
    }
}
