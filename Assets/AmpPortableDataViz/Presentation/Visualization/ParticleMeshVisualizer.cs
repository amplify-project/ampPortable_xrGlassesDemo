using AmpPortableDataViz.Core;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Visualization
{
    /// <summary>
    /// Temporary receiver for ParticleMesh signal frames while the visual behavior is being designed.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleMeshManualDriver))]
    [AddComponentMenu("Amp Portable Data Viz/Visualization/Particle Mesh Visualizer")]
    public sealed class ParticleMeshVisualizer : MonoBehaviour, IVisualizer<ParticleMeshSignalSample>
    {
        [Header("Source")]
        [SerializeField] private ParticleMeshManualDriver manualDriver;

        [Header("Diagnostics")]
        [SerializeField] private bool logReceivedSamples = true;
        [SerializeField, Range(0f, 5f)] private float minimumLogIntervalSeconds = 0.5f;

        private double _nextLogTime;

        private void Awake()
        {
            ResolveManualDriver();
        }

        private void OnEnable()
        {
            ResolveManualDriver();

            if (manualDriver == null)
            {
                Debug.LogWarning($"{nameof(ParticleMeshVisualizer)} requires a {nameof(ParticleMeshManualDriver)} on the same GameObject.", this);
                return;
            }

            manualDriver.OnFrame += OnManualDriverFrame;

            if (manualDriver.HasLatestFrame)
            {
                OnManualDriverFrame(manualDriver.LatestFrame);
            }
        }

        private void OnDisable()
        {
            if (manualDriver != null)
            {
                manualDriver.OnFrame -= OnManualDriverFrame;
            }
        }

        public void Apply(in ParticleMeshSignalSample parameters, long timestampTicksUtc)
        {
            ReceiveSample(parameters, timestampTicksUtc, -1);
        }

        private void OnManualDriverFrame(DataFrame<ParticleMeshSignalSample> frame)
        {
            ReceiveSample(frame.Payload, frame.TimestampTicksUtc, frame.SequenceId);
        }

        private void ResolveManualDriver()
        {
            if (manualDriver == null)
            {
                manualDriver = GetComponent<ParticleMeshManualDriver>();
            }
        }

        private void ReceiveSample(in ParticleMeshSignalSample sample, long timestampTicksUtc, int sequenceId)
        {
            if (!logReceivedSamples || !ShouldLogNow())
            {
                return;
            }

            string sequenceText = sequenceId >= 0 ? $"seq={sequenceId}" : "seq=n/a";
            Debug.Log($"{nameof(ParticleMeshVisualizer)} received {sequenceText} ts={timestampTicksUtc} {sample}", this);
        }

        private bool ShouldLogNow()
        {
            if (!UnityEngine.Application.isPlaying || minimumLogIntervalSeconds <= 0f)
            {
                return true;
            }

            double now = Time.unscaledTimeAsDouble;
            if (now < _nextLogTime)
            {
                return false;
            }

            _nextLogTime = now + minimumLogIntervalSeconds;
            return true;
        }
    }
}
