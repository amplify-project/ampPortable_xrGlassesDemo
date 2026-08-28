using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AmpPortableDataViz.Presentation.Bootstrap;
using AmpPortableDataViz.Presentation.Visualization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public sealed class BootstrapperArtistVizTests
    {
        private GameObject _testRoot;

        [TearDown]
        public void TearDown()
        {
            if (_testRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(_testRoot);
            }
        }

        [UnityTest]
        public IEnumerator SelectedArtistVisual_IsSpawnedRegisteredAndCleared()
        {
            _testRoot = new GameObject(nameof(BootstrapperArtistVizTests));
            _testRoot.SetActive(false);

            var bootstrapper = _testRoot.AddComponent<Bootstrapper>();
            bootstrapper.EmotionVisualOriginOffset = new Vector3(1f, 2f, 3f);
            var parentObject = new GameObject("ArtistVizParent");
            parentObject.transform.SetParent(_testRoot.transform, false);

            var template = new GameObject("ArtistVizTemplate");
            template.transform.SetParent(_testRoot.transform, false);
            template.AddComponent<ArtistVizVisualizer>();
            template.AddComponent<AudienceSignalVisualizerBinding>();

            GameObject instance = InvokeSpawnSelectedSensorVisual(
                bootstrapper,
                template,
                parentObject.transform);

            Assert.IsNotNull(instance);
            Assert.AreEqual(parentObject.transform, instance.transform.parent);
            Assert.AreEqual(Vector3.zero, instance.transform.localPosition);
            Assert.IsNotNull(instance.GetComponent<ArtistVizVisualizer>());

            var bindings = new List<AudienceSignalVisualizerBinding>();
            InvokeAddVisualizerBindings(instance, bindings);
            Assert.AreEqual(1, bindings.Count);

            SetField(bootstrapper, "_selectedArtistVisual", instance);
            Invoke(bootstrapper, "ClearSelectedSensorStreamMode");
            yield return null;

            Assert.IsTrue(instance == null);
            Assert.IsNull(GetField(bootstrapper, "_selectedArtistVisual"));
        }

        private static GameObject InvokeSpawnSelectedSensorVisual(
            Bootstrapper bootstrapper,
            GameObject prefab,
            Transform parent)
        {
            return (GameObject)Invoke(
                bootstrapper,
                "SpawnSelectedSensorVisual",
                prefab,
                parent,
                "SelectedSensor",
                false,
                true);
        }

        private static void InvokeAddVisualizerBindings(
            GameObject instance,
            ICollection<AudienceSignalVisualizerBinding> bindings)
        {
            MethodInfo method = typeof(Bootstrapper).GetMethod(
                "AddVisualizerBindings",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(null, new object[] { instance, bindings });
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method {methodName}.");
            return method.Invoke(target, arguments);
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
