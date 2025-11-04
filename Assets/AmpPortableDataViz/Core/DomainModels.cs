using UnityEngine;

namespace AmpPortableDataViz.Core
{
    /// <summary>
    /// Represents an absolute pose sample captured at a specific point in time.
    /// </summary>
    public readonly struct PoseSample
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly long TimestampTicksUtc;

        public PoseSample(Vector3 position, Quaternion rotation, long timestampTicksUtc)
        {
            Position = position;
            Rotation = rotation;
            TimestampTicksUtc = timestampTicksUtc;
        }
    }

    /// <summary>
    /// Describes the information required to spawn a visualization in the scene.
    /// </summary>
    public readonly struct VisualizationSpawnRequest
    {
        public readonly string VisualId;
        public readonly AnchorDescriptor Anchor;
        public readonly int Seed;
        public readonly string ProfileId;

        public VisualizationSpawnRequest(string visualId, AnchorDescriptor anchor, int seed, string profileId)
        {
            VisualId = visualId;
            Anchor = anchor;
            Seed = seed;
            ProfileId = profileId;
        }
    }
}

