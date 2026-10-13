using System.Collections.Generic;
using AmpPortableDataViz.Presentation.ArtisticElements;
using NUnit.Framework;
using UnityEngine;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public sealed class ArtisticElementControllerTests
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int UnrelatedValueId = Shader.PropertyToID("_ArtisticTestValue");
        private readonly List<Object> _assets = new List<Object>();
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject(nameof(ArtisticElementControllerTests));
            _root.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            foreach (Object asset in _assets)
            {
                if (asset != null) Object.DestroyImmediate(asset);
            }
            _assets.Clear();
        }

        [Test]
        public void TransformControls_PreserveAuthoredProportionsRotationAndPosition()
        {
            GameObject element = CreateElement("RectangleBlock_obj");
            Vector3 originalScale = new Vector3(2f, 3f, 0.4f);
            Vector3 originalPosition = new Vector3(1f, -2f, 5f);
            Quaternion originalRotation = Quaternion.Euler(12f, 34f, -17f);
            element.transform.localScale = originalScale;
            element.transform.localPosition = originalPosition;
            element.transform.localRotation = originalRotation;
            var controller = element.AddComponent<ArtisticElementController>();
            _root.SetActive(true);

            controller.ConfigureThickness(true);
            controller.SetScaleMultiplier(2f);
            controller.SetThicknessMultiplier(0.5f);
            controller.SetRotationOffset(new Vector3(10f, -20f, 30f));

            AssertVector(new Vector3(4f, 6f, 0.4f), element.transform.localScale);
            Assert.Less(Quaternion.Angle(originalRotation * Quaternion.Euler(10f, -20f, 30f),
                element.transform.localRotation), 0.001f);
            AssertVector(originalPosition, element.transform.localPosition);

            controller.ResetToDefaults();

            AssertVector(originalScale, element.transform.localScale);
            Assert.Less(Quaternion.Angle(originalRotation, element.transform.localRotation), 0.001f);
            AssertVector(originalPosition, element.transform.localPosition);
            AssertVector(Vector3.zero, controller.RotationOffset);
            Assert.AreEqual(1f, controller.ScaleMultiplier);
            Assert.AreEqual(1f, controller.ThicknessMultiplier);
        }

        [TestCase(ArtisticElementController.ThicknessAxis.X, false)]
        [TestCase(ArtisticElementController.ThicknessAxis.Y, false)]
        [TestCase(ArtisticElementController.ThicknessAxis.Z, false)]
        [TestCase(ArtisticElementController.ThicknessAxis.X, true)]
        [TestCase(ArtisticElementController.ThicknessAxis.Y, true)]
        [TestCase(ArtisticElementController.ThicknessAxis.Z, true)]
        public void ScaleAndThickness_ComposeForEachLocalAxisInEitherOrder(
            ArtisticElementController.ThicknessAxis axis, bool thicknessFirst)
        {
            GameObject element = CreateElement("RectangleBlock_obj");
            Vector3 originalScale = new Vector3(0.5f, 2f, 3f);
            element.transform.localScale = originalScale;
            element.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            var controller = element.AddComponent<ArtisticElementController>();
            _root.SetActive(true);
            controller.ConfigureThickness(true, axis);

            if (thicknessFirst)
            {
                controller.SetThicknessMultiplier(0.25f);
                controller.SetScaleMultiplier(3f);
            }
            else
            {
                controller.SetScaleMultiplier(3f);
                controller.SetThicknessMultiplier(0.25f);
            }

            Vector3 expected = originalScale * 3f;
            expected[(int)axis] *= 0.25f;
            AssertVector(expected, element.transform.localScale);
            Assert.AreEqual(3f, controller.ScaleMultiplier);
            Assert.AreEqual(0.25f, controller.ThicknessMultiplier);

            controller.ConfigureThickness(false, axis);
            AssertVector(originalScale * 3f, element.transform.localScale);
            Assert.AreEqual(1f, controller.ThicknessMultiplier);
        }

        [Test]
        public void ImageElement_ThicknessRequestsLeaveUniformScaleUnchanged()
        {
            Material material = CreateMaterial(Color.white);
            MeshRenderer renderer = CreateMeshElement("BabyPhoto", material);
            renderer.transform.localScale = new Vector3(2f, 1.5f, 1f);
            var controller = renderer.gameObject.AddComponent<ArtisticElementController>();
            _root.SetActive(true);

            controller.SetScaleMultiplier(2f);
            controller.SetThicknessMultiplier(4f);

            Assert.IsFalse(controller.SupportsThickness);
            Assert.AreEqual(1f, controller.ThicknessMultiplier);
            AssertVector(new Vector3(4f, 3f, 2f), renderer.transform.localScale);
        }

        [TestCase(-3f, ArtisticElementController.MinimumMultiplier)]
        [TestCase(0f, ArtisticElementController.MinimumMultiplier)]
        [TestCase(20f, ArtisticElementController.MaximumMultiplier)]
        public void Multipliers_ClampToPositiveUsableBounds(float requested, float expected)
        {
            GameObject element = CreateElement("RectangleBlock_obj");
            var controller = element.AddComponent<ArtisticElementController>();
            _root.SetActive(true);
            controller.ConfigureThickness(true);

            controller.SetScaleMultiplier(requested);
            controller.SetThicknessMultiplier(requested);

            Assert.AreEqual(expected, controller.ScaleMultiplier);
            Assert.AreEqual(expected, controller.ThicknessMultiplier);
            AssertVector(new Vector3(expected, expected, expected * expected),
                element.transform.localScale);
        }

        [Test]
        public void NonFiniteInputs_KeepLastValidTransformAndColour()
        {
            var sprite = CreateElement("Asset Graphic Element").AddComponent<SpriteRenderer>();
            sprite.color = new Color(0.2f, 0.3f, 0.4f, 0.6f);
            var controller = sprite.gameObject.AddComponent<ArtisticElementController>();
            _root.SetActive(true);
            controller.ConfigureThickness(true);
            controller.SetScaleMultiplier(2f);
            controller.SetThicknessMultiplier(0.5f);
            controller.SetRotationOffset(new Vector3(10f, 20f, 30f));
            controller.SetColour(new Color(0.8f, 0.7f, 0.6f));
            Vector3 scale = sprite.transform.localScale;
            Quaternion rotation = sprite.transform.localRotation;
            Color colour = sprite.color;

            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                controller.SetScaleMultiplier(invalid);
                controller.SetThicknessMultiplier(invalid);
                controller.SetRotationOffset(new Vector3(invalid, 0f, 0f));
                controller.SetRotationOffset(new Vector3(0f, invalid, 0f));
                controller.SetRotationOffset(new Vector3(0f, 0f, invalid));
                controller.SetColour(new Color(invalid, 0f, 0f));
                controller.SetColour(new Color(0f, invalid, 0f));
                controller.SetColour(new Color(0f, 0f, invalid));
            }

            AssertVector(scale, sprite.transform.localScale);
            Assert.Less(Quaternion.Angle(rotation, sprite.transform.localRotation), 0.001f);
            AssertColour(colour, sprite.color);
            Assert.AreEqual(2f, controller.ScaleMultiplier);
            Assert.AreEqual(0.5f, controller.ThicknessMultiplier);
            AssertVector(new Vector3(10f, 20f, 30f), controller.RotationOffset);
        }

        [Test]
        public void SpriteColour_PreservesOpacityClampsRgbAndRestoresDefaults()
        {
            var sprite = CreateElement("Asset Graphic Element").AddComponent<SpriteRenderer>();
            Color original = new Color(0.2f, 0.4f, 0.6f, 0.35f);
            sprite.color = original;
            var controller = sprite.gameObject.AddComponent<ArtisticElementController>();
            _root.SetActive(true);

            controller.SetColour(new Color(1.5f, -0.5f, 0.8f, 0f));

            Assert.IsTrue(controller.SupportsColour);
            AssertColour(new Color(1f, 0f, 0.8f, 0.35f), sprite.color);
            AssertColour(sprite.color, controller.Colour);

            controller.ResetToDefaults();
            AssertColour(original, sprite.color);
            AssertColour(original, controller.Colour);
        }

        [Test]
        public void MeshColour_IsIndependentForElementsSharingMaterial()
        {
            Color original = new Color(0.2f, 0.4f, 0.6f, 0.45f);
            Material material = CreateMaterial(original);
            MeshRenderer selected = CreateMeshElement("BabyPhoto", material);
            MeshRenderer other = CreateMeshElement("Eye", material);
            var selectedController = selected.gameObject.AddComponent<ArtisticElementController>();
            var otherController = other.gameObject.AddComponent<ArtisticElementController>();
            _root.SetActive(true);

            selectedController.SetColour(new Color(0.8f, 0.1f, 0.3f, 1f));

            Assert.AreSame(material, selected.sharedMaterial);
            Assert.AreSame(material, other.sharedMaterial);
            AssertColour(original, material.GetColor(BaseColorId));
            var block = new MaterialPropertyBlock();
            selected.GetPropertyBlock(block, 0);
            AssertColour(new Color(0.8f, 0.1f, 0.3f, 0.45f), block.GetColor(BaseColorId));
            other.GetPropertyBlock(block, 0);
            Assert.IsTrue(block.isEmpty);
            AssertColour(original, otherController.Colour);

            selectedController.ResetToDefaults();
            selected.GetPropertyBlock(block, 0);
            Assert.IsTrue(block.isEmpty);
            AssertColour(original, selectedController.Colour);
        }

        [Test]
        public void MeshColour_PreservesAndRestoresExistingPerSlotOverrides()
        {
            Material material = CreateMaterial(Color.white);
            MeshRenderer renderer = CreateMeshElement("RectangleBlock_obj", material);
            renderer.sharedMaterials = new[] { material, material };
            Color firstOriginal = new Color(0.1f, 0.2f, 0.3f, 0.25f);
            Color secondOriginal = new Color(0.3f, 0.2f, 0.1f, 0.75f);
            var first = new MaterialPropertyBlock();
            first.SetColor(BaseColorId, firstOriginal);
            first.SetFloat(UnrelatedValueId, 13f);
            renderer.SetPropertyBlock(first, 0);
            var second = new MaterialPropertyBlock();
            second.SetColor(BaseColorId, secondOriginal);
            second.SetFloat(UnrelatedValueId, 29f);
            renderer.SetPropertyBlock(second, 1);
            var controller = renderer.gameObject.AddComponent<ArtisticElementController>();
            _root.SetActive(true);

            controller.SetColour(new Color(0.9f, 0.8f, 0.7f));

            renderer.GetPropertyBlock(first, 0);
            renderer.GetPropertyBlock(second, 1);
            AssertColour(new Color(0.9f, 0.8f, 0.7f, 0.25f), first.GetColor(BaseColorId));
            AssertColour(new Color(0.9f, 0.8f, 0.7f, 0.75f), second.GetColor(BaseColorId));
            Assert.AreEqual(13f, first.GetFloat(UnrelatedValueId));
            Assert.AreEqual(29f, second.GetFloat(UnrelatedValueId));

            controller.ResetToDefaults();
            renderer.GetPropertyBlock(first, 0);
            renderer.GetPropertyBlock(second, 1);
            AssertColour(firstOriginal, first.GetColor(BaseColorId));
            AssertColour(secondOriginal, second.GetColor(BaseColorId));
            Assert.AreEqual(13f, first.GetFloat(UnrelatedValueId));
            Assert.AreEqual(29f, second.GetFloat(UnrelatedValueId));
        }

        [Test]
        public void MeshColour_PreservesRendererWideTextureAcrossTintResetAndRetint()
        {
            Material material = CreateMaterial(Color.white);
            MeshRenderer renderer = CreateMeshElement("BlurAsset_orig", material);
            var texture = new Texture2D(2, 2);
            _assets.Add(texture);
            Color original = new Color(0.4f, 0.5f, 0.6f, 0.55f);
            var global = new MaterialPropertyBlock();
            global.SetColor(BaseColorId, original);
            global.SetTexture(BaseMapId, texture);
            global.SetFloat(UnrelatedValueId, 37f);
            renderer.SetPropertyBlock(global);
            var controller = renderer.gameObject.AddComponent<ArtisticElementController>();
            _root.SetActive(true);
            var slot = new MaterialPropertyBlock();

            controller.SetColour(Color.red);
            renderer.GetPropertyBlock(slot, 0);
            AssertColour(new Color(1f, 0f, 0f, 0.55f), slot.GetColor(BaseColorId));
            Assert.AreSame(texture, slot.GetTexture(BaseMapId));
            Assert.AreEqual(37f, slot.GetFloat(UnrelatedValueId));

            controller.ResetToDefaults();
            renderer.GetPropertyBlock(slot, 0);
            Assert.IsTrue(slot.isEmpty);
            renderer.GetPropertyBlock(global);
            AssertColour(original, global.GetColor(BaseColorId));
            Assert.AreSame(texture, global.GetTexture(BaseMapId));
            Assert.AreEqual(37f, global.GetFloat(UnrelatedValueId));

            controller.SetColour(Color.green);
            renderer.GetPropertyBlock(slot, 0);
            AssertColour(new Color(0f, 1f, 0f, 0.55f), slot.GetColor(BaseColorId));
            Assert.AreSame(texture, slot.GetTexture(BaseMapId));
            Assert.AreEqual(37f, slot.GetFloat(UnrelatedValueId));
        }

        private GameObject CreateElement(string name)
        {
            var element = new GameObject(name);
            element.transform.SetParent(_root.transform, false);
            return element;
        }

        private MeshRenderer CreateMeshElement(string name, Material material)
        {
            var renderer = CreateElement(name).AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private Material CreateMaterial(Color colour)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            Assert.IsNotNull(shader, "The artistic elements require the project's URP Unlit shader.");
            var material = new Material(shader);
            material.SetColor(BaseColorId, colour);
            _assets.Add(material);
            return material;
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
