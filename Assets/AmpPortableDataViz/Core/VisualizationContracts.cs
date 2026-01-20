using UnityEngine;

namespace AmpPortableDataViz.Core
{
    /// <summary>
    /// Parameters that drive the simple visualization shader/material.
    /// </summary>
    public struct SimpleVisualParams
    {
        public float Intensity;
        public Color Color;
        public Vector3 Flow;
    }

    /// <summary>
    /// Parameters for Graph visualization
    /// </summary>
    public struct GraphParams
    {
        float xMin;
        float xMax;
        float yMin;
        float yMax;
        Color lineColor;
        float lineWidth;
        Vector2[] dataPoints;
    }
}

