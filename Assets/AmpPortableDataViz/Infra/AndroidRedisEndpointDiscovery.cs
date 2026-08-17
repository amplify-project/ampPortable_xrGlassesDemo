using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AmpPortableDataViz.Core;
using UnityEngine;
using UnityEngine.Scripting;

namespace AmpPortableDataViz.Infra
{
    public sealed class AndroidRedisEndpointDiscovery : IRedisEndpointDiscovery
    {
        public const string ServiceType = "_amplify-redis._tcp";

        private readonly IReadOnlyList<RedisTxtRecordRequirement> _requirements;
        private TaskCompletionSource<RedisServiceEndpoint> _completion;
        private GameObject _callbackObject;
        private AndroidRedisDiscoveryCallbackReceiver _receiver;
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _nativeDiscovery;
#endif
        private bool _disposed;

        public AndroidRedisEndpointDiscovery(IReadOnlyList<RedisTxtRecordRequirement> requirements = null)
        {
            _requirements = requirements ?? Array.Empty<RedisTxtRecordRequirement>();
        }

        public static bool IsSupported
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public async Task<RedisServiceEndpoint> DiscoverAsync(int timeoutMs, CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AndroidRedisEndpointDiscovery));
            }

            if (_completion != null)
            {
                throw new InvalidOperationException("Redis endpoint discovery is already running.");
            }

            int effectiveTimeoutMs = Mathf.Max(1, timeoutMs);
            var completion = new TaskCompletionSource<RedisServiceEndpoint>();
            _completion = completion;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                StartNativeDiscovery();
                var timeoutTask = Task.Delay(effectiveTimeoutMs, cancellationToken);
                var completedTask = await Task.WhenAny(completion.Task, timeoutTask);
                cancellationToken.ThrowIfCancellationRequested();
                return completedTask == completion.Task ? await completion.Task : null;
            }
            finally
            {
                StopNativeDiscovery();
                if (ReferenceEquals(_completion, completion))
                {
                    _completion = null;
                }
            }
#else
            await Task.Yield();
            if (ReferenceEquals(_completion, completion))
            {
                _completion = null;
            }
            return null;
#endif
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            StopNativeDiscovery();
#endif
            _completion?.TrySetResult(null);
        }

        internal void HandleResolvedService(string json)
        {
            if (_disposed || _completion == null || string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            try
            {
                var payload = JsonUtility.FromJson<ResolvedServicePayload>(json);
                if (payload == null)
                {
                    return;
                }

                var txtRecords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (payload.txt != null)
                {
                    foreach (var pair in payload.txt)
                    {
                        if (pair != null && !string.IsNullOrWhiteSpace(pair.key))
                        {
                            txtRecords[pair.key] = pair.value ?? string.Empty;
                        }
                    }
                }

                var endpoint = new RedisServiceEndpoint(payload.host, payload.port, payload.serviceName, txtRecords);
                if (!RedisServiceMetadataValidator.IsCompatible(endpoint, _requirements))
                {
                    Debug.LogWarning($"AndroidRedisEndpointDiscovery: Ignoring incompatible service '{payload.serviceName}'.");
                    return;
                }

                Debug.Log($"AndroidRedisEndpointDiscovery: Resolved compatible Redis service '{endpoint.ServiceName}' at {endpoint.Host}:{endpoint.Port}.");
                _completion.TrySetResult(endpoint);
            }
            catch (ArgumentException ex)
            {
                Debug.LogWarning($"AndroidRedisEndpointDiscovery: Could not parse resolved service data ({ex.Message}).");
            }
        }

        internal void HandleDiscoveryError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Debug.LogWarning($"AndroidRedisEndpointDiscovery: {message}");
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void StartNativeDiscovery()
        {
            _callbackObject = new GameObject($"RedisNsdCallback-{Guid.NewGuid():N}");
            UnityEngine.Object.DontDestroyOnLoad(_callbackObject);
            _receiver = _callbackObject.AddComponent<AndroidRedisDiscoveryCallbackReceiver>();
            _receiver.Initialize(this);

            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            _nativeDiscovery = new AndroidJavaObject(
                "com.amplify.discovery.RedisNsdDiscovery",
                activity,
                _callbackObject.name);
            _nativeDiscovery.Call("start", ServiceType);
        }

        private void StopNativeDiscovery()
        {
            if (_nativeDiscovery != null)
            {
                try
                {
                    _nativeDiscovery.Call("stop");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"AndroidRedisEndpointDiscovery: Failed to stop native discovery cleanly ({ex.Message}).");
                }

                _nativeDiscovery.Dispose();
                _nativeDiscovery = null;
            }

            _receiver = null;
            if (_callbackObject != null)
            {
                UnityEngine.Object.Destroy(_callbackObject);
                _callbackObject = null;
            }
        }
#endif

        [Serializable]
        [Preserve]
        private sealed class ResolvedServicePayload
        {
            public string serviceName;
            public string host;
            public int port;
            public TxtRecordPayload[] txt;
        }

        [Serializable]
        [Preserve]
        private sealed class TxtRecordPayload
        {
            public string key;
            public string value;
        }
    }

    [Preserve]
    internal sealed class AndroidRedisDiscoveryCallbackReceiver : MonoBehaviour
    {
        private AndroidRedisEndpointDiscovery _owner;

        public void Initialize(AndroidRedisEndpointDiscovery owner)
        {
            _owner = owner;
        }

        [Preserve]
        public void OnRedisServiceResolved(string json)
        {
            _owner?.HandleResolvedService(json);
        }

        [Preserve]
        public void OnRedisDiscoveryError(string message)
        {
            _owner?.HandleDiscoveryError(message);
        }
    }
}
