using AmpPortableDataViz.Core;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Visualization
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class EmotionRayPlasmaVisualizer : MonoBehaviour, IVisualizer<RaymarchPlasmaParams>
    {
        [SerializeField] private Renderer targetRenderer;

        private MaterialPropertyBlock _propertyBlock;

        private static readonly int ColorInnerId = Shader.PropertyToID("_ColorInner");
        private static readonly int ColorOuterId = Shader.PropertyToID("_ColorOuter");

        private static readonly int BoundsRadiusId = Shader.PropertyToID("_BoundsRadius");
        private static readonly int StepSizeId = Shader.PropertyToID("_StepSize");
        private static readonly int PlasmaScaleId = Shader.PropertyToID("_PlasmaScale");
        private static readonly int WarpStrengthId = Shader.PropertyToID("_WarpStrength");
        private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");

        private static readonly int DensityGainId = Shader.PropertyToID("_DensityGain");
        private static readonly int DensityThresholdId = Shader.PropertyToID("_DensityThreshold");
        private static readonly int DensityPowerId = Shader.PropertyToID("_DensityPower");

        private static readonly int AbsorptionId = Shader.PropertyToID("_Absorption");
        private static readonly int FalloffId = Shader.PropertyToID("_Falloff");
        private static readonly int EmissionId = Shader.PropertyToID("_Emission");

        private void Awake()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            _propertyBlock = new MaterialPropertyBlock();
        }

        public void Apply(in RaymarchPlasmaParams parameters, long timestampTicksUtc)
        {
            if (targetRenderer == null)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();

            _propertyBlock.SetColor(ColorInnerId, parameters.ColorInner);
            _propertyBlock.SetColor(ColorOuterId, parameters.ColorOuter);

            _propertyBlock.SetFloat(BoundsRadiusId, parameters.BoundsRadius);
            _propertyBlock.SetFloat(StepSizeId, parameters.StepSize);
            _propertyBlock.SetFloat(PlasmaScaleId, parameters.PlasmaScale);
            _propertyBlock.SetFloat(WarpStrengthId, parameters.WarpStrength);
            _propertyBlock.SetFloat(FlowSpeedId, parameters.FlowSpeed);

            _propertyBlock.SetFloat(DensityGainId, parameters.DensityGain);
            _propertyBlock.SetFloat(DensityThresholdId, parameters.DensityThreshold);
            _propertyBlock.SetFloat(DensityPowerId, parameters.DensityPower);

            _propertyBlock.SetFloat(AbsorptionId, parameters.Absorption);
            _propertyBlock.SetFloat(FalloffId, parameters.Falloff);
            _propertyBlock.SetFloat(EmissionId, parameters.Emission);

            targetRenderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
