using System;
using AmpPortableDataViz.Core;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Visualization
{
    [ExecuteAlways]
    [RequireComponent(typeof(GraphVisualizer))]
    public sealed class GraphManualDriver : MonoBehaviour
    {
        public enum SourceMode
        {
            PerlinNoise,
            ManualPoints
        }

        [Header("Target")]
        [SerializeField] private GraphVisualizer targetVisualizer;

        [Header("Graph Defaults")]
        [SerializeField] private Vector2 xRange = new Vector2(0f, 10f);
        [SerializeField] private Vector2 yRange = new Vector2(-3f, 3f);
        [SerializeField] private Color lineColor = Color.white;
        [SerializeField, Range(0.0005f, 0.05f)] private float lineWidth = 0.01f;

        [Header("Driver")]
        [SerializeField] private SourceMode sourceMode = SourceMode.PerlinNoise;
        [SerializeField] private bool autoApply = true;
        [SerializeField] private bool animate = true;
        [SerializeField, Range(0f, 5f)] private float timeScale = 0.35f;

        [Header("Perlin Noise")]
        [SerializeField, Range(2, 2048)] private int sampleCount = 128;
        [SerializeField, Range(0.05f, 10f)] private float noiseFrequency = 0.75f;
        [SerializeField, Range(0f, 2f)] private float noiseAmplitude = 1f;
        [SerializeField] private float noiseSeed = 0.1234f;
        [SerializeField] private float noiseOffset = 0f;
        [SerializeField] private bool clampNoiseToRange = true;

        [Header("Manual Points")]
        [SerializeField] private Vector2[] manualPoints = Array.Empty<Vector2>();
        [SerializeField] private bool autoSortByX = true;
        [SerializeField] private bool autoBoundsFromPoints = false;

        private GraphVisualizer _visualizer;
        private Vector2[] _sortedBuffer;

        private void Awake()
        {
            if (targetVisualizer == null)
            {
                targetVisualizer = GetComponent<GraphVisualizer>();
            }

            _visualizer = targetVisualizer;
        }

        private void OnEnable()
        {
            if (autoApply)
            {
                ApplyNow();
            }
        }

        private void OnValidate()
        {
            if (autoApply)
            {
                ApplyNow();
            }
        }

        private void Update()
        {
            if (!autoApply)
            {
                return;
            }

            if (animate && sourceMode == SourceMode.PerlinNoise)
            {
                noiseOffset += Time.deltaTime * timeScale;
            }

            ApplyNow();
        }

        [ContextMenu("Apply Now")]
        public void ApplyNow()
        {
            var viz = targetVisualizer != null ? targetVisualizer : _visualizer;
            if (viz == null)
            {
                return;
            }

            Vector2[] points;
            float xMin;
            float xMax;
            float yMin;
            float yMax;

            if (sourceMode == SourceMode.PerlinNoise)
            {
                points = BuildPerlinPoints(out xMin, out xMax, out yMin, out yMax);
            }
            else
            {
                points = BuildManualPoints(out xMin, out xMax, out yMin, out yMax);
            }

            var parameters = new GraphParams
            {
                xMin = xMin,
                xMax = xMax,
                yMin = yMin,
                yMax = yMax,
                lineColor = lineColor,
                lineWidth = lineWidth,
                dataPoints = points
            };

            viz.Apply(parameters, DateTime.UtcNow.Ticks);
        }

        private Vector2[] BuildPerlinPoints(out float xMin, out float xMax, out float yMin, out float yMax)
        {
            xMin = xRange.x;
            xMax = xRange.y;
            NormalizeRange(ref xMin, ref xMax);

            yMin = yRange.x;
            yMax = yRange.y;
            NormalizeRange(ref yMin, ref yMax);

            int count = Mathf.Max(2, sampleCount);
            var points = new Vector2[count];

            float halfYRange = (yMax - yMin) * 0.5f;
            float yCenter = (yMin + yMax) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0f : (float)i / (count - 1);
                float x = Mathf.Lerp(xMin, xMax, t);
                float perlinX = x * noiseFrequency + noiseOffset;
                float noise = Mathf.PerlinNoise(perlinX, noiseSeed);
                float signedNoise = noise * 2f - 1f;
                float y = yCenter + signedNoise * halfYRange * noiseAmplitude;

                if (clampNoiseToRange)
                {
                    y = Mathf.Clamp(y, yMin, yMax);
                }

                points[i] = new Vector2(x, y);
            }

            return points;
        }

        private Vector2[] BuildManualPoints(out float xMin, out float xMax, out float yMin, out float yMax)
        {
            Vector2[] source = manualPoints ?? Array.Empty<Vector2>();
            Vector2[] points = source;

            if (autoSortByX && source.Length > 1)
            {
                if (_sortedBuffer == null || _sortedBuffer.Length != source.Length)
                {
                    _sortedBuffer = new Vector2[source.Length];
                }

                Array.Copy(source, _sortedBuffer, source.Length);
                Array.Sort(_sortedBuffer, (a, b) => a.x.CompareTo(b.x));
                points = _sortedBuffer;
            }

            if (autoBoundsFromPoints)
            {
                ComputeBounds(points, out xMin, out xMax, out yMin, out yMax);
            }
            else
            {
                xMin = xRange.x;
                xMax = xRange.y;
                yMin = yRange.x;
                yMax = yRange.y;
                NormalizeRange(ref xMin, ref xMax);
                NormalizeRange(ref yMin, ref yMax);
            }

            return points.Length == 0 ? Array.Empty<Vector2>() : points;
        }

        private static void ComputeBounds(Vector2[] points, out float xMin, out float xMax, out float yMin, out float yMax)
        {
            if (points == null || points.Length == 0)
            {
                xMin = 0f;
                xMax = 1f;
                yMin = 0f;
                yMax = 1f;
                return;
            }

            xMin = points[0].x;
            xMax = points[0].x;
            yMin = points[0].y;
            yMax = points[0].y;

            for (int i = 1; i < points.Length; i++)
            {
                var point = points[i];
                if (point.x < xMin) xMin = point.x;
                if (point.x > xMax) xMax = point.x;
                if (point.y < yMin) yMin = point.y;
                if (point.y > yMax) yMax = point.y;
            }

            NormalizeRange(ref xMin, ref xMax);
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
