using System;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Presentation.Interaction;
using AmpPortableDataViz.Presentation.Visualization;
using NUnit.Framework;
using UnityEngine;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public sealed class SensorStreamPresentationTests
    {
        private GameObject _root;
        private SensorStreamCoordinator _coordinator;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("SensorStreamPresentationTests");
            _root.SetActive(false);
            _coordinator = _root.AddComponent<SensorStreamCoordinator>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void Coordinator_SelectNextSensor_AdvancesConfiguredSources()
        {
            _coordinator.ConfigureSources(new IDataSource<AudienceSignalSample>[]
            {
                new FakeAudienceSource("sensor-b"),
                new FakeAudienceSource("sensor-a")
            });

            Assert.AreEqual("sensor-a", _coordinator.ActiveSensorId);
            Assert.IsTrue(_coordinator.SelectNextSensor());
            Assert.AreEqual("sensor-b", _coordinator.ActiveSensorId);
        }

        [Test]
        public void TempleCycler_PublicAction_UsesCoordinatorSelection()
        {
            _coordinator.ConfigureSources(new IDataSource<AudienceSignalSample>[]
            {
                new FakeAudienceSource("sensor-a"),
                new FakeAudienceSource("sensor-b")
            });
            var cycler = _root.AddComponent<RayNeoTempleSensorStreamCycler>();
            cycler.Configure(_coordinator);

            cycler.SelectNextSensor();

            Assert.AreEqual("sensor-b", _coordinator.ActiveSensorId);
        }

        [Test]
        public void HudLabel_ShowsActivePositionCountAndId()
        {
            string label = ActiveSensorHudBinding.FormatLabel(1, 2, "sensor-b");

            Assert.AreEqual("SENSOR 2 / 2\nsensor-b", label);
        }

        [Test]
        public void HudLabel_ShowsEmptyStateWithoutSources()
        {
            string label = ActiveSensorHudBinding.FormatLabel(-1, 0, string.Empty);

            Assert.AreEqual("NO SENSOR STREAMS", label);
        }

        private sealed class FakeAudienceSource : IDataSource<AudienceSignalSample>
        {
            public FakeAudienceSource(string sourceId)
            {
                SourceId = sourceId;
            }

            public string SourceId { get; }
            public event Action<DataFrame<AudienceSignalSample>> OnFrame;

            public void Emit(DataFrame<AudienceSignalSample> frame)
            {
                OnFrame?.Invoke(frame);
            }
        }
    }
}
