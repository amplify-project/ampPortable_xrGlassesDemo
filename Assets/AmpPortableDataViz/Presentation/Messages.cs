using UnityEngine;
using AmpPortableDataViz.Core;

namespace AmpPortableDataViz.Presentation
{
    [System.Serializable]
    public struct SpawnVisualizationMsg
    {
        public AnchorDescriptor Anchor;
        public string VisualId;
        public int Seed;
        public string ProfileId;
    }

    [System.Serializable]
    public struct ParamDeltaMsg
    {
        public string VisualId;
        public long TimestampTicks;
        public Vector4 Params0;
        public Vector4 Params1;
    }
}
