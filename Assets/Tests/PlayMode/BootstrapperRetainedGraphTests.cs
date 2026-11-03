using System.Reflection;
using AmpPortableDataViz.Presentation.Bootstrap;
using AmpPortableDataViz.Presentation.Visualization;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public sealed class BootstrapperRetainedGraphTests
    {
        private GameObject _testRoot;
        private GameObject _graphInstance;
        private GameObject _manualGraphFixture;

        [TearDown]
        public void TearDown()
        {
            if (_graphInstance != null)
            {
                Object.DestroyImmediate(_graphInstance);
            }
            if (_testRoot != null)
            {
                Object.DestroyImmediate(_testRoot);
            }
            if (_manualGraphFixture != null)
            {
                Object.DestroyImmediate(_manualGraphFixture);
            }
        }

#if UNITY_EDITOR
        [Test]
        public void ManualGraphPrefab_RendersWithoutLiveSource()
        {
            _graphInstance = Object.Instantiate(CreateManualGraphFixture());
            AssertNoMissingScripts(_graphInstance);

            var driver = _graphInstance.GetComponent<GraphManualDriver>();
            Assert.IsNotNull(driver);
            driver.ApplyNow();

            var line = _graphInstance.GetComponent<LineRenderer>();
            Assert.IsNotNull(line);
            Assert.Greater(line.positionCount, 1);
            Assert.AreNotEqual(line.GetPosition(0), line.GetPosition(line.positionCount - 1));
        }

        [Test]
        public void ManualMode_SpawnsGraphWithRedisEndpointUnconfirmed()
        {
            Bootstrapper bootstrapper = CreateBootstrapper();
            BootstrapperTestConfiguration.Set(bootstrapper, "UseManualVisualizationDrivers", true);
            BootstrapperTestConfiguration.Set(bootstrapper, "RequireRedisEndpointConfirmation", true);
            BootstrapperTestConfiguration.Set(bootstrapper, "RedisHost", string.Empty);
            BootstrapperTestConfiguration.Set(bootstrapper, "GraphPrefab", CreateManualGraphFixture());
            BootstrapperTestConfiguration.Set(bootstrapper, "GraphGroupParent", _testRoot.transform);

            Invoke(bootstrapper, "ApplySignalConfiguration");

            var driver = _testRoot.GetComponentInChildren<GraphManualDriver>(true);
            Assert.IsNotNull(driver);
            Assert.IsTrue(driver.enabled);
            Assert.IsNotNull(driver.GetComponent<GraphVisualizer>());
            AssertNoMissingScripts(driver.gameObject);
        }

        [Test]
        public void SelectedGraphPrefab_KeepsVisualizerBindingAndDisablesLocalSources()
        {
            Bootstrapper bootstrapper = CreateBootstrapper();
            _graphInstance = (GameObject)Invoke(
                GetVisuals(bootstrapper), "SpawnSelectedSensorVisual", LoadPrefab("SensorGraphPrefab"),
                _testRoot.transform, "SelectedGraph", true, false);

            Assert.IsNotNull(_graphInstance);
            AssertNoMissingScripts(_graphInstance);
            Assert.IsNotNull(_graphInstance.GetComponentInChildren<GraphVisualizer>(true));
            var bindings = _graphInstance.GetComponentsInChildren<SensorVisualizationBinding>(true);
            Assert.IsNotEmpty(bindings);
            foreach (var binding in bindings)
            {
                Assert.IsTrue(binding.enabled);
            }
            foreach (var source in _graphInstance.GetComponentsInChildren<SensorSampleAssembler>(true))
            {
                Assert.IsFalse(source.enabled);
            }
            foreach (var driver in _graphInstance.GetComponentsInChildren<SensorDataSimulator>(true))
            {
                Assert.IsFalse(driver.enabled);
            }
        }

        private GameObject CreateManualGraphFixture()
        {
            _manualGraphFixture = new GameObject("ManualGraphFixture");
            var driver = _manualGraphFixture.AddComponent<GraphManualDriver>();
            driver.enabled = false;
            var serialized = new SerializedObject(driver);
            serialized.FindProperty("targetVisualizer").objectReferenceValue =
                _manualGraphFixture.GetComponent<GraphVisualizer>();
            serialized.FindProperty("yRange").vector2Value = new Vector2(0f, 1f);
            serialized.FindProperty("noiseOffset").floatValue = 13.953234f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return _manualGraphFixture;
        }

        private static GameObject LoadPrefab(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
            Assert.IsNotNull(prefab);
            return prefab;
        }

        private static void AssertNoMissingScripts(GameObject root)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject));
            }
        }
#endif

        private Bootstrapper CreateBootstrapper()
        {
            _testRoot = new GameObject(nameof(BootstrapperRetainedGraphTests));
            _testRoot.SetActive(false);
            return _testRoot.AddComponent<Bootstrapper>();
        }

        private static VisualizationPresenter GetVisuals(Bootstrapper bootstrapper)
        {
            return (VisualizationPresenter)typeof(Bootstrapper).GetProperty(
                "Visuals", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bootstrapper);
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return method.Invoke(target, arguments);
        }
    }
}
