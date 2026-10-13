using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AmpPortableDataViz.Presentation.ArtisticElements
{
    /// <summary>Creates the desktop panel's graphics and controls without binding element actions.</summary>
    internal static class ArtisticElementsPanelLayout
    {
        private static readonly Color PanelColour = new Color(0.055f, 0.07f, 0.1f, 0.97f);
        private static readonly Color ButtonColour = new Color(0.13f, 0.17f, 0.23f, 1f);
        private static readonly Color TrackColour = new Color(0.18f, 0.23f, 0.3f, 1f);
        private static readonly Color AccentColour = new Color(0.35f, 0.75f, 0.95f, 1f);

        internal sealed class Controls
        {
            internal GameObject CanvasRoot, ControlsBody, ThicknessRow, LengthRow;
            internal Button[] ElementButtons;
            internal Slider[] ColourSliders, RotationSliders;
            internal Slider ScaleSlider, ThicknessSlider, LengthSlider;
            internal TMP_Text[] ValueLabels, ElementLabels;
            internal TMP_Text SelectedElementLabel;
            internal Image ColourPreview;
            internal Button ResetSelectedButton, ResetAllButton, ToggleButton;
            internal TMP_Text ToggleLabel;
        }

        internal static Controls Create(Transform parent, ArtisticElementsSelectionController selection)
        {
            var controls = new Controls
            {
                ElementButtons = new Button[selection.ElementCount],
                ElementLabels = new TMP_Text[selection.ElementCount],
                ColourSliders = new Slider[3],
                RotationSliders = new Slider[3],
                ValueLabels = new TMP_Text[9]
            };
            controls.CanvasRoot = new GameObject("ArtisticElementsCanvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            controls.CanvasRoot.transform.SetParent(parent, false);
            Canvas canvas = controls.CanvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = controls.CanvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            EnsureEventSystem(controls.CanvasRoot.transform);

            RectTransform toggle = DockBox("ToggleControls", controls.CanvasRoot.transform, 12f, 12f, 340f, 34f);
            controls.ToggleButton = AddButton(toggle, "Hide controls", out controls.ToggleLabel);
            RectTransform body = DockBox("ControlsBody", controls.CanvasRoot.transform, 12f, 54f, 340f, 840f);
            controls.ControlsBody = body.gameObject;
            AddImage(body, PanelColour, true);
            AddText(Box("Title", body, 16f, 14f, 308f, 26f), "Artistic elements", 22f);
            controls.SelectedElementLabel = AddText(Box("SelectedElement", body, 16f, 44f, 308f, 28f),
                string.Empty, 15f);
            for (int i = 0; i < selection.ElementCount; i++)
            {
                RectTransform button = Box($"Element_{i}", body, 16f, 80f + i * 38f, 308f, 32f);
                controls.ElementButtons[i] = AddButton(button, selection.GetElement(i).name,
                    out controls.ElementLabels[i]);
            }

            AddText(Box("ColourHeading", body, 16f, 284f, 224f, 24f), "Tint colour", 17f);
            controls.ColourPreview = AddImage(Box("ColourPreview", body, 246f, 284f, 78f, 24f), Color.white, false);
            string[] colourNames = { "R", "G", "B" };
            string[] axisNames = { "X", "Y", "Z" };
            for (int i = 0; i < 3; i++)
            {
                controls.ColourSliders[i] = AddSliderRow(body, "Colour_" + colourNames[i], colourNames[i],
                    318f + i * 44f, 0f, 1f, 1f, out controls.ValueLabels[i]);
            }
            AddText(Box("RotationHeading", body, 16f, 464f, 308f, 24f), "Rotation offset (degrees)", 17f);
            for (int i = 0; i < 3; i++)
            {
                controls.RotationSliders[i] = AddSliderRow(body, "Rotation_" + axisNames[i], axisNames[i],
                    496f + i * 44f, -180f, 180f, 0f, out controls.ValueLabels[i + 3]);
            }
            controls.ScaleSlider = AddSliderRow(body, "Scale", "Scale", 646f,
                ArtisticElementController.MinimumMultiplier, ArtisticElementController.MaximumMultiplier,
                1f, out controls.ValueLabels[6]);
            RectTransform thickness = Box("ThicknessRow", body, 16f, 690f, 308f, 36f);
            controls.ThicknessRow = thickness.gameObject;
            controls.ThicknessSlider = AddSlider(thickness, "Thickness", "Depth",
                ArtisticElementController.MinimumMultiplier, ArtisticElementController.MaximumMultiplier,
                1f, out controls.ValueLabels[7]);
            RectTransform length = Box("LengthRow", body, 16f, 734f, 308f, 36f);
            controls.LengthRow = length.gameObject;
            controls.LengthSlider = AddSlider(length, "Length", "Length",
                ArtisticElementController.MinimumMultiplier, ArtisticElementController.MaximumMultiplier,
                1f, out controls.ValueLabels[8]);
            controls.ResetSelectedButton = AddButton(Box("ResetSelected", body, 16f, 788f, 150f, 40f),
                "Reset selected", out _);
            controls.ResetAllButton = AddButton(Box("ResetAll", body, 174f, 788f, 150f, 40f),
                "Reset all", out _);
            return controls;
        }

        private static Slider AddSliderRow(Transform parent, string name, string label, float y,
            float minimum, float maximum, float initialValue, out TMP_Text valueLabel)
        {
            return AddSlider(Box(name + "Row", parent, 16f, y, 308f, 36f), name, label,
                minimum, maximum, initialValue, out valueLabel);
        }

        private static Slider AddSlider(RectTransform row, string name, string label,
            float minimum, float maximum, float initialValue, out TMP_Text valueLabel)
        {
            AddText(Box("Label", row, 0f, 0f, 64f, 36f), label, 15f);
            valueLabel = AddText(Box("Value", row, 264f, 0f, 44f, 36f), string.Empty, 14f);
            valueLabel.alignment = TextAlignmentOptions.MidlineRight;
            RectTransform sliderRect = Box(name, row, 72f, 4f, 180f, 28f);
            // A transparent Image supplies pointer hits along the whole track, including the unfilled area.
            AddImage(sliderRect, Color.clear, true);
            RectTransform track = StretchBox("Track", sliderRect, 0f, 10f, 0f, 10f);
            AddImage(track, TrackColour, false);
            RectTransform fillArea = StretchBox("FillArea", sliderRect, 0f, 10f, 0f, 10f);
            RectTransform fill = StretchBox("Fill", fillArea);
            AddImage(fill, AccentColour, false);
            RectTransform handleArea = StretchBox("HandleArea", sliderRect, 9f, 0f, 9f, 0f);
            RectTransform handle = StretchBox("Handle", handleArea);
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            handle.sizeDelta = new Vector2(18f, 0f);
            Image handleImage = AddImage(handle, AccentColour, true);
            Slider slider = sliderRect.gameObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.SetValueWithoutNotify(initialValue);
            return slider;
        }

        private static Button AddButton(RectTransform rect, string label, out TMP_Text text)
        {
            Image image = AddImage(rect, ButtonColour, true);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            text = AddText(StretchBox("Label", rect, 8f, 0f, 8f, 0f), label, 15f);
            text.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static TMP_Text AddText(RectTransform rect, string text, float size)
        {
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = size;
            label.color = new Color(0.93f, 0.96f, 1f, 1f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        private static Image AddImage(RectTransform rect, Color colour, bool raycast)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = raycast;
            return image;
        }

        private static RectTransform Box(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static RectTransform DockBox(string name, Transform parent, float x, float y, float width, float height)
        {
            RectTransform rect = Box(name, parent, x, y, width, height);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-x, -y);
            return rect;
        }

        private static RectTransform StretchBox(string name, Transform parent,
            float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            RectTransform rect = Box(name, parent, 0f, 0f, 0f, 0f);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        private static void EnsureEventSystem(Transform parent)
        {
            foreach (EventSystem existing in Object.FindObjectsOfType<EventSystem>())
            {
                if (existing.isActiveAndEnabled) return;
            }
            var eventObject = new GameObject("ArtisticElementsEventSystem",
                typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventObject.transform.SetParent(parent, false);
        }
    }
}
