using System;
using NUnit.Framework;
using UnityEngine;
using AmpPortableDataViz.Core;

namespace AmpPortableDataViz.Tests.Core
{
    public class DataFrameEdgeCaseTests
    {
        [Test]
        public void DataFrame_BoundaryValues()
        {
            var minFrame = new DataFrame<int>(long.MinValue, int.MinValue, int.MinValue);
            Assert.AreEqual(long.MinValue, minFrame.TimestampTicksUtc);
            Assert.AreEqual(int.MinValue, minFrame.SequenceId);
            Assert.AreEqual(int.MinValue, minFrame.Payload);

            var maxFrame = new DataFrame<int>(long.MaxValue, int.MaxValue, int.MaxValue);
            Assert.AreEqual(long.MaxValue, maxFrame.TimestampTicksUtc);
            Assert.AreEqual(int.MaxValue, maxFrame.SequenceId);
            Assert.AreEqual(int.MaxValue, maxFrame.Payload);
        }

        [Test]
        public void DataFrame_NullPayload_ReferenceType()
        {
            var frame = new DataFrame<string>(0, 0, null);
            Assert.IsNull(frame.Payload);
        }

        [Test]
        public void DataFrame_DefaultStruct()
        {
            var frame = default(DataFrame<int>);
            Assert.AreEqual(0, frame.TimestampTicksUtc);
            Assert.AreEqual(0, frame.SequenceId);
            Assert.AreEqual(0, frame.Payload);
        }
    }

    public class ClockEdgeCaseTests
    {
        class EdgeClock : IClock
        {
            public long UtcNowTicks { get; set; }
            public double SecondsSinceStartup { get; set; }
        }

        [Test]
        public void IClock_BoundaryValues()
        {
            var clock = new EdgeClock { UtcNowTicks = long.MinValue, SecondsSinceStartup = double.NaN };
            Assert.AreEqual(long.MinValue, clock.UtcNowTicks);
            Assert.IsTrue(double.IsNaN(clock.SecondsSinceStartup));

            clock = new EdgeClock { UtcNowTicks = long.MaxValue, SecondsSinceStartup = double.PositiveInfinity };
            Assert.AreEqual(long.MaxValue, clock.UtcNowTicks);
            Assert.IsTrue(double.IsPositiveInfinity(clock.SecondsSinceStartup));
        }
    }

    public class DataSourceEdgeCaseTests
    {
        class MockDataSource : IDataSource<int>
        {
            public string SourceId { get; set; }
            public event Action<DataFrame<int>> OnFrame;
            public void RaiseFrame(DataFrame<int> frame) => OnFrame?.Invoke(frame);
        }

        [Test]
        public void IDataSource_RaiseFrame_NoSubscribers_DoesNotThrow()
        {
            var dataSource = new MockDataSource();
            Assert.DoesNotThrow(() => dataSource.RaiseFrame(new DataFrame<int>(0, 0, 0)));
        }

        [Test]
        public void IDataSource_MultipleSubscribers_AllCalled()
        {
            var dataSource = new MockDataSource();
            int callCount = 0;
            dataSource.OnFrame += _ => callCount++;
            dataSource.OnFrame += _ => callCount++;
            dataSource.RaiseFrame(new DataFrame<int>(0, 0, 0));
            Assert.AreEqual(2, callCount);
        }

        [Test]
        public void IDataSource_Unsubscribe_HandlerNotCalled()
        {
            var dataSource = new MockDataSource();
            int callCount = 0;
            Action<DataFrame<int>> handler = _ => callCount++;
            dataSource.OnFrame += handler;
            dataSource.OnFrame -= handler;
            dataSource.RaiseFrame(new DataFrame<int>(0, 0, 0));
            Assert.AreEqual(0, callCount);
        }
    }

    public class MapperEdgeCaseTests
    {
        class MockMapper : IMapper<string, int>
        {
            public int Map(in DataFrame<string> inputFrame) => inputFrame.Payload == null ? -1 : inputFrame.Payload.Length;
        }

        [Test]
        public void IMapper_Map_NullPayload()
        {
            var mapper = new MockMapper();
            var frame = new DataFrame<string>(0, 0, null);
            Assert.AreEqual(-1, mapper.Map(frame));
        }

        [Test]
        public void IMapper_Map_DefaultFrame()
        {
            var mapper = new MockMapper();
            var frame = default(DataFrame<string>);
            Assert.AreEqual(-1, mapper.Map(frame));
        }
    }

    public class VisualizerEdgeCaseTests
    {
        class MockVisualizer : IVisualizer<string>
        {
            public string LastParams;
            public long LastTimestamp;
            public void Apply(in string parameters, long timestampTicksUtc)
            {
                LastParams = parameters;
                LastTimestamp = timestampTicksUtc;
            }
        }

        [Test]
        public void IVisualizer_Apply_NullParams()
        {
            var visualizer = new MockVisualizer();
            visualizer.Apply(null, 0);
            Assert.IsNull(visualizer.LastParams);
        }

        [Test]
        public void IVisualizer_Apply_BoundaryTimestamp()
        {
            var visualizer = new MockVisualizer();
            visualizer.Apply("test", long.MaxValue);
            Assert.AreEqual(long.MaxValue, visualizer.LastTimestamp);
        }
    }

}