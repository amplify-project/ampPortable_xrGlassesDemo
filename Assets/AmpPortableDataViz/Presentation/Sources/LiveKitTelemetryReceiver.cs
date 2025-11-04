using System;
using System.Collections;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Infra;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Sources
{
    [DisallowMultipleComponent]
    public sealed class LiveKitTelemetryReceiver : MonoBehaviour, IDataSource<PoseSample>
    {
        [Header("Sandbox token endpoint + header")]
        public string tokenEndpoint = "https://cloud-api.livekit.io/api/sandbox/connection-details";
        public string sandboxId = "amp-portable-viz-16o67i";

        [Header("Room + identity to request")]
        public string roomName = "xr-room";
        public string identity = "xreal-receiver-1";   // make this unique per headset

        private LiveKitTelemetryService _telemetryService;
        private IClock _clock;
        private int _sequenceId;

        public string SourceId => string.IsNullOrEmpty(identity) ? gameObject.name : identity;
        public event Action<DataFrame<PoseSample>> OnFrame;

        private void Awake()
        {
            _clock = new UnityClock();
        }

        private IEnumerator Start()
        {
            _telemetryService = new LiveKitTelemetryService(StartCoroutine, tokenEndpoint, sandboxId, roomName, identity);
            _telemetryService.TelemetryReceived += OnTelemetryReceived;

            yield return _telemetryService.Connect();
        }

        private void OnTelemetryReceived(PoseSample sample)
        {
            var dataFrame = new DataFrame<PoseSample>(_clock.UtcNowTicks, _sequenceId++, sample);

            OnFrame?.Invoke(dataFrame);
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void OnApplicationQuit()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            if (_telemetryService == null)
            {
                return;
            }

            _telemetryService.TelemetryReceived -= OnTelemetryReceived;
            _telemetryService.Dispose();
            _telemetryService = null;
        }
    }
}
