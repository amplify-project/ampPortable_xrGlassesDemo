using AmpPortableDataViz.Core;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Visualization
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class EmotionPlasmaVisualizer : MonoBehaviour, IVisualizer<EmotionPlasmaParams>
    {
        [SerializeField] private Renderer targetRenderer;

        private MaterialPropertyBlock _propertyBlock;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int InnerColorId = Shader.PropertyToID("_ColorInner");
        private static readonly int OuterColorId = Shader.PropertyToID("_ColorOuter");

        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");

        private static readonly int CurvatureId = Shader.PropertyToID("_Curvature");
        private static readonly int AsymmetryId = Shader.PropertyToID("_Asymmetry");
        private static readonly int SurfaceSharpnessId = Shader.PropertyToID("_SurfaceSharpness");

        private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
        private static readonly int TurbulenceId = Shader.PropertyToID("_Turbulence");
        private static readonly int PulseFrequencyId = Shader.PropertyToID("_PulseFrequency");
        private static readonly int PulseAmplitudeId = Shader.PropertyToID("_PulseAmplitude");

        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int NoiseContrastId = Shader.PropertyToID("_NoiseContrast");

        private void Awake()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            _propertyBlock = new MaterialPropertyBlock();
        }

        public void Apply(in EmotionPlasmaParams parameters, long timestampTicksUtc)
        {
            if (targetRenderer == null)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();

            _propertyBlock.SetColor(BaseColorId, parameters.BaseColor);
            _propertyBlock.SetColor(InnerColorId, parameters.InnerColor);
            _propertyBlock.SetColor(OuterColorId, parameters.OuterColor);

            _propertyBlock.SetFloat(BrightnessId, parameters.Brightness);
            _propertyBlock.SetFloat(SaturationId, parameters.Saturation);

            _propertyBlock.SetFloat(CurvatureId, parameters.Curvature);
            _propertyBlock.SetFloat(AsymmetryId, parameters.Asymmetry);
            _propertyBlock.SetFloat(SurfaceSharpnessId, parameters.SurfaceSharpness);

            _propertyBlock.SetFloat(FlowSpeedId, parameters.FlowSpeed);
            _propertyBlock.SetFloat(TurbulenceId, parameters.Turbulence);
            _propertyBlock.SetFloat(PulseFrequencyId, parameters.PulseFrequency);
            _propertyBlock.SetFloat(PulseAmplitudeId, parameters.PulseAmplitude);

            _propertyBlock.SetFloat(SmoothnessId, parameters.Smoothness);
            _propertyBlock.SetFloat(NoiseScaleId, parameters.NoiseScale);
            _propertyBlock.SetFloat(NoiseContrastId, parameters.NoiseContrast);

            targetRenderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
