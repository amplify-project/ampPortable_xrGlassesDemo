using AmpPortableDataViz.Presentation.Visualization;
using TMPro;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Bootstrap
{
    /// <summary>Positions visualization instances and creates sensor labels and HUD presentation.</summary>
    internal sealed class VisualizationLayout
    {
        private Settings _settings;

        internal void Configure(Settings settings)
        {
            _settings = settings;
        }

        internal struct Settings
        {
            public Vector3 SpawnPosition;
            public Vector3 SpawnRotationEuler;
            public Vector3 AbstractVizOriginOffset;
            public Vector3 AbstractVizSpacing;
            public int AbstractVizPerRow;
            public Vector3 GraphGroupOriginOffset;
            public Vector3 GraphGroupSpacing;
            public int GraphGroupsPerRow;
            public bool ShowDeviceLabels;
            public Vector3 DeviceLabelOffset;
            public float DeviceLabelFontSize;
            public Color DeviceLabelColor;
            public bool ShowGraphGroupLabels;
            public Vector3 GraphGroupLabelOffset;
            public float GraphGroupLabelVerticalOffset;
            public float GraphGroupLabelFontSize;
            public Color GraphGroupLabelColor;
            public Vector3 GraphItemBoundsSize;
            public Vector3 GraphGroupBoundsPadding;
            public Camera SensorHudCamera;
            public Vector3 SensorHudLocalPosition;
            public float SensorHudLocalScale;
            public float SensorHudFontSize;
            public Color SensorHudColor;
        }

        internal void PositionAbstractViz(Transform target, int index, bool useLocalSpace)
        {
            if (target == null)
            {
                return;
            }

            int perRow = Mathf.Max(1, _settings.AbstractVizPerRow);
            int row = index / perRow;
            int column = index % perRow;

            Vector3 offset = _settings.AbstractVizOriginOffset + new Vector3(
                column * _settings.AbstractVizSpacing.x,
                row * _settings.AbstractVizSpacing.y,
                row * _settings.AbstractVizSpacing.z);

            if (useLocalSpace)
            {
                target.localPosition = offset;
                target.localRotation = Quaternion.identity;
            }
            else
            {
                target.position = _settings.SpawnPosition + offset;
                target.rotation = Quaternion.Euler(_settings.SpawnRotationEuler);
            }
        }

        internal void PositionGraphGroup(Transform target, int index, bool useLocalSpace)
        {
            if (target == null)
            {
                return;
            }

            int perRow = Mathf.Max(1, _settings.GraphGroupsPerRow);
            int row = index / perRow;
            int column = index % perRow;

            Vector3 offset = _settings.GraphGroupOriginOffset + new Vector3(
                column * _settings.GraphGroupSpacing.x,
                row * _settings.GraphGroupSpacing.y,
                row * _settings.GraphGroupSpacing.z);

            if (useLocalSpace)
            {
                target.localPosition = offset;
                target.localRotation = Quaternion.identity;
            }
            else
            {
                target.position = _settings.SpawnPosition + offset;
                target.rotation = Quaternion.Euler(_settings.SpawnRotationEuler);
            }
        }

        internal void PositionAtParentOrigin(Transform target, bool useLocalSpace)
        {
            if (target == null)
            {
                return;
            }

            if (useLocalSpace)
            {
                target.localPosition = Vector3.zero;
                target.localRotation = Quaternion.identity;
                return;
            }

            target.position = _settings.SpawnPosition;
            target.rotation = Quaternion.Euler(_settings.SpawnRotationEuler);
        }

        internal void AttachDeviceLabel(GameObject visualInstance, string deviceId, int deviceIndex)
        {
            if (!_settings.ShowDeviceLabels || visualInstance == null)
            {
                return;
            }

            Transform labelParent = ResolveDeviceLabelParent(visualInstance);
            if (labelParent == null)
            {
                return;
            }

            var labelObject = new GameObject("DeviceLabel");
            labelObject.transform.SetParent(labelParent, false);
            labelObject.transform.localPosition = _settings.DeviceLabelOffset;
            labelObject.transform.localRotation = Quaternion.identity;
            labelObject.transform.localScale = Vector3.one;
            labelObject.layer = visualInstance.layer;

            var text = labelObject.AddComponent<TextMeshPro>();
            text.text = FormatDeviceLabel(deviceId, deviceIndex);
            text.fontSize = Mathf.Max(0.01f, _settings.DeviceLabelFontSize);
            text.color = _settings.DeviceLabelColor;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.richText = false;

            labelObject.AddComponent<BillboardLabel>();
        }

        internal void AttachGraphGroupLabel(GameObject groupRoot, string deviceId, int deviceIndex)
        {
            if (!_settings.ShowGraphGroupLabels || groupRoot == null)
            {
                return;
            }

            string labelText = FormatGraphGroupLabel(deviceId, deviceIndex);
            if (string.IsNullOrWhiteSpace(labelText))
            {
                return;
            }

            var labelObject = new GameObject("GraphGroupLabel");
            labelObject.transform.SetParent(groupRoot.transform, false);
            labelObject.transform.localRotation = Quaternion.identity;
            labelObject.transform.localScale = Vector3.one;
            labelObject.layer = groupRoot.layer;

            float extraYOffset = Mathf.Max(0f, _settings.GraphGroupLabelVerticalOffset);
            Vector3 localPosition = _settings.GraphGroupLabelOffset + new Vector3(0f, extraYOffset, 0f);
            bool boundsTooSmall = _settings.GraphItemBoundsSize.sqrMagnitude < 1e-6f;
            if (!boundsTooSmall)
            {
                Vector3 size = _settings.GraphItemBoundsSize + _settings.GraphGroupBoundsPadding * 2f;
                float yOffset = size.y * 0.5f + extraYOffset;
                localPosition = new Vector3(0f, yOffset, 0f) + _settings.GraphGroupLabelOffset;
            }

            labelObject.transform.localPosition = localPosition;

            var text = labelObject.AddComponent<TextMeshPro>();
            text.text = labelText;
            text.fontSize = Mathf.Max(0.01f, _settings.GraphGroupLabelFontSize);
            text.color = _settings.GraphGroupLabelColor;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.richText = false;

            labelObject.AddComponent<BillboardLabel>();
        }

        private static string FormatGraphGroupLabel(string deviceId, int deviceIndex)
        {
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                return deviceId.Trim();
            }

            return $"Device {deviceIndex + 1}";
        }

        private static string FormatDeviceLabel(string deviceId, int deviceIndex)
        {
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                string numeric = ExtractTrailingDigits(deviceId);
                if (!string.IsNullOrEmpty(numeric))
                {
                    return $"Device {numeric}";
                }

                return $"Device {deviceId.Trim()}";
            }

            return $"Device {deviceIndex + 1}";
        }

        private static string ExtractTrailingDigits(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            int end = value.Length - 1;
            while (end >= 0 && !char.IsDigit(value[end]))
            {
                end--;
            }

            if (end < 0)
            {
                return string.Empty;
            }

            int start = end;
            while (start >= 0 && char.IsDigit(value[start]))
            {
                start--;
            }

            int length = end - start;
            return length > 0 ? value.Substring(start + 1, length) : string.Empty;
        }

        private static Transform ResolveDeviceLabelParent(GameObject instance)
        {
            if (instance == null)
            {
                return null;
            }

            var particleMeshVisualizer = instance.GetComponentInChildren<ParticleMeshVisualizer>(true);
            if (particleMeshVisualizer != null)
            {
                return particleMeshVisualizer.transform;
            }

            return instance.transform;
        }

        internal GameObject CreateSensorHud(SensorStreamCoordinator coordinator)
        {
            Camera hudCamera = _settings.SensorHudCamera != null ? _settings.SensorHudCamera : Camera.main;
            if (hudCamera == null)
            {
                hudCamera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            }

            if (hudCamera == null)
            {
                Debug.LogWarning("Bootstrapper: No camera is available for the active sensor HUD.");
                return null;
            }

            var hudObject = new GameObject("ActiveSensorHud");
            hudObject.transform.SetParent(hudCamera.transform, false);
            hudObject.transform.localPosition = _settings.SensorHudLocalPosition;
            hudObject.transform.localRotation = Quaternion.identity;
            hudObject.transform.localScale = Vector3.one * Mathf.Max(0.0001f, _settings.SensorHudLocalScale);

            var label = hudObject.AddComponent<TextMeshPro>();
            label.fontSize = Mathf.Max(0.01f, _settings.SensorHudFontSize);
            label.color = _settings.SensorHudColor;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.richText = false;
            label.sortingOrder = 100;
            label.rectTransform.sizeDelta = new Vector2(60f, 8f);

            var hudBinding = hudObject.AddComponent<ActiveSensorHudBinding>();
            hudBinding.Configure(coordinator, label);
            return hudObject;
        }
    }
}
