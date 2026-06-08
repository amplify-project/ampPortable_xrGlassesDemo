// Assets/AmpPortableDataViz/Presentation/Visualization/EngagementHudBinding.cs
using System;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Presentation.Mapping;
using AmpPortableDataViz.Presentation.Sources;
using UnityEngine;

[RequireComponent(typeof(ColourChangingHudSide))]
public sealed class EngagementHudBinding : MonoBehaviour
{
    [SerializeField] private RedisDataPump engagementSource;
    [SerializeField] private EngagementToHudSideMapper.Settings mapperSettings =
        new EngagementToHudSideMapper.Settings { InputRange = new Vector2(0f, 1f), ClampToZeroOne = true };

    private ColourChangingHudSide _hudSide;
    private IMapper<float, HudSideParams> _mapper;
    private Action<DataFrame<float>> _handler;

    private void Awake()
    {
        _hudSide = GetComponent<ColourChangingHudSide>();
        _mapper = new EngagementToHudSideMapper(mapperSettings);
    }

    private void OnEnable()
    {
        AttachSource();
    }

    private void OnDisable()
    {
        DetachSource();
    }

    public void ConfigureSource(RedisDataPump source)
    {
        bool wasEnabled = isActiveAndEnabled;
        if (wasEnabled)
        {
            DetachSource();
        }

        engagementSource = source;

        if (wasEnabled && engagementSource != null)
        {
            AttachSource();
        }
    }

    private void AttachSource()
    {
        if (engagementSource == null)
        {
            Debug.LogWarning($"{nameof(EngagementHudBinding)} requires a RedisDataPump reference.", this);
            return;
        }

        _handler ??= OnFrame;
        engagementSource.OnFrame += _handler;
    }

    private void DetachSource()
    {
        if (engagementSource != null && _handler != null)
        {
            engagementSource.OnFrame -= _handler;
        }

        _handler = null;
    }

    private void OnFrame(DataFrame<float> frame)
    {
        var hudParams = _mapper.Map(in frame);
        _hudSide.Apply(hudParams, frame.TimestampTicksUtc);
    }
}
