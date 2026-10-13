using System;
using System.Globalization;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.ArtisticElements
{
    /// <summary>Mouse-operated test panel for the selected artistic element.</summary>
    [DisallowMultipleComponent]
    public sealed class ArtisticElementsControlPanel : MonoBehaviour
    {
        [SerializeField] private ArtisticElementsSelectionController selection;

        private ArtisticElementsPanelLayout.Controls _view;
        private static readonly Color SelectedButtonColour = new Color(0.18f, 0.42f, 0.61f);
        private static readonly Color DefaultButtonColour = new Color(0.16f, 0.18f, 0.24f);

        public ArtisticElementsSelectionController Selection => selection;

        private void Start()
        {
            if (_view == null && selection != null) Configure(selection);
        }

        private void OnEnable()
        {
            if (_view != null && _view.CanvasRoot != null) _view.CanvasRoot.SetActive(true);
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            if (selection != null) selection.Changed -= Refresh;
            if (_view != null && _view.CanvasRoot != null) _view.CanvasRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_view != null && _view.CanvasRoot != null) Destroy(_view.CanvasRoot);
        }

        public void Configure(ArtisticElementsSelectionController target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (selection != null) selection.Changed -= Refresh;
            selection = target;
            if (_view == null || _view.ElementButtons.Length != selection.ElementCount) BuildView();
            Subscribe();
            Refresh();
        }

        private void Subscribe()
        {
            if (selection == null || _view == null || !isActiveAndEnabled) return;
            selection.Changed -= Refresh;
            selection.Changed += Refresh;
        }

        private void BuildView()
        {
            if (_view != null)
            {
                _view.CanvasRoot.SetActive(false);
                Destroy(_view.CanvasRoot);
            }
            _view = ArtisticElementsPanelLayout.Create(transform, selection);
            for (int i = 0; i < _view.ElementButtons.Length; i++)
            {
                int index = i;
                _view.ElementButtons[i].onClick.AddListener(() => selection.SelectElement(index));
            }
            foreach (var slider in _view.ColourSliders) slider.onValueChanged.AddListener(HandleColour);
            foreach (var slider in _view.RotationSliders) slider.onValueChanged.AddListener(HandleRotation);
            _view.ScaleSlider.onValueChanged.AddListener(value => selection.SetScaleMultiplier(value));
            _view.ThicknessSlider.onValueChanged.AddListener(value => selection.SetThicknessMultiplier(value));
            _view.LengthSlider.onValueChanged.AddListener(value => selection.SetLengthMultiplier(value));
            _view.ResetSelectedButton.onClick.AddListener(() => selection.ResetSelected());
            _view.ResetAllButton.onClick.AddListener(() => selection.ResetAll());
            _view.ToggleButton.onClick.AddListener(ToggleControls);
            _view.CanvasRoot.SetActive(isActiveAndEnabled);
        }

        private void HandleColour(float value)
        {
            selection.SetColour(new Color(_view.ColourSliders[0].value,
                _view.ColourSliders[1].value, _view.ColourSliders[2].value));
        }

        private void HandleRotation(float value)
        {
            selection.SetRotationOffset(new Vector3(_view.RotationSliders[0].value,
                _view.RotationSliders[1].value, _view.RotationSliders[2].value));
        }

        private void ToggleControls()
        {
            bool visible = !_view.ControlsBody.activeSelf;
            _view.ControlsBody.SetActive(visible);
            _view.ToggleLabel.text = visible ? "Hide controls" : "Show controls";
        }

        private void Refresh()
        {
            if (_view == null || selection == null) return;
            if (_view.ElementButtons.Length != selection.ElementCount) BuildView();
            for (int i = 0; i < _view.ElementButtons.Length; i++)
            {
                ArtisticElementController element = selection.GetElement(i);
                _view.ElementButtons[i].interactable = element != null;
                _view.ElementButtons[i].image.color = i == selection.SelectedIndex
                    ? SelectedButtonColour : DefaultButtonColour;
                _view.ElementLabels[i].text = element != null ? element.name : "Missing element";
            }

            ArtisticElementController selected = selection.SelectedElement;
            bool available = selected != null;
            _view.SelectedElementLabel.text = available ? selected.name : "No element selected";
            _view.ResetSelectedButton.interactable = available;
            _view.ResetAllButton.interactable = selection.ElementCount > 0;
            _view.ScaleSlider.interactable = available;
            _view.ThicknessSlider.interactable = available && selected.SupportsThickness;
            _view.ThicknessRow.SetActive(available && selected.SupportsThickness);
            _view.LengthSlider.interactable = available && selected.SupportsLength;
            _view.LengthRow.SetActive(available && selected.SupportsLength);
            for (int i = 0; i < 3; i++)
            {
                _view.ColourSliders[i].interactable = available && selected.SupportsColour;
                _view.RotationSliders[i].interactable = available;
            }
            if (!available) return;

            Color colour = selected.Colour;
            Vector3 rotation = selected.RotationOffset;
            _view.ColourPreview.color = colour;
            for (int i = 0; i < 3; i++)
            {
                _view.ColourSliders[i].SetValueWithoutNotify(colour[i]);
                _view.RotationSliders[i].SetValueWithoutNotify(rotation[i]);
                _view.ValueLabels[i].text = colour[i].ToString("0.00", CultureInfo.InvariantCulture);
                _view.ValueLabels[i + 3].text = rotation[i].ToString("0", CultureInfo.InvariantCulture) + "°";
            }
            _view.ScaleSlider.SetValueWithoutNotify(selected.ScaleMultiplier);
            _view.ThicknessSlider.SetValueWithoutNotify(selected.ThicknessMultiplier);
            _view.LengthSlider.SetValueWithoutNotify(selected.LengthMultiplier);
            _view.ValueLabels[6].text = selected.ScaleMultiplier.ToString("0.00", CultureInfo.InvariantCulture) + "×";
            _view.ValueLabels[7].text = selected.ThicknessMultiplier.ToString("0.00", CultureInfo.InvariantCulture) + "×";
            _view.ValueLabels[8].text = selected.LengthMultiplier.ToString("0.00", CultureInfo.InvariantCulture) + "×";
        }
    }
}
