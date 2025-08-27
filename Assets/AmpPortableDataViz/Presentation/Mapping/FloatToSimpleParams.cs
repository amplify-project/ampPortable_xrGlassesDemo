using AmpPortableDataViz.Core;
using AmpPortableDataViz.Presentation.Visualization;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Mapping
{
    public sealed class FloatToSimpleParams : IMapper<float, SimpleVisualParams>
    {
        [System.Serializable]
        public struct Settings
        {
            public float Scale;
            public Vector2 ClampRange;
            [Range(0f, 1f)] public float HueMin;
            [Range(0f, 1f)] public float HueMax;
        }

        private readonly Settings _settings;
        public FloatToSimpleParams(Settings settings) => _settings = settings;

        public SimpleVisualParams Map(in DataFrame<float> inputFrame)
        {
            float scaledValue = inputFrame.Payload * _settings.Scale;
            float clampedValue = Mathf.Clamp(scaledValue, _settings.ClampRange.x, _settings.ClampRange.y);
            float hueValue = Mathf.Lerp(_settings.HueMin, _settings.HueMax, clampedValue);
            Color colorRgb = Color.HSVToRGB(hueValue, 0.9f, Mathf.Lerp(0.3f, 1f, clampedValue));

            Vector3 flowVector = new Vector3(
                0f,
                Mathf.Sin((float)(inputFrame.TimestampTicksUtc % 1_000_000) * 0.00001f),
                clampedValue
            );

            return new SimpleVisualParams
            {
                Intensity = clampedValue,
                Color = colorRgb,
                Flow = flowVector
            };
        }
    }
}
