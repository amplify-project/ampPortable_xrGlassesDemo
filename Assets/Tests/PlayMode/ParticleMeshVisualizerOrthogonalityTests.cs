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
        [TestCase(5, 4, 0, 4, 15, 19)]
        [TestCase(10, 10, 0, 9, 90, 99)]
        public void CornerIndices_ResolveAllFourGridCorners(
            int width,
            int height,
            int expectedBottomLeft,
            int expectedBottomRight,
            int expectedTopLeft,
            int expectedTopRight)
        {
            ParticleMeshCornerIndices corners = ParticleMeshCornerIndexMapper.Resolve(width, height);

            Assert.AreEqual(expectedBottomLeft, corners.BottomLeft);
            Assert.AreEqual(expectedBottomRight, corners.BottomRight);
            Assert.AreEqual(expectedTopLeft, corners.TopLeft);
            Assert.AreEqual(expectedTopRight, corners.TopRight);
        }

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
        public IEnumerator CornerHalos_AreCreatedAtRenderedCornerParticles()
        {
            GameObject visualizerObject = CreateVisualizer("corner-heart-rate");

            try
            {
                var visualizer = visualizerObject.GetComponent<ParticleMeshVisualizer>();
                SetPrivateField(visualizer, "heartRateCornerHaloCenterSmoothTime", 0f);
                visualizer.Apply(
                    new ParticleMeshSignalSample("device-a", 0f, 0f, 0f, 1f, 0f),
                    1L);

                yield return null;
                yield return null;

                Vector3[] renderedVertices = ReadPrivateField<Vector3[]>(visualizer, "_vertices");
                int builtWidth = ReadPrivateField<int>(visualizer, "_builtGridWidth");
                int builtHeight = ReadPrivateField<int>(visualizer, "_builtGridHeight");
                Assert.AreEqual(builtWidth * builtHeight, renderedVertices.Length);

                ParticleMeshCornerIndices corners = ParticleMeshCornerIndexMapper.Resolve(builtWidth, builtHeight);
                AssertCornerHaloCenter(visualizerObject, "Heart Rate Corner Bottom Left", renderedVertices[corners.BottomLeft]);
                AssertCornerHaloCenter(visualizerObject, "Heart Rate Corner Bottom Right", renderedVertices[corners.BottomRight]);
                AssertCornerHaloCenter(visualizerObject, "Heart Rate Corner Top Left", renderedVertices[corners.TopLeft]);
                AssertCornerHaloCenter(visualizerObject, "Heart Rate Corner Top Right", renderedVertices[corners.TopRight]);

                Assert.AreEqual(4, CountHaloLines(visualizerObject, "Heart Rate Neutral Reference"));
                Assert.AreEqual(4, CountHaloLines(visualizerObject, "Heart Rate Active Halo"));
            }
            finally
            {
                Object.Destroy(visualizerObject);
            }
        }

        [UnityTest]
        public IEnumerator CornerHalos_RenderOpenUpperSemicircles()
        {
            GameObject visualizerObject = CreateVisualizer("semicircle-heart-rate");

            try
            {
                visualizerObject.GetComponent<ParticleMeshVisualizer>().Apply(
                    new ParticleMeshSignalSample("device-a", 0f, 0f, 0f, 1f, 0.5f),
                    1L);

                yield return null;

                LineRenderer[] referenceHalos = FindHaloLines(visualizerObject, "Heart Rate Neutral Reference");
                LineRenderer[] activeHalos = FindHaloLines(visualizerObject, "Heart Rate Active Halo");
                for (int i = 0; i < ParticleMeshCornerIndexMapper.CornerCount; i++)
                {
                    AssertUpperSemicircle(referenceHalos[i]);
                    AssertUpperSemicircle(activeHalos[i]);
                }
            }
            finally
            {
                Object.Destroy(visualizerObject);
            }
        }

        [UnityTest]
        public IEnumerator CornerHalos_StaySynchronizedWithNegativeInsideAndPositiveOutside()
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

                LineRenderer[] negativeReferences = FindHaloLines(negativeObject, "Heart Rate Neutral Reference");
                LineRenderer[] negativeActives = FindHaloLines(negativeObject, "Heart Rate Active Halo");
                LineRenderer[] positiveReferences = FindHaloLines(positiveObject, "Heart Rate Neutral Reference");
                LineRenderer[] positiveActives = FindHaloLines(positiveObject, "Heart Rate Active Halo");

                float negativeReferenceRadius = ReadHaloRadius(negativeReferences[0]);
                float negativeActiveRadius = ReadHaloRadius(negativeActives[0]);
                float positiveReferenceRadius = ReadHaloRadius(positiveReferences[0]);
                float positiveActiveRadius = ReadHaloRadius(positiveActives[0]);
                Assert.Less(negativeActiveRadius, negativeReferenceRadius);
                Assert.Greater(positiveActiveRadius, positiveReferenceRadius);

                for (int i = 1; i < ParticleMeshCornerIndexMapper.CornerCount; i++)
                {
                    Assert.AreEqual(negativeReferenceRadius, ReadHaloRadius(negativeReferences[i]), 0.0001f);
                    Assert.AreEqual(negativeActiveRadius, ReadHaloRadius(negativeActives[i]), 0.0001f);
                    Assert.AreEqual(positiveReferenceRadius, ReadHaloRadius(positiveReferences[i]), 0.0001f);
                    Assert.AreEqual(positiveActiveRadius, ReadHaloRadius(positiveActives[i]), 0.0001f);
                }
            }
            finally
            {
                Object.Destroy(negativeObject);
                Object.Destroy(positiveObject);
            }
        }

        [UnityTest]
        public IEnumerator CornerHalos_WhenHeartDataBecomesStale_FadeAllActiveRings()
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

                LineRenderer[] activeHalos = FindHaloLines(visualizerObject, "Heart Rate Active Halo");
                for (int i = 0; i < activeHalos.Length; i++)
                {
                    Assert.Greater(activeHalos[i].startColor.a, 0f);
                }

                yield return new WaitForSeconds(0.1f);

                for (int i = 0; i < activeHalos.Length; i++)
                {
                    Assert.AreEqual(0f, activeHalos[i].startColor.a, 0.01f);
                }
            }
            finally
            {
                Object.Destroy(visualizerObject);
            }
        }

        [UnityTest]
        public IEnumerator CornerHalos_WhenPhysiologyTimestampIsMissing_HideAllActiveRings()
        {
            GameObject visualizerObject = CreateVisualizer("missing-heart-rate");

            try
            {
                visualizerObject.GetComponent<ParticleMeshVisualizer>().Apply(
                    new ParticleMeshSignalSample("device-a", 0f, 0f, 0f, 1f, 0.5f),
                    0L);

                yield return null;

                LineRenderer[] activeHalos = FindHaloLines(visualizerObject, "Heart Rate Active Halo");
                for (int i = 0; i < activeHalos.Length; i++)
                {
                    Assert.AreEqual(0f, activeHalos[i].startColor.a, 0.01f);
                }
            }
            finally
            {
                Object.Destroy(visualizerObject);
            }
        }

        [UnityTest]
        public IEnumerator CornerHalos_WhenGridDimensionsChange_RetargetNewCorners()
        {
            GameObject visualizerObject = CreateVisualizer("resized-corner-heart-rate");

            try
            {
                var visualizer = visualizerObject.GetComponent<ParticleMeshVisualizer>();
                SetPrivateField(visualizer, "heartRateCornerHaloCenterSmoothTime", 0f);
                SetPrivateField(visualizer, "gridWidth", 5);
                SetPrivateField(visualizer, "gridHeight", 4);
                SetPrivateField(visualizer, "_needsRebuild", true);

                yield return null;
                yield return null;

                Vector3[] renderedVertices = ReadPrivateField<Vector3[]>(visualizer, "_vertices");
                ParticleMeshCornerIndices corners = ParticleMeshCornerIndexMapper.Resolve(5, 4);
                Assert.AreEqual(20, renderedVertices.Length);
                AssertCornerHaloCenter(visualizerObject, "Heart Rate Corner Bottom Left", renderedVertices[corners.BottomLeft]);
                AssertCornerHaloCenter(visualizerObject, "Heart Rate Corner Bottom Right", renderedVertices[corners.BottomRight]);
                AssertCornerHaloCenter(visualizerObject, "Heart Rate Corner Top Left", renderedVertices[corners.TopLeft]);
                AssertCornerHaloCenter(visualizerObject, "Heart Rate Corner Top Right", renderedVertices[corners.TopRight]);
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

        private static float ReadHaloRadius(LineRenderer line)
        {
            Vector3 firstPosition = line.GetPosition(0);
            return new Vector2(firstPosition.x, firstPosition.y).magnitude;
        }

        private static void AssertUpperSemicircle(LineRenderer line)
        {
            Assert.IsFalse(line.loop);
            Assert.GreaterOrEqual(line.positionCount, 3);
            Assert.Greater(line.numCapVertices, 0);

            Vector3 first = line.GetPosition(0);
            Vector3 last = line.GetPosition(line.positionCount - 1);
            Assert.Greater(first.x, 0f);
            Assert.AreEqual(0f, first.y, 0.0001f);
            Assert.Less(last.x, 0f);
            Assert.AreEqual(0f, last.y, 0.0001f);

            bool hasPointAboveCenter = false;
            for (int i = 0; i < line.positionCount; i++)
            {
                Vector3 point = line.GetPosition(i);
                Assert.GreaterOrEqual(point.y, -0.0001f);
                hasPointAboveCenter |= point.y > 0.0001f;
            }

            Assert.IsTrue(hasPointAboveCenter);
        }

        private static void AssertCornerHaloCenter(
            GameObject visualizerObject,
            string cornerName,
            Vector3 expectedLocalPosition)
        {
            Transform corner = visualizerObject.transform.Find($"Heart Rate Corner Halos/{cornerName}");
            Assert.IsNotNull(corner, $"Could not find corner halo '{cornerName}'.");
            Vector3 expectedWorldPosition = visualizerObject.transform.TransformPoint(expectedLocalPosition);
            Assert.AreEqual(expectedWorldPosition.x, corner.position.x, 0.0001f);
            Assert.AreEqual(expectedWorldPosition.y, corner.position.y, 0.0001f);
            Assert.AreEqual(expectedWorldPosition.z, corner.position.z, 0.0001f);
        }

        private static int CountHaloLines(GameObject visualizerObject, string lineName)
        {
            int count = 0;
            LineRenderer[] lines = visualizerObject.GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].name == lineName)
                {
                    count++;
                }
            }

            return count;
        }

        private static LineRenderer[] FindHaloLines(GameObject visualizerObject, string lineName)
        {
            LineRenderer[] allLines = visualizerObject.GetComponentsInChildren<LineRenderer>(true);
            var matches = new LineRenderer[ParticleMeshCornerIndexMapper.CornerCount];
            int matchCount = 0;
            for (int i = 0; i < allLines.Length; i++)
            {
                if (allLines[i].name != lineName)
                {
                    continue;
                }

                Assert.Less(matchCount, matches.Length, $"Found too many halo lines named '{lineName}'.");
                matches[matchCount++] = allLines[i];
            }

            Assert.AreEqual(matches.Length, matchCount, $"Expected four halo lines named '{lineName}'.");
            return matches;
        }

        private static void SetPrivateField<T>(ParticleMeshVisualizer visualizer, string fieldName, T value)
        {
            FieldInfo field = typeof(ParticleMeshVisualizer).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Could not find field '{fieldName}'.");
            field.SetValue(visualizer, value);
        }

        private static T ReadPrivateField<T>(ParticleMeshVisualizer visualizer, string fieldName)
        {
            FieldInfo field = typeof(ParticleMeshVisualizer).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Could not find field '{fieldName}'.");
            return (T)field.GetValue(visualizer);
        }
    }
}
