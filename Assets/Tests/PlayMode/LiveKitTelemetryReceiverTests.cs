using System.Reflection;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Infra;
using AmpPortableDataViz.Presentation.Sources;
using NUnit.Framework;
using UnityEngine;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public class LiveKitTelemetryReceiverTests
    {
        [Test]
        public void OnTelemetryReceived_RaisesDataFrameWithClockTimestamp()
        {
            var gameObject = new GameObject("Receiver");
            var receiver = gameObject.AddComponent<LiveKitTelemetryReceiver>();
            receiver.enabled = false;

            var stubClock = new StubClock { UtcNowTicks = 987654321L };
            SetPrivateField(receiver, "_clock", stubClock);

            var poseSample = new PoseSample(
                new Vector3(0.1f, 0.2f, 0.3f),
                new Quaternion(0.4f, 0.5f, 0.6f, 0.7f),
                123456789L);

            bool eventRaised = false;
            DataFrame<PoseSample> capturedFrame = default;
            receiver.OnFrame += frame =>
            {
                eventRaised = true;
                capturedFrame = frame;
            };

            InvokePrivateMethod(receiver, "OnTelemetryReceived", poseSample);

            Assert.IsTrue(eventRaised);
            Assert.That(capturedFrame.TimestampTicksUtc, Is.EqualTo(stubClock.UtcNowTicks));
            Assert.That(capturedFrame.SequenceId, Is.Zero);
            Assert.That(capturedFrame.Payload.Position, Is.EqualTo(poseSample.Position));
            Assert.That(capturedFrame.Payload.Rotation, Is.EqualTo(poseSample.Rotation));
            Assert.That(capturedFrame.Payload.TimestampTicksUtc, Is.EqualTo(poseSample.TimestampTicksUtc));

            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void OnTelemetryReceived_IncrementsSequenceId()
        {
            var gameObject = new GameObject("ReceiverSeq");
            var receiver = gameObject.AddComponent<LiveKitTelemetryReceiver>();
            receiver.enabled = false;

            SetPrivateField(receiver, "_clock", new StubClock { UtcNowTicks = 1 });
            SetPrivateField(receiver, "_sequenceId", 5);

            var poseSample = new PoseSample(
                Vector3.one,
                Quaternion.identity,
                10L);

            DataFrame<PoseSample> capturedFrame = default;
            receiver.OnFrame += frame => capturedFrame = frame;

            InvokePrivateMethod(receiver, "OnTelemetryReceived", poseSample);

            Assert.That(capturedFrame.SequenceId, Is.EqualTo(5));
            Assert.That(GetPrivateField<int>(receiver, "_sequenceId"), Is.EqualTo(6));

            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void SourceId_FallsBackToGameObjectName_WhenIdentityMissing()
        {
            var gameObject = new GameObject("ReceiverName");
            var receiver = gameObject.AddComponent<LiveKitTelemetryReceiver>();
            receiver.identity = string.Empty;

            Assert.That(receiver.SourceId, Is.EqualTo(gameObject.name));

            Object.DestroyImmediate(gameObject);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return (T)field.GetValue(target);
        }

        private static void InvokePrivateMethod(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, args);
        }

        private sealed class StubClock : IClock
        {
            public long UtcNowTicks { get; set; }
            public double SecondsSinceStartup { get; set; }
        }
    }
}
