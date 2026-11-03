using System;
using System.Linq;
using System.Reflection;
using AmpPortableDataViz.Presentation.Bootstrap;
using AmpPortableDataViz.Presentation.Visualization;
using AmpPortableDataViz.Core;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public sealed class RayNeoParticlePrefabTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                UnityEngine.Object.DestroyImmediate(_root);
            }
        }

#if UNITY_EDITOR
        [TestCase("ParticleMeshViz")]
        [TestCase("ParticleMeshVizRoot")]
        [TestCase("ParticleMeshVizRoot_RayNeo")]
        [TestCase("AudienceGraphPrefab")]
        [TestCase("ArtistViz")]
        public void RenamedComponents_AllRelatedPrefabsHaveNoMissingScripts(string name)
        {
            var prefab = LoadPrefab(name);
            foreach (var child in prefab.GetComponentsInChildren<Transform>(true))
            {
                Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject), child.name);
            }
        }

        [Test]
        public void RayNeoVariant_HasDisplayBindingAndSimulatorWithoutGrabPhysicsOrLocalAssembler()
        {
            var prefab = LoadPrefab("ParticleMeshVizRoot_RayNeo");
            var renderer = prefab.GetComponentInChildren<ParticleMeshVisualizer>(true);
            var binding = prefab.GetComponentInChildren<SensorVisualizationBinding>(true);
            var simulator = prefab.GetComponentInChildren<SensorDataSimulator>(true);
            Assert.IsNotNull(renderer);
            Assert.IsNotNull(binding);
            Assert.IsNotNull(simulator);
            Assert.AreSame(renderer, new SerializedObject(binding).FindProperty("particleMeshVisualizer").objectReferenceValue);
            Assert.AreSame(simulator, new SerializedObject(binding).FindProperty("manualSource").objectReferenceValue);
            Assert.IsNull(new SerializedObject(binding).FindProperty("liveSource").objectReferenceValue);
            Assert.IsNull(prefab.GetComponentInChildren<SensorSampleAssembler>(true));
            Assert.IsEmpty(prefab.GetComponentsInChildren<Rigidbody>(true));
            Assert.IsFalse(prefab.GetComponentsInChildren<Component>(true).Any(component =>
                component != null && component.GetType().Name == "XRGrabInteractable"));

            var basePrefab = LoadPrefab("ParticleMeshVizRoot");
            Assert.IsNotNull(basePrefab.GetComponent<Rigidbody>(), "The shared base prefab keeps its existing interaction setup.");
            Assert.IsNotNull(basePrefab.GetComponentInChildren<SensorSampleAssembler>(true));
            Assert.IsNotNull(basePrefab.GetComponentInChildren<SensorDataSimulator>(true));
        }

        [Test]
        public void RayNeoVariant_ColliderStillSupportsGazeRaycastsWithoutRigidbody()
        {
            _root = UnityEngine.Object.Instantiate(LoadPrefab("ParticleMeshVizRoot_RayNeo"));
            _root.transform.position = new Vector3(100f, 100f, 100f);
            Physics.SyncTransforms();
            var collider = _root.GetComponentInChildren<Collider>();
            Assert.IsNotNull(collider);
            Assert.IsTrue(collider.enabled);
            Assert.IsNull(collider.attachedRigidbody);

            var ray = new Ray(collider.bounds.center - Vector3.forward * 2f, Vector3.forward);
            Assert.IsTrue(Physics.RaycastAll(ray, 4f, ~0, QueryTriggerInteraction.Collide)
                .Any(hit => hit.collider == collider));
        }

        [Test]
        public void RayNeoVariant_SelectedSourceUpdatesRendererAndReplaysAfterReenable()
        {
            _root = new GameObject(nameof(RayNeoParticlePrefabTests));
            var coordinator = _root.AddComponent<SensorStreamCoordinator>();
            var visual = UnityEngine.Object.Instantiate(LoadPrefab("ParticleMeshVizRoot_RayNeo"), _root.transform);
            visual.GetComponentInChildren<SensorDataSimulator>().enabled = false;
            var binding = visual.GetComponentInChildren<SensorVisualizationBinding>();
            var renderer = visual.GetComponentInChildren<ParticleMeshVisualizer>();
            var first = new FakeSensorSource("sensor-a");
            var second = new FakeSensorSource("sensor-b");
            first.Emit(0.2f);
            second.Emit(0.8f);
            coordinator.ConfigureVisualizerBindings(new[] { binding });
            coordinator.ConfigureSources(new IDataSource<AudienceSignalSample>[] { first, second });
            AssertTargetEngagement(renderer, 0.2f);

            second.Emit(0.9f);
            AssertTargetEngagement(renderer, 0.2f);
            Assert.IsTrue(coordinator.SelectNextSensor());
            AssertTargetEngagement(renderer, 0.9f);

            binding.enabled = false;
            second.Emit(0.7f);
            AssertTargetEngagement(renderer, 0.9f);
            binding.enabled = true;
            AssertTargetEngagement(renderer, 0.7f);
            first.Emit(0.1f);
            AssertTargetEngagement(renderer, 0.7f);
            Assert.IsNull(visual.GetComponentInChildren<SensorSampleAssembler>(true));
        }

        [Test]
        public void RayNeoVariant_ManualModeStillSpawnsAndEmitsWithoutRedis()
        {
            _root = new GameObject(nameof(RayNeoParticlePrefabTests));
            _root.SetActive(false);
            var bootstrapper = _root.AddComponent<Bootstrapper>();
            BootstrapperTestConfiguration.Set(bootstrapper, "AbstractVizPrefab", LoadPrefab("ParticleMeshVizRoot_RayNeo"));
            BootstrapperTestConfiguration.Set(bootstrapper, "AbstractVizParent", _root.transform);
            BootstrapperTestConfiguration.Set(bootstrapper, "UseManualVisualizationDrivers", true);
            BootstrapperTestConfiguration.Set(bootstrapper, "AutoDetectSensorIds", false);
            BootstrapperTestConfiguration.Set(bootstrapper, "RedisHost", string.Empty);
            typeof(Bootstrapper).GetMethod("ApplySignalConfiguration", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(bootstrapper, null);
            _root.SetActive(true);

            var simulator = _root.GetComponentInChildren<SensorDataSimulator>(true);
            Assert.IsNotNull(simulator);
            Assert.IsTrue(simulator.enabled);
            simulator.SetManualValues(0f, 0f, 0f, 1f, 0f, 1f, 1f, 0.75f);
            simulator.EmitNow();
            Assert.IsTrue(simulator.HasLatestFrame);
            Assert.AreEqual(0.75f, simulator.LatestFrame.Payload.Engagement);
            var binding = simulator.GetComponent<SensorVisualizationBinding>();
            Assert.IsTrue(binding.enabled);
            Assert.AreSame(simulator, new SerializedObject(binding).FindProperty("manualSource").objectReferenceValue);
            var renderer = simulator.GetComponent<ParticleMeshVisualizer>();
            AssertTargetEngagement(renderer, 0.75f);
        }

        private static GameObject LoadPrefab(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
            Assert.IsNotNull(prefab);
            return prefab;
        }

        private static void AssertTargetEngagement(ParticleMeshVisualizer renderer, float expected)
        {
            Assert.AreEqual(expected, typeof(ParticleMeshVisualizer)
                .GetField("_targetEngagement", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(renderer));
        }

        private sealed class FakeSensorSource : ILatestDataSource<AudienceSignalSample>
        {
            public FakeSensorSource(string sourceId)
            {
                SourceId = sourceId;
            }

            public string SourceId { get; }
            public bool HasLatestFrame { get; private set; }
            public DataFrame<AudienceSignalSample> LatestFrame { get; private set; }
            public event Action<DataFrame<AudienceSignalSample>> OnFrame;

            public void Emit(float engagement)
            {
                var sample = new AudienceSignalSample(SourceId, 0f, 0f, 0f, 0f, 0f, engagement, 1f, 1f);
                LatestFrame = new DataFrame<AudienceSignalSample>(DateTime.UtcNow.Ticks, 1, sample);
                HasLatestFrame = true;
                OnFrame?.Invoke(LatestFrame);
            }
        }
#endif
    }
}
