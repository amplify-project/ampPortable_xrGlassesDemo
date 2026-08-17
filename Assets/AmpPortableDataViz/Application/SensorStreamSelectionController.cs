using System;
using System.Collections.Generic;

namespace AmpPortableDataViz.Application
{
    public readonly struct SensorStreamSelectionChanged
    {
        public SensorStreamSelectionChanged(
            string previousSensorId,
            string activeSensorId,
            int activeIndex,
            int sensorCount)
        {
            PreviousSensorId = previousSensorId ?? string.Empty;
            ActiveSensorId = activeSensorId ?? string.Empty;
            ActiveIndex = activeIndex;
            SensorCount = sensorCount;
        }

        public string PreviousSensorId { get; }
        public string ActiveSensorId { get; }
        public int ActiveIndex { get; }
        public int SensorCount { get; }
        public bool HasActiveSensor => SensorCount > 0 && ActiveIndex >= 0 && !string.IsNullOrEmpty(ActiveSensorId);
    }

    /// <summary>
    /// Owns the stable, ordered sensor catalog and the currently selected sensor stream.
    /// </summary>
    public sealed class SensorStreamSelectionController
    {
        private static readonly StringComparer SensorIdComparer = StringComparer.OrdinalIgnoreCase;

        private string[] _sensorIds = Array.Empty<string>();
        private int _activeIndex = -1;

        public IReadOnlyList<string> SensorIds => _sensorIds;
        public int SensorCount => _sensorIds.Length;
        public int ActiveIndex => _activeIndex;
        public string ActiveSensorId => _activeIndex >= 0 && _activeIndex < _sensorIds.Length
            ? _sensorIds[_activeIndex]
            : string.Empty;
        public bool HasActiveSensor => _activeIndex >= 0 && _activeIndex < _sensorIds.Length;

        public event Action<SensorStreamSelectionChanged> SelectionChanged;

        public void ReplaceSensors(IEnumerable<string> sensorIds)
        {
            string previousSensorId = ActiveSensorId;
            string[] normalizedSensorIds = NormalizeSensorIds(sensorIds);
            bool catalogChanged = !CatalogsMatch(_sensorIds, normalizedSensorIds);

            _sensorIds = normalizedSensorIds;
            _activeIndex = FindSensorIndex(previousSensorId);
            if (_activeIndex < 0 && _sensorIds.Length > 0)
            {
                _activeIndex = 0;
            }

            if (catalogChanged || !SensorIdComparer.Equals(previousSensorId, ActiveSensorId))
            {
                RaiseSelectionChanged(previousSensorId);
            }
        }

        public bool SelectNext()
        {
            if (_sensorIds.Length <= 1)
            {
                return false;
            }

            string previousSensorId = ActiveSensorId;
            _activeIndex = (_activeIndex + 1) % _sensorIds.Length;
            RaiseSelectionChanged(previousSensorId);
            return true;
        }

        public bool TrySelect(string sensorId)
        {
            int selectedIndex = FindSensorIndex(sensorId);
            if (selectedIndex < 0 || selectedIndex == _activeIndex)
            {
                return false;
            }

            string previousSensorId = ActiveSensorId;
            _activeIndex = selectedIndex;
            RaiseSelectionChanged(previousSensorId);
            return true;
        }

        private void RaiseSelectionChanged(string previousSensorId)
        {
            SelectionChanged?.Invoke(new SensorStreamSelectionChanged(
                previousSensorId,
                ActiveSensorId,
                ActiveIndex,
                SensorCount));
        }

        private int FindSensorIndex(string sensorId)
        {
            if (string.IsNullOrWhiteSpace(sensorId))
            {
                return -1;
            }

            for (int i = 0; i < _sensorIds.Length; i++)
            {
                if (SensorIdComparer.Equals(_sensorIds[i], sensorId.Trim()))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string[] NormalizeSensorIds(IEnumerable<string> sensorIds)
        {
            if (sensorIds == null)
            {
                return Array.Empty<string>();
            }

            var uniqueIds = new HashSet<string>(SensorIdComparer);
            foreach (string sensorId in sensorIds)
            {
                if (!string.IsNullOrWhiteSpace(sensorId))
                {
                    uniqueIds.Add(sensorId.Trim());
                }
            }

            var normalizedIds = new string[uniqueIds.Count];
            uniqueIds.CopyTo(normalizedIds);
            Array.Sort(normalizedIds, SensorIdComparer);
            return normalizedIds;
        }

        private static bool CatalogsMatch(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (!SensorIdComparer.Equals(left[i], right[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
