using System;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Presentation.Visualization;
using NUnit.Framework;
using UnityEngine;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public sealed class ArtistVizBindingTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                UnityEngine.Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void LiveFrame_IsMappedAndAppliedToArtistVisualizer()
        {
            var source = new FakeAudienceSource("sensor-a");
            CreateBinding(source, out ArtistVizVisualizer visualizer);
            var frame = CreateFrame("sensor-a", 123L, 0.7f);

            source.Emit(frame);

            Assert.IsTrue(visualizer.HasAppliedParameters);
            Assert.AreEqual("sensor-a", visualizer.CurrentParameters.DeviceId);
            Assert.AreEqual(0.7f, visualizer.CurrentParameters.Engagement, 0.0001f);
            Assert.AreEqual(123L, visualizer.LastTimestampTicksUtc);
        }

        [Test]
        public void EnablingBinding_ReplaysLatestSourceFrame()
        {
            var source = new FakeAudienceSource("sensor-b");
            source.Emit(CreateFrame("sensor-b", 456L, 0.35f));

            CreateBinding(source, out ArtistVizVisualizer visualizer);

            Assert.IsTrue(visualizer.HasAppliedParameters);
            Assert.AreEqual("sensor-b", visualizer.CurrentParameters.DeviceId);
            Assert.AreEqual(0.35f, visualizer.CurrentParameters.Engagement, 0.0001f);
            Assert.AreEqual(456L, visualizer.LastTimestampTicksUtc);
        }

        [Test]
        public void CoordinatorSelectionChange_AppliesNewSensorLatestFrame()
        {
            _root = new GameObject(nameof(CoordinatorSelectionChange_AppliesNewSensorLatestFrame));
            _root.SetActive(false);

            var coordinator = _root.AddComponent<SensorStreamCoordinator>();
            var visualizer = _root.AddComponent<ArtistVizVisualizer>();
            var binding = _root.AddComponent<AudienceSignalVisualizerBinding>();
            binding.ConfigureArtistVizTarget(visualizer);

            var sourceA = new FakeAudienceSource("sensor-a");
            var sourceB = new FakeAudienceSource("sensor-b");
            sourceA.Emit(CreateFrame("sensor-a", 100L, 0.2f));
            sourceB.Emit(CreateFrame("sensor-b", 200L, 0.8f));

            coordinator.ConfigureVisualizerBindings(new[] { binding });
            coordinator.ConfigureSources(new IDataSource<AudienceSignalSample>[] { sourceB, sourceA });
            _root.SetActive(true);

            Assert.AreEqual("sensor-a", visualizer.CurrentParameters.DeviceId);
            Assert.IsTrue(coordinator.SelectNextSensor());
            Assert.AreEqual("sensor-b", visualizer.CurrentParameters.DeviceId);
            Assert.AreEqual(0.8f, visualizer.CurrentParameters.Engagement, 0.0001f);
            Assert.AreEqual(200L, visualizer.LastTimestampTicksUtc);
        }

        private void CreateBinding(FakeAudienceSource source, out ArtistVizVisualizer visualizer)
        {
            _root = new GameObject(nameof(ArtistVizBindingTests));
            _root.SetActive(false);

            visualizer = _root.AddComponent<ArtistVizVisualizer>();
            var binding = _root.AddComponent<AudienceSignalVisualizerBinding>();
            binding.ConfigureArtistVizTarget(visualizer);
            binding.ConfigureLiveSource(source);

            _root.SetActive(true);
        }

        private static DataFrame<AudienceSignalSample> CreateFrame(
            string deviceId,
            long timestampTicksUtc,
            float engagement)
        {
            var sample = new AudienceSignalSample(
                deviceId,
                PhysioMetricsEncoding.ZScore,
                -3f,
                -1.5f,
                1.5f,
                3f,
                0f,
                engagement,
                1f,
                1f);
            return new DataFrame<AudienceSignalSample>(timestampTicksUtc, 1, sample);
        }

        private sealed class FakeAudienceSource : ILatestDataSource<AudienceSignalSample>
        {
            public FakeAudienceSource(string sourceId)
            {
                SourceId = sourceId;
            }

            public string SourceId { get; }
            public bool HasLatestFrame { get; private set; }
            public DataFrame<AudienceSignalSample> LatestFrame { get; private set; }
            public event Action<DataFrame<AudienceSignalSample>> OnFrame;

            public void Emit(DataFrame<AudienceSignalSample> frame)
            {
                LatestFrame = frame;
                HasLatestFrame = true;
                OnFrame?.Invoke(frame);
            }
        }
    }
}
