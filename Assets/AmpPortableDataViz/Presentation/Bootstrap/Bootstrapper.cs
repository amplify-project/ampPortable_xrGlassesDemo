using System;
using System.Collections;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Infra;
using AmpPortableDataViz.Presentation.Anchors;
using AmpPortableDataViz.Presentation.Mapping;
using AmpPortableDataViz.Presentation.Sources;
using AmpPortableDataViz.Presentation.Visualization;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Bootstrap
{
    public sealed class Bootstrapper : MonoBehaviour
    {
        public AnchorRegistry AnchorRegistry;
        public SineWaveSource SineSource;
        public LiveKitTelemetryReceiver LiveKitSource;
        public GameObject VisualPrefab;
        public bool IsHost = true;

        private IClock _clock;
        private IAnchorService _anchorService;
        private INetworkSync _networkSync;
        private IDisposable _spawnSubscription;
        private Transform _liveKitTargetTransform;

        private const string SpawnTopic = "session/visual/spawn";
        private const string ParamTopic = "session/visual/param";

        private void Awake()
        {
            _clock = new UnityClock();
            _anchorService = new MockAnchorService();
            _networkSync = new LocalLoopbackNetworkSync();

            if (AnchorRegistry == null) AnchorRegistry = FindObjectOfType<AnchorRegistry>();
            if (SineSource == null)
            {
                var sourceGameObject = new GameObject("SineWaveSource");
                SineSource = sourceGameObject.AddComponent<SineWaveSource>();
            }
            if (LiveKitSource == null) LiveKitSource = FindObjectOfType<LiveKitTelemetryReceiver>();
            if (VisualPrefab == null)
            {
                VisualPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                VisualPrefab.name = "SimpleVisualizerPrefab";
                if (VisualPrefab.GetComponent<SimpleVisualizer>() == null) VisualPrefab.AddComponent<SimpleVisualizer>();
                if (VisualPrefab.GetComponent<AnchoredVisualization>() == null) VisualPrefab.AddComponent<AnchoredVisualization>();
            }
        }

        private void OnEnable()
        {
            _spawnSubscription = _networkSync.Subscribe(SpawnTopic, OnSpawnMessageReceived);
            if (LiveKitSource == null) LiveKitSource = FindObjectOfType<LiveKitTelemetryReceiver>();
            if (LiveKitSource != null)
            {
                LiveKitSource.OnFrame += OnLiveKitFrame;
            }
            else
            {
                Debug.LogWarning("Bootstrapper could not locate a LiveKitTelemetryReceiver in the scene.");
            }
        }

        private void OnDisable()
        {
            _spawnSubscription?.Dispose();
            _spawnSubscription = null;

            if (LiveKitSource != null)
            {
                LiveKitSource.OnFrame -= OnLiveKitFrame;
            }

            _liveKitTargetTransform = null;
        }

        private IEnumerator Start()
        {
            if (AnchorRegistry == null)
            {
                var registryGameObject = new GameObject("AnchorRegistry");
                AnchorRegistry = registryGameObject.AddComponent<AnchorRegistry>();
            }

            if (IsHost)
            {
                Vector3 anchorPosition = new Vector3(0, 1.2f, 2f);
                Quaternion anchorRotation = Quaternion.identity;
                string createdAnchorId = string.Empty;

                yield return _anchorService.CreateAsync("Demo", anchorPosition, anchorRotation)
                    .AsIEnumerator(result => createdAnchorId = result);

                AnchorDescriptor exportedAnchorDescriptor = null;
                yield return _anchorService.ExportDescriptorAsync(createdAnchorId)
                    .AsIEnumerator(result => exportedAnchorDescriptor = result);

                AnchorRegistry.Register(exportedAnchorDescriptor.AnchorId, _anchorService.GetTransform(exportedAnchorDescriptor.AnchorId));

                var spawnMessage = new SpawnVisualizationMsg
                {
                    Anchor = exportedAnchorDescriptor,
                    VisualId = "SimpleDemo",
                    Seed = 12345,
                    ProfileId = "Default"
                };

                _networkSync.Publish(SpawnTopic, spawnMessage, _clock.UtcNowTicks);
            }

            var mapperSettings = new FloatToSimpleParams.Settings
            {
                Scale = 1f,
                ClampRange = new Vector2(0f, 1f),
                HueMin = 0.55f,
                HueMax = 0.85f
            };
            var floatToParamsMapper = new FloatToSimpleParams(mapperSettings);

            SineSource.OnFrame += dataFrame =>
            {
                var parameters = floatToParamsMapper.Map(dataFrame);
                var paramDeltaMessage = new ParamDeltaMsg
                {
                    VisualId = "SimpleDemo",
                    TimestampTicks = dataFrame.TimestampTicksUtc,
                    Params0 = new Vector4(parameters.Intensity, parameters.Color.r, parameters.Color.g, parameters.Color.b),
                    Params1 = new Vector4(parameters.Flow.x, parameters.Flow.y, parameters.Flow.z, 0f)
                };
                _networkSync.Publish(ParamTopic, paramDeltaMessage, dataFrame.TimestampTicksUtc);
            };

            _networkSync.Subscribe(ParamTopic, (payloadObject, payloadTimestamp) =>
            {
                var paramDelta = (ParamDeltaMsg)payloadObject;
                var visualizationGameObject = GameObject.Find($"Viz_{paramDelta.VisualId}");
                if (visualizationGameObject == null) return;

                var visualizer = visualizationGameObject.GetComponentInChildren<SimpleVisualizer>();
                if (visualizer == null) return;

                var parameters = new SimpleVisualParams
                {
                    Intensity = paramDelta.Params0.x,
                    Color = new Color(paramDelta.Params0.y, paramDelta.Params0.z, paramDelta.Params0.w, 1f),
                    Flow = new Vector3(paramDelta.Params1.x, paramDelta.Params1.y, paramDelta.Params1.z)
                };

                visualizer.Apply(parameters, paramDelta.TimestampTicks);
            });

            yield break;
        }

        private async void OnSpawnMessageReceived(object payloadObject, long messageTimestampTicksUtc)
        {
            var spawnMessage = (SpawnVisualizationMsg)payloadObject;
            string resolvedAnchorId = await _anchorService.ImportAndResolveAsync(spawnMessage.Anchor);
            Transform resolvedAnchorTransform = _anchorService.GetTransform(resolvedAnchorId);
            AnchorRegistry.Register(resolvedAnchorId, resolvedAnchorTransform);

            var spawnedVisualization = Instantiate(VisualPrefab);
            spawnedVisualization.name = $"Viz_{spawnMessage.VisualId}";

            var anchoredComponent = spawnedVisualization.GetComponent<AnchoredVisualization>();
            if (anchoredComponent != null)
            {
                anchoredComponent.AnchorId = resolvedAnchorId;
                anchoredComponent.BindTransform();
                _liveKitTargetTransform = anchoredComponent.VisualRoot == null ? anchoredComponent.transform : anchoredComponent.VisualRoot;
            }
            else
            {
                _liveKitTargetTransform = spawnedVisualization.transform;
            }
        }

        private void OnLiveKitFrame(DataFrame<LiveKitTelemetryService.TelemetryFrame> frame)
        {
            if (_liveKitTargetTransform == null)
            {
                return;
            }

            var payload = frame.Payload;
            _liveKitTargetTransform.localPosition = payload.Position;
            _liveKitTargetTransform.localRotation = payload.Rotation;
        }
    }

    internal static class TaskExtensions
    {
        public static IEnumerator AsIEnumerator<TResult>(this System.Threading.Tasks.Task<TResult> task, Action<TResult> onCompleteAction)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) yield break;
            onCompleteAction?.Invoke(task.Result);
        }
    }
}
