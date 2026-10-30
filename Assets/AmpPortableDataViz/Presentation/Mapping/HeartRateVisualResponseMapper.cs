using System;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.Mapping
{
    [Serializable]
    public struct HeartRateVisualSettings
    {
        [Range(0f, 0.25f)] public float DeadBand;
        [Range(0.1f, 1f)] public float NegativeParticleScale;
        [Range(1f, 3f)] public float PositiveParticleScale;
        [Range(20f, 120f)] public float MinimumPulseBpm;
        [Range(20f, 180f)] public float NeutralPulseBpm;
        [Range(60f, 240f)] public float MaximumPulseBpm;
        [Range(0f, 0.75f)] public float NegativePulseDepth;
        [Range(0f, 0.75f)] public float NeutralPulseDepth;
        [Range(0f, 1f)] public float PositivePulseDepth;
        [Range(0.5f, 1f)] public float NegativeHaloRadiusScale;
        [Range(1f, 1.5f)] public float PositiveHaloRadiusScale;
        [Range(0f, 0.2f)] public float HaloPulseTravelFraction;

        public static HeartRateVisualSettings CreateDefault()
        {
            return new HeartRateVisualSettings
            {
                DeadBand = 0.06f,
                NegativeParticleScale = 0.65f,
                PositiveParticleScale = 1.75f,
                MinimumPulseBpm = 38f,
                NeutralPulseBpm = 72f,
                MaximumPulseBpm = 144f,
                NegativePulseDepth = 0.18f,
                NeutralPulseDepth = 0.08f,
                PositivePulseDepth = 0.3f,
                NegativeHaloRadiusScale = 0.82f,
                PositiveHaloRadiusScale = 1.18f,
                HaloPulseTravelFraction = 0.06f
            };
        }
    }

    internal readonly struct HeartRateVisualResponse
    {
        public readonly float SignedValue;
        public readonly float Direction;
        public readonly float Magnitude;
        public readonly float ParticleScale;
        public readonly float PulseBpm;
        public readonly float HaloRadiusScale;

        public HeartRateVisualResponse(
            float signedValue,
            float direction,
            float magnitude,
            float particleScale,
            float pulseBpm,
            float haloRadiusScale)
        {
            SignedValue = signedValue;
            Direction = direction;
            Magnitude = magnitude;
            ParticleScale = particleScale;
            PulseBpm = pulseBpm;
            HaloRadiusScale = haloRadiusScale;
        }

        public bool IsNeutral => Mathf.Approximately(Direction, 0f);
    }

    internal static class HeartRateVisualResponseMapper
    {
        public static HeartRateVisualResponse Resolve(float heartRate, HeartRateVisualSettings settings)
        {
            settings = ResolveSettings(settings);

            float signedValue = IsFinite(heartRate) ? Mathf.Clamp(heartRate, -1f, 1f) : 0f;
            float absoluteValue = Mathf.Abs(signedValue);
            if (absoluteValue <= settings.DeadBand)
            {
                return new HeartRateVisualResponse(0f, 0f, 0f, 1f, settings.NeutralPulseBpm, 1f);
            }

            float direction = signedValue < 0f ? -1f : 1f;
            float magnitude = Mathf.InverseLerp(settings.DeadBand, 1f, absoluteValue);
            float particleScale = direction < 0f
                ? Mathf.Lerp(1f, settings.NegativeParticleScale, magnitude)
                : Mathf.Lerp(1f, settings.PositiveParticleScale, magnitude);
            float pulseBpm = direction < 0f
                ? Mathf.Lerp(settings.NeutralPulseBpm, settings.MinimumPulseBpm, magnitude)
                : Mathf.Lerp(settings.NeutralPulseBpm, settings.MaximumPulseBpm, magnitude);
            float haloRadiusScale = direction < 0f
                ? Mathf.Lerp(1f, settings.NegativeHaloRadiusScale, magnitude)
                : Mathf.Lerp(1f, settings.PositiveHaloRadiusScale, magnitude);

            return new HeartRateVisualResponse(
                signedValue,
                direction,
                magnitude,
                particleScale,
                pulseBpm,
                haloRadiusScale);
        }

        public static HeartRateVisualSettings ResolveSettings(HeartRateVisualSettings settings)
        {
            if (settings.DeadBand <= 0f &&
                settings.NegativeParticleScale <= 0f &&
                settings.PositiveParticleScale <= 0f &&
                settings.MinimumPulseBpm <= 0f &&
                settings.NeutralPulseBpm <= 0f &&
                settings.MaximumPulseBpm <= 0f &&
                settings.NegativePulseDepth <= 0f &&
                settings.NeutralPulseDepth <= 0f &&
                settings.PositivePulseDepth <= 0f &&
                settings.NegativeHaloRadiusScale <= 0f &&
                settings.PositiveHaloRadiusScale <= 0f &&
                settings.HaloPulseTravelFraction <= 0f)
            {
                return HeartRateVisualSettings.CreateDefault();
            }

            settings.DeadBand = Mathf.Clamp(settings.DeadBand, 0f, 0.25f);
            settings.NegativeParticleScale = Mathf.Clamp(settings.NegativeParticleScale, 0.1f, 1f);
            settings.PositiveParticleScale = Mathf.Clamp(settings.PositiveParticleScale, 1f, 3f);
            settings.NeutralPulseBpm = Mathf.Clamp(settings.NeutralPulseBpm, 20f, 180f);
            settings.MinimumPulseBpm = Mathf.Clamp(settings.MinimumPulseBpm, 20f, settings.NeutralPulseBpm);
            settings.MaximumPulseBpm = Mathf.Clamp(settings.MaximumPulseBpm, settings.NeutralPulseBpm, 240f);
            settings.NegativePulseDepth = Mathf.Clamp(settings.NegativePulseDepth, 0f, 0.75f);
            settings.NeutralPulseDepth = Mathf.Clamp(settings.NeutralPulseDepth, 0f, 0.75f);
            settings.PositivePulseDepth = Mathf.Clamp(settings.PositivePulseDepth, 0f, 1f);
            settings.NegativeHaloRadiusScale = Mathf.Clamp(settings.NegativeHaloRadiusScale, 0.5f, 1f);
            settings.PositiveHaloRadiusScale = Mathf.Clamp(settings.PositiveHaloRadiusScale, 1f, 1.5f);
            settings.HaloPulseTravelFraction = Mathf.Clamp(settings.HaloPulseTravelFraction, 0f, 0.2f);
            return settings;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    internal readonly struct HeartRatePulseEnvelope
    {
        public readonly float Energy;
        public readonly float Scale;

        public HeartRatePulseEnvelope(float energy, float scale)
        {
            Energy = energy;
            Scale = scale;
        }
    }

    internal static class HeartRatePulseEnvelopeMapper
    {
        private const float ActivePhaseFraction = 0.35f;

        public static HeartRatePulseEnvelope Resolve(
            float phase,
            in HeartRateVisualResponse response,
            HeartRateVisualSettings settings)
        {
            settings = HeartRateVisualResponseMapper.ResolveSettings(settings);
            float wrappedPhase = Mathf.Repeat(IsFinite(phase) ? phase : 0f, 1f);
            float energy = 0f;
            if (wrappedPhase < ActivePhaseFraction)
            {
                float activePhase = wrappedPhase / ActivePhaseFraction;
                float sine = Mathf.Sin(activePhase * Mathf.PI);
                energy = sine * sine;
            }

            float scale;
            if (response.Direction < 0f)
            {
                scale = 1f - energy * settings.NegativePulseDepth;
            }
            else if (response.Direction > 0f)
            {
                scale = 1f + energy * settings.PositivePulseDepth;
            }
            else
            {
                scale = 1f + energy * settings.NeutralPulseDepth;
            }

            return new HeartRatePulseEnvelope(energy, Mathf.Max(0.1f, scale));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    internal static class HeartRateParticleAppearanceMapper
    {
        public static float ResolveSize(
            float baseParticleSize,
            in HeartRateVisualResponse response,
            in HeartRatePulseEnvelope pulse)
        {
            return Mathf.Max(0f, baseParticleSize) * response.ParticleScale * pulse.Scale;
        }

        public static Color ResolveColor(Color mappedColor, float minimumAlpha)
        {
            mappedColor.a = Mathf.Max(mappedColor.a, Mathf.Clamp01(minimumAlpha));
            return mappedColor;
        }
    }

    internal readonly struct HeartRateHaloResponse
    {
        public readonly float ReferenceRadius;
        public readonly float ActiveRadius;
        public readonly float PulseEnergy;

        public HeartRateHaloResponse(float referenceRadius, float activeRadius, float pulseEnergy)
        {
            ReferenceRadius = referenceRadius;
            ActiveRadius = activeRadius;
            PulseEnergy = pulseEnergy;
        }
    }

    internal static class HeartRateHaloResponseMapper
    {
        public static HeartRateHaloResponse Resolve(
            float referenceRadius,
            in HeartRateVisualResponse response,
            in HeartRatePulseEnvelope pulse,
            HeartRateVisualSettings settings)
        {
            settings = HeartRateVisualResponseMapper.ResolveSettings(settings);
            float safeReferenceRadius = Mathf.Max(0f, referenceRadius);
            float signedPulseTravel = response.Direction *
                safeReferenceRadius *
                settings.HaloPulseTravelFraction *
                response.Magnitude *
                pulse.Energy;
            float activeRadius = safeReferenceRadius * response.HaloRadiusScale + signedPulseTravel;

            if (response.Direction < 0f)
            {
                activeRadius = Mathf.Min(activeRadius, safeReferenceRadius);
            }
            else if (response.Direction > 0f)
            {
                activeRadius = Mathf.Max(activeRadius, safeReferenceRadius);
            }

            return new HeartRateHaloResponse(
                safeReferenceRadius,
                Mathf.Max(0f, activeRadius),
                pulse.Energy);
        }
    }

    internal static class HeartRateFreshnessMapper
    {
        public static float Resolve(
            bool hasSample,
            float secondsSinceSample,
            float staleAfterSeconds,
            float fadeDurationSeconds)
        {
            if (!hasSample || float.IsNaN(secondsSinceSample) || float.IsInfinity(secondsSinceSample))
            {
                return 0f;
            }

            float safeAge = Mathf.Max(0f, secondsSinceSample);
            float safeStaleAfter = Mathf.Max(0f, staleAfterSeconds);
            if (safeAge <= safeStaleAfter)
            {
                return 1f;
            }

            float safeFadeDuration = Mathf.Max(0.01f, fadeDurationSeconds);
            return 1f - Mathf.InverseLerp(
                safeStaleAfter,
                safeStaleAfter + safeFadeDuration,
                safeAge);
        }
    }
}
