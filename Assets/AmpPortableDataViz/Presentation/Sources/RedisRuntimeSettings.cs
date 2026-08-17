using UnityEngine;

using System;
using System.Collections.Generic;
using AmpPortableDataViz.Core;

namespace AmpPortableDataViz.Presentation.Sources
{
    [CreateAssetMenu(fileName = "RedisRuntimeSettings", menuName = "Amp/Redis Runtime Settings", order = 0)]
    public sealed class RedisRuntimeSettings : ScriptableObject
    {
        private const string ResourcesPath = "RedisRuntimeSettings";
        private const string PlayerPrefsHostKey = "Redis.Host";
        private const string PlayerPrefsPortKey = "Redis.Port";
        private const string PlayerPrefsDiscoveredKey = "Redis.WasDiscovered";
        private const string PlayerPrefsServiceNameKey = "Redis.ServiceName";
        private const string PlayerPrefsTxtMetadataKey = "Redis.TxtMetadata";

        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] private int port = 6379;
        [SerializeField] private bool wasDiscovered;
        [SerializeField] private string serviceName = string.Empty;
        [SerializeField] private string txtMetadataJson = string.Empty;

        private static RedisRuntimeSettings _instance;

        public string Host
        {
            get => host;
            set => host = value;
        }

        public int Port
        {
            get => port;
            set => port = value;
        }

        public bool HasPersistedEndpoint =>
            PlayerPrefs.HasKey(PlayerPrefsHostKey) &&
            PlayerPrefs.HasKey(PlayerPrefsPortKey);

        public bool WasDiscovered => wasDiscovered;
        public string ServiceName => serviceName;

        public static RedisRuntimeSettings Instance
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }

                var loaded = Resources.Load<RedisRuntimeSettings>(ResourcesPath);
                if (loaded != null)
                {
                    _instance = loaded;
                    return _instance;
                }

                _instance = Create();
                return _instance;
            }
        }

        /// <summary>
        /// Loads settings from PlayerPrefs (if present) falling back to the Resource or default instance.
        /// </summary>
        public static RedisRuntimeSettings Load()
        {
            var settings = Instance;

            if (PlayerPrefs.HasKey(PlayerPrefsHostKey))
            {
                settings.host = PlayerPrefs.GetString(PlayerPrefsHostKey, settings.host);
            }

            if (PlayerPrefs.HasKey(PlayerPrefsPortKey))
            {
                settings.port = PlayerPrefs.GetInt(PlayerPrefsPortKey, settings.port);
            }

            settings.wasDiscovered = PlayerPrefs.GetInt(PlayerPrefsDiscoveredKey, 0) == 1;
            settings.serviceName = PlayerPrefs.GetString(PlayerPrefsServiceNameKey, string.Empty);
            settings.txtMetadataJson = PlayerPrefs.GetString(PlayerPrefsTxtMetadataKey, string.Empty);

            return settings;
        }

        /// <summary>
        /// Saves the current host/port to PlayerPrefs for next launch.
        /// </summary>
        public void Save()
        {
            PlayerPrefs.SetString(PlayerPrefsHostKey, host);
            PlayerPrefs.SetInt(PlayerPrefsPortKey, port);
            PlayerPrefs.SetInt(PlayerPrefsDiscoveredKey, wasDiscovered ? 1 : 0);
            PlayerPrefs.SetString(PlayerPrefsServiceNameKey, serviceName ?? string.Empty);
            PlayerPrefs.SetString(PlayerPrefsTxtMetadataKey, txtMetadataJson ?? string.Empty);
            PlayerPrefs.Save();
        }

        public void Apply(string newHost, int newPort)
        {
            host = newHost;
            port = newPort;
            wasDiscovered = false;
            serviceName = string.Empty;
            txtMetadataJson = string.Empty;
        }

        public void ApplyDiscovered(RedisServiceEndpoint endpoint)
        {
            if (endpoint == null || !endpoint.IsValid)
            {
                throw new ArgumentException("A valid discovered Redis endpoint is required.", nameof(endpoint));
            }

            host = endpoint.Host;
            port = endpoint.Port;
            wasDiscovered = true;
            serviceName = endpoint.ServiceName;
            txtMetadataJson = SerializeTxtRecords(endpoint.TxtRecords);
        }

        public RedisServiceEndpoint ToEndpoint()
        {
            return new RedisServiceEndpoint(host, port, serviceName, DeserializeTxtRecords(txtMetadataJson));
        }

        public static void SetInstance(RedisRuntimeSettings settings)
        {
            _instance = settings;
        }

        public static RedisRuntimeSettings Create(string newHost = "127.0.0.1", int newPort = 6379)
        {
            var settings = CreateInstance<RedisRuntimeSettings>();
            settings.Apply(newHost, newPort);
            return settings;
        }

        private void OnEnable()
        {
            if (_instance == null)
            {
                _instance = this;
            }
        }

        private static string SerializeTxtRecords(IReadOnlyDictionary<string, string> txtRecords)
        {
            var payload = new TxtMetadataPayload();
            if (txtRecords != null)
            {
                foreach (var pair in txtRecords)
                {
                    payload.Records.Add(new TxtMetadataRecord
                    {
                        Key = pair.Key,
                        Value = pair.Value
                    });
                }
            }

            return JsonUtility.ToJson(payload);
        }

        private static IReadOnlyDictionary<string, string> DeserializeTxtRecords(string json)
        {
            var records = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(json))
            {
                return records;
            }

            try
            {
                var payload = JsonUtility.FromJson<TxtMetadataPayload>(json);
                if (payload?.Records == null)
                {
                    return records;
                }

                foreach (var record in payload.Records)
                {
                    if (record != null && !string.IsNullOrWhiteSpace(record.Key))
                    {
                        records[record.Key] = record.Value ?? string.Empty;
                    }
                }
            }
            catch (ArgumentException)
            {
                // Ignore malformed legacy cache data and retain the endpoint itself.
            }

            return records;
        }

        [Serializable]
        private sealed class TxtMetadataPayload
        {
            public List<TxtMetadataRecord> Records = new List<TxtMetadataRecord>();
        }

        [Serializable]
        private sealed class TxtMetadataRecord
        {
            public string Key;
            public string Value;
        }
    }
}
