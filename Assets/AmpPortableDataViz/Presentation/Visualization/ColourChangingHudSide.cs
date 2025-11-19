using AmpPortableDataViz.Core;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public sealed class ColourChangingHudSide : MonoBehaviour, IVisualizer<HudSideParams>
{
    [Range(0f, 1f)]
    public float value = 0f;   // <-- Set this at runtime

    private LineRenderer lr;

    // Define your colour gradient
    public Color lowColor = Color.red;
    public Color midColor = new Color(1f, 0.6f, 0f); // orange
    public Color highColor = Color.green;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
    }

    void Update()
    {
        lr.startColor = GetColorForValue(value);
        lr.endColor = lr.startColor;
    }

    Color GetColorForValue(float t)
    {
        t = Mathf.Clamp01(t);

        // 0.0 – 0.5 → red → orange
        if (t < 0.5f)
        {
            float nt = t / 0.5f;
            return Color.Lerp(lowColor, midColor, nt);
        }

        // 0.5 – 1.0 → orange → green
        else
        {
            float nt = (t - 0.5f) / 0.5f;
            return Color.Lerp(midColor, highColor, nt);
        }
    }

    public void Apply(in HudSideParams parameters, long timestampTicksUtc)
    {
        SetValue(parameters.NormalizedValue);
    }
    
    // Public method to update from your external system
    public void SetValue(float v)
    {
        value = Mathf.Clamp01(v);
    }
}
