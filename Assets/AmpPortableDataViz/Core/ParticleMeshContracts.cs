using System;
using UnityEngine;

namespace AmpPortableDataViz.Core
{
    /// <summary>
    /// Combined normalized signal snapshot for the ParticleMesh visualizer.
    /// </summary>
    [Serializable]
    public readonly struct ParticleMeshSignalSample
    {
        public readonly string DeviceId;
        public readonly float TonicElectrodermalActivityStdDev;
        public readonly float TemperatureRateOfChangeStdDev;
        public readonly float SkinConductanceResponseFrequencyStdDev;
        public readonly float HeartRateStdDev;
        public readonly float InterBeatIntervalStdDev;
        public readonly float FacialEmotion;
        public readonly float EngagementArousal;
        public readonly float EngagementValence;

        public ParticleMeshSignalSample(
            string deviceId,
            float tonicElectrodermalActivityStdDev,
            float temperatureRateOfChangeStdDev,
            float skinConductanceResponseFrequencyStdDev,
            float heartRateStdDev,
            float interBeatIntervalStdDev,
            float facialEmotion,
            float engagementArousal,
            float engagementValence)
        {
            DeviceId = deviceId ?? string.Empty;
            TonicElectrodermalActivityStdDev = Mathf.Clamp01(tonicElectrodermalActivityStdDev);
            TemperatureRateOfChangeStdDev = Mathf.Clamp01(temperatureRateOfChangeStdDev);
            SkinConductanceResponseFrequencyStdDev = Mathf.Clamp01(skinConductanceResponseFrequencyStdDev);
            HeartRateStdDev = Mathf.Clamp01(heartRateStdDev);
            InterBeatIntervalStdDev = Mathf.Clamp01(interBeatIntervalStdDev);
            FacialEmotion = Mathf.Clamp01(facialEmotion);
            EngagementArousal = Mathf.Clamp01(engagementArousal);
            EngagementValence = Mathf.Clamp01(engagementValence);
        }

        public override string ToString()
        {
            return $"Device={DeviceId}, " +
                $"TonicEDA={TonicElectrodermalActivityStdDev:F3}, " +
                $"TempRate={TemperatureRateOfChangeStdDev:F3}, " +
                $"SCRFreq={SkinConductanceResponseFrequencyStdDev:F3}, " +
                $"HeartRate={HeartRateStdDev:F3}, " +
                $"IBI={InterBeatIntervalStdDev:F3}, " +
                $"FacialEmotion={FacialEmotion:F3}, " +
                $"EngagementArousal={EngagementArousal:F3}, " +
                $"EngagementValence={EngagementValence:F3}";
        }
    }
}
