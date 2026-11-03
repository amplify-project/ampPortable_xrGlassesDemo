using System.Reflection;
using AmpPortableDataViz.Presentation.Bootstrap;
using AmpPortableDataViz.Presentation.Visualization;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public sealed class BootstrapperPlacementTests
    {
        private GameObject _root;
        private Bootstrapper _bootstrapper;
        private Transform _target;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject(nameof(BootstrapperPlacementTests));
            _root.SetActive(false);
            _root.transform.SetPositionAndRotation(new Vector3(20f, 30f, 40f), Quaternion.Euler(0f, 35f, 0f));
            _bootstrapper = _root.AddComponent<Bootstrapper>();
            _bootstrapper.SpawnPosition = new Vector3(3f, 4f, 5f);
            _bootstrapper.SpawnRotationEuler = new Vector3(10f, 20f, 30f);
            _bootstrapper.AbstractVizOriginOffset = new Vector3(1f, 2f, 3f);
            _bootstrapper.AbstractVizSpacing = new Vector3(4f, 5f, 6f);
            _bootstrapper.AbstractVizPerRow = 2;
            _bootstrapper.GraphGroupOriginOffset = new Vector3(2f, 3f, 4f);
            _bootstrapper.GraphGroupSpacing = new Vector3(5f, 6f, 7f);
            _bootstrapper.GraphGroupsPerRow = 2;
            _target = new GameObject("Target").transform;
            _target.SetParent(_root.transform, false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AbstractPlacement_PreservesParentLocalAndWorldFallback(bool useLocalSpace)
        {
            Invoke("PositionAbstractViz", _target, 3, useLocalSpace);
            AssertPlacement(useLocalSpace, new Vector3(5f, 7f, 9f), new Vector3(8f, 11f, 14f));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void GraphPlacement_PreservesParentLocalAndWorldFallback(bool useLocalSpace)
        {
            Invoke("PositionGraphGroup", _target, 3, useLocalSpace);
            AssertPlacement(useLocalSpace, new Vector3(7f, 9f, 11f), new Vector3(10f, 13f, 16f));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ArtistPlacement_PreservesParentOriginAndWorldFallback(bool useLocalSpace)
        {
            Invoke("PositionAtParentOrigin", _target, useLocalSpace);
            AssertPlacement(useLocalSpace, Vector3.zero, new Vector3(3f, 4f, 5f));
        }

        [TestCase(0f, 0f, 0.0001f, 0.01f)]
        [TestCase(0.025f, 8f, 0.025f, 8f)]
        public void SensorHud_PreservesCameraPlacementStyleAndSizeFallback(
            float scale, float fontSize, float expectedScale, float expectedFontSize)
        {
            var cameraObject = new GameObject("HudCamera");
            cameraObject.transform.SetParent(_root.transform, false);
            _bootstrapper.SensorHudCamera = cameraObject.AddComponent<Camera>();
            _bootstrapper.SensorHudLocalPosition = new Vector3(1f, 2f, 3f);
            _bootstrapper.SensorHudLocalScale = scale;
            _bootstrapper.SensorHudFontSize = fontSize;
            _bootstrapper.SensorHudColor = Color.cyan;
            var coordinator = _root.AddComponent<SensorStreamCoordinator>();
            var layout = (VisualizationLayout)typeof(Bootstrapper).GetProperty(
                "Layout", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_bootstrapper);

            GameObject hud = layout.CreateSensorHud(coordinator);

            Assert.AreSame(cameraObject.transform, hud.transform.parent);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), hud.transform.localPosition);
            Assert.AreEqual(Quaternion.identity, hud.transform.localRotation);
            Assert.AreEqual(Vector3.one * expectedScale, hud.transform.localScale);
            var label = hud.GetComponent<TextMeshPro>();
            Assert.AreEqual(expectedFontSize, label.fontSize, 0.00001f);
            Assert.AreEqual(Color.cyan, label.color);
            Assert.AreEqual("NO SENSOR STREAMS", label.text);
            Assert.IsNotNull(hud.GetComponent<ActiveSensorHudBinding>());
        }

        private void AssertPlacement(bool useLocalSpace, Vector3 localPosition, Vector3 worldPosition)
        {
            Vector3 actual = useLocalSpace ? _target.localPosition : _target.position;
            Vector3 expected = useLocalSpace ? localPosition : worldPosition;
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.0001f));
            Quaternion rotation = useLocalSpace ? _target.localRotation : _target.rotation;
            Quaternion expectedRotation = useLocalSpace ? Quaternion.identity : Quaternion.Euler(10f, 20f, 30f);
            Assert.That(Quaternion.Angle(rotation, expectedRotation), Is.LessThan(0.01f));
        }

        private void Invoke(string name, params object[] arguments)
        {
            var layout = (VisualizationLayout)typeof(Bootstrapper).GetProperty(
                "Layout", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_bootstrapper);
            MethodInfo method = typeof(VisualizationLayout).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(layout, arguments);
        }
    }
}
