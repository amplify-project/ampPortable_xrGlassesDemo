using AmpPortableDataViz.Infra;
using NUnit.Framework;
using UnityEngine;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public class LiveKitTelemetryServiceTests
    {
        private static LiveKitTelemetryService CreateService()
        {
            return new LiveKitTelemetryService(_ => null, "https://example.com", "sandbox", "room", "identity");
        }

        [Test]
        public void TryProcessTelemetryJson_WhenPayloadValid_RaisesEventAndReturnsTrue()
        {
            var service = CreateService();
            var expectedPosition = new Vector3(1f, 2f, 3f);
            var expectedRotation = new Quaternion(0f, 0f, 0f, 1f);
            const long expectedTimestamp = 4242L;
            var json = "{\"position\":[1,2,3],\"rotation\":[0,0,0,1],\"timestamp\":4242}";

            bool eventRaised = false;
            LiveKitTelemetryService.TelemetryFrame capturedFrame = default;
            service.TelemetryReceived += frame =>
            {
                eventRaised = true;
                capturedFrame = frame;
            };

            var handled = service.TryProcessTelemetryJson("sender", json);

            Assert.IsTrue(handled);
            Assert.IsTrue(eventRaised);
            Assert.That(capturedFrame.Position, Is.EqualTo(expectedPosition));
            Assert.That(capturedFrame.Rotation, Is.EqualTo(expectedRotation));
            Assert.That(capturedFrame.Timestamp, Is.EqualTo(expectedTimestamp));
        }

        [Test]
        public void TryProcessTelemetryJson_WhenPayloadInvalid_ReturnsFalseAndDoesNotRaiseEvent()
        {
            var service = CreateService();
            var json = "{\"position\":[1,2,3],\"rotation\":[0,0,0],\"timestamp\":4242}"; // rotation missing 4th component

            bool eventRaised = false;
            service.TelemetryReceived += _ => eventRaised = true;

            var handled = service.TryProcessTelemetryJson("sender", json);

            Assert.IsFalse(handled);
            Assert.IsFalse(eventRaised);
        }

        [Test]
        public void TryProcessTelemetryJson_WhenJsonEmpty_ReturnsFalse()
        {
            var service = CreateService();

            bool eventRaised = false;
            service.TelemetryReceived += _ => eventRaised = true;

            var handled = service.TryProcessTelemetryJson("sender", string.Empty);

            Assert.IsFalse(handled);
            Assert.IsFalse(eventRaised);
        }
    }
}
