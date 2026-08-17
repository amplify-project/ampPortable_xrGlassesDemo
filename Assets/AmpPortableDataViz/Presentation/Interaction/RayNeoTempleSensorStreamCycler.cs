using AmpPortableDataViz.Presentation.Visualization;
using RayNeo;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Interaction
{
    /// <summary>
    /// Advances the active sensor stream on a RayNeo right-temple double tap.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Amp Portable Data Viz/Interaction/RayNeo Temple Sensor Stream Cycler")]
    public sealed class RayNeoTempleSensorStreamCycler : MonoBehaviour
    {
        [SerializeField] private SensorStreamCoordinator coordinator;
        [SerializeField] private bool logDiagnostics = true;

        private bool _subscribed;

        private void Awake()
        {
            ResolveCoordinator();
        }

        private void OnEnable()
        {
            SubscribeToTempleDoubleTap();
        }

        private void OnDisable()
        {
            UnsubscribeFromTempleDoubleTap();
        }

        private void OnDestroy()
        {
            UnsubscribeFromTempleDoubleTap();
        }

        public void Configure(SensorStreamCoordinator newCoordinator)
        {
            coordinator = newCoordinator;
        }

        public void SelectNextSensor()
        {
            ResolveCoordinator();
            if (coordinator == null)
            {
                Debug.LogWarning("RayNeoTempleSensorStreamCycler: SensorStreamCoordinator is not assigned.", this);
                return;
            }

            bool changed = coordinator.SelectNextSensor();
            if (logDiagnostics)
            {
                string result = changed ? coordinator.ActiveSensorId : "unchanged";
                Debug.Log($"RayNeoTempleSensorStreamCycler: Double tap selected {result}.", this);
            }
        }

        private void SubscribeToTempleDoubleTap()
        {
            if (_subscribed)
            {
                return;
            }

            SimpleTouchForLite.Instance.OnDoubleTap.AddListener(SelectNextSensor);
            _subscribed = true;
        }

        private void UnsubscribeFromTempleDoubleTap()
        {
            if (!_subscribed || !SimpleTouchForLite.SingletonExist)
            {
                _subscribed = false;
                return;
            }

            SimpleTouchForLite.Instance.OnDoubleTap.RemoveListener(SelectNextSensor);
            _subscribed = false;
        }

        private void ResolveCoordinator()
        {
            if (coordinator == null)
            {
                coordinator = GetComponent<SensorStreamCoordinator>();
            }
        }
    }
}
