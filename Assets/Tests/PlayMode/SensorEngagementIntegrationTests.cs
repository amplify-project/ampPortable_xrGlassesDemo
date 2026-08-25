using System;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Presentation.Sources;
using AmpPortableDataViz.Presentation.Visualization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public class SensorEngagementIntegrationTests
    {
        [Test]
        public void Coordinator_RoutesOnlyConfirmedObservationToRegisteredSensor()
        {
            var observationSource = new FakeSource<SensorEngagementObservation>("observations");
            var gameObject = new GameObject("sensor-engagement-coordinator-test");
            gameObject.SetActive(false);
            var coordinator = gameObject.AddComponent<SensorEngagementCoordinator>();

            try
            {
                coordinator.Configure(observationSource, new[] { "sensor-a" }, 10f, 0.5f);
                gameObject.SetActive(true);
                ILatestDataSource<SensorEngagementState> source = coordinator.GetSource("sensor-a");

                observationSource.Publish(
                    new SensorEngagementObservation("sensor-a", 0.2f, false),
                    DateTime.UtcNow.Ticks);
                Assert.AreEqual(SensorEngagementStatus.WaitingForConfirmedScore, source.LatestFrame.Payload.Status);

                observationSource.Publish(
                    new SensorEngagementObservation("sensor-a", 0.8f, true),
                    DateTime.UtcNow.Ticks);
                Assert.AreEqual(SensorEngagementStatus.Confirmed, source.LatestFrame.Payload.Status);
                Assert.AreEqual(0.8f, source.LatestFrame.Payload.Engagement, 0.0001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AudienceBinding_UsesSensorSpecificStateAndNeutralStartupValue()
        {
            var physioSource = new FakeSource<PhysioMetricsSample>("sensor-a");
            var engagementSource = new FakeLatestSource<SensorEngagementState>(
                "sensor-a",
                new SensorEngagementState(
                    "sensor-a",
                    0.5f,
                    0.5f,
                    0L,
                    SensorEngagementStatus.WaitingForConfirmedScore));
            var gameObject = new GameObject("audience-binding-sensor-engagement-test");
            gameObject.SetActive(false);
            var binding = gameObject.AddComponent<AudienceSignalBinding>();

            try
            {
                binding.ConfigureSensorSources(physioSource, engagementSource, "sensor-a");
                LogAssert.Expect(
                    LogType.Warning,
                    "AudienceSignalBinding[sensor-a] has no particle mesh or graph targets configured.");
                gameObject.SetActive(true);

                physioSource.Publish(
                    new PhysioMetricsSample("sensor-a", 0.1f, 0.2f, 0.3f, 0.4f, 0.5f),
                    DateTime.UtcNow.Ticks);

                Assert.IsTrue(binding.HasLatestFrame);
                Assert.AreEqual(0.5f, binding.LatestFrame.Payload.Engagement, 0.0001f);

                engagementSource.Publish(
                    new SensorEngagementState(
                        "sensor-a",
                        0.85f,
                        0.85f,
                        DateTime.UtcNow.Ticks,
                        SensorEngagementStatus.Confirmed),
                    DateTime.UtcNow.Ticks);

                Assert.AreEqual("sensor-a", binding.LatestFrame.Payload.DeviceId);
                Assert.AreEqual(0.85f, binding.LatestFrame.Payload.Engagement, 0.0001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AudienceBinding_IgnoresStateForDifferentSensor()
        {
            var physioSource = new FakeSource<PhysioMetricsSample>("sensor-a");
            var engagementSource = new FakeLatestSource<SensorEngagementState>(
                "sensor-a",
                new SensorEngagementState(
                    "sensor-a",
                    0.5f,
                    0.5f,
                    0L,
                    SensorEngagementStatus.WaitingForConfirmedScore));
            var gameObject = new GameObject("audience-binding-mismatch-test");
            gameObject.SetActive(false);
            var binding = gameObject.AddComponent<AudienceSignalBinding>();

            try
            {
                binding.ConfigureSensorSources(physioSource, engagementSource, "sensor-a");
                LogAssert.Expect(
                    LogType.Warning,
                    "AudienceSignalBinding[sensor-a] has no particle mesh or graph targets configured.");
                gameObject.SetActive(true);
                physioSource.Publish(
                    new PhysioMetricsSample("sensor-a", 0.1f, 0.2f, 0.3f, 0.4f, 0.5f),
                    DateTime.UtcNow.Ticks);

                engagementSource.Publish(
                    new SensorEngagementState(
                        "sensor-b",
                        0.9f,
                        0.9f,
                        DateTime.UtcNow.Ticks,
                        SensorEngagementStatus.Confirmed),
                    DateTime.UtcNow.Ticks);

                Assert.AreEqual(0.5f, binding.LatestFrame.Payload.Engagement, 0.0001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private class FakeSource<TPayload> : IDataSource<TPayload>
        {
            private int _sequenceId;

            public FakeSource(string sourceId)
            {
                SourceId = sourceId;
            }

            public string SourceId { get; }
            public event Action<DataFrame<TPayload>> OnFrame;

            public void Publish(TPayload payload, long timestampTicksUtc)
            {
                OnFrame?.Invoke(new DataFrame<TPayload>(timestampTicksUtc, _sequenceId++, payload));
            }
        }

        private sealed class FakeLatestSource<TPayload> : FakeSource<TPayload>, ILatestDataSource<TPayload>
        {
            public FakeLatestSource(string sourceId, TPayload initialPayload)
                : base(sourceId)
            {
                LatestFrame = new DataFrame<TPayload>(DateTime.UtcNow.Ticks, 0, initialPayload);
                HasLatestFrame = true;
            }

            public bool HasLatestFrame { get; private set; }
            public DataFrame<TPayload> LatestFrame { get; private set; }

            public new void Publish(TPayload payload, long timestampTicksUtc)
            {
                LatestFrame = new DataFrame<TPayload>(timestampTicksUtc, LatestFrame.SequenceId + 1, payload);
                HasLatestFrame = true;
                base.Publish(payload, timestampTicksUtc);
            }
        }
    }
}
