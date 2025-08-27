using AmpPortableDataViz.Core;

namespace AmpPortableDataViz.Tests
{
    public sealed class TestClock : IClock
    {
        public long UtcNowTicks { get; private set; }
        public double SecondsSinceStartup { get; private set; }

        public void Advance(double secondsToAdvance)
        {
            SecondsSinceStartup += secondsToAdvance;
            UtcNowTicks += (long)(secondsToAdvance * System.TimeSpan.TicksPerSecond);
        }

        public void Set(long utcTicks, double secondsSinceStartup)
        {
            UtcNowTicks = utcTicks;
            SecondsSinceStartup = secondsSinceStartup;
        }
    }
}
