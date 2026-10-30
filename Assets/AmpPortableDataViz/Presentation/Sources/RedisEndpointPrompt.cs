using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AmpPortableDataViz.Application;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Infra;
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
        private const string ConnectionFailedMessage = "Could not connect to the server. Please check IP address and port numbers are correct and try again.";

        [Header("UI References")]
        [SerializeField] private TMP_InputField hostInput;
        [SerializeField] private TMP_InputField portInput;
        [SerializeField] private Button confirmButton;
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private TMP_Text validationLabel;
        [SerializeField] private bool useTouchScreenKeyboard = true;
        [SerializeField] private bool autoCreateValidationLabel = true;
        [SerializeField] private Color validationLabelColor = new Color(0.9f, 0.3f, 0.3f, 1f);
        [SerializeField] private int connectionTimeoutMs = 2000;

        [Header("Automatic Discovery")]
        [SerializeField] private bool automaticallyDiscoverEndpoint = true;
        [SerializeField] private int cachedEndpointTimeoutMs = 750;
        [SerializeField] private int automaticDiscoveryTimeoutMs = 5000;
        [Tooltip("Required TXT entries in key=value form. A key without '=' only requires that the key exists.")]
        [SerializeField] private string[] requiredTxtRecords = Array.Empty<string>();

        [Header("Events")]
        public UnityEvent OnEndpointReady = new UnityEvent();
        public UnityEvent<string> OnValidationError = new UnityEvent<string>();

        private TouchScreenKeyboard _keyboard;
        private TMP_InputField _activeInput;
        private bool _confirmInProgress;
        private bool _endpointAccepted;
        private CancellationTokenSource _automaticDiscoveryCancellation;

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

        private void OnEnable()
        {
            if (automaticallyDiscoverEndpoint && AndroidRedisEndpointDiscovery.IsSupported)
            {
                StartAutomaticDiscovery();
            }
        }

        private void OnDisable()
        {
            CancelAutomaticDiscovery();
        }

        private void OnDestroy()
        {
            CancelAutomaticDiscovery();

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

        private async void HandleConfirm()
        {
            if (_confirmInProgress)
            {
                return;
            }

            string hostValue = hostInput != null ? hostInput.text.Trim() : string.Empty;
            string portText = portInput != null ? portInput.text.Trim() : string.Empty;

            ClearValidationError();
            CloseKeyboard();
            Debug.Log($"RedisEndpointPrompt: Confirm pressed with host '{hostValue}' and port '{portText}'.");

            if (!Validate(hostValue, portText, out var portValue))
            {
                return;
            }

            _confirmInProgress = true;
            SetConfirmInteractable(false);
            CancelAutomaticDiscovery();

            try
            {
                bool canConnect = await VerifyRedisConnectionAsync(hostValue, portValue);
                if (!canConnect)
                {
                    RaiseValidationError(ConnectionFailedMessage);
                    return;
                }

                SaveManualEndpoint(hostValue, portValue);
                Debug.Log($"RedisEndpointPrompt: Saved Redis endpoint {hostValue}:{portValue}.");
                CompleteEndpointReady("manual confirmation");
            }
            finally
            {
                _confirmInProgress = false;
                SetConfirmInteractable(true);
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
                validationLabel.color = validationLabelColor;
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

        private void StartAutomaticDiscovery()
        {
            CancelAutomaticDiscovery();
            _automaticDiscoveryCancellation = new CancellationTokenSource();
            _ = DiscoverEndpointAutomaticallyAsync(_automaticDiscoveryCancellation);
        }

        private async Task DiscoverEndpointAutomaticallyAsync(CancellationTokenSource cancellation)
        {
            var settings = RedisRuntimeSettings.Load();
            SetStatus(settings.HasPersistedEndpoint
                ? "Checking the saved Redis server and searching the network..."
                : "Searching for the Redis server...");

            try
            {
                using var discovery = new AndroidRedisEndpointDiscovery(BuildTxtRequirements(requiredTxtRecords));
                var resolver = new RedisEndpointStartupResolver(
                    discovery,
                    (endpoint, timeoutMs, _) => RedisDeviceDiscovery.CanConnectAsync(endpoint.Host, endpoint.Port, timeoutMs));

                var resolution = await resolver.ResolveAsync(
                    settings.ToEndpoint(),
                    settings.HasPersistedEndpoint,
                    cachedEndpointTimeoutMs,
                    automaticDiscoveryTimeoutMs,
                    connectionTimeoutMs,
                    cancellation.Token);

                if (cancellation.IsCancellationRequested || !resolution.Succeeded || _confirmInProgress)
                {
                    if (!cancellation.IsCancellationRequested && !resolution.Succeeded)
                    {
                        SetStatus("Redis was not found automatically. Enter the server address to connect manually.");
                    }

                    return;
                }

                if (resolution.Source == RedisEndpointResolutionSource.Discovered)
                {
                    settings.ApplyDiscovered(resolution.Endpoint);
                    settings.Save();
                    RedisRuntimeSettings.SetInstance(settings);
                    Debug.Log($"RedisEndpointPrompt: Discovered and saved Redis endpoint {resolution.Endpoint.Host}:{resolution.Endpoint.Port}.");
                }
                else
                {
                    Debug.Log($"RedisEndpointPrompt: Reusing reachable saved Redis endpoint {resolution.Endpoint.Host}:{resolution.Endpoint.Port}.");
                }

                UpdateInputs(resolution.Endpoint);
                CompleteEndpointReady(resolution.Source == RedisEndpointResolutionSource.Cached
                    ? "saved endpoint"
                    : "DNS-SD discovery");
            }
            catch (OperationCanceledException)
            {
                // Expected when manual confirmation wins or the panel is disabled.
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"RedisEndpointPrompt: Automatic Redis discovery failed ({ex.Message}).");
                SetStatus("Automatic discovery failed. Enter the server address to connect manually.");
            }
            finally
            {
                cancellation.Dispose();
                if (ReferenceEquals(_automaticDiscoveryCancellation, cancellation))
                {
                    _automaticDiscoveryCancellation = null;
                }
            }
        }

        private void CancelAutomaticDiscovery()
        {
            if (_automaticDiscoveryCancellation == null)
            {
                return;
            }

            _automaticDiscoveryCancellation.Cancel();
        }

        private static IReadOnlyList<RedisTxtRecordRequirement> BuildTxtRequirements(IEnumerable<string> serializedRequirements)
        {
            var requirements = new List<RedisTxtRecordRequirement>();
            if (serializedRequirements == null)
            {
                return requirements;
            }

            foreach (var serializedRequirement in serializedRequirements)
            {
                if (string.IsNullOrWhiteSpace(serializedRequirement))
                {
                    continue;
                }

                int separatorIndex = serializedRequirement.IndexOf('=');
                if (separatorIndex < 0)
                {
                    requirements.Add(new RedisTxtRecordRequirement(serializedRequirement));
                    continue;
                }

                string key = serializedRequirement.Substring(0, separatorIndex).Trim();
                string value = serializedRequirement.Substring(separatorIndex + 1).Trim();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    requirements.Add(new RedisTxtRecordRequirement(key, value));
                }
            }

            return requirements;
        }

        private void SaveManualEndpoint(string hostValue, int portValue)
        {
            var settings = RedisRuntimeSettings.Instance;
            settings.Apply(hostValue, portValue);
            settings.Save();
            RedisRuntimeSettings.SetInstance(settings);
        }

        private void CompleteEndpointReady(string source)
        {
            if (_endpointAccepted)
            {
                return;
            }

            _endpointAccepted = true;
            try
            {
                Debug.Log($"RedisEndpointPrompt: Invoking OnEndpointReady after {source}.");
                OnEndpointReady?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }
            finally
            {
                HidePanel();
                Debug.Log("RedisEndpointPrompt: Startup panel hidden.");
            }
        }

        private void UpdateInputs(RedisServiceEndpoint endpoint)
        {
            if (endpoint == null)
            {
                return;
            }

            if (hostInput != null)
            {
                hostInput.text = endpoint.Host;
            }

            if (portInput != null)
            {
                portInput.text = endpoint.Port.ToString();
            }
        }

        private void SetStatus(string message)
        {
            if (validationLabel == null || _endpointAccepted)
            {
                return;
            }

            validationLabel.color = Color.white;
            validationLabel.text = message;
            validationLabel.gameObject.SetActive(true);
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

        private async Task<bool> VerifyRedisConnectionAsync(string hostValue, int portValue)
        {
            Debug.Log($"RedisEndpointPrompt: Verifying Redis connection to {hostValue}:{portValue}.");
            bool canConnect = await RedisDeviceDiscovery.CanConnectAsync(hostValue, portValue, connectionTimeoutMs);
            if (!canConnect)
            {
                Debug.LogWarning($"RedisEndpointPrompt: Could not verify Redis connection to {hostValue}:{portValue}.");
                return false;
            }

            Debug.Log($"RedisEndpointPrompt: Redis connection verified for {hostValue}:{portValue}.");
            return true;
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

        private void SetConfirmInteractable(bool isInteractable)
        {
            if (confirmButton != null)
            {
                confirmButton.interactable = isInteractable;
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
            rectTransform.anchoredPosition = new Vector2(0f, -92f);
            rectTransform.sizeDelta = new Vector2(280f, 64f);

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
