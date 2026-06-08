using System;
using UnityEngine;

namespace AmpPortableDataViz.Core
{
    /// <summary>
    /// Combined normalized signal snapshot for the ParticleMesh visualizer.
    /// Physiological channels are signed -1..1 values; emotion and engagement remain 0..1.
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
            TonicElectrodermalActivityStdDev = ClampSigned(tonicElectrodermalActivityStdDev);
            TemperatureRateOfChangeStdDev = ClampSigned(temperatureRateOfChangeStdDev);
            SkinConductanceResponseFrequencyStdDev = ClampSigned(skinConductanceResponseFrequencyStdDev);
            HeartRateStdDev = ClampSigned(heartRateStdDev);
            InterBeatIntervalStdDev = ClampSigned(interBeatIntervalStdDev);
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

        private static float ClampSigned(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return Mathf.Clamp(value, -1f, 1f);
        }
    }
}
