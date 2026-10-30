using System.Collections.Generic;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Presentation.Sources;
using NUnit.Framework;
using UnityEngine;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public sealed class RedisEndpointContractsTests
    {
        [Test]
        public void MetadataValidator_WhenRequiredRecordsMatch_AcceptsEndpoint()
        {
            var endpoint = new RedisServiceEndpoint(
                "192.168.1.20",
                6379,
                "Amplify Redis",
                new Dictionary<string, string>
                {
                    ["app"] = "amplify",
                    ["schema"] = "1"
                });

            var requirements = new[]
            {
                new RedisTxtRecordRequirement("APP", "AMPLIFY"),
                new RedisTxtRecordRequirement("schema", "1")
            };

            Assert.IsTrue(RedisServiceMetadataValidator.IsCompatible(endpoint, requirements));
        }

        [Test]
        public void MetadataValidator_WhenRequiredRecordIsMissing_RejectsEndpoint()
        {
            var endpoint = new RedisServiceEndpoint("192.168.1.20", 6379);
            var requirements = new[] { new RedisTxtRecordRequirement("schema", "1") };

            Assert.IsFalse(RedisServiceMetadataValidator.IsCompatible(endpoint, requirements));
        }

        [Test]
        public void RuntimeSettings_DiscoveredEndpoint_RoundTripsMetadata()
        {
            var settings = RedisRuntimeSettings.Create();
            try
            {
                settings.ApplyDiscovered(new RedisServiceEndpoint(
                    "redis-host.local",
                    6380,
                    "Amplify Redis",
                    new Dictionary<string, string> { ["schema"] = "1" }));

                var restored = settings.ToEndpoint();

                Assert.AreEqual("redis-host.local", restored.Host);
                Assert.AreEqual(6380, restored.Port);
                Assert.AreEqual("Amplify Redis", restored.ServiceName);
                Assert.AreEqual("1", restored.TxtRecords["schema"]);
                Assert.IsTrue(settings.WasDiscovered);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }
    }
}
