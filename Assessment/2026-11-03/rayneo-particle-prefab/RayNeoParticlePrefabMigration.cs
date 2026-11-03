#if UNITY_EDITOR
using System;
using System.IO;
using AmpPortableDataViz.Presentation.Visualization;
using UnityEditor;
using UnityEngine;

// Run only in the isolated validation project. Save only the RayNeo variant.
public static class RayNeoParticlePrefabMigration
{
    public static void Apply()
    {
        const string path = "Assets/Prefabs/ParticleMeshVizRoot_RayNeo.prefab";
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

            foreach (var binding in root.GetComponentsInChildren<SensorVisualizationBinding>(true))
            {
                var serialized = new SerializedObject(binding);
                serialized.FindProperty("liveSource").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (var assembler in root.GetComponentsInChildren<SensorSampleAssembler>(true))
            {
                UnityEngine.Object.DestroyImmediate(assembler);
            }

            if (root.GetComponentInChildren<Collider>(true) == null ||
                root.GetComponentInChildren<ParticleMeshVisualizer>(true) == null ||
                root.GetComponentInChildren<SensorVisualizationBinding>(true) == null ||
                root.GetComponentInChildren<SensorDataSimulator>(true) == null)
            {
                throw new InvalidOperationException("Required rendering, gaze or development components are missing.");
            }

            PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
            if (!saved)
            {
                throw new InvalidOperationException("RayNeo prefab variant was not saved.");
            }

            File.WriteAllText(Environment.GetEnvironmentVariable("AMP_PREFAB_MIGRATION_RECORD"),
                "RayNeo variant saved using Unity PrefabUtility; removed root XRGrabInteractable/Rigidbody and local SensorSampleAssembler; cleared local liveSource; retained collider, renderer, visualization binding and simulator.\n");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
