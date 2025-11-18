using System;
using UnityEngine;

namespace AmpPortableDataViz.Core
{
    /// <summary>
    /// Combined affective sample emitted once both valence and arousal readings are available.
    /// </summary>
    [Serializable]
    public readonly struct ArousalValenceSample
    {
        public readonly string DeviceId;
        public readonly int ValenceLevel;
        public readonly int ArousalLevel;

        public ArousalValenceSample(string deviceId, int valenceLevel, int arousalLevel)
        {
            DeviceId = deviceId ?? string.Empty;
            ValenceLevel = Mathf.Clamp(valenceLevel, 0, 2);
            ArousalLevel = Mathf.Clamp(arousalLevel, 0, 2);
        }

        public override string ToString()
        {
            return $"Device={DeviceId}, Valence={ValenceLevel}, Arousal={ArousalLevel}";
        }
    }

    /// <summary>
    /// Snapshot of all parameters that drive the EmotionPlasma shader.
    /// </summary>
    [Serializable]
    public struct EmotionPlasmaParams
    {
        public Color BaseColor;
        public Color InnerColor;
        public Color OuterColor;

        public float Brightness;
        public float Saturation;

        public float Curvature;
        public float Asymmetry;
        public float SurfaceSharpness;

        public float FlowSpeed;
        public float Turbulence;
        public float PulseFrequency;
        public float PulseAmplitude;

        public float Smoothness;
        public float NoiseScale;
        public float NoiseContrast;
    }
}
