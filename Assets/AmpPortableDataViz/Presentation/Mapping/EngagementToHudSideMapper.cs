// Assets/AmpPortableDataViz/Presentation/Mapping/EngagementToHudSideMapper.cs
using AmpPortableDataViz.Core;
using UnityEngine;

[System.Serializable]
public struct HudSideParams
{
    public float NormalizedValue;
}

public sealed class EngagementToHudSideMapper : IMapper<float, HudSideParams>
{
    [System.Serializable]
    public struct Settings
    {
        public Vector2 InputRange;     // e.g. 0..100 if the queue publishes percentages
        public bool ClampToZeroOne;
    }

    private readonly Settings _settings;

    public EngagementToHudSideMapper(Settings settings) => _settings = settings;

    public HudSideParams Map(in DataFrame<float> inputFrame)
    {
        float normalized = Mathf.InverseLerp(
            _settings.InputRange.x,
            _settings.InputRange.y,
            inputFrame.Payload);

        if (_settings.ClampToZeroOne)
        {
            normalized = Mathf.Clamp01(normalized);
        }

        return new HudSideParams { NormalizedValue = normalized };
    }
}
