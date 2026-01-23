using System;
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

    /// <summary>
    /// Snapshot of a graph series with bounds and points.
    /// </summary>
    public readonly struct GraphSeriesSample
    {
        public readonly float XMin;
        public readonly float XMax;
        public readonly float YMin;
        public readonly float YMax;
        public readonly Vector2[] Points;

        public GraphSeriesSample(float xMin, float xMax, float yMin, float yMax, Vector2[] points)
        {
            XMin = xMin;
            XMax = xMax;
            YMin = yMin;
            YMax = yMax;
            Points = points ?? Array.Empty<Vector2>();
        }
    }
}
