using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using AmpPortableDataViz.Application;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Infra;
using AmpPortableDataViz.Presentation.Anchors;
using AmpPortableDataViz.Presentation.Bootstrap;
using AmpPortableDataViz.Presentation.Sources;
using AmpPortableDataViz.Presentation.Visualization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public sealed class BootstrapperLifecycleTests
    {
        private GameObject _root;
        private Bootstrapper _bootstrapper;
        private RedisRuntimeSettings _previousSettings;
        private RedisRuntimeSettings _settings;
        private bool _hadHost;
        private bool _hadPort;
        private string _previousHost;
        private int _previousPort;
        private RecordingNetwork _network;
        private RecordingFactory _factory;

        [SetUp]
        public void SetUp()
        {
            _previousSettings = RedisRuntimeSettings.Instance;
            _settings = RedisRuntimeSettings.Create("127.0.0.1", 6379);
            RedisRuntimeSettings.SetInstance(_settings);
            _hadHost = PlayerPrefs.HasKey("Redis.Host");
            _hadPort = PlayerPrefs.HasKey("Redis.Port");
            _previousHost = PlayerPrefs.GetString("Redis.Host");
            _previousPort = PlayerPrefs.GetInt("Redis.Port");
            PlayerPrefs.SetString("Redis.Host", "127.0.0.1");
            PlayerPrefs.SetInt("Redis.Port", 6379);

            _root = new GameObject(nameof(BootstrapperLifecycleTests));
            _root.SetActive(false);
            _bootstrapper = _root.AddComponent<Bootstrapper>();
            _bootstrapper.AnchorRegistry = _root.AddComponent<AnchorRegistry>();
            _bootstrapper.VisualPrefab = Child("VisualTemplate");
            _bootstrapper.EmotionVisualParent = _root.transform;
            _bootstrapper.GraphGroupParent = _root.transform;
            _bootstrapper.AutoDetectEmotionDeviceIds = false;
            _bootstrapper.IncludeEmotionChannels = false;
            _bootstrapper.UseIndividualSensorEngagement = false;
            _bootstrapper.AutoSpawnAudienceVisualsFromChannels = false;
            _bootstrapper.AutoSpawnGraphVisualsFromChannels = false;
            _bootstrapper.AutoSpawnArtistVizFromChannels = false;
            _bootstrapper.RequireRedisEndpointConfirmation = true;
            _bootstrapper.RedisHost = "127.0.0.1";

            // Inactive transport object: exercise frame delivery without opening sockets.
            var pumpObject = Child("PrimaryPump");
            pumpObject.SetActive(false);
            _bootstrapper.RedisSource = pumpObject.AddComponent<RedisDataPump>();
            _bootstrapper.RedisSource.enabled = false;
            SetField(_bootstrapper.RedisSource, "_clock", new UnityClock());
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            RedisRuntimeSettings.SetInstance(_previousSettings);
            if (_settings != null) UnityEngine.Object.DestroyImmediate(_settings);
            if (_hadHost) PlayerPrefs.SetString("Redis.Host", _previousHost);
            else PlayerPrefs.DeleteKey("Redis.Host");
            if (_hadPort) PlayerPrefs.SetInt("Redis.Port", _previousPort);
            else PlayerPrefs.DeleteKey("Redis.Port");
        }

        [Test]
        public void RedisIsDefault_AndRetiredSourceValueCannotStartSession()
        {
            Assert.AreEqual(Bootstrapper.SignalSourceType.Redis, _bootstrapper.ActiveSignalSource);
            Assert.AreEqual(1, (int)Bootstrapper.SignalSourceType.Redis);
            _bootstrapper.ActiveSignalSource = (Bootstrapper.SignalSourceType)0;
            SetField(_bootstrapper, "_redisEndpointReady", true);
            AttachRecordingSession();
            Assert.IsFalse((bool)Invoke(_bootstrapper, "ShouldInitializeSessionController"));
        }

        [UnityTest]
        public IEnumerator PrimaryRedis_WaitsForEndpointConfirmation()
        {
            ActivateInManualMode();
            _bootstrapper.UseManualVisualizationDrivers = false;
            Invoke(_bootstrapper, "OnEnable");
            yield return null;
            Assert.AreEqual(0, _network.ActiveSubscriptions);
            Assert.AreEqual(0, _factory.Instances.Count);
            Assert.IsFalse(_bootstrapper.RedisSource.enabled);
        }

        [UnityTest]
        public IEnumerator PrimaryRedis_RepeatedConfirmationBindsAndSpawnsOnce()
        {
            ActivateInManualMode();
            _bootstrapper.UseManualVisualizationDrivers = false;
            _bootstrapper.BeginAfterEndpoint();
            _bootstrapper.BeginAfterEndpoint();
            yield return null;
            _bootstrapper.BeginAfterEndpoint();
            yield return null;
            Assert.AreEqual(2, _network.ActiveSubscriptions);
            Assert.AreEqual(1, _factory.Instances.Count);
            Invoke(_bootstrapper.RedisSource, "EmitFrame", 0.25f);
            Assert.AreEqual(1, _factory.Instances[0].ApplyCount);
            Assert.AreEqual(0.25f, _factory.Instances[0].LastIntensity, 1e-5f);
        }

        [UnityTest]
        public IEnumerator PrimaryRedis_DisableReenableAndDestroyReleaseSubscriptions()
        {
            ActivateInManualMode();
            _bootstrapper.UseManualVisualizationDrivers = false;
            _bootstrapper.BeginAfterEndpoint();
            yield return null;
            var first = _factory.Instances[0];
            _bootstrapper.enabled = false;
            Assert.AreEqual(0, _network.ActiveSubscriptions);
            Invoke(_bootstrapper.RedisSource, "EmitFrame", 0.3f);
            Assert.AreEqual(0, first.ApplyCount);

            _bootstrapper.enabled = true;
            yield return null;
            Assert.AreEqual(2, _network.ActiveSubscriptions);
            Assert.AreEqual(2, _factory.Instances.Count);
            Invoke(_bootstrapper.RedisSource, "EmitFrame", 0.4f);
            Assert.AreEqual(0, first.ApplyCount);
            Assert.AreEqual(1, _factory.Instances[1].ApplyCount);

            UnityEngine.Object.Destroy(_bootstrapper);
            yield return null;
            Assert.AreEqual(0, _network.ActiveSubscriptions);
            Invoke(_root.GetComponentInChildren<RedisDataPump>(true), "EmitFrame", 0.6f);
            Assert.AreEqual(1, _factory.Instances[1].ApplyCount);
        }

        [UnityTest]
        public IEnumerator PrimaryToManualTransition_StopsSessionAndCanReturnToRedis()
        {
            ActivateInManualMode();
            _bootstrapper.UseManualVisualizationDrivers = false;
            _bootstrapper.BeginAfterEndpoint();
            yield return null;
            var first = _factory.Instances[0];
            _bootstrapper.UseManualVisualizationDrivers = true;
            Invoke(_bootstrapper, "ApplySignalConfiguration");
            Assert.AreEqual(0, _network.ActiveSubscriptions);
            Assert.IsFalse(_bootstrapper.RedisSource.enabled);
            Invoke(_bootstrapper.RedisSource, "EmitFrame", 0.5f);
            Assert.AreEqual(0, first.ApplyCount);

            _bootstrapper.UseManualVisualizationDrivers = false;
            _bootstrapper.BeginAfterEndpoint();
            yield return null;
            Assert.AreEqual(2, _network.ActiveSubscriptions);
            Invoke(_bootstrapper.RedisSource, "EmitFrame", 0.6f);
            Assert.AreEqual(1, _factory.Instances[1].ApplyCount);

            _bootstrapper.AutoSpawnAudienceVisualsFromChannels = true;
            Invoke(_bootstrapper, "ApplySignalConfiguration");
            Assert.AreEqual(0, _network.ActiveSubscriptions);
            Invoke(_bootstrapper.RedisSource, "EmitFrame", 0.8f);
            Assert.AreEqual(1, _factory.Instances[1].ApplyCount);
        }

        [UnityTest]
        public IEnumerator ManualMode_ReconfigurationReusesAndDisableReenableRecreatesVisuals()
        {
            _bootstrapper.UseManualVisualizationDrivers = true;
            _bootstrapper.GraphPrefab = Child("ManualGraphTemplate");
            _bootstrapper.GraphPrefab.AddComponent<GraphManualDriver>();
            Invoke(_bootstrapper, "ApplySignalConfiguration");
            var instances = (IList)GetField(_bootstrapper, "_manualDriverInstances");
            Assert.AreEqual(1, instances.Count);
            var first = (GameObject)instances[0];
            Invoke(_bootstrapper, "ApplySignalConfiguration");
            Assert.AreEqual(1, instances.Count);
            Assert.AreSame(first, instances[0]);

            Invoke(_bootstrapper, "OnDisable");
            yield return null;
            Assert.AreEqual(0, instances.Count);
            Assert.IsTrue(first == null);
            Invoke(_bootstrapper, "OnEnable");
            Assert.AreEqual(1, instances.Count);
            var second = (GameObject)instances[0];
            Assert.IsTrue(second.GetComponent<GraphManualDriver>().enabled);
            Invoke(_bootstrapper, "OnDisable");
            Invoke(_bootstrapper, "OnDestroy");
            yield return null;
            Assert.AreEqual(0, instances.Count);
            Assert.IsTrue(second == null);
        }

        [UnityTest]
        public IEnumerator SelectedStreams_ReconfirmationAndDisableClearOwnedObjects()
        {
            ConfigureAutomaticTemplates();
            _bootstrapper.BeginAfterEndpoint();
            var firstVisual = (GameObject)GetField(_bootstrapper, "_selectedParticleVisual");
            Assert.AreEqual(2, _root.GetComponent<SensorStreamCoordinator>().SensorCount);
            _bootstrapper.BeginAfterEndpoint();
            yield return null;
            Assert.IsTrue(firstVisual == null);
            Assert.AreEqual(2, _root.GetComponentsInChildren<RedisPhysioMetricsPump>(true).Length);
            Assert.AreEqual(2, _root.GetComponent<SensorStreamCoordinator>().SensorCount);

            Invoke(_bootstrapper, "OnDisable");
            yield return null;
            Assert.AreEqual(0, _root.GetComponent<SensorStreamCoordinator>().SensorCount);
            Assert.AreEqual(0, _root.GetComponentsInChildren<RedisPhysioMetricsPump>(true).Length);
            Assert.IsNull(GetField(_bootstrapper, "_selectedParticleVisual"));
            Assert.IsNull(GetField(_bootstrapper, "_selectedGraphVisual"));
            Assert.IsNull(GetField(_bootstrapper, "_sensorEngagementBackendObject"));

            Invoke(_bootstrapper, "OnEnable");
            Assert.AreEqual(2, _root.GetComponent<SensorStreamCoordinator>().SensorCount);
            Assert.AreEqual(2, _root.GetComponentsInChildren<RedisPhysioMetricsPump>(true).Length);

            Invoke(_bootstrapper, "OnDisable");
            Invoke(_bootstrapper, "OnDestroy");
            yield return null;
            Assert.AreEqual(0, _root.GetComponent<SensorStreamCoordinator>().SensorCount);
            Assert.AreEqual(0, _root.GetComponentsInChildren<RedisPhysioMetricsPump>(true).Length);
        }

        [UnityTest]
        public IEnumerator PerDeviceToSelectedToManual_ReleasesSharedPumpsAndTargets()
        {
            ConfigureAutomaticTemplates();
            _bootstrapper.UseSingleSelectedSensorStream = false;
            _bootstrapper.BeginAfterEndpoint();
            Assert.AreEqual(2, ((ICollection)GetField(_bootstrapper, "_audienceDeviceInstances")).Count);
            Assert.AreEqual(2, ((ICollection)GetField(_bootstrapper, "_audienceGraphDeviceGroups")).Count);
            Assert.AreEqual(2, ((IDictionary)GetField(_bootstrapper, "_sharedPhysioPumpsByChannel")).Count);
            Assert.AreEqual(2, _root.GetComponentsInChildren<RedisPhysioMetricsPump>(true).Length);
            foreach (object handle in ((IDictionary)GetField(_bootstrapper, "_sharedPhysioPumpsByChannel")).Values)
            {
                Assert.AreEqual(2, handle.GetType().GetField("ReferenceCount").GetValue(handle));
            }

            _bootstrapper.UseSingleSelectedSensorStream = true;
            Invoke(_bootstrapper, "ApplySignalConfiguration");
            yield return null;
            Assert.AreEqual(0, ((ICollection)GetField(_bootstrapper, "_audienceDeviceInstances")).Count);
            Assert.AreEqual(0, ((ICollection)GetField(_bootstrapper, "_audienceGraphDeviceGroups")).Count);
            Assert.AreEqual(2, _root.GetComponentsInChildren<RedisPhysioMetricsPump>(true).Length);
            Assert.AreEqual(2, _root.GetComponent<SensorStreamCoordinator>().SensorCount);

            _bootstrapper.UseManualVisualizationDrivers = true;
            Invoke(_bootstrapper, "ApplySignalConfiguration");
            yield return null;
            Assert.AreEqual(0, _root.GetComponent<SensorStreamCoordinator>().SensorCount);
            Assert.AreEqual(0, ((IDictionary)GetField(_bootstrapper, "_sharedPhysioPumpsByChannel")).Count);
            Assert.AreEqual(0, _root.GetComponentsInChildren<RedisPhysioMetricsPump>(true).Length);
            Assert.IsNull(GetField(_bootstrapper, "_sensorEngagementBackendObject"));
        }

        private void ActivateInManualMode()
        {
            _bootstrapper.UseManualVisualizationDrivers = true;
            _root.SetActive(true);
            AttachRecordingSession();
        }

        private void AttachRecordingSession()
        {
            (GetField(_bootstrapper, "_sessionController") as IDisposable)?.Dispose();
            _network = new RecordingNetwork();
            _factory = new RecordingFactory();
            var controller = new VisualizationSessionController(new UnityClock(),
                new LocalAnchor(_root.transform), _network, _bootstrapper.AnchorRegistry, _factory);
            SetField(_bootstrapper, "_sessionController", controller);
        }

        private void ConfigureAutomaticTemplates()
        {
            _bootstrapper.RedisSource = null;
            _bootstrapper.AutoSpawnAudienceVisualsFromChannels = true;
            _bootstrapper.AutoSpawnGraphVisualsFromChannels = true;
            _bootstrapper.ShowDeviceLabels = false;
            _bootstrapper.ShowGraphGroupLabels = false;
            _bootstrapper.RedisChannels = new[] { "device:a:physio_metrics", "device:b:physio_metrics" };
            _bootstrapper.EmotionDeviceIds = Array.Empty<string>();
            _bootstrapper.VisualPrefab.AddComponent<AudienceSignalBinding>();
            _bootstrapper.VisualPrefab.AddComponent<AudienceSignalVisualizerBinding>();
            _bootstrapper.GraphPrefab = Child("GraphTemplate");
            _bootstrapper.GraphPrefab.AddComponent<AudienceSignalBinding>();
            _bootstrapper.GraphPrefab.AddComponent<AudienceSignalVisualizerBinding>();
            _bootstrapper.GraphPrefab.AddComponent<GraphVisualizer>();
            _bootstrapper.SensorHudCamera = Child("HudCamera").AddComponent<Camera>();
        }

        private GameObject Child(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(_root.transform, false);
            return child;
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, name);
            return method.Invoke(target, arguments);
        }

        private static object GetField(object target, string name)
        {
            return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private sealed class LocalAnchor : IAnchorService
        {
            private readonly Transform _transform;
            public LocalAnchor(Transform transform) => _transform = transform;
            public Task<string> CreateAsync(string label, Vector3 position, Quaternion rotation) => Task.FromResult("test-anchor");
            public Task<bool> LoadAsync(string anchorId) => Task.FromResult(true);
            public Task<AnchorDescriptor> ExportDescriptorAsync(string anchorId) => Task.FromResult(new AnchorDescriptor { AnchorId = anchorId });
            public Task<string> ImportAndResolveAsync(AnchorDescriptor descriptor) => Task.FromResult(descriptor.AnchorId);
            public Transform GetTransform(string anchorId) => _transform;
        }

        private sealed class RecordingNetwork : INetworkSync
        {
            public int ActiveSubscriptions;
            public void Publish<T>(string topic, T payload, long timestampTicksUtc) { }
            public IDisposable Subscribe(string topic, Action<object, long> handler)
            {
                ActiveSubscriptions++;
                return new Subscription(this);
            }
            private sealed class Subscription : IDisposable
            {
                private RecordingNetwork _owner;
                public Subscription(RecordingNetwork owner) => _owner = owner;
                public void Dispose()
                {
                    if (_owner == null) return;
                    _owner.ActiveSubscriptions--;
                    _owner = null;
                }
            }
        }

        private sealed class RecordingFactory : IVisualizationFactory
        {
            public readonly List<RecordingVisual> Instances = new List<RecordingVisual>();
            public IVisualizationInstance Create(VisualizationSpawnRequest request)
            {
                var instance = new RecordingVisual(request.VisualId);
                Instances.Add(instance);
                return instance;
            }
        }

        private sealed class RecordingVisual : IVisualizationInstance
        {
            public string VisualId { get; }
            public int ApplyCount;
            public float LastIntensity;
            public RecordingVisual(string visualId) => VisualId = visualId;
            public void BindToAnchor(string anchorId, Transform anchorTransform) { }
            public void Apply(SimpleVisualParams parameters, long timestampTicksUtc)
            {
                ApplyCount++;
                LastIntensity = parameters.Intensity;
            }
            public void ApplyPose(in PoseSample pose) { }
        }
    }
}
