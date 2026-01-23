using AmpPortableDataViz.Core;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Visualization
{

    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class GraphVisualizer : MonoBehaviour, IVisualizer<GraphParams>
    {
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private Vector2 graphSize = new Vector2(1f, 1f);
        [SerializeField] private bool clampToBounds = true;

        private Vector3[] _positions;

        private void Awake()
        {
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }

            if (lineRenderer != null)
            {
                lineRenderer.useWorldSpace = false;
            }
        }

        public void Apply(in GraphParams parameters, long timestampTicksUtc)
        {
            if (lineRenderer == null)
            {
                return;
            }

            var points = parameters.dataPoints;
            if (points == null || points.Length == 0)
            {
                lineRenderer.positionCount = 0;
                return;
            }

            float xRange = parameters.xMax - parameters.xMin;
            float yRange = parameters.yMax - parameters.yMin;
            if (Mathf.Abs(xRange) < 1e-5f)
            {
                xRange = 1f;
            }

            if (Mathf.Abs(yRange) < 1e-5f)
            {
                yRange = 1f;
            }

            int count = points.Length;
            if (_positions == null || _positions.Length != count)
            {
                _positions = new Vector3[count];
            }

            float halfWidth = graphSize.x * 0.5f;
            float halfHeight = graphSize.y * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float nx = (points[i].x - parameters.xMin) / xRange;
                float ny = (points[i].y - parameters.yMin) / yRange;

                if (clampToBounds)
                {
                    nx = Mathf.Clamp01(nx);
                    ny = Mathf.Clamp01(ny);
                }

                float x = Mathf.Lerp(-halfWidth, halfWidth, nx);
                float y = Mathf.Lerp(-halfHeight, halfHeight, ny);
                _positions[i] = new Vector3(x, y, 0f);
            }

            lineRenderer.positionCount = count;
            lineRenderer.startColor = parameters.lineColor;
            lineRenderer.endColor = parameters.lineColor;

            float width = Mathf.Max(0f, parameters.lineWidth);
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;

            lineRenderer.SetPositions(_positions);
        }

    }
}
