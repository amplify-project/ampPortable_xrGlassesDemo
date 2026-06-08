using AmpPortableDataViz.Core;
using AmpPortableDataViz.Presentation.Mapping;
using AmpPortableDataViz.Presentation.Sources;
using NUnit.Framework;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public class PhysioZScoreTests
    {
        [Test]
        public void TryParsePayload_WhenZScorePayloadPresent_ParsesSignedValuesAndClampsToRange()
        {
            const string payload = "{\"hr_z\":4.2,\"eda_z\":-2.5,\"temperature_roc_z\":0.25,\"ibi_z\":\"-3.4\",\"scr_frequency_z\":1.5}";

            bool parsed = RedisPhysioMetricsPump.TryParsePayload(payload, "device-a", out var sample);

            Assert.IsTrue(parsed);
            Assert.AreEqual("device-a", sample.DeviceId);
            Assert.AreEqual(PhysioMetricsEncoding.ZScore, sample.Encoding);
            Assert.AreEqual(3f, sample.HeartRateStdDev);
            Assert.AreEqual(-2.5f, sample.TonicElectrodermalActivityStdDev);
            Assert.AreEqual(0.25f, sample.TemperatureRateOfChangeStdDev);
            Assert.AreEqual(-3f, sample.InterBeatIntervalStdDev);
            Assert.AreEqual(1.5f, sample.SkinConductanceResponseFrequencyStdDev);
        }

        [Test]
        public void TryParsePayload_WhenLegacyStdDevPayloadPresent_ParsesLegacyEncoding()
        {
            const string payload = "{\"hr_sd\":5,\"edl_sd\":0.2,\"temperature_roc_sd\":0.025,\"ibi_sd\":35,\"scr_frequency_sd\":2}";

            bool parsed = RedisPhysioMetricsPump.TryParsePayload(payload, "device-a", out var sample);

            Assert.IsTrue(parsed);
            Assert.AreEqual(PhysioMetricsEncoding.LegacyStdDev, sample.Encoding);
            Assert.AreEqual(5f, sample.HeartRateStdDev);
            Assert.AreEqual(0.2f, sample.TonicElectrodermalActivityStdDev);
        }

        [Test]
        public void AudienceSignalToParticleMeshMapper_WhenZScorePayload_MapsMinusThreeToMinusOneAndPlusThreeToPlusOne()
        {
            var mapper = new AudienceSignalToParticleMeshMapper(AudienceSignalToParticleMeshMapper.CreateDefaultSettings());
            var sample = new AudienceSignalSample(
                "device-a",
                PhysioMetricsEncoding.ZScore,
                -3f,
                0f,
                3f,
                1.5f,
                -1.5f,
                0.5f,
                1f,
                1f);
            var frame = new DataFrame<AudienceSignalSample>(1L, 0, sample);

            ParticleMeshSignalSample mapped = mapper.Map(in frame);

            Assert.AreEqual(-1f, mapped.TonicElectrodermalActivityStdDev);
            Assert.AreEqual(0f, mapped.TemperatureRateOfChangeStdDev);
            Assert.AreEqual(1f, mapped.SkinConductanceResponseFrequencyStdDev);
            Assert.AreEqual(0.5f, mapped.HeartRateStdDev);
            Assert.AreEqual(-0.5f, mapped.InterBeatIntervalStdDev);
        }

        [Test]
        public void AudienceSignalToParticleMeshMapper_WhenLegacyPayload_UsesPreviousPositiveRanges()
        {
            var mapper = new AudienceSignalToParticleMeshMapper(AudienceSignalToParticleMeshMapper.CreateDefaultSettings());
            var sample = new AudienceSignalSample(
                "device-a",
                PhysioMetricsEncoding.LegacyStdDev,
                0.5f,
                0.1f,
                5f,
                10f,
                100f,
                0.5f,
                1f,
                1f);
            var frame = new DataFrame<AudienceSignalSample>(1L, 0, sample);

            ParticleMeshSignalSample mapped = mapper.Map(in frame);

            Assert.AreEqual(1f, mapped.TonicElectrodermalActivityStdDev);
            Assert.AreEqual(1f, mapped.TemperatureRateOfChangeStdDev);
            Assert.AreEqual(1f, mapped.SkinConductanceResponseFrequencyStdDev);
            Assert.AreEqual(1f, mapped.HeartRateStdDev);
            Assert.AreEqual(1f, mapped.InterBeatIntervalStdDev);
        }
    }
}
