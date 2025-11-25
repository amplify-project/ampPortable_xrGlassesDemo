using AmpPortableDataViz.Core;
using UnityEngine;

public static class RaymarchPlasmaParamsMapper
{
    /// <summary>
    /// Builds raymarch plasma shader parameters from valence/arousal inputs in the range [0, 2].
    /// </summary>
    public static RaymarchPlasmaParams BuildParams(int valenceLevel, int arousalLevel)
    {
        return BuildParams((float)valenceLevel, (float)arousalLevel, float.NaN);
    }

    /// <summary>
    /// Builds raymarch plasma shader parameters from continuous valence/arousal inputs in the range [0, 2].
    /// </summary>
    public static RaymarchPlasmaParams BuildParams(float valenceLevel, float arousalLevel)
    {
        return BuildParams(valenceLevel, arousalLevel, float.NaN);
    }

    /// <summary>
    /// Builds raymarch plasma shader parameters from continuous valence/arousal inputs in the range [0, 2]
    /// and heart rate in bpm. Heart rate is normalized from [30, 200] bpm to [0, 1] and drives halo pulse speed/amplitude.
    /// </summary>
    public static RaymarchPlasmaParams BuildParams(float valenceLevel, float arousalLevel, float heartRateBpm)
    {
        float vLevel = Mathf.Clamp(valenceLevel, 0f, 2f); // 0 negative, 1 neutral, 2 positive
        float aLevel = Mathf.Clamp(arousalLevel, 0f, 2f); // 0 low, 1 medium, 2 high
        float hrNormalized = NormalizeHeartRate(heartRateBpm);
        float haloPulseSpeed = 0f;
        float haloPulseAmplitude = 0f;
        if (hrNormalized > 0f)
        {
            // Map bpm to angular speed (rad/sec) for the sine wave: speed = 2π * beatsPerSecond.
            float beatsPerSecond = Mathf.Lerp(30f / 60f, 200f / 60f, hrNormalized);
            haloPulseSpeed = beatsPerSecond * Mathf.PI * 2f;
            haloPulseAmplitude = Mathf.Lerp(0.05f, 1f, hrNormalized);
        }

        // Extract directional weights while keeping the discrete 0/1/2 inputs intact.
        float valencePos = Mathf.Clamp01(vLevel - 1f);      // >0 when above neutral
        float negativeValence = Mathf.Clamp01(1f - vLevel); // >0 when below neutral
        float valenceSigned = valencePos - negativeValence; // -1..1 balance for signed effects

        float arousalPos = Mathf.Clamp01(aLevel - 1f);
        float arousalNeg = Mathf.Clamp01(1f - aLevel);
        float arousalSigned = arousalPos - arousalNeg;      // -1..1 balance for signed effects

        float arousalBlend = Mathf.InverseLerp(0f, 2f, aLevel); // 0..1 for arousal-driven lerps

        // ----------------------------------------------------------
        // 1. COLOUR SELECTION (Valence = warm vs cool)
        // ----------------------------------------------------------

        // Inner palette (hostile warm -> magenta -> friendly cool).
        Color innerNegCol = new Color(1.00f, 0.18f, 0.05f);
        Color innerNeuCol = new Color(0.82f, 0.45f, 0.92f);
        Color innerPosCol = new Color(0.05f, 0.87f, 0.95f);

        Color innerBaseValenceColor =
            (valenceSigned < 0f) ? Color.Lerp(innerNeuCol, innerNegCol, negativeValence) :
            (valenceSigned > 0f) ? Color.Lerp(innerNeuCol, innerPosCol,  valencePos) :
                                   innerNeuCol;

        // Arousal modulates brightness of the inner core.
        float innerSatBoost = Mathf.Lerp(0.24f, 0.45f, arousalBlend);
        float innerValBoost = Mathf.Lerp(0.18f, 0.30f, arousalBlend);
        Color saturatedValenceColor = BoostVibrancy(innerBaseValenceColor, innerSatBoost, innerValBoost);

        float brightnessMod = Mathf.Lerp(3.50f, 4.80f, arousalBlend);
        Color innerColor = saturatedValenceColor * brightnessMod;

        // Outer palette is distinct (cool violet -> mint -> amber) to avoid matching the inner hue.
        Color outerNegCol = new Color(0.18f, 0.32f, 0.95f);
        Color outerNeuCol = new Color(0.25f, 0.95f, 0.60f);
        Color outerPosCol = new Color(1.00f, 0.72f, 0.18f);

        Color outerBaseValenceColor =
            (valenceSigned < 0f) ? Color.Lerp(outerNeuCol, outerNegCol, negativeValence) :
            (valenceSigned > 0f) ? Color.Lerp(outerNeuCol, outerPosCol,  valencePos) :
                                   outerNeuCol;

        float outerSatBoost = Mathf.Lerp(0.12f, 0.32f, arousalBlend);
        float outerValBoost = Mathf.Lerp(0.12f, 0.35f, arousalBlend);
        Color outerVivid = BoostVibrancy(outerBaseValenceColor, outerSatBoost, outerValBoost);

        // Give the halo extra lift and slight blend towards the outer palette to keep it soft but distinct.
        float outerGlowLift = Mathf.Lerp(2.18f, 3.42f, arousalBlend);
        Color outerColor = Color.Lerp(outerVivid, Color.white, outerGlowLift * 0.25f);

        // ----------------------------------------------------------
        // 2. EMISSION (major arousal cue)
        // ----------------------------------------------------------

        float emission =
            Mathf.Lerp(3.5f, 6.0f, arousalBlend) +   // much brighter floor at low arousal
            1.2f * arousalNeg +                      // extra lift when arousal is low
            0.6f * valenceSigned;
        emission = Mathf.Clamp(emission, 3.0f, 7.0f);

        // ----------------------------------------------------------
        // 3. MOVEMENT & TURBULENCE
        // ----------------------------------------------------------

        float flowSpeed =
            1.3f +
            3.2f * arousalSigned +    // arousal = main motion driver
            0.6f * negativeValence;   // negative valence = restless
        flowSpeed = Mathf.Clamp(flowSpeed, 1f, 6f);

        float warpStrength =
            1.25f +
            0.6f * arousalSigned +         // high arousal = more spatial wobble
            0.5f * negativeValence;        // negative valence = distorted shape
        warpStrength = Mathf.Clamp(warpStrength, 1.25f, 3f);

        // ----------------------------------------------------------
        // 4. DENSITY / SHAPE CONTRAST
        // ----------------------------------------------------------

        // Harsh negative valence = high contrast clumps; positive = smooth volume.
        float densityGain =
            0.65f +
            0.45f * arousalSigned +
            0.35f * negativeValence;
        densityGain = Mathf.Clamp(densityGain, 0.65f, 2.2f);

        float densityThreshold =
            0.18f +
            0.22f * arousalSigned +        // arousal concentrates bright pockets
            0.18f * negativeValence;
        densityThreshold = Mathf.Clamp(densityThreshold, 0f, 0.65f);

        float densityPower =
            0.9f +
            0.35f * arousalSigned +         // high arousal = strong bright/dark contrast
            0.4f * negativeValence;         // negative valence = harsher transitions
        densityPower = Mathf.Clamp(densityPower, 0.8f, 2.4f);

        // ----------------------------------------------------------
        // 5. TRANSPARENCY & EDGE FEEL
        // ----------------------------------------------------------

        float absorption =
            0.18f +                              // much lighter baseline so low arousal stays visible
            0.12f * negativeValence +
            0.08f * arousalSigned;               // high arousal = denser, low arousal = lighter
        absorption = Mathf.Clamp(absorption, 0.12f, 1.2f);

        float falloff =
            0.35f +
            0.25f * valenceSigned -    // positive soft edge, negative tight edge
            0.05f * arousalSigned;     // high arousal = slightly sharper falloff
        falloff = Mathf.Clamp(falloff, 0.3f, 0.8f);

        // ----------------------------------------------------------
        // 6. VOLUME SCALE
        // ----------------------------------------------------------

        float boundsRadius =
            0.45f +
            0.1f * valenceSigned +   // positive valence feels more open
            0.04f * arousalSigned;   // high arousal slightly expands volume
        boundsRadius = Mathf.Clamp(boundsRadius, 0.4f, 0.75f);

        // Step size: subtle arousal cue (crisper raymarch at high arousal)
        float stepSize =
            0.035f -
            0.01f * arousalSigned;  // high arousal = finer marching steps
        stepSize = Mathf.Clamp(stepSize, 0.005f, 0.06f);

        // PlasmaScale: detail frequency
        float plasmaScale =
            3.0f +
            0.7f * arousalSigned +                  // high arousal = finer detail
            0.5f * negativeValence;                 // negative valence = noisy
        plasmaScale = Mathf.Clamp(plasmaScale, 1f, 6f);

        // ----------------------------------------------------------

        return new RaymarchPlasmaParams
        {
            ColorInner = innerColor,
            ColorOuter = outerColor,

            BoundsRadius = boundsRadius,
            StepSize = stepSize,
            PlasmaScale = plasmaScale,
            WarpStrength = warpStrength,
            FlowSpeed = flowSpeed,

            DensityGain = densityGain,
            DensityThreshold = densityThreshold,
            DensityPower = densityPower,

            Absorption = absorption,
            Falloff = falloff,
            Emission = emission,

            HaloPulseSpeed = haloPulseSpeed,
            HaloPulseAmplitude = haloPulseAmplitude
        };

        static Color BoostVibrancy(Color color, float saturationBoost, float valueBoost)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            s = Mathf.Clamp01(s + saturationBoost);
            v = Mathf.Clamp01(v + valueBoost);
            return Color.HSVToRGB(h, s, v);
        }

        static float NormalizeHeartRate(float bpm)
        {
            if (float.IsNaN(bpm) || float.IsInfinity(bpm))
            {
                return 0f;
            }

            return Mathf.Clamp01(Mathf.InverseLerp(30f, 200f, bpm));
        }
    }
}
