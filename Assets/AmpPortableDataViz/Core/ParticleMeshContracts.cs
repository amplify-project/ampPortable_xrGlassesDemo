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
        public readonly float FacialEmotionArousal;
        public readonly float FacialEmotionValence;
        public readonly float Engagement;

        public ParticleMeshSignalSample(
            string deviceId,
            float tonicElectrodermalActivityStdDev,
            float temperatureRateOfChangeStdDev,
            float skinConductanceResponseFrequencyStdDev,
            float heartRateStdDev,
            float interBeatIntervalStdDev,
            float facialEmotionArousal,
            float facialEmotionValence,
            float engagement)
        {
            DeviceId = deviceId ?? string.Empty;
            TonicElectrodermalActivityStdDev = Mathf.Clamp01(tonicElectrodermalActivityStdDev);
            TemperatureRateOfChangeStdDev = Mathf.Clamp01(temperatureRateOfChangeStdDev);
            SkinConductanceResponseFrequencyStdDev = Mathf.Clamp01(skinConductanceResponseFrequencyStdDev);
            HeartRateStdDev = Mathf.Clamp01(heartRateStdDev);
            InterBeatIntervalStdDev = Mathf.Clamp01(interBeatIntervalStdDev);
            FacialEmotionArousal = Mathf.Clamp01(facialEmotionArousal);
            FacialEmotionValence = Mathf.Clamp01(facialEmotionValence);
            Engagement = Mathf.Clamp01(engagement);
        }

        public override string ToString()
        {
            return $"Device={DeviceId}, " +
                $"TonicEDA={TonicElectrodermalActivityStdDev:F3}, " +
                $"TempRate={TemperatureRateOfChangeStdDev:F3}, " +
                $"SCRFreq={SkinConductanceResponseFrequencyStdDev:F3}, " +
                $"HeartRate={HeartRateStdDev:F3}, " +
                $"IBI={InterBeatIntervalStdDev:F3}, " +
                $"FacialEmotionArousal={FacialEmotionArousal:F3}, " +
                $"FacialEmotionValence={FacialEmotionValence:F3}, " +
                $"Engagement={Engagement:F3}";
        }
    }
}
