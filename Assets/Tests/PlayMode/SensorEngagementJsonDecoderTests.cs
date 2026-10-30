using AmpPortableDataViz.Core;
using AmpPortableDataViz.Infra;
using NUnit.Framework;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public class SensorEngagementJsonDecoderTests
    {
        [Test]
        public void TryDecode_ExactPerSensorPayload_UsesBooleanConfirmationAndUnixTimestamp()
        {
            var decoder = new SensorEngagementJsonDecoder(SensorEngagementJsonFormat.CreateDefault());

            bool parsed = decoder.TryDecode(
                "device:MD-V5-0000334:engagement",
                "MD-V5-0000334",
                "{\"device\":\"MD-V5-0000334\",\"engagement\":0.72,\"confirmed\":true," +
                "\"confidence\":0.61,\"source\":\"face\",\"timestamp\":1756113600.0}",
                out SensorEngagementObservation[] observations);

            Assert.IsTrue(parsed);
            Assert.AreEqual(1, observations.Length);
            Assert.AreEqual("MD-V5-0000334", observations[0].SensorId);
            Assert.AreEqual(0.72f, observations[0].Engagement, 0.0001f);
            Assert.IsTrue(observations[0].IsConfirmed);
            Assert.AreEqual(
                621355968000000000L + 1756113600L * System.TimeSpan.TicksPerSecond,
                observations[0].SourceTimestampTicksUtc);
        }

        [Test]
        public void TryDecode_ExactPerSensorPayload_AcceptsFalseConfirmation()
        {
            var decoder = new SensorEngagementJsonDecoder(SensorEngagementJsonFormat.CreateDefault());

            bool parsed = decoder.TryDecode(
                "device:MD-V5-0000334:engagement",
                "MD-V5-0000334",
                "{\"device\":\"MD-V5-0000334\",\"engagement\":0.21,\"confirmed\":false," +
                "\"confidence\":0.22,\"source\":\"face\",\"timestamp\":1756113601.0}",
                out SensorEngagementObservation[] observations);

            Assert.IsTrue(parsed);
            Assert.AreEqual(1, observations.Length);
            Assert.IsFalse(observations[0].IsConfirmed);
        }

        [Test]
        public void TryDecode_CustomPropertyNames_DoesNotRequireBackendChanges()
        {
            var decoder = new SensorEngagementJsonDecoder(new SensorEngagementJsonFormat
            {
                SensorIdPropertyName = "device",
                EngagementPropertyName = "engagement_value",
                ConfirmationPropertyName = "identity_match",
                TimestampPropertyName = "observed_at"
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
                ConfirmationPropertyName = "certain",
                TimestampPropertyName = "observed_at"
            });

            bool parsed = decoder.TryDecode(
                "individual-engagement",
                string.Empty,
                "{\"readings\":[{\"sensor\":\"a\",\"score\":0.2,\"certain\":true},{\"sensor\":\"b\",\"score\":0.8,\"certain\":false}]}",
                out SensorEngagementObservation[] observations);

            Assert.IsTrue(parsed);
            Assert.AreEqual(2, observations.Length);
            Assert.AreEqual("a", observations[0].SensorId);
            Assert.AreEqual("b", observations[1].SensorId);
        }

        [TestCase("2")]
        [TestCase("null")]
        [TestCase("\"yes\"")]
        public void TryDecode_InvalidConfirmation_RejectsObservation(string flagJson)
        {
            var decoder = new SensorEngagementJsonDecoder(SensorEngagementJsonFormat.CreateDefault());

            bool parsed = decoder.TryDecode(
                "device:sensor-a:engagement",
                "sensor-a",
                $"{{\"device\":\"sensor-a\",\"engagement\":0.72,\"confirmed\":{flagJson}}}",
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
                "{\"device\":\"sensor-b\",\"engagement\":0.72,\"confirmed\":true}",
                out SensorEngagementObservation[] observations);

            Assert.IsFalse(parsed);
            Assert.AreEqual(0, observations.Length);
        }

        [Test]
        public void TryDecode_InvalidUnixTimestamp_RejectsObservation()
        {
            var decoder = new SensorEngagementJsonDecoder(SensorEngagementJsonFormat.CreateDefault());

            bool parsed = decoder.TryDecode(
                "device:sensor-a:engagement",
                "sensor-a",
                "{\"device\":\"sensor-a\",\"engagement\":0.72,\"confirmed\":true,\"timestamp\":\"invalid\"}",
                out SensorEngagementObservation[] observations);

            Assert.IsFalse(parsed);
            Assert.AreEqual(0, observations.Length);
        }
    }
}
