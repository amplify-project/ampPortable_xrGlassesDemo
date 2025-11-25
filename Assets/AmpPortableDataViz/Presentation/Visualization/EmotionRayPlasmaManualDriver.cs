// Assets/Tests/EmotionRayPlasmaManualDriver.cs (any folder is fine)
using System;
using AmpPortableDataViz.Presentation.Mapping;
using AmpPortableDataViz.Presentation.Visualization;
using UnityEngine;
using UnityEngine.Events;

[ExecuteAlways]
[RequireComponent(typeof(EmotionRayPlasmaVisualizer))]
public class EmotionRayPlasmaManualDriver : MonoBehaviour
{
    [Range(0f, 2f)] public float valence = 1f;   // 0 negative, 1 neutral, 2 positive
    [Range(0f, 2f)] public float arousal = 1f;   // 0 low, 1 medium, 2 high
    [Range(30f, 200f)] public float heartRateBpm = 70f;
    [SerializeField] bool emitHeartRateOnApply = true;
    [SerializeField] bool logHeartRate;
    [SerializeField] UnityEvent<float> onHeartRateValue;
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

        if (emitHeartRateOnApply)
        {
            onHeartRateValue?.Invoke(heartRateBpm);
            if (logHeartRate)
            {
                Debug.Log($"EmotionRayPlasmaManualDriver HR={heartRateBpm:F1} bpm");
            }
        }
    }
}
