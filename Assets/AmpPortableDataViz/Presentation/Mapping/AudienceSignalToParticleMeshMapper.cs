using AmpPortableDataViz.Core;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Mapping
{
    public sealed class AudienceSignalToParticleMeshMapper : IMapper<AudienceSignalSample, ParticleMeshSignalSample>
    {
        [System.Serializable]
        public struct Settings
        {
            public Vector2 TonicElectrodermalActivityRange;
            public Vector2 TemperatureRateOfChangeRange;
            public Vector2 SkinConductanceResponseFrequencyRange;
            public Vector2 HeartRateRange;
            public Vector2 InterBeatIntervalRange;
            public Vector2 EmotionRange;
        }

        public Settings CurrentSettings;

        public AudienceSignalToParticleMeshMapper(Settings settings)
        {
            CurrentSettings = settings;
        }

        public ParticleMeshSignalSample Map(in DataFrame<AudienceSignalSample> inputFrame)
        {
            var payload = inputFrame.Payload;
            var settings = ResolveSettings(CurrentSettings);

            return new ParticleMeshSignalSample(
                payload.DeviceId,
                Normalize(payload.TonicElectrodermalActivityStdDev, settings.TonicElectrodermalActivityRange),
                Normalize(payload.TemperatureRateOfChangeStdDev, settings.TemperatureRateOfChangeRange),
                Normalize(payload.SkinConductanceResponseFrequencyStdDev, settings.SkinConductanceResponseFrequencyRange),
                Normalize(payload.HeartRateStdDev, settings.HeartRateRange),
                Normalize(payload.InterBeatIntervalStdDev, settings.InterBeatIntervalRange),
                Normalize(payload.Arousal, settings.EmotionRange),
                Normalize(payload.Valence, settings.EmotionRange),
                payload.Engagement);
        }

        public static Settings CreateDefaultSettings()
        {
            return new Settings
            {
                TonicElectrodermalActivityRange = new Vector2(0f, 3f),
                TemperatureRateOfChangeRange = new Vector2(0f, 3f),
                SkinConductanceResponseFrequencyRange = new Vector2(0f, 3f),
                HeartRateRange = new Vector2(0f, 3f),
                InterBeatIntervalRange = new Vector2(0f, 3f),
                EmotionRange = new Vector2(0f, 2f)
            };
        }

        private static Settings ResolveSettings(Settings settings)
        {
            NormalizeRange(ref settings.TonicElectrodermalActivityRange);
            NormalizeRange(ref settings.TemperatureRateOfChangeRange);
            NormalizeRange(ref settings.SkinConductanceResponseFrequencyRange);
            NormalizeRange(ref settings.HeartRateRange);
            NormalizeRange(ref settings.InterBeatIntervalRange);
            NormalizeRange(ref settings.EmotionRange);
            return settings;
        }

        private static float Normalize(float value, Vector2 range)
        {
            return Mathf.Clamp01(Mathf.InverseLerp(range.x, range.y, value));
        }

        private static void NormalizeRange(ref Vector2 range)
        {
            if (range == Vector2.zero)
            {
                range = new Vector2(0f, 1f);
                return;
            }

            if (range.y < range.x)
            {
                float previousX = range.x;
                range.x = range.y;
                range.y = previousX;
            }

            if (Mathf.Abs(range.y - range.x) < 1e-5f)
            {
                range.y = range.x + 1e-4f;
            }
        }
    }
}
