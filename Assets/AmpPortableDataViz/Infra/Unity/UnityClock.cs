using AmpPortableDataViz.Core;
using UnityEngine;

namespace AmpPortableDataViz.Infra
{
    public sealed class UnityClock : IClock
    {
        public long UtcNowTicks => System.DateTime.UtcNow.Ticks;
        public double SecondsSinceStartup => Time.realtimeSinceStartupAsDouble;
    }
}
