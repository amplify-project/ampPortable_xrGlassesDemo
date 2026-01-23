using System;
using System.Collections.Generic;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Presentation.Mapping;
using AmpPortableDataViz.Presentation.Sources;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Visualization
{
    /// <summary>
    /// Binds Redis float streams to graph line renderers (heart rate, valence, arousal).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GraphBinding : MonoBehaviour
    {
        public enum LayoutMode
        {
            Overlay,
            Stacked
        }

        [Serializable]
        private struct StreamBinding
        {
            public string Label;
            public RedisDataPump Source;
            public GraphVisualizer Visualizer;
            public Vector2 YRange;
            public Color LineColor;
            [Range(0.0001f, 0.05f)] public float LineWidth;

            public string ResolveLabel()
            {
                return string.IsNullOrWhiteSpace(Label) ? "Graph" : Label.Trim();
            }
        }

        [Header("Streams")]
        [SerializeField] private StreamBinding heartRate = new StreamBinding
        {
            Label = "Heart Rate",
            YRange = new Vector2(30f, 200f),
            LineColor = new Color(1f, 0.3f, 0.6f),
            LineWidth = 0.01f
        };

        [SerializeField] private StreamBinding valence = new StreamBinding
        {
            Label = "Valence",
            YRange = new Vector2(0f, 2f),
            LineColor = new Color(0.25f, 0.9f, 1f),
            LineWidth = 0.01f
        };

        [SerializeField] private StreamBinding arousal = new StreamBinding
        {
            Label = "Arousal",
            YRange = new Vector2(0f, 2f),
            LineColor = new Color(1f, 0.8f, 0.2f),
            LineWidth = 0.01f
        };

        [Header("Layout")]
        [SerializeField] private LayoutMode layoutMode = LayoutMode.Overlay;
        [SerializeField] private Vector3 overlayLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 overlayLocalScale = Vector3.one;
        [SerializeField] private Vector3 stackedBaseLocalPosition = Vector3.zero;
        [SerializeField] private float stackedVerticalSpacing = 0.6f;
        [SerializeField] private Vector3 stackedLocalScale = new Vector3(1f, 0.35f, 1f);

        [Header("Window")]
        [SerializeField, Range(0f, 120f)] private float windowSeconds = 10f;
        [SerializeField, Range(16, 4096)] private int maxSamples = 512;

        private readonly List<Vector2> _heartRateSamples = new List<Vector2>();
        private readonly List<Vector2> _valenceSamples = new List<Vector2>();
        private readonly List<Vector2> _arousalSamples = new List<Vector2>();

        private GraphSeriesToGraphParamsMapper _heartRateMapper;
        private GraphSeriesToGraphParamsMapper _valenceMapper;
        private GraphSeriesToGraphParamsMapper _arousalMapper;

        private Action<DataFrame<float>> _heartRateHandler;
        private Action<DataFrame<float>> _valenceHandler;
        private Action<DataFrame<float>> _arousalHandler;

        private int _heartRateSequenceId;
        private int _valenceSequenceId;
        private int _arousalSequenceId;

        private long _startTimestampTicks;
        private float _latestXSeconds;

        private void Awake()
        {
            EnsureMappers();
            ApplyLayout();
        }

        private void OnEnable()
        {
            ResetState();
            EnsureSampleCapacity();
            AttachSources();
            ApplyLayout();
            WarnIfMissingVisualizers();
        }

        private void OnDisable()
        {
            DetachSources();
            ResetState();
        }

        private void OnValidate()
        {
            EnsureMappers();
            UpdateMapperSettings();
            ApplyLayout();
        }

        public void SetLayoutMode(LayoutMode mode)
        {
            layoutMode = mode;
            ApplyLayout();
        }

        public void ApplyLayout()
        {
            if (layoutMode == LayoutMode.Overlay)
            {
                SetTransform(heartRate.Visualizer?.transform, overlayLocalPosition, overlayLocalScale);
                SetTransform(valence.Visualizer?.transform, overlayLocalPosition, overlayLocalScale);
                SetTransform(arousal.Visualizer?.transform, overlayLocalPosition, overlayLocalScale);
                return;
            }

            var up = Vector3.up * stackedVerticalSpacing;
            SetTransform(heartRate.Visualizer?.transform, stackedBaseLocalPosition + up, stackedLocalScale);
            SetTransform(valence.Visualizer?.transform, stackedBaseLocalPosition, stackedLocalScale);
            SetTransform(arousal.Visualizer?.transform, stackedBaseLocalPosition - up, stackedLocalScale);
        }

        private static void SetTransform(Transform target, Vector3 localPosition, Vector3 localScale)
        {
            if (target == null)
            {
                return;
            }

            target.localPosition = localPosition;
            target.localScale = localScale;
        }

        private void WarnIfMissingVisualizers()
        {
            WarnIfMissingVisualizer(heartRate);
            WarnIfMissingVisualizer(valence);
            WarnIfMissingVisualizer(arousal);
        }

        private void WarnIfMissingVisualizer(StreamBinding binding)
        {
            if (binding.Visualizer == null)
            {
                Debug.LogWarning($"GraphBinding[{name}] missing {binding.ResolveLabel()} GraphVisualizer reference.", this);
            }
        }

        private void AttachSources()
        {
            SubscribeToPump(heartRate, ref _heartRateHandler, OnHeartRateFrame);
            SubscribeToPump(valence, ref _valenceHandler, OnValenceFrame);
            SubscribeToPump(arousal, ref _arousalHandler, OnArousalFrame);
        }

        private void DetachSources()
        {
            UnsubscribeFromPump(heartRate, ref _heartRateHandler);
            UnsubscribeFromPump(valence, ref _valenceHandler);
            UnsubscribeFromPump(arousal, ref _arousalHandler);
        }

        private void SubscribeToPump(StreamBinding binding, ref Action<DataFrame<float>> handler, Action<DataFrame<float>> callback)
        {
            if (binding.Source == null)
            {
                Debug.LogWarning($"GraphBinding[{name}] missing {binding.ResolveLabel()} RedisDataPump reference.", this);
                return;
            }

            handler ??= callback;
            binding.Source.OnFrame += handler;
        }

        private void UnsubscribeFromPump(StreamBinding binding, ref Action<DataFrame<float>> handler)
        {
            if (binding.Source != null && handler != null)
            {
                binding.Source.OnFrame -= handler;
            }

            handler = null;
        }

        private void OnHeartRateFrame(DataFrame<float> frame)
        {
            HandleFrame(frame, _heartRateSamples);
        }

        private void OnValenceFrame(DataFrame<float> frame)
        {
            HandleFrame(frame, _valenceSamples);
        }

        private void OnArousalFrame(DataFrame<float> frame)
        {
            HandleFrame(frame, _arousalSamples);
        }

        private void HandleFrame(DataFrame<float> frame, List<Vector2> samples)
        {
            if (_startTimestampTicks == 0)
            {
                _startTimestampTicks = frame.TimestampTicksUtc;
            }

            float xSeconds = (float)((frame.TimestampTicksUtc - _startTimestampTicks) / (double)TimeSpan.TicksPerSecond);
            samples.Add(new Vector2(xSeconds, frame.Payload));

            if (xSeconds > _latestXSeconds)
            {
                _latestXSeconds = xSeconds;
            }

            PruneAllStreams();
            UpdateAllGraphs(frame.TimestampTicksUtc);
        }

        private void UpdateAllGraphs(long timestampTicksUtc)
        {
            float xMax = _latestXSeconds;
            float xMin = windowSeconds > 0f ? Mathf.Max(0f, xMax - windowSeconds) : 0f;
            NormalizeRange(ref xMin, ref xMax);

            ApplyStream(heartRate, _heartRateSamples, _heartRateMapper, ref _heartRateSequenceId, xMin, xMax, timestampTicksUtc);
            ApplyStream(valence, _valenceSamples, _valenceMapper, ref _valenceSequenceId, xMin, xMax, timestampTicksUtc);
            ApplyStream(arousal, _arousalSamples, _arousalMapper, ref _arousalSequenceId, xMin, xMax, timestampTicksUtc);
        }

        private void ApplyStream(
            StreamBinding binding,
            List<Vector2> samples,
            GraphSeriesToGraphParamsMapper mapper,
            ref int sequenceId,
            float xMin,
            float xMax,
            long timestampTicksUtc)
        {
            if (binding.Visualizer == null || mapper == null)
            {
                return;
            }

            mapper.CurrentSettings = MakeSettings(binding);

            float yMin = binding.YRange.x;
            float yMax = binding.YRange.y;
            NormalizeRange(ref yMin, ref yMax);

            Vector2[] points = samples.Count == 0 ? Array.Empty<Vector2>() : samples.ToArray();
            var series = new GraphSeriesSample(xMin, xMax, yMin, yMax, points);
            var frame = new DataFrame<GraphSeriesSample>(timestampTicksUtc, sequenceId++, series);
            GraphParams parameters = mapper.Map(in frame);
            binding.Visualizer.Apply(parameters, frame.TimestampTicksUtc);
        }

        private void PruneAllStreams()
        {
            if (windowSeconds > 0f)
            {
                float cutoff = _latestXSeconds - windowSeconds;
                PruneSamples(_heartRateSamples, cutoff);
                PruneSamples(_valenceSamples, cutoff);
                PruneSamples(_arousalSamples, cutoff);
            }

            TrimSamples(_heartRateSamples);
            TrimSamples(_valenceSamples);
            TrimSamples(_arousalSamples);
        }

        private static void PruneSamples(List<Vector2> samples, float cutoff)
        {
            if (samples.Count == 0)
            {
                return;
            }

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

        private void TrimSamples(List<Vector2> samples)
        {
            if (maxSamples <= 0)
            {
                return;
            }

            int removeCount = samples.Count - maxSamples;
            if (removeCount > 0)
            {
                samples.RemoveRange(0, removeCount);
            }
        }

        private void ResetState()
        {
            _startTimestampTicks = 0;
            _latestXSeconds = 0f;
            _heartRateSequenceId = 0;
            _valenceSequenceId = 0;
            _arousalSequenceId = 0;
            _heartRateSamples.Clear();
            _valenceSamples.Clear();
            _arousalSamples.Clear();
        }

        private void EnsureSampleCapacity()
        {
            if (maxSamples <= 0)
            {
                return;
            }

            EnsureCapacity(_heartRateSamples, maxSamples);
            EnsureCapacity(_valenceSamples, maxSamples);
            EnsureCapacity(_arousalSamples, maxSamples);
        }

        private static void EnsureCapacity(List<Vector2> samples, int capacity)
        {
            if (samples.Capacity < capacity)
            {
                samples.Capacity = capacity;
            }
        }

        private void EnsureMappers()
        {
            _heartRateMapper ??= CreateMapper(heartRate);
            _valenceMapper ??= CreateMapper(valence);
            _arousalMapper ??= CreateMapper(arousal);
        }

        private void UpdateMapperSettings()
        {
            if (_heartRateMapper != null)
            {
                _heartRateMapper.CurrentSettings = MakeSettings(heartRate);
            }

            if (_valenceMapper != null)
            {
                _valenceMapper.CurrentSettings = MakeSettings(valence);
            }

            if (_arousalMapper != null)
            {
                _arousalMapper.CurrentSettings = MakeSettings(arousal);
            }
        }

        private static GraphSeriesToGraphParamsMapper CreateMapper(StreamBinding binding)
        {
            return new GraphSeriesToGraphParamsMapper(MakeSettings(binding));
        }

        private static GraphSeriesToGraphParamsMapper.Settings MakeSettings(StreamBinding binding)
        {
            return new GraphSeriesToGraphParamsMapper.Settings
            {
                LineColor = binding.LineColor,
                LineWidth = Mathf.Max(0f, binding.LineWidth)
            };
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
