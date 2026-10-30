using System;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.ArtisticElements
{
    /// <summary>Routes manual panel actions to the selected element without changing visibility.</summary>
    [DisallowMultipleComponent]
    public sealed class ArtisticElementsSelectionController : MonoBehaviour
    {
        private ArtisticElementController[] _elements = Array.Empty<ArtisticElementController>();

        public event Action Changed;
        public int ElementCount => _elements.Length;
        public int SelectedIndex { get; private set; } = -1;
        public ArtisticElementController SelectedElement => SelectedIndex >= 0 ? _elements[SelectedIndex] : null;

        public void Configure(ArtisticElementController[] targets)
        {
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            foreach (ArtisticElementController target in targets)
            {
                if (target == null) throw new ArgumentException("Each element must have a controller.", nameof(targets));
            }
            _elements = (ArtisticElementController[])targets.Clone();
            SelectedIndex = _elements.Length > 0 ? 0 : -1;
            Changed?.Invoke();
        }

        public ArtisticElementController GetElement(int index)
        {
            return index >= 0 && index < _elements.Length ? _elements[index] : null;
        }

        public bool SelectElement(int index)
        {
            if (GetElement(index) == null) return false;
            SelectedIndex = index;
            Changed?.Invoke();
            return true;
        }

        public void SetColour(Color colour)
        {
            if (SelectedElement == null) return;
            SelectedElement.SetColour(colour);
            Changed?.Invoke();
        }

        public void SetRotationOffset(Vector3 degrees)
        {
            if (SelectedElement == null) return;
            SelectedElement.SetRotationOffset(degrees);
            Changed?.Invoke();
        }

        public void SetScaleMultiplier(float multiplier)
        {
            if (SelectedElement == null) return;
            SelectedElement.SetScaleMultiplier(multiplier);
            Changed?.Invoke();
        }

        public void SetThicknessMultiplier(float multiplier)
        {
            if (SelectedElement == null) return;
            SelectedElement.SetThicknessMultiplier(multiplier);
            Changed?.Invoke();
        }

        public void SetLengthMultiplier(float multiplier)
        {
            if (SelectedElement == null) return;
            SelectedElement.SetLengthMultiplier(multiplier);
            Changed?.Invoke();
        }

        public void ResetSelected()
        {
            if (SelectedElement == null) return;
            SelectedElement.ResetToDefaults();
            Changed?.Invoke();
        }

        public void ResetAll()
        {
            foreach (ArtisticElementController element in _elements)
            {
                if (element != null) element.ResetToDefaults();
            }
            Changed?.Invoke();
        }
    }
}
