using System;
using AmpPortableDataViz.Presentation.Mapping;
using NUnit.Framework;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public class GraphTemporalSmootherTests
    {
        private static readonly long OneSecond = TimeSpan.TicksPerSecond;

        [Test]
        public void Apply_FirstValue_PassesThroughWithoutStartupRamp()
        {
            var smoother = new GraphTemporalSmoother();

            float result = smoother.Apply(2.5f, OneSecond, 0.2f);

            Assert.AreEqual(2.5f, result);
        }

        [Test]
        public void Apply_AfterOneHalfLife_MovesHalfwayToNewValue()
        {
            var smoother = new GraphTemporalSmoother();
            smoother.Apply(0f, OneSecond, 1f);

            float result = smoother.Apply(10f, 2L * OneSecond, 1f);

            Assert.AreEqual(5f, result, 0.0001f);
        }

        [Test]
        public void Apply_IrregularIntervals_UsesElapsedTime()
        {
            var smoother = new GraphTemporalSmoother();
            smoother.Apply(0f, OneSecond, 1f);
            smoother.Apply(10f, OneSecond + OneSecond / 4L, 1f);

            float result = smoother.Apply(10f, 2L * OneSecond, 1f);

            Assert.AreEqual(5f, result, 0.0001f);
        }

        [Test]
        public void Apply_ZeroHalfLife_BypassesSmoothingAndSeedsLatestValue()
        {
            var smoother = new GraphTemporalSmoother();
            smoother.Apply(0f, OneSecond, 1f);
            float bypassed = smoother.Apply(8f, 2L * OneSecond, 0f);

            float resumed = smoother.Apply(10f, 3L * OneSecond, 1f);

            Assert.AreEqual(8f, bypassed);
            Assert.AreEqual(9f, resumed, 0.0001f);
        }

        [Test]
        public void Reset_DiscardsPreviousValue()
        {
            var smoother = new GraphTemporalSmoother();
            smoother.Apply(0f, OneSecond, 1f);
            smoother.Apply(10f, 2L * OneSecond, 1f);

            smoother.Reset();
            float result = smoother.Apply(3f, 3L * OneSecond, 1f);

            Assert.AreEqual(3f, result);
        }

        [Test]
        public void Apply_NonIncreasingTimestamp_HoldsCurrentValue()
        {
            var smoother = new GraphTemporalSmoother();
            smoother.Apply(2f, 2L * OneSecond, 1f);

            float result = smoother.Apply(10f, OneSecond, 1f);

            Assert.AreEqual(2f, result);
        }
    }
}
