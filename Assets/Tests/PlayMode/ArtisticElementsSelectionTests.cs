using System;
using AmpPortableDataViz.Presentation.ArtisticElements;
using NUnit.Framework;
using UnityEngine;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public sealed class ArtisticElementsSelectionTests
    {
        private GameObject _root;
        private ArtisticElementsSelectionController _selection;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject(nameof(ArtisticElementsSelectionTests));
            _root.SetActive(false);
            _selection = _root.AddComponent<ArtisticElementsSelectionController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void SwitchingSelection_KeepsAllElementsVisibleAndRetainsTheirSettings()
        {
            Color firstOriginalColour = new Color(0.2f, 0.4f, 0.6f, 0.7f);
            Color secondOriginalColour = new Color(0.6f, 0.4f, 0.2f, 0.5f);
            Quaternion secondOriginalRotation = Quaternion.Euler(10f, 20f, 30f);
            Vector3 secondOriginalScale = new Vector3(0.3f, 0.6f, 1f);
            ArtisticElementController first = CreateElement("BabyPhoto", firstOriginalColour,
                new Vector3(2f, 3f, 1f), Quaternion.Euler(5f, 15f, 25f));
            ArtisticElementController second = CreateElement("Asset Graphic Element", secondOriginalColour,
                secondOriginalScale, secondOriginalRotation);
            ActivateAndConfigure(first, second);

            _selection.SetColour(Color.red);
            _selection.SetScaleMultiplier(2f);
            _selection.SetRotationOffset(new Vector3(12f, 34f, 56f));
            Vector3 firstEditedScale = first.transform.localScale;
            Quaternion firstEditedRotation = first.transform.localRotation;
            Assert.IsTrue(_selection.SelectElement(1));

            AssertVector(secondOriginalScale, second.transform.localScale);
            Assert.Less(Quaternion.Angle(secondOriginalRotation, second.transform.localRotation), 0.001f);
            AssertColour(secondOriginalColour, second.GetComponent<SpriteRenderer>().color);

            _selection.SetColour(Color.blue);
            _selection.SetScaleMultiplier(0.5f);
            Assert.IsTrue(_selection.SelectElement(0));

            Assert.AreSame(first, _selection.SelectedElement);
            Assert.IsTrue(first.gameObject.activeInHierarchy);
            Assert.IsTrue(second.gameObject.activeInHierarchy);
            AssertVector(firstEditedScale, first.transform.localScale);
            Assert.Less(Quaternion.Angle(firstEditedRotation, first.transform.localRotation), 0.001f);
            AssertColour(new Color(1f, 0f, 0f, firstOriginalColour.a), first.Colour);
            AssertColour(new Color(0f, 0f, 1f, secondOriginalColour.a), second.Colour);
            Assert.AreEqual(2f, first.ScaleMultiplier);
            Assert.AreEqual(0.5f, second.ScaleMultiplier);
        }

        [Test]
        public void PanelActions_OnlyAffectSelectedElementAndImageThicknessDoesNothing()
        {
            ArtisticElementController image = CreateElement("Eye", Color.white,
                new Vector3(2f, 1f, 1f), Quaternion.identity);
            ArtisticElementController block = CreateElement("RectangleBlock_obj", Color.white,
                new Vector3(1f, 2f, 3f), Quaternion.identity, true);
            ActivateAndConfigure(image, block);

            _selection.SetColour(Color.green);
            _selection.SetRotationOffset(new Vector3(20f, 30f, 40f));
            _selection.SetScaleMultiplier(2f);
            _selection.SetThicknessMultiplier(4f);

            AssertColour(Color.green, image.Colour);
            AssertVector(new Vector3(20f, 30f, 40f), image.RotationOffset);
            AssertVector(new Vector3(4f, 2f, 2f), image.transform.localScale);
            Assert.AreEqual(1f, image.ThicknessMultiplier);
            AssertColour(Color.white, block.Colour);
            AssertVector(Vector3.zero, block.RotationOffset);
            AssertVector(new Vector3(1f, 2f, 3f), block.transform.localScale);

            _selection.SelectElement(1);
            _selection.SetThicknessMultiplier(0.25f);
            _selection.SetScaleMultiplier(3f);

            AssertVector(new Vector3(3f, 6f, 2.25f), block.transform.localScale);
            AssertVector(new Vector3(4f, 2f, 2f), image.transform.localScale);
            Assert.AreEqual(1f, image.ThicknessMultiplier);
        }

        [Test]
        public void ResetSelectedAndResetAll_RestoreOnlyTheirIntendedElements()
        {
            Color firstOriginalColour = new Color(0.1f, 0.2f, 0.3f, 0.4f);
            Color secondOriginalColour = new Color(0.4f, 0.3f, 0.2f, 0.6f);
            Vector3 firstOriginalScale = new Vector3(2f, 3f, 4f);
            Vector3 secondOriginalScale = new Vector3(0.5f, 0.75f, 1f);
            Quaternion firstOriginalRotation = Quaternion.Euler(5f, 10f, 15f);
            Quaternion secondOriginalRotation = Quaternion.Euler(20f, 25f, 30f);
            ArtisticElementController first = CreateElement("RectangleBlock_obj", firstOriginalColour,
                firstOriginalScale, firstOriginalRotation, true);
            ArtisticElementController second = CreateElement("BlurAsset_orig", secondOriginalColour,
                secondOriginalScale, secondOriginalRotation);
            ActivateAndConfigure(first, second);
            _selection.SetColour(Color.red);
            _selection.SetRotationOffset(new Vector3(30f, 40f, 50f));
            _selection.SetScaleMultiplier(2f);
            _selection.SetThicknessMultiplier(0.5f);
            Vector3 firstEditedScale = first.transform.localScale;
            _selection.SelectElement(1);
            _selection.SetColour(Color.blue);
            _selection.SetRotationOffset(new Vector3(-20f, -30f, -40f));
            _selection.SetScaleMultiplier(3f);

            _selection.ResetSelected();

            AssertVector(secondOriginalScale, second.transform.localScale);
            Assert.Less(Quaternion.Angle(secondOriginalRotation, second.transform.localRotation), 0.001f);
            AssertColour(secondOriginalColour, second.Colour);
            AssertVector(firstEditedScale, first.transform.localScale);
            AssertColour(new Color(1f, 0f, 0f, firstOriginalColour.a), first.Colour);

            _selection.ResetAll();

            AssertVector(firstOriginalScale, first.transform.localScale);
            Assert.Less(Quaternion.Angle(firstOriginalRotation, first.transform.localRotation), 0.001f);
            AssertColour(firstOriginalColour, first.Colour);
            Assert.AreEqual(1f, first.ThicknessMultiplier);
            Assert.AreEqual(1f, first.ScaleMultiplier);
            AssertVector(secondOriginalScale, second.transform.localScale);
            AssertColour(secondOriginalColour, second.Colour);
            Assert.AreEqual(1, _selection.SelectedIndex);
            Assert.IsTrue(first.gameObject.activeInHierarchy);
            Assert.IsTrue(second.gameObject.activeInHierarchy);
        }

        [Test]
        public void EmptySelection_HandlesActionsAndInvalidIndicesSafely()
        {
            ActivateAndConfigure(Array.Empty<ArtisticElementController>());

            Assert.AreEqual(0, _selection.ElementCount);
            Assert.AreEqual(-1, _selection.SelectedIndex);
            Assert.IsNull(_selection.SelectedElement);
            Assert.IsNull(_selection.GetElement(-1));
            Assert.IsNull(_selection.GetElement(0));
            Assert.IsFalse(_selection.SelectElement(-1));
            Assert.IsFalse(_selection.SelectElement(0));
            Assert.DoesNotThrow(() =>
            {
                _selection.SetColour(Color.red);
                _selection.SetRotationOffset(new Vector3(10f, 20f, 30f));
                _selection.SetScaleMultiplier(2f);
                _selection.SetThicknessMultiplier(2f);
                _selection.ResetSelected();
                _selection.ResetAll();
            });
            Assert.AreEqual(-1, _selection.SelectedIndex);
            Assert.IsNull(_selection.SelectedElement);
        }

        [Test]
        public void ChangedEvent_ObserversSeeUpdatedSelectionControlsAndResets()
        {
            ArtisticElementController first = CreateElement("BabyPhoto", Color.white,
                Vector3.one, Quaternion.identity);
            ArtisticElementController second = CreateElement("Asset Graphic Element", Color.white,
                Vector3.one, Quaternion.identity);
            _root.SetActive(true);
            int changes = 0;
            int observedIndex = -1;
            ArtisticElementController observedElement = null;
            float observedScale = 0f;
            Color observedColour = Color.clear;
            float firstScaleAtChange = 0f;
            float secondScaleAtChange = 0f;
            _selection.Changed += () =>
            {
                changes++;
                observedIndex = _selection.SelectedIndex;
                observedElement = _selection.SelectedElement;
                observedScale = observedElement != null ? observedElement.ScaleMultiplier : 0f;
                observedColour = observedElement != null ? observedElement.Colour : Color.clear;
                firstScaleAtChange = first.ScaleMultiplier;
                secondScaleAtChange = second.ScaleMultiplier;
            };

            _selection.Configure(new[] { first, second });
            Assert.AreEqual(1, changes);
            Assert.AreEqual(0, observedIndex);
            Assert.AreSame(first, observedElement);

            _selection.SetScaleMultiplier(2f);
            Assert.AreEqual(2, changes);
            Assert.AreEqual(2f, observedScale);
            _selection.SetColour(Color.red);
            Assert.AreEqual(3, changes);
            AssertColour(Color.red, observedColour);
            _selection.SelectElement(1);
            Assert.AreEqual(4, changes);
            Assert.AreEqual(1, observedIndex);
            Assert.AreSame(second, observedElement);
            _selection.SetScaleMultiplier(3f);
            _selection.ResetSelected();
            Assert.AreEqual(6, changes);
            Assert.AreEqual(1f, observedScale);
            Assert.AreEqual(2f, firstScaleAtChange);
            _selection.ResetAll();
            Assert.AreEqual(7, changes);
            Assert.AreEqual(1f, firstScaleAtChange);
            Assert.AreEqual(1f, secondScaleAtChange);
        }

        [Test]
        public void Configure_ClonesCallerArrayAndRejectsNullTargets()
        {
            ArtisticElementController first = CreateElement("Eye", Color.white,
                Vector3.one, Quaternion.identity);
            ArtisticElementController second = CreateElement("BabyPhoto", Color.white,
                Vector3.one, Quaternion.identity);
            var targets = new[] { first, second };
            ActivateAndConfigure(targets);

            targets[0] = second;
            targets[1] = null;

            Assert.AreEqual(2, _selection.ElementCount);
            Assert.AreSame(first, _selection.GetElement(0));
            Assert.AreSame(second, _selection.GetElement(1));
            Assert.AreSame(first, _selection.SelectedElement);
            Assert.IsTrue(_selection.SelectElement(1));
            Assert.AreSame(second, _selection.SelectedElement);
            Assert.IsFalse(_selection.SelectElement(2));
            Assert.AreEqual(1, _selection.SelectedIndex);
            Assert.That(() => _selection.Configure(null), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => _selection.Configure(new ArtisticElementController[] { first, null }),
                Throws.InstanceOf<ArgumentException>());
        }

        private ArtisticElementController CreateElement(string name, Color colour, Vector3 scale,
            Quaternion rotation, bool supportsThickness = false)
        {
            var element = new GameObject(name);
            element.transform.SetParent(_root.transform, false);
            element.transform.localScale = scale;
            element.transform.localRotation = rotation;
            var sprite = element.AddComponent<SpriteRenderer>();
            sprite.color = colour;
            var controller = element.AddComponent<ArtisticElementController>();
            if (supportsThickness) controller.ConfigureThickness(true);
            return controller;
        }

        private void ActivateAndConfigure(params ArtisticElementController[] targets)
        {
            _root.SetActive(true);
            _selection.Configure(targets);
        }

        private static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 0.0001f);
            Assert.AreEqual(expected.y, actual.y, 0.0001f);
            Assert.AreEqual(expected.z, actual.z, 0.0001f);
        }

        private static void AssertColour(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 0.0001f);
            Assert.AreEqual(expected.g, actual.g, 0.0001f);
            Assert.AreEqual(expected.b, actual.b, 0.0001f);
            Assert.AreEqual(expected.a, actual.a, 0.0001f);
        }
    }
}
