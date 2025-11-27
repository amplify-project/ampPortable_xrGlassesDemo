using System;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Presentation.Mapping;
using AmpPortableDataViz.Presentation.Sources;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Visualization
{
    /// <summary>
    /// Subscribes to valence/arousal Redis streams for a specific device and updates a raymarched plasma visual.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EmotionRayPlasmaVisualizer))]
    public sealed class EmotionRayPlasmaBinding : MonoBehaviour
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
        [SerializeField] private RedisDataPump heartRateSource;

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

        private EmotionRayPlasmaVisualizer _visualizer;
        private IMapper<ArousalValenceSample, RaymarchPlasmaParams> _mapper;
        private FrameSmoother<RaymarchPlasmaParams> _smoother;

        private Action<DataFrame<float>> _valenceHandler;
        private Action<DataFrame<float>> _arousalHandler;
        private Action<DataFrame<float>> _heartRateHandler;

        private bool _hasValence;
        private bool _hasArousal;
        private float _valenceValue;
        private float _arousalValue;
        private float _heartRateValue;
        private long _valenceTimestamp;
        private long _arousalTimestamp;
        private long _heartRateTimestamp;
        private int _sequenceId;
        private bool _hasHeartRate;
        private bool _needsApply;

        private void Awake()
        {
            _visualizer = GetComponent<EmotionRayPlasmaVisualizer>();
            _mapper = new ArousalValenceToRaymarchPlasmaMapper();
            _smoother = new FrameSmoother<RaymarchPlasmaParams>(LerpParams);
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

        public void ConfigureSources(RedisDataPump newValenceSource, RedisDataPump newArousalSource, RedisDataPump newHeartRateSource = null, string overrideDeviceId = null)
        {
            bool wasEnabled = isActiveAndEnabled;
            if (wasEnabled)
            {
                DetachSources();
            }

            valenceSource = newValenceSource;
            arousalSource = newArousalSource;
            heartRateSource = newHeartRateSource;

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
            if (heartRateSource != null)
            {
                SubscribeToPump(heartRateSource, ref _heartRateHandler, OnHeartRateFrame, EmotionChannelKind.HeartRate);
            }
        }

        private void DetachSources()
        {
            UnsubscribeFromPump(valenceSource, ref _valenceHandler);
            UnsubscribeFromPump(arousalSource, ref _arousalHandler);
            UnsubscribeFromPump(heartRateSource, ref _heartRateHandler);
        }

        private void SubscribeToPump(RedisDataPump pump, ref Action<DataFrame<float>> handler, Action<DataFrame<float>> callback, EmotionChannelKind channelKind)
        {
            if (pump == null)
            {
                Debug.LogWarning($"EmotionRayPlasmaBinding[{ResolveDeviceId()}] is missing a {channelKind} RedisDataPump reference.", this);
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
                Debug.Log($"EmotionRayPlasmaBinding[{ResolveDeviceId()}] Valence raw={_valenceValue:F4} seq={frame.SequenceId} ts={frame.TimestampTicksUtc}");
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
                Debug.Log($"EmotionRayPlasmaBinding[{ResolveDeviceId()}] Arousal raw={_arousalValue:F4} seq={frame.SequenceId} ts={frame.TimestampTicksUtc}");
            }

            TryEmit();
        }

        private void OnHeartRateFrame(DataFrame<float> frame)
        {
            _heartRateValue = frame.Payload;
            _heartRateTimestamp = frame.TimestampTicksUtc;
            _hasHeartRate = true;

            if (logRawInputs)
            {
                Debug.Log($"EmotionRayPlasmaBinding[{ResolveDeviceId()}] HeartRate raw={_heartRateValue:F4} seq={frame.SequenceId} ts={frame.TimestampTicksUtc}");
            }

            // Push updated halo parameters as soon as new HR data arrives (as long as we already have valence/arousal).
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

            RaymarchPlasmaParams parameters = _mapper.Map(in frame);

            float hrNormalized = _hasHeartRate ? Mathf.Clamp01(Mathf.InverseLerp(30f, 200f, _heartRateValue)) : 0f;
            if (hrNormalized > 0f)
            {
                float clampedBpm = Mathf.Clamp(_heartRateValue, 30f, 200f);
                float beatsPerSecond = clampedBpm / 60f;
                parameters.HaloPulseSpeed = beatsPerSecond * Mathf.PI * 2f;
                parameters.HaloPulseAmplitude = Mathf.Lerp(0.05f, 1f, hrNormalized);
            }
            else
            {
                parameters.HaloPulseSpeed = 0f;
                parameters.HaloPulseAmplitude = 0f;
            }
            timestamp = Math.Max(timestamp, _heartRateTimestamp);
            QueueParameters(parameters, timestamp);

            if (logResolvedSamples)
            {
                Debug.Log($"EmotionRayPlasmaBinding[{payload.DeviceId}] -> valence {valenceLevel}, arousal {arousalLevel}, ts {timestamp}");
            }

            if (logShaderParameters)
            {
                Debug.Log($"EmotionRayPlasmaBinding[{payload.DeviceId}] ShaderParams => " +
                    $"ColorInner={parameters.ColorInner}, ColorOuter={parameters.ColorOuter}, " +
                    $"BoundsRadius={parameters.BoundsRadius:F3}, StepSize={parameters.StepSize:F4}, PlasmaScale={parameters.PlasmaScale:F3}, WarpStrength={parameters.WarpStrength:F3}, FlowSpeed={parameters.FlowSpeed:F3}, " +
                    $"DensityGain={parameters.DensityGain:F3}, DensityThreshold={parameters.DensityThreshold:F3}, DensityPower={parameters.DensityPower:F3}, " +
                    $"Absorption={parameters.Absorption:F3}, Falloff={parameters.Falloff:F3}, Emission={parameters.Emission:F3}, " +
                    $"HaloPulseSpeed={parameters.HaloPulseSpeed:F3}, HaloPulseAmplitude={parameters.HaloPulseAmplitude:F3}");
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
            _hasHeartRate = false;
            _sequenceId = 0;
            _valenceTimestamp = 0;
            _arousalTimestamp = 0;
            _heartRateTimestamp = 0;
            _heartRateValue = 0f;
            _needsApply = false;
            _smoother?.Reset();
        }

        private void QueueParameters(in RaymarchPlasmaParams parameters, long timestamp)
        {
            _smoother ??= new FrameSmoother<RaymarchPlasmaParams>(LerpParams);
            _smoother.SetTarget(parameters, timestamp);
            _needsApply = true;
        }

        private static RaymarchPlasmaParams LerpParams(RaymarchPlasmaParams from, RaymarchPlasmaParams to, float t)
        {
            return new RaymarchPlasmaParams
            {
                ColorInner = Color.Lerp(from.ColorInner, to.ColorInner, t),
                ColorOuter = Color.Lerp(from.ColorOuter, to.ColorOuter, t),
                BoundsRadius = Mathf.Lerp(from.BoundsRadius, to.BoundsRadius, t),
                StepSize = Mathf.Lerp(from.StepSize, to.StepSize, t),
                PlasmaScale = Mathf.Lerp(from.PlasmaScale, to.PlasmaScale, t),
                WarpStrength = Mathf.Lerp(from.WarpStrength, to.WarpStrength, t),
                FlowSpeed = Mathf.Lerp(from.FlowSpeed, to.FlowSpeed, t),
                DensityGain = Mathf.Lerp(from.DensityGain, to.DensityGain, t),
                DensityThreshold = Mathf.Lerp(from.DensityThreshold, to.DensityThreshold, t),
                DensityPower = Mathf.Lerp(from.DensityPower, to.DensityPower, t),
                Absorption = Mathf.Lerp(from.Absorption, to.Absorption, t),
                Falloff = Mathf.Lerp(from.Falloff, to.Falloff, t),
                Emission = Mathf.Lerp(from.Emission, to.Emission, t),
                HaloPulseSpeed = Mathf.Lerp(from.HaloPulseSpeed, to.HaloPulseSpeed, t),
                HaloPulseAmplitude = Mathf.Lerp(from.HaloPulseAmplitude, to.HaloPulseAmplitude, t)
            };
        }
    }
}
