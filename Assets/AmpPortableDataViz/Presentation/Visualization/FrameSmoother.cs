using System;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Visualization
{
    /// <summary>
    /// Lightweight helper that blends between parameter snapshots at the render framerate.
    /// </summary>
    internal sealed class FrameSmoother<T>
    {
        private readonly Func<T, T, float, T> _lerp;

        private bool _hasValue;
        private T _current;
        private T _start;
        private T _target;
        private float _progress;
        private long _targetTimestamp;

        public FrameSmoother(Func<T, T, float, T> lerp)
        {
            _lerp = lerp ?? throw new ArgumentNullException(nameof(lerp));
            Reset();
        }

        public bool HasValue => _hasValue;
        public bool IsInterpolating => _hasValue && _progress < 1f;
        public T Current => _current;
        public long TargetTimestamp => _targetTimestamp;

        public void Reset()
        {
            _hasValue = false;
            _progress = 1f;
            _current = default;
            _start = default;
            _target = default;
            _targetTimestamp = 0;
        }

        public void SetTarget(in T target, long timestamp)
        {
            if (!_hasValue)
            {
                _current = target;
                _start = target;
                _target = target;
                _progress = 1f;
                _hasValue = true;
            }
            else
            {
                _start = _current;
                _target = target;
                _progress = 0f;
            }

            _targetTimestamp = timestamp;
        }

        public void Step(float deltaTime, float durationSeconds)
        {
            if (!_hasValue)
            {
                return;
            }

            if (durationSeconds <= 0f)
            {
                _progress = 1f;
                _current = _target;
                return;
            }

            if (_progress < 1f)
            {
                float t = deltaTime / Mathf.Max(durationSeconds, 1e-4f);
                _progress = Mathf.Clamp01(_progress + t);
                _current = (_progress >= 1f) ? _target : _lerp(_start, _target, _progress);
            }
        }
    }
}
