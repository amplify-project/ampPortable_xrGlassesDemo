using System;
using AmpPortableDataViz.Application;
using AmpPortableDataViz.Core;
using NUnit.Framework;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public sealed class SelectedSensorDataSourceTests
    {
        [Test]
        public void ForwardsOnlyFramesFromSelectedSource()
        {
            var selection = new SensorStreamSelectionController();
            var first = new FakeLatestSource("sensor-a");
            var second = new FakeLatestSource("sensor-b");
            using var selectedSource = new SelectedSensorDataSource<int>(selection);
            int received = -1;
            int eventCount = 0;
            selectedSource.OnFrame += frame =>
            {
                received = frame.Payload;
                eventCount++;
            };
            selectedSource.ReplaceSources(new IDataSource<int>[] { first, second });

            second.Emit(20);
            first.Emit(10);

            Assert.AreEqual(1, eventCount);
            Assert.AreEqual(10, received);
        }

        [Test]
        public void ChangingSelection_ReplaysLatestFrameFromNewSource()
        {
            var selection = new SensorStreamSelectionController();
            var first = new FakeLatestSource("sensor-a");
            var second = new FakeLatestSource("sensor-b");
            first.Emit(10);
            second.Emit(20);

            using var selectedSource = new SelectedSensorDataSource<int>(selection);
            int received = -1;
            selectedSource.OnFrame += frame => received = frame.Payload;
            selectedSource.ReplaceSources(new IDataSource<int>[] { first, second });

            Assert.AreEqual(10, received);
            Assert.IsTrue(selection.SelectNext());
            Assert.AreEqual(20, received);
            Assert.AreEqual("sensor-b", selectedSource.SourceId);
        }

        [Test]
        public void ReplaceSources_DetachesRemovedSources()
        {
            var selection = new SensorStreamSelectionController();
            var removed = new FakeLatestSource("sensor-a");
            var retained = new FakeLatestSource("sensor-b");
            using var selectedSource = new SelectedSensorDataSource<int>(selection);
            int eventCount = 0;
            selectedSource.OnFrame += _ => eventCount++;
            selectedSource.ReplaceSources(new IDataSource<int>[] { removed, retained });
            selectedSource.ReplaceSources(new IDataSource<int>[] { retained });

            removed.Emit(10);
            retained.Emit(20);

            Assert.AreEqual(1, eventCount);
            Assert.AreEqual("sensor-b", selection.ActiveSensorId);
        }

        [Test]
        public void SelectedSourceWithoutAFrame_ClearsLatestState()
        {
            var selection = new SensorStreamSelectionController();
            var first = new FakeLatestSource("sensor-a");
            var second = new FakeLatestSource("sensor-b");
            first.Emit(10);

            using var selectedSource = new SelectedSensorDataSource<int>(selection);
            selectedSource.ReplaceSources(new IDataSource<int>[] { first, second });
            Assert.IsTrue(selectedSource.HasLatestFrame);

            selection.SelectNext();

            Assert.IsFalse(selectedSource.HasLatestFrame);
        }

        private sealed class FakeLatestSource : ILatestDataSource<int>
        {
            private int _sequence;

            public FakeLatestSource(string sourceId)
            {
                SourceId = sourceId;
            }

            public string SourceId { get; }
            public bool HasLatestFrame { get; private set; }
            public DataFrame<int> LatestFrame { get; private set; }

            public event Action<DataFrame<int>> OnFrame;

            public void Emit(int value)
            {
                LatestFrame = new DataFrame<int>(DateTime.UtcNow.Ticks, _sequence++, value);
                HasLatestFrame = true;
                OnFrame?.Invoke(LatestFrame);
            }
        }
    }
}
