using System;
using System.Collections.Generic;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Mapping
{
    /// <summary>
    /// Tracks a graph's visible Y range, expanding quickly for new extremes and
    /// contracting gradually when the visible samples occupy a smaller range.
    /// </summary>
    public sealed class DynamicGraphYRangeTracker
    {
        private const float MinimumRange = 1e-4f;

        private bool _hasRange;
        private float _currentMin;
        private float _currentMax;
        private long _lastTimestampTicksUtc;

        public void Reset()
        {
            _hasRange = false;
            _currentMin = 0f;
            _currentMax = 0f;
            _lastTimestampTicksUtc = 0L;
        }

        public void Resolve(
            IReadOnlyList<Vector2> samples,
            float hardMin,
            float hardMax,
            float stdDevMultiplier,
            float minimumRangeFraction,
            float paddingFraction,
            float zoomOutTimeSeconds,
            float zoomInTimeSeconds,
            long timestampTicksUtc,
            out float yMin,
            out float yMax)
        {
            NormalizeRange(ref hardMin, ref hardMax);

            if (samples == null || samples.Count == 0)
            {
                yMin = hardMin;
                yMax = hardMax;
                return;
            }

            float observedMin = float.PositiveInfinity;
            float observedMax = float.NegativeInfinity;
            double sum = 0d;
            double sumSquares = 0d;
            int validCount = 0;

            for (int i = 0; i < samples.Count; i++)
            {
                float value = samples[i].y;
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    continue;
                }

                observedMin = Mathf.Min(observedMin, value);
                observedMax = Mathf.Max(observedMax, value);
                sum += value;
                sumSquares += (double)value * value;
                validCount++;
            }

            if (validCount == 0)
            {
                yMin = hardMin;
                yMax = hardMax;
                return;
            }

            observedMin = Mathf.Clamp(observedMin, hardMin, hardMax);
            observedMax = Mathf.Clamp(observedMax, hardMin, hardMax);

            float hardRange = hardMax - hardMin;
            float minimumSpan = Mathf.Max(MinimumRange, hardRange * Mathf.Clamp01(minimumRangeFraction));
            float observedSpan = Mathf.Max(0f, observedMax - observedMin);
            float paddedSpan = observedSpan * (1f + (2f * Mathf.Max(0f, paddingFraction)));

            double mean = sum / validCount;
            double variance = Math.Max(0d, (sumSquares / validCount) - (mean * mean));
            float stdDevSpan = 2f * Mathf.Sqrt((float)variance) * Mathf.Max(0f, stdDevMultiplier);
            float targetSpan = Mathf.Clamp(
                Mathf.Max(minimumSpan, Mathf.Max(paddedSpan, stdDevSpan)),
                minimumSpan,
                hardRange);

            float targetCentre = (observedMin + observedMax) * 0.5f;
            float targetMin = targetCentre - (targetSpan * 0.5f);
            float targetMax = targetCentre + (targetSpan * 0.5f);
            ShiftInsideHardBounds(ref targetMin, ref targetMax, hardMin, hardMax);

            if (!_hasRange)
            {
                _currentMin = targetMin;
                _currentMax = targetMax;
                _hasRange = true;
            }
            else
            {
                bool requiresZoomOut = targetMin < _currentMin || targetMax > _currentMax;
                float timeConstant = requiresZoomOut ? zoomOutTimeSeconds : zoomInTimeSeconds;
                float deltaTime = ResolveDeltaTime(timestampTicksUtc);
                float smoothing = ResolveSmoothing(deltaTime, timeConstant);

                _currentMin = Mathf.Lerp(_currentMin, targetMin, smoothing);
                _currentMax = Mathf.Lerp(_currentMax, targetMax, smoothing);

                // Never allow smoothing to clip a currently visible sample.
                _currentMin = Mathf.Min(_currentMin, observedMin);
                _currentMax = Mathf.Max(_currentMax, observedMax);
                ShiftInsideHardBounds(ref _currentMin, ref _currentMax, hardMin, hardMax);
            }

            _lastTimestampTicksUtc = timestampTicksUtc;
            yMin = _currentMin;
            yMax = _currentMax;
            NormalizeRange(ref yMin, ref yMax);
        }

        private float ResolveDeltaTime(long timestampTicksUtc)
        {
            if (_lastTimestampTicksUtc <= 0L || timestampTicksUtc <= _lastTimestampTicksUtc)
            {
                return 0f;
            }

            return (float)((timestampTicksUtc - _lastTimestampTicksUtc) / (double)TimeSpan.TicksPerSecond);
        }

        private static float ResolveSmoothing(float deltaTime, float timeConstant)
        {
            if (timeConstant <= 0f)
            {
                return 1f;
            }

            if (deltaTime <= 0f)
            {
                return 0f;
            }

            return 1f - Mathf.Exp(-deltaTime / timeConstant);
        }

        private static void ShiftInsideHardBounds(ref float min, ref float max, float hardMin, float hardMax)
        {
            if (min < hardMin)
            {
                float shift = hardMin - min;
                min += shift;
                max += shift;
            }

            if (max > hardMax)
            {
                float shift = max - hardMax;
                min -= shift;
                max -= shift;
            }

            min = Mathf.Clamp(min, hardMin, hardMax);
            max = Mathf.Clamp(max, hardMin, hardMax);
            NormalizeRange(ref min, ref max);
        }

        private static void NormalizeRange(ref float min, ref float max)
        {
            if (max < min)
            {
                (min, max) = (max, min);
            }

            if (max - min < MinimumRange)
            {
                max = min + MinimumRange;
            }
        }
    }
}
