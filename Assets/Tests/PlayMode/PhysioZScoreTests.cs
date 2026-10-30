using AmpPortableDataViz.Core;
using AmpPortableDataViz.Presentation.Mapping;
using AmpPortableDataViz.Presentation.Sources;
using AmpPortableDataViz.Presentation.Visualization;
using NUnit.Framework;
using UnityEngine;

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
        public void TryParsePayload_WhenLivePayloadOmitsTonicEda_ParsesRemainingZScoreMetrics()
        {
            const string payload =
                "{\"device\":\"MD-V5-0000448\",\"timestamp\":\"2026-06-08T15:41:39.505030\",\"temperature_roc_sd\":1.9282060223988804,\"scr_frequency_sd\":4.325434877955538,\"hr_sd\":7.433940209175751,\"ibi_sd\":101.03794138837152,\"calibrating\":false,\"calibration_remaining_s\":0.0,\"session_age_s\":89.2,\"baseline_n\":88,\"hr_z\":2.532,\"ibi_z\":-0.178,\"temperature_roc_z\":0.607,\"scr_frequency_z\":1.985,\"hr_event_soft\":true,\"hr_event_hard\":true,\"eda_event_soft\":false,\"eda_event_hard\":false,\"ibi_event_soft\":false,\"ibi_event_hard\":false,\"temperature_roc_event_soft\":false,\"temperature_roc_event_hard\":false,\"scr_frequency_event_soft\":true,\"scr_frequency_event_hard\":false,\"quality\":{\"hr\":\"ok\",\"ibi\":\"ok\",\"eda\":\"low\",\"temperature_roc\":\"ok\",\"scr_frequency\":\"ok\"}}";

            bool parsed = RedisPhysioMetricsPump.TryParsePayload(payload, "MD-V5-0000448", out var sample);

            Assert.IsTrue(parsed);
            Assert.AreEqual("MD-V5-0000448", sample.DeviceId);
            Assert.AreEqual(PhysioMetricsEncoding.ZScore, sample.Encoding);
            Assert.AreEqual(0f, sample.TonicElectrodermalActivityStdDev);
            Assert.AreEqual(0.607f, sample.TemperatureRateOfChangeStdDev);
            Assert.AreEqual(1.985f, sample.SkinConductanceResponseFrequencyStdDev);
            Assert.AreEqual(2.532f, sample.HeartRateStdDev);
            Assert.AreEqual(-0.178f, sample.InterBeatIntervalStdDev);
        }

        [Test]
        public void TryParsePayload_WhenLivePayloadOmitsLowQualityZScoreFields_ParsesAsZScoreWithNeutralDefaults()
        {
            const string payload =
                "{\"device\":\"MD-V5-0001019\",\"timestamp\":\"2026-06-08T15:50:57.953357\",\"scr_frequency_sd\":0.4913605736890171,\"hr_sd\":25.816525251063148,\"ibi_sd\":168.42842151724844,\"calibrating\":false,\"calibration_remaining_s\":0.0,\"session_age_s\":77.9,\"baseline_n\":68,\"hr_z\":0.065,\"ibi_z\":-0.949,\"scr_frequency_z\":-0.003,\"hr_event_soft\":false,\"hr_event_hard\":false,\"eda_event_soft\":false,\"eda_event_hard\":false,\"ibi_event_soft\":false,\"ibi_event_hard\":false,\"temperature_roc_event_soft\":false,\"temperature_roc_event_hard\":false,\"scr_frequency_event_soft\":false,\"scr_frequency_event_hard\":false,\"quality\":{\"hr\":\"ok\",\"ibi\":\"ok\",\"eda\":\"low\",\"temperature_roc\":\"low\",\"scr_frequency\":\"ok\"}}";

            bool parsed = RedisPhysioMetricsPump.TryParsePayload(payload, "MD-V5-0001019", out var sample);

            Assert.IsTrue(parsed);
            Assert.AreEqual("MD-V5-0001019", sample.DeviceId);
            Assert.AreEqual(PhysioMetricsEncoding.ZScore, sample.Encoding);
            Assert.AreEqual(0f, sample.TonicElectrodermalActivityStdDev);
            Assert.AreEqual(0f, sample.TemperatureRateOfChangeStdDev);
            Assert.AreEqual(-0.003f, sample.SkinConductanceResponseFrequencyStdDev);
            Assert.AreEqual(0.065f, sample.HeartRateStdDev);
            Assert.AreEqual(-0.949f, sample.InterBeatIntervalStdDev);
        }

        [Test]
        public void TryParsePayload_WhenOnlyLegacyStdDevPayloadPresent_IgnoresPayload()
        {
            const string payload = "{\"hr_sd\":5,\"edl_sd\":0.2,\"temperature_roc_sd\":0.025,\"ibi_sd\":35,\"scr_frequency_sd\":2}";

            bool parsed = RedisPhysioMetricsPump.TryParsePayload(payload, "device-a", out var sample);

            Assert.IsFalse(parsed);
            Assert.AreEqual(default(PhysioMetricsSample), sample);
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
            Assert.AreEqual(0.5f, mapped.Engagement);
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
            Assert.AreEqual(0.5f, mapped.Engagement);
        }

        [Test]
        public void AudienceSignalToParticleMeshMapper_WhenRemovedSignalsChange_ProducesSameParticleValues()
        {
            var mapper = new AudienceSignalToParticleMeshMapper(AudienceSignalToParticleMeshMapper.CreateDefaultSettings());
            var firstSample = new AudienceSignalSample(
                "device-a",
                PhysioMetricsEncoding.ZScore,
                -1f,
                0.75f,
                1.5f,
                -0.5f,
                -3f,
                0.65f,
                0f,
                0f);
            var secondSample = new AudienceSignalSample(
                "device-a",
                PhysioMetricsEncoding.ZScore,
                -1f,
                0.75f,
                1.5f,
                -0.5f,
                3f,
                0.65f,
                2f,
                2f);
            var firstFrame = new DataFrame<AudienceSignalSample>(1L, 0, firstSample);
            var secondFrame = new DataFrame<AudienceSignalSample>(2L, 1, secondSample);

            ParticleMeshSignalSample first = mapper.Map(in firstFrame);
            ParticleMeshSignalSample second = mapper.Map(in secondFrame);

            Assert.AreEqual(first.TonicElectrodermalActivityStdDev, second.TonicElectrodermalActivityStdDev);
            Assert.AreEqual(first.TemperatureRateOfChangeStdDev, second.TemperatureRateOfChangeStdDev);
            Assert.AreEqual(first.SkinConductanceResponseFrequencyStdDev, second.SkinConductanceResponseFrequencyStdDev);
            Assert.AreEqual(first.HeartRateStdDev, second.HeartRateStdDev);
            Assert.AreEqual(first.Engagement, second.Engagement);
        }

        [TestCase(AudienceMetricKind.InterBeatIntervalStdDev)]
        [TestCase(AudienceMetricKind.Arousal)]
        [TestCase(AudienceMetricKind.Valence)]
        public void AudienceVisualizationMetricPolicy_WhenMetricRemoved_RejectsGraphMetric(AudienceMetricKind metric)
        {
            Assert.IsFalse(AudienceVisualizationMetricPolicy.IsGraphMetricSupported(metric));
        }

        [TestCase(AudienceMetricKind.TonicElectrodermalActivityStdDev)]
        [TestCase(AudienceMetricKind.TemperatureRateOfChangeStdDev)]
        [TestCase(AudienceMetricKind.SkinConductanceResponseFrequencyStdDev)]
        [TestCase(AudienceMetricKind.HeartRateStdDev)]
        [TestCase(AudienceMetricKind.Engagement)]
        public void AudienceVisualizationMetricPolicy_WhenMetricRetained_AcceptsGraphMetric(AudienceMetricKind metric)
        {
            Assert.IsTrue(AudienceVisualizationMetricPolicy.IsGraphMetricSupported(metric));
        }

        [Test]
        public void TemperatureTrailResponseMapper_WhenTemperatureRatePositive_UsesHotUpwardResponse()
        {
            Color hot = Color.red;
            TemperatureTrailResponse response = TemperatureTrailResponseMapper.Resolve(
                0.6f, false, 0f, 0.18f, 0.25f, 0.65f, hot, Color.blue);

            Assert.IsTrue(response.IsActive);
            Assert.AreEqual(1f, response.Direction);
            Assert.AreEqual(hot, response.Color);
            Assert.Greater(response.Strength, 0f);
        }

        [Test]
        public void TemperatureTrailResponseMapper_WhenTemperatureRateNegative_UsesColdDownwardResponseAndFlagsReversal()
        {
            Color cold = Color.blue;
            TemperatureTrailResponse response = TemperatureTrailResponseMapper.Resolve(
                -0.6f, true, 1f, 0.18f, 0.25f, 0.65f, Color.red, cold);

            Assert.IsTrue(response.IsActive);
            Assert.IsTrue(response.DirectionChanged);
            Assert.AreEqual(-1f, response.Direction);
            Assert.AreEqual(cold, response.Color);
        }

        [Test]
        public void TemperatureTrailResponseMapper_UsesDeadbandAndReleaseHysteresis()
        {
            TemperatureTrailResponse inactive = TemperatureTrailResponseMapper.Resolve(
                0.1f, false, 0f, 0.18f, 0.25f, 0.65f, Color.red, Color.blue);
            TemperatureTrailResponse heldActive = TemperatureTrailResponseMapper.Resolve(
                0.13f, true, 1f, 0.18f, 0.25f, 0.65f, Color.red, Color.blue);
            TemperatureTrailResponse released = TemperatureTrailResponseMapper.Resolve(
                0.1f, true, 1f, 0.18f, 0.25f, 0.65f, Color.red, Color.blue);

            Assert.IsFalse(inactive.IsActive);
            Assert.IsTrue(heldActive.IsActive);
            Assert.IsFalse(released.IsActive);
        }

        [Test]
        public void TemperatureTrailResponseMapper_WhenThresholdAtMaximum_StillProducesVisibleStrength()
        {
            TemperatureTrailResponse response = TemperatureTrailResponseMapper.Resolve(
                1f, false, 0f, 1f, 0.25f, 1f, Color.red, Color.blue);

            Assert.IsTrue(response.IsActive);
            Assert.AreEqual(1f, response.Strength);
        }
    }
}
