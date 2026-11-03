using System.Collections;
using System.Reflection;
using AmpPortableDataViz.Infra;
using AmpPortableDataViz.Presentation.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public sealed class RedisStreamRegistryTests
    {
        private GameObject _root;
        private RedisStreamRegistry _streams;
        private RedisStreamRegistry.Settings _settings;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject(nameof(RedisStreamRegistryTests));
            _root.SetActive(false);
            _streams = new RedisStreamRegistry(_root.transform);
            _settings = new RedisStreamRegistry.Settings
            {
                RedisHost = "127.0.0.1",
                RedisPort = 6379,
                SensorEngagementTimeoutSeconds = 10f,
                SensorEngagementNeutralValue = 0.5f,
                SensorEngagementPayloadFormat = SensorEngagementJsonFormat.CreateDefault()
            };
            _streams.Configure(_settings);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [UnityTest]
        public IEnumerator SharedPump_SurvivesUntilItsLastConsumerReleasesIt()
        {
            const string channel = "device:a:physio_metrics";
            var first = _streams.AcquirePhysioPumpForChannel("a", channel);
            var second = _streams.AcquirePhysioPumpForChannel("a", channel);
            Assert.AreSame(first, second);
            Assert.AreSame(_root.transform, first.transform.parent);
            _streams.ReleasePhysioPump(channel);
            yield return null;
            Assert.IsTrue(first != null);
            _streams.ReleasePhysioPump(channel);
            yield return null;
            Assert.IsTrue(first == null);
            Assert.DoesNotThrow(() => _streams.ReleasePhysioPump(channel));
        }

        [Test]
        public void RawChannelSpelling_RemainsPartOfPumpIdentity()
        {
            var lower = _streams.AcquirePhysioPumpForChannel("a", "device:a:physio_metrics");
            var upper = _streams.AcquirePhysioPumpForChannel("a", "DEVICE:a:physio_metrics");
            Assert.AreNotSame(lower, upper);
            Assert.IsNull(_streams.AcquirePhysioPumpForChannel("a", " "));
        }

        [Test]
        public void EndpointChange_ReconfiguresTheSharedPump()
        {
            const string channel = "device:a:physio_metrics";
            var first = _streams.AcquirePhysioPumpForChannel("a", channel);
            _settings.RedisHost = "192.0.2.10";
            _settings.RedisPort = 6380;
            _streams.Configure(_settings);
            var second = _streams.AcquirePhysioPumpForChannel("a", channel);
            Assert.AreSame(first, second);
            Assert.AreEqual("192.0.2.10", ReadField(second, "redisHost"));
            Assert.AreEqual(6380, ReadField(second, "redisPort"));
        }

        [UnityTest]
        public IEnumerator EngagementBackend_ReusesUnchangedConfigurationAndReplacesChangedConfiguration()
        {
            _streams.EnsureSensorEngagementBackend(new[] { " b ", "a", "A" });
            var first = _streams.ResolveSensorEngagementSource("a");
            Assert.IsNotNull(first);
            Assert.AreEqual(0.5f, first.LatestFrame.Payload.Engagement);
            _streams.EnsureSensorEngagementBackend(new[] { "a", "b" });
            Assert.AreSame(first, _streams.ResolveSensorEngagementSource("a"));
            _settings.SensorEngagementNeutralValue = 0.25f;
            _streams.Configure(_settings);
            _streams.EnsureSensorEngagementBackend(new[] { "a", "b" });
            var replacement = _streams.ResolveSensorEngagementSource("a");
            Assert.AreNotSame(first, replacement);
            Assert.AreEqual(0.25f, replacement.LatestFrame.Payload.Engagement);
            _streams.ClearSensorEngagementBackend();
            _streams.ClearSensorEngagementBackend();
            yield return null;
            Assert.IsNull(_streams.ResolveSensorEngagementSource("a"));
            Assert.AreEqual(0, _root.transform.childCount);
        }

        private static object ReadField(object target, string name)
        {
            return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }
    }
}
