using System;
using System.Collections.Generic;
using System.Reflection;
using AmpPortableDataViz.Core;
using AmpPortableDataViz.Presentation.Visualization;
using NUnit.Framework;
using UnityEngine;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public class AudienceSignalVisualizerBindingSmoothingTests
    {
        private static readonly long OneSecond = TimeSpan.TicksPerSecond;
        private GameObject _gameObject;

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_gameObject);
            }
        }

        [Test]
        public void AppendGraphSample_WhenSmoothingEnabled_StoresSmoothedValue()
        {
            AudienceSignalVisualizerBinding binding = CreateBinding(true, 1f);

            Append(binding, 0f, OneSecond);
            Append(binding, 10f, 2L * OneSecond);

            List<Vector2> samples = GetSamples(binding);
            Assert.AreEqual(2, samples.Count);
            Assert.AreEqual(5f, samples[1].y, 0.0001f);
        }

        [Test]
        public void AppendGraphSample_WhenSmoothingDisabled_StoresRawValue()
        {
            AudienceSignalVisualizerBinding binding = CreateBinding(false, 1f);

            Append(binding, 0f, OneSecond);
            Append(binding, 10f, 2L * OneSecond);

            List<Vector2> samples = GetSamples(binding);
            Assert.AreEqual(10f, samples[1].y);
        }

        [Test]
        public void ResetGraphState_ClearsSmoothingHistory()
        {
            AudienceSignalVisualizerBinding binding = CreateBinding(true, 1f);
            Append(binding, 0f, OneSecond);
            Append(binding, 10f, 2L * OneSecond);

            Invoke(binding, "ResetGraphState");
            Append(binding, 3f, 3L * OneSecond);

            List<Vector2> samples = GetSamples(binding);
            Assert.AreEqual(1, samples.Count);
            Assert.AreEqual(3f, samples[0].y);
        }

        private AudienceSignalVisualizerBinding CreateBinding(bool smoothingEnabled, float halfLifeSeconds)
        {
            _gameObject = new GameObject(nameof(AudienceSignalVisualizerBindingSmoothingTests));
            _gameObject.SetActive(false);
            var binding = _gameObject.AddComponent<AudienceSignalVisualizerBinding>();

            var graphStreams = new[]
            {
                new AudienceSignalVisualizerBinding.GraphMetricBinding
                {
                    Metric = AudienceMetricKind.Engagement,
                    YRange = new Vector2(0f, 1f),
                    LineColor = Color.white,
                    LineWidth = 0.01f
                }
            };

            SetField(binding, "graphStreams", graphStreams);
            SetField(binding, "useGraphTemporalSmoothing", smoothingEnabled);
            SetField(binding, "graphSmoothingHalfLifeSeconds", halfLifeSeconds);
            Invoke(binding, "EnsureGraphState");
            return binding;
        }

        private static void Append(AudienceSignalVisualizerBinding binding, float value, long timestampTicksUtc)
        {
            Invoke(binding, "AppendGraphSample", 0, value, timestampTicksUtc);
        }

        private static List<Vector2> GetSamples(AudienceSignalVisualizerBinding binding)
        {
            var sampleLists = (List<Vector2>[])GetField(binding, "_graphSamples");
            return sampleLists[0];
        }

        private static object GetField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field {fieldName}.");
            return field.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field {fieldName}.");
            field.SetValue(target, value);
        }

        private static void Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method {methodName}.");
            method.Invoke(target, arguments);
        }
    }
}
