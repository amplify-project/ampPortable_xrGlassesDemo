using System.Collections.Generic;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.ArtisticElements
{
    /// <summary>Manual appearance controls relative to an element's authored transform.</summary>
    [DisallowMultipleComponent]
    public sealed class ArtisticElementController : MonoBehaviour
    {
        public enum ThicknessAxis { X, Y, Z }

        public const float MinimumMultiplier = 0.05f;
        public const float MaximumMultiplier = 5f;

        [SerializeField] private bool supportsThickness;
        [SerializeField] private ThicknessAxis thicknessAxis = ThicknessAxis.Z;
        [SerializeField] private bool supportsLength;
        [Tooltip("The long edge's direction in the element root's local coordinates.")]
        [SerializeField] private Vector3 lengthDirection = Vector3.up;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private readonly List<MeshTintTarget> _meshTargets = new List<MeshTintTarget>();
        private readonly List<SpriteTintTarget> _spriteTargets = new List<SpriteTintTarget>();
        private bool _initialized;
        private Vector3 _originalScale;
        private Quaternion _originalRotation;
        private Color _originalColour = Color.white;
        private Transform _lengthBasis;
        private Transform _lengthGeometry;

        public bool SupportsThickness => supportsThickness;
        public bool SupportsLength => supportsLength;
        public bool SupportsColour => _meshTargets.Count > 0 || _spriteTargets.Count > 0;
        public Color Colour { get; private set; } = Color.white;
        public Vector3 RotationOffset { get; private set; }
        public float ScaleMultiplier { get; private set; } = 1f;
        public float ThicknessMultiplier { get; private set; } = 1f;
        public float LengthMultiplier { get; private set; } = 1f;

        private void Awake()
        {
            CaptureDefaults();
            if (supportsLength) ApplyLength();
        }

        public void ConfigureThickness(bool enabled, ThicknessAxis axis = ThicknessAxis.Z)
        {
            CaptureDefaults();
            supportsThickness = enabled;
            thicknessAxis = (int)axis >= 0 && (int)axis <= 2 ? axis : ThicknessAxis.Z;
            if (!enabled)
            {
                ThicknessMultiplier = 1f;
            }
            ApplyScale();
        }

        public void SetColour(Color colour)
        {
            CaptureDefaults();
            if (!IsFinite(colour.r) || !IsFinite(colour.g) || !IsFinite(colour.b))
            {
                return;
            }

            // RGB controls preserve the element's original opacity.
            Colour = new Color(Mathf.Clamp01(colour.r), Mathf.Clamp01(colour.g),
                Mathf.Clamp01(colour.b), _originalColour.a);
            foreach (MeshTintTarget target in _meshTargets)
            {
                if (target.Renderer == null) continue;
                target.Renderer.GetPropertyBlock(target.Working, target.MaterialIndex);
                if (target.Working.isEmpty) target.Renderer.GetPropertyBlock(target.Working);
                target.Working.SetColor(target.PropertyId,
                    new Color(Colour.r, Colour.g, Colour.b, target.OriginalColour.a));
                target.Renderer.SetPropertyBlock(target.Working, target.MaterialIndex);
            }
            foreach (SpriteTintTarget target in _spriteTargets)
            {
                if (target.Renderer != null)
                {
                    target.Renderer.color = new Color(Colour.r, Colour.g, Colour.b, target.OriginalColour.a);
                }
            }
        }

        /// <summary>Enables length adjustment for geometry parented below this element root.</summary>
        public void ConfigureLength(bool enabled, Vector3 direction)
        {
            CaptureDefaults();
            supportsLength = enabled;
            lengthDirection = IsFinite(direction.x) && IsFinite(direction.y) && IsFinite(direction.z)
                && direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
            if (!enabled) LengthMultiplier = 1f;
            ApplyLength();
        }

        public void SetRotationOffset(Vector3 degrees)
        {
            CaptureDefaults();
            if (!IsFinite(degrees.x) || !IsFinite(degrees.y) || !IsFinite(degrees.z)) return;
            RotationOffset = new Vector3(Mathf.Clamp(degrees.x, -180f, 180f),
                Mathf.Clamp(degrees.y, -180f, 180f), Mathf.Clamp(degrees.z, -180f, 180f));
            transform.localRotation = _originalRotation * Quaternion.Euler(RotationOffset);
        }

        public void SetScaleMultiplier(float multiplier)
        {
            CaptureDefaults();
            if (!IsFinite(multiplier)) return;
            ScaleMultiplier = Mathf.Clamp(multiplier, MinimumMultiplier, MaximumMultiplier);
            ApplyScale();
        }

        public void SetThicknessMultiplier(float multiplier)
        {
            CaptureDefaults();
            if (!supportsThickness || !IsFinite(multiplier)) return;
            ThicknessMultiplier = Mathf.Clamp(multiplier, MinimumMultiplier, MaximumMultiplier);
            ApplyScale();
        }

        public void SetLengthMultiplier(float multiplier)
        {
            CaptureDefaults();
            if (!supportsLength || !IsFinite(multiplier)) return;
            LengthMultiplier = Mathf.Clamp(multiplier, MinimumMultiplier, MaximumMultiplier);
            ApplyLength();
        }

        public void ResetToDefaults()
        {
            CaptureDefaults();
            RotationOffset = Vector3.zero;
            ScaleMultiplier = 1f;
            ThicknessMultiplier = 1f;
            LengthMultiplier = 1f;
            Colour = _originalColour;
            transform.localRotation = _originalRotation;
            transform.localScale = _originalScale;
            ApplyLength();
            foreach (MeshTintTarget target in _meshTargets)
            {
                if (target.Renderer == null) continue;
                target.Renderer.SetPropertyBlock(target.Original.isEmpty ? null : target.Original,
                    target.MaterialIndex);
                target.Renderer.GetPropertyBlock(target.Working, target.MaterialIndex);
            }
            foreach (SpriteTintTarget target in _spriteTargets)
            {
                if (target.Renderer != null) target.Renderer.color = target.OriginalColour;
            }
        }

        private void CaptureDefaults()
        {
            if (_initialized) return;
            _initialized = true;
            _originalScale = transform.localScale;
            _originalRotation = transform.localRotation;
            bool colourFound = false;
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is SpriteRenderer sprite)
                {
                    _spriteTargets.Add(new SpriteTintTarget { Renderer = sprite, OriginalColour = sprite.color });
                    CaptureFirstColour(sprite.color, ref colourFound);
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null) continue;
                    int propertyId;
                    if (material.HasProperty(BaseColorId)) propertyId = BaseColorId;
                    else if (material.HasProperty(ColorId)) propertyId = ColorId;
                    else continue;

                    var original = new MaterialPropertyBlock();
                    var working = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(original, i);
                    renderer.GetPropertyBlock(working, i);
                    // A slot block takes precedence over a renderer-wide block. Preserve both.
                    var global = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(global);
                    if (working.isEmpty) renderer.GetPropertyBlock(working);
                    Color colour = original.HasColor(propertyId) ? original.GetColor(propertyId)
                        : original.isEmpty && global.HasColor(propertyId) ? global.GetColor(propertyId)
                        : material.GetColor(propertyId);
                    _meshTargets.Add(new MeshTintTarget
                    {
                        Renderer = renderer, MaterialIndex = i, PropertyId = propertyId,
                        Original = original, Working = working, OriginalColour = colour
                    });
                    CaptureFirstColour(colour, ref colourFound);
                }
            }
            Colour = _originalColour;
        }

        private void CaptureFirstColour(Color colour, ref bool colourFound)
        {
            if (colourFound) return;
            _originalColour = colour;
            colourFound = true;
        }

        private void ApplyScale()
        {
            Vector3 scale = _originalScale * ScaleMultiplier;
            int axis = Mathf.Clamp((int)thicknessAxis, 0, 2);
            if (supportsThickness) scale[axis] *= ThicknessMultiplier;
            transform.localScale = scale;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void ApplyLength()
        {
            if (!supportsLength && _lengthBasis == null) return;
            if (_lengthBasis == null)
            {
                // Align a scaling frame with the slanted mesh without modifying its imported vertices.
                var children = new Transform[transform.childCount];
                for (int i = 0; i < children.Length; i++) children[i] = transform.GetChild(i);
                _lengthBasis = new GameObject("LengthBasis").transform;
                _lengthBasis.SetParent(transform, false);
                _lengthGeometry = new GameObject("LengthGeometry").transform;
                _lengthGeometry.SetParent(_lengthBasis, false);
                foreach (Transform child in children) child.SetParent(_lengthGeometry, false);
            }
            Quaternion alignment = Quaternion.FromToRotation(Vector3.up, lengthDirection);
            _lengthBasis.localRotation = alignment;
            _lengthGeometry.localRotation = Quaternion.Inverse(alignment);
            _lengthBasis.localScale = new Vector3(1f, LengthMultiplier, 1f);
        }

        private sealed class MeshTintTarget
        {
            public Renderer Renderer;
            public int MaterialIndex;
            public int PropertyId;
            public MaterialPropertyBlock Original;
            public MaterialPropertyBlock Working;
            public Color OriginalColour;
        }

        private sealed class SpriteTintTarget
        {
            public SpriteRenderer Renderer;
            public Color OriginalColour;
        }
    }
}
