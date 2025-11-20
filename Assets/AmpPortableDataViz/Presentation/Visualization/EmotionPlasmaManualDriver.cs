// Debug helper to drive EmotionPlasma without a data feed.
using System;
using AmpPortableDataViz.Presentation.Mapping;
using AmpPortableDataViz.Presentation.Visualization;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(EmotionPlasmaVisualizer))]
public class EmotionPlasmaManualDriver : MonoBehaviour
{
    [Range(0, 2)] public int valence = 1;   // 0 negative, 1 neutral, 2 positive
    [Range(0, 2)] public int arousal = 1;   // 0 low, 1 medium, 2 high
    [SerializeField] bool autoApply = true;

    EmotionPlasmaVisualizer _visualizer;

    void Awake() => _visualizer = GetComponent<EmotionPlasmaVisualizer>();

    void OnValidate() { if (autoApply) ApplyNow(); }          // updates in the inspector
    void Update() { if (autoApply) ApplyNow(); }              // updates in play & edit mode

    [ContextMenu("Apply Current Values")]
    public void ApplyNow()
    {
        if (_visualizer == null) return;

        var parameters = PlasmaAffectMapper.BuildParams(valence, arousal);
        _visualizer.Apply(parameters, DateTime.UtcNow.Ticks);
    }
}
