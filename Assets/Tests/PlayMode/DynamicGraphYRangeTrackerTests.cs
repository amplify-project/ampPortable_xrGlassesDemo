using System;
using System.Collections.Generic;
using AmpPortableDataViz.Presentation.Mapping;
using NUnit.Framework;
using UnityEngine;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public class DynamicGraphYRangeTrackerTests
    {
        private const float HardMin = -3f;
        private const float HardMax = 3f;
        private static readonly long OneSecond = TimeSpan.TicksPerSecond;

        [Test]
        public void Resolve_WhenVariationIsSmall_ZoomsInAroundObservedValues()
        {
            var tracker = new DynamicGraphYRangeTracker();
            var samples = Samples(0.48f, 0.5f, 0.52f);

            Resolve(tracker, samples, OneSecond, out float yMin, out float yMax);

            Assert.Less(yMax - yMin, HardMax - HardMin);
            Assert.LessOrEqual(yMin, 0.48f);
            Assert.GreaterOrEqual(yMax, 0.52f);
        }

        [Test]
        public void Resolve_WhenVariationSuddenlyIncreases_ExpandsWithoutClippingExtremes()
        {
            var tracker = new DynamicGraphYRangeTracker();
            Resolve(tracker, Samples(0.48f, 0.5f, 0.52f), OneSecond, out float quietMin, out float quietMax);

            Resolve(tracker, Samples(-2.25f, 0f, 2.4f), 2L * OneSecond, out float yMin, out float yMax);

            Assert.Less(yMin, quietMin);
            Assert.Greater(yMax, quietMax);
            Assert.LessOrEqual(yMin, -2.25f);
            Assert.GreaterOrEqual(yMax, 2.4f);
        }

        [Test]
        public void Resolve_WhenVariationDecreases_ContractsGradually()
        {
            var tracker = new DynamicGraphYRangeTracker();
            Resolve(tracker, Samples(-2f, 0f, 2f), OneSecond, out float wideMin, out float wideMax);

            Resolve(tracker, Samples(0.45f, 0.5f, 0.55f), OneSecond + TimeSpan.TicksPerMillisecond * 100L, out float yMin, out float yMax);

            Assert.Less(yMax - yMin, wideMax - wideMin);
            Assert.Greater(yMax - yMin, 0.5f);
            Assert.LessOrEqual(yMin, 0.45f);
            Assert.GreaterOrEqual(yMax, 0.55f);
        }

        [Test]
        public void Resolve_WhenValuesApproachHardLimit_KeepsRangeInsideHardBounds()
        {
            var tracker = new DynamicGraphYRangeTracker();

            Resolve(tracker, Samples(2.8f, 2.9f, 3f), OneSecond, out float yMin, out float yMax);

            Assert.GreaterOrEqual(yMin, HardMin);
            Assert.LessOrEqual(yMax, HardMax);
            Assert.GreaterOrEqual(yMax, 3f);
        }

        [Test]
        public void Reset_DiscardsPreviousRange()
        {
            var tracker = new DynamicGraphYRangeTracker();
            Resolve(tracker, Samples(-2f, 0f, 2f), OneSecond, out _, out _);

            tracker.Reset();
            Resolve(tracker, Samples(0.48f, 0.5f, 0.52f), 2L * OneSecond, out float yMin, out float yMax);

            Assert.Less(yMax - yMin, 1f);
        }

        private static void Resolve(
            DynamicGraphYRangeTracker tracker,
            IReadOnlyList<Vector2> samples,
            long timestampTicksUtc,
            out float yMin,
            out float yMax)
        {
            tracker.Resolve(
                samples,
                HardMin,
                HardMax,
                2.5f,
                0.05f,
                0.15f,
                0.1f,
                2f,
                timestampTicksUtc,
                out yMin,
                out yMax);
        }

        private static IReadOnlyList<Vector2> Samples(params float[] values)
        {
            var samples = new List<Vector2>(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                samples.Add(new Vector2(i, values[i]));
            }

            return samples;
        }
    }
}
