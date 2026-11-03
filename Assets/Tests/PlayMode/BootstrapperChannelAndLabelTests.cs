using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AmpPortableDataViz.Presentation.Bootstrap;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public sealed class BootstrapperChannelAndLabelTests
    {
        private GameObject _testRoot;
        private Bootstrapper _bootstrapper;

        [SetUp]
        public void SetUp()
        {
            _testRoot = new GameObject(nameof(BootstrapperChannelAndLabelTests));
            _testRoot.SetActive(false);
            _bootstrapper = _testRoot.AddComponent<Bootstrapper>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_testRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(_testRoot);
            }
        }

        [Test]
        public void ChannelDefinitions_PreserveParsedInputChannelsAndSensorOrder()
        {
            const string firstChannel = "DEVICE: Sensor-B :PHYSIO_METRICS";
            const string secondChannel = "device:sensor-a:physio_metrics";
            const string replacementChannel = "device: sensor-b :physio_metrics";
            var channels = new[]
            {
                null, string.Empty, " ", "device::physio_metrics", "device:old:valence",
                firstChannel, secondChannel, replacementChannel
            };

            object[] definitions = BuildDefinitions(channels);

            Assert.AreEqual(2, definitions.Length);
            Assert.AreEqual("Sensor-B", ReadField(definitions[0], "DeviceId"));
            Assert.AreEqual("sensor-a", ReadField(definitions[1], "DeviceId"));
            Assert.AreEqual(replacementChannel, ReadField(definitions[0], "PhysioMetricsChannel"));
            Assert.AreEqual(secondChannel, ReadField(definitions[1], "PhysioMetricsChannel"));
            foreach (object definition in definitions)
            {
                string channel = ReadField(definition, "PhysioMetricsChannel");
                Assert.IsFalse(string.IsNullOrWhiteSpace(channel));
                CollectionAssert.Contains(channels, channel);
            }
        }

        [Test]
        public void ChannelDefinitions_DoNotGenerateDefinitionsFromInvalidChannels()
        {
            Assert.IsEmpty(BuildDefinitions(Array.Empty<string>()));
            Assert.IsEmpty(BuildDefinitions(new[]
            {
                null, string.Empty, " ", "device::physio_metrics", "device: :physio_metrics",
                "device:sensor-a:valence", "device:sensor-a:physio_metrics:extra"
            }));
        }

        [TestCaseSource(nameof(LabelPositions))]
        public void GraphLabel_PreservesConfiguredPosition(
            Vector3 bounds, Vector3 padding, Vector3 offset, float verticalOffset, Vector3 expected)
        {
            _bootstrapper.GraphItemBoundsSize = bounds;
            _bootstrapper.GraphGroupBoundsPadding = padding;
            _bootstrapper.GraphGroupLabelOffset = offset;
            _bootstrapper.GraphGroupLabelVerticalOffset = verticalOffset;

            AttachLabel();

            Transform label = _testRoot.transform.Find("GraphGroupLabel");
            Assert.IsNotNull(label);
            Assert.That(label.localPosition.x, Is.EqualTo(expected.x).Within(0.00001f));
            Assert.That(label.localPosition.y, Is.EqualTo(expected.y).Within(0.00001f));
            Assert.That(label.localPosition.z, Is.EqualTo(expected.z).Within(0.00001f));
            Assert.AreEqual("sensor-a", label.GetComponent<TextMeshPro>().text);
        }

        private static IEnumerable<TestCaseData> LabelPositions()
        {
            yield return new TestCaseData(
                new Vector3(1f, 1f, 0.02f), Vector3.one * 0.05f,
                new Vector3(0f, -0.35f, 0f), 0f, new Vector3(0f, 0.2f, 0f))
                .SetName("GraphLabel_DefaultBounds");
            yield return new TestCaseData(
                new Vector3(2f, 4f, 6f), new Vector3(0.2f, 0.3f, 0.4f),
                new Vector3(1f, 2f, 3f), 0.75f, new Vector3(1f, 5.05f, 3f))
                .SetName("GraphLabel_CustomBoundsAndPadding");
            yield return new TestCaseData(
                new Vector3(2f, 4f, 6f), new Vector3(0.2f, 0.3f, 0.4f),
                new Vector3(1f, 2f, 3f), -2f, new Vector3(1f, 4.3f, 3f))
                .SetName("GraphLabel_NegativeVerticalOffsetClamped");
            yield return new TestCaseData(
                Vector3.zero, Vector3.one * 20f,
                new Vector3(1f, 2f, 3f), 0.75f, new Vector3(1f, 2.75f, 3f))
                .SetName("GraphLabel_ZeroBoundsUsesOffsetOnly");
            yield return new TestCaseData(
                new Vector3(0.0005f, 0f, 0f), Vector3.one * 20f,
                new Vector3(1f, 2f, 3f), 0.75f, new Vector3(1f, 2.75f, 3f))
                .SetName("GraphLabel_TinyBoundsUsesOffsetOnly");
            yield return new TestCaseData(
                new Vector3(0.001f, 0f, 0f), new Vector3(0.2f, 0.3f, 0.4f),
                new Vector3(1f, 2f, 3f), 0.75f, new Vector3(1f, 3.05f, 3f))
                .SetName("GraphLabel_BoundsAtThresholdUsesPadding");
            yield return new TestCaseData(
                new Vector3(2f, -4f, 6f), new Vector3(0.2f, 0.3f, 0.4f),
                new Vector3(1f, 2f, 3f), 0.75f, new Vector3(1f, 1.05f, 3f))
                .SetName("GraphLabel_SignedHeightPreserved");
        }

        private object[] BuildDefinitions(string[] channels)
        {
            var definitions = RedisStreamRegistry.BuildAudienceDeviceChannelDefinitions(channels);
            return definitions.Cast<object>().ToArray();
        }

        private void AttachLabel()
        {
            Invoke("AttachGraphGroupLabel", _testRoot, " sensor-a ", 0);
        }

        private object Invoke(string name, params object[] arguments)
        {
            MethodInfo method = typeof(Bootstrapper).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return method.Invoke(_bootstrapper, arguments);
        }

        private static string ReadField(object definition, string name)
        {
            FieldInfo field = definition.GetType().GetField(name);
            Assert.IsNotNull(field);
            return (string)field.GetValue(definition);
        }
    }
}
