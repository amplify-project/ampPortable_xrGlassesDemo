using AmpPortableDataViz.Presentation.ArtisticElements;
using NUnit.Framework;
using UnityEngine;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public sealed class ArtisticElementLengthTests
    {
        private static readonly Vector3 LengthDirection = new Vector3(-0.3935771f, 0.9192916f, 0f).normalized;
        private static readonly Vector3 WidthDirection = new Vector3(LengthDirection.y, -LengthDirection.x, 0f);
        private GameObject _root;
        private Transform _geometry;
        private ArtisticElementController _controller;
        private Matrix4x4 _authoredChildInverse;
        private Vector3 _rootPosition;
        private Quaternion _rootRotation;
        private Vector3 _rootScale;
        private Vector3 _childPosition;
        private Quaternion _childRotation;
        private Vector3 _childScale;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject(nameof(ArtisticElementLengthTests));
            _root.SetActive(false);
            _root.transform.localPosition = _rootPosition = new Vector3(1f, -2f, 3f);
            _root.transform.localRotation = _rootRotation = Quaternion.Euler(10f, 20f, -30f);
            _root.transform.localScale = _rootScale = new Vector3(2f, 3f, 0.5f);
            _geometry = new GameObject("Authored geometry").transform;
            _geometry.SetParent(_root.transform, false);
            _geometry.localPosition = _childPosition = new Vector3(-0.7f, 0.4f, 0.2f);
            _geometry.localRotation = _childRotation = Quaternion.Euler(25f, -16f, 8f);
            _geometry.localScale = _childScale = new Vector3(0.8f, 1.4f, 0.6f);
            _authoredChildInverse = Matrix4x4.TRS(_childPosition, _childRotation, _childScale).inverse;
            _controller = _root.AddComponent<ArtisticElementController>();
            _root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [TestCase(0.25f)]
        [TestCase(3f)]
        public void Length_StretchesSlantedLongEdgeAndPreservesWidthAndDepth(float multiplier)
        {
            _controller.ConfigureLength(true, LengthDirection);
            _controller.SetLengthMultiplier(multiplier);

            Assert.IsTrue(_controller.SupportsLength);
            Assert.AreEqual(multiplier, _controller.LengthMultiplier);
            AssertGeometry(multiplier);
            AssertVector(_rootScale, _root.transform.localScale);
            AssertVector(_rootPosition, _root.transform.localPosition);
            AssertRotation(_rootRotation, _root.transform.localRotation);
        }

        [Test]
        public void ConfigureAndReset_PreserveAuthoredChildAndRootTransforms()
        {
            Vector3 originalPoint = WorldPoint(new Vector3(0.7f, -1.2f, 0.3f));
            _controller.ConfigureLength(true, LengthDirection);
            AssertVector(originalPoint, WorldPoint(new Vector3(0.7f, -1.2f, 0.3f)));
            _controller.ConfigureThickness(true);
            _controller.SetLengthMultiplier(3f);
            _controller.SetScaleMultiplier(2f);
            _controller.SetThicknessMultiplier(0.4f);
            _controller.SetRotationOffset(new Vector3(35f, -25f, 12f));
            _controller.ResetToDefaults();

            Assert.AreEqual(1f, _controller.LengthMultiplier);
            Assert.AreEqual(1f, _controller.ScaleMultiplier);
            Assert.AreEqual(1f, _controller.ThicknessMultiplier);
            AssertVector(_rootPosition, _root.transform.localPosition);
            AssertVector(_rootScale, _root.transform.localScale);
            AssertRotation(_rootRotation, _root.transform.localRotation);
            AssertVector(_childPosition, _geometry.localPosition);
            AssertVector(_childScale, _geometry.localScale);
            AssertRotation(_childRotation, _geometry.localRotation);
            AssertVector(originalPoint, WorldPoint(new Vector3(0.7f, -1.2f, 0.3f)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Length_ComposesWithUniformScaleAndDepthInEitherOrder(bool lengthFirst)
        {
            _controller.ConfigureLength(true, LengthDirection);
            _controller.ConfigureThickness(true);
            _controller.SetRotationOffset(new Vector3(-20f, 40f, 10f));
            if (lengthFirst) _controller.SetLengthMultiplier(2f);
            _controller.SetThicknessMultiplier(0.35f);
            _controller.SetScaleMultiplier(1.8f);
            if (!lengthFirst) _controller.SetLengthMultiplier(2f);

            AssertVector(new Vector3(3.6f, 5.4f, 0.315f), _root.transform.localScale);
            AssertGeometry(2f);
        }

        [TestCase(-3f, ArtisticElementController.MinimumMultiplier)]
        [TestCase(0f, ArtisticElementController.MinimumMultiplier)]
        [TestCase(20f, ArtisticElementController.MaximumMultiplier)]
        public void Length_ClampsBoundsAndIgnoresNonFiniteRequests(float requested, float expected)
        {
            _controller.ConfigureLength(true, LengthDirection);
            _controller.SetLengthMultiplier(requested);
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                _controller.SetLengthMultiplier(invalid);
                Assert.AreEqual(expected, _controller.LengthMultiplier);
                AssertGeometry(expected);
            }
        }

        [Test]
        public void DisableLength_RestoresLengthKeepsScaleAndDepthAndIgnoresRequests()
        {
            _controller.SetLengthMultiplier(4f);
            Assert.IsFalse(_controller.SupportsLength);
            Assert.AreEqual(1f, _controller.LengthMultiplier);
            Assert.AreSame(_root.transform, _geometry.parent);
            _controller.ConfigureLength(true, LengthDirection);
            _controller.ConfigureThickness(true);
            _controller.SetLengthMultiplier(3f);
            _controller.SetScaleMultiplier(2f);
            _controller.SetThicknessMultiplier(0.5f);
            _controller.ConfigureLength(false, LengthDirection);
            _controller.SetLengthMultiplier(4f);

            Assert.IsFalse(_controller.SupportsLength);
            Assert.AreEqual(1f, _controller.LengthMultiplier);
            Assert.AreEqual(2f, _controller.ScaleMultiplier);
            Assert.AreEqual(0.5f, _controller.ThicknessMultiplier);
            AssertGeometry(1f);
        }

        [Test]
        public void RepeatedConfiguration_ReusesHierarchyAndPreservesGeometry()
        {
            _controller.ConfigureLength(true, LengthDirection);
            _controller.SetLengthMultiplier(2f);
            _controller.ConfigureLength(true, LengthDirection);
            _controller.ConfigureLength(false, LengthDirection);
            _controller.ConfigureLength(true, LengthDirection);
            _controller.SetLengthMultiplier(2f);

            Assert.AreEqual(1, _root.transform.childCount);
            Assert.AreEqual(4, _root.GetComponentsInChildren<Transform>(true).Length);
            AssertVector(_childPosition, _geometry.localPosition);
            AssertVector(_childScale, _geometry.localScale);
            AssertRotation(_childRotation, _geometry.localRotation);
            AssertGeometry(2f);
        }

        private void AssertGeometry(float multiplier)
        {
            Vector3 origin = WorldPoint(Vector3.zero);
            AssertVector(_root.transform.position, origin);
            AssertVector(_root.transform.TransformVector(LengthDirection * 2f * multiplier),
                WorldPoint(LengthDirection * 2f) - origin);
            AssertVector(_root.transform.TransformVector(WidthDirection * 2f),
                WorldPoint(WidthDirection * 2f) - origin);
            AssertVector(_root.transform.TransformVector(Vector3.forward * 2f),
                WorldPoint(Vector3.forward * 2f) - origin);
        }

        private Vector3 WorldPoint(Vector3 authoredRootPoint)
            => _geometry.TransformPoint(_authoredChildInverse.MultiplyPoint3x4(authoredRootPoint));

        private static void AssertVector(Vector3 expected, Vector3 actual)
            => Assert.Less(Vector3.Distance(expected, actual), 0.0001f);

        private static void AssertRotation(Quaternion expected, Quaternion actual)
            => Assert.Less(Quaternion.Angle(expected, actual), 0.001f);
    }
}
