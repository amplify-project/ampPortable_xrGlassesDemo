using System;
using System.Threading.Tasks;
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

    public class AnchorDescriptorTests
    {
        [Test]
        public void AnchorDescriptor_Properties_SetAndGet()
        {
            var desc = new AnchorDescriptor
            {
                AnchorId = "id",
                Position = new Vector3(1, 2, 3),
                Rotation = Quaternion.Euler(10, 20, 30),
                Label = "test"
            };

            Assert.AreEqual("id", desc.AnchorId);
            Assert.AreEqual(new Vector3(1, 2, 3), desc.Position);
            Assert.AreEqual(Quaternion.Euler(10, 20, 30), desc.Rotation);
            Assert.AreEqual("test", desc.Label);
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

    class MockNetworkSync : INetworkSync
    {
        public string LastTopic;
        public object LastPayload;
        public long LastTimestamp;
        public void Publish<TPayload>(string topic, TPayload payload, long timestampTicksUtc)
        {
            LastTopic = topic;
            LastPayload = payload;
            LastTimestamp = timestampTicksUtc;
        }
        public IDisposable Subscribe(string topic, Action<object, long> messageHandler)
        {
            return new DummyDisposable();
        }
        class DummyDisposable : IDisposable { public void Dispose() { } }
    }

    class MockAnchorService : IAnchorService
    {
        public Task<string> CreateAsync(string label, Vector3 position, Quaternion rotation) => Task.FromResult("anchorId");
        public Task<bool> LoadAsync(string anchorId) => Task.FromResult(true);
        public Task<AnchorDescriptor> ExportDescriptorAsync(string anchorId) => Task.FromResult(new AnchorDescriptor { AnchorId = anchorId });
        public Task<string> ImportAndResolveAsync(AnchorDescriptor descriptor) => Task.FromResult(descriptor.AnchorId);
        public Transform GetTransform(string anchorId) => null;
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

        [Test]
        public void INetworkSync_Publish_SetsValues()
        {
            var sync = new MockNetworkSync();
            sync.Publish("topic", 42, 100);
            Assert.AreEqual("topic", sync.LastTopic);
            Assert.AreEqual(42, sync.LastPayload);
            Assert.AreEqual(100, sync.LastTimestamp);
        }

        [Test]
        public void INetworkSync_Subscribe_ReturnsDisposable()
        {
            var sync = new MockNetworkSync();
            var disposable = sync.Subscribe("topic", (obj, ts) => { });
            Assert.IsNotNull(disposable);
            disposable.Dispose();
        }

        [Test]
        public async Task IAnchorService_Methods_Work()
        {
            var service = new MockAnchorService();
            var anchorId = await service.CreateAsync("label", Vector3.zero, Quaternion.identity);
            Assert.AreEqual("anchorId", anchorId);

            var loaded = await service.LoadAsync("anchorId");
            Assert.IsTrue(loaded);

            var desc = await service.ExportDescriptorAsync("anchorId");
            Assert.AreEqual("anchorId", desc.AnchorId);

            var resolved = await service.ImportAndResolveAsync(desc);
            Assert.AreEqual("anchorId", resolved);

            var transform = service.GetTransform("anchorId");
            Assert.IsNull(transform);
        }
    }
}