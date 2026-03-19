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
        [SerializeField] private TMP_Text validationLabel;
        [SerializeField] private bool useTouchScreenKeyboard = true;
        [SerializeField] private bool autoCreateValidationLabel = true;
        [SerializeField] private Color validationLabelColor = new Color(0.9f, 0.3f, 0.3f, 1f);

        [Header("Events")]
        public UnityEvent OnEndpointReady = new UnityEvent();
        public UnityEvent<string> OnValidationError = new UnityEvent<string>();

        private TouchScreenKeyboard _keyboard;
        private TMP_InputField _activeInput;

        private void Awake()
        {
            PrefillFromSettings();
            EnsureValidationLabel();
            ClearValidationError();

            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(HandleConfirm);
            }

            if (hostInput != null)
            {
                hostInput.onSelect.AddListener(HandleHostSelected);
            }

            if (portInput != null)
            {
                portInput.onSelect.AddListener(HandlePortSelected);
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
                hostInput.onSelect.RemoveListener(HandleHostSelected);
            }

            if (portInput != null)
            {
                portInput.onSelect.RemoveListener(HandlePortSelected);
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

            ClearValidationError();
            Debug.Log($"RedisEndpointPrompt: Confirm pressed with host '{hostValue}' and port '{portText}'.");

            if (!Validate(hostValue, portText, out var portValue))
            {
                return;
            }

            var settings = RedisRuntimeSettings.Instance;
            settings.Apply(hostValue, portValue);
            settings.Save();
            RedisRuntimeSettings.SetInstance(settings);
            Debug.Log($"RedisEndpointPrompt: Saved Redis endpoint {hostValue}:{portValue}.");

            try
            {
                Debug.Log("RedisEndpointPrompt: Invoking OnEndpointReady.");
                OnEndpointReady?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }
            finally
            {
                HidePanel();
                CloseKeyboard();
                Debug.Log("RedisEndpointPrompt: Startup panel hidden.");
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
            if (validationLabel != null)
            {
                validationLabel.text = message;
                validationLabel.gameObject.SetActive(true);
            }

            Debug.LogWarning($"RedisEndpointPrompt: {message}");
            OnValidationError?.Invoke(message);
        }

        private void ClearValidationError()
        {
            if (validationLabel == null)
            {
                return;
            }

            validationLabel.text = string.Empty;
            validationLabel.gameObject.SetActive(false);
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

        private void HandleHostSelected(string _)
        {
            TryOpenKeyboard(hostInput);
        }

        private void HandlePortSelected(string _)
        {
            TryOpenKeyboard(portInput);
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

        private void HidePanel()
        {
            if (rootPanel != null)
            {
                rootPanel.SetActive(false);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void EnsureValidationLabel()
        {
            if (validationLabel != null)
            {
                return;
            }

            var existingLabel = transform.Find("ValidationMessage");
            if (existingLabel != null)
            {
                validationLabel = existingLabel.GetComponent<TMP_Text>();
            }

            if (validationLabel != null || !autoCreateValidationLabel)
            {
                return;
            }

            var parentRect = transform as RectTransform;
            if (parentRect == null)
            {
                return;
            }

            var labelObject = new GameObject("ValidationMessage", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rectTransform = labelObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parentRect, false);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(0f, -72f);
            rectTransform.sizeDelta = new Vector2(220f, 40f);

            validationLabel = labelObject.GetComponent<TextMeshProUGUI>();
            validationLabel.font = hostInput?.textComponent?.font ??
                                   portInput?.textComponent?.font ??
                                   TMP_Settings.defaultFontAsset;
            validationLabel.fontSize = 12f;
            validationLabel.alignment = TextAlignmentOptions.Center;
            validationLabel.enableWordWrapping = true;
            validationLabel.color = validationLabelColor;
            validationLabel.raycastTarget = false;
            validationLabel.text = string.Empty;
            validationLabel.gameObject.SetActive(false);
        }
    }
}
