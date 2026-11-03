#if UNITY_EDITOR
using System;
using System.IO;
using AmpPortableDataViz.Presentation.Visualization;
using UnityEditor;
using UnityEngine;

// Run only in the isolated validation project. Save only AudienceGraphPrefab.
public static class AudienceGraphPrefabMigration
{
    public static void Apply()
    {
        const string path = "Assets/Prefabs/AudienceGraphPrefab.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var component in root.GetComponents<Component>())
            {
                if (component != null && component.GetType().Name == "XRGrabInteractable")
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }
            }

            var body = root.GetComponent<Rigidbody>();
            if (body != null)
            {
                UnityEngine.Object.DestroyImmediate(body);
            }

            var binding = root.GetComponent<SensorVisualizationBinding>();
            var serialized = new SerializedObject(binding);
            serialized.FindProperty("liveSource").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (var assembler in root.GetComponentsInChildren<SensorSampleAssembler>(true))
            {
                UnityEngine.Object.DestroyImmediate(assembler);
            }

            if (root.GetComponentInChildren<Collider>(true) == null ||
                root.GetComponentsInChildren<GraphVisualizer>(true).Length != 5 ||
                binding == null || root.GetComponent<SensorDataSimulator>() == null)
            {
                throw new InvalidOperationException("Required graph, gaze or development components are missing.");
            }

            PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
            if (!saved)
            {
                throw new InvalidOperationException("Audience graph prefab was not saved.");
            }

            File.WriteAllText(Environment.GetEnvironmentVariable("AMP_PREFAB_MIGRATION_RECORD"),
                "AudienceGraphPrefab saved using Unity PrefabUtility; removed root XRGrabInteractable/Rigidbody and local SensorSampleAssembler; cleared liveSource; retained colliders, five graphs, visualization binding and simulator.\n");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
