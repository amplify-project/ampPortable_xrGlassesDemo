using System;
using AmpPortableDataViz.Core;
using UnityEngine;
using UnityEngine.Events;

namespace AmpPortableDataViz.Presentation.Visualization
{
    /// <summary>
    /// Manual/debug source for simulating the shared audience data payload without Redis.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Amp Portable Data Viz/Visualization/Audience Data Manual Driver")]
    public sealed class AudienceDataManualDriver : MonoBehaviour, IDataSource<AudienceSignalSample>
    {
        public enum SourceMode
        {
            Manual,
            ProceduralNoise
        }

        [Serializable]
        public sealed class AudienceSignalSampleEvent : UnityEvent<AudienceSignalSample> { }

        [Header("Identity")]
        [SerializeField] private string sourceId = "audience-manual";
        [SerializeField] private string deviceId;

        [Header("Physiological Signals")]
        [SerializeField, Range(0f, 3f)] private float tonicElectrodermalActivityStdDev = 1f;
        [SerializeField, Range(0f, 3f)] private float temperatureRateOfChangeStdDev = 1f;
        [SerializeField, Range(0f, 3f)] private float skinConductanceResponseFrequencyStdDev = 1f;
        [SerializeField, Range(0f, 3f)] private float heartRateStdDev = 1f;
        [SerializeField, Range(0f, 3f)] private float interBeatIntervalStdDev = 1f;

        [Header("Emotion")]
        [SerializeField, Range(0f, 2f)] private float arousal = 1f;
        [SerializeField, Range(0f, 2f)] private float valence = 1f;

        [Header("Engagement")]
        [SerializeField, Range(0f, 1f)] private float engagement = 0.5f;

        [Header("Driver")]
        [SerializeField] private SourceMode sourceMode = SourceMode.Manual;
        [SerializeField] private bool autoEmit = true;
        [SerializeField] private bool animateProceduralNoise = true;
        [SerializeField, Range(0f, 5f)] private float timeScale = 0.35f;

        [Header("Procedural Noise")]
        [SerializeField, Range(0.05f, 10f)] private float noiseFrequency = 0.75f;
        [SerializeField] private float noiseSeed = 0.1234f;
        [SerializeField] private float noiseOffset;

        [Header("Events")]
        [SerializeField] private AudienceSignalSampleEvent onSampleEmitted = new();
        [SerializeField] private UnityEvent<float> onTonicElectrodermalActivity = new();
        [SerializeField] private UnityEvent<float> onTemperatureRateOfChange = new();
        [SerializeField] private UnityEvent<float> onSkinConductanceResponseFrequency = new();
        [SerializeField] private UnityEvent<float> onHeartRate = new();
        [SerializeField] private UnityEvent<float> onInterBeatInterval = new();
        [SerializeField] private UnityEvent<float> onArousal = new();
        [SerializeField] private UnityEvent<float> onValence = new();
        [SerializeField] private UnityEvent<float> onEngagement = new();

        [Header("Diagnostics")]
        [SerializeField] private bool logEmittedSample;

        private int _sequenceId;

        public string SourceId => string.IsNullOrWhiteSpace(sourceId) ? name : sourceId.Trim();

        public bool HasLatestFrame { get; private set; }

        public DataFrame<AudienceSignalSample> LatestFrame { get; private set; }

        public event Action<DataFrame<AudienceSignalSample>> OnFrame;

        private void OnEnable()
        {
            if (autoEmit)
            {
                EmitNow();
            }
        }

        private void OnValidate()
        {
            ClampInspectorValues();

            if (autoEmit)
            {
                EmitNow();
            }
        }

        private void Update()
        {
            if (!autoEmit)
            {
                return;
            }

            if (sourceMode == SourceMode.ProceduralNoise && animateProceduralNoise)
            {
                noiseOffset += Time.deltaTime * timeScale;
            }

            EmitNow();
        }

