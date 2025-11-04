using AmpPortableDataViz.Core;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Visualization
{
    [RequireComponent(typeof(MeshRenderer))]
    [DisallowMultipleComponent]
    public sealed class SimpleVisualizer : MonoBehaviour, IVisualizer<SimpleVisualParams>
    {
        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _materialPropertyBlock;

        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int IntensityPropertyId = Shader.PropertyToID("_Intensity");
        private static readonly int FlowVectorPropertyId = Shader.PropertyToID("_Flow");
        private static readonly int EmissionPropertyId = Shader.PropertyToID("_Emission");

        private void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            _materialPropertyBlock = new MaterialPropertyBlock();

            if (_meshRenderer.sharedMaterial == null)
            {
                var defaultMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                defaultMaterial.SetColor(BaseColorPropertyId, Color.white);
                _meshRenderer.material = defaultMaterial;
            }
        }

        public void Apply(in SimpleVisualParams parameters, long timestampTicksUtc)
        {
            _meshRenderer.GetPropertyBlock(_materialPropertyBlock);
            _materialPropertyBlock.SetColor(BaseColorPropertyId, parameters.Color);
            _materialPropertyBlock.SetFloat(IntensityPropertyId, Mathf.Clamp01(parameters.Intensity));
            _materialPropertyBlock.SetVector(FlowVectorPropertyId, parameters.Flow);
            _materialPropertyBlock.SetFloat(EmissionPropertyId, Mathf.Max(0f, parameters.Intensity));
            _meshRenderer.SetPropertyBlock(_materialPropertyBlock);
        }
    }
}
