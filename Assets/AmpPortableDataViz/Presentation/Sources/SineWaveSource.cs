using System;
using AmpPortableDataViz.Core;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Sources
{
    public sealed class SineWaveSource : MonoBehaviour, IDataSource<float>
    {
        public string SourceId => gameObject.name;
        public event Action<DataFrame<float>> OnFrame;

        [Header("Signal Parameters")]
        public float FrequencyHz = 0.5f;
        public float Amplitude = 1.0f;
        public float Bias = 0.5f;

        private IClock _clock;
        private int _sequenceIdCounter;

        private void Awake()
        {
            _clock = new AmpPortableDataViz.Infra.UnityClock();
        }

        private void Update()
        {
            double secondsSinceStartup = _clock.SecondsSinceStartup;
            float sineValue = Mathf.Sin((float)(2 * Math.PI * FrequencyHz * secondsSinceStartup));
            float normalizedValue = Bias + Amplitude * 0.5f * (1f + sineValue);

            var dataFrame = new DataFrame<float>(_clock.UtcNowTicks, _sequenceIdCounter++, normalizedValue);
            OnFrame?.Invoke(dataFrame);
        }
    }
}
