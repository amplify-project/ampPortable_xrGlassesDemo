using System.Collections;
using System.Reflection;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Presentation.Visualization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public class ParticleMeshVisualizerOrthogonalityTests
    {
        [UnityTest]
        public IEnumerator MainParticleSizeAndVisibility_DoNotDependOnOtherStreams()
        {
            GameObject lowEngagementObject = CreateVisualizer("low-engagement");
            GameObject highEngagementObject = CreateVisualizer("high-engagement");

            try
            {
                var lowEngagementVisualizer = lowEngagementObject.GetComponent<ParticleMeshVisualizer>();
                var highEngagementVisualizer = highEngagementObject.GetComponent<ParticleMeshVisualizer>();

                lowEngagementVisualizer.Apply(
                    new ParticleMeshSignalSample("device-a", -1f, -1f, -1f, 0.6f, 0f),
                    1L);
                highEngagementVisualizer.Apply(
                    new ParticleMeshSignalSample("device-b", 1f, 1f, 1f, 0.6f, 1f),
                    1L);

                yield return null;
                yield return null;

                ParticleSystem.Particle lowParticle = ReadFirstParticle(lowEngagementObject);
                ParticleSystem.Particle highParticle = ReadFirstParticle(highEngagementObject);

                Assert.AreEqual(lowParticle.startSize, highParticle.startSize, 0.0001f);
                Assert.GreaterOrEqual(lowParticle.startColor.a, 183);
                Assert.GreaterOrEqual(highParticle.startColor.a, 183);
            }
            finally
            {
                Object.Destroy(lowEngagementObject);
                Object.Destroy(highEngagementObject);
            }
        }

        [UnityTest]
        public IEnumerator GlobalHalo_KeepsNegativeInsideAndPositiveOutsideNeutralReference()
        {
            GameObject negativeObject = CreateVisualizer("negative-heart-rate");
            GameObject positiveObject = CreateVisualizer("positive-heart-rate");

            try
            {
                negativeObject.GetComponent<ParticleMeshVisualizer>().Apply(
                    new ParticleMeshSignalSample("device-a", 0f, 0f, 0f, -1f, 0f),
                    1L);
                positiveObject.GetComponent<ParticleMeshVisualizer>().Apply(
                    new ParticleMeshSignalSample("device-b", 0f, 0f, 0f, 1f, 1f),
                    1L);

                yield return null;
                yield return null;

                float negativeReferenceRadius = ReadHaloRadius(negativeObject, "Heart Rate Neutral Reference");
                float negativeActiveRadius = ReadHaloRadius(negativeObject, "Heart Rate Active Halo");
                float positiveReferenceRadius = ReadHaloRadius(positiveObject, "Heart Rate Neutral Reference");
                float positiveActiveRadius = ReadHaloRadius(positiveObject, "Heart Rate Active Halo");

                Assert.Less(negativeActiveRadius, negativeReferenceRadius);
                Assert.Greater(positiveActiveRadius, positiveReferenceRadius);
            }
            finally
            {
                Object.Destroy(negativeObject);
                Object.Destroy(positiveObject);
            }
        }

        [UnityTest]
        public IEnumerator GlobalHalo_WhenHeartDataBecomesStale_FadesActiveRing()
        {
            GameObject visualizerObject = CreateVisualizer("stale-heart-rate");

            try
            {
                var visualizer = visualizerObject.GetComponent<ParticleMeshVisualizer>();
                SetPrivateField(visualizer, "heartRateStaleAfterSeconds", 0.05f);
                SetPrivateField(visualizer, "heartRateStaleFadeSeconds", 0.02f);
                visualizer.Apply(
                    new ParticleMeshSignalSample("device-a", 0f, 0f, 0f, 1f, 0.5f),
                    1L);

                yield return null;

                LineRenderer activeHalo = FindHaloLine(visualizerObject, "Heart Rate Active Halo");
                Assert.Greater(activeHalo.startColor.a, 0f);

                yield return new WaitForSeconds(0.1f);

                Assert.AreEqual(0f, activeHalo.startColor.a, 0.01f);
            }
            finally
            {
                Object.Destroy(visualizerObject);
            }
        }

        [UnityTest]
        public IEnumerator GlobalHalo_WhenPhysiologyTimestampIsMissing_DoesNotShowActiveRing()
        {
            GameObject visualizerObject = CreateVisualizer("missing-heart-rate");

            try
            {
                visualizerObject.GetComponent<ParticleMeshVisualizer>().Apply(
                    new ParticleMeshSignalSample("device-a", 0f, 0f, 0f, 1f, 0.5f),
                    0L);

                yield return null;

                LineRenderer activeHalo = FindHaloLine(visualizerObject, "Heart Rate Active Halo");
                Assert.AreEqual(0f, activeHalo.startColor.a, 0.01f);
            }
            finally
            {
                Object.Destroy(visualizerObject);
            }
        }

        private static GameObject CreateVisualizer(string objectName)
        {
            var visualizerObject = new GameObject(objectName);
            visualizerObject.AddComponent<ParticleMeshVisualizer>();
            return visualizerObject;
        }

        private static ParticleSystem.Particle ReadFirstParticle(GameObject visualizerObject)
        {
            ParticleSystem particleSystem = visualizerObject.GetComponent<ParticleSystem>();
            Assert.Greater(particleSystem.particleCount, 0);

            var particles = new ParticleSystem.Particle[particleSystem.particleCount];
            int count = particleSystem.GetParticles(particles);
            Assert.Greater(count, 0);
            return particles[0];
        }

        private static float ReadHaloRadius(GameObject visualizerObject, string lineName)
        {
            LineRenderer line = FindHaloLine(visualizerObject, lineName);
            Vector3 firstPosition = line.GetPosition(0);
            return new Vector2(firstPosition.x, firstPosition.y).magnitude;
        }

        private static LineRenderer FindHaloLine(GameObject visualizerObject, string lineName)
        {
            LineRenderer[] lines = visualizerObject.GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].name == lineName)
                {
                    return lines[i];
                }
            }

            Assert.Fail($"Could not find halo line '{lineName}'.");
            return null;
        }

        private static void SetPrivateField<T>(ParticleMeshVisualizer visualizer, string fieldName, T value)
        {
            FieldInfo field = typeof(ParticleMeshVisualizer).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Could not find field '{fieldName}'.");
            field.SetValue(visualizer, value);
        }
    }
}
