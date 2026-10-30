// Validation-only template. Copy into Assets/Editor ONLY in the isolated snapshot.
// It is deliberately outside Assets in the source checkout and changes no settings.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PreMergeValidation
{
    [Serializable]
    private sealed class Issue
    {
        public string severity;
        public string kind;
        public string asset;
        public string objectPath;
        public string detail;
    }

    [Serializable]
    private sealed class Inspection
    {
        public string generatedAtUtc;
        public string unityVersion;
        public string[] enabledScenes;
        public int dependencyCount;
        public int gameObjectsScanned;
        public int componentsScanned;
        public int prefabsScanned;
        public int materialsScanned;
        public int missingScriptCount;
        public int brokenObjectReferenceCount;
        public int blockerCount;
        public int warningCount;
        public List<Issue> issues = new List<Issue>();
        public string limitations = "Editor inspection only. Null object references with instance ID zero cannot be distinguished from intentional empty fields. Duplicate findings may occur when a prefab is inspected both in-scene and as a dependency. Package/native/runtime-only content, real network behavior, device shader support, and wearer comfort require separate validation.";
    }

    [Serializable]
    private sealed class BuildEvidence
    {
        public string generatedAtUtc;
        public string unityVersion;
        public string projectPath;
        public string outputPath;
        public string result = "NotStarted";
        public bool buildAttempted;
        public string exception;
        public string target = "Android";
        public string options = "None";
        public string activeBuildTarget;
        public string applicationIdentifier;
        public string scriptingBackend;
        public string architectures;
        public string minimumAndroidApi;
        public string targetAndroidApi;
        public bool buildAppBundle;
        public bool exportAsGoogleAndroidProject;
        public bool automaticGraphicsApi;
        public string[] graphicsApis;
        public string[] scenes;
        public bool inspectionHasBlockers;
        public int errors;
        public int warnings;
        public double durationSeconds;
        public ulong sizeBytes;
        public List<Issue> messages = new List<Issue>();
    }

    public static void InspectAndBuildAndroid()
    {
        string projectPath = Directory.GetParent(Application.dataPath).FullName;
        string evidenceFolder = Environment.GetEnvironmentVariable("AMP_ASSESSMENT_OUTPUT");
        if (string.IsNullOrEmpty(evidenceFolder))
        {
            evidenceFolder = Directory.GetParent(projectPath).FullName;
        }
        Directory.CreateDirectory(evidenceFolder);
        string inspectionPath = Path.Combine(evidenceFolder, "scene-inspection.json");
        string buildPath = Path.Combine(evidenceFolder, "build-summary.json");
        var scenes = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
            {
                scenes.Add(scene.path);
            }
        }

        var inspection = new Inspection
        {
            generatedAtUtc = DateTime.UtcNow.ToString("o"),
            unityVersion = Application.unityVersion,
            enabledScenes = scenes.ToArray()
        };
        var build = new BuildEvidence
        {
            generatedAtUtc = DateTime.UtcNow.ToString("o"),
            unityVersion = Application.unityVersion,
            projectPath = projectPath,
            outputPath = Path.Combine(evidenceFolder, "RayNeo-assessment.apk"),
            activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
            scenes = scenes.ToArray()
        };

        try
        {
            InspectEnabledScenesAndDependencies(inspection);
        }
        catch (Exception exception)
        {
            AddIssue(inspection, "error", "inspection-exception", "", "", exception.ToString());
        }

        WriteJson(inspectionPath, inspection);
        build.inspectionHasBlockers = inspection.blockerCount != 0;
        try
        {
            build.applicationIdentifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            build.scriptingBackend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android).ToString();
            build.architectures = PlayerSettings.Android.targetArchitectures.ToString();
            build.minimumAndroidApi = PlayerSettings.Android.minSdkVersion.ToString();
            build.targetAndroidApi = PlayerSettings.Android.targetSdkVersion.ToString();
            build.buildAppBundle = EditorUserBuildSettings.buildAppBundle;
            build.exportAsGoogleAndroidProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            build.automaticGraphicsApi = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            build.graphicsApis = new string[apis.Length];
            for (int i = 0; i < apis.Length; i++)
            {
                build.graphicsApis[i] = apis[i].ToString();
            }

            WriteJson(buildPath, build);
            if (scenes.Count == 0)
            {
                throw new InvalidOperationException("No enabled build scenes; no APK build was attempted.");
            }

            build.result = "Running";
            build.buildAttempted = true;
            WriteJson(buildPath, build);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = build.outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            if (report == null)
            {
                throw new InvalidOperationException("BuildPipeline returned no BuildReport.");
            }

            BuildSummary summary = report.summary;
            build.result = summary.result.ToString();
            build.errors = summary.totalErrors;
            build.warnings = summary.totalWarnings;
            build.durationSeconds = summary.totalTime.TotalSeconds;
            build.sizeBytes = summary.totalSize;
            foreach (BuildStep step in report.steps)
            {
                foreach (BuildStepMessage message in step.messages)
                {
                    if (message.type == LogType.Warning || message.type == LogType.Error ||
                        message.type == LogType.Exception || message.type == LogType.Assert)
                    {
                        build.messages.Add(new Issue
                        {
                            severity = message.type == LogType.Warning ? "warning" : "error",
                            kind = "build-message",
                            asset = step.name,
                            objectPath = "",
                            detail = SanitizeDiagnostic(message.content)
                        });
                    }
                }
            }
        }
        catch (Exception exception)
        {
            build.result = "Exception";
            build.exception = SanitizeDiagnostic(exception.ToString());
            Debug.LogException(exception);
        }

        WriteJson(buildPath, build);
        bool passed = build.result == BuildResult.Succeeded.ToString() && !build.inspectionHasBlockers;
        Debug.Log("Pre-merge assessment evidence: " + inspectionPath + " ; " + buildPath);
        EditorApplication.Exit(passed ? 0 : 1);
    }

    private static void InspectEnabledScenesAndDependencies(Inspection inspection)
    {
        if (inspection.enabledScenes.Length == 0)
        {
            AddIssue(inspection, "error", "no-enabled-scenes", "", "", "No enabled build scenes.");
            return;
        }

        foreach (string scenePath in inspection.enabledScenes)
        {
            try
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    InspectHierarchy(root, scenePath, inspection);
                }
            }
            catch (Exception exception)
            {
                AddIssue(inspection, "error", "scene-load-exception", scenePath, "", exception.ToString());
            }
        }

        string[] dependencies = AssetDatabase.GetDependencies(inspection.enabledScenes, true);
        inspection.dependencyCount = dependencies.Length;
        foreach (string path in dependencies)
        {
            if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                GameObject contents = null;
                try
                {
                    contents = PrefabUtility.LoadPrefabContents(path);
                    inspection.prefabsScanned++;
                    InspectHierarchy(contents, path, inspection);
                }
                catch (Exception exception)
                {
                    AddIssue(inspection, "error", "prefab-load-exception", path, "", exception.ToString());
                }
                finally
                {
                    if (contents != null)
                    {
                        PrefabUtility.UnloadPrefabContents(contents);
                    }
                }
            }
            else if (path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                inspection.materialsScanned++;
                if (material == null)
                {
                    AddIssue(inspection, "warning", "material-unresolved", path, "", "Material did not resolve in the Editor.");
                }
                else if (material.shader == null)
                {
                    AddIssue(inspection, "warning", "shader-unresolved", path, "", "Material has no resolved shader; inspect build/device output.");
                }
                else if (material.shader.name == "Hidden/InternalErrorShader" || !material.shader.isSupported)
                {
                    AddIssue(inspection, "warning", "shader-editor-support", path, "", material.shader.name + ": Editor shader support does not establish Android support.");
                }
            }
        }
    }

    private static void InspectHierarchy(GameObject root, string assetPath, Inspection inspection)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            GameObject gameObject = transform.gameObject;
            string objectPath = HierarchyPath(transform);
            inspection.gameObjectsScanned++;
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
            if (missing > 0)
            {
                inspection.missingScriptCount += missing;
                AddIssue(inspection, "error", "missing-script", assetPath, objectPath, missing + " missing script component(s).");
            }

            foreach (Component component in gameObject.GetComponents<Component>())
            {
                if (component == null)
                {
                    continue;
                }

                inspection.componentsScanned++;
                try
                {
                    using (var serialized = new SerializedObject(component))
                    {
                        SerializedProperty property = serialized.GetIterator();
                        while (property.Next(true))
                        {
                            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                                property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                            {
                                inspection.brokenObjectReferenceCount++;
                                AddIssue(inspection, "warning", "broken-object-reference", assetPath, objectPath,
                                    component.GetType().FullName + "." + property.propertyPath + " has a nonzero instance ID but no resolved object.");
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    AddIssue(inspection, "warning", "serialized-inspection-exception", assetPath, objectPath,
                        component.GetType().FullName + ": " + exception.Message);
                }
            }
        }
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

    private static void AddIssue(Inspection inspection, string severity, string kind, string asset, string objectPath, string detail)
    {
        inspection.issues.Add(new Issue
        {
            severity = severity,
            kind = kind,
            asset = asset,
            objectPath = objectPath,
            detail = detail
        });
        if (severity == "error")
        {
            inspection.blockerCount++;
        }
        else
        {
            inspection.warningCount++;
        }
    }

    private static void WriteJson(string path, object value)
    {
        File.WriteAllText(path, JsonUtility.ToJson(value, true));
    }

    private static string SanitizeDiagnostic(string detail)
    {
        // Unity Gradle exceptions can dump the entire inherited process environment.
        // Keep the failure and stderr, without promoting unrelated environment data.
        return Regex.Replace(detail ?? "", @"Environment Variables:.*?(?=stderr\[|stdout\[|\z)",
            "Environment Variables: [omitted from assessment evidence]\n", RegexOptions.Singleline);
    }
}
#endif
