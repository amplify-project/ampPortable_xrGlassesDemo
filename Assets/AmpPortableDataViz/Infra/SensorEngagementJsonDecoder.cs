using System;
using System.Collections.Generic;
using System.Globalization;
using AmpPortableDataViz.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AmpPortableDataViz.Infra
{
    [Serializable]
    public struct SensorEngagementJsonFormat
    {
        public string ItemsPropertyName;
        public string SensorIdPropertyName;
        public string EngagementPropertyName;
        public string ConfirmationPropertyName;
        public string TimestampPropertyName;

        public static SensorEngagementJsonFormat CreateDefault()
        {
            return new SensorEngagementJsonFormat
            {
                ItemsPropertyName = string.Empty,
                SensorIdPropertyName = "device",
                EngagementPropertyName = "engagement",
                ConfirmationPropertyName = "confirmed",
                TimestampPropertyName = "timestamp"
            };
        }
    }

    /// <summary>
    /// Configurable JSON decoder for sensor engagement payloads. A different decoder can be
    /// supplied without changing the Redis source or trusted-state routing.
    /// </summary>
    public sealed class SensorEngagementJsonDecoder : ISensorEngagementPayloadDecoder
    {
        private readonly SensorEngagementJsonFormat _format;

        public SensorEngagementJsonDecoder(SensorEngagementJsonFormat format)
        {
            _format = ResolveFormat(format);
        }

        public bool TryDecode(
            string channelName,
            string expectedSensorId,
            string rawPayload,
            out SensorEngagementObservation[] observations)
        {
            observations = Array.Empty<SensorEngagementObservation>();
            if (string.IsNullOrWhiteSpace(rawPayload))
            {
                return false;
            }

            try
            {
                JToken root = JToken.Parse(rawPayload);
                var decoded = new List<SensorEngagementObservation>();
                DecodeRoot(root, Normalize(expectedSensorId), decoded);
                if (decoded.Count == 0)
                {
                    return false;
                }

                observations = decoded.ToArray();
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private void DecodeRoot(JToken root, string expectedSensorId, List<SensorEngagementObservation> decoded)
        {
            if (!string.IsNullOrEmpty(_format.ItemsPropertyName) &&
                root.Type == JTokenType.Object &&
                TryGetProperty((JObject)root, _format.ItemsPropertyName, out JToken container))
            {
                DecodeContainer(container, expectedSensorId, decoded);
                return;
            }

            if (root.Type == JTokenType.Array)
            {
                DecodeContainer(root, expectedSensorId, decoded);
                return;
            }

            if (TryDecodeObservation(root, expectedSensorId, string.Empty, out SensorEngagementObservation observation))
            {
                decoded.Add(observation);
            }
        }

        private void DecodeContainer(JToken container, string expectedSensorId, List<SensorEngagementObservation> decoded)
        {
            if (container.Type == JTokenType.Array)
            {
                foreach (JToken item in container.Children())
                {
                    if (TryDecodeObservation(item, expectedSensorId, string.Empty, out SensorEngagementObservation observation))
                    {
                        decoded.Add(observation);
                    }
                }

                return;
            }

            if (container.Type != JTokenType.Object)
            {
                return;
            }

            foreach (JProperty property in container.Children<JProperty>())
            {
                if (TryDecodeObservation(property.Value, expectedSensorId, property.Name, out SensorEngagementObservation observation))
                {
                    decoded.Add(observation);
                }
            }
        }

        private bool TryDecodeObservation(
            JToken token,
            string expectedSensorId,
            string fallbackSensorId,
            out SensorEngagementObservation observation)
        {
            observation = default;
            if (token.Type != JTokenType.Object)
            {
                return false;
            }

            var item = (JObject)token;
            if (!TryGetProperty(item, _format.EngagementPropertyName, out JToken engagementToken) ||
                !TryReadFloat(engagementToken, out float engagement) ||
                !TryGetProperty(item, _format.ConfirmationPropertyName, out JToken confirmationToken) ||
                !TryReadConfirmation(confirmationToken, out bool isConfirmed))
            {
                return false;
            }

            long sourceTimestampTicksUtc = 0L;
            if (TryGetProperty(item, _format.TimestampPropertyName, out JToken timestampToken) &&
                !TryReadUnixTimestampTicksUtc(timestampToken, out sourceTimestampTicksUtc))
            {
                return false;
            }

            string sensorId = string.Empty;
            if (TryGetProperty(item, _format.SensorIdPropertyName, out JToken sensorIdToken))
            {
                sensorId = Normalize(sensorIdToken.Type == JTokenType.String
                    ? sensorIdToken.Value<string>()
                    : sensorIdToken.ToString(Formatting.None));
            }

            if (string.IsNullOrEmpty(sensorId))
            {
                sensorId = Normalize(fallbackSensorId);
            }

            if (string.IsNullOrEmpty(sensorId))
            {
                sensorId = expectedSensorId;
            }

            if (string.IsNullOrEmpty(sensorId) ||
                (!string.IsNullOrEmpty(expectedSensorId) &&
                 !string.Equals(sensorId, expectedSensorId, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            observation = new SensorEngagementObservation(
                sensorId,
                engagement,
                isConfirmed,
                sourceTimestampTicksUtc);
            return true;
        }

        private static bool TryGetProperty(JObject item, string propertyName, out JToken value)
        {
            value = null;
            return !string.IsNullOrWhiteSpace(propertyName) &&
                item.TryGetValue(propertyName.Trim(), StringComparison.OrdinalIgnoreCase, out value);
        }

        private static bool TryReadFloat(JToken token, out float value)
        {
            value = 0f;
            switch (token.Type)
            {
                case JTokenType.Integer:
                case JTokenType.Float:
                    value = token.Value<float>();
                    return !float.IsNaN(value) && !float.IsInfinity(value);
                case JTokenType.String:
                    return float.TryParse(
                        token.Value<string>(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out value) && !float.IsNaN(value) && !float.IsInfinity(value);
                default:
                    return false;
            }
        }

        private static bool TryReadConfirmation(JToken token, out bool isConfirmed)
        {
            isConfirmed = false;
            if (token.Type == JTokenType.Boolean)
            {
                isConfirmed = token.Value<bool>();
                return true;
            }

            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                double value = token.Value<double>();
                if (Math.Abs(value) < double.Epsilon)
                {
                    return true;
                }

                if (Math.Abs(value - 1d) < double.Epsilon)
                {
                    isConfirmed = true;
                    return true;
                }

                return false;
            }

            if (token.Type != JTokenType.String)
            {
                return false;
            }

            string valueText = token.Value<string>()?.Trim();
            if (valueText == "0")
            {
                return true;
            }

            if (valueText == "1")
            {
                isConfirmed = true;
                return true;
            }

            return false;
        }

        private static bool TryReadUnixTimestampTicksUtc(JToken token, out long ticksUtc)
        {
            const long unixEpochTicks = 621355968000000000L;
            ticksUtc = 0L;

            double seconds;
            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                seconds = token.Value<double>();
            }
            else if (token.Type == JTokenType.String &&
                double.TryParse(
                    token.Value<string>(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double parsedSeconds))
            {
                seconds = parsedSeconds;
            }
            else
            {
                return false;
            }

            if (double.IsNaN(seconds) || double.IsInfinity(seconds))
            {
                return false;
            }

            double elapsedTicks = seconds * TimeSpan.TicksPerSecond;
            if (elapsedTicks < DateTime.MinValue.Ticks - unixEpochTicks ||
                elapsedTicks > DateTime.MaxValue.Ticks - unixEpochTicks)
            {
                return false;
            }

            ticksUtc = unixEpochTicks + (long)Math.Round(elapsedTicks);
            return true;
        }

        private static SensorEngagementJsonFormat ResolveFormat(SensorEngagementJsonFormat format)
        {
            SensorEngagementJsonFormat defaults = SensorEngagementJsonFormat.CreateDefault();
            if (string.IsNullOrWhiteSpace(format.SensorIdPropertyName))
            {
                format.SensorIdPropertyName = defaults.SensorIdPropertyName;
            }

            if (string.IsNullOrWhiteSpace(format.EngagementPropertyName))
            {
                format.EngagementPropertyName = defaults.EngagementPropertyName;
            }

            if (string.IsNullOrWhiteSpace(format.ConfirmationPropertyName))
            {
                format.ConfirmationPropertyName = defaults.ConfirmationPropertyName;
            }

            if (string.IsNullOrWhiteSpace(format.TimestampPropertyName))
            {
                format.TimestampPropertyName = defaults.TimestampPropertyName;
            }

            format.ItemsPropertyName = Normalize(format.ItemsPropertyName);
            format.SensorIdPropertyName = Normalize(format.SensorIdPropertyName);
            format.EngagementPropertyName = Normalize(format.EngagementPropertyName);
            format.ConfirmationPropertyName = Normalize(format.ConfirmationPropertyName);
            format.TimestampPropertyName = Normalize(format.TimestampPropertyName);
            return format;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
