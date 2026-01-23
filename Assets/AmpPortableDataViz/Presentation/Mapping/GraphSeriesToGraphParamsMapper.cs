using AmpPortableDataViz.Core;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Mapping
{
    public sealed class GraphSeriesToGraphParamsMapper : IMapper<GraphSeriesSample, GraphParams>
    {
        [System.Serializable]
        public struct Settings
        {
            public Color LineColor;
            public float LineWidth;
        }

        public Settings CurrentSettings;

        public GraphSeriesToGraphParamsMapper(Settings settings)
        {
            CurrentSettings = settings;
        }

        public GraphParams Map(in DataFrame<GraphSeriesSample> inputFrame)
        {
            var payload = inputFrame.Payload;
            return new GraphParams
            {
                xMin = payload.XMin,
                xMax = payload.XMax,
                yMin = payload.YMin,
                yMax = payload.YMax,
                lineColor = CurrentSettings.LineColor,
                lineWidth = CurrentSettings.LineWidth,
                dataPoints = payload.Points
            };
        }
    }
}