        [ContextMenu("Emit Current Values")]
        public void EmitNow()
        {
            AudienceSignalSample sample = sourceMode == SourceMode.ProceduralNoise
                ? BuildProceduralSample()
                : BuildManualSample();

            LatestFrame = new DataFrame<AudienceSignalSample>(DateTime.UtcNow.Ticks, _sequenceId++, sample);
            HasLatestFrame = true;
            OnFrame?.Invoke(LatestFrame);
            onSampleEmitted?.Invoke(sample);

            onTonicElectrodermalActivity?.Invoke(sample.TonicElectrodermalActivityStdDev);
            onTemperatureRateOfChange?.Invoke(sample.TemperatureRateOfChangeStdDev);
            onSkinConductanceResponseFrequency?.Invoke(sample.SkinConductanceResponseFrequencyStdDev);
            onHeartRate?.Invoke(sample.HeartRateStdDev);
            onInterBeatInterval?.Invoke(sample.InterBeatIntervalStdDev);
            onArousal?.Invoke(sample.Arousal);
            onValence?.Invoke(sample.Valence);
            onEngagement?.Invoke(sample.Engagement);

            if (logEmittedSample)
            {
                Debug.Log($"{nameof(AudienceDataManualDriver)} emitted {sample}", this);
            }
        }

        public void SetManualValues(
            float tonicEda,
            float temperatureRateOfChange,
            float skinConductanceResponseFrequency,
            float heartRate,
            float interBeatInterval,
            float arousalValue,
            float valenceValue,
            float engagementValue)
        {
            sourceMode = SourceMode.Manual;
            tonicElectrodermalActivityStdDev = Mathf.Clamp(tonicEda, 0f, 3f);
            temperatureRateOfChangeStdDev = Mathf.Clamp(temperatureRateOfChange, 0f, 3f);
            skinConductanceResponseFrequencyStdDev = Mathf.Clamp(skinConductanceResponseFrequency, 0f, 3f);
            heartRateStdDev = Mathf.Clamp(heartRate, 0f, 3f);
            interBeatIntervalStdDev = Mathf.Clamp(interBeatInterval, 0f, 3f);
            arousal = Mathf.Clamp(arousalValue, 0f, 2f);
            valence = Mathf.Clamp(valenceValue, 0f, 2f);
            engagement = Mathf.Clamp01(engagementValue);
        }

        private AudienceSignalSample BuildManualSample()
        {
            ClampInspectorValues();

            return new AudienceSignalSample(
                ResolveDeviceId(),
                tonicElectrodermalActivityStdDev,
                temperatureRateOfChangeStdDev,
                skinConductanceResponseFrequencyStdDev,
                heartRateStdDev,
                interBeatIntervalStdDev,
                engagement,
                arousal,
                valence);
        }

        private AudienceSignalSample BuildProceduralSample()
        {
            tonicElectrodermalActivityStdDev = SampleNoise(0.11f) * 3f;
            temperatureRateOfChangeStdDev = SampleNoise(1.37f) * 3f;
            skinConductanceResponseFrequencyStdDev = SampleNoise(2.61f) * 3f;
            heartRateStdDev = SampleNoise(3.89f) * 3f;
            interBeatIntervalStdDev = SampleNoise(5.23f) * 3f;
            arousal = SampleNoise(6.47f) * 2f;
            valence = SampleNoise(7.79f) * 2f;
            engagement = SampleNoise(9.01f);

            return BuildManualSample();
        }

        private float SampleNoise(float channelOffset)
        {
            float x = noiseSeed + channelOffset;
            float y = noiseOffset * Mathf.Max(0.0001f, noiseFrequency) + channelOffset * 0.173f;
            return Mathf.Clamp01(Mathf.PerlinNoise(x, y));
        }

        private string ResolveDeviceId()
        {
            return string.IsNullOrWhiteSpace(deviceId) ? name : deviceId.Trim();
        }

        private void ClampInspectorValues()
        {
            tonicElectrodermalActivityStdDev = Mathf.Clamp(tonicElectrodermalActivityStdDev, 0f, 3f);
            temperatureRateOfChangeStdDev = Mathf.Clamp(temperatureRateOfChangeStdDev, 0f, 3f);
            skinConductanceResponseFrequencyStdDev = Mathf.Clamp(skinConductanceResponseFrequencyStdDev, 0f, 3f);
            heartRateStdDev = Mathf.Clamp(heartRateStdDev, 0f, 3f);
            interBeatIntervalStdDev = Mathf.Clamp(interBeatIntervalStdDev, 0f, 3f);
            arousal = Mathf.Clamp(arousal, 0f, 2f);
            valence = Mathf.Clamp(valence, 0f, 2f);
            engagement = Mathf.Clamp01(engagement);
        }
    }
}
