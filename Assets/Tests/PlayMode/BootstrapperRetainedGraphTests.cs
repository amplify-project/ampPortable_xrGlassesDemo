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
        }

#if UNITY_EDITOR
        [Test]
        public void ManualGraphPrefab_RendersWithoutLiveSource()
        {
            _graphInstance = Object.Instantiate(LoadPrefab("GraphPrefab"));
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
            bootstrapper.UseManualVisualizationDrivers = true;
            bootstrapper.RequireRedisEndpointConfirmation = true;
            bootstrapper.RedisHost = string.Empty;
            bootstrapper.GraphPrefab = LoadPrefab("GraphPrefab");
            bootstrapper.GraphGroupParent = _testRoot.transform;

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
                GetVisuals(bootstrapper), "SpawnSelectedSensorVisual", LoadPrefab("AudienceGraphPrefab"),
                _testRoot.transform, "SelectedGraph", true, false);

            Assert.IsNotNull(_graphInstance);
            AssertNoMissingScripts(_graphInstance);
            Assert.IsNotNull(_graphInstance.GetComponentInChildren<GraphVisualizer>(true));
            var bindings = _graphInstance.GetComponentsInChildren<AudienceSignalVisualizerBinding>(true);
            Assert.IsNotEmpty(bindings);
            foreach (var binding in bindings)
            {
                Assert.IsTrue(binding.enabled);
            }
            foreach (var source in _graphInstance.GetComponentsInChildren<AudienceSignalBinding>(true))
            {
                Assert.IsFalse(source.enabled);
            }
            foreach (var driver in _graphInstance.GetComponentsInChildren<AudienceDataManualDriver>(true))
            {
                Assert.IsFalse(driver.enabled);
            }
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
