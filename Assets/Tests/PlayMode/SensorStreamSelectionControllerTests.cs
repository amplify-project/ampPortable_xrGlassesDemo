using AmpPortableDataViz.Application;
using NUnit.Framework;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public sealed class SensorStreamSelectionControllerTests
    {
        [Test]
        public void ReplaceSensors_NormalizesAndSortsSensorIds()
        {
            var controller = new SensorStreamSelectionController();

            controller.ReplaceSensors(new[] { " sensor-b ", "sensor-A", null, "SENSOR-A", "" });

            Assert.AreEqual(2, controller.SensorCount);
            Assert.AreEqual("sensor-A", controller.SensorIds[0]);
            Assert.AreEqual("sensor-b", controller.SensorIds[1]);
            Assert.AreEqual("sensor-A", controller.ActiveSensorId);
            Assert.AreEqual(0, controller.ActiveIndex);
        }

        [Test]
        public void SelectNext_WrapsThroughSensors()
        {
            var controller = new SensorStreamSelectionController();
            controller.ReplaceSensors(new[] { "sensor-a", "sensor-b" });

            Assert.IsTrue(controller.SelectNext());
            Assert.AreEqual("sensor-b", controller.ActiveSensorId);
            Assert.IsTrue(controller.SelectNext());
            Assert.AreEqual("sensor-a", controller.ActiveSensorId);
        }

        [Test]
        public void ReplaceSensors_PreservesActiveSensorWhenCatalogChanges()
        {
            var controller = new SensorStreamSelectionController();
            controller.ReplaceSensors(new[] { "sensor-a", "sensor-b" });
            controller.TrySelect("sensor-b");

            controller.ReplaceSensors(new[] { "sensor-c", "SENSOR-B" });

            Assert.AreEqual("SENSOR-B", controller.ActiveSensorId);
            Assert.AreEqual(0, controller.ActiveIndex);
        }

        [Test]
        public void ReplaceSensors_SelectsFirstSensorWhenActiveSensorDisappears()
        {
            var controller = new SensorStreamSelectionController();
            controller.ReplaceSensors(new[] { "sensor-b", "sensor-c" });
            controller.TrySelect("sensor-c");

            controller.ReplaceSensors(new[] { "sensor-a", "sensor-b" });

            Assert.AreEqual("sensor-a", controller.ActiveSensorId);
        }

        [Test]
        public void EmptyAndSingleSensorCatalogs_DoNotAdvance()
        {
            var controller = new SensorStreamSelectionController();

            Assert.IsFalse(controller.SelectNext());
            Assert.IsFalse(controller.HasActiveSensor);

            controller.ReplaceSensors(new[] { "sensor-a" });

            Assert.IsFalse(controller.SelectNext());
            Assert.AreEqual("sensor-a", controller.ActiveSensorId);
        }

        [Test]
        public void SelectionChanged_ReportsCurrentPositionAndCount()
        {
            var controller = new SensorStreamSelectionController();
            SensorStreamSelectionChanged latestChange = default;
            int eventCount = 0;
            controller.SelectionChanged += change =>
            {
                latestChange = change;
                eventCount++;
            };

            controller.ReplaceSensors(new[] { "sensor-a", "sensor-b" });
            controller.SelectNext();

            Assert.AreEqual(2, eventCount);
            Assert.AreEqual("sensor-a", latestChange.PreviousSensorId);
            Assert.AreEqual("sensor-b", latestChange.ActiveSensorId);
            Assert.AreEqual(1, latestChange.ActiveIndex);
            Assert.AreEqual(2, latestChange.SensorCount);
            Assert.IsTrue(latestChange.HasActiveSensor);
        }
    }
}
