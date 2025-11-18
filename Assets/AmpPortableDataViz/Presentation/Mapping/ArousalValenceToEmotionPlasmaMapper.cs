using AmpPortableDataViz.Core;

namespace AmpPortableDataViz.Presentation.Mapping
{
    /// <summary>
    /// Maps incoming valence/arousal samples to the parameter set expected by the EmotionPlasma shader.
    /// </summary>
    public sealed class ArousalValenceToEmotionPlasmaMapper : IMapper<ArousalValenceSample, EmotionPlasmaParams>
    {
        public EmotionPlasmaParams Map(in DataFrame<ArousalValenceSample> inputFrame)
        {
            var payload = inputFrame.Payload;
            return PlasmaAffectMapper.BuildParams(payload.ValenceLevel, payload.ArousalLevel);
        }
    }
}
