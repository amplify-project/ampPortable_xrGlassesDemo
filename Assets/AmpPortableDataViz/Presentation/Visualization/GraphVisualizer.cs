using AmpPortableDataViz.Core;
using TMPro;
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
        [Header("Value Label")]
        [SerializeField] private bool showValueLabel = true;
        [SerializeField] private TextMeshPro valueLabel;
        [SerializeField] private Vector3 valueLabelOffset = new Vector3(0.08f, 0f, 0f);
        [SerializeField] private float valueLabelFontSize = 0.16f;
        [SerializeField] private Color valueLabelColor = Color.white;
        [SerializeField] private bool valueLabelUseLineColor = true;
        [SerializeField] private string valueLabelFormat = "0.00";
        [SerializeField] private bool valueLabelBillboard = true;

        private Vector3[] _positions;
        public Vector2 GraphSize => graphSize;
        private Vector2 _baseGraphSize;
        private string _valueLabelFormatString;

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

            _baseGraphSize = graphSize;
            UpdateValueLabelFormatCache();
            if (showValueLabel)
            {
                EnsureValueLabel();
            }
        }

        private void OnValidate()
        {
            _baseGraphSize = graphSize;
            UpdateValueLabelFormatCache();
            ApplyValueLabelStyle();
            if (!showValueLabel)
            {
                HideValueLabel();
            }
        }

        public void SetGraphSizeYScale(float scale)
        {
            if (_baseGraphSize.sqrMagnitude < 1e-6f)
            {
                _baseGraphSize = graphSize;
            }

            float clamped = Mathf.Max(0.01f, scale);
            graphSize = new Vector2(_baseGraphSize.x, _baseGraphSize.y * clamped);
        }

        public void Apply(in GraphParams parameters, long timestampTicksUtc)
        {
            if (lineRenderer == null)
            {
                return;
            }
            if (lineRenderer.useWorldSpace)
            {
                lineRenderer.useWorldSpace = false;
            }

            var points = parameters.dataPoints;
            if (points == null || points.Length == 0)
            {
                lineRenderer.positionCount = 0;
                HideValueLabel();
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
            UpdateValueLabel(parameters, count);
        }

        private void UpdateValueLabel(in GraphParams parameters, int count)
        {
            if (!showValueLabel)
            {
                HideValueLabel();
                return;
            }

            var label = EnsureValueLabel();
            if (label == null || _positions == null || _positions.Length < count)
            {
                return;
            }

            label.gameObject.SetActive(true);

            float halfWidth = graphSize.x * 0.5f;
            float latestYLocal = _positions[count - 1].y;
            Vector3 anchor = new Vector3(halfWidth, latestYLocal, 0f);
            label.transform.localPosition = anchor + valueLabelOffset;

            float value = parameters.dataPoints[count - 1].y;
            if (string.IsNullOrWhiteSpace(_valueLabelFormatString))
            {
                label.text = value.ToString("0.00");
            }
            else
            {
                label.SetText(_valueLabelFormatString, value);
            }

            if (valueLabelUseLineColor)
            {
                var color = parameters.lineColor;
                color.a *= valueLabelColor.a;
                label.color = color;
            }
            else
            {
                label.color = valueLabelColor;
            }

            label.fontSize = Mathf.Max(0.01f, valueLabelFontSize);
        }

        private TextMeshPro EnsureValueLabel()
        {
            if (valueLabel != null)
            {
                return valueLabel;
            }

            if (!showValueLabel)
            {
                return null;
            }

            var labelObject = new GameObject("GraphValueLabel");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localRotation = Quaternion.identity;
            labelObject.transform.localScale = Vector3.one;
            labelObject.layer = gameObject.layer;

            valueLabel = labelObject.AddComponent<TextMeshPro>();
            ApplyValueLabelStyle();

            if (valueLabelBillboard)
            {
                labelObject.AddComponent<BillboardLabel>();
            }

            return valueLabel;
        }

        private void ApplyValueLabelStyle()
        {
            if (valueLabel == null)
            {
                return;
            }

            valueLabel.fontSize = Mathf.Max(0.01f, valueLabelFontSize);
            valueLabel.color = valueLabelColor;
            valueLabel.alignment = TextAlignmentOptions.Left;
            valueLabel.enableWordWrapping = false;
            valueLabel.richText = false;
            var rect = valueLabel.rectTransform;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
        }

        private void HideValueLabel()
        {
            if (valueLabel != null)
            {
                valueLabel.gameObject.SetActive(false);
            }
        }

        private void UpdateValueLabelFormatCache()
        {
            string format = string.IsNullOrWhiteSpace(valueLabelFormat) ? "0.00" : valueLabelFormat.Trim();
            _valueLabelFormatString = "{0:" + format + "}";
        }

    }
}
