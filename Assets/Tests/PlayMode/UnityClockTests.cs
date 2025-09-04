using AmpPortableDataViz.Core;
using AmpPortableDataViz.Infra;
using NUnit.Framework;
using System;
using System.Collections;
using System.Threading;
using UnityEngine.TestTools;

namespace AmpPortableDataViz.Tests.Infra
{
    public class UnityClockTests
    {
        private static IClock MakeClock() => new UnityClock();

        [Test]
        public void UtcNowTicks_CloseTo_DateTimeUtcNow()
        {
            var before = DateTime.UtcNow;
            var clock = MakeClock();
            var ticks = clock.UtcNowTicks;
            var after = DateTime.UtcNow;

            // Convert clock ticks to DateTime for comparison.
            var clockUtc = new DateTime(ticks, DateTimeKind.Utc);

            // The reported UTC time should lie between the 'before' and 'after' bounds,
            // or within a small tolerance to allow scheduling jitter.
            var lowerOk = clockUtc >= before.AddMilliseconds(-50);
            var upperOk = clockUtc <= after.AddMilliseconds(50);
            Assert.IsTrue(lowerOk && upperOk,
                $"Clock UTC {clockUtc:o} not within expected window [{before:o}, {after:o}] (±50 ms).");
        }

        [Test]
        public void UtcNowTicks_Monotonic_WithDelay()
        {
            var clock = MakeClock();
            long t1 = clock.UtcNowTicks;
            Thread.Sleep(5);
            long t2 = clock.UtcNowTicks;
            Assert.Greater(t2, t1, "UtcNowTicks should strictly increase over time.");
        }

        [Test]
        public void SecondsSinceStartup_NonNegative()
        {
            var clock = MakeClock();
            double s = clock.SecondsSinceStartup;
            Assert.GreaterOrEqual(s, 0.0, "SecondsSinceStartup should never be negative.");
        }

        [Test]
        public void SecondsSinceStartup_Increases_AfterShortSleep()
        {
            var clock = MakeClock();
            double s1 = clock.SecondsSinceStartup;
            Thread.Sleep(30);
            double s2 = clock.SecondsSinceStartup;
            Assert.Greater(s2, s1, "SecondsSinceStartup should increase after waiting.");
        }

        [Test]
        public void SecondsSinceStartup_Advances_Approximately_WithRealTime()
        {
            var clock = MakeClock();
            Thread.Sleep(10); // small settle
            double s1 = clock.SecondsSinceStartup;

            const int sleepMs = 120;
            Thread.Sleep(sleepMs);

            double s2 = clock.SecondsSinceStartup;
            double delta = s2 - s1;

            // Allow broad tolerance for platform timer granularity and CI load.
            Assert.Greater(delta, 0.06, $"Elapsed too small: {delta:0.000}s (slept ~{sleepMs}ms).");
            Assert.Less(delta, 0.40, $"Elapsed too large: {delta:0.000}s (slept ~{sleepMs}ms).");
        }

        [Test]
        public void HighFrequency_Reads_DoNotThrow_And_Stay_NonDecreasing()
        {
            var clock = MakeClock();

            long lastTicks = clock.UtcNowTicks;
            double lastSecs = clock.SecondsSinceStartup;

            for (int i = 0; i < 10_000; i++)
            {
                long t = clock.UtcNowTicks;
                double s = clock.SecondsSinceStartup;

                // In extremely tight loops, two successive reads can be equal; require non-decreasing.
                Assert.GreaterOrEqual(t, lastTicks, "UtcNowTicks should be non-decreasing on rapid reads.");
                Assert.GreaterOrEqual(s, lastSecs - 1e-12, "SecondsSinceStartup should be non-decreasing.");

                lastTicks = t;
                lastSecs = s;
            }
        }

        // Optional PlayMode-friendly check that spans frames (works in EditMode runner too).
        [UnityTest]
        public IEnumerator SecondsSinceStartup_Increases_AcrossFrames()
        {
            var clock = MakeClock();
            double s1 = clock.SecondsSinceStartup;
            yield return null; // next frame
            double s2 = clock.SecondsSinceStartup;
            yield return null; // another frame
            double s3 = clock.SecondsSinceStartup;

            Assert.GreaterOrEqual(s2, s1, "Should not decrease across frames.");
            Assert.Greater(s3, s2, "Should increase across additional frame.");
        }
    }
}
