using AmpPortableDataViz.Application;
using TMPro;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Visualization
{
    /// <summary>
    /// Displays the selected sensor identity independently from visualization visibility.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Amp Portable Data Viz/Visualization/Active Sensor HUD Binding")]
    public sealed class ActiveSensorHudBinding : MonoBehaviour
    {
        [SerializeField] private SensorStreamCoordinator coordinator;
        [SerializeField] private TMP_Text sensorLabel;
        [SerializeField] private string emptyLabel = "NO SENSOR STREAMS";

        private bool _subscribed;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            Subscribe();
            RefreshLabel();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        public void Configure(SensorStreamCoordinator newCoordinator, TMP_Text newSensorLabel = null)
        {
            Unsubscribe();
            coordinator = newCoordinator;
            if (newSensorLabel != null)
            {
                sensorLabel = newSensorLabel;
            }

            if (isActiveAndEnabled)
            {
                Subscribe();
            }

            RefreshLabel();
        }

        public void RefreshLabel()
        {
            ResolveReferences();
            if (sensorLabel == null)
            {
                return;
            }

            if (coordinator == null || !coordinator.HasActiveSensor)
            {
                sensorLabel.text = FormatLabel(-1, 0, string.Empty, emptyLabel);
                return;
            }

            sensorLabel.text = FormatLabel(
                coordinator.ActiveSensorIndex,
                coordinator.SensorCount,
                coordinator.ActiveSensorId,
                emptyLabel);
        }

        public static string FormatLabel(int activeIndex, int sensorCount, string activeSensorId, string noSensorsLabel = "NO SENSOR STREAMS")
        {
            if (activeIndex < 0 || sensorCount <= 0 || string.IsNullOrWhiteSpace(activeSensorId))
            {
                return string.IsNullOrWhiteSpace(noSensorsLabel) ? "NO SENSOR STREAMS" : noSensorsLabel;
            }

            return $"SENSOR {activeIndex + 1} / {sensorCount}\n{activeSensorId}";
        }

        private void Subscribe()
        {
            ResolveReferences();
            if (_subscribed || coordinator == null)
            {
                return;
            }

            coordinator.SelectionChanged += OnSelectionChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_subscribed && coordinator != null)
            {
                coordinator.SelectionChanged -= OnSelectionChanged;
            }

            _subscribed = false;
        }

        private void ResolveReferences()
        {
            if (coordinator == null)
            {
                coordinator = GetComponentInParent<SensorStreamCoordinator>();
            }

            if (sensorLabel == null)
            {
                sensorLabel = GetComponent<TMP_Text>();
            }
        }

        private void OnSelectionChanged(SensorStreamSelectionChanged change)
        {
            RefreshLabel();
        }
    }
}
