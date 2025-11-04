using System;
using System.Collections;
using System.Text;
using LiveKit;
using AmpPortableDataViz.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace AmpPortableDataViz.Infra
{
    public class LiveKitTelemetryService : IDisposable
    {
        private readonly Func<IEnumerator, Coroutine> _startCoroutine;
        private readonly string _tokenEndpoint;
        private readonly string _sandboxId;
        private readonly string _roomName;
        private readonly string _identity;

        private Room _room;

        public event Action<PoseSample> TelemetryReceived;

        public LiveKitTelemetryService(
            Func<IEnumerator, Coroutine> startCoroutine,
            string tokenEndpoint,
            string sandboxId,
            string roomName,
            string identity)
        {
            _startCoroutine = startCoroutine ?? throw new ArgumentNullException(nameof(startCoroutine));
            _tokenEndpoint = tokenEndpoint;
            _sandboxId = sandboxId;
            _roomName = roomName;
            _identity = identity;
        }

        public IEnumerator Connect()
        {
            Disconnect();

            var bodyObj = new SandboxRequest
            {
                room_name = _roomName,
                participant_name = _identity
            };
            var bodyJson = JsonUtility.ToJson(bodyObj);
            Debug.Log($"[LK] POST body: {bodyJson}");

            using var req = new UnityWebRequest(_tokenEndpoint, "POST");
            byte[] body = Encoding.UTF8.GetBytes(bodyJson);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("X-Sandbox-ID", _sandboxId);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Token endpoint failed: {req.responseCode} {req.error}\\n{req.downloadHandler.text}");
                yield break;
            }

            var resp = JsonUtility.FromJson<ConnectionResponse>(
                SanitizeForJsonUtility(req.downloadHandler.text));
            if (resp == null || string.IsNullOrEmpty(resp.participantToken) || string.IsNullOrEmpty(resp.serverUrl))
            {
                Debug.LogError("Unexpected response from sandbox endpoint:\n" + req.downloadHandler.text);
                yield break;
            }

            _room = new Room();

            _room.RegisterTextStreamHandler("telemetry", (reader, fromIdentity) =>
                _startCoroutine(HandleTelemetryStream(reader, fromIdentity)));

            var options = new RoomOptions
            {
                AutoSubscribe = true,
                AdaptiveStream = false,
                Dynacast = false
            };

            var connect = _room.Connect(resp.serverUrl, resp.participantToken, options);
            yield return connect;

            if (connect.IsError)
            {
                Debug.LogError("LiveKit connect failed.");
                yield break;
            }

            Debug.Log($"Connected as {_identity} ? room: {_roomName}");
        }

        private IEnumerator HandleTelemetryStream(TextStreamReader reader, string fromIdentity)
        {
            var readAll = reader.ReadAll();
            yield return readAll;
            var json = readAll.Text;

            Debug.Log($"[LK] telemetry stream from {fromIdentity}, bytes={json?.Length}");

            TryProcessTelemetryJson(fromIdentity, json);
        }

        internal bool TryProcessTelemetryJson(string fromIdentity, string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            var data = JsonUtility.FromJson<TelemetryPayload>(SanitizeForJsonUtility(json));
            if (data?.IsValid ?? false)
            {
                TelemetryReceived?.Invoke(data.ToPoseSample());
                return true;
            }

            return false;
        }

        public void Disconnect()
        {
            if (_room == null)
            {
                return;
            }

            try
            {
                _room.Disconnect();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"LiveKit disconnect threw: {ex}");
            }
            finally
            {
                _room = null;
            }
        }

        public void Dispose()
        {
            Disconnect();
            TelemetryReceived = null;
        }

        private static string SanitizeForJsonUtility(string s)
        {
            return s;
        }

        [Serializable]
        private class SandboxRequest
        {
            public string room_name;
            public string participant_name;
        }

        [Serializable]
        private class ConnectionResponse
        {
            public string serverUrl;
            public string roomName;
            public string participantName;
            public string participantToken;
        }

        [Serializable]
        private class TelemetryPayload
        {
            public float[] position;
            public float[] rotation;
            public long timestamp;

            public bool IsValid => position?.Length == 3 && rotation?.Length == 4;

            public PoseSample ToPoseSample()
            {
                return new PoseSample(
                    new Vector3(position[0], position[1], position[2]),
                    new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]),
                    timestamp);
            }
        }
    }
}
