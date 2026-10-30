using System;
using System.Collections.Generic;
using AmpPortableDataViz.Core;

namespace AmpPortableDataViz.Application
{
    /// <summary>
    /// Keeps all registered sensor sources live while forwarding frames only from the selected sensor.
    /// </summary>
    public sealed class SelectedSensorDataSource<TPayload> : ILatestDataSource<TPayload>, IDisposable
    {
        private static readonly StringComparer SensorIdComparer = StringComparer.OrdinalIgnoreCase;

        private readonly SensorStreamSelectionController _selection;
        private readonly Dictionary<string, IDataSource<TPayload>> _sourcesById =
            new Dictionary<string, IDataSource<TPayload>>(SensorIdComparer);
        private readonly Dictionary<string, DataFrame<TPayload>> _latestFramesById =
            new Dictionary<string, DataFrame<TPayload>>(SensorIdComparer);
        private readonly Dictionary<IDataSource<TPayload>, Action<DataFrame<TPayload>>> _handlersBySource =
            new Dictionary<IDataSource<TPayload>, Action<DataFrame<TPayload>>>();

        private bool _suppressSelectionReplay;
        private bool _disposed;

        public SelectedSensorDataSource(SensorStreamSelectionController selection)
        {
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _selection.SelectionChanged += OnSelectionChanged;
        }

        public string SourceId => _selection.ActiveSensorId;
        public bool HasLatestFrame { get; private set; }
        public DataFrame<TPayload> LatestFrame { get; private set; }

        public event Action<DataFrame<TPayload>> OnFrame;

        public void ReplaceSources(IEnumerable<IDataSource<TPayload>> sources)
        {
            ThrowIfDisposed();
            DetachSources();

            if (sources != null)
            {
                foreach (IDataSource<TPayload> source in sources)
                {
                    AddSource(source);
                }
            }

            _suppressSelectionReplay = true;
            try
            {
                _selection.ReplaceSensors(_sourcesById.Keys);
            }
            finally
            {
                _suppressSelectionReplay = false;
            }

            ReplayLatestSelectedFrame();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _selection.SelectionChanged -= OnSelectionChanged;
            DetachSources();
            OnFrame = null;
        }

        private void AddSource(IDataSource<TPayload> source)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.SourceId))
            {
                return;
            }

            string sourceId = source.SourceId.Trim();
            if (_sourcesById.ContainsKey(sourceId))
            {
                return;
            }

            _sourcesById.Add(sourceId, source);

            if (source is ILatestDataSource<TPayload> latestSource && latestSource.HasLatestFrame)
            {
                _latestFramesById[sourceId] = latestSource.LatestFrame;
            }

            void Handler(DataFrame<TPayload> frame)
            {
                _latestFramesById[sourceId] = frame;
                if (SensorIdComparer.Equals(sourceId, _selection.ActiveSensorId))
                {
                    Publish(frame);
                }
            }

            _handlersBySource.Add(source, Handler);
            source.OnFrame += Handler;
        }

        private void DetachSources()
        {
            foreach (KeyValuePair<IDataSource<TPayload>, Action<DataFrame<TPayload>>> pair in _handlersBySource)
            {
                pair.Key.OnFrame -= pair.Value;
            }

            _handlersBySource.Clear();
            _sourcesById.Clear();
            _latestFramesById.Clear();
            HasLatestFrame = false;
            LatestFrame = default;
        }

        private void OnSelectionChanged(SensorStreamSelectionChanged change)
        {
            if (!_suppressSelectionReplay)
            {
                ReplayLatestSelectedFrame();
            }
        }

        private void ReplayLatestSelectedFrame()
        {
            if (!_selection.HasActiveSensor ||
                !_latestFramesById.TryGetValue(_selection.ActiveSensorId, out DataFrame<TPayload> latestFrame))
            {
                HasLatestFrame = false;
                LatestFrame = default;
                return;
            }

            Publish(latestFrame);
        }

        private void Publish(DataFrame<TPayload> frame)
        {
            LatestFrame = frame;
            HasLatestFrame = true;
            OnFrame?.Invoke(frame);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SelectedSensorDataSource<TPayload>));
            }
        }
    }
}
