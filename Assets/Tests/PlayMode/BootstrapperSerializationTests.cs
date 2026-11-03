#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using AmpPortableDataViz.Presentation.Bootstrap;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public sealed class BootstrapperSerializationTests
    {
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
                bootstrapper.AbstractVizPrefab = prefab;
                bootstrapper.AbstractVizParent = root.transform;
                bootstrapper.IncludeSensorChannels = false;
                bootstrapper.SensorIds = new[] { "migration-a", "migration-b" };
                bootstrapper.AutoDetectSensorIds = false;
                bootstrapper.AbstractVizOriginOffset = new Vector3(1f, 2f, 3f);
                bootstrapper.AbstractVizSpacing = new Vector3(4f, 5f, 6f);
                bootstrapper.AbstractVizPerRow = 7;
                bootstrapper.AutoSpawnAbstractViz = false;
                bootstrapper.SpawnPosition = new Vector3(8f, 9f, 10f);
                bootstrapper.SpawnRotationEuler = new Vector3(11f, 12f, 13f);
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
                Assert.AreSame(prefab, imported.AbstractVizPrefab);
                Assert.AreSame(importedRoot.transform, imported.AbstractVizParent);
                Assert.IsFalse(imported.IncludeSensorChannels);
                CollectionAssert.AreEqual(new[] { "migration-a", "migration-b" }, imported.SensorIds);
                Assert.IsFalse(imported.AutoDetectSensorIds);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), imported.AbstractVizOriginOffset);
                Assert.AreEqual(new Vector3(4f, 5f, 6f), imported.AbstractVizSpacing);
                Assert.AreEqual(7, imported.AbstractVizPerRow);
                Assert.IsFalse(imported.AutoSpawnAbstractViz);
                Assert.AreEqual(new Vector3(8f, 9f, 10f), imported.SpawnPosition);
                Assert.AreEqual(new Vector3(11f, 12f, 13f), imported.SpawnRotationEuler);
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
