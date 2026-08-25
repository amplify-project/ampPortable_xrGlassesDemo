using AmpPortableDataViz.Core;
using AmpPortableDataViz.Infra;
using NUnit.Framework;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public class SensorEngagementJsonDecoderTests
    {
        [Test]
        public void TryDecode_PerSensorChannel_UsesExpectedSensorId()
        {
            var decoder = new SensorEngagementJsonDecoder(SensorEngagementJsonFormat.CreateDefault());

            bool parsed = decoder.TryDecode(
                "device:sensor-a:engagement",
                "sensor-a",
                "{\"score\":0.72,\"confirmed\":1}",
                out SensorEngagementObservation[] observations);

            Assert.IsTrue(parsed);
            Assert.AreEqual(1, observations.Length);
            Assert.AreEqual("sensor-a", observations[0].SensorId);
            Assert.AreEqual(0.72f, observations[0].Engagement, 0.0001f);
            Assert.IsTrue(observations[0].IsConfirmed);
        }

        [Test]
        public void TryDecode_CustomPropertyNames_DoesNotRequireBackendChanges()
        {
            var decoder = new SensorEngagementJsonDecoder(new SensorEngagementJsonFormat
            {
                SensorIdPropertyName = "device",
                EngagementPropertyName = "engagement_value",
                ConfirmationPropertyName = "identity_match"
            });

            bool parsed = decoder.TryDecode(
                "individual-engagement",
                string.Empty,
                "{\"device\":\"sensor-b\",\"engagement_value\":\"0.31\",\"identity_match\":\"0\"}",
                out SensorEngagementObservation[] observations);

            Assert.IsTrue(parsed);
            Assert.AreEqual("sensor-b", observations[0].SensorId);
            Assert.AreEqual(0.31f, observations[0].Engagement, 0.0001f);
            Assert.IsFalse(observations[0].IsConfirmed);
        }

        [Test]
        public void TryDecode_ArrayContainer_ReturnsAllSensorObservations()
        {
            var decoder = new SensorEngagementJsonDecoder(new SensorEngagementJsonFormat
            {
                ItemsPropertyName = "readings",
                SensorIdPropertyName = "sensor",
                EngagementPropertyName = "score",
                ConfirmationPropertyName = "certain"
            });

            bool parsed = decoder.TryDecode(
                "individual-engagement",
                string.Empty,
                "{\"readings\":[{\"sensor\":\"a\",\"score\":0.2,\"certain\":1},{\"sensor\":\"b\",\"score\":0.8,\"certain\":0}]}",
                out SensorEngagementObservation[] observations);

            Assert.IsTrue(parsed);
            Assert.AreEqual(2, observations.Length);
            Assert.AreEqual("a", observations[0].SensorId);
            Assert.AreEqual("b", observations[1].SensorId);
        }

        [TestCase("2")]
        [TestCase("true")]
        [TestCase("null")]
        public void TryDecode_NonBinaryFlag_RejectsObservation(string flagJson)
        {
            var decoder = new SensorEngagementJsonDecoder(SensorEngagementJsonFormat.CreateDefault());

            bool parsed = decoder.TryDecode(
                "device:sensor-a:engagement",
                "sensor-a",
                $"{{\"score\":0.72,\"confirmed\":{flagJson}}}",
                out SensorEngagementObservation[] observations);

            Assert.IsFalse(parsed);
            Assert.AreEqual(0, observations.Length);
        }

        [Test]
        public void TryDecode_PayloadSensorDoesNotMatchChannel_RejectsObservation()
        {
            var decoder = new SensorEngagementJsonDecoder(SensorEngagementJsonFormat.CreateDefault());

            bool parsed = decoder.TryDecode(
                "device:sensor-a:engagement",
                "sensor-a",
                "{\"sensor_id\":\"sensor-b\",\"score\":0.72,\"confirmed\":1}",
                out SensorEngagementObservation[] observations);

            Assert.IsFalse(parsed);
            Assert.AreEqual(0, observations.Length);
        }
    }
}
