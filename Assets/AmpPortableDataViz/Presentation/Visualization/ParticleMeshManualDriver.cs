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
        [SerializeField, Range(0f, 1f)] private float tonicElectrodermalActivityStdDev = 0.5f;
        [SerializeField, Range(0f, 1f)] private float temperatureRateOfChangeStdDev = 0.5f;
        [SerializeField, Range(0f, 1f)] private float skinConductanceResponseFrequencyStdDev = 0.5f;
        [SerializeField, Range(0f, 1f)] private float heartRateStdDev = 0.5f;
        [SerializeField, Range(0f, 1f)] private float interBeatIntervalStdDev = 0.5f;

        [Header("Facial Emotion")]
        [SerializeField, Range(0f, 1f)] private float facialEmotion = 0.5f;

        [Header("Engagement")]
        [SerializeField, Range(0f, 1f)] private float engagementArousal = 0.5f;
        [SerializeField, Range(0f, 1f)] private float engagementValence = 0.5f;

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
        [SerializeField] private UnityEvent<float> onFacialEmotion = new();
        [SerializeField] private Vector2Event onEngagementArousalValence = new();

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
            onFacialEmotion?.Invoke(sample.FacialEmotion);
            onEngagementArousalValence?.Invoke(new Vector2(sample.EngagementArousal, sample.EngagementValence));

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
            float facialEmotionValue,
            float arousal,
            float valence)
        {
            sourceMode = SourceMode.Manual;
            tonicElectrodermalActivityStdDev = Mathf.Clamp01(tonicEda);
            temperatureRateOfChangeStdDev = Mathf.Clamp01(temperatureRateOfChange);
            skinConductanceResponseFrequencyStdDev = Mathf.Clamp01(skinConductanceResponseFrequency);
            heartRateStdDev = Mathf.Clamp01(heartRate);
            interBeatIntervalStdDev = Mathf.Clamp01(interBeatInterval);
            facialEmotion = Mathf.Clamp01(facialEmotionValue);
            engagementArousal = Mathf.Clamp01(arousal);
            engagementValence = Mathf.Clamp01(valence);
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
                facialEmotion,
                engagementArousal,
                engagementValence);
        }

        private ParticleMeshSignalSample BuildProceduralSample()
        {
            tonicElectrodermalActivityStdDev = SampleNoise(0.11f);
            temperatureRateOfChangeStdDev = SampleNoise(1.37f);
            skinConductanceResponseFrequencyStdDev = SampleNoise(2.61f);
            heartRateStdDev = SampleNoise(3.89f);
            interBeatIntervalStdDev = SampleNoise(5.23f);
            facialEmotion = SampleNoise(6.47f);
            engagementArousal = SampleNoise(7.79f);
            engagementValence = SampleNoise(9.01f);

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
            tonicElectrodermalActivityStdDev = Mathf.Clamp01(tonicElectrodermalActivityStdDev);
            temperatureRateOfChangeStdDev = Mathf.Clamp01(temperatureRateOfChangeStdDev);
            skinConductanceResponseFrequencyStdDev = Mathf.Clamp01(skinConductanceResponseFrequencyStdDev);
            heartRateStdDev = Mathf.Clamp01(heartRateStdDev);
            interBeatIntervalStdDev = Mathf.Clamp01(interBeatIntervalStdDev);
            facialEmotion = Mathf.Clamp01(facialEmotion);
            engagementArousal = Mathf.Clamp01(engagementArousal);
            engagementValence = Mathf.Clamp01(engagementValence);
        }
    }
}
