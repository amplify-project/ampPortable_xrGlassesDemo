using AmpPortableDataViz.Core;
using UnityEngine;

public static class RaymarchPlasmaParamsMapper
{
    /// <summary>
    /// Builds raymarch plasma shader parameters from valence (−1,0,1) and arousal (−1,0,1).
    /// </summary>
    public static RaymarchPlasmaParams BuildParams(int valenceLevel, int arousalLevel)
    {
        float vNorm = Mathf.Clamp(valenceLevel, -1f, 1f); // -1 negative, 0 neutral, +1 positive
        float aNorm = Mathf.Clamp(arousalLevel, -1f, 1f); // -1 low,     0 medium,    +1 high
        float arousalBlend = (aNorm + 1f) * 0.5f;
        float negativeValence = Mathf.Max(-vNorm, 0f);

        // ----------------------------------------------------------
        // 1. COLOUR SELECTION (Valence = warm vs cool)
        // ----------------------------------------------------------

        // Push colour separation for clarity.
        Color negCol = new Color(1.00f, 0.18f, 0.05f);  // hostile warm
        Color neuCol = new Color(0.82f, 0.45f, 0.92f);  // balanced magenta
        Color posCol = new Color(0.05f, 0.87f, 0.95f);  // friendly cool

        Color baseValenceColor =
            (vNorm < 0f) ? Color.Lerp(neuCol, negCol, -vNorm) :
            (vNorm > 0f) ? Color.Lerp(neuCol, posCol,  vNorm) :
                           neuCol;

        // Arousal modulates brightness of both inner & outer colours.
        float saturationBoost = Mathf.Lerp(0.18f, 0.45f, arousalBlend);
        float valueBoost = Mathf.Lerp(0.08f, 0.25f, arousalBlend);
        Color saturatedValenceColor = BoostVibrancy(baseValenceColor, saturationBoost, valueBoost);

        float brightnessMod = Mathf.Lerp(1.05f, 1.6f, arousalBlend);
        Color innerColor = saturatedValenceColor * brightnessMod;

        // Outer halo keeps colour influence but adds glow.
        float outerTintSat = Mathf.Lerp(0.1f, 0.35f, arousalBlend);
        float outerTintVal = Mathf.Lerp(0.05f, 0.3f, arousalBlend);
        Color haloTarget = BoostVibrancy(Color.Lerp(saturatedValenceColor, Color.white, 0.25f), outerTintSat, outerTintVal);
        float outerBlend = Mathf.Lerp(0.25f, 0.8f, arousalBlend);
        Color outerColor = Color.Lerp(innerColor, haloTarget, outerBlend);

        // ----------------------------------------------------------
        // 2. EMISSION (major arousal cue)
        // ----------------------------------------------------------

        float emission =
            Mathf.Lerp(1.2f, 5f, arousalBlend) +    // arousal controls brightness strongly
            0.7f * vNorm;                           // positive valence makes glow cleaner
        emission = Mathf.Clamp(emission, 0.8f, 6f);

        // ----------------------------------------------------------
        // 3. MOVEMENT & TURBULENCE
        // ----------------------------------------------------------

        float flowSpeed =
            0.3f +
            3.2f * aNorm +    // arousal = main motion driver
            0.6f * negativeValence; // negative valence = restless
        flowSpeed = Mathf.Clamp(flowSpeed, 0f, 6f);

        float warpStrength =
            0.25f +
            0.6f * aNorm +              // high arousal = more spatial wobble
            0.5f * negativeValence; // negative valence = distorted shape
        warpStrength = Mathf.Clamp(warpStrength, 0f, 2f);

        // ----------------------------------------------------------
        // 4. DENSITY / SHAPE CONTRAST
        // ----------------------------------------------------------

        // Harsh negative valence = high contrast clumps; positive = smooth volume.
        float densityGain =
            0.85f +
            0.7f * aNorm +
            0.5f * negativeValence;
        densityGain = Mathf.Clamp(densityGain, 0.7f, 2.4f);

        float densityThreshold =
            0.18f +
            0.22f * aNorm +               // arousal concentrates bright pockets
            0.18f * negativeValence;
        densityThreshold = Mathf.Clamp(densityThreshold, 0f, 0.65f);

        float densityPower =
            1.0f +
            0.4f * aNorm +                // high arousal = strong bright/dark contrast
            0.5f * negativeValence;       // negative valence = harsher transitions
        densityPower = Mathf.Clamp(densityPower, 0.9f, 2.6f);

        // ----------------------------------------------------------
        // 5. TRANSPARENCY & EDGE FEEL
        // ----------------------------------------------------------

        float absorption =
            0.12f +
            0.28f * negativeValence +   // negative = darker, more opaque
            0.18f * aNorm;              // high arousal = slightly denser
        absorption = Mathf.Clamp(absorption, 0.08f, 1.2f);

        float falloff =
            0.45f +
            0.25f * vNorm -    // positive soft edge, negative tight edge
            0.10f * aNorm;     // high arousal = slightly sharper falloff
        falloff = Mathf.Clamp(falloff, 0.25f, 1f);

        // ----------------------------------------------------------
        // 6. VOLUME SCALE
        // ----------------------------------------------------------

        float boundsRadius =
            0.75f +
            0.1f * vNorm +   // positive valence feels more open
            0.04f * aNorm;   // high arousal slightly expands volume
        boundsRadius = Mathf.Clamp(boundsRadius, 0.45f, 1.05f);

        // Step size: subtle arousal cue (crisper raymarch at high arousal)
        float stepSize =
            0.035f -
            0.01f * aNorm;  // high arousal = finer marching steps
        stepSize = Mathf.Clamp(stepSize, 0.005f, 0.06f);

        // PlasmaScale: detail frequency
        float plasmaScale =
            3.0f +
            0.7f * aNorm +                  // high arousal = finer detail
            0.5f * negativeValence;         // negative valence = noisy
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
            Emission = emission
        };

        static Color BoostVibrancy(Color color, float saturationBoost, float valueBoost)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            s = Mathf.Clamp01(s + saturationBoost);
            v = Mathf.Clamp01(v + valueBoost);
            return Color.HSVToRGB(h, s, v);
        }
    }
}
