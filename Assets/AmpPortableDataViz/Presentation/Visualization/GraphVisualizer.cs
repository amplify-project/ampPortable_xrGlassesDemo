using AmpPortableDataViz.Core;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Visualization
{

    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class GraphVisualizer : MonoBehaviour, IVisualizer<GraphParams>
    {
        public void Apply(in GraphParams parameters, long timestampTicksUtc)
        {

        }

    }
}