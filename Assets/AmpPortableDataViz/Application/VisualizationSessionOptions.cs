using UnityEngine;

namespace AmpPortableDataViz.Application
{
    public sealed class VisualizationSessionOptions
    {
        public bool IsHost { get; set; } = true;
        public string VisualId { get; set; } = "SimpleDemo";
        public string ProfileId { get; set; } = "Default";
        public int Seed { get; set; } = 12345;
        public string AnchorLabel { get; set; } = "Demo";
        public Vector3 AnchorPosition { get; set; } = new Vector3(0f, 1.2f, 2f);
        public Quaternion AnchorRotation { get; set; } = Quaternion.identity;
        public string SpawnTopic { get; set; } = "session/visual/spawn";
        public string ParamTopic { get; set; } = "session/visual/param";
    }
}
