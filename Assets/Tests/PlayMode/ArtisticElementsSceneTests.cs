#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AmpPortableDataViz.Presentation.ArtisticElements;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public sealed class ArtisticElementsSceneTests
    {
        private const string ScenePath = "Assets/Scenes/ArtisticElementsTest.unity";
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Vector3 LengthDirection = new Vector3(-0.3935771f, 0.9192916f, 0f).normalized;
        private Scene _scene;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest]
        public IEnumerator SavedScene_ControlsAllFiveImportedElementsAndRestoresAuthoredAppearance()
        {
            yield return LoadTestScene();
            ArtisticElementsTestSetup setup = GetSetup();
            ArtisticElementsSelectionController selection = setup.Selection;
            ArtisticElementsControlPanel panel = setup.Panel;
            Assert.IsNotNull(selection);
            Assert.IsNotNull(panel);
            Assert.AreSame(selection, panel.Selection);
            Assert.AreEqual(5, selection.ElementCount);
            var names = new List<string>();
            var originals = new ElementSnapshot[selection.ElementCount];
            var materials = new List<MaterialSnapshot>();
            for (int i = 0; i < selection.ElementCount; i++)
            {
                ArtisticElementController element = selection.GetElement(i);
                names.Add(element.name);
                Assert.IsTrue(element.gameObject.activeInHierarchy, element.name);
                Assert.IsTrue(element.SupportsColour, element.name);
                Assert.AreEqual(element.name == "RectangleBlock_obj", element.SupportsThickness, element.name);
                Assert.AreEqual(element.name == "RectangleBlock_obj", element.SupportsLength, element.name);
                originals[i] = new ElementSnapshot(element);
                foreach (Renderer renderer in element.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material == null) continue;
                        int propertyId = ColourProperty(material);
                        if (propertyId >= 0)
                        {
                            materials.Add(new MaterialSnapshot(material, propertyId));
                        }
                    }
                }
                if (element.name == "Asset Graphic Element")
                {
                    Assert.IsNotNull(element.GetComponentInChildren<SpriteRenderer>(true),
                        "The retained SVG should use its imported vector sprite.");
                }
            }
            CollectionAssert.AreEquivalent(new[]
            {
                "RectangleBlock_obj", "BabyPhoto", "BlurAsset_orig", "Eye", "Asset Graphic Element"
            }, names);
            Assert.IsNotEmpty(materials, "The model and PNG mesh renderers must have colour-capable materials.");

            Canvas.ForceUpdateCanvases();
            yield return CapturePreview(panel);

            Color requested = new Color(0.15f, 0.65f, 0.95f);
            for (int i = 0; i < selection.ElementCount; i++)
            {
                Find<Button>(panel, "Element_" + i).onClick.Invoke();
                ArtisticElementController element = selection.SelectedElement;
                Assert.AreSame(originals[i].Element, element);
                Find<Slider>(panel, "Colour_R").value = requested.r;
                Find<Slider>(panel, "Colour_G").value = requested.g;
                Find<Slider>(panel, "Colour_B").value = requested.b;
                Find<Slider>(panel, "Rotation_Z").value = 35f;
                Find<Slider>(panel, "Scale").value = 1.5f;
                Vector3 expectedScale = originals[i].Scale * 1.5f;
                if (element.SupportsThickness)
                {
                    Find<Slider>(panel, "Thickness").value = 0.5f;
                    expectedScale.z *= 0.5f;
                }

                AssertVector(expectedScale, element.transform.localScale);
                AssertVector(originals[i].Position, element.transform.localPosition);
                Assert.Less(Quaternion.Angle(originals[i].Rotation * Quaternion.Euler(0f, 0f, 35f),
                    element.transform.localRotation), 0.001f);
                AssertRenderedRgb(element, requested);
                foreach (ElementSnapshot original in originals)
                {
                    Assert.IsTrue(original.Element.gameObject.activeInHierarchy, original.Element.name);
                    AssertVector(original.Position, original.Element.transform.localPosition);
                }
            }

            Find<Button>(panel, "ResetAll").onClick.Invoke();

            foreach (ElementSnapshot original in originals)
            {
                AssertVector(original.Scale, original.Element.transform.localScale);
                AssertVector(original.Position, original.Element.transform.localPosition);
                Assert.Less(Quaternion.Angle(original.Rotation, original.Element.transform.localRotation), 0.001f);
                AssertColour(original.Colour, original.Element.Colour);
                foreach (SpriteSnapshot sprite in original.Sprites)
                {
                    AssertColour(sprite.Colour, sprite.Renderer.color);
                }
                foreach (SlotSnapshot slot in original.Slots)
                {
                    var restored = new MaterialPropertyBlock();
                    slot.Renderer.GetPropertyBlock(restored, slot.MaterialIndex);
                    Assert.AreEqual(slot.WasEmpty, restored.isEmpty);
                    if (slot.HadColour) AssertColour(slot.Colour, restored.GetColor(slot.PropertyId));
                }
            }
            foreach (MaterialSnapshot material in materials)
            {
                AssertColour(material.Colour, material.Material.GetColor(material.PropertyId));
            }
            Assert.AreEqual(1f, Find<Slider>(panel, "Scale").value);
            Assert.AreEqual(0f, Find<Slider>(panel, "Rotation_Z").value);
        }

        [UnityTest]
        public IEnumerator SavedScene_LengthSliderStretchesImportedModelWithoutChangingItsMesh()
        {
            yield return LoadTestScene();
            ArtisticElementsTestSetup setup = GetSetup();
            ArtisticElementsSelectionController selection = setup.Selection;
            ArtisticElementsControlPanel panel = setup.Panel;
            int blockIndex = -1;
            int svgIndex = -1;
            for (int i = 0; i < selection.ElementCount; i++)
            {
                if (selection.GetElement(i).name == "RectangleBlock_obj") blockIndex = i;
                if (selection.GetElement(i).name == "Asset Graphic Element") svgIndex = i;
            }
            Assert.GreaterOrEqual(blockIndex, 0);
            Assert.GreaterOrEqual(svgIndex, 0);
            Find<Button>(panel, "Element_" + blockIndex).onClick.Invoke();
            ArtisticElementController block = selection.SelectedElement;
            MeshFilter filter = block.GetComponentInChildren<MeshFilter>(true);
            Assert.IsNotNull(filter);
            Mesh originalMesh = filter.sharedMesh;
            Assert.IsNotNull(originalMesh);
            Vector3 originalPosition = block.transform.localPosition;
            Quaternion originalRotation = block.transform.localRotation;
            Vector3 originalScale = block.transform.localScale;
            Vector3 originalMeshPoint = filter.transform.TransformPoint(originalMesh.bounds.min);
            Matrix4x4 rootToMesh = (block.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix).inverse;
            Slider length = Find<Slider>(panel, "Length");
            Assert.IsTrue(Find<Transform>(panel, "LengthRow").gameObject.activeSelf);

            length.value = 2f;
            AssertModelDirections(block, filter, rootToMesh, 2f);
            length.value = 0.25f;
            AssertModelDirections(block, filter, rootToMesh, 0.25f);
            AssertVector(originalScale, block.transform.localScale);
            AssertVector(originalPosition, block.transform.localPosition);
            Assert.Less(Quaternion.Angle(originalRotation, block.transform.localRotation), 0.001f);
            Assert.AreSame(originalMesh, filter.sharedMesh);

            Find<Slider>(panel, "Scale").value = 1.5f;
            Find<Slider>(panel, "Thickness").value = 0.5f;
            length.value = 3f;
            AssertModelDirections(block, filter, rootToMesh, 3f);
            Find<Button>(panel, "Element_" + svgIndex).onClick.Invoke();
            Assert.IsFalse(Find<Transform>(panel, "LengthRow").gameObject.activeSelf);
            Assert.IsFalse(length.interactable);
            Assert.AreEqual(3f, block.LengthMultiplier);
            Find<Button>(panel, "Element_" + blockIndex).onClick.Invoke();
            Assert.AreEqual(3f, length.value);
            Find<Button>(panel, "ResetSelected").onClick.Invoke();

            Assert.AreEqual(1f, length.value);
            AssertModelDirections(block, filter, rootToMesh, 1f);
            AssertVector(originalPosition, block.transform.localPosition);
            AssertVector(originalScale, block.transform.localScale);
            Assert.Less(Quaternion.Angle(originalRotation, block.transform.localRotation), 0.001f);
            AssertVector(originalMeshPoint, filter.transform.TransformPoint(originalMesh.bounds.min));
            Assert.AreSame(originalMesh, filter.sharedMesh);
        }

        [UnityTest]
        public IEnumerator Initialize_IsIdempotentAndDoesNotDuplicateControlsOrEventSystem()
        {
            yield return LoadTestScene();
            ArtisticElementsTestSetup setup = GetSetup();
            ArtisticElementsSelectionController selection = setup.Selection;
            ArtisticElementsControlPanel panel = setup.Panel;
            int originalEventSystems = Object.FindObjectsOfType<EventSystem>().Length;
            int originalCanvases = panel.GetComponentsInChildren<Canvas>(true).Length;
            Assert.AreEqual(1, originalCanvases);
            selection.SelectElement(2);
            selection.SetScaleMultiplier(2f);

            setup.Initialize();
            setup.Initialize();
            yield return null;

            Assert.AreSame(selection, setup.Selection);
            Assert.AreSame(panel, setup.Panel);
            Assert.AreEqual(2, selection.SelectedIndex);
            Assert.AreEqual(2f, selection.SelectedElement.ScaleMultiplier);
            Assert.AreEqual(5, ComponentsInScene<ArtisticElementController>().Count);
            Assert.AreEqual(1, ComponentsInScene<ArtisticElementsSelectionController>().Count);
            Assert.AreEqual(1, ComponentsInScene<ArtisticElementsControlPanel>().Count);
            Assert.AreEqual(originalCanvases, panel.GetComponentsInChildren<Canvas>(true).Length);
            Assert.AreEqual(originalEventSystems, Object.FindObjectsOfType<EventSystem>().Length);
        }

        private IEnumerator LoadTestScene()
        {
            AsyncOperation loading = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            Assert.IsNotNull(loading);
            yield return loading;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            Assert.IsTrue(_scene.IsValid() && _scene.isLoaded);
            yield return null;
        }

        private IEnumerator CapturePreview(ArtisticElementsControlPanel panel)
        {
            // ScreenCapture does not save GameView images in batch mode. Render the scene to a texture instead.
            Camera camera = ComponentsInScene<Camera>()[0];
            Canvas canvas = panel.GetComponentInChildren<Canvas>();
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            RenderTexture originalTarget = camera.targetTexture;
            RenderTexture originalActive = RenderTexture.active;
            float originalAspect = camera.aspect;
            RenderMode originalMode = canvas.renderMode;
            Camera originalCanvasCamera = canvas.worldCamera;
            float originalPlaneDistance = canvas.planeDistance;
            float originalScale = canvas.scaleFactor;
            bool originalScalerEnabled = scaler.enabled;
            var target = new RenderTexture(1600, 900, 24);
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                target.Create();
                camera.targetTexture = target;
                camera.aspect = 1600f / 900f;
                scaler.enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                canvas.scaleFactor = 1f;
                Canvas.ForceUpdateCanvases();
                yield return null;
                yield return null;
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, 1600f, 900f), 0, 0);
                image.Apply();
                string folder = Path.Combine(Directory.GetParent(UnityEngine.Application.dataPath).FullName, "TestResults");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "artistic-panel-preview.png"), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = originalActive;
                camera.targetTexture = originalTarget;
                camera.aspect = originalAspect;
                canvas.renderMode = originalMode;
                canvas.worldCamera = originalCanvasCamera;
                canvas.planeDistance = originalPlaneDistance;
                scaler.enabled = originalScalerEnabled;
                canvas.scaleFactor = originalScale;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
            }
        }

        private ArtisticElementsTestSetup GetSetup()
        {
            List<ArtisticElementsTestSetup> setups = ComponentsInScene<ArtisticElementsTestSetup>();
            Assert.AreEqual(1, setups.Count, "The saved scene must contain one explicitly wired setup.");
            return setups[0];
        }

        private List<T> ComponentsInScene<T>() where T : Component
        {
            var components = new List<T>();
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                components.AddRange(root.GetComponentsInChildren<T>(true));
            }
            return components;
        }

        private static T Find<T>(ArtisticElementsControlPanel panel, string name) where T : Component
        {
            foreach (T component in panel.GetComponentsInChildren<T>(true))
            {
                if (component.gameObject.name == name) return component;
            }
            Assert.Fail($"Missing {typeof(T).Name} named {name}.");
            return null;
        }

        private static int ColourProperty(Material material)
        {
            if (material.HasProperty(BaseColorId)) return BaseColorId;
            return material.HasProperty(ColorId) ? ColorId : -1;
        }

        private static void AssertModelDirections(ArtisticElementController block, MeshFilter filter,
            Matrix4x4 rootToMesh, float length)
        {
            Vector3 origin = filter.transform.TransformPoint(rootToMesh.MultiplyPoint3x4(Vector3.zero));
            Vector3 width = new Vector3(LengthDirection.y, -LengthDirection.x, 0f);
            AssertVector(block.transform.TransformVector(LengthDirection * length),
                filter.transform.TransformPoint(rootToMesh.MultiplyPoint3x4(LengthDirection)) - origin);
            AssertVector(block.transform.TransformVector(width),
                filter.transform.TransformPoint(rootToMesh.MultiplyPoint3x4(width)) - origin);
            AssertVector(block.transform.TransformVector(Vector3.forward),
                filter.transform.TransformPoint(rootToMesh.MultiplyPoint3x4(Vector3.forward)) - origin);
        }

        private static void AssertRenderedRgb(ArtisticElementController element, Color expected)
        {
            int checkedRenderers = 0;
            foreach (Renderer renderer in element.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is SpriteRenderer sprite)
                {
                    AssertRgb(expected, sprite.color);
                    checkedRenderers++;
                    continue;
                }
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    int propertyId = ColourProperty(materials[i]);
                    if (propertyId < 0) continue;
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block, i);
                    Assert.IsTrue(block.HasColor(propertyId));
                    AssertRgb(expected, block.GetColor(propertyId));
                    checkedRenderers++;
                }
            }
            Assert.Greater(checkedRenderers, 0, element.name);
        }

        private static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 0.0001f);
            Assert.AreEqual(expected.y, actual.y, 0.0001f);
            Assert.AreEqual(expected.z, actual.z, 0.0001f);
        }

        private static void AssertRgb(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 0.0001f);
            Assert.AreEqual(expected.g, actual.g, 0.0001f);
            Assert.AreEqual(expected.b, actual.b, 0.0001f);
        }

        private static void AssertColour(Color expected, Color actual)
        {
            AssertRgb(expected, actual);
            Assert.AreEqual(expected.a, actual.a, 0.0001f);
        }

        private sealed class ElementSnapshot
        {
            public readonly ArtisticElementController Element;
            public readonly Vector3 Scale, Position;
            public readonly Quaternion Rotation;
            public readonly Color Colour;
            public readonly List<SpriteSnapshot> Sprites = new List<SpriteSnapshot>();
            public readonly List<SlotSnapshot> Slots = new List<SlotSnapshot>();

            public ElementSnapshot(ArtisticElementController element)
            {
                Element = element;
                Scale = element.transform.localScale;
                Position = element.transform.localPosition;
                Rotation = element.transform.localRotation;
                Colour = element.Colour;
                foreach (Renderer renderer in element.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer is SpriteRenderer sprite)
                    {
                        Sprites.Add(new SpriteSnapshot(sprite));
                        continue;
                    }
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] == null) continue;
                        int propertyId = ColourProperty(materials[i]);
                        if (propertyId >= 0) Slots.Add(new SlotSnapshot(renderer, i, propertyId));
                    }
                }
            }
        }

        private sealed class SpriteSnapshot
        {
            public readonly SpriteRenderer Renderer;
            public readonly Color Colour;
            public SpriteSnapshot(SpriteRenderer renderer) { Renderer = renderer; Colour = renderer.color; }
        }

        private sealed class MaterialSnapshot
        {
            public readonly Material Material;
            public readonly int PropertyId;
            public readonly Color Colour;
            public MaterialSnapshot(Material material, int propertyId)
            {
                Material = material;
                PropertyId = propertyId;
                Colour = material.GetColor(propertyId);
            }
        }

        private sealed class SlotSnapshot
        {
            public readonly Renderer Renderer;
            public readonly int MaterialIndex, PropertyId;
            public readonly bool WasEmpty, HadColour;
            public readonly Color Colour;
            public SlotSnapshot(Renderer renderer, int materialIndex, int propertyId)
            {
                Renderer = renderer;
                MaterialIndex = materialIndex;
                PropertyId = propertyId;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block, materialIndex);
                WasEmpty = block.isEmpty;
                HadColour = block.HasColor(propertyId);
                Colour = block.GetColor(propertyId);
            }
        }
    }
}
#endif
