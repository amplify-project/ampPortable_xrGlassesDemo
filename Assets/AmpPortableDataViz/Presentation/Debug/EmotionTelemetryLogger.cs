using System;
using System.Collections.Generic;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Presentation.Sources;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Diagnostics
{
    /// <summary>
    /// Helper MonoBehaviour that listens to multiple RedisDataPump sources and prints their values into the console.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EmotionTelemetryLogger : MonoBehaviour
    {
        [Serializable]
        private struct PumpBinding
        {
            [Tooltip("Optional label to use in logs. Defaults to the pump's Source Identity.")]
            public string Label;
            public RedisDataPump Pump;
        }

        [Header("Display")]
        [SerializeField] private string logPrefix = "[EmotionTelemetry]";
        [SerializeField] private bool includeSequenceAndTimestamp = true;

        [Header("Tracked Pumps")]
        [SerializeField] private PumpBinding broadcastPump;
        [SerializeField] private PumpBinding[] additionalPumps = Array.Empty<PumpBinding>();

        private readonly Dictionary<RedisDataPump, Action<DataFrame<float>>> _subscriptions = new();

        private void OnEnable()
        {
            RegisterPump(broadcastPump);

            if (additionalPumps == null)
            {
                return;
            }

            foreach (var binding in additionalPumps)
            {
                RegisterPump(binding);
            }
        }

        private void OnDisable()
        {
            foreach (var pair in _subscriptions)
            {
                pair.Key.OnFrame -= pair.Value;
            }

            _subscriptions.Clear();
        }

        private void RegisterPump(PumpBinding binding)
        {
            if (binding.Pump == null || _subscriptions.ContainsKey(binding.Pump))
            {
                return;
            }

            var label = string.IsNullOrWhiteSpace(binding.Label)
                ? binding.Pump.SourceId
                : binding.Label.Trim();

            void Handler(DataFrame<float> frame)
            {
                var value = frame.Payload;
                if (includeSequenceAndTimestamp)
                {
                    Debug.Log($"{logPrefix} {label} = {value:F4} (seq {frame.SequenceId}, ts {frame.TimestampTicksUtc})");
                }
                else
                {
                    Debug.Log($"{logPrefix} {label} = {value:F4}");
                }
            }

            binding.Pump.OnFrame += Handler;
            _subscriptions.Add(binding.Pump, Handler);
        }
    }
}
