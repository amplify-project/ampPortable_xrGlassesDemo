#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using AmpPortableDataViz.Infra;
using AmpPortableDataViz.Presentation.Bootstrap;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public sealed class BootstrapperSerializationTests
    {
        [Test]
        public void CurrentPrefab_RoundTripsEveryConfigurationField()
        {
            string prefix = "Assets/Tests/PlayMode/BootstrapperConfiguration_" + Guid.NewGuid().ToString("N");
            string currentPath = prefix + "_current.prefab";
            string importedPath = prefix + "_imported.prefab";
            string cameraPath = prefix + "_camera.prefab";
            var root = new GameObject(nameof(BootstrapperSerializationTests));
            var cameraRoot = new GameObject("ConfigurationCamera");
            root.SetActive(false);
            cameraRoot.SetActive(false);
            try
            {
                cameraRoot.AddComponent<Camera>();
                var cameraAsset = PrefabUtility.SaveAsPrefabAsset(cameraRoot, cameraPath);
                Assert.IsNotNull(cameraAsset);
                var camera = cameraAsset.GetComponent<Camera>();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ParticleMeshVizRoot_RayNeo.prefab");
                Assert.IsNotNull(prefab);
                var bootstrapper = root.AddComponent<Bootstrapper>();
                FieldInfo[] fields = Array.FindAll(typeof(Bootstrapper).GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly),
                    field => field.IsPublic || Attribute.IsDefined(field, typeof(SerializeField)));
                Assert.AreEqual(50, fields.Length);
                var expected = new Dictionary<string, object>();
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    object value = ConfigurationValue(field, bootstrapper, prefab, camera, i);
                    field.SetValue(bootstrapper, value);
                    expected.Add(field.Name, value);
                }

                Assert.IsNotNull(PrefabUtility.SaveAsPrefabAsset(root, currentPath));
                string yaml = File.ReadAllText(currentPath);
                foreach (FieldInfo field in fields)
                {
                    Assert.AreEqual(1, Regex.Matches(yaml, "(?m)^  " + field.Name + ":").Count, field.Name);
                }
                // A fresh asset prevents the test from reading the original component's memory.
                File.WriteAllText(importedPath, yaml);
                AssetDatabase.ImportAsset(importedPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var importedRoot = AssetDatabase.LoadAssetAtPath<GameObject>(importedPath);
                Assert.IsNotNull(importedRoot);
                var imported = importedRoot.GetComponent<Bootstrapper>();
                Assert.IsNotNull(imported);
                foreach (FieldInfo field in fields)
                {
                    object value = field.GetValue(imported);
                    if (field.FieldType.IsArray)
                        CollectionAssert.AreEqual((Array)expected[field.Name], (Array)value, field.Name);
                    else if (expected[field.Name] is UnityEngine.Object)
                        Assert.AreSame(expected[field.Name], value, field.Name);
                    else
                        Assert.AreEqual(expected[field.Name], value, field.Name);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(cameraRoot);
                AssetDatabase.DeleteAsset(currentPath);
                AssetDatabase.DeleteAsset(importedPath);
                AssetDatabase.DeleteAsset(cameraPath);
            }
        }

        private static object ConfigurationValue(FieldInfo field, Bootstrapper target,
            GameObject prefab, Camera camera, int index)
        {
            Type type = field.FieldType;
            if (type == typeof(bool)) return !(bool)field.GetValue(target);
            if (type == typeof(int)) return index + 100;
            if (type == typeof(float)) return (index + 1) * 0.01f;
            if (type == typeof(string)) return "configuration-" + field.Name;
            if (type == typeof(string[])) return new[] { "sensor-one", "sensor-two" };
            if (type == typeof(Vector3)) return new Vector3(index + 1, index + 2, index + 3);
            if (type == typeof(Color)) return new Color(0.1f, 0.2f, 0.3f, 0.4f);
            if (type == typeof(GameObject)) return prefab;
            if (type == typeof(Transform)) return prefab.transform;
            if (type == typeof(Camera)) return camera;
            if (type == typeof(Bootstrapper.SensorEngagementChannelOverride[]))
                return new[] { new Bootstrapper.SensorEngagementChannelOverride
                    { SensorId = "sensor-one", ChannelName = "custom:sensor-one:engagement" } };
            if (type == typeof(SensorEngagementJsonFormat))
                return new SensorEngagementJsonFormat
                {
                    ItemsPropertyName = "records", SensorIdPropertyName = "sensor",
                    EngagementPropertyName = "score", ConfirmationPropertyName = "trusted",
                    TimestampPropertyName = "time"
                };
            throw new InvalidOperationException("Unhandled configuration type: " + type);
        }

        [Test]
        public void LegacyPrefab_ImportsRetainedSettingsAndObjectReferences()
        {
            string prefix = "Assets/Tests/PlayMode/BootstrapperMigration_" + Guid.NewGuid().ToString("N");
            string currentPath = prefix + "_current.prefab";
            string legacyPath = prefix + "_legacy.prefab";
            var root = new GameObject(nameof(BootstrapperSerializationTests));
            root.SetActive(false);
            try
            {
                var bootstrapper = root.AddComponent<Bootstrapper>();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ParticleMeshVizRoot_RayNeo.prefab");
                Assert.IsNotNull(prefab);
                BootstrapperTestConfiguration.Set(bootstrapper, "AbstractVizPrefab", prefab);
                BootstrapperTestConfiguration.Set(bootstrapper, "AbstractVizParent", root.transform);
                BootstrapperTestConfiguration.Set(bootstrapper, "IncludeSensorChannels", false);
                BootstrapperTestConfiguration.Set(bootstrapper, "SensorIds", new[] { "migration-a", "migration-b" });
                BootstrapperTestConfiguration.Set(bootstrapper, "AutoDetectSensorIds", false);
                BootstrapperTestConfiguration.Set(bootstrapper, "AbstractVizOriginOffset", new Vector3(1f, 2f, 3f));
                BootstrapperTestConfiguration.Set(bootstrapper, "AbstractVizSpacing", new Vector3(4f, 5f, 6f));
                BootstrapperTestConfiguration.Set(bootstrapper, "AbstractVizPerRow", 7);
                BootstrapperTestConfiguration.Set(bootstrapper, "AutoSpawnAbstractViz", false);
                BootstrapperTestConfiguration.Set(bootstrapper, "SpawnPosition", new Vector3(8f, 9f, 10f));
                BootstrapperTestConfiguration.Set(bootstrapper, "SpawnRotationEuler", new Vector3(11f, 12f, 13f));
                Assert.IsNotNull(PrefabUtility.SaveAsPrefabAsset(root, currentPath));

                // Import a fresh prefab containing only the former field names.
                string yaml = File.ReadAllText(currentPath);
                string[,] names =
                {
                    { "AbstractVizPrefab", "VisualPrefab" },
                    { "AbstractVizParent", "EmotionVisualParent" },
                    { "IncludeSensorChannels", "IncludeEmotionChannels" },
                    { "SensorIds", "EmotionDeviceIds" },
                    { "AutoDetectSensorIds", "AutoDetectEmotionDeviceIds" },
                    { "AbstractVizOriginOffset", "EmotionVisualOriginOffset" },
                    { "AbstractVizSpacing", "EmotionVisualSpacing" },
                    { "AbstractVizPerRow", "EmotionVisualsPerRow" },
                    { "AutoSpawnAbstractViz", "AutoSpawnAudienceVisualsFromChannels" },
                    { "SpawnPosition", "AnchorPosition" },
                    { "SpawnRotationEuler", "AnchorRotationEuler" }
                };
                for (int i = 0; i < names.GetLength(0); i++)
                {
                    string pattern = "(?m)^  " + names[i, 0] + ":";
                    Assert.AreEqual(1, Regex.Matches(yaml, pattern).Count, names[i, 0]);
                    yaml = Regex.Replace(yaml, pattern, "  " + names[i, 1] + ":");
                }
                File.WriteAllText(legacyPath, yaml);
                AssetDatabase.ImportAsset(legacyPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var importedRoot = AssetDatabase.LoadAssetAtPath<GameObject>(legacyPath);
                Assert.IsNotNull(importedRoot);
                var imported = importedRoot.GetComponent<Bootstrapper>();
                Assert.IsNotNull(imported);
                Assert.AreSame(prefab, BootstrapperTestConfiguration.Get<GameObject>(imported, "AbstractVizPrefab"));
                Assert.AreSame(importedRoot.transform, BootstrapperTestConfiguration.Get<Transform>(imported, "AbstractVizParent"));
                Assert.IsFalse(BootstrapperTestConfiguration.Get<bool>(imported, "IncludeSensorChannels"));
                CollectionAssert.AreEqual(new[] { "migration-a", "migration-b" }, BootstrapperTestConfiguration.Get<string[]>(imported, "SensorIds"));
                Assert.IsFalse(BootstrapperTestConfiguration.Get<bool>(imported, "AutoDetectSensorIds"));
                Assert.AreEqual(new Vector3(1f, 2f, 3f), BootstrapperTestConfiguration.Get<Vector3>(imported, "AbstractVizOriginOffset"));
                Assert.AreEqual(new Vector3(4f, 5f, 6f), BootstrapperTestConfiguration.Get<Vector3>(imported, "AbstractVizSpacing"));
                Assert.AreEqual(7, BootstrapperTestConfiguration.Get<int>(imported, "AbstractVizPerRow"));
                Assert.IsFalse(BootstrapperTestConfiguration.Get<bool>(imported, "AutoSpawnAbstractViz"));
                Assert.AreEqual(new Vector3(8f, 9f, 10f), BootstrapperTestConfiguration.Get<Vector3>(imported, "SpawnPosition"));
                Assert.AreEqual(new Vector3(11f, 12f, 13f), BootstrapperTestConfiguration.Get<Vector3>(imported, "SpawnRotationEuler"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                AssetDatabase.DeleteAsset(currentPath);
                AssetDatabase.DeleteAsset(legacyPath);
            }
        }
    }
}
#endif
