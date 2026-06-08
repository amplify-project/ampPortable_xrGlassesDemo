using System;
using AmpPortableDataViz.Core;
using UnityEngine;
using UnityEngine.Events;

namespace AmpPortableDataViz.Presentation.Visualization
{
    /// <summary>
    /// Debug source for simulating ParticleMesh input signals before the visualizer is implemented.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Amp Portable Data Viz/Visualization/Particle Mesh Manual Driver")]
    public sealed class ParticleMeshManualDriver : MonoBehaviour, IDataSource<ParticleMeshSignalSample>
    {
        public enum SourceMode
        {
            Manual,
            ProceduralNoise
        }

        [Serializable]
        public sealed class ParticleMeshSignalSampleEvent : UnityEvent<ParticleMeshSignalSample> { }

        [Serializable]
        public sealed class Vector2Event : UnityEvent<Vector2> { }

        [Header("Identity")]
        [SerializeField] private string sourceId = "particle-mesh-manual";
        [SerializeField] private string deviceId;

        [Header("Physiological Signals")]
        [SerializeField, Range(-1f, 1f)] private float tonicElectrodermalActivityStdDev;
        [SerializeField, Range(-1f, 1f)] private float temperatureRateOfChangeStdDev;
        [SerializeField, Range(-1f, 1f)] private float skinConductanceResponseFrequencyStdDev;
        [SerializeField, Range(-1f, 1f)] private float heartRateStdDev;
        [SerializeField, Range(-1f, 1f)] private float interBeatIntervalStdDev;

        [Header("Facial Emotion")]
        [SerializeField, Range(0f, 1f)] private float facialEmotionArousal = 0.5f;
        [SerializeField, Range(0f, 1f)] private float facialEmotionValence = 0.5f;

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
        [SerializeField] private ParticleMeshSignalSampleEvent onSampleEmitted = new();
        [SerializeField] private UnityEvent<float> onTonicElectrodermalActivity = new();
        [SerializeField] private UnityEvent<float> onTemperatureRateOfChange = new();
        [SerializeField] private UnityEvent<float> onSkinConductanceResponseFrequency = new();
        [SerializeField] private UnityEvent<float> onHeartRate = new();
        [SerializeField] private UnityEvent<float> onInterBeatInterval = new();
        [SerializeField] private Vector2Event onFacialEmotionArousalValence = new();
        [SerializeField] private UnityEvent<float> onEngagement = new();

        [Header("Diagnostics")]
        [SerializeField] private bool logEmittedSample;

        private int _sequenceId;

        public string SourceId => string.IsNullOrWhiteSpace(sourceId) ? name : sourceId.Trim();

        public bool HasLatestFrame { get; private set; }

        public DataFrame<ParticleMeshSignalSample> LatestFrame { get; private set; }

        public event Action<DataFrame<ParticleMeshSignalSample>> OnFrame;

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
            ParticleMeshSignalSample sample = sourceMode == SourceMode.ProceduralNoise
                ? BuildProceduralSample()
                : BuildManualSample();

            LatestFrame = new DataFrame<ParticleMeshSignalSample>(DateTime.UtcNow.Ticks, _sequenceId++, sample);
            HasLatestFrame = true;
            OnFrame?.Invoke(LatestFrame);
            onSampleEmitted?.Invoke(sample);

            onTonicElectrodermalActivity?.Invoke(sample.TonicElectrodermalActivityStdDev);
            onTemperatureRateOfChange?.Invoke(sample.TemperatureRateOfChangeStdDev);
            onSkinConductanceResponseFrequency?.Invoke(sample.SkinConductanceResponseFrequencyStdDev);
            onHeartRate?.Invoke(sample.HeartRateStdDev);
            onInterBeatInterval?.Invoke(sample.InterBeatIntervalStdDev);
            onFacialEmotionArousalValence?.Invoke(new Vector2(sample.FacialEmotionArousal, sample.FacialEmotionValence));
            onEngagement?.Invoke(sample.Engagement);

            if (logEmittedSample)
            {
                Debug.Log($"{nameof(ParticleMeshManualDriver)} emitted {sample}", this);
            }
        }

        public void SetManualValues(
            float tonicEda,
            float temperatureRateOfChange,
            float skinConductanceResponseFrequency,
            float heartRate,
            float interBeatInterval,
            float facialEmotionArousalValue,
            float facialEmotionValenceValue,
            float engagementValue)
        {
            sourceMode = SourceMode.Manual;
            tonicElectrodermalActivityStdDev = Mathf.Clamp(tonicEda, -1f, 1f);
            temperatureRateOfChangeStdDev = Mathf.Clamp(temperatureRateOfChange, -1f, 1f);
            skinConductanceResponseFrequencyStdDev = Mathf.Clamp(skinConductanceResponseFrequency, -1f, 1f);
            heartRateStdDev = Mathf.Clamp(heartRate, -1f, 1f);
            interBeatIntervalStdDev = Mathf.Clamp(interBeatInterval, -1f, 1f);
            facialEmotionArousal = Mathf.Clamp01(facialEmotionArousalValue);
            facialEmotionValence = Mathf.Clamp01(facialEmotionValenceValue);
            engagement = Mathf.Clamp01(engagementValue);
        }

        private ParticleMeshSignalSample BuildManualSample()
        {
            ClampInspectorValues();

            return new ParticleMeshSignalSample(
                ResolveDeviceId(),
                tonicElectrodermalActivityStdDev,
                temperatureRateOfChangeStdDev,
                skinConductanceResponseFrequencyStdDev,
                heartRateStdDev,
                interBeatIntervalStdDev,
                facialEmotionArousal,
                facialEmotionValence,
                engagement);
        }

        private ParticleMeshSignalSample BuildProceduralSample()
        {
            tonicElectrodermalActivityStdDev = SampleNoise(0.11f);
            temperatureRateOfChangeStdDev = SampleNoise(1.37f);
            skinConductanceResponseFrequencyStdDev = SampleNoise(2.61f);
            heartRateStdDev = SampleNoise(3.89f);
            interBeatIntervalStdDev = SampleNoise(5.23f);
            facialEmotionArousal = SampleNoise(6.47f);
            facialEmotionValence = SampleNoise(7.79f);
            engagement = SampleNoise(9.01f);

            return BuildManualSample();
        }

        private float SampleNoise(float channelOffset)
        {
            float x = noiseSeed + channelOffset;
            float y = noiseOffset * Mathf.Max(0.0001f, noiseFrequency) + channelOffset * 0.173f;
            return Mathf.Clamp(Mathf.PerlinNoise(x, y) * 2f - 1f, -1f, 1f);
        }

        private string ResolveDeviceId()
        {
            return string.IsNullOrWhiteSpace(deviceId) ? name : deviceId.Trim();
        }

        private void ClampInspectorValues()
        {
            tonicElectrodermalActivityStdDev = Mathf.Clamp(tonicElectrodermalActivityStdDev, -1f, 1f);
            temperatureRateOfChangeStdDev = Mathf.Clamp(temperatureRateOfChangeStdDev, -1f, 1f);
            skinConductanceResponseFrequencyStdDev = Mathf.Clamp(skinConductanceResponseFrequencyStdDev, -1f, 1f);
            heartRateStdDev = Mathf.Clamp(heartRateStdDev, -1f, 1f);
            interBeatIntervalStdDev = Mathf.Clamp(interBeatIntervalStdDev, -1f, 1f);
            facialEmotionArousal = Mathf.Clamp01(facialEmotionArousal);
            facialEmotionValence = Mathf.Clamp01(facialEmotionValence);
            engagement = Mathf.Clamp01(engagement);
        }
    }
}
