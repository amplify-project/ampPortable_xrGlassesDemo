using System.Collections;
using System.Collections.Generic;
using AmpPortableDataViz.Presentation.ArtisticElements;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public sealed class ArtisticElementsControlPanelTests
    {
        private static readonly Color ImageColour = new Color(0.2f, 0.4f, 0.6f, 0.45f);
        private static readonly Color BlockColour = new Color(0.7f, 0.3f, 0.1f, 0.8f);
        private static readonly Vector3 ImageScale = new Vector3(2f, 3f, 1f);
        private static readonly Vector3 BlockScale = new Vector3(1f, 2f, 3f);
        private static readonly Vector3 LengthDirection = new Vector3(-0.3935771f, 0.9192916f, 0f).normalized;
        private GameObject _root;
        private Transform _blockGeometry;
        private ArtisticElementController _image;
        private ArtisticElementController _block;
        private ArtisticElementsSelectionController _selection;
        private ArtisticElementsControlPanel _panel;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject(nameof(ArtisticElementsControlPanelTests));
            _root.SetActive(false);
            _image = CreateElement("BabyPhoto", ImageColour, ImageScale);
            _block = CreateElement("RectangleBlock_obj", BlockColour, BlockScale);
            _block.ConfigureThickness(true);
            _block.ConfigureLength(true, LengthDirection);
            _selection = _root.AddComponent<ArtisticElementsSelectionController>();
            _root.SetActive(true);
            _selection.Configure(new[] { _image, _block });
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void Sliders_UpdateOnlySelectedColourRotationAndUniformScale()
        {
            CreatePanel();

            Find<Slider>("Colour_R").value = 0.9f;
            Find<Slider>("Colour_G").value = 0.8f;
            Find<Slider>("Colour_B").value = 0.7f;
            Find<Slider>("Rotation_X").value = 12f;
            Find<Slider>("Rotation_Y").value = -34f;
            Find<Slider>("Rotation_Z").value = 56f;
            Find<Slider>("Scale").value = 2f;

            Assert.AreSame(_selection, _panel.Selection);
            AssertColour(new Color(0.9f, 0.8f, 0.7f, ImageColour.a), _image.Colour);
            AssertVector(new Vector3(12f, -34f, 56f), _image.RotationOffset);
            AssertVector(ImageScale * 2f, _image.transform.localScale);
            AssertColour(_image.Colour, Find<Image>("ColourPreview").color);
            AssertColour(BlockColour, _block.Colour);
            AssertVector(BlockScale, _block.transform.localScale);
            AssertVector(Vector3.zero, _block.RotationOffset);
        }

        [Test]
        public void SelectionButtons_RefreshControlsWithoutOverwritingElementSettings()
        {
            CreatePanel();
            Find<Slider>("Colour_R").value = 0.85f;
            Find<Slider>("Scale").value = 2.5f;
            Find<Slider>("Rotation_Y").value = 45f;

            Find<Button>("Element_1").onClick.Invoke();

            Assert.AreSame(_block, _selection.SelectedElement);
            AssertColour(BlockColour, _block.Colour);
            AssertVector(BlockScale, _block.transform.localScale);
            AssertVector(Vector3.zero, _block.RotationOffset);
            Assert.AreEqual(BlockColour.r, Find<Slider>("Colour_R").value, 0.0001f);
            Assert.AreEqual(BlockColour.g, Find<Slider>("Colour_G").value, 0.0001f);
            Assert.AreEqual(BlockColour.b, Find<Slider>("Colour_B").value, 0.0001f);
            Assert.AreEqual(1f, Find<Slider>("Scale").value, 0.0001f);
            Assert.AreEqual(0f, Find<Slider>("Rotation_Y").value, 0.0001f);
            StringAssert.Contains("RectangleBlock_obj", Find<TMP_Text>("SelectedElement").text);
            AssertColour(BlockColour, Find<Image>("ColourPreview").color);

            Find<Button>("Element_0").onClick.Invoke();

            Assert.AreSame(_image, _selection.SelectedElement);
            Assert.AreEqual(0.85f, Find<Slider>("Colour_R").value, 0.0001f);
            Assert.AreEqual(2.5f, Find<Slider>("Scale").value, 0.0001f);
            Assert.AreEqual(45f, Find<Slider>("Rotation_Y").value, 0.0001f);
            AssertColour(new Color(0.85f, ImageColour.g, ImageColour.b, ImageColour.a), _image.Colour);
            Assert.IsTrue(_image.gameObject.activeInHierarchy);
            Assert.IsTrue(_block.gameObject.activeInHierarchy);
        }

        [Test]
        public void ThicknessRow_OnlyShowsForBlockAndCombinesWithUniformScale()
        {
            CreatePanel();
            Assert.IsFalse(FindTransform("ThicknessRow").gameObject.activeSelf);

            Find<Button>("Element_1").onClick.Invoke();
            Assert.IsTrue(FindTransform("ThicknessRow").gameObject.activeSelf);
            Find<Slider>("Thickness").value = 0.25f;
            Find<Slider>("Scale").value = 3f;

            AssertVector(new Vector3(3f, 6f, 2.25f), _block.transform.localScale);
            Assert.AreEqual(0.25f, _block.ThicknessMultiplier);
            Assert.AreEqual(3f, _block.ScaleMultiplier);
            Find<Button>("Element_0").onClick.Invoke();
            Assert.IsFalse(FindTransform("ThicknessRow").gameObject.activeSelf);
            Find<Slider>("Scale").value = 2f;
            AssertVector(ImageScale * 2f, _image.transform.localScale);
            AssertVector(new Vector3(3f, 6f, 2.25f), _block.transform.localScale);
        }

        [Test]
        public void LengthRow_ControlsOnlyBlockLengthAndRetainsItsValueAcrossSelection()
        {
            CreatePanel();
            Transform lengthRow = FindTransform("LengthRow");
            Slider length = Find<Slider>("Length");
            Assert.IsFalse(lengthRow.gameObject.activeSelf);
            Assert.IsFalse(length.interactable);

            Find<Button>("Element_1").onClick.Invoke();
            Assert.IsTrue(lengthRow.gameObject.activeSelf);
            Assert.IsTrue(length.interactable);
            length.value = 2f;
            AssertBlockGeometry(2f);
            AssertVector(BlockScale, _block.transform.localScale);
            Find<Slider>("Scale").value = 1.5f;
            Find<Slider>("Thickness").value = 0.25f;
            AssertVector(new Vector3(1.5f, 3f, 1.125f), _block.transform.localScale);
            AssertBlockGeometry(2f);

            Find<Button>("Element_0").onClick.Invoke();
            Assert.IsFalse(lengthRow.gameObject.activeSelf);
            Assert.IsFalse(length.interactable);
            Find<Slider>("Scale").value = 2f;
            AssertVector(ImageScale * 2f, _image.transform.localScale);
            Assert.AreEqual(1f, _image.LengthMultiplier);
            Find<Button>("Element_1").onClick.Invoke();
            Assert.IsTrue(lengthRow.gameObject.activeSelf);
            Assert.AreEqual(2f, length.value);
            StringAssert.Contains("2.00", lengthRow.Find("Value").GetComponent<TMP_Text>().text);
            AssertBlockGeometry(2f);
        }

        [Test]
        public void ResetButtons_RestoreAuthoredValuesAndRefreshSelectedControls()
        {
            CreatePanel();
            Find<Slider>("Scale").value = 2f;
            Find<Slider>("Colour_R").value = 0.9f;
            Find<Button>("Element_1").onClick.Invoke();
            Find<Slider>("Scale").value = 3f;
            Find<Slider>("Thickness").value = 0.5f;
            Find<Slider>("Length").value = 2.5f;
            Find<Slider>("Rotation_X").value = 30f;
            Find<Slider>("Colour_G").value = 0.8f;

            Find<Button>("ResetSelected").onClick.Invoke();

            AssertVector(BlockScale, _block.transform.localScale);
            AssertVector(Vector3.zero, _block.RotationOffset);
            AssertColour(BlockColour, _block.Colour);
            Assert.AreEqual(1f, Find<Slider>("Scale").value);
            Assert.AreEqual(1f, Find<Slider>("Thickness").value);
            Assert.AreEqual(1f, Find<Slider>("Length").value);
            AssertBlockGeometry(1f);
            Assert.AreEqual(0f, Find<Slider>("Rotation_X").value);
            Assert.AreEqual(BlockColour.g, Find<Slider>("Colour_G").value, 0.0001f);
            AssertVector(ImageScale * 2f, _image.transform.localScale);

            Find<Slider>("Length").value = 0.25f;
            Find<Button>("ResetAll").onClick.Invoke();

            AssertVector(ImageScale, _image.transform.localScale);
            AssertColour(ImageColour, _image.Colour);
            AssertVector(BlockScale, _block.transform.localScale);
            AssertColour(BlockColour, _block.Colour);
            Assert.AreSame(_block, _selection.SelectedElement);
            Assert.AreEqual(1f, Find<Slider>("Length").value);
            AssertBlockGeometry(1f);
        }

        [Test]
        public void ToggleControls_HidesOnlyPanelBodyAndCanReopenIt()
        {
            CreatePanel();
            Button toggle = Find<Button>("ToggleControls");
            GameObject body = FindTransform("ControlsBody").gameObject;
            Assert.IsTrue(body.activeSelf);

            toggle.onClick.Invoke();

            Assert.IsFalse(body.activeSelf);
            Assert.IsTrue(toggle.gameObject.activeInHierarchy);
            Assert.IsTrue(_image.gameObject.activeInHierarchy);
            Assert.IsTrue(_block.gameObject.activeInHierarchy);
            toggle.onClick.Invoke();
            Assert.IsTrue(body.activeInHierarchy);
            Assert.AreSame(_image, _selection.SelectedElement);
        }

        [Test]
        public void Panel_ReusesProvidedEventSystemWithoutCreatingDuplicate()
        {
            EventSystem existing = EventSystem.current;
            if (existing == null)
            {
                var eventObject = new GameObject("ProvidedEventSystem");
                eventObject.transform.SetParent(_root.transform, false);
                existing = eventObject.AddComponent<EventSystem>();
                eventObject.AddComponent<InputSystemUIInputModule>();
            }
            int originalCount = Object.FindObjectsOfType<EventSystem>().Length;

            CreatePanel();
            _panel.Configure(_selection);

            Assert.AreEqual(originalCount, Object.FindObjectsOfType<EventSystem>().Length);
            Assert.AreSame(existing, EventSystem.current);
            Assert.AreEqual(1, _panel.GetComponentsInChildren<Canvas>(true).Length);
        }

        [UnityTest]
        public IEnumerator PanelLifecycle_HidesDisabledUiRefreshesOnEnableAndCleansUpOnRemoval()
        {
            CreatePanel();
            Canvas canvas = _panel.GetComponentInChildren<Canvas>();
            EventSystem ownedEventSystem = canvas.GetComponentInChildren<EventSystem>();
            _panel.enabled = false;
            Assert.IsFalse(canvas.gameObject.activeSelf);
            _selection.SetScaleMultiplier(2f);

            _panel.enabled = true;
            Assert.IsTrue(canvas.gameObject.activeInHierarchy);
            Assert.AreEqual(2f, Find<Slider>("Scale").value);
            Assert.AreEqual(1, _panel.GetComponentsInChildren<Canvas>(true).Length);

            Object.Destroy(_panel);
            yield return null;
            yield return null;
            Assert.IsTrue(canvas == null, "Removing the panel should remove its generated UI.");
            Assert.IsTrue(ownedEventSystem == null, "The panel should clean up its owned EventSystem.");
            Assert.IsTrue(_image.gameObject.activeInHierarchy);
            Assert.IsTrue(_block.gameObject.activeInHierarchy);
        }

        [UnityTest]
        public IEnumerator SliderTrackClickAndDrag_UseCanvasRaycastAndAffectOnlySelectedElement()
        {
            CreatePanel();
            yield return null;
            Canvas.ForceUpdateCanvases();
            Slider slider = Find<Slider>("Scale");
            var track = slider.fillRect.parent as RectTransform;
            Assert.IsNotNull(track);
            EventSystem eventSystem = EventSystem.current;
            Assert.IsNotNull(eventSystem);
            var pointer = new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                position = TrackScreenPoint(track, 0.85f)
            };
            var hits = new List<RaycastResult>();
            eventSystem.RaycastAll(pointer, hits);

            Assert.IsNotEmpty(hits, "The unfilled slider track must accept pointer input.");
            Assert.AreSame(slider, hits[0].gameObject.GetComponentInParent<Slider>());
            pointer.pointerCurrentRaycast = hits[0];
            pointer.pointerPressRaycast = hits[0];
            pointer.pressPosition = pointer.position;
            pointer.pointerPress = ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer,
                ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.initializePotentialDrag);

            Assert.Greater(slider.value, 3.5f, "Clicking the unfilled track must move the slider handle.");
            Assert.AreEqual(slider.value, _image.ScaleMultiplier, 0.0001f);
            AssertVector(BlockScale, _block.transform.localScale);
            float clickedValue = slider.value;
            Vector2 previousPosition = pointer.position;
            pointer.position = TrackScreenPoint(track, 0.35f);
            pointer.delta = pointer.position - previousPosition;
            pointer.dragging = true;
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerUpHandler);

            Assert.Less(slider.value, clickedValue);
            Assert.AreEqual(slider.value, _image.ScaleMultiplier, 0.0001f);
            AssertVector(ImageScale * slider.value, _image.transform.localScale);
            AssertVector(BlockScale, _block.transform.localScale);
        }

        private ArtisticElementController CreateElement(string name, Color colour, Vector3 scale)
        {
            var element = new GameObject(name);
            element.transform.SetParent(_root.transform, false);
            element.transform.localScale = scale;
            GameObject geometry = element;
            if (name == "RectangleBlock_obj")
            {
                geometry = new GameObject("Block geometry");
                geometry.transform.SetParent(element.transform, false);
                _blockGeometry = geometry.transform;
            }
            var sprite = geometry.AddComponent<SpriteRenderer>();
            sprite.color = colour;
            return element.AddComponent<ArtisticElementController>();
        }

        private void CreatePanel()
        {
            var panelObject = new GameObject("ControlPanel");
            panelObject.transform.SetParent(_root.transform, false);
            _panel = panelObject.AddComponent<ArtisticElementsControlPanel>();
            _panel.Configure(_selection);
        }

        private void AssertBlockGeometry(float length)
        {
            Vector3 origin = _blockGeometry.TransformPoint(Vector3.zero);
            Vector3 width = new Vector3(LengthDirection.y, -LengthDirection.x, 0f);
            AssertVector(_block.transform.TransformVector(LengthDirection * length),
                _blockGeometry.TransformPoint(LengthDirection) - origin);
            AssertVector(_block.transform.TransformVector(width),
                _blockGeometry.TransformPoint(width) - origin);
            AssertVector(_block.transform.TransformVector(Vector3.forward),
                _blockGeometry.TransformPoint(Vector3.forward) - origin);
        }

        private T Find<T>(string name) where T : Component
        {
            foreach (T component in _panel.GetComponentsInChildren<T>(true))
            {
                if (component.gameObject.name == name) return component;
            }
            Assert.Fail($"Missing {typeof(T).Name} named {name}.");
            return null;
        }

        private Transform FindTransform(string name)
        {
            return Find<Transform>(name);
        }

        private static Vector2 TrackScreenPoint(RectTransform track, float fraction)
        {
            Vector3 local = new Vector3(Mathf.Lerp(track.rect.xMin, track.rect.xMax, fraction),
                track.rect.center.y, 0f);
            return RectTransformUtility.WorldToScreenPoint(null, track.TransformPoint(local));
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
