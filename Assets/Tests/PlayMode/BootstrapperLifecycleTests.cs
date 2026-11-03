using System;
using System.Collections;
using System.Reflection;
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
            _bootstrapper.AbstractVizPrefab = Child("VisualTemplate");
            _bootstrapper.AbstractVizParent = _root.transform;
            _bootstrapper.GraphGroupParent = _root.transform;
            _bootstrapper.AutoDetectSensorIds = false;
            _bootstrapper.IncludeSensorChannels = false;
            _bootstrapper.UseIndividualSensorEngagement = false;
            _bootstrapper.AutoSpawnAbstractViz = false;
            _bootstrapper.AutoSpawnGraphVisualsFromChannels = false;
            _bootstrapper.AutoSpawnArtistVizFromChannels = false;
            _bootstrapper.RequireRedisEndpointConfirmation = true;
            _bootstrapper.RedisHost = "127.0.0.1";
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
            Assert.IsNull(GetField(GetField(_bootstrapper, "_streams"), "_sensorEngagementBackendObject"));

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
            Assert.AreEqual(2, ((IDictionary)GetField(GetField(_bootstrapper, "_streams"), "_sharedPhysioPumpsByChannel")).Count);
            Assert.AreEqual(2, _root.GetComponentsInChildren<RedisPhysioMetricsPump>(true).Length);
            foreach (object handle in ((IDictionary)GetField(GetField(_bootstrapper, "_streams"), "_sharedPhysioPumpsByChannel")).Values)
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
            Assert.AreEqual(0, ((IDictionary)GetField(GetField(_bootstrapper, "_streams"), "_sharedPhysioPumpsByChannel")).Count);
            Assert.AreEqual(0, _root.GetComponentsInChildren<RedisPhysioMetricsPump>(true).Length);
            Assert.IsNull(GetField(GetField(_bootstrapper, "_streams"), "_sensorEngagementBackendObject"));
        }

        private void ConfigureAutomaticTemplates()
        {
            _bootstrapper.AutoSpawnAbstractViz = true;
            _bootstrapper.AutoSpawnGraphVisualsFromChannels = true;
            _bootstrapper.ShowDeviceLabels = false;
            _bootstrapper.ShowGraphGroupLabels = false;
            _bootstrapper.RedisChannels = new[] { "device:a:physio_metrics", "device:b:physio_metrics" };
            _bootstrapper.SensorIds = Array.Empty<string>();
            _bootstrapper.AbstractVizPrefab.AddComponent<AudienceSignalBinding>();
            _bootstrapper.AbstractVizPrefab.AddComponent<AudienceSignalVisualizerBinding>();
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

    }
}
