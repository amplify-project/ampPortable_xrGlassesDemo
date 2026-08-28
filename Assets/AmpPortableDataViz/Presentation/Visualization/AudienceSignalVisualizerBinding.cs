using System;
using System.Collections.Generic;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Presentation.Mapping;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Visualization
{
    /// <summary>
    /// Applies shared audience data samples to particle mesh and graph visualizers.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Amp Portable Data Viz/Visualization/Audience Signal Visualizer Binding")]
    public sealed class AudienceSignalVisualizerBinding : MonoBehaviour
    {
        private static readonly Vector2 SignedPhysioGraphRange = new Vector2(-3f, 3f);
        private static readonly Vector2 LegacyTonicEdaRange = new Vector2(0.01f, 0.5f);
        private static readonly Vector2 LegacyTemperatureRateRange = new Vector2(0.001f, 0.1f);
        private static readonly Vector2 LegacyScrFrequencyRange = new Vector2(0.5f, 5f);
        private static readonly Vector2 LegacyHeartRateRange = new Vector2(1f, 10f);

        public enum SourceMode
        {
            Auto,
            LiveRedis,
            Manual
        }

        [Serializable]
        public struct GraphMetricBinding
        {
            public AudienceMetricKind Metric;
            public GraphVisualizer Visualizer;
            public Vector2 YRange;
            public Color LineColor;
            [Range(0.0001f, 0.05f)] public float LineWidth;
        }

        [Header("Sources")]
        [SerializeField] private SourceMode sourceMode = SourceMode.Auto;
        [SerializeField] private AudienceSignalBinding liveSource;
        [SerializeField] private AudienceDataManualDriver manualSource;

        [Header("Particle Mesh Target")]
        [SerializeField] private ParticleMeshVisualizer particleMeshVisualizer;
        [SerializeField] private AudienceSignalToParticleMeshMapper.Settings particleMapperSettings =
            AudienceSignalToParticleMeshMapper.CreateDefaultSettings();

        [Header("Particle Mesh Dynamic Physio Amplification")]
        [SerializeField] private ParticleMeshPhysioAmplificationSettings particlePhysioAmplificationSettings =
            ParticleMeshPhysioAmplificationSettings.CreateDefault();

        [Header("Graph Targets")]
        [SerializeField] private GraphMetricBinding[] graphStreams = Array.Empty<GraphMetricBinding>();
        [SerializeField, Range(0f, 120f)] private float graphWindowSeconds = 10f;
        [SerializeField, Range(16, 4096)] private int graphMaxSamples = 512;

        [Header("Graph Temporal Smoothing")]
        [SerializeField] private bool useGraphTemporalSmoothing = true;
        [SerializeField, Range(0.01f, 2f)] private float graphSmoothingHalfLifeSeconds = 0.2f;

        [Header("Graph Dynamic Y Range")]
        [SerializeField] private bool useDynamicYRange = true;
        [SerializeField, Range(0.5f, 4f)] private float dynamicYRangeStdDevMultiplier = 2.5f;
        [SerializeField, Range(0.01f, 1f)] private float dynamicYRangeMinRangeFraction = 0.05f;
        [SerializeField, Range(0f, 0.5f)] private float dynamicYRangePaddingFraction = 0.15f;
        [SerializeField, Range(0f, 2f)] private float dynamicYRangeZoomOutTimeSeconds = 0.1f;
        [SerializeField, Range(0.1f, 10f)] private float dynamicYRangeZoomInTimeSeconds = 2f;

        [Header("Diagnostics")]
        [SerializeField] private bool logReceivedSamples;

        private IMapper<AudienceSignalSample, ParticleMeshSignalSample> _particleMapper;
        private readonly ParticleMeshPhysioAmplifier _particlePhysioAmplifier = new ParticleMeshPhysioAmplifier();
        private GraphSeriesToGraphParamsMapper[] _graphMappers;
        private List<Vector2>[] _graphSamples;
        private long[] _graphStartTimestampTicks;
        private float[] _graphLatestXSeconds;
        private DynamicGraphYRangeTracker[] _graphYRangeTrackers;
        private GraphTemporalSmoother[] _graphTemporalSmoothers;
        private int[] _graphSequenceIds;

        private Action<DataFrame<AudienceSignalSample>> _liveHandler;
        private Action<DataFrame<AudienceSignalSample>> _manualHandler;
        private IDataSource<AudienceSignalSample> _runtimeLiveSource;
        private string _activeDeviceId = string.Empty;

        private void Awake()
        {
            ResolveReferences();
            EnsureMappers();
            EnsureGraphState();
        }

        private void OnEnable()
        {
            ResetGraphState();
            _particlePhysioAmplifier.Reset();
            ResolveReferences();
            EnsureMappers();
            EnsureGraphState();
            AttachSource();
            ApplyLatestFrameIfAvailable();
        }

        private void OnDisable()
        {
            DetachSource();
            ResetGraphState();
        }

        private void OnValidate()
        {
            ResolveReferences();
            EnsureMappers();
            EnsureGraphState();
            UpdateGraphMapperSettings();
        }

        public void ConfigureLiveSource(AudienceSignalBinding source, string overrideDeviceId = null)
        {
            ConfigureLiveSource((IDataSource<AudienceSignalSample>)source, overrideDeviceId);
        }

        public void ConfigureLiveSource(IDataSource<AudienceSignalSample> source, string overrideDeviceId = null)
        {
            bool wasEnabled = isActiveAndEnabled;
            if (wasEnabled)
            {
                DetachSource();
            }

            liveSource = source as AudienceSignalBinding;
            _runtimeLiveSource = source;
            if (source != null)
            {
                sourceMode = SourceMode.LiveRedis;
            }

            if (wasEnabled)
            {
                ResetGraphState();
                AttachSource();
                ApplyLatestFrameIfAvailable();
            }
        }

        public void ConfigureManualSource(AudienceDataManualDriver source)
        {
            bool wasEnabled = isActiveAndEnabled;
            if (wasEnabled)
            {
                DetachSource();
            }

            manualSource = source;
            if (source != null)
            {
                sourceMode = SourceMode.Manual;
            }

            if (wasEnabled)
            {
                ResetGraphState();
                AttachSource();
                ApplyLatestFrameIfAvailable();
            }
        }

        public void ConfigureParticleTarget(ParticleMeshVisualizer visualizer)
        {
            particleMeshVisualizer = visualizer;
        }

        private void ResolveReferences()
        {
            if (liveSource == null)
            {
                liveSource = GetComponent<AudienceSignalBinding>();
            }

            if (_runtimeLiveSource == null)
            {
                _runtimeLiveSource = liveSource;
            }

            if (manualSource == null)
            {
                manualSource = GetComponent<AudienceDataManualDriver>();
            }

            if (particleMeshVisualizer == null)
            {
                particleMeshVisualizer = GetComponent<ParticleMeshVisualizer>();
            }
        }

        private void AttachSource()
        {
            switch (ResolveSourceMode())
            {
                case SourceMode.Manual:
                    if (manualSource == null)
                    {
                        Debug.LogWarning($"{nameof(AudienceSignalVisualizerBinding)}[{name}] is missing a manual source.", this);
                        return;
                    }

                    _manualHandler ??= OnFrame;
                    manualSource.OnFrame += _manualHandler;
                    break;
                case SourceMode.LiveRedis:
                    IDataSource<AudienceSignalSample> selectedLiveSource = ResolveLiveSource();
                    if (selectedLiveSource == null)
                    {
                        Debug.LogWarning($"{nameof(AudienceSignalVisualizerBinding)}[{name}] is missing a live source.", this);
                        return;
                    }

                    _liveHandler ??= OnFrame;
                    selectedLiveSource.OnFrame += _liveHandler;
                    break;
            }
        }

        private void DetachSource()
        {
            IDataSource<AudienceSignalSample> selectedLiveSource = ResolveLiveSource();
            if (selectedLiveSource != null && _liveHandler != null)
            {
                selectedLiveSource.OnFrame -= _liveHandler;
            }

            if (manualSource != null && _manualHandler != null)
            {
                manualSource.OnFrame -= _manualHandler;
            }

            _liveHandler = null;
            _manualHandler = null;
        }

        private void ApplyLatestFrameIfAvailable()
        {
            switch (ResolveSourceMode())
            {
                case SourceMode.Manual:
                    if (manualSource != null && manualSource.HasLatestFrame)
                    {
                        OnFrame(manualSource.LatestFrame);
                    }
                    break;
                case SourceMode.LiveRedis:
                    if (ResolveLiveSource() is ILatestDataSource<AudienceSignalSample> latestLiveSource && latestLiveSource.HasLatestFrame)
                    {
                        OnFrame(latestLiveSource.LatestFrame);
                    }
                    break;
            }
        }

        private SourceMode ResolveSourceMode()
        {
            if (sourceMode == SourceMode.Auto)
            {
                if (manualSource != null && manualSource.enabled)
                {
                    return SourceMode.Manual;
                }

                if (liveSource != null && liveSource.enabled)
                {
                    return SourceMode.LiveRedis;
                }

                return manualSource != null ? SourceMode.Manual : SourceMode.LiveRedis;
            }

            return sourceMode;
        }

        private void OnFrame(DataFrame<AudienceSignalSample> frame)
        {
            string frameDeviceId = frame.Payload.DeviceId ?? string.Empty;
            if (!string.Equals(_activeDeviceId, frameDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                ResetGraphState();
                _particlePhysioAmplifier.Reset();
                _activeDeviceId = frameDeviceId;
            }

            if (logReceivedSamples)
            {
                Debug.Log($"{nameof(AudienceSignalVisualizerBinding)}[{name}] received {frame.Payload}", this);
            }

            ApplyParticleMesh(frame);
            ApplyGraphStreams(frame);
        }

        private IDataSource<AudienceSignalSample> ResolveLiveSource()
        {
            return _runtimeLiveSource ?? liveSource;
        }

        private void ApplyParticleMesh(DataFrame<AudienceSignalSample> frame)
        {
            if (particleMeshVisualizer == null || _particleMapper == null)
            {
                return;
            }

            ParticleMeshSignalSample parameters = _particleMapper.Map(in frame);
            parameters = _particlePhysioAmplifier.Apply(parameters, frame.TimestampTicksUtc, particlePhysioAmplificationSettings);
            particleMeshVisualizer.Apply(parameters, frame.TimestampTicksUtc);
        }

        private void ApplyGraphStreams(DataFrame<AudienceSignalSample> frame)
        {
            if (graphStreams == null || graphStreams.Length == 0)
            {
                return;
            }

            EnsureGraphState();
            for (int i = 0; i < graphStreams.Length; i++)
            {
                if (graphStreams[i].Visualizer == null ||
                    !AudienceVisualizationMetricPolicy.IsGraphMetricSupported(graphStreams[i].Metric))
                {
                    continue;
                }

                float value = ResolveGraphValue(graphStreams[i].Metric, frame.Payload);
                AppendGraphSample(i, value, frame.TimestampTicksUtc);
            }
        }

        private void AppendGraphSample(int index, float value, long timestampTicksUtc)
        {
            var samples = _graphSamples[index];
            if (samples == null)
            {
                samples = new List<Vector2>(Mathf.Max(16, graphMaxSamples));
                _graphSamples[index] = samples;
            }

            if (_graphStartTimestampTicks[index] == 0)
            {
                _graphStartTimestampTicks[index] = timestampTicksUtc;
            }

            float xSeconds = (float)((timestampTicksUtc - _graphStartTimestampTicks[index]) / (double)TimeSpan.TicksPerSecond);
            float displayValue = value;
            if (_graphTemporalSmoothers != null && index >= 0 && index < _graphTemporalSmoothers.Length)
            {
                var smoother = _graphTemporalSmoothers[index];
                if (smoother != null)
                {
                    float halfLifeSeconds = useGraphTemporalSmoothing ? graphSmoothingHalfLifeSeconds : 0f;
                    displayValue = smoother.Apply(value, timestampTicksUtc, halfLifeSeconds);
                }
            }

            samples.Add(new Vector2(xSeconds, displayValue));

            if (xSeconds > _graphLatestXSeconds[index])
            {
                _graphLatestXSeconds[index] = xSeconds;
            }

            PruneGraphSamples(samples, _graphLatestXSeconds[index]);
            ApplyGraphStream(index, timestampTicksUtc);
        }

        private void ApplyGraphStream(int index, long timestampTicksUtc)
        {
            if (index < 0 || graphStreams == null || index >= graphStreams.Length)
            {
                return;
            }

            var binding = graphStreams[index];
            var visualizer = binding.Visualizer;
            var mapper = _graphMappers[index];
            var samples = _graphSamples[index];

            if (visualizer == null || mapper == null || samples == null)
            {
                return;
            }

            mapper.CurrentSettings = MakeGraphSettings(binding);

            float xMax = _graphLatestXSeconds[index];
            float xMin = graphWindowSeconds > 0f ? Mathf.Max(0f, xMax - graphWindowSeconds) : 0f;
            NormalizeRange(ref xMin, ref xMax);

            ResolveYRange(index, binding, samples, timestampTicksUtc, out float yMin, out float yMax);

            Vector2[] points = samples.Count == 0 ? Array.Empty<Vector2>() : samples.ToArray();
            var series = new GraphSeriesSample(xMin, xMax, yMin, yMax, points);
            var frame = new DataFrame<GraphSeriesSample>(timestampTicksUtc, _graphSequenceIds[index]++, series);
            GraphParams parameters = mapper.Map(in frame);
            visualizer.Apply(parameters, frame.TimestampTicksUtc);
        }

        private void PruneGraphSamples(List<Vector2> samples, float latestXSeconds)
        {
            if (samples == null || samples.Count == 0)
            {
                return;
            }

            if (graphWindowSeconds > 0f)
            {
                float cutoff = latestXSeconds - graphWindowSeconds;
                int removeCount = 0;
                while (removeCount < samples.Count && samples[removeCount].x < cutoff)
                {
                    removeCount++;
                }

                if (removeCount > 0)
                {
                    samples.RemoveRange(0, removeCount);
                }
            }

            if (graphMaxSamples <= 0)
            {
                return;
            }

            int extraCount = samples.Count - graphMaxSamples;
            if (extraCount > 0)
            {
                samples.RemoveRange(0, extraCount);
            }
        }

        private void EnsureMappers()
        {
            _particleMapper ??= new AudienceSignalToParticleMeshMapper(particleMapperSettings);
            if (_particleMapper is AudienceSignalToParticleMeshMapper particleMapper)
            {
                particleMapper.CurrentSettings = particleMapperSettings;
            }
        }

        private void EnsureGraphState()
        {
            int length = graphStreams == null ? 0 : graphStreams.Length;
            if (_graphSamples != null &&
                _graphSamples.Length == length &&
                _graphYRangeTrackers != null &&
                _graphYRangeTrackers.Length == length &&
                _graphTemporalSmoothers != null &&
                _graphTemporalSmoothers.Length == length)
            {
                EnsureGraphSampleCapacities();
                return;
            }

            _graphMappers = new GraphSeriesToGraphParamsMapper[length];
            _graphSamples = new List<Vector2>[length];
            _graphStartTimestampTicks = new long[length];
            _graphLatestXSeconds = new float[length];
            _graphYRangeTrackers = new DynamicGraphYRangeTracker[length];
            _graphTemporalSmoothers = new GraphTemporalSmoother[length];
            _graphSequenceIds = new int[length];

            for (int i = 0; i < length; i++)
            {
                _graphMappers[i] = new GraphSeriesToGraphParamsMapper(MakeGraphSettings(graphStreams[i]));
                _graphSamples[i] = new List<Vector2>(Mathf.Max(16, graphMaxSamples));
                _graphYRangeTrackers[i] = new DynamicGraphYRangeTracker();
                _graphTemporalSmoothers[i] = new GraphTemporalSmoother();
            }
        }

        private void EnsureGraphSampleCapacities()
        {
            if (_graphSamples == null || graphMaxSamples <= 0)
            {
                return;
            }

            for (int i = 0; i < _graphSamples.Length; i++)
            {
                if (_graphSamples[i] == null)
                {
                    _graphSamples[i] = new List<Vector2>(Mathf.Max(16, graphMaxSamples));
                    continue;
                }

                if (_graphSamples[i].Capacity < graphMaxSamples)
                {
                    _graphSamples[i].Capacity = graphMaxSamples;
                }
            }
        }

        private void UpdateGraphMapperSettings()
        {
            if (_graphMappers == null || graphStreams == null)
            {
                return;
            }

            int count = Mathf.Min(_graphMappers.Length, graphStreams.Length);
            for (int i = 0; i < count; i++)
            {
                if (_graphMappers[i] != null)
                {
                    _graphMappers[i].CurrentSettings = MakeGraphSettings(graphStreams[i]);
                }
            }
        }

        private void ResetGraphState()
        {
            if (_graphSamples != null)
            {
                for (int i = 0; i < _graphSamples.Length; i++)
                {
                    _graphSamples[i]?.Clear();
                }
            }

            if (_graphStartTimestampTicks != null)
            {
                Array.Clear(_graphStartTimestampTicks, 0, _graphStartTimestampTicks.Length);
            }

            if (_graphLatestXSeconds != null)
            {
                Array.Clear(_graphLatestXSeconds, 0, _graphLatestXSeconds.Length);
            }

            if (_graphSequenceIds != null)
            {
                Array.Clear(_graphSequenceIds, 0, _graphSequenceIds.Length);
            }

            if (_graphYRangeTrackers != null)
            {
                for (int i = 0; i < _graphYRangeTrackers.Length; i++)
                {
                    _graphYRangeTrackers[i]?.Reset();
                }
            }

            if (_graphTemporalSmoothers != null)
            {
                for (int i = 0; i < _graphTemporalSmoothers.Length; i++)
                {
                    _graphTemporalSmoothers[i]?.Reset();
                }
            }
        }

        private static GraphSeriesToGraphParamsMapper.Settings MakeGraphSettings(GraphMetricBinding binding)
        {
            return new GraphSeriesToGraphParamsMapper.Settings
            {
                LineColor = binding.LineColor,
                LineWidth = Mathf.Max(0f, binding.LineWidth)
            };
        }

        private void ResolveYRange(
            int index,
            GraphMetricBinding binding,
            IReadOnlyList<Vector2> samples,
            long timestampTicksUtc,
            out float yMin,
            out float yMax)
        {
            if (IsPhysioMetric(binding.Metric))
            {
                yMin = SignedPhysioGraphRange.x;
                yMax = SignedPhysioGraphRange.y;
            }
            else
            {
                yMin = binding.YRange.x;
                yMax = binding.YRange.y;
            }

            NormalizeRange(ref yMin, ref yMax);

            if (!useDynamicYRange ||
                samples == null ||
                samples.Count == 0 ||
                index < 0 ||
                _graphYRangeTrackers == null ||
                index >= _graphYRangeTrackers.Length)
            {
                return;
            }

            var tracker = _graphYRangeTrackers[index];
            if (tracker == null)
            {
                tracker = new DynamicGraphYRangeTracker();
                _graphYRangeTrackers[index] = tracker;
            }

            tracker.Resolve(
                samples,
                yMin,
                yMax,
                dynamicYRangeStdDevMultiplier,
                dynamicYRangeMinRangeFraction,
                dynamicYRangePaddingFraction,
                dynamicYRangeZoomOutTimeSeconds,
                dynamicYRangeZoomInTimeSeconds,
                timestampTicksUtc,
                out yMin,
                out yMax);
        }

        private static float ResolveGraphValue(AudienceMetricKind metricKind, AudienceSignalSample sample)
        {
            float value = sample.GetMetricValue(metricKind);
            if (!IsPhysioMetric(metricKind))
            {
                return value;
            }

            if (sample.PhysioEncoding != PhysioMetricsEncoding.LegacyStdDev)
            {
                return Mathf.Clamp(value, SignedPhysioGraphRange.x, SignedPhysioGraphRange.y);
            }

            return Mathf.Clamp(NormalizeLegacyPhysioValue(metricKind, value) * SignedPhysioGraphRange.y, 0f, SignedPhysioGraphRange.y);
        }

        private static float NormalizeLegacyPhysioValue(AudienceMetricKind metricKind, float value)
        {
            Vector2 range = metricKind switch
            {
                AudienceMetricKind.TonicElectrodermalActivityStdDev => LegacyTonicEdaRange,
                AudienceMetricKind.TemperatureRateOfChangeStdDev => LegacyTemperatureRateRange,
                AudienceMetricKind.SkinConductanceResponseFrequencyStdDev => LegacyScrFrequencyRange,
                AudienceMetricKind.HeartRateStdDev => LegacyHeartRateRange,
                _ => new Vector2(0f, 1f)
            };

            return Mathf.Clamp01(Mathf.InverseLerp(range.x, range.y, value));
        }

        private static bool IsPhysioMetric(AudienceMetricKind metricKind)
        {
            return metricKind == AudienceMetricKind.TonicElectrodermalActivityStdDev ||
                metricKind == AudienceMetricKind.TemperatureRateOfChangeStdDev ||
                metricKind == AudienceMetricKind.SkinConductanceResponseFrequencyStdDev ||
                metricKind == AudienceMetricKind.HeartRateStdDev;
        }

        private static void NormalizeRange(ref float min, ref float max)
        {
            if (max < min)
            {
                (min, max) = (max, min);
            }

            if (Mathf.Abs(max - min) < 1e-5f)
            {
                max = min + 1e-4f;
            }
        }
    }

    internal static class AudienceVisualizationMetricPolicy
    {
        public static bool IsGraphMetricSupported(AudienceMetricKind metricKind)
        {
            return metricKind == AudienceMetricKind.TonicElectrodermalActivityStdDev ||
                metricKind == AudienceMetricKind.TemperatureRateOfChangeStdDev ||
                metricKind == AudienceMetricKind.SkinConductanceResponseFrequencyStdDev ||
                metricKind == AudienceMetricKind.HeartRateStdDev ||
                metricKind == AudienceMetricKind.Engagement;
        }
    }
}
