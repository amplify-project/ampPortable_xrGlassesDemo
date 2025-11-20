// Assets/Tests/EmotionRayPlasmaManualDriver.cs (any folder is fine)
using System;
using AmpPortableDataViz.Presentation.Mapping;
using AmpPortableDataViz.Presentation.Visualization;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(EmotionRayPlasmaVisualizer))]
public class EmotionRayPlasmaManualDriver : MonoBehaviour
{
    [Range(0, 2)] public int valence = 1;   // 0 negative, 1 neutral, 2 positive
    [Range(0, 2)] public int arousal = 1;   // 0 low, 1 medium, 2 high
    [SerializeField] bool autoApply = true;

    EmotionRayPlasmaVisualizer _viz;

    void Awake() => _viz = GetComponent<EmotionRayPlasmaVisualizer>();

    void OnValidate() { if (autoApply) ApplyNow(); }          // updates in the inspector
    void Update() { if (autoApply) ApplyNow(); }              // updates in play & edit mode

    [ContextMenu("Apply Current Values")]
    public void ApplyNow()
    {
        if (_viz == null) return;
        var parms = RaymarchPlasmaParamsMapper.BuildParams(valence, arousal);
        _viz.Apply(parms, DateTime.UtcNow.Ticks);
    }
}
