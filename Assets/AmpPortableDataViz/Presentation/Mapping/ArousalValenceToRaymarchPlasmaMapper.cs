using AmpPortableDataViz.Core;

namespace AmpPortableDataViz.Presentation.Mapping
{
    /// <summary>
    /// Maps incoming valence/arousal samples to the parameter set expected by the EmotionPlasma shader.
    /// </summary>
    public sealed class ArousalValenceToRaymarchPlasmaMapper : IMapper<ArousalValenceSample, RaymarchPlasmaParams>
    {
        public RaymarchPlasmaParams Map(in DataFrame<ArousalValenceSample> inputFrame)
        {
            var payload = inputFrame.Payload;
            return RaymarchPlasmaParamsMapper.BuildParams(payload.ValenceLevel, payload.ArousalLevel);
        }
    }
}
