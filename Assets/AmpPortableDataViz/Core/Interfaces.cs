using System;
using UnityEngine;

namespace AmpPortableDataViz.Core
{
    public readonly struct DataFrame<TPayload>
    {
        public readonly long TimestampTicksUtc;
        public readonly int SequenceId;
        public readonly TPayload Payload;
        public DataFrame(long timestampTicksUtc, int sequenceId, TPayload payload)
        { TimestampTicksUtc = timestampTicksUtc; SequenceId = sequenceId; Payload = payload; }
    }

    public interface IClock
    {
        long UtcNowTicks { get; }
        double SecondsSinceStartup { get; }
    }

    public interface IDataSource<TPayload>
    {
        string SourceId { get; }
        event Action<DataFrame<TPayload>> OnFrame;
    }

    public interface ILatestDataSource<TPayload> : IDataSource<TPayload>
    {
        bool HasLatestFrame { get; }
        DataFrame<TPayload> LatestFrame { get; }
    }

    public interface IMapper<TInput, TOutput>
    {
        TOutput Map(in DataFrame<TInput> inputFrame);
    }

    public interface IVisualizer<TParams>
    {
        void Apply(in TParams parameters, long timestampTicksUtc);
    }

    public interface INetworkSync
    {
        void Publish<TPayload>(string topic, TPayload payload, long timestampTicksUtc);
        IDisposable Subscribe(string topic, Action<object, long> messageHandler);
    }

    [Serializable]
    public class AnchorDescriptor
    {
        public string AnchorId;
        public Vector3 Position;
        public Quaternion Rotation;
        public string Label;
    }

    public interface IAnchorService
    {
        System.Threading.Tasks.Task<string> CreateAsync(string label, Vector3 position, Quaternion rotation);
        System.Threading.Tasks.Task<bool> LoadAsync(string anchorId);
        System.Threading.Tasks.Task<AnchorDescriptor> ExportDescriptorAsync(string anchorId);
        System.Threading.Tasks.Task<string> ImportAndResolveAsync(AnchorDescriptor descriptor);
        Transform GetTransform(string anchorId);
    }
}
