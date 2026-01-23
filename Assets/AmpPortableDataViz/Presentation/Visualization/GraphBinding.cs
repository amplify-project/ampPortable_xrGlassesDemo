using System;
using System.Collections.Generic;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Presentation.Mapping;
using AmpPortableDataViz.Presentation.Sources;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Visualization
{
    /// <summary>
    /// Binds a Redis float stream to a graph line renderer.
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

        [Header("Stream")]
        [SerializeField] private StreamBinding stream = new StreamBinding
        {
            Label = "Graph Stream",
            YRange = new Vector2(0f, 1f),
            LineColor = Color.white,
            LineWidth = 0.01f
        };

        [Header("Layout")]
        [SerializeField] private LayoutMode layoutMode = LayoutMode.Overlay;
        [SerializeField] private Vector3 overlayLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 overlayLocalScale = Vector3.one;
        [SerializeField] private Vector3 stackedLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 stackedLocalScale = Vector3.one;

        [Header("Window")]
        [SerializeField, Range(0f, 120f)] private float windowSeconds = 10f;
        [SerializeField, Range(16, 4096)] private int maxSamples = 512;

        private readonly List<Vector2> _samples = new List<Vector2>();

        private GraphSeriesToGraphParamsMapper _mapper;

        private Action<DataFrame<float>> _handler;

        private int _sequenceId;

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
            WarnIfMissingVisualizer();
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

        public void ConfigureSource(
            RedisDataPump source,
            GraphVisualizer visualizer = null,
            string label = null,
            Vector2? yRangeOverride = null,
            Color? lineColorOverride = null,
            float? lineWidthOverride = null)
        {
            bool wasEnabled = isActiveAndEnabled;
            if (wasEnabled)
            {
                DetachSources();
            }

            stream.Source = source;
            if (visualizer != null)
            {
                stream.Visualizer = visualizer;
            }

            if (!string.IsNullOrWhiteSpace(label))
            {
                stream.Label = label.Trim();
            }

            if (yRangeOverride.HasValue)
            {
                stream.YRange = yRangeOverride.Value;
            }

            if (lineColorOverride.HasValue)
            {
                stream.LineColor = lineColorOverride.Value;
            }

            if (lineWidthOverride.HasValue)
            {
                stream.LineWidth = Mathf.Max(0f, lineWidthOverride.Value);
            }

            UpdateMapperSettings();
            ApplyLayout();

            if (wasEnabled)
            {
                ResetState();
                AttachSources();
            }
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
                SetTransform(stream.Visualizer?.transform, overlayLocalPosition, overlayLocalScale);
                return;
            }

            SetTransform(stream.Visualizer?.transform, stackedLocalPosition, stackedLocalScale);
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

        private void WarnIfMissingVisualizer()
        {
            if (stream.Visualizer == null)
            {
                Debug.LogWarning($"GraphBinding[{name}] missing {stream.ResolveLabel()} GraphVisualizer reference.", this);
            }
        }

        private void AttachSources()
        {
            SubscribeToPump(stream, ref _handler, OnFrame);
        }

        private void DetachSources()
        {
            UnsubscribeFromPump(stream, ref _handler);
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

        private void OnFrame(DataFrame<float> frame)
        {
            if (_startTimestampTicks == 0)
            {
                _startTimestampTicks = frame.TimestampTicksUtc;
            }

            float xSeconds = (float)((frame.TimestampTicksUtc - _startTimestampTicks) / (double)TimeSpan.TicksPerSecond);
            _samples.Add(new Vector2(xSeconds, frame.Payload));

            if (xSeconds > _latestXSeconds)
            {
                _latestXSeconds = xSeconds;
            }

            PruneSamples();
            UpdateGraph(frame.TimestampTicksUtc);
        }

        private void UpdateGraph(long timestampTicksUtc)
        {
            float xMax = _latestXSeconds;
            float xMin = windowSeconds > 0f ? Mathf.Max(0f, xMax - windowSeconds) : 0f;
            NormalizeRange(ref xMin, ref xMax);

            ApplyStream(stream, _samples, _mapper, ref _sequenceId, xMin, xMax, timestampTicksUtc);
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

        private void PruneSamples()
        {
            if (windowSeconds > 0f)
            {
                float cutoff = _latestXSeconds - windowSeconds;
                PruneSamples(_samples, cutoff);
            }

            TrimSamples(_samples);
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
            _sequenceId = 0;
            _samples.Clear();
        }

        private void EnsureSampleCapacity()
        {
            if (maxSamples <= 0)
            {
                return;
            }

            EnsureCapacity(_samples, maxSamples);
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
            _mapper ??= CreateMapper(stream);
        }

        private void UpdateMapperSettings()
        {
            if (_mapper != null)
            {
                _mapper.CurrentSettings = MakeSettings(stream);
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
