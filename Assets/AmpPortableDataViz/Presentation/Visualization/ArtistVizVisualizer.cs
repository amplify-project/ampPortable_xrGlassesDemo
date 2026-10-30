using AmpPortableDataViz.Core;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Visualization
{
    /// <summary>
    /// Receives normalized audience parameters for the artist visualization.
    /// Visual behavior will be added once the final artistic mapping is defined.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Amp Portable Data Viz/Visualization/Artist Viz Visualizer")]
    public sealed class ArtistVizVisualizer : MonoBehaviour, IVisualizer<ArtistVizParams>
    {
        [Header("Diagnostics")]
        [SerializeField] private bool logAppliedParameters;

        public bool HasAppliedParameters { get; private set; }
        public ArtistVizParams CurrentParameters { get; private set; }
        public long LastTimestampTicksUtc { get; private set; }

        public void Apply(in ArtistVizParams parameters, long timestampTicksUtc)
        {
            CurrentParameters = parameters;
            LastTimestampTicksUtc = timestampTicksUtc;
            HasAppliedParameters = true;

            if (logAppliedParameters)
            {
                Debug.Log(
                    $"ArtistVizVisualizer[{name}] received parameters for '{parameters.DeviceId}' at {timestampTicksUtc}.",
                    this);
            }
        }
    }
}
