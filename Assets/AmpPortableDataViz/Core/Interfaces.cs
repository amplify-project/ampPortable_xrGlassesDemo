using System;

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

}
