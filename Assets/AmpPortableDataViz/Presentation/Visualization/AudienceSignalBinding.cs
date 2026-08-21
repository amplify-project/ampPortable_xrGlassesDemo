using System;
using System.Collections.Generic;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Presentation.Mapping;
using AmpPortableDataViz.Presentation.Sources;
using UnityEngine;
using UnityEngine.Serialization;

namespace AmpPortableDataViz.Presentation.Visualization
{
    /// <summary>
    /// Aggregates Redis physiological and engagement streams for particle mesh and graph visuals.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Amp Portable Data Viz/Visualization/Audience Signal Binding")]
    public sealed class AudienceSignalBinding : MonoBehaviour, ILatestDataSource<AudienceSignalSample>
    {
        private static readonly Vector2 SignedPhysioGraphRange = new Vector2(-3f, 3f);
        private static readonly Vector2 LegacyTonicEdaRange = new Vector2(0.01f, 0.5f);
        private static readonly Vector2 LegacyTemperatureRateRange = new Vector2(0.001f, 0.1f);
        private static readonly Vector2 LegacyScrFrequencyRange = new Vector2(0.5f, 5f);
        private static readonly Vector2 LegacyHeartRateRange = new Vector2(1f, 10f);

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
        [SerializeField] private RedisDataPump engagementSource;

        [Header("Device Identity")]
        [SerializeField] private string deviceId;

        [Header("Particle Mesh Target")]
        [SerializeField] private ParticleMeshVisualizer particleMeshVisualizer;
        [FormerlySerializedAs("requireAllSignalsBeforeParticleApply")]
        [SerializeField] private bool requirePhysioAndEngagementBeforeApply = true;
        [SerializeField] private AudienceSignalToParticleMeshMapper.Settings particleMapperSettings =
            AudienceSignalToParticleMeshMapper.CreateDefaultSettings();

        [Header("Particle Mesh Dynamic Physio Amplification")]
        [SerializeField] private ParticleMeshPhysioAmplificationSettings particlePhysioAmplificationSettings =
            ParticleMeshPhysioAmplificationSettings.CreateDefault();

        [Header("Graph Targets")]
        [SerializeField] private GraphMetricBinding[] graphStreams = Array.Empty<GraphMetricBinding>();
        [SerializeField, Range(0f, 120f)] private float graphWindowSeconds = 10f;
        [SerializeField, Range(16, 4096)] private int graphMaxSamples = 512;

        [Header("Graph Dynamic Y Range")]
        [SerializeField] private bool useDynamicYRange = true;
        [SerializeField, Range(0.5f, 4f)] private float dynamicYRangeStdDevMultiplier = 2.5f;
        [SerializeField, Range(0.01f, 1f)] private float dynamicYRangeMinRangeFraction = 0.05f;
        [SerializeField, Range(0f, 0.5f)] private float dynamicYRangePaddingFraction = 0.15f;
        [SerializeField, Range(0f, 2f)] private float dynamicYRangeZoomOutTimeSeconds = 0.1f;
        [SerializeField, Range(0.1f, 10f)] private float dynamicYRangeZoomInTimeSeconds = 2f;

        [Header("Diagnostics")]
        [SerializeField] private bool logRawInputs;
        [SerializeField] private bool logResolvedSamples;

        private IMapper<AudienceSignalSample, ParticleMeshSignalSample> _particleMapper;
        private readonly ParticleMeshPhysioAmplifier _particlePhysioAmplifier = new ParticleMeshPhysioAmplifier();
        private GraphSeriesToGraphParamsMapper[] _graphMappers;
        private List<Vector2>[] _graphSamples;
        private long[] _graphStartTimestampTicks;
        private float[] _graphLatestXSeconds;
        private DynamicGraphYRangeTracker[] _graphYRangeTrackers;
        private int[] _graphSequenceIds;

        private Action<DataFrame<PhysioMetricsSample>> _physioHandler;
        private Action<DataFrame<float>> _engagementHandler;

        private bool _hasPhysio;
        private bool _hasEngagement;

        private PhysioMetricsSample _latestPhysio;
        private float _latestEngagement = 0.5f;
        private long _latestPhysioTimestamp;
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
            RedisDataPump newEngagementSource,
            string overrideDeviceId = null)
        {
            bool wasEnabled = isActiveAndEnabled;
            if (wasEnabled)
            {
                DetachSources();
            }

            physioSource = newPhysioSource;
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
            SubscribeToFloatPump(engagementSource, ref _engagementHandler, OnEngagementFrame, "engagement");
        }

