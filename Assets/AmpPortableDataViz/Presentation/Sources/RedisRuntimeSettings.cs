using UnityEngine;

namespace AmpPortableDataViz.Presentation.Sources
{
    [CreateAssetMenu(fileName = "RedisRuntimeSettings", menuName = "Amp/Redis Runtime Settings", order = 0)]
    public sealed class RedisRuntimeSettings : ScriptableObject
    {
        private const string ResourcesPath = "RedisRuntimeSettings";
        private const string PlayerPrefsHostKey = "Redis.Host";
        private const string PlayerPrefsPortKey = "Redis.Port";

        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] private int port = 6379;

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

            return settings;
        }

        /// <summary>
        /// Saves the current host/port to PlayerPrefs for next launch.
        /// </summary>
        public void Save()
        {
            PlayerPrefs.SetString(PlayerPrefsHostKey, host);
            PlayerPrefs.SetInt(PlayerPrefsPortKey, port);
            PlayerPrefs.Save();
        }

        public void Apply(string newHost, int newPort)
        {
            host = newHost;
            port = newPort;
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
    }
}
