using NUnit.Framework;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public class RedisSubscriberEngagementTests
    {
        [Test]
        public void FormatSensorEngagementChannel_UsesExactSerialChannelFormat()
        {
            Assert.AreEqual(
                "device:MD-V5-0000334:engagement",
                RedisAudienceChannels.FormatSensorEngagementChannel("MD-V5-0000334"));
        }

        [Test]
        public void TryParsePayload_WhenNestedEngagementScoresPresent_AveragesDeviceScores()
        {
            const string payload = "{\"timestamp\":1710000000,\"scores\":{\"MD-A\":0.25,\"MD-B\":0.75}}";

            bool parsed = RedisSubscriber.TryParsePayload(RedisAudienceChannels.EngagementScoresChannel, payload, out var message);

            Assert.IsTrue(parsed);
            Assert.AreEqual(0.5d, message.Value, 0.0001d);
        }

        [Test]
        public void TryParsePayload_WhenEngagementScoresArrayPresent_AveragesNamedScores()
        {
            const string payload = "{\"scores\":[{\"device_id\":\"MD-A\",\"score\":0.2},{\"device_id\":\"MD-B\",\"score\":\"0.6\"}]}";

            bool parsed = RedisSubscriber.TryParsePayload(RedisAudienceChannels.EngagementScoresChannel, payload, out var message);

            Assert.IsTrue(parsed);
            Assert.AreEqual(0.4d, message.Value, 0.0001d);
        }

        [Test]
        public void TryParsePayload_WhenLegacyEngagementScoreChannelPresent_UsesEngagementParsing()
        {
            const string payload = "{\"timestamp\":1710000000,\"scores\":{\"MD-A\":0.25,\"MD-B\":0.75}}";

            bool parsed = RedisSubscriber.TryParsePayload(RedisAudienceChannels.LegacyEngagementScoreChannel, payload, out var message);

            Assert.IsTrue(parsed);
            Assert.AreEqual(0.5d, message.Value, 0.0001d);
        }

        [Test]
        public void TryParsePayload_WhenNonEngagementObjectHasNoValue_PreservesTopLevelNumericFallback()
        {
            const string payload = "{\"timestamp\":1710000000,\"scores\":{\"MD-A\":0.25,\"MD-B\":0.75}}";

            bool parsed = RedisSubscriber.TryParsePayload("some:other:channel", payload, out var message);

            Assert.IsTrue(parsed);
            Assert.AreEqual(1710000000d, message.Value, 0.0001d);
        }
    }
}
