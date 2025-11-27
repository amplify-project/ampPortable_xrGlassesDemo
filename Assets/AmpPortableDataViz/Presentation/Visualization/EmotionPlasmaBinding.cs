using System;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Presentation.Mapping;
using AmpPortableDataViz.Presentation.Sources;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Visualization
{
    /// <summary>
    /// Subscribes to valence/arousal Redis streams for a specific device and updates an EmotionPlasma visual.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EmotionPlasmaVisualizer))]
    public sealed class EmotionPlasmaBinding : MonoBehaviour
    {
        [Serializable]
        private struct AffectLevelQuantizer
        {
            public bool InputIsDiscreteLevels;
            public Vector2 InputRange;
            [Range(0f, 1f)] public float LowThreshold;
            [Range(0f, 1f)] public float HighThreshold;

            public float Map(float value)
            {
                if (InputIsDiscreteLevels)
                {
                    return Mathf.Clamp(Mathf.RoundToInt(value), 0, 2);
                }

                float normalized = Mathf.InverseLerp(InputRange.x, InputRange.y, value);
                normalized = Mathf.Clamp01(normalized);

                return normalized * 2f;
            }

            public static AffectLevelQuantizer CreateValenceDefaults()
            {
                return new AffectLevelQuantizer
                {
                    InputIsDiscreteLevels = false,
                    InputRange = new Vector2(0f, 2f),
                    LowThreshold = 0.35f,
                    HighThreshold = 0.65f
                };
            }

            public static AffectLevelQuantizer CreateArousalDefaults()
            {
                return new AffectLevelQuantizer
                {
                    InputIsDiscreteLevels = false,
                    InputRange = new Vector2(0f, 2f),
                    LowThreshold = 0.35f,
                    HighThreshold = 0.65f
                };
            }
        }

        [Header("Redis Sources")]
        [SerializeField] private RedisDataPump valenceSource;
        [SerializeField] private RedisDataPump arousalSource;

        [Header("Device Identity")]
        [SerializeField] private string deviceId;

        [Header("Quantization")]
        [SerializeField] private AffectLevelQuantizer valenceQuantizer = AffectLevelQuantizer.CreateValenceDefaults();
        [SerializeField] private AffectLevelQuantizer arousalQuantizer = AffectLevelQuantizer.CreateArousalDefaults();

        [Header("Smoothing")]
        [SerializeField] private bool enableSmoothing = true;
        [SerializeField, Range(0.05f, 5f)] private float smoothingDurationSeconds = 1f;

        [Header("Diagnostics")]
        [SerializeField] private bool logResolvedSamples;
        [SerializeField] private bool logRawInputs;
        [SerializeField] private bool logShaderParameters;

        private EmotionPlasmaVisualizer _visualizer;
        private IMapper<ArousalValenceSample, EmotionPlasmaParams> _mapper;
        private FrameSmoother<EmotionPlasmaParams> _smoother;

        private Action<DataFrame<float>> _valenceHandler;
        private Action<DataFrame<float>> _arousalHandler;

        private bool _hasValence;
        private bool _hasArousal;
        private float _valenceValue;
        private float _arousalValue;
        private long _valenceTimestamp;
        private long _arousalTimestamp;
        private int _sequenceId;
        private bool _needsApply;

        private void Awake()
        {
            _visualizer = GetComponent<EmotionPlasmaVisualizer>();
            _mapper = new ArousalValenceToEmotionPlasmaMapper();
            _smoother = new FrameSmoother<EmotionPlasmaParams>(LerpParams);
        }

        private void OnEnable()
        {
            ResetState();
            AttachSources();
        }

        private void Update()
        {
            if (_visualizer == null || _smoother == null || !_smoother.HasValue)
            {
                return;
            }

            float duration = enableSmoothing ? Mathf.Max(0f, smoothingDurationSeconds) : 0f;
            _smoother.Step(Time.deltaTime, duration);

            if (!_needsApply && !_smoother.IsInterpolating)
            {
                return;
            }

            _visualizer.Apply(_smoother.Current, _smoother.TargetTimestamp);
            _needsApply = _smoother.IsInterpolating;
        }

        private void OnDisable()
        {
            DetachSources();
            ResetState();
        }

        public void ConfigureSources(RedisDataPump newValenceSource, RedisDataPump newArousalSource, string overrideDeviceId = null)
        {
            bool wasEnabled = isActiveAndEnabled;
            if (wasEnabled)
            {
                DetachSources();
            }

            valenceSource = newValenceSource;
            arousalSource = newArousalSource;

            if (!string.IsNullOrWhiteSpace(overrideDeviceId))
            {
                deviceId = overrideDeviceId.Trim();
            }

            if (wasEnabled)
            {
                ResetState();
                AttachSources();
            }
        }

        private void AttachSources()
        {
            SubscribeToPump(valenceSource, ref _valenceHandler, OnValenceFrame, EmotionChannelKind.Valence);
            SubscribeToPump(arousalSource, ref _arousalHandler, OnArousalFrame, EmotionChannelKind.Arousal);
        }

        private void DetachSources()
        {
            UnsubscribeFromPump(valenceSource, ref _valenceHandler);
            UnsubscribeFromPump(arousalSource, ref _arousalHandler);
        }

        private void SubscribeToPump(RedisDataPump pump, ref Action<DataFrame<float>> handler, Action<DataFrame<float>> callback, EmotionChannelKind channelKind)
        {
            if (pump == null)
            {
                Debug.LogWarning($"EmotionPlasmaBinding[{ResolveDeviceId()}] is missing a {channelKind} RedisDataPump reference.", this);
                return;
            }

            handler ??= callback;
            pump.OnFrame += handler;
        }

        private void UnsubscribeFromPump(RedisDataPump pump, ref Action<DataFrame<float>> handler)
        {
            if (pump != null && handler != null)
            {
                pump.OnFrame -= handler;
            }

            handler = null;
        }

        private void OnValenceFrame(DataFrame<float> frame)
        {
            _valenceValue = frame.Payload;
            _valenceTimestamp = frame.TimestampTicksUtc;
            _hasValence = true;

            if (logRawInputs)
            {
                Debug.Log($"EmotionPlasmaBinding[{ResolveDeviceId()}] Valence raw={_valenceValue:F4} seq={frame.SequenceId} ts={frame.TimestampTicksUtc}");
            }

            TryEmit();
        }

        private void OnArousalFrame(DataFrame<float> frame)
        {
            _arousalValue = frame.Payload;
            _arousalTimestamp = frame.TimestampTicksUtc;
            _hasArousal = true;

            if (logRawInputs)
            {
                Debug.Log($"EmotionPlasmaBinding[{ResolveDeviceId()}] Arousal raw={_arousalValue:F4} seq={frame.SequenceId} ts={frame.TimestampTicksUtc}");
            }

            TryEmit();
        }

        private void TryEmit()
        {
            if (!_hasValence || !_hasArousal || _visualizer == null || _mapper == null)
            {
                return;
            }

            float valenceLevel = valenceQuantizer.Map(_valenceValue);
            float arousalLevel = arousalQuantizer.Map(_arousalValue);
            long timestamp = Math.Max(_valenceTimestamp, _arousalTimestamp);
            var payload = new ArousalValenceSample(ResolveDeviceId(), valenceLevel, arousalLevel);
            var frame = new DataFrame<ArousalValenceSample>(timestamp, _sequenceId++, payload);

            EmotionPlasmaParams parameters = _mapper.Map(in frame);
            QueueParameters(parameters, timestamp);

            if (logResolvedSamples)
            {
                Debug.Log($"EmotionPlasmaBinding[{payload.DeviceId}] -> valence {valenceLevel}, arousal {arousalLevel}, ts {timestamp}");
            }

            if (logShaderParameters)
            {
                Debug.Log($"EmotionPlasmaBinding[{payload.DeviceId}] ShaderParams => " +
                    $"BaseColor={parameters.BaseColor}, InnerColor={parameters.InnerColor}, OuterColor={parameters.OuterColor}, " +
                    $"Brightness={parameters.Brightness:F3}, Saturation={parameters.Saturation:F3}, " +
                    $"Curvature={parameters.Curvature:F3}, Asymmetry={parameters.Asymmetry:F3}, SurfaceSharpness={parameters.SurfaceSharpness:F3}, " +
                    $"FlowSpeed={parameters.FlowSpeed:F3}, Turbulence={parameters.Turbulence:F3}, PulseFreq={parameters.PulseFrequency:F3}, PulseAmp={parameters.PulseAmplitude:F3}, " +
                    $"Smoothness={parameters.Smoothness:F3}, NoiseScale={parameters.NoiseScale:F3}, NoiseContrast={parameters.NoiseContrast:F3}");
            }
        }

        private string ResolveDeviceId()
        {
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                return deviceId.Trim();
            }

            return _visualizer != null && _visualizer.gameObject != null
                ? _visualizer.gameObject.name
                : name;
        }

        private void ResetState()
        {
            _hasValence = false;
            _hasArousal = false;
            _sequenceId = 0;
            _valenceTimestamp = 0;
            _arousalTimestamp = 0;
            _needsApply = false;
            _smoother?.Reset();
        }

        private void QueueParameters(in EmotionPlasmaParams parameters, long timestamp)
        {
            _smoother ??= new FrameSmoother<EmotionPlasmaParams>(LerpParams);
            _smoother.SetTarget(parameters, timestamp);
            _needsApply = true;
        }

        private static EmotionPlasmaParams LerpParams(EmotionPlasmaParams from, EmotionPlasmaParams to, float t)
        {
            return new EmotionPlasmaParams
            {
                BaseColor = Color.Lerp(from.BaseColor, to.BaseColor, t),
                InnerColor = Color.Lerp(from.InnerColor, to.InnerColor, t),
                OuterColor = Color.Lerp(from.OuterColor, to.OuterColor, t),
                Brightness = Mathf.Lerp(from.Brightness, to.Brightness, t),
                Saturation = Mathf.Lerp(from.Saturation, to.Saturation, t),
                Curvature = Mathf.Lerp(from.Curvature, to.Curvature, t),
                Asymmetry = Mathf.Lerp(from.Asymmetry, to.Asymmetry, t),
                SurfaceSharpness = Mathf.Lerp(from.SurfaceSharpness, to.SurfaceSharpness, t),
                FlowSpeed = Mathf.Lerp(from.FlowSpeed, to.FlowSpeed, t),
                Turbulence = Mathf.Lerp(from.Turbulence, to.Turbulence, t),
                PulseFrequency = Mathf.Lerp(from.PulseFrequency, to.PulseFrequency, t),
                PulseAmplitude = Mathf.Lerp(from.PulseAmplitude, to.PulseAmplitude, t),
                Smoothness = Mathf.Lerp(from.Smoothness, to.Smoothness, t),
                NoiseScale = Mathf.Lerp(from.NoiseScale, to.NoiseScale, t),
                NoiseContrast = Mathf.Lerp(from.NoiseContrast, to.NoiseContrast, t)
            };
        }
    }
}
