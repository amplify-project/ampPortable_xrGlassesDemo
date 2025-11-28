using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AmpPortableDataViz.Presentation.Sources
{
    /// <summary>
    /// Simple startup UI to collect Redis host/port from the user, persist them, and notify listeners.
    /// </summary>
    public sealed class RedisEndpointPrompt : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField hostInput;
        [SerializeField] private TMP_InputField portInput;
        [SerializeField] private Button confirmButton;
        [SerializeField] private GameObject rootPanel;

        [Header("Events")]
        public UnityEvent OnEndpointReady = new UnityEvent();
        public UnityEvent<string> OnValidationError = new UnityEvent<string>();

        private void Awake()
        {
            PrefillFromSettings();

            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(HandleConfirm);
            }
        }

        private void OnDestroy()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(HandleConfirm);
            }
        }

        private void PrefillFromSettings()
        {
            var settings = RedisRuntimeSettings.Load();
            if (hostInput != null)
            {
                hostInput.text = settings.Host;
            }

            if (portInput != null)
            {
                portInput.text = settings.Port.ToString();
            }
        }

        private void HandleConfirm()
        {
            string hostValue = hostInput != null ? hostInput.text.Trim() : string.Empty;
            string portText = portInput != null ? portInput.text.Trim() : string.Empty;

            if (!Validate(hostValue, portText, out var portValue))
            {
                return;
            }

            var settings = RedisRuntimeSettings.Instance;
            settings.Apply(hostValue, portValue);
            settings.Save();
            RedisRuntimeSettings.SetInstance(settings);

            OnEndpointReady?.Invoke();

            if (rootPanel != null)
            {
                rootPanel.SetActive(false);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private bool Validate(string hostValue, string portText, out int portValue)
        {
            portValue = 0;

            if (string.IsNullOrWhiteSpace(hostValue))
            {
                RaiseValidationError("Host is required.");
                return false;
            }

            if (!int.TryParse(portText, out portValue) || portValue <= 0 || portValue > 65535)
            {
                RaiseValidationError("Port must be a number between 1 and 65535.");
                return false;
            }

            return true;
        }

        private void RaiseValidationError(string message)
        {
            Debug.LogWarning($"RedisEndpointPrompt: {message}");
            OnValidationError?.Invoke(message);
        }
    }
}
