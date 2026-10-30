using AmpPortableDataViz.Core;
using AmpPortableDataViz.Presentation.Mapping;
using NUnit.Framework;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public sealed class ArtistVizMapperTests
    {
        [Test]
        public void Map_ZScores_ProducesSignedParametersAndPreservesEngagement()
        {
            var mapper = new AudienceSignalToArtistVizMapper(
                AudienceSignalToArtistVizMapper.CreateDefaultSettings());
            var sample = new AudienceSignalSample(
                "sensor-a",
                PhysioMetricsEncoding.ZScore,
                -3f,
                -1.5f,
                1.5f,
                3f,
                0f,
                0.65f,
                1f,
                1f);
            var frame = new DataFrame<AudienceSignalSample>(123L, 7, sample);

            ArtistVizParams parameters = mapper.Map(in frame);

            Assert.AreEqual("sensor-a", parameters.DeviceId);
            Assert.AreEqual(-1f, parameters.TonicElectrodermalActivity, 0.0001f);
            Assert.AreEqual(-0.5f, parameters.TemperatureRateOfChange, 0.0001f);
            Assert.AreEqual(0.5f, parameters.SkinConductanceResponseFrequency, 0.0001f);
            Assert.AreEqual(1f, parameters.HeartRate, 0.0001f);
            Assert.AreEqual(0.65f, parameters.Engagement, 0.0001f);
        }

        [Test]
        public void Map_LegacyValues_NormalizesEachConfiguredRangeToSignedValues()
        {
            var mapper = new AudienceSignalToArtistVizMapper(
                AudienceSignalToArtistVizMapper.CreateDefaultSettings());
            var sample = new AudienceSignalSample(
                "sensor-b",
                PhysioMetricsEncoding.LegacyStdDev,
                0.01f,
                0.0505f,
                5f,
                5.5f,
                0f,
                1f,
                1f,
                1f);
            var frame = new DataFrame<AudienceSignalSample>(456L, 8, sample);

            ArtistVizParams parameters = mapper.Map(in frame);

            Assert.AreEqual(-1f, parameters.TonicElectrodermalActivity, 0.0001f);
            Assert.AreEqual(0f, parameters.TemperatureRateOfChange, 0.0001f);
            Assert.AreEqual(1f, parameters.SkinConductanceResponseFrequency, 0.0001f);
            Assert.AreEqual(0f, parameters.HeartRate, 0.0001f);
        }
    }
}
