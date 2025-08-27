using System;
using System.Diagnostics;
using AmpPortableDataViz.Core;

namespace AmpPortableDataViz.Infra
{
    public sealed class SystemClock : IClock
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        public long UtcNowTicks => DateTime.UtcNow.Ticks;
        public double SecondsSinceStartup => _stopwatch.Elapsed.TotalSeconds;
    }
}
