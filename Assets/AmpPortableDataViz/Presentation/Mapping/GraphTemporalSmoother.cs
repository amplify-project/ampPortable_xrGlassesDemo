using System;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Mapping
{
    /// <summary>
    /// Applies time-aware exponential smoothing to a single graph value stream.
    /// </summary>
    public sealed class GraphTemporalSmoother
    {
        private bool _hasValue;
        private float _currentValue;
        private long _lastTimestampTicksUtc;

        public void Reset()
        {
            _hasValue = false;
            _currentValue = 0f;
            _lastTimestampTicksUtc = 0L;
        }

        public float Apply(float value, long timestampTicksUtc, float halfLifeSeconds)
        {
            if (!_hasValue || halfLifeSeconds <= 0f)
            {
                Seed(value, timestampTicksUtc);
                return value;
            }

            long elapsedTicks = timestampTicksUtc - _lastTimestampTicksUtc;
            if (elapsedTicks <= 0L)
            {
                return _currentValue;
            }

            float elapsedSeconds = (float)(elapsedTicks / (double)TimeSpan.TicksPerSecond);
            float blend = 1f - Mathf.Pow(0.5f, elapsedSeconds / halfLifeSeconds);
            _currentValue = Mathf.Lerp(_currentValue, value, blend);
            _lastTimestampTicksUtc = timestampTicksUtc;
            return _currentValue;
        }

        private void Seed(float value, long timestampTicksUtc)
        {
            _hasValue = true;
            _currentValue = value;
            _lastTimestampTicksUtc = timestampTicksUtc;
        }
    }
}
