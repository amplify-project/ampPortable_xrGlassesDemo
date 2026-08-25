using System;
using AmpPortableDataViz.Application;
using AmpPortableDataViz.Core;
using NUnit.Framework;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public class SensorEngagementRouterTests
    {
        private static readonly long OneSecond = TimeSpan.TicksPerSecond;

        [Test]
        public void ConfirmedObservation_UpdatesOnlyMatchingSensor()
        {
            var router = new SensorEngagementRouter(TimeSpan.FromSeconds(5));
            SensorEngagementSource first = router.RegisterSensor("sensor-a");
            SensorEngagementSource second = router.RegisterSensor("sensor-b");

            bool accepted = router.Process(
                new SensorEngagementObservation("SENSOR-A", 0.8f, true),
                OneSecond);

            Assert.IsTrue(accepted);
            Assert.AreEqual(SensorEngagementStatus.Confirmed, first.LatestFrame.Payload.Status);
            Assert.AreEqual(0.8f, first.LatestFrame.Payload.Engagement, 0.0001f);
            Assert.AreEqual(SensorEngagementStatus.WaitingForConfirmedScore, second.LatestFrame.Payload.Status);
            Assert.AreEqual(0.5f, second.LatestFrame.Payload.Engagement, 0.0001f);
        }

        [Test]
        public void UnconfirmedObservation_DoesNotReplaceTrustedScoreOrRefreshTimeout()
        {
            var router = new SensorEngagementRouter(TimeSpan.FromSeconds(5));
            SensorEngagementSource source = router.RegisterSensor("sensor-a");
            router.Process(new SensorEngagementObservation("sensor-a", 0.7f, true), OneSecond);

            bool accepted = router.Process(
                new SensorEngagementObservation("sensor-a", 0.1f, false),
                4L * OneSecond);
            router.EvaluateTimeouts(6L * OneSecond);

            Assert.IsFalse(accepted);
            Assert.AreEqual(SensorEngagementStatus.Stale, source.LatestFrame.Payload.Status);
            Assert.AreEqual(0.5f, source.LatestFrame.Payload.Engagement, 0.0001f);
            Assert.AreEqual(0.7f, source.LatestFrame.Payload.LastConfirmedEngagement, 0.0001f);
            Assert.AreEqual(OneSecond, source.LatestFrame.Payload.LastConfirmedReceivedTicksUtc);
        }

        [Test]
        public void Timeout_EmitsStaleTransitionOnlyOnce()
        {
            var router = new SensorEngagementRouter(TimeSpan.FromSeconds(3), 0.4f);
            SensorEngagementSource source = router.RegisterSensor("sensor-a");
            int frameCount = 0;
            source.OnFrame += _ => frameCount++;
            router.Process(new SensorEngagementObservation("sensor-a", 0.9f, true), OneSecond);

            router.EvaluateTimeouts(4L * OneSecond);
            router.EvaluateTimeouts(5L * OneSecond);

            Assert.AreEqual(2, frameCount);
            Assert.AreEqual(SensorEngagementStatus.Stale, source.LatestFrame.Payload.Status);
            Assert.AreEqual(0.4f, source.LatestFrame.Payload.Engagement, 0.0001f);
        }

        [Test]
        public void ConfirmedObservation_AfterTimeoutRecoversStream()
        {
            var router = new SensorEngagementRouter(TimeSpan.FromSeconds(2));
            SensorEngagementSource source = router.RegisterSensor("sensor-a");
            router.Process(new SensorEngagementObservation("sensor-a", 0.9f, true), OneSecond);
            router.EvaluateTimeouts(3L * OneSecond);

            bool accepted = router.Process(
                new SensorEngagementObservation("sensor-a", 0.3f, true),
                4L * OneSecond);

            Assert.IsTrue(accepted);
            Assert.AreEqual(SensorEngagementStatus.Confirmed, source.LatestFrame.Payload.Status);
            Assert.AreEqual(0.3f, source.LatestFrame.Payload.Engagement, 0.0001f);
        }

        [Test]
        public void OlderConfirmedObservation_DoesNotReplaceNewerSourceValue()
        {
            var router = new SensorEngagementRouter(TimeSpan.FromSeconds(5));
            SensorEngagementSource source = router.RegisterSensor("sensor-a");
            router.Process(
                new SensorEngagementObservation("sensor-a", 0.8f, true, 20L * OneSecond),
                OneSecond);

            bool accepted = router.Process(
                new SensorEngagementObservation("sensor-a", 0.2f, true, 10L * OneSecond),
                2L * OneSecond);

            Assert.IsFalse(accepted);
            Assert.AreEqual(0.8f, source.LatestFrame.Payload.Engagement, 0.0001f);
            Assert.AreEqual(OneSecond, source.LatestFrame.Payload.LastConfirmedReceivedTicksUtc);
        }

        [Test]
        public void Observation_ClampsScoreAndUnknownSensorIsIgnored()
        {
            var router = new SensorEngagementRouter(TimeSpan.FromSeconds(5));
            SensorEngagementSource source = router.RegisterSensor("sensor-a");

            bool unknownAccepted = router.Process(
                new SensorEngagementObservation("sensor-b", 0.6f, true),
                OneSecond);
            bool knownAccepted = router.Process(
                new SensorEngagementObservation("sensor-a", 1.4f, true),
                2L * OneSecond);

            Assert.IsFalse(unknownAccepted);
            Assert.IsTrue(knownAccepted);
            Assert.AreEqual(1f, source.LatestFrame.Payload.Engagement, 0.0001f);
        }
    }
}
