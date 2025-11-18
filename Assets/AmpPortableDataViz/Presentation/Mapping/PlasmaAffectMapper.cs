using AmpPortableDataViz.Core;
using UnityEngine;

public static class PlasmaAffectMapper
{
    /// <summary>
    /// Generates a full set of EmotionPlasma parameters for the provided affective levels.
    /// </summary>
    public static EmotionPlasmaParams BuildParams(int valenceLevel, int arousalLevel)
    {
        float vNorm = Mathf.Clamp(valenceLevel - 1, -1f, 1f);
        float aNorm = Mathf.Clamp(arousalLevel - 1, -1f, 1f);

        // Push colour extremes harder so valence swings feel bolder.
        Color negCol = new Color(1.00f, 0.10f, 0.05f);
        Color neuCol = new Color(0.90f, 0.95f, 1.00f);
        Color posCol = new Color(0.05f, 0.65f, 1.00f);

        Color baseValenceCol =
            (vNorm < 0f) ? Color.Lerp(neuCol, negCol, -vNorm) :
            (vNorm > 0f) ? Color.Lerp(neuCol, posCol, vNorm) :
                           neuCol;

        float brightnessMod = Mathf.Lerp(0.4f, 1.8f, (aNorm + 1f) * 0.5f);
        Color finalBaseColor = baseValenceCol * brightnessMod;

        float brightness =
            Mathf.Lerp(0.6f, 4.5f, (aNorm + 1f) * 0.5f) +
            0.5f * vNorm;
        brightness = Mathf.Clamp(brightness, 0f, 5.5f);

        float saturation =
            0.7f +
            1.6f * vNorm +
            1.2f * aNorm;
        saturation = Mathf.Clamp(saturation, 0f, 3.5f);

        float curvature =
            0.45f +
            0.45f * vNorm -
            0.20f * aNorm;
        curvature = Mathf.Clamp01(curvature);

        float asymmetry =
            0.15f -
            0.45f * vNorm +
            0.60f * aNorm;
        asymmetry = Mathf.Clamp01(asymmetry);

        float surfaceSharpness =
            0.2f +
            1.5f * (-Mathf.Min(vNorm, 0f)) +
            0.9f * aNorm;
        surfaceSharpness = Mathf.Clamp(surfaceSharpness, 0f, 2f);

        float flowSpeed =
            0.2f +
            3.2f * aNorm +
            0.8f * vNorm;
        flowSpeed = Mathf.Clamp(flowSpeed, 0f, 6f);

        float turbulence =
            0.05f +
            0.7f * aNorm +
            0.8f * (-Mathf.Min(vNorm, 0f));
        turbulence = Mathf.Clamp01(turbulence);

        float pulseFreq =
            0.5f +
            7.5f * aNorm +
            0.8f * vNorm;
        pulseFreq = Mathf.Clamp(pulseFreq, 0f, 10f);

        float pulseAmp =
            0.15f +
            0.6f * aNorm +
            0.5f * (-Mathf.Min(vNorm, 0f));
        pulseAmp = Mathf.Clamp01(pulseAmp);

        float smoothness =
            0.85f +
            0.15f * vNorm -
            0.45f * aNorm;
        smoothness = Mathf.Clamp01(smoothness);

        float noiseScale =
            0.3f +
            1.0f * aNorm +
            0.7f * (-Mathf.Min(vNorm, 0f));
        noiseScale = Mathf.Clamp(noiseScale, 0f, 2f);

        float noiseContrast =
            0.4f +
            1.1f * aNorm +
            1.2f * (-Mathf.Min(vNorm, 0f));
        noiseContrast = Mathf.Clamp(noiseContrast, 0f, 2f);

        float outerBlend = Mathf.Lerp(0.2f, 0.6f, (aNorm + 1f) * 0.5f);
        Color innerColor = finalBaseColor;
        Color outerColor = Color.Lerp(finalBaseColor, Color.white, outerBlend);

        return new EmotionPlasmaParams
        {
            BaseColor = finalBaseColor,
            InnerColor = innerColor,
            OuterColor = outerColor,
            Brightness = brightness,
            Saturation = saturation,
            Curvature = curvature,
            Asymmetry = asymmetry,
            SurfaceSharpness = surfaceSharpness,
            FlowSpeed = flowSpeed,
            Turbulence = turbulence,
            PulseFrequency = pulseFreq,
            PulseAmplitude = pulseAmp,
            Smoothness = smoothness,
            NoiseScale = noiseScale,
            NoiseContrast = noiseContrast
        };
    }

    /// <summary>
    /// Legacy helper that immediately writes the mapped values into the provided material.
    /// </summary>
    public static void ApplyArousalValence(Material mat, int valenceLevel, int arousalLevel)
    {
        if (mat == null)
        {
            return;
        }

        EmotionPlasmaParams parameters = BuildParams(valenceLevel, arousalLevel);

        mat.SetColor("_BaseColor", parameters.BaseColor);
        mat.SetColor("_ColorInner", parameters.InnerColor);
        mat.SetColor("_ColorOuter", parameters.OuterColor);

        mat.SetFloat("_Brightness", parameters.Brightness);
        mat.SetFloat("_Saturation", parameters.Saturation);

        mat.SetFloat("_Curvature", parameters.Curvature);
        mat.SetFloat("_Asymmetry", parameters.Asymmetry);
        mat.SetFloat("_SurfaceSharpness", parameters.SurfaceSharpness);

        mat.SetFloat("_FlowSpeed", parameters.FlowSpeed);
        mat.SetFloat("_Turbulence", parameters.Turbulence);
        mat.SetFloat("_PulseFrequency", parameters.PulseFrequency);
        mat.SetFloat("_PulseAmplitude", parameters.PulseAmplitude);

        mat.SetFloat("_Smoothness", parameters.Smoothness);
        mat.SetFloat("_NoiseScale", parameters.NoiseScale);
        mat.SetFloat("_NoiseContrast", parameters.NoiseContrast);
    }
}