        private void DetachSources()
        {
            UnsubscribeFromPhysioPump(physioSource, ref _physioHandler);
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

            AppendGraphSample(AudienceMetricKind.TonicElectrodermalActivityStdDev, ResolvePhysioGraphValue(AudienceMetricKind.TonicElectrodermalActivityStdDev, frame.Payload), frame.TimestampTicksUtc);
            AppendGraphSample(AudienceMetricKind.TemperatureRateOfChangeStdDev, ResolvePhysioGraphValue(AudienceMetricKind.TemperatureRateOfChangeStdDev, frame.Payload), frame.TimestampTicksUtc);
            AppendGraphSample(AudienceMetricKind.SkinConductanceResponseFrequencyStdDev, ResolvePhysioGraphValue(AudienceMetricKind.SkinConductanceResponseFrequencyStdDev, frame.Payload), frame.TimestampTicksUtc);
            AppendGraphSample(AudienceMetricKind.HeartRateStdDev, ResolvePhysioGraphValue(AudienceMetricKind.HeartRateStdDev, frame.Payload), frame.TimestampTicksUtc);
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
            if (requirePhysioAndEngagementBeforeApply && (!_hasPhysio || !_hasEngagement))
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
                parameters = _particlePhysioAmplifier.Apply(parameters, _latestPhysioTimestamp, particlePhysioAmplificationSettings);
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
                _latestPhysio.Encoding,
                _latestPhysio.TonicElectrodermalActivityStdDev,
                _latestPhysio.TemperatureRateOfChangeStdDev,
                _latestPhysio.SkinConductanceResponseFrequencyStdDev,
                _latestPhysio.HeartRateStdDev,
                _latestPhysio.InterBeatIntervalStdDev,
                _latestEngagement,
                1f,
                1f);
        }

        private long ResolveLatestTimestamp()
        {
            long timestamp = _latestPhysioTimestamp;
            if (_latestEngagementTimestamp > timestamp) timestamp = _latestEngagementTimestamp;
            return timestamp;
        }

        private void AppendGraphSample(AudienceMetricKind metricKind, float value, long timestampTicksUtc)
        {
            if (!AudienceVisualizationMetricPolicy.IsGraphMetricSupported(metricKind))
            {
                return;
            }

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
                _graphYRangeTrackers.Length == length)
            {
                EnsureGraphSampleCapacities();
                return;
            }

            _graphMappers = new GraphSeriesToGraphParamsMapper[length];
            _graphSamples = new List<Vector2>[length];
            _graphStartTimestampTicks = new long[length];
            _graphLatestXSeconds = new float[length];
            _graphYRangeTrackers = new DynamicGraphYRangeTracker[length];
            _graphSequenceIds = new int[length];

            for (int i = 0; i < length; i++)
            {
                _graphMappers[i] = new GraphSeriesToGraphParamsMapper(MakeGraphSettings(graphStreams[i]));
                _graphSamples[i] = new List<Vector2>(Mathf.Max(16, graphMaxSamples));
                _graphYRangeTrackers[i] = new DynamicGraphYRangeTracker();
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
            _hasEngagement = false;
            _latestPhysio = default;
            _latestEngagement = 0.5f;
            _latestPhysioTimestamp = 0;
            _latestEngagementTimestamp = 0;
            _particleSequenceId = 0;
            HasLatestFrame = false;
            LatestFrame = default;
            _particlePhysioAmplifier.Reset();

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

        private static float ResolvePhysioGraphValue(AudienceMetricKind metricKind, PhysioMetricsSample sample)
        {
            float value = metricKind switch
            {
                AudienceMetricKind.TonicElectrodermalActivityStdDev => sample.TonicElectrodermalActivityStdDev,
                AudienceMetricKind.TemperatureRateOfChangeStdDev => sample.TemperatureRateOfChangeStdDev,
                AudienceMetricKind.SkinConductanceResponseFrequencyStdDev => sample.SkinConductanceResponseFrequencyStdDev,
                AudienceMetricKind.HeartRateStdDev => sample.HeartRateStdDev,
                _ => 0f
            };

            if (sample.Encoding != PhysioMetricsEncoding.LegacyStdDev)
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

    [Serializable]
    public struct ParticleMeshPhysioAmplificationSettings
    {
        public bool Enabled;
        [Range(0.5f, 120f)] public float WindowSeconds;
        [Range(8, 4096)] public int MaxSamples;
        [Range(0.5f, 4f)] public float StdDevMultiplier;
        [Range(0.01f, 2f)] public float MinRange;
        [Range(0f, 1f)] public float Smoothing;
        [Range(0f, 1f)] public float Blend;

        public static ParticleMeshPhysioAmplificationSettings CreateDefault()
        {
            return new ParticleMeshPhysioAmplificationSettings
            {
                Enabled = true,
                WindowSeconds = 10f,
                MaxSamples = 256,
                StdDevMultiplier = 2f,
                MinRange = 0.18f,
                Smoothing = 0.15f,
                Blend = 1f
            };
        }
    }

    internal sealed class ParticleMeshPhysioAmplifier
    {
        private const int ChannelCount = 4;
        private const float HardMin = -1f;
        private const float HardMax = 1f;
        private const float HardRange = HardMax - HardMin;

        private readonly List<Vector2>[] _samples = new List<Vector2>[ChannelCount];
        private readonly float[] _currentMin = new float[ChannelCount];
        private readonly float[] _currentMax = new float[ChannelCount];
        private readonly bool[] _hasDynamicRange = new bool[ChannelCount];
        private readonly float[] _lastValues = new float[ChannelCount];
        private readonly float[] _candidateValues = new float[ChannelCount];

        private long _startTimestampTicks;
        private long _lastSampleTimestampTicks;
        private float _latestXSeconds;
        private bool _hasLastSample;

        public ParticleMeshSignalSample Apply(
            in ParticleMeshSignalSample sample,
            long physioTimestampTicksUtc,
            ParticleMeshPhysioAmplificationSettings settings)
        {
            settings = ResolveSettings(settings);
            if (!settings.Enabled || settings.Blend <= 0f)
            {
                return sample;
            }

            EnsureSampleLists(settings.MaxSamples);
            AppendSampleIfChanged(sample, physioTimestampTicksUtc, settings);

            return new ParticleMeshSignalSample(
                sample.DeviceId,
                AmplifyChannel(0, sample.TonicElectrodermalActivityStdDev, settings),
                AmplifyChannel(1, sample.TemperatureRateOfChangeStdDev, settings),
                AmplifyChannel(2, sample.SkinConductanceResponseFrequencyStdDev, settings),
                AmplifyChannel(3, sample.HeartRateStdDev, settings),
                sample.Engagement);
        }

        public void Reset()
        {
            for (int i = 0; i < ChannelCount; i++)
            {
                _samples[i]?.Clear();
            }

            Array.Clear(_currentMin, 0, _currentMin.Length);
            Array.Clear(_currentMax, 0, _currentMax.Length);
            Array.Clear(_hasDynamicRange, 0, _hasDynamicRange.Length);
            Array.Clear(_lastValues, 0, _lastValues.Length);
            Array.Clear(_candidateValues, 0, _candidateValues.Length);
            _startTimestampTicks = 0;
            _lastSampleTimestampTicks = 0;
            _latestXSeconds = 0f;
            _hasLastSample = false;
        }

        private static ParticleMeshPhysioAmplificationSettings ResolveSettings(ParticleMeshPhysioAmplificationSettings settings)
        {
            if (!settings.Enabled &&
                settings.WindowSeconds <= 0f &&
                settings.MaxSamples <= 0 &&
                settings.StdDevMultiplier <= 0f &&
                settings.MinRange <= 0f &&
                settings.Smoothing <= 0f &&
                settings.Blend <= 0f)
            {
                return ParticleMeshPhysioAmplificationSettings.CreateDefault();
            }

            settings.WindowSeconds = Mathf.Clamp(settings.WindowSeconds, 0.5f, 120f);
            settings.MaxSamples = Mathf.Clamp(settings.MaxSamples, 8, 4096);
            settings.StdDevMultiplier = Mathf.Clamp(settings.StdDevMultiplier, 0.5f, 4f);
            settings.MinRange = Mathf.Clamp(settings.MinRange, 0.01f, HardRange);
            settings.Smoothing = Mathf.Clamp01(settings.Smoothing);
            settings.Blend = Mathf.Clamp01(settings.Blend);
            return settings;
        }

        private void EnsureSampleLists(int maxSamples)
        {
            int capacity = Mathf.Max(8, maxSamples);
            for (int i = 0; i < ChannelCount; i++)
            {
                if (_samples[i] == null)
                {
                    _samples[i] = new List<Vector2>(capacity);
                    continue;
                }

                if (_samples[i].Capacity < capacity)
                {
                    _samples[i].Capacity = capacity;
                }
            }
        }

        private void AppendSampleIfChanged(
            in ParticleMeshSignalSample sample,
            long physioTimestampTicksUtc,
            ParticleMeshPhysioAmplificationSettings settings)
        {
            _candidateValues[0] = sample.TonicElectrodermalActivityStdDev;
            _candidateValues[1] = sample.TemperatureRateOfChangeStdDev;
            _candidateValues[2] = sample.SkinConductanceResponseFrequencyStdDev;
            _candidateValues[3] = sample.HeartRateStdDev;

            if (_hasLastSample && _lastSampleTimestampTicks == physioTimestampTicksUtc && HasSameValues())
            {
                return;
            }

            if (_startTimestampTicks == 0)
            {
                _startTimestampTicks = physioTimestampTicksUtc;
            }

            float xSeconds = physioTimestampTicksUtc > 0 && _startTimestampTicks > 0
                ? (float)((physioTimestampTicksUtc - _startTimestampTicks) / (double)TimeSpan.TicksPerSecond)
                : _latestXSeconds;

            if (xSeconds < _latestXSeconds)
            {
                Reset();
                EnsureSampleLists(settings.MaxSamples);
                _startTimestampTicks = physioTimestampTicksUtc;
                xSeconds = 0f;
            }

            _latestXSeconds = xSeconds;
            for (int i = 0; i < ChannelCount; i++)
            {
                _samples[i].Add(new Vector2(xSeconds, _candidateValues[i]));
                _lastValues[i] = _candidateValues[i];
                PruneSamples(_samples[i], xSeconds, settings);
            }

            _lastSampleTimestampTicks = physioTimestampTicksUtc;
            _hasLastSample = true;
        }

        private bool HasSameValues()
        {
            for (int i = 0; i < ChannelCount; i++)
            {
                if (!Mathf.Approximately(_lastValues[i], _candidateValues[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static void PruneSamples(List<Vector2> samples, float latestXSeconds, ParticleMeshPhysioAmplificationSettings settings)
        {
            float cutoff = latestXSeconds - settings.WindowSeconds;
            int removeCount = 0;
            while (removeCount < samples.Count && samples[removeCount].x < cutoff)
            {
                removeCount++;
            }

            if (removeCount > 0)
            {
                samples.RemoveRange(0, removeCount);
            }

            int extraCount = samples.Count - settings.MaxSamples;
            if (extraCount > 0)
            {
                samples.RemoveRange(0, extraCount);
            }
        }

        private float AmplifyChannel(int channelIndex, float value, ParticleMeshPhysioAmplificationSettings settings)
        {
            List<Vector2> channelSamples = _samples[channelIndex];
            if (channelSamples == null || channelSamples.Count < 2)
            {
                return value;
            }

            float sum = 0f;
            float sumSquares = 0f;
            int count = channelSamples.Count;
            for (int i = 0; i < count; i++)
            {
                float sampleValue = channelSamples[i].y;
                sum += sampleValue;
                sumSquares += sampleValue * sampleValue;
            }

            float mean = sum / count;
            float variance = Mathf.Max(0f, (sumSquares / count) - mean * mean);
            float stdDev = Mathf.Sqrt(variance);
            float targetRange = Mathf.Max(settings.MinRange, stdDev * settings.StdDevMultiplier * 2f);
            if (targetRange >= HardRange)
            {
                return value;
            }

            float halfRange = targetRange * 0.5f;
            float targetMin = mean - halfRange;
            float targetMax = mean + halfRange;

            if (targetMin < HardMin)
            {
                float shift = HardMin - targetMin;
                targetMin += shift;
                targetMax += shift;
            }

            if (targetMax > HardMax)
            {
                float shift = targetMax - HardMax;
                targetMin -= shift;
                targetMax -= shift;
            }

            targetMin = Mathf.Clamp(targetMin, HardMin, HardMax);
            targetMax = Mathf.Clamp(targetMax, HardMin, HardMax);
            NormalizeRange(ref targetMin, ref targetMax);

            if (!_hasDynamicRange[channelIndex] || settings.Smoothing <= 0f)
            {
                _currentMin[channelIndex] = targetMin;
                _currentMax[channelIndex] = targetMax;
                _hasDynamicRange[channelIndex] = true;
            }
            else
            {
                _currentMin[channelIndex] = Mathf.Lerp(_currentMin[channelIndex], targetMin, settings.Smoothing);
                _currentMax[channelIndex] = Mathf.Lerp(_currentMax[channelIndex], targetMax, settings.Smoothing);
            }

            float yMin = Mathf.Clamp(_currentMin[channelIndex], HardMin, HardMax);
            float yMax = Mathf.Clamp(_currentMax[channelIndex], HardMin, HardMax);
            NormalizeRange(ref yMin, ref yMax);

            float amplified = Mathf.Lerp(HardMin, HardMax, Mathf.InverseLerp(yMin, yMax, value));
            return Mathf.Clamp(Mathf.Lerp(value, amplified, settings.Blend), HardMin, HardMax);
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
