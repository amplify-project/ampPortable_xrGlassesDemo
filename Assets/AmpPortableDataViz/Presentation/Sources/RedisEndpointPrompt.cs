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
        [SerializeField] private bool useTouchScreenKeyboard = true;

        [Header("Events")]
        public UnityEvent OnEndpointReady = new UnityEvent();
        public UnityEvent<string> OnValidationError = new UnityEvent<string>();

        private TouchScreenKeyboard _keyboard;
        private TMP_InputField _activeInput;

        private void Awake()
        {
            PrefillFromSettings();

            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(HandleConfirm);
            }

            if (hostInput != null)
            {
                hostInput.onSelect.AddListener(_ => TryOpenKeyboard(hostInput));
            }

            if (portInput != null)
            {
                portInput.onSelect.AddListener(_ => TryOpenKeyboard(portInput));
            }
        }

        private void OnDestroy()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(HandleConfirm);
            }

            if (hostInput != null)
            {
                hostInput.onSelect.RemoveListener(_ => TryOpenKeyboard(hostInput));
            }

            if (portInput != null)
            {
                portInput.onSelect.RemoveListener(_ => TryOpenKeyboard(portInput));
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

            CloseKeyboard();
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

        private void Update()
        {
            if (_keyboard == null || _activeInput == null)
            {
                return;
            }

            if (_keyboard.status == TouchScreenKeyboard.Status.Canceled ||
                _keyboard.status == TouchScreenKeyboard.Status.Done ||
                !_keyboard.active)
            {
                CloseKeyboard();
                return;
            }

            _activeInput.text = _keyboard.text;
        }

        private void TryOpenKeyboard(TMP_InputField target)
        {
            if (!useTouchScreenKeyboard || target == null)
            {
                return;
            }

            // Only supported on mobile platforms; on others this is a no-op.
            _activeInput = target;
            _keyboard = TouchScreenKeyboard.Open(target.text, TouchScreenKeyboardType.Default, false, false, false, false, target.placeholder != null ? target.placeholder.GetComponent<TMP_Text>()?.text : string.Empty);
        }

        private void CloseKeyboard()
        {
            _keyboard = null;
            _activeInput = null;
        }
    }
}
