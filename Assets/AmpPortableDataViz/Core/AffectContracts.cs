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
        public readonly float ValenceLevel;
        public readonly float ArousalLevel;

        public ArousalValenceSample(string deviceId, float valenceLevel, float arousalLevel)
        {
            DeviceId = deviceId ?? string.Empty;
            ValenceLevel = Mathf.Clamp(valenceLevel, 0f, 2f);
            ArousalLevel = Mathf.Clamp(arousalLevel, 0f, 2f);
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

    /// <summary>
    /// Snapshot of all parameters exposed by the PlasmaMass raymarch shader.
    /// </summary>
    [Serializable]
    public struct RaymarchPlasmaParams
    {
        public Color ColorInner;
        public Color ColorOuter;

        public float BoundsRadius;
        public float StepSize;
        public float PlasmaScale;
        public float WarpStrength;
        public float FlowSpeed;

        public float DensityGain;
        public float DensityThreshold;
        public float DensityPower;

        public float Absorption;
        public float Falloff;
        public float Emission;

        public float HaloPulseSpeed;
        public float HaloPulseAmplitude;
    }
}
