using System;
using System.Collections.Generic;
using AmpPortableDataViz.Application;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Infra;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Sources
{
    /// <summary>
    /// Connects decoded Redis observations to per-sensor trusted sources and timeout checks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SensorEngagementCoordinator : MonoBehaviour
    {
        private IDataSource<SensorEngagementObservation> _observationSource;
        private Action<DataFrame<SensorEngagementObservation>> _observationHandler;
        private SensorEngagementRouter _router;
        private IClock _clock;

        private void Awake()
        {
            _clock = new UnityClock();
        }

        private void OnEnable()
        {
            AttachSource();
        }

        private void Update()
        {
            EvaluateTimeouts();
        }

        private void OnDisable()
        {
            DetachSource();
        }

        public void Configure(
            IDataSource<SensorEngagementObservation> observationSource,
            IEnumerable<string> sensorIds,
            float timeoutSeconds,
            float neutralEngagement)
        {
            _clock ??= new UnityClock();
            bool wasEnabled = isActiveAndEnabled;
            if (wasEnabled)
            {
                DetachSource();
            }

            _observationSource = observationSource;
            _router = new SensorEngagementRouter(
                TimeSpan.FromSeconds(Mathf.Max(0f, timeoutSeconds)),
                neutralEngagement);

            if (sensorIds != null)
            {
                foreach (string sensorId in sensorIds)
                {
                    if (!string.IsNullOrWhiteSpace(sensorId))
                    {
                        _router.RegisterSensor(sensorId, _clock.UtcNowTicks);
                    }
                }
            }

            if (wasEnabled)
            {
                AttachSource();
            }
        }

        public ILatestDataSource<SensorEngagementState> GetSource(string sensorId)
        {
            return _router != null && _router.TryGetSource(sensorId, out SensorEngagementSource source)
                ? source
                : null;
        }

        internal void EvaluateTimeouts()
        {
            if (_router == null || _clock == null)
            {
                return;
            }

            _router.EvaluateTimeouts(_clock.UtcNowTicks, ResolveTimeoutClockTicks());
        }

        private void AttachSource()
        {
            if (_observationSource == null || _router == null)
            {
                return;
            }

            _observationHandler ??= OnObservation;
            _observationSource.OnFrame += _observationHandler;
        }

        private void DetachSource()
        {
            if (_observationSource != null && _observationHandler != null)
            {
                _observationSource.OnFrame -= _observationHandler;
            }

            _observationHandler = null;
        }

        private void OnObservation(DataFrame<SensorEngagementObservation> frame)
        {
            _router?.Process(frame.Payload, frame.TimestampTicksUtc, ResolveTimeoutClockTicks());
        }

        private long ResolveTimeoutClockTicks()
        {
            return (long)(_clock.SecondsSinceStartup * TimeSpan.TicksPerSecond);
        }
    }
}
