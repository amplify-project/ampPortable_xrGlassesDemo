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
            BootstrapperTestConfiguration.Set(_bootstrapper, "AbstractVizPrefab", Child("VisualTemplate"));
            BootstrapperTestConfiguration.Set(_bootstrapper, "AbstractVizParent", _root.transform);
            BootstrapperTestConfiguration.Set(_bootstrapper, "GraphGroupParent", _root.transform);
            BootstrapperTestConfiguration.Set(_bootstrapper, "AutoDetectSensorIds", false);
            BootstrapperTestConfiguration.Set(_bootstrapper, "IncludeSensorChannels", false);
            BootstrapperTestConfiguration.Set(_bootstrapper, "UseIndividualSensorEngagement", false);
            BootstrapperTestConfiguration.Set(_bootstrapper, "AutoSpawnAbstractViz", false);
            BootstrapperTestConfiguration.Set(_bootstrapper, "AutoSpawnGraphVisualsFromChannels", false);
            BootstrapperTestConfiguration.Set(_bootstrapper, "AutoSpawnArtistVizFromChannels", false);
            BootstrapperTestConfiguration.Set(_bootstrapper, "RequireRedisEndpointConfirmation", true);
            BootstrapperTestConfiguration.Set(_bootstrapper, "RedisHost", "127.0.0.1");
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
            BootstrapperTestConfiguration.Set(_bootstrapper, "UseManualVisualizationDrivers", true);
            BootstrapperTestConfiguration.Set(_bootstrapper, "GraphPrefab", Child("ManualGraphTemplate"));
            BootstrapperTestConfiguration.Get<GameObject>(_bootstrapper, "GraphPrefab").AddComponent<GraphManualDriver>();
            Invoke(_bootstrapper, "ApplySignalConfiguration");
            var instances = (IList)GetField(GetVisuals(_bootstrapper), "_manualDriverInstances");
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
            var firstVisual = (GameObject)GetField(GetVisuals(_bootstrapper), "_selectedParticleVisual");
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
            Assert.IsNull(GetField(GetVisuals(_bootstrapper), "_selectedParticleVisual"));
            Assert.IsNull(GetField(GetVisuals(_bootstrapper), "_selectedGraphVisual"));
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
            BootstrapperTestConfiguration.Set(_bootstrapper, "UseSingleSelectedSensorStream", false);
            _bootstrapper.BeginAfterEndpoint();
            Assert.AreEqual(2, ((ICollection)GetField(GetVisuals(_bootstrapper), "_audienceDeviceInstances")).Count);
            Assert.AreEqual(2, ((ICollection)GetField(GetVisuals(_bootstrapper), "_audienceGraphDeviceGroups")).Count);
            Assert.AreEqual(2, ((IDictionary)GetField(GetField(_bootstrapper, "_streams"), "_sharedPhysioPumpsByChannel")).Count);
            Assert.AreEqual(2, _root.GetComponentsInChildren<RedisPhysioMetricsPump>(true).Length);
            foreach (object handle in ((IDictionary)GetField(GetField(_bootstrapper, "_streams"), "_sharedPhysioPumpsByChannel")).Values)
            {
                Assert.AreEqual(2, handle.GetType().GetField("ReferenceCount").GetValue(handle));
            }

            BootstrapperTestConfiguration.Set(_bootstrapper, "UseSingleSelectedSensorStream", true);
            Invoke(_bootstrapper, "ApplySignalConfiguration");
            yield return null;
            Assert.AreEqual(0, ((ICollection)GetField(GetVisuals(_bootstrapper), "_audienceDeviceInstances")).Count);
            Assert.AreEqual(0, ((ICollection)GetField(GetVisuals(_bootstrapper), "_audienceGraphDeviceGroups")).Count);
            Assert.AreEqual(2, _root.GetComponentsInChildren<RedisPhysioMetricsPump>(true).Length);
            Assert.AreEqual(2, _root.GetComponent<SensorStreamCoordinator>().SensorCount);

            BootstrapperTestConfiguration.Set(_bootstrapper, "UseManualVisualizationDrivers", true);
            Invoke(_bootstrapper, "ApplySignalConfiguration");
            yield return null;
            Assert.AreEqual(0, _root.GetComponent<SensorStreamCoordinator>().SensorCount);
            Assert.AreEqual(0, ((IDictionary)GetField(GetField(_bootstrapper, "_streams"), "_sharedPhysioPumpsByChannel")).Count);
            Assert.AreEqual(0, _root.GetComponentsInChildren<RedisPhysioMetricsPump>(true).Length);
            Assert.IsNull(GetField(GetField(_bootstrapper, "_streams"), "_sensorEngagementBackendObject"));
        }

        [UnityTest]
        public IEnumerator DisplayOnlyPrefab_SelectedModeSpawnsAndSwitchesWithoutLocalAssembler()
        {
            ConfigureAutomaticTemplates();
            var template = BootstrapperTestConfiguration.Get<GameObject>(_bootstrapper, "AbstractVizPrefab");
            UnityEngine.Object.DestroyImmediate(template.GetComponent<SensorSampleAssembler>());
            BootstrapperTestConfiguration.Set(_bootstrapper, "AutoSpawnGraphVisualsFromChannels", false);
            _bootstrapper.BeginAfterEndpoint();

            var visual = (GameObject)GetField(GetVisuals(_bootstrapper), "_selectedParticleVisual");
            Assert.IsNotNull(visual, "The abstract view must not depend on a prefab-local assembler.");
            Assert.IsNull(visual.GetComponentInChildren<SensorSampleAssembler>(true));
            var coordinator = _root.GetComponent<SensorStreamCoordinator>();
            Assert.AreEqual(2, coordinator.SensorCount);
            string firstSensor = coordinator.ActiveSensorId;
            Assert.IsTrue(coordinator.SelectNextSensor());
            Assert.AreNotEqual(firstSensor, coordinator.ActiveSensorId);
            Assert.AreSame(visual, GetField(GetVisuals(_bootstrapper), "_selectedParticleVisual"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisplayOnlyPrefab_PerDeviceModeCreatesAndReleasesAssemblers()
        {
            ConfigureAutomaticTemplates();
            var template = BootstrapperTestConfiguration.Get<GameObject>(_bootstrapper, "AbstractVizPrefab");
            UnityEngine.Object.DestroyImmediate(template.GetComponent<SensorSampleAssembler>());
            BootstrapperTestConfiguration.Set(_bootstrapper, "AutoSpawnGraphVisualsFromChannels", false);
            BootstrapperTestConfiguration.Set(_bootstrapper, "UseSingleSelectedSensorStream", false);
            _bootstrapper.BeginAfterEndpoint();

            var instances = (ICollection)GetField(GetVisuals(_bootstrapper), "_audienceDeviceInstances");
            Assert.AreEqual(2, instances.Count);
            var ownedAssemblers = new System.Collections.Generic.List<SensorSampleAssembler>();
            foreach (var instance in instances)
            {
                var visual = (GameObject)instance.GetType().GetField("VisualInstance").GetValue(instance);
                var assembler = visual.GetComponentInChildren<SensorSampleAssembler>(true);
                Assert.IsNotNull(assembler);
                Assert.IsTrue(assembler.enabled);
                ownedAssemblers.Add(assembler);
                var serializedBinding = visual.GetComponentInChildren<SensorVisualizationBinding>(true);
                Assert.AreSame(assembler, GetField(serializedBinding, "liveSource"));
            }
            Assert.IsNull(template.GetComponent<SensorSampleAssembler>(), "Runtime configuration must not modify the template.");

            Invoke(_bootstrapper, "OnDisable");
            yield return null;
            Assert.AreEqual(0, instances.Count);
            foreach (var assembler in ownedAssemblers)
            {
                Assert.IsTrue(assembler == null, "Each runtime assembler must be destroyed with its visual.");
            }
            Assert.AreEqual(0, _root.GetComponentsInChildren<RedisPhysioMetricsPump>(true).Length);
        }

        [UnityTest]
        public IEnumerator DisplayOnlyGraph_SelectedModeSpawnsReconfirmsAndReenables()
        {
            ConfigureAutomaticTemplates();
            var template = BootstrapperTestConfiguration.Get<GameObject>(_bootstrapper, "GraphPrefab");
            UnityEngine.Object.DestroyImmediate(template.GetComponent<SensorSampleAssembler>());
            BootstrapperTestConfiguration.Set(_bootstrapper, "AutoSpawnAbstractViz", false);
            _bootstrapper.BeginAfterEndpoint();

            var visual = (GameObject)GetField(GetVisuals(_bootstrapper), "_selectedGraphVisual");
            Assert.IsNotNull(visual, "The graph view must not depend on a prefab-local assembler.");
            Assert.IsNull(visual.GetComponentInChildren<SensorSampleAssembler>(true));
            var coordinator = _root.GetComponent<SensorStreamCoordinator>();
            Assert.AreEqual(2, coordinator.SensorCount);
            string firstSensor = coordinator.ActiveSensorId;
            Assert.IsTrue(coordinator.SelectNextSensor());
            Assert.AreNotEqual(firstSensor, coordinator.ActiveSensorId);
            Assert.AreSame(visual, GetField(GetVisuals(_bootstrapper), "_selectedGraphVisual"));

            _bootstrapper.BeginAfterEndpoint();
            yield return null;
            Assert.IsTrue(visual == null);
            Assert.IsNotNull(GetField(GetVisuals(_bootstrapper), "_selectedGraphVisual"));
            Assert.AreEqual(2, coordinator.SensorCount);

            Invoke(_bootstrapper, "OnDisable");
            yield return null;
            Assert.AreEqual(0, coordinator.SensorCount);
            Assert.AreEqual(0, _root.GetComponentsInChildren<RedisPhysioMetricsPump>(true).Length);
            Invoke(_bootstrapper, "OnEnable");
            Assert.IsNotNull(GetField(GetVisuals(_bootstrapper), "_selectedGraphVisual"));
            Assert.AreEqual(2, coordinator.SensorCount);
        }

        [UnityTest]
        public IEnumerator DisplayOnlyGraph_PerDeviceToSelectedModeReleasesLocalAssemblers()
        {
            ConfigureAutomaticTemplates();
            var template = BootstrapperTestConfiguration.Get<GameObject>(_bootstrapper, "GraphPrefab");
            UnityEngine.Object.DestroyImmediate(template.GetComponent<SensorSampleAssembler>());
            BootstrapperTestConfiguration.Set(_bootstrapper, "AutoSpawnAbstractViz", false);
            BootstrapperTestConfiguration.Set(_bootstrapper, "UseSingleSelectedSensorStream", false);
            _bootstrapper.BeginAfterEndpoint();

            var instances = (ICollection)GetField(GetVisuals(_bootstrapper), "_audienceGraphDeviceGroups");
            Assert.AreEqual(2, instances.Count);
            var ownedAssemblers = new System.Collections.Generic.List<SensorSampleAssembler>();
            foreach (var instance in instances)
            {
                var visual = (GameObject)instance.GetType().GetField("GroupRoot").GetValue(instance);
                var assembler = visual.GetComponentInChildren<SensorSampleAssembler>(true);
                Assert.IsNotNull(assembler);
                Assert.IsTrue(assembler.enabled);
                ownedAssemblers.Add(assembler);
                var visualizerBinding = visual.GetComponentInChildren<SensorVisualizationBinding>(true);
                Assert.AreSame(assembler, GetField(visualizerBinding, "liveSource"));
            }
            Assert.IsNull(template.GetComponent<SensorSampleAssembler>());

            BootstrapperTestConfiguration.Set(_bootstrapper, "UseSingleSelectedSensorStream", true);
            Invoke(_bootstrapper, "ApplySignalConfiguration");
            yield return null;
            Assert.AreEqual(0, instances.Count);
            foreach (var assembler in ownedAssemblers)
            {
                Assert.IsTrue(assembler == null, "Each per-device assembler must be destroyed with its graph group.");
            }
            Assert.IsNotNull(GetField(GetVisuals(_bootstrapper), "_selectedGraphVisual"));
            Assert.AreEqual(2, _root.GetComponent<SensorStreamCoordinator>().SensorCount);
            Assert.AreEqual(2, _root.GetComponentsInChildren<RedisPhysioMetricsPump>(true).Length);
            Invoke(_bootstrapper, "OnDisable");
            yield return null;
            Assert.AreEqual(0, _root.GetComponentsInChildren<RedisPhysioMetricsPump>(true).Length);
        }

        private void ConfigureAutomaticTemplates()
        {
            BootstrapperTestConfiguration.Set(_bootstrapper, "AutoSpawnAbstractViz", true);
            BootstrapperTestConfiguration.Set(_bootstrapper, "AutoSpawnGraphVisualsFromChannels", true);
            BootstrapperTestConfiguration.Set(_bootstrapper, "ShowDeviceLabels", false);
            BootstrapperTestConfiguration.Set(_bootstrapper, "ShowGraphGroupLabels", false);
            BootstrapperTestConfiguration.Set(_bootstrapper, "RedisChannels", new[] { "device:a:physio_metrics", "device:b:physio_metrics" });
            BootstrapperTestConfiguration.Set(_bootstrapper, "SensorIds", Array.Empty<string>());
            BootstrapperTestConfiguration.Get<GameObject>(_bootstrapper, "AbstractVizPrefab").AddComponent<SensorSampleAssembler>();
            BootstrapperTestConfiguration.Get<GameObject>(_bootstrapper, "AbstractVizPrefab").AddComponent<SensorVisualizationBinding>();
            BootstrapperTestConfiguration.Set(_bootstrapper, "GraphPrefab", Child("GraphTemplate"));
            BootstrapperTestConfiguration.Get<GameObject>(_bootstrapper, "GraphPrefab").AddComponent<SensorSampleAssembler>();
            BootstrapperTestConfiguration.Get<GameObject>(_bootstrapper, "GraphPrefab").AddComponent<SensorVisualizationBinding>();
            BootstrapperTestConfiguration.Get<GameObject>(_bootstrapper, "GraphPrefab").AddComponent<GraphVisualizer>();
            BootstrapperTestConfiguration.Set(_bootstrapper, "SensorHudCamera", Child("HudCamera").AddComponent<Camera>());
        }

        private GameObject Child(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(_root.transform, false);
            return child;
        }

        private static VisualizationPresenter GetVisuals(Bootstrapper bootstrapper)
        {
            return (VisualizationPresenter)typeof(Bootstrapper).GetProperty(
                "Visuals", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bootstrapper);
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
