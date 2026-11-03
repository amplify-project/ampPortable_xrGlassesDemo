using System;
using NUnit.Framework;
using UnityEngine;
using AmpPortableDataViz.Core;

namespace AmpPortableDataViz.Tests.Core
{
    public class DataFrameTests
    {
        [Test]
        public void DataFrame_Constructor_SetsProperties()
        {
            var payload = 42;
            var frame = new DataFrame<int>(123456789, 7, payload);

            Assert.AreEqual(123456789, frame.TimestampTicksUtc);
            Assert.AreEqual(7, frame.SequenceId);
            Assert.AreEqual(payload, frame.Payload);
        }
    }

    // Mock implementations for interface testing
    class MockClock : IClock
    {
        public long UtcNowTicks { get; set; }
        public double SecondsSinceStartup { get; set; }
    }

    class MockDataSource : IDataSource<int>
    {
        public string SourceId { get; set; }
        public event Action<DataFrame<int>> OnFrame;
        public void RaiseFrame(DataFrame<int> frame) => OnFrame?.Invoke(frame);
    }

    class MockMapper : IMapper<int, string>
    {
        public string Map(in DataFrame<int> inputFrame) => inputFrame.Payload.ToString();
    }

    class MockVisualizer : IVisualizer<Vector3>
    {
        public Vector3 LastParams;
        public long LastTimestamp;
        public void Apply(in Vector3 parameters, long timestampTicksUtc)
        {
            LastParams = parameters;
            LastTimestamp = timestampTicksUtc;
        }
    }

    public class InterfaceTests
    {
        [Test]
        public void IClock_Properties_Work()
        {
            var clock = new MockClock { UtcNowTicks = 123, SecondsSinceStartup = 4.5 };
            Assert.AreEqual(123, clock.UtcNowTicks);
            Assert.AreEqual(4.5, clock.SecondsSinceStartup);
        }

        [Test]
        public void IDataSource_OnFrame_Event_Raises()
        {
            var dataSource = new MockDataSource { SourceId = "src" };
            DataFrame<int> received = default;
            dataSource.OnFrame += frame => received = frame;
            var frameToSend = new DataFrame<int>(1, 2, 3);
            dataSource.RaiseFrame(frameToSend);
            Assert.AreEqual(frameToSend, received);
        }

        [Test]
        public void IMapper_Map_ReturnsExpected()
        {
            var mapper = new MockMapper();
            var frame = new DataFrame<int>(0, 0, 123);
            var result = mapper.Map(frame);
            Assert.AreEqual("123", result);
        }

        [Test]
        public void IVisualizer_Apply_SetsValues()
        {
            var visualizer = new MockVisualizer();
            var vec = new Vector3(1, 2, 3);
            visualizer.Apply(vec, 99);
            Assert.AreEqual(vec, visualizer.LastParams);
            Assert.AreEqual(99, visualizer.LastTimestamp);
        }

    }
}