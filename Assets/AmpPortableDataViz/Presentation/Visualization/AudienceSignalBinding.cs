using System;
using System.Collections.Generic;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Presentation.Mapping;
using AmpPortableDataViz.Presentation.Sources;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Visualization
{
    /// <summary>
    /// Aggregates Redis physiological, engagement, valence, and arousal streams for particle mesh and graph visuals.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Amp Portable Data Viz/Visualization/Audience Signal Binding")]
    public sealed class AudienceSignalBinding : MonoBehaviour, IDataSource<AudienceSignalSample>
    {
        [Serializable]
        private struct GraphMetricBinding
        {
            public AudienceMetricKind Metric;
            public GraphVisualizer Visualizer;
            public Vector2 YRange;
            public Color LineColor;
            [Range(0.0001f, 0.05f)] public float LineWidth;

            public string ResolveLabel()
            {
                return Metric.ToString();
            }
        }

        [Header("Redis Sources")]
        [SerializeField] private RedisPhysioMetricsPump physioSource;
        [SerializeField] private RedisDataPump valenceSource;
        [SerializeField] private RedisDataPump arousalSource;
        [SerializeField] private RedisDataPump engagementSource;

        [Header("Device Identity")]
        [SerializeField] private string deviceId;

        [Header("Particle Mesh Target")]
        [SerializeField] private ParticleMeshVisualizer particleMeshVisualizer;
        [SerializeField] private bool requireAllSignalsBeforeParticleApply = true;
        [SerializeField] private AudienceSignalToParticleMeshMapper.Settings particleMapperSettings =
            AudienceSignalToParticleMeshMapper.CreateDefaultSettings();

        [Header("Graph Targets")]
        [SerializeField] private GraphMetricBinding[] graphStreams = Array.Empty<GraphMetricBinding>();
        [SerializeField, Range(0f, 120f)] private float graphWindowSeconds = 10f;
        [SerializeField, Range(16, 4096)] private int graphMaxSamples = 512;

        [Header("Graph Dynamic Y Range")]
        [SerializeField] private bool useDynamicYRange = true;
        [SerializeField, Range(0.5f, 4f)] private float dynamicYRangeStdDevMultiplier = 2.5f;
        [SerializeField, Range(0.01f, 1f)] private float dynamicYRangeMinRangeFraction = 0.1f;
        [SerializeField, Range(0f, 1f)] private float dynamicYRangeSmoothing = 0.15f;

        [Header("Diagnostics")]
        [SerializeField] private bool logRawInputs;
        [SerializeField] private bool logResolvedSamples;

        private IMapper<AudienceSignalSample, ParticleMeshSignalSample> _particleMapper;
        private GraphSeriesToGraphParamsMapper[] _graphMappers;
        private List<Vector2>[] _graphSamples;
        private long[] _graphStartTimestampTicks;
        private float[] _graphLatestXSeconds;
        private float[] _graphCurrentYMin;
        private float[] _graphCurrentYMax;
        private bool[] _graphHasDynamicYRange;
        private int[] _graphSequenceIds;

        private Action<DataFrame<PhysioMetricsSample>> _physioHandler;
        private Action<DataFrame<float>> _valenceHandler;
        private Action<DataFrame<float>> _arousalHandler;
        private Action<DataFrame<float>> _engagementHandler;

        private bool _hasPhysio;
        private bool _hasValence;
        private bool _hasArousal;
        private bool _hasEngagement;

        private PhysioMetricsSample _latestPhysio;
        private float _latestValence = 1f;
        private float _latestArousal = 1f;
        private float _latestEngagement = 0.5f;
        private long _latestPhysioTimestamp;
        private long _latestValenceTimestamp;
        private long _latestArousalTimestamp;
        private long _latestEngagementTimestamp;
        private int _particleSequenceId;

        public string SourceId => ResolveDeviceId();

        public bool HasLatestFrame { get; private set; }

        public DataFrame<AudienceSignalSample> LatestFrame { get; private set; }

        public event Action<DataFrame<AudienceSignalSample>> OnFrame;

        private void Awake()
        {
            ResolveTargets();
            EnsureMappers();
        }

        private void OnEnable()
        {
            ResetState();
            EnsureGraphState();
            AttachSources();
            WarnIfUnconfigured();
        }

        private void OnDisable()
        {
            DetachSources();
            ResetState();
        }

        private void OnValidate()
        {
            ResolveTargets();
            EnsureMappers();
            EnsureGraphState();
            UpdateGraphMapperSettings();
        }

        public void ConfigureSources(
            RedisPhysioMetricsPump newPhysioSource,
            RedisDataPump newValenceSource,
            RedisDataPump newArousalSource,
            RedisDataPump newEngagementSource,
            string overrideDeviceId = null)
        {
            bool wasEnabled = isActiveAndEnabled;
            if (wasEnabled)
            {
                DetachSources();
            }

            physioSource = newPhysioSource;
            valenceSource = newValenceSource;
            arousalSource = newArousalSource;
            engagementSource = newEngagementSource;

            if (!string.IsNullOrWhiteSpace(overrideDeviceId))
            {
                deviceId = overrideDeviceId.Trim();
            }

            if (wasEnabled)
            {
                ResetState();
                AttachSources();
            }
        }

        public void ConfigureParticleTarget(ParticleMeshVisualizer visualizer)
        {
            particleMeshVisualizer = visualizer;
        }

        private void ResolveTargets()
        {
            if (particleMeshVisualizer == null)
            {
                particleMeshVisualizer = GetComponent<ParticleMeshVisualizer>();
            }
        }

        private void AttachSources()
        {
            SubscribeToPhysioPump(physioSource, ref _physioHandler, OnPhysioFrame);
            SubscribeToFloatPump(valenceSource, ref _valenceHandler, OnValenceFrame, "valence");
            SubscribeToFloatPump(arousalSource, ref _arousalHandler, OnArousalFrame, "arousal");
            SubscribeToFloatPump(engagementSource, ref _engagementHandler, OnEngagementFrame, "engagement");
        }

        private void DetachSources()
        {
            UnsubscribeFromPhysioPump(physioSource, ref _physioHandler);
            UnsubscribeFromFloatPump(valenceSource, ref _valenceHandler);
            UnsubscribeFromFloatPump(arousalSource, ref _arousalHandler);
            UnsubscribeFromFloatPump(engagementSource, ref _engagementHandler);
        }

        private void SubscribeToPhysioPump(RedisPhysioMetricsPump pump, ref Action<DataFrame<PhysioMetricsSample>> handler, Action<DataFrame<PhysioMetricsSample>> callback)
        {
            if (pump == null)
            {
                Debug.LogWarning($"AudienceSignalBinding[{ResolveDeviceId()}] is missing a physio Redis source.", this);
                return;
            }

            handler ??= callback;
            pump.OnFrame += handler;
        }

        private void SubscribeToFloatPump(RedisDataPump pump, ref Action<DataFrame<float>> handler, Action<DataFrame<float>> callback, string label)
        {
            if (pump == null)
            {
                Debug.LogWarning($"AudienceSignalBinding[{ResolveDeviceId()}] is missing a {label} Redis source.", this);
                return;
            }

            handler ??= callback;
            pump.OnFrame += handler;
        }

        private void UnsubscribeFromPhysioPump(RedisPhysioMetricsPump pump, ref Action<DataFrame<PhysioMetricsSample>> handler)
        {
            if (pump != null && handler != null)
            {
                pump.OnFrame -= handler;
            }

            handler = null;
        }

        private void UnsubscribeFromFloatPump(RedisDataPump pump, ref Action<DataFrame<float>> handler)
        {
            if (pump != null && handler != null)
            {
                pump.OnFrame -= handler;
            }

            handler = null;
        }

        private void OnPhysioFrame(DataFrame<PhysioMetricsSample> frame)
        {
            _latestPhysio = frame.Payload;
            _latestPhysioTimestamp = frame.TimestampTicksUtc;
            _hasPhysio = true;

            if (logRawInputs)
            {
                Debug.Log($"AudienceSignalBinding[{ResolveDeviceId()}] physio {frame.Payload} seq={frame.SequenceId} ts={frame.TimestampTicksUtc}", this);
            }

            AppendGraphSample(AudienceMetricKind.TonicElectrodermalActivityStdDev, frame.Payload.TonicElectrodermalActivityStdDev, frame.TimestampTicksUtc);
            AppendGraphSample(AudienceMetricKind.TemperatureRateOfChangeStdDev, frame.Payload.TemperatureRateOfChangeStdDev, frame.TimestampTicksUtc);
            AppendGraphSample(AudienceMetricKind.SkinConductanceResponseFrequencyStdDev, frame.Payload.SkinConductanceResponseFrequencyStdDev, frame.TimestampTicksUtc);
            AppendGraphSample(AudienceMetricKind.HeartRateStdDev, frame.Payload.HeartRateStdDev, frame.TimestampTicksUtc);
            AppendGraphSample(AudienceMetricKind.InterBeatIntervalStdDev, frame.Payload.InterBeatIntervalStdDev, frame.TimestampTicksUtc);
            TryApplyParticleMesh();
        }

        private void OnValenceFrame(DataFrame<float> frame)
        {
            _latestValence = frame.Payload;
            _latestValenceTimestamp = frame.TimestampTicksUtc;
            _hasValence = true;

            if (logRawInputs)
            {
                Debug.Log($"AudienceSignalBinding[{ResolveDeviceId()}] valence={_latestValence:F4} seq={frame.SequenceId} ts={frame.TimestampTicksUtc}", this);
            }

            AppendGraphSample(AudienceMetricKind.Valence, _latestValence, frame.TimestampTicksUtc);
            TryApplyParticleMesh();
        }

        private void OnArousalFrame(DataFrame<float> frame)
        {
            _latestArousal = frame.Payload;
            _latestArousalTimestamp = frame.TimestampTicksUtc;
            _hasArousal = true;

            if (logRawInputs)
            {
                Debug.Log($"AudienceSignalBinding[{ResolveDeviceId()}] arousal={_latestArousal:F4} seq={frame.SequenceId} ts={frame.TimestampTicksUtc}", this);
            }

            AppendGraphSample(AudienceMetricKind.Arousal, _latestArousal, frame.TimestampTicksUtc);
            TryApplyParticleMesh();
        }

        private void OnEngagementFrame(DataFrame<float> frame)
        {
            _latestEngagement = frame.Payload;
            _latestEngagementTimestamp = frame.TimestampTicksUtc;
            _hasEngagement = true;

            if (logRawInputs)
            {
                Debug.Log($"AudienceSignalBinding[{ResolveDeviceId()}] engagement={_latestEngagement:F4} seq={frame.SequenceId} ts={frame.TimestampTicksUtc}", this);
            }

            AppendGraphSample(AudienceMetricKind.Engagement, _latestEngagement, frame.TimestampTicksUtc);
            TryApplyParticleMesh();
        }

        private void TryApplyParticleMesh()
        {
            if (requireAllSignalsBeforeParticleApply && (!_hasPhysio || !_hasValence || !_hasArousal || !_hasEngagement))
            {
                return;
            }

            var sample = BuildAudienceSample();
            long timestamp = ResolveLatestTimestamp();
            var frame = new DataFrame<AudienceSignalSample>(timestamp, _particleSequenceId++, sample);
            LatestFrame = frame;
            HasLatestFrame = true;
            OnFrame?.Invoke(frame);

            if (particleMeshVisualizer != null && _particleMapper != null)
            {
                ParticleMeshSignalSample parameters = _particleMapper.Map(in frame);
                particleMeshVisualizer.Apply(parameters, frame.TimestampTicksUtc);
            }

            if (logResolvedSamples)
            {
                Debug.Log($"AudienceSignalBinding[{ResolveDeviceId()}] -> ParticleMesh {sample}", this);
            }
        }

        private AudienceSignalSample BuildAudienceSample()
        {
            return new AudienceSignalSample(
                ResolveDeviceId(),
                _latestPhysio.TonicElectrodermalActivityStdDev,
                _latestPhysio.TemperatureRateOfChangeStdDev,
                _latestPhysio.SkinConductanceResponseFrequencyStdDev,
                _latestPhysio.HeartRateStdDev,
                _latestPhysio.InterBeatIntervalStdDev,
                _latestEngagement,
                _latestArousal,
                _latestValence);
        }

        private long ResolveLatestTimestamp()
        {
            long timestamp = _latestPhysioTimestamp;
            if (_latestValenceTimestamp > timestamp) timestamp = _latestValenceTimestamp;
            if (_latestArousalTimestamp > timestamp) timestamp = _latestArousalTimestamp;
            if (_latestEngagementTimestamp > timestamp) timestamp = _latestEngagementTimestamp;
            return timestamp;
        }

        private void AppendGraphSample(AudienceMetricKind metricKind, float value, long timestampTicksUtc)
        {
            EnsureGraphState();

            if (graphStreams == null || _graphSamples == null)
            {
                return;
            }

            for (int i = 0; i < graphStreams.Length; i++)
            {
                if (graphStreams[i].Metric != metricKind || graphStreams[i].Visualizer == null)
                {
                    continue;
                }

                AppendGraphSample(i, value, timestampTicksUtc);
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
            samples.Add(new Vector2(xSeconds, value));

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

            ResolveYRange(index, binding, samples, out float yMin, out float yMax);

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
                _graphCurrentYMin != null &&
                _graphCurrentYMin.Length == length &&
                _graphCurrentYMax != null &&
                _graphCurrentYMax.Length == length &&
                _graphHasDynamicYRange != null &&
                _graphHasDynamicYRange.Length == length)
            {
                EnsureGraphSampleCapacities();
                return;
            }

            _graphMappers = new GraphSeriesToGraphParamsMapper[length];
            _graphSamples = new List<Vector2>[length];
            _graphStartTimestampTicks = new long[length];
            _graphLatestXSeconds = new float[length];
            _graphCurrentYMin = new float[length];
            _graphCurrentYMax = new float[length];
            _graphHasDynamicYRange = new bool[length];
            _graphSequenceIds = new int[length];

            for (int i = 0; i < length; i++)
            {
                _graphMappers[i] = new GraphSeriesToGraphParamsMapper(MakeGraphSettings(graphStreams[i]));
                _graphSamples[i] = new List<Vector2>(Mathf.Max(16, graphMaxSamples));
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

        private void ResetState()
        {
            _hasPhysio = false;
            _hasValence = false;
            _hasArousal = false;
            _hasEngagement = false;
            _latestPhysio = default;
            _latestValence = 1f;
            _latestArousal = 1f;
            _latestEngagement = 0.5f;
            _latestPhysioTimestamp = 0;
            _latestValenceTimestamp = 0;
            _latestArousalTimestamp = 0;
            _latestEngagementTimestamp = 0;
            _particleSequenceId = 0;
            HasLatestFrame = false;
            LatestFrame = default;

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

            if (_graphHasDynamicYRange != null)
            {
                Array.Clear(_graphHasDynamicYRange, 0, _graphHasDynamicYRange.Length);
            }
        }

        private void WarnIfUnconfigured()
        {
            bool hasExternalVisualizerBinding = GetComponentInChildren<AudienceSignalVisualizerBinding>() != null;
            if (!hasExternalVisualizerBinding && particleMeshVisualizer == null && (graphStreams == null || graphStreams.Length == 0))
            {
                Debug.LogWarning($"AudienceSignalBinding[{ResolveDeviceId()}] has no particle mesh or graph targets configured.", this);
            }
        }

        private string ResolveDeviceId()
        {
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                return deviceId.Trim();
            }

            if (_hasPhysio && !string.IsNullOrWhiteSpace(_latestPhysio.DeviceId))
            {
                return _latestPhysio.DeviceId;
            }

            return name;
        }

        private static GraphSeriesToGraphParamsMapper.Settings MakeGraphSettings(GraphMetricBinding binding)
        {
            return new GraphSeriesToGraphParamsMapper.Settings
            {
                LineColor = binding.LineColor,
                LineWidth = Mathf.Max(0f, binding.LineWidth)
            };
        }

        private void ResolveYRange(int index, GraphMetricBinding binding, IReadOnlyList<Vector2> samples, out float yMin, out float yMax)
        {
            yMin = binding.YRange.x;
            yMax = binding.YRange.y;
            NormalizeRange(ref yMin, ref yMax);

            if (!useDynamicYRange || samples == null || samples.Count == 0 || index < 0)
            {
                return;
            }

            float hardMin = yMin;
            float hardMax = yMax;
            float hardRange = Mathf.Max(1e-4f, hardMax - hardMin);

            float sum = 0f;
            float sumSquares = 0f;
            int count = samples.Count;
            for (int i = 0; i < count; i++)
            {
                float value = samples[i].y;
                sum += value;
                sumSquares += value * value;
            }

            float mean = sum / count;
            float variance = Mathf.Max(0f, (sumSquares / count) - mean * mean);
            float stdDev = Mathf.Sqrt(variance);
            float minRange = Mathf.Max(1e-4f, hardRange * Mathf.Max(0.001f, dynamicYRangeMinRangeFraction));
            float targetRange = Mathf.Max(minRange, stdDev * Mathf.Max(0f, dynamicYRangeStdDevMultiplier) * 2f);
            if (targetRange >= hardRange)
            {
                yMin = hardMin;
                yMax = hardMax;
            }
            else
            {
                float halfRange = targetRange * 0.5f;
                yMin = mean - halfRange;
                yMax = mean + halfRange;

                if (yMin < hardMin)
                {
                    float shift = hardMin - yMin;
                    yMin += shift;
                    yMax += shift;
                }

                if (yMax > hardMax)
                {
                    float shift = yMax - hardMax;
                    yMin -= shift;
                    yMax -= shift;
                }

                yMin = Mathf.Clamp(yMin, hardMin, hardMax);
                yMax = Mathf.Clamp(yMax, hardMin, hardMax);
            }

            if (_graphCurrentYMin == null || _graphCurrentYMax == null || _graphHasDynamicYRange == null || index >= _graphCurrentYMin.Length)
            {
                return;
            }

            float smoothing = Mathf.Clamp01(dynamicYRangeSmoothing);
            if (!_graphHasDynamicYRange[index] || smoothing <= 0f)
            {
                _graphCurrentYMin[index] = yMin;
                _graphCurrentYMax[index] = yMax;
                _graphHasDynamicYRange[index] = true;
            }
            else
            {
                _graphCurrentYMin[index] = Mathf.Lerp(_graphCurrentYMin[index], yMin, smoothing);
                _graphCurrentYMax[index] = Mathf.Lerp(_graphCurrentYMax[index], yMax, smoothing);
            }

            yMin = Mathf.Clamp(_graphCurrentYMin[index], hardMin, hardMax);
            yMax = Mathf.Clamp(_graphCurrentYMax[index], hardMin, hardMax);
            NormalizeRange(ref yMin, ref yMax);
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
}
