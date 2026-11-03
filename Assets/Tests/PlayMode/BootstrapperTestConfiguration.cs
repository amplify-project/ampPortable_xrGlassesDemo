using System.Reflection;
using AmpPortableDataViz.Presentation.Bootstrap;
using NUnit.Framework;

namespace AmpPortableDataViz.Tests.PlayMode
{
    // Test fixtures configure the same named fields exposed by Unity's Inspector.
    internal static class BootstrapperTestConfiguration
    {
        internal static void Set<T>(Bootstrapper target, string name, T value)
        {
            Find(name).SetValue(target, value);
        }

        internal static T Get<T>(Bootstrapper target, string name)
        {
            return (T)Find(name).GetValue(target);
        }

        private static FieldInfo Find(string name)
        {
            FieldInfo field = typeof(Bootstrapper).GetField(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            return field;
        }
    }
}
