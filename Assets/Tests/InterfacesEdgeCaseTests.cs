using System;
using System.Threading.Tasks;
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

    public class AnchorDescriptorEdgeCaseTests
    {
        [Test]
        public void AnchorDescriptor_NullAndEmptyFields()
        {
            var desc = new AnchorDescriptor
            {
                AnchorId = null,
                Label = null,
                Position = Vector3.zero,
                Rotation = Quaternion.identity
            };
            Assert.IsNull(desc.AnchorId);
            Assert.IsNull(desc.Label);

            desc.AnchorId = "";
            desc.Label = "";
            Assert.AreEqual("", desc.AnchorId);
            Assert.AreEqual("", desc.Label);
        }

        [Test]
        public void AnchorDescriptor_ExtremeValues()
        {
            var desc = new AnchorDescriptor
            {
                AnchorId = "id",
                Label = "label",
                Position = Vector3.positiveInfinity,
                Rotation = new Quaternion(float.PositiveInfinity, 0, 0, 1)
            };
            Assert.IsTrue(float.IsInfinity(desc.Position.x));
            Assert.IsTrue(float.IsInfinity(desc.Rotation.x));
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

    public class NetworkSyncEdgeCaseTests
    {
        class MockNetworkSync : INetworkSync
        {
            public string LastTopic;
            public object LastPayload;
            public long LastTimestamp;
            public Action<object, long> Handler;
            public void Publish<TPayload>(string topic, TPayload payload, long timestampTicksUtc)
            {
                LastTopic = topic;
                LastPayload = payload;
                LastTimestamp = timestampTicksUtc;
            }
            public IDisposable Subscribe(string topic, Action<object, long> messageHandler)
            {
                Handler = messageHandler;
                return new DummyDisposable();
            }
            class DummyDisposable : IDisposable { public void Dispose() { } }
        }

        [Test]
        public void INetworkSync_Publish_NullOrEmptyTopic()
        {
            var sync = new MockNetworkSync();
            Assert.DoesNotThrow(() => sync.Publish(null, 42, 0));
            Assert.DoesNotThrow(() => sync.Publish("", 42, 0));
        }

        [Test]
        public void INetworkSync_Publish_NullPayload()
        {
            var sync = new MockNetworkSync();
            Assert.DoesNotThrow(() => sync.Publish<object>("topic", null, 0));
        }

        [Test]
        public void INetworkSync_Subscribe_NullHandler()
        {
            var sync = new MockNetworkSync();
            Assert.DoesNotThrow(() => sync.Subscribe("topic", null));
        }

        [Test]
        public void INetworkSync_Subscribe_NullOrEmptyTopic()
        {
            var sync = new MockNetworkSync();
            Assert.DoesNotThrow(() => sync.Subscribe(null, (obj, ts) => { }));
            Assert.DoesNotThrow(() => sync.Subscribe("", (obj, ts) => { }));
        }

        [Test]
        public void INetworkSync_DisposeTwice_DoesNotThrow()
        {
            var sync = new MockNetworkSync();
            var disposable = sync.Subscribe("topic", (obj, ts) => { });
            Assert.DoesNotThrow(() => { disposable.Dispose(); disposable.Dispose(); });
        }
    }

    public class AnchorServiceEdgeCaseTests
    {
        class MockAnchorService : IAnchorService
        {
            public Task<string> CreateAsync(string label, Vector3 position, Quaternion rotation) => Task.FromResult(label ?? "null");
            public Task<bool> LoadAsync(string anchorId) => Task.FromResult(anchorId == "exists");
            public Task<AnchorDescriptor> ExportDescriptorAsync(string anchorId) => Task.FromResult(anchorId == "exists" ? new AnchorDescriptor { AnchorId = anchorId } : null);
            public Task<string> ImportAndResolveAsync(AnchorDescriptor descriptor) => Task.FromResult(descriptor?.AnchorId ?? "null");
            public Transform GetTransform(string anchorId) => null;
        }

        [Test]
        public async Task IAnchorService_NullOrEmptyArguments()
        {
            var service = new MockAnchorService();
            var id = await service.CreateAsync(null, Vector3.zero, Quaternion.identity);
            Assert.AreEqual("null", id);

            var loaded = await service.LoadAsync(null);
            Assert.IsFalse(loaded);

            var desc = await service.ExportDescriptorAsync(null);
            Assert.IsNull(desc);

            var resolved = await service.ImportAndResolveAsync(null);
            Assert.AreEqual("null", resolved);
        }

        [Test]
        public async Task IAnchorService_NonexistentAnchor()
        {
            var service = new MockAnchorService();
            var loaded = await service.LoadAsync("doesnotexist");
            Assert.IsFalse(loaded);

            var desc = await service.ExportDescriptorAsync("doesnotexist");
            Assert.IsNull(desc);

            var transform = service.GetTransform("doesnotexist");
            Assert.IsNull(transform);
        }
    }
}