using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AmpPortableDataViz.Core;
using UnityEngine;

namespace AmpPortableDataViz.Application
{
    /// <summary>
    /// Coordinates the visualization session lifecycle while remaining agnostic of Unity-specific concerns.
    /// Presentation adapters feed data sources and lifecycle events into this controller.
    /// </summary>
    public sealed class VisualizationSessionController : IDisposable
    {
        private readonly IClock _clock;
        private readonly IAnchorService _anchorService;
        private readonly INetworkSync _networkSync;
        private readonly IAnchorRegistry _anchorRegistry;
        private readonly IVisualizationFactory _visualizationFactory;

        private readonly Dictionary<string, IVisualizationInstance> _visualizations = new Dictionary<string, IVisualizationInstance>();
        private readonly Dictionary<string, PendingLocalParameters> _pendingLocalParameters = new Dictionary<string, PendingLocalParameters>();
        private readonly List<IDisposable> _dataSourceSubscriptions = new List<IDisposable>();

        private VisualizationSessionOptions _options;
        private IDisposable _spawnSubscription;
        private IDisposable _paramSubscription;
        private string _telemetryTargetVisualId;
        private bool _initialized;

        public VisualizationSessionController(
            IClock clock,
            IAnchorService anchorService,
            INetworkSync networkSync,
            IAnchorRegistry anchorRegistry,
            IVisualizationFactory visualizationFactory)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _anchorService = anchorService ?? throw new ArgumentNullException(nameof(anchorService));
            _networkSync = networkSync ?? throw new ArgumentNullException(nameof(networkSync));
            _anchorRegistry = anchorRegistry ?? throw new ArgumentNullException(nameof(anchorRegistry));
            _visualizationFactory = visualizationFactory ?? throw new ArgumentNullException(nameof(visualizationFactory));
        }

        public async Task InitializeAsync(VisualizationSessionOptions options, CancellationToken cancellationToken = default)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (_initialized) throw new InvalidOperationException("VisualizationSessionController has already been initialized.");

            _options = options;
            _initialized = true;

            _spawnSubscription = _networkSync.Subscribe(options.SpawnTopic, OnSpawnMessageReceived);
            _paramSubscription = _networkSync.Subscribe(options.ParamTopic, OnParamMessageReceived);

            if (!options.IsHost)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            string anchorId = await _anchorService.CreateAsync(
                options.AnchorLabel,
                options.AnchorPosition,
                options.AnchorRotation).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            AnchorDescriptor anchorDescriptor = await _anchorService.ExportDescriptorAsync(anchorId).ConfigureAwait(false);
            if (anchorDescriptor == null)
            {
                Debug.LogError("Failed to export anchor descriptor.");
                return;
            }

            var hostAnchorTransform = RegisterAnchor(anchorDescriptor.AnchorId);

            var spawnMessage = new SpawnVisualizationMsg
            {
                Anchor = anchorDescriptor,
                VisualId = options.VisualId,
                Seed = options.Seed,
                ProfileId = options.ProfileId
            };

            _networkSync.Publish(options.SpawnTopic, spawnMessage, _clock.UtcNowTicks);

            if (hostAnchorTransform != null && !_visualizations.ContainsKey(options.VisualId))
            {
                var instance = _visualizationFactory.Create(new VisualizationSpawnRequest(
                    options.VisualId,
                    anchorDescriptor,
                    options.Seed,
                    options.ProfileId));

                if (instance != null)
                {
                    instance.BindToAnchor(anchorDescriptor.AnchorId, hostAnchorTransform);
                    _visualizations[options.VisualId] = instance;
                    _telemetryTargetVisualId = options.VisualId;
                }
            }
        }

        public void BindSignalSource(IDataSource<float> source, IMapper<float, SimpleVisualParams> mapper)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (mapper == null) throw new ArgumentNullException(nameof(mapper));

            void Handler(DataFrame<float> frame)
            {
                var parameters = mapper.Map(in frame);
                var message = new ParamDeltaMsg
                {
                    VisualId = _options.VisualId,
                    TimestampTicks = frame.TimestampTicksUtc,
                    Params0 = new Vector4(parameters.Intensity, parameters.Color.r, parameters.Color.g, parameters.Color.b),
                    Params1 = new Vector4(parameters.Flow.x, parameters.Flow.y, parameters.Flow.z, 0f)
                };

                _networkSync.Publish(_options.ParamTopic, message, frame.TimestampTicksUtc);
            }

            source.OnFrame += Handler;
            _dataSourceSubscriptions.Add(new DelegateUnsubscriber(() => source.OnFrame -= Handler));
        }

        public void BindExternalSignalSource(IDataSource<float> source, IMapper<float, SimpleVisualParams> mapper)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (mapper == null) throw new ArgumentNullException(nameof(mapper));

            void Handler(DataFrame<float> frame)
            {
                var parameters = mapper.Map(in frame);

                if (_visualizations.TryGetValue(_options.VisualId, out var instance))
                {
                    instance.Apply(parameters, frame.TimestampTicksUtc);
                }
                else
                {
                    _pendingLocalParameters[_options.VisualId] = new PendingLocalParameters(parameters, frame.TimestampTicksUtc);
                }
            }

            source.OnFrame += Handler;
            _dataSourceSubscriptions.Add(new DelegateUnsubscriber(() => source.OnFrame -= Handler));
        }

        public void BindTelemetrySource(IDataSource<PoseSample> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            void Handler(DataFrame<PoseSample> frame)
            {
                if (string.IsNullOrEmpty(_telemetryTargetVisualId))
                {
                    return;
                }

                if (_visualizations.TryGetValue(_telemetryTargetVisualId, out var instance))
                {
                    instance.ApplyPose(frame.Payload);
                }
            }

            source.OnFrame += Handler;
            _dataSourceSubscriptions.Add(new DelegateUnsubscriber(() => source.OnFrame -= Handler));
        }

        public void Shutdown()
        {
            _spawnSubscription?.Dispose();
            _paramSubscription?.Dispose();
            _spawnSubscription = null;
            _paramSubscription = null;

            foreach (var subscription in _dataSourceSubscriptions)
            {
                subscription.Dispose();
            }
            _dataSourceSubscriptions.Clear();

            _visualizations.Clear();
            _pendingLocalParameters.Clear();
            _telemetryTargetVisualId = null;
            _initialized = false;
        }

        public void Dispose()
        {
            Shutdown();
        }

        private async void OnSpawnMessageReceived(object payload, long messageTimestampTicksUtc)
        {
            if (payload is not SpawnVisualizationMsg spawnMessage)
            {
                return;
            }

            try
            {
                string resolvedAnchorId = await _anchorService.ImportAndResolveAsync(spawnMessage.Anchor).ConfigureAwait(false);
                var anchorTransform = RegisterAnchor(resolvedAnchorId);

                if (!_visualizations.TryGetValue(spawnMessage.VisualId, out var instance))
                {
                    instance = _visualizationFactory.Create(new VisualizationSpawnRequest(
                        spawnMessage.VisualId,
                        spawnMessage.Anchor,
                        spawnMessage.Seed,
                        spawnMessage.ProfileId));

                    if (instance == null)
                    {
                        Debug.LogWarning($"Visualization factory returned null for visual id '{spawnMessage.VisualId}'.");
                        return;
                    }

                    _visualizations[spawnMessage.VisualId] = instance;
                }

                if (anchorTransform != null)
                {
                    instance.BindToAnchor(resolvedAnchorId, anchorTransform);
                }

                if (_pendingLocalParameters.TryGetValue(spawnMessage.VisualId, out var pendingParameters))
                {
                    instance.Apply(pendingParameters.Parameters, pendingParameters.TimestampTicks);
                    _pendingLocalParameters.Remove(spawnMessage.VisualId);
                }

                _telemetryTargetVisualId = spawnMessage.VisualId;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void OnParamMessageReceived(object payload, long messageTimestampTicksUtc)
        {
            if (payload is not ParamDeltaMsg paramDelta)
            {
                return;
            }

            if (!_visualizations.TryGetValue(paramDelta.VisualId, out var instance))
            {
                return;
            }

            var parameters = new SimpleVisualParams
            {
                Intensity = paramDelta.Params0.x,
                Color = new Color(paramDelta.Params0.y, paramDelta.Params0.z, paramDelta.Params0.w, 1f),
                Flow = new Vector3(paramDelta.Params1.x, paramDelta.Params1.y, paramDelta.Params1.z)
            };

            instance.Apply(parameters, paramDelta.TimestampTicks);
        }

        private Transform RegisterAnchor(string anchorId)
        {
            if (string.IsNullOrEmpty(anchorId))
            {
                return null;
            }

            var anchorTransform = _anchorService.GetTransform(anchorId);
            if (anchorTransform == null)
            {
                Debug.LogWarning($"Anchor transform not found for id '{anchorId}'.");
                return null;
            }

            _anchorRegistry.Register(anchorId, anchorTransform);
            return anchorTransform;
        }

        private sealed class DelegateUnsubscriber : IDisposable
        {
            private readonly Action _onDispose;
            private bool _disposed;

            public DelegateUnsubscriber(Action onDispose)
            {
                _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _onDispose();
            }
        }

        private readonly struct PendingLocalParameters
        {
            public SimpleVisualParams Parameters { get; }
            public long TimestampTicks { get; }

            public PendingLocalParameters(SimpleVisualParams parameters, long timestampTicks)
            {
                Parameters = parameters;
                TimestampTicks = timestampTicks;
            }
        }
    }
}
