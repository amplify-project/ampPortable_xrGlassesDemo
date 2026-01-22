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
        public float xMin;
        public float xMax;
        public float yMin;
        public float yMax;
        public Color lineColor;
        public float lineWidth;
        public Vector2[] dataPoints;
    }
}

