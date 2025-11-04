using UnityEngine;

namespace AmpPortableDataViz.Core
{
    /// <summary>
    /// Application-facing abstraction for registering and resolving anchors.
    /// Implementation lives in the infrastructure layer (e.g., a Unity MonoBehaviour registry).
    /// </summary>
    public interface IAnchorRegistry
    {
        void Register(string anchorId, Transform anchorTransform);
        bool TryGet(string anchorId, out Transform anchorTransform);
    }

    /// <summary>
    /// Factory responsible for creating visualization instances given a spawn request.
    /// Implementations are adapters that know how to construct Unity objects, prefabs, etc.
    /// </summary>
    public interface IVisualizationFactory
    {
        IVisualizationInstance Create(VisualizationSpawnRequest request);
    }

    /// <summary>
    /// Represents a spawned visualization instance under application control.
    /// Allows the application layer to remain agnostic of Unity specific behaviours.
    /// </summary>
    public interface IVisualizationInstance
    {
        string VisualId { get; }
        void BindToAnchor(string anchorId, Transform anchorTransform);
        void Apply(SimpleVisualParams parameters, long timestampTicksUtc);
        void ApplyPose(in PoseSample pose);
    }
}
