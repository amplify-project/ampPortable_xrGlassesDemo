// Validation-only template. Copy to Assets/Editor ONLY in the isolated snapshot.
// Reads imported script/type associations and prefab components; never saves,
// repairs, reimports, or changes settings. Exit zero means evidence was written,
// not that every association is valid. GetClass() may legitimately be null for
// contract files, interfaces, generic types, or files without a matching class.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class MonoScriptProbe
{
    [Serializable]
    private sealed class ScriptEvidence
    {
        public string searchGuid;
        public string guidToAssetPath;
        public bool scriptLoaded;
        public string loadedAssetPath;
        public bool importedIdentifierAvailable;
        public string importedGuid;
        public long importedLocalFileId;
        public string importedClass;
        public string expectedClass;
        public bool expectedManagedTypeAvailable;
        public string expectedManagedAssemblyQualifiedName;
        public bool expectedTypeIsMonoBehaviour;
        public bool importedClassMatchesExpected;
        public string exception;
    }

    [Serializable]
    private sealed class ObjectEvidence
    {
        public string objectPath;
        public int missingScriptCount;
        public int nullComponentSlots;
        public string[] managedComponentTypes;
    }

    [Serializable]
    private sealed class PrefabEvidence
    {
        public string assetPath;
        public bool loaded;
        public int missingScriptCount;
        public string exception;
        public List<ObjectEvidence> objects = new List<ObjectEvidence>();
    }

    [Serializable]
    private sealed class ProbeEvidence
    {
        public string generatedAtUtc;
        public string unityVersion;
        public string projectPath;
        public string activeBuildTarget;
        public string ampAssembly;
        public int scriptCount;
        public int importedClassCount;
        public int knownTypeChecks;
        public int knownTypeMismatches;
        public List<ScriptEvidence> scripts = new List<ScriptEvidence>();
        public List<PrefabEvidence> prefabs = new List<PrefabEvidence>();
        public string fatalException;
        public string limitations = "Supplementary diagnostic after the initial import/test run. A zero process exit means evidence was written, not a passing baseline. MonoScript.GetClass may legitimately be null for contracts/interfaces/generic types. Known-type comparisons concern explicitly named MonoBehaviour scripts. Prefab counts can repeat inherited base components. No scene, asset, script, metadata or settings are saved, repaired or reimported by this helper.";
    }

    private static readonly Dictionary<string, string> ExpectedTypes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "ArtistVizVisualizer", "AmpPortableDataViz.Presentation.Visualization.ArtistVizVisualizer" },
        { "AudienceSignalVisualizerBinding", "AmpPortableDataViz.Presentation.Visualization.AudienceSignalVisualizerBinding" },
        { "ArtisticElementsTestSetup", "AmpPortableDataViz.Presentation.ArtisticElements.ArtisticElementsTestSetup" },
        { "ArtisticElementController", "AmpPortableDataViz.Presentation.ArtisticElements.ArtisticElementController" },
        { "RuntimePngLoader", "RuntimePngLoader" },
        { "ParticleMeshVisualizer", "AmpPortableDataViz.Presentation.Visualization.ParticleMeshVisualizer" },
        { "AudienceSignalBinding", "AmpPortableDataViz.Presentation.Visualization.AudienceSignalBinding" },
        { "ParticleMeshManualDriver", "AmpPortableDataViz.Presentation.Visualization.ParticleMeshManualDriver" },
        { "AudienceDataManualDriver", "AmpPortableDataViz.Presentation.Visualization.AudienceDataManualDriver" },
        { "GraphVisualizer", "AmpPortableDataViz.Presentation.Visualization.GraphVisualizer" },
        { "Bootstrapper", "AmpPortableDataViz.Presentation.Bootstrap.Bootstrapper" },
        { "RedisEndpointPrompt", "AmpPortableDataViz.Presentation.Sources.RedisEndpointPrompt" },
        { "RedisManager", "RedisManager" }
    };

    public static void Inspect()
    {
        string projectPath = Directory.GetParent(Application.dataPath).FullName;
        string outputPath = Path.Combine(Directory.GetParent(projectPath).FullName, "mono-script-probe.json");
        var evidence = new ProbeEvidence
        {
            generatedAtUtc = DateTime.UtcNow.ToString("o"),
            unityVersion = Application.unityVersion,
            projectPath = projectPath,
            activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString()
        };

        try
        {
            Assembly ampAssembly = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "Amp")
                {
                    ampAssembly = assembly;
                    evidence.ampAssembly = assembly.FullName;
                    break;
                }
            }

            string[] guids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/AmpPortableDataViz" });
            Array.Sort(guids, StringComparer.Ordinal);
            foreach (string guid in guids)
            {
                var scriptEvidence = new ScriptEvidence
                {
                    searchGuid = guid,
                    guidToAssetPath = AssetDatabase.GUIDToAssetPath(guid)
                };
                evidence.scripts.Add(scriptEvidence);
                try
                {
                    string name = Path.GetFileNameWithoutExtension(scriptEvidence.guidToAssetPath);
                    bool expected = ExpectedTypes.TryGetValue(name, out string expectedClass);
                    scriptEvidence.expectedClass = expectedClass;
                    Type expectedType = expected && ampAssembly != null ? ampAssembly.GetType(expectedClass, false) : null;
                    scriptEvidence.expectedManagedTypeAvailable = expectedType != null;
                    scriptEvidence.expectedManagedAssemblyQualifiedName = expectedType == null ? null : expectedType.AssemblyQualifiedName;
                    scriptEvidence.expectedTypeIsMonoBehaviour = expectedType != null && typeof(MonoBehaviour).IsAssignableFrom(expectedType);

                    MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptEvidence.guidToAssetPath);
                    scriptEvidence.scriptLoaded = script != null;
                    if (script != null)
                    {
                        scriptEvidence.loadedAssetPath = AssetDatabase.GetAssetPath(script);
                        scriptEvidence.importedIdentifierAvailable = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                            script, out string importedGuid, out long importedLocalFileId);
                        scriptEvidence.importedGuid = importedGuid;
                        scriptEvidence.importedLocalFileId = importedLocalFileId;
                        Type importedClass = script.GetClass();
                        scriptEvidence.importedClass = importedClass == null ? null : importedClass.AssemblyQualifiedName;
                        scriptEvidence.importedClassMatchesExpected = expected && expectedType != null && importedClass == expectedType;
                        if (importedClass != null) evidence.importedClassCount++;
                    }
                    if (expected)
                    {
                        evidence.knownTypeChecks++;
                        if (!scriptEvidence.importedClassMatchesExpected) evidence.knownTypeMismatches++;
                    }
                }
                catch (Exception exception)
                {
                    scriptEvidence.exception = exception.ToString();
                }
            }
            evidence.scriptCount = evidence.scripts.Count;

            foreach (string assetPath in new[]
            {
                "Assets/Prefabs/ArtistViz.prefab",
                "Assets/Prefabs/ImageBoard.prefab",
                "Assets/Prefabs/ParticleMeshViz.prefab",
                "Assets/Prefabs/ParticleMeshVizRoot.prefab",
                "Assets/Prefabs/ParticleMeshVizRoot_RayNeo.prefab",
                "Assets/Prefabs/AudienceGraphPrefab.prefab"
            })
            {
                evidence.prefabs.Add(InspectPrefab(assetPath));
            }
        }
        catch (Exception exception)
        {
            evidence.fatalException = exception.ToString();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllText(outputPath, JsonUtility.ToJson(evidence, true));
        Debug.Log("MonoScript diagnostic evidence written to " + outputPath);
        EditorApplication.Exit(evidence.fatalException == null ? 0 : 1);
    }

    private static PrefabEvidence InspectPrefab(string path)
    {
        var evidence = new PrefabEvidence { assetPath = path };
        GameObject contents = null;
        try
        {
            contents = PrefabUtility.LoadPrefabContents(path);
            evidence.loaded = contents != null;
            if (contents == null) return evidence;
            foreach (Transform transform in contents.GetComponentsInChildren<Transform>(true))
            {
                var item = new ObjectEvidence
                {
                    objectPath = HierarchyPath(transform),
                    missingScriptCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject)
                };
                var managedTypes = new List<string>();
                foreach (Component component in transform.GetComponents<Component>())
                {
                    if (component == null) item.nullComponentSlots++;
                    else if (component is MonoBehaviour) managedTypes.Add(component.GetType().AssemblyQualifiedName);
                }
                item.managedComponentTypes = managedTypes.ToArray();
                evidence.missingScriptCount += item.missingScriptCount;
                if (item.missingScriptCount != 0 || managedTypes.Count != 0) evidence.objects.Add(item);
            }
        }
        catch (Exception exception)
        {
            evidence.exception = exception.ToString();
        }
        finally
        {
            if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
        }
        return evidence;
    }

    private static string HierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
}
#endif
