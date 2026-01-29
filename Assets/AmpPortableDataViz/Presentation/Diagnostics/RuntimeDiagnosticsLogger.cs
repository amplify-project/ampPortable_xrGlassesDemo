using System;
using System.Collections.Generic;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Presentation.Sources;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;

namespace AmpPortableDataViz.Presentation.Diagnostics
{
    public sealed class RuntimeDiagnosticsLogger : MonoBehaviour
    {
        [Header("Hand Tracking")]
        [SerializeField] private bool logHands = true;
        [SerializeField] private float handLogInterval = 2f;
        [SerializeField] private bool includeJointAvailability = true;

        [Header("Data Frames (RedisDataPump)")]
        [SerializeField] private bool logDataFrames = true;
        [SerializeField] private float dataLogInterval = 5f;
        [SerializeField] private bool includePayload = true;

        [Header("Input Devices")]
        [SerializeField] private bool logInputDevicesOnce = true;

        [Header("Pump Discovery")]
        [SerializeField] private bool autoFindPumps = true;
        [SerializeField] private float pumpScanInterval = 5f;
        [SerializeField] private RedisDataPump[] pumps = Array.Empty<RedisDataPump>();

        private XRHandSubsystem _hands;
        private bool _warnedNoHands;
        private float _nextHandLogTime;
        private float _nextDataLogTime;
        private float _nextPumpScanTime;
        private bool _dumpedInputDevices;

        private readonly Dictionary<RedisDataPump, PumpState> _pumpStates = new Dictionary<RedisDataPump, PumpState>();

        private sealed class PumpState
        {
            public readonly RedisDataPump Pump;
            public int TotalFrames;
            public int FramesSinceLastLog;
            public float LastPayload;
            public long LastTimestampTicksUtc;
            public Action<DataFrame<float>> Handler;

            public PumpState(RedisDataPump pump)
            {
                Pump = pump;
            }
        }

        private void Awake()
        {
            if (logHands)
            {
                var list = new List<XRHandSubsystem>();
                SubsystemManager.GetSubsystems(list);
                if (list.Count > 0)
                {
                    _hands = list[0];
                }
            }
        }

        private void OnEnable()
        {
            if (logInputDevicesOnce && !_dumpedInputDevices)
            {
                DumpInputDevices();
                _dumpedInputDevices = true;
            }

            if (logDataFrames)
            {
                RegisterConfiguredPumps();
            }
        }

        private void OnDisable()
        {
            foreach (var kvp in _pumpStates)
            {
                if (kvp.Key != null && kvp.Value?.Handler != null)
                {
                    kvp.Key.OnFrame -= kvp.Value.Handler;
                }
            }
        }

        private void Update()
        {
            if (autoFindPumps && logDataFrames && Time.time >= _nextPumpScanTime)
            {
                _nextPumpScanTime = Time.time + Mathf.Max(0.5f, pumpScanInterval);
                ScanForPumps();
            }

            if (logHands && Time.time >= _nextHandLogTime)
            {
                _nextHandLogTime = Time.time + Mathf.Max(0.1f, handLogInterval);
                LogHandStatus();
            }

            if (logDataFrames && Time.time >= _nextDataLogTime)
            {
                _nextDataLogTime = Time.time + Mathf.Max(0.5f, dataLogInterval);
                LogPumpStatus();
            }
        }

        private void RegisterConfiguredPumps()
        {
            if (pumps == null || pumps.Length == 0)
            {
                if (autoFindPumps)
                {
                    ScanForPumps();
                }
                return;
            }

            int added = 0;
            foreach (var pump in pumps)
            {
                if (TryRegisterPump(pump))
                {
                    added++;
                }
            }

            if (added > 0)
            {
                Debug.Log($"RuntimeDiagnostics: Registered {added} RedisDataPump(s). Total={_pumpStates.Count}.");
            }
        }

        private void ScanForPumps()
        {
            var found = FindObjectsOfType<RedisDataPump>(true);
            int added = 0;
            foreach (var pump in found)
            {
                if (TryRegisterPump(pump))
                {
                    added++;
                }
            }

            if (added > 0)
            {
                Debug.Log($"RuntimeDiagnostics: Discovered {added} RedisDataPump(s). Total={_pumpStates.Count}.");
            }
        }

        private bool TryRegisterPump(RedisDataPump pump)
        {
            if (pump == null || _pumpStates.ContainsKey(pump))
            {
                return false;
            }

            var state = new PumpState(pump);
            state.Handler = frame =>
            {
                state.TotalFrames++;
                state.FramesSinceLastLog++;
                state.LastPayload = frame.Payload;
                state.LastTimestampTicksUtc = frame.TimestampTicksUtc;
            };

            pump.OnFrame += state.Handler;
            _pumpStates[pump] = state;
            return true;
        }

        private void LogHandStatus()
        {
            if (_hands == null)
            {
                if (!_warnedNoHands)
                {
                    Debug.LogWarning("RuntimeDiagnostics: XRHandSubsystem not found.");
                    _warnedNoHands = true;
                }
                return;
            }

            if (!_hands.running)
            {
                Debug.LogWarning("RuntimeDiagnostics: XRHandSubsystem is not running.");
                return;
            }

            var left = _hands.leftHand;
            var right = _hands.rightHand;

            bool leftTracked = left.isTracked;
            bool rightTracked = right.isTracked;

            string leftDetails = string.Empty;
            string rightDetails = string.Empty;

            if (includeJointAvailability)
            {
                leftDetails = leftTracked && left.GetJoint(XRHandJointID.Wrist).TryGetPose(out _) ? "Wrist=Y" : "Wrist=N";
                rightDetails = rightTracked && right.GetJoint(XRHandJointID.Wrist).TryGetPose(out _) ? "Wrist=Y" : "Wrist=N";
            }

            Debug.Log($"RuntimeDiagnostics: Hands tracked L={leftTracked} {leftDetails} | R={rightTracked} {rightDetails}");
        }

        private void LogPumpStatus()
        {
            if (_pumpStates.Count == 0)
            {
                Debug.LogWarning("RuntimeDiagnostics: No RedisDataPump instances registered.");
                return;
            }

            foreach (var kvp in _pumpStates)
            {
                var state = kvp.Value;
                if (state?.Pump == null)
                {
                    continue;
                }

                if (includePayload && state.TotalFrames > 0)
                {
                    Debug.Log($"RuntimeDiagnostics: Pump '{state.Pump.SourceId}' frames={state.FramesSinceLastLog} total={state.TotalFrames} last={state.LastPayload:F4} ts={state.LastTimestampTicksUtc}");
                }
                else
                {
                    Debug.Log($"RuntimeDiagnostics: Pump '{state.Pump.SourceId}' frames={state.FramesSinceLastLog} total={state.TotalFrames}");
                }

                state.FramesSinceLastLog = 0;
            }
        }

        private void DumpInputDevices()
        {
            var devices = InputSystem.devices;
            if (devices.Count == 0)
            {
                Debug.LogWarning("RuntimeDiagnostics: No InputSystem devices found.");
                return;
            }

            Debug.Log($"RuntimeDiagnostics: InputSystem devices ({devices.Count})");
            foreach (var device in devices)
            {
                if (device == null)
                {
                    continue;
                }

                string layout = string.IsNullOrWhiteSpace(device.layout) ? "UnknownLayout" : device.layout;
                string display = string.IsNullOrWhiteSpace(device.displayName) ? device.name : device.displayName;
                Debug.Log($"RuntimeDiagnostics: Device '{display}' layout={layout} id={device.deviceId}");
            }
        }
    }
}
