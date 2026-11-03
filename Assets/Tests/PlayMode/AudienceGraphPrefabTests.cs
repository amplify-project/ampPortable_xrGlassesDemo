using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Presentation.Bootstrap;
using AmpPortableDataViz.Presentation.Visualization;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public sealed class AudienceGraphPrefabTests
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
        [Test]
        public void Prefab_KeepsFiveGraphTargetsAndSimulatorWithoutGrabPhysicsOrLocalAssembler()
        {
            var prefab = LoadPrefab();
            var binding = prefab.GetComponent<SensorVisualizationBinding>();
            var simulator = prefab.GetComponent<SensorDataSimulator>();
            Assert.IsNotNull(binding);
            Assert.IsNotNull(simulator);
            Assert.IsNull(prefab.GetComponentInChildren<SensorSampleAssembler>(true));
            Assert.IsEmpty(prefab.GetComponentsInChildren<Rigidbody>(true));
            Assert.IsFalse(prefab.GetComponentsInChildren<Component>(true).Any(component =>
                component != null && component.GetType().Name == "XRGrabInteractable"));
            var serialized = new SerializedObject(binding);
            Assert.IsNull(serialized.FindProperty("liveSource").objectReferenceValue);
            Assert.AreSame(simulator, serialized.FindProperty("manualSource").objectReferenceValue);

            var streams = GetField<SensorVisualizationBinding.GraphMetricBinding[]>(binding, "graphStreams");
            Assert.AreEqual(5, streams.Length);
            CollectionAssert.AreEquivalent(new[]
            {
                AudienceMetricKind.TonicElectrodermalActivityStdDev,
                AudienceMetricKind.TemperatureRateOfChangeStdDev,
                AudienceMetricKind.SkinConductanceResponseFrequencyStdDev,
                AudienceMetricKind.HeartRateStdDev,
                AudienceMetricKind.Engagement
            }, streams.Select(stream => stream.Metric));
            CollectionAssert.AreEquivalent(prefab.GetComponentsInChildren<GraphVisualizer>(true),
                streams.Select(stream => stream.Visualizer));
            foreach (var child in prefab.GetComponentsInChildren<Transform>(true))
            {
                Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject), child.name);
            }
        }

        [Test]
        public void Prefab_ColliderSupportsGazeRaycastsWithoutRigidbody()
        {
            _root = UnityEngine.Object.Instantiate(LoadPrefab());
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
        public void SelectedSensor_UpdatesAllGraphsAndResetsHistoryOnSwitchAndReenable()
        {
            _root = new GameObject(nameof(AudienceGraphPrefabTests));
            var coordinator = _root.AddComponent<SensorStreamCoordinator>();
            var visual = UnityEngine.Object.Instantiate(LoadPrefab(), _root.transform);
            visual.GetComponent<SensorDataSimulator>().enabled = false;
            var binding = visual.GetComponent<SensorVisualizationBinding>();
            var first = new FakeSensorSource("sensor-a");
            var second = new FakeSensorSource("sensor-b");
            first.Emit(0.2f);
            second.Emit(0.8f);
            coordinator.ConfigureVisualizerBindings(new[] { binding });
            coordinator.ConfigureSources(new IDataSource<AudienceSignalSample>[] { first, second });
            AssertGraphSampleCounts(binding, 1);
            AssertEngagement(binding, 0.2f);

            first.Emit(0.3f);
            AssertGraphSampleCounts(binding, 2);
            second.Emit(0.9f);
            AssertGraphSampleCounts(binding, 2);
            Assert.IsTrue(coordinator.SelectNextSensor());
            AssertGraphSampleCounts(binding, 1);
            AssertEngagement(binding, 0.9f);

            binding.enabled = false;
            second.Emit(0.7f);
            AssertGraphSampleCounts(binding, 0);
            binding.enabled = true;
            AssertGraphSampleCounts(binding, 1);
            AssertEngagement(binding, 0.7f);
            first.Emit(0.1f);
            AssertGraphSampleCounts(binding, 1);
            Assert.IsNull(visual.GetComponent<SensorSampleAssembler>());
        }

        [Test]
        public void ManualMode_EmitsSamplesIntoAllFiveGraphsWithoutRedis()
        {
            _root = new GameObject(nameof(AudienceGraphPrefabTests));
            _root.SetActive(false);
            var bootstrapper = _root.AddComponent<Bootstrapper>();
            BootstrapperTestConfiguration.Set(bootstrapper, "GraphPrefab", LoadPrefab());
            BootstrapperTestConfiguration.Set(bootstrapper, "GraphGroupParent", _root.transform);
            BootstrapperTestConfiguration.Set(bootstrapper, "UseManualVisualizationDrivers", true);
            BootstrapperTestConfiguration.Set(bootstrapper, "AutoDetectSensorIds", false);
            BootstrapperTestConfiguration.Set(bootstrapper, "RedisHost", string.Empty);
            _root.SetActive(true);

            var simulator = _root.GetComponentInChildren<SensorDataSimulator>();
            Assert.IsNotNull(simulator);
            Assert.IsTrue(simulator.enabled);
            var binding = simulator.GetComponent<SensorVisualizationBinding>();
            Assert.IsTrue(binding.enabled);
            simulator.SetManualValues(0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 1f, 1f, 0.75f);
            // The prefab is renamed after instantiation, changing the simulator's fallback sensor ID.
            // Establish that identity before checking the next sample appends to its history.
            simulator.EmitNow();
            int before = GetField<List<Vector2>[]>(binding, "_graphSamples")[0].Count;
            simulator.EmitNow();
            AssertGraphSampleCounts(binding, before + 1);
            foreach (var graph in simulator.GetComponentsInChildren<GraphVisualizer>())
            {
                Assert.Greater(graph.GetComponent<LineRenderer>().positionCount, 0);
            }
        }

        private static GameObject LoadPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SensorGraphPrefab.prefab");
            Assert.IsNotNull(prefab);
            return prefab;
        }

        private static void AssertGraphSampleCounts(SensorVisualizationBinding binding, int expected)
        {
            var samples = GetField<List<Vector2>[]>(binding, "_graphSamples");
            Assert.AreEqual(5, samples.Length);
            foreach (var series in samples)
            {
                Assert.AreEqual(expected, series.Count);
            }
        }

        private static void AssertEngagement(SensorVisualizationBinding binding, float expected)
        {
            var streams = GetField<SensorVisualizationBinding.GraphMetricBinding[]>(binding, "graphStreams");
            int index = Array.FindIndex(streams, stream => stream.Metric == AudienceMetricKind.Engagement);
            Assert.GreaterOrEqual(index, 0);
            Assert.AreEqual(expected, GetField<List<Vector2>[]>(binding, "_graphSamples")[index][0].y);
        }

        private static T GetField<T>(object target, string name)
        {
            return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }

        private sealed class FakeSensorSource : ILatestDataSource<AudienceSignalSample>
        {
            private int _sequenceId;

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
                var sample = new AudienceSignalSample(SourceId, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, engagement, 1f, 1f);
                LatestFrame = new DataFrame<AudienceSignalSample>(DateTime.UtcNow.Ticks, _sequenceId++, sample);
                HasLatestFrame = true;
                OnFrame?.Invoke(LatestFrame);
            }
        }
#endif
    }
}
