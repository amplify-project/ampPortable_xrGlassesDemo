using System.Reflection;
using AmpPortableDataViz.Presentation.Interaction;
using AmpPortableDataViz.Presentation.Visualization;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public sealed class ArtistVizAssetWiringTests
    {
        private const string ArtistPrefabPath = "Assets/Prefabs/ArtistViz.prefab";
        private GameObject _testRoot;

        [TearDown]
        public void TearDown()
        {
            if (_testRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(_testRoot);
            }
        }

#if UNITY_EDITOR
        [Test]
        public void ArtistPrefab_HasVisualizerAndConfiguredBinding()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtistPrefabPath);

            Assert.IsNotNull(prefab);
            var visualizer = prefab.GetComponent<ArtistVizVisualizer>();
            var binding = prefab.GetComponent<AudienceSignalVisualizerBinding>();
            Assert.IsNotNull(visualizer);
            Assert.IsNotNull(binding);
            Assert.AreSame(visualizer, GetField(binding, "artistVizVisualizer"));
        }
#endif

        [Test]
        public void VisualizationToggle_CyclesAcrossFourRootsAndWraps()
        {
            _testRoot = new GameObject(nameof(ArtistVizAssetWiringTests));
            _testRoot.SetActive(false);
            var roots = new GameObject[4];
            for (int i = 0; i < roots.Length; i++)
            {
                roots[i] = new GameObject($"VisualizationRoot_{i}");
                roots[i].transform.SetParent(_testRoot.transform, false);
            }

            var toggle = _testRoot.AddComponent<RayNeoTempleVisualizationToggle>();
            SetField(toggle, "visualizationRoots", roots);

            toggle.Show(0);
            for (int i = 1; i <= roots.Length; i++)
            {
                toggle.ShowNext();
                int expectedIndex = i % roots.Length;
                Assert.AreEqual(expectedIndex, toggle.CurrentIndex);
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    Assert.AreEqual(rootIndex == expectedIndex, roots[rootIndex].activeSelf);
                }
            }
        }

        private static object GetField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field {fieldName}.");
            return field.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field {fieldName}.");
            field.SetValue(target, value);
        }
    }
}
