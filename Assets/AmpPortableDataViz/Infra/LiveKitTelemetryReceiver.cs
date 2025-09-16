using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking; // for UnityWebRequest
using LiveKit;

[DisallowMultipleComponent]
public class LiveKitTelemetryReceiver : MonoBehaviour
{
    [Header("Sandbox token endpoint + header")]
    public string tokenEndpoint = "https://cloud-api.livekit.io/api/sandbox/connection-details";
    public string sandboxId = "amp-portable-viz-16o67i";

    [Header("Room + identity to request")]
    public string roomName = "xr-room";
    public string identity = "xreal-receiver-1";   // make this unique per headset

    [Header("What to move with incoming data")]
    public Transform target;                      // assign a cube or anchor in scene

    private Room _room;

    [System.Serializable]
    class TelemetryPayload
    {
        public float[] position;
        public float[] rotation; // [qx,qy,qz,qw]
        public long timestamp;
    }

    [System.Serializable]
    class SandboxRequest
    {
        public string room_name;
        public string participant_name;
    }

    IEnumerator Start()
    {
        // 1) Ask sandbox endpoint for connection details (token + ws url)
        var bodyObj = new SandboxRequest
        {
            room_name = roomName,
            participant_name = identity
        };
        var bodyJson = JsonUtility.ToJson(bodyObj);
        Debug.Log($"[LK] POST body: {bodyJson}"); // should print: {"room_name":"xr-room","participant_name":"xreal-receiver-1"}

        using var req = new UnityWebRequest(tokenEndpoint, "POST");
        byte[] body = Encoding.UTF8.GetBytes(bodyJson);
        req.uploadHandler = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("X-Sandbox-ID", sandboxId);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Token endpoint failed: {req.responseCode} {req.error}\n{req.downloadHandler.text}");
            yield break;
        }
            
        // Response looks like:
        // { "serverUrl": "...", "participantName": "...", "roomName": "...", "participantToken": "..." }
        var resp = JsonUtility.FromJson<ConnectionResponse>(
            SanitizeForJsonUtility(req.downloadHandler.text));
        if (resp == null || string.IsNullOrEmpty(resp.participantToken) || string.IsNullOrEmpty(resp.serverUrl))
        {
            Debug.LogError("Unexpected response from sandbox endpoint:\n" + req.downloadHandler.text);
            yield break;
        }

        // 2) Connect to LiveKit
        _room = new Room();

        // Register a text-stream handler for our topic BEFORE connecting
        _room.RegisterTextStreamHandler("telemetry", (reader, fromIdentity) =>
            StartCoroutine(HandleTelemetryStream(reader, fromIdentity)));

        var options = new RoomOptions
        {
            // these are sensible defaults; tweak as you like
            AutoSubscribe = true,   // auto-subscribe to tracks on join
            AdaptiveStream = false, // leave off if you're not rendering video
            Dynacast = false        // only useful when publishing multi-layer video
        };

        var connect = _room.Connect(resp.serverUrl, resp.participantToken, options);
        yield return connect;

        if (connect.IsError)
        {
            Debug.LogError("LiveKit connect failed.");
            yield break;
        }

        Debug.Log($"Connected as {identity} ? room: {roomName}");
    }

    // Reads entire text stream, parses JSON, applies to Transform
    IEnumerator HandleTelemetryStream(TextStreamReader reader, string fromIdentity)
    {
        var readAll = reader.ReadAll();
        yield return readAll;
        var json = readAll.Text;

        Debug.Log($"[LK] telemetry stream from {fromIdentity}, bytes={json?.Length}");

        // Parse payload
        var data = JsonUtility.FromJson<TelemetryPayload>(SanitizeForJsonUtility(json));
        if (data?.position?.Length == 3 && data?.rotation?.Length == 4 && target != null)
        {
            // Apply (Unity main thread)
            target.localPosition = new Vector3(data.position[0], data.position[1], data.position[2]);
            target.localRotation = new Quaternion(data.rotation[0], data.rotation[1], data.rotation[2], data.rotation[3]);
        }
    }

    // JsonUtility is strict about property names/quotes; ensure it's valid JSON
    static string SanitizeForJsonUtility(string s)
    {
        // If the sandbox returns camelCase keys we care about below, no changes needed.
        return s;
    }

    [System.Serializable]
    class ConnectionResponse
    {
        public string serverUrl;
        public string roomName;
        public string participantName;
        public string participantToken;
    }

    private void OnDestroy()
    {
        TryDisconnect();
    }

    private void OnApplicationQuit()
    {
        TryDisconnect();
    }

    private void TryDisconnect()
    {
        if (_room != null)
        {
            try
            {
                _room.Disconnect();   // returns void in your SDK version
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"LiveKit disconnect threw: {ex}");
            }
            _room = null;
        }
    }


    IEnumerator Disconnect()
    {
        _room?.Disconnect();   // no assignment, no yield
        yield break;
    }
}