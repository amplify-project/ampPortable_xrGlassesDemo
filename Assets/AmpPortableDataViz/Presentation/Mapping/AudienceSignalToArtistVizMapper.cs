using AmpPortableDataViz.Core;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Mapping
{
    public sealed class AudienceSignalToArtistVizMapper : IMapper<AudienceSignalSample, ArtistVizParams>
    {
        [System.Serializable]
        public struct Settings
        {
            public Vector2 PhysioZScoreRange;
            public Vector2 TonicElectrodermalActivityRange;
            public Vector2 TemperatureRateOfChangeRange;
            public Vector2 SkinConductanceResponseFrequencyRange;
            public Vector2 HeartRateRange;
        }

        public Settings CurrentSettings;

        public AudienceSignalToArtistVizMapper(Settings settings)
        {
            CurrentSettings = settings;
        }

        public ArtistVizParams Map(in DataFrame<AudienceSignalSample> inputFrame)
        {
            AudienceSignalSample payload = inputFrame.Payload;
            Settings settings = ResolveSettings(CurrentSettings);

            return new ArtistVizParams(
                payload.DeviceId,
                MapPhysio(payload.TonicElectrodermalActivityStdDev, payload.PhysioEncoding, settings.TonicElectrodermalActivityRange, settings.PhysioZScoreRange),
                MapPhysio(payload.TemperatureRateOfChangeStdDev, payload.PhysioEncoding, settings.TemperatureRateOfChangeRange, settings.PhysioZScoreRange),
                MapPhysio(payload.SkinConductanceResponseFrequencyStdDev, payload.PhysioEncoding, settings.SkinConductanceResponseFrequencyRange, settings.PhysioZScoreRange),
                MapPhysio(payload.HeartRateStdDev, payload.PhysioEncoding, settings.HeartRateRange, settings.PhysioZScoreRange),
                payload.Engagement);
        }

        public static Settings CreateDefaultSettings()
        {
            return new Settings
            {
                PhysioZScoreRange = new Vector2(-3f, 3f),
                TonicElectrodermalActivityRange = new Vector2(0.01f, 0.5f),
                TemperatureRateOfChangeRange = new Vector2(0.001f, 0.1f),
                SkinConductanceResponseFrequencyRange = new Vector2(0.5f, 5f),
                HeartRateRange = new Vector2(1f, 10f)
            };
        }

        private static Settings ResolveSettings(Settings settings)
        {
            NormalizeRange(ref settings.PhysioZScoreRange);
            if (settings.PhysioZScoreRange == new Vector2(0f, 1f))
            {
                settings.PhysioZScoreRange = new Vector2(-3f, 3f);
            }

            NormalizeRange(ref settings.TonicElectrodermalActivityRange);
            NormalizeRange(ref settings.TemperatureRateOfChangeRange);
            NormalizeRange(ref settings.SkinConductanceResponseFrequencyRange);
            NormalizeRange(ref settings.HeartRateRange);
            return settings;
        }

        private static float MapPhysio(
            float value,
            PhysioMetricsEncoding encoding,
            Vector2 legacyRange,
            Vector2 zScoreRange)
        {
            if (encoding == PhysioMetricsEncoding.LegacyStdDev)
            {
                return Mathf.Lerp(-1f, 1f, Mathf.InverseLerp(legacyRange.x, legacyRange.y, value));
            }

            float midpoint = (zScoreRange.x + zScoreRange.y) * 0.5f;
            float halfRange = Mathf.Max(1e-5f, (zScoreRange.y - zScoreRange.x) * 0.5f);
            return Mathf.Clamp((value - midpoint) / halfRange, -1f, 1f);
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
                (range.x, range.y) = (range.y, range.x);
            }

            if (Mathf.Abs(range.y - range.x) < 1e-5f)
            {
                range.y = range.x + 1e-4f;
            }
        }
    }
}
