using AmpPortableDataViz.Presentation.Mapping;
using NUnit.Framework;
using UnityEngine;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public class HeartRateVisualResponseTests
    {
        [Test]
        public void Resolve_WithinDeadBand_ReturnsNeutralResponse()
        {
            HeartRateVisualSettings settings = HeartRateVisualSettings.CreateDefault();

            HeartRateVisualResponse response = HeartRateVisualResponseMapper.Resolve(settings.DeadBand * 0.5f, settings);

            Assert.IsTrue(response.IsNeutral);
            Assert.AreEqual(1f, response.ParticleScale);
            Assert.AreEqual(settings.NeutralPulseBpm, response.PulseBpm);
            Assert.AreEqual(1f, response.HaloRadiusScale);
        }

        [Test]
        public void Resolve_AcrossSignedRange_ProducesMonotonicParticleSizeAndTempo()
        {
            HeartRateVisualSettings settings = HeartRateVisualSettings.CreateDefault();

            HeartRateVisualResponse negative = HeartRateVisualResponseMapper.Resolve(-1f, settings);
            HeartRateVisualResponse neutral = HeartRateVisualResponseMapper.Resolve(0f, settings);
            HeartRateVisualResponse positive = HeartRateVisualResponseMapper.Resolve(1f, settings);

            Assert.Less(negative.ParticleScale, neutral.ParticleScale);
            Assert.Less(neutral.ParticleScale, positive.ParticleScale);
            Assert.Less(negative.PulseBpm, neutral.PulseBpm);
            Assert.Less(neutral.PulseBpm, positive.PulseBpm);
        }

        [Test]
        public void Resolve_AcrossSignedRange_PlacesHaloInsideOrOutsideNeutralReference()
        {
            HeartRateVisualSettings settings = HeartRateVisualSettings.CreateDefault();

            HeartRateVisualResponse negative = HeartRateVisualResponseMapper.Resolve(-1f, settings);
            HeartRateVisualResponse neutral = HeartRateVisualResponseMapper.Resolve(0f, settings);
            HeartRateVisualResponse positive = HeartRateVisualResponseMapper.Resolve(1f, settings);

            Assert.Less(negative.HaloRadiusScale, 1f);
            Assert.AreEqual(1f, neutral.HaloRadiusScale);
            Assert.Greater(positive.HaloRadiusScale, 1f);
            Assert.AreEqual(-1f, negative.Direction);
            Assert.AreEqual(1f, positive.Direction);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Resolve_WhenInputIsNotFinite_ReturnsNeutralResponse(float value)
        {
            HeartRateVisualResponse response = HeartRateVisualResponseMapper.Resolve(
                value,
                HeartRateVisualSettings.CreateDefault());

            Assert.IsTrue(response.IsNeutral);
        }

        [Test]
        public void PulseEnvelope_AtPulsePeak_ExpandsPositiveAndContractsNegative()
        {
            HeartRateVisualSettings settings = HeartRateVisualSettings.CreateDefault();
            HeartRateVisualResponse negative = HeartRateVisualResponseMapper.Resolve(-1f, settings);
            HeartRateVisualResponse positive = HeartRateVisualResponseMapper.Resolve(1f, settings);

            HeartRatePulseEnvelope negativePulse = HeartRatePulseEnvelopeMapper.Resolve(0.175f, in negative, settings);
            HeartRatePulseEnvelope positivePulse = HeartRatePulseEnvelopeMapper.Resolve(0.175f, in positive, settings);

            Assert.AreEqual(1f, negativePulse.Energy, 0.0001f);
            Assert.Less(negativePulse.Scale, 1f);
            Assert.Greater(positivePulse.Scale, 1f);
        }

        [Test]
        public void PulseEnvelope_DuringRest_ReturnsUnmodifiedScale()
        {
            HeartRateVisualSettings settings = HeartRateVisualSettings.CreateDefault();
            HeartRateVisualResponse positive = HeartRateVisualResponseMapper.Resolve(1f, settings);

            HeartRatePulseEnvelope pulse = HeartRatePulseEnvelopeMapper.Resolve(0.75f, in positive, settings);

            Assert.AreEqual(0f, pulse.Energy);
            Assert.AreEqual(1f, pulse.Scale);
        }

        [Test]
        public void PulseEnvelope_WrapsPhaseWithoutChangingResult()
        {
            HeartRateVisualSettings settings = HeartRateVisualSettings.CreateDefault();
            HeartRateVisualResponse positive = HeartRateVisualResponseMapper.Resolve(1f, settings);

            HeartRatePulseEnvelope firstCycle = HeartRatePulseEnvelopeMapper.Resolve(0.1f, in positive, settings);
            HeartRatePulseEnvelope secondCycle = HeartRatePulseEnvelopeMapper.Resolve(1.1f, in positive, settings);

            Assert.AreEqual(firstCycle.Energy, secondCycle.Energy, 0.0001f);
            Assert.AreEqual(firstCycle.Scale, secondCycle.Scale, 0.0001f);
        }

        [Test]
        public void ParticleAppearance_UsesOnlyHeartResponseForSize()
        {
            HeartRateVisualSettings settings = HeartRateVisualSettings.CreateDefault();
            HeartRateVisualResponse negative = HeartRateVisualResponseMapper.Resolve(-1f, settings);
            HeartRateVisualResponse positive = HeartRateVisualResponseMapper.Resolve(1f, settings);
            HeartRatePulseEnvelope negativePulse = HeartRatePulseEnvelopeMapper.Resolve(0.75f, in negative, settings);
            HeartRatePulseEnvelope positivePulse = HeartRatePulseEnvelopeMapper.Resolve(0.75f, in positive, settings);

            float negativeSize = HeartRateParticleAppearanceMapper.ResolveSize(0.025f, in negative, in negativePulse);
            float positiveSize = HeartRateParticleAppearanceMapper.ResolveSize(0.025f, in positive, in positivePulse);

            Assert.AreEqual(0.025f * settings.NegativeParticleScale, negativeSize, 0.0001f);
            Assert.AreEqual(0.025f * settings.PositiveParticleScale, positiveSize, 0.0001f);
        }

        [Test]
        public void ParticleAppearance_EnforcesVisibilityFloorWithoutChangingHue()
        {
            var mappedColor = new Color(0.2f, 0.4f, 0.8f, 0.1f);

            Color visibleColor = HeartRateParticleAppearanceMapper.ResolveColor(mappedColor, 0.72f);

            Assert.AreEqual(mappedColor.r, visibleColor.r);
            Assert.AreEqual(mappedColor.g, visibleColor.g);
            Assert.AreEqual(mappedColor.b, visibleColor.b);
            Assert.AreEqual(0.72f, visibleColor.a);
        }

        [Test]
        public void HaloResponse_AtPulsePeak_PreservesSignedSideOfReference()
        {
            HeartRateVisualSettings settings = HeartRateVisualSettings.CreateDefault();
            HeartRateVisualResponse negative = HeartRateVisualResponseMapper.Resolve(-1f, settings);
            HeartRateVisualResponse positive = HeartRateVisualResponseMapper.Resolve(1f, settings);
            HeartRatePulseEnvelope negativePulse = HeartRatePulseEnvelopeMapper.Resolve(0.175f, in negative, settings);
            HeartRatePulseEnvelope positivePulse = HeartRatePulseEnvelopeMapper.Resolve(0.175f, in positive, settings);

            HeartRateHaloResponse negativeHalo = HeartRateHaloResponseMapper.Resolve(1f, in negative, in negativePulse, settings);
            HeartRateHaloResponse positiveHalo = HeartRateHaloResponseMapper.Resolve(1f, in positive, in positivePulse, settings);

            Assert.Less(negativeHalo.ActiveRadius, negativeHalo.ReferenceRadius);
            Assert.Greater(positiveHalo.ActiveRadius, positiveHalo.ReferenceRadius);
        }

        [Test]
        public void HaloResponse_WhenNeutral_AlignsWithReference()
        {
            HeartRateVisualSettings settings = HeartRateVisualSettings.CreateDefault();
            HeartRateVisualResponse neutral = HeartRateVisualResponseMapper.Resolve(0f, settings);
            HeartRatePulseEnvelope pulse = HeartRatePulseEnvelopeMapper.Resolve(0.175f, in neutral, settings);

            HeartRateHaloResponse halo = HeartRateHaloResponseMapper.Resolve(1.5f, in neutral, in pulse, settings);

            Assert.AreEqual(halo.ReferenceRadius, halo.ActiveRadius);
        }

        [Test]
        public void FreshnessResponse_BeforeTimeout_RemainsFullyVisible()
        {
            float freshness = HeartRateFreshnessMapper.Resolve(true, 2f, 5f, 1f);

            Assert.AreEqual(1f, freshness);
        }

        [Test]
        public void FreshnessResponse_DuringFade_TransitionsToInactive()
        {
            float midFade = HeartRateFreshnessMapper.Resolve(true, 5.5f, 5f, 1f);
            float stale = HeartRateFreshnessMapper.Resolve(true, 6f, 5f, 1f);

            Assert.AreEqual(0.5f, midFade, 0.0001f);
            Assert.AreEqual(0f, stale);
        }

        [Test]
        public void FreshnessResponse_WithoutSample_IsInactive()
        {
            Assert.AreEqual(0f, HeartRateFreshnessMapper.Resolve(false, 0f, 5f, 1f));
        }
    }
}
