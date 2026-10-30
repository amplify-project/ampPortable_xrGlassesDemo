using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AmpPortableDataViz.Core
{
    public sealed class RedisServiceEndpoint
    {
        private readonly Dictionary<string, string> _txtRecords;

        public RedisServiceEndpoint(
            string host,
            int port,
            string serviceName = "",
            IReadOnlyDictionary<string, string> txtRecords = null)
        {
            Host = string.IsNullOrWhiteSpace(host) ? string.Empty : host.Trim();
            Port = port;
            ServiceName = string.IsNullOrWhiteSpace(serviceName) ? string.Empty : serviceName.Trim();
            _txtRecords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (txtRecords == null)
            {
                return;
            }

            foreach (var pair in txtRecords)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key))
                {
                    _txtRecords[pair.Key.Trim()] = pair.Value ?? string.Empty;
                }
            }
        }

        public string Host { get; }
        public int Port { get; }
        public string ServiceName { get; }
        public IReadOnlyDictionary<string, string> TxtRecords => _txtRecords;
        public bool IsValid => !string.IsNullOrWhiteSpace(Host) && Port > 0 && Port <= 65535;
    }

    public readonly struct RedisTxtRecordRequirement
    {
        public RedisTxtRecordRequirement(string key, string expectedValue = "")
        {
            Key = string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
            ExpectedValue = expectedValue ?? string.Empty;
        }

        public string Key { get; }
        public string ExpectedValue { get; }
        public bool RequiresExactValue => !string.IsNullOrEmpty(ExpectedValue);
    }

    public static class RedisServiceMetadataValidator
    {
        public static bool IsCompatible(
            RedisServiceEndpoint endpoint,
            IReadOnlyList<RedisTxtRecordRequirement> requirements)
        {
            if (endpoint == null || !endpoint.IsValid)
            {
                return false;
            }

            if (requirements == null || requirements.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                var requirement = requirements[i];
                if (string.IsNullOrWhiteSpace(requirement.Key) ||
                    !endpoint.TxtRecords.TryGetValue(requirement.Key, out var actualValue))
                {
                    return false;
                }

                if (requirement.RequiresExactValue &&
                    !string.Equals(actualValue, requirement.ExpectedValue, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public interface IRedisEndpointDiscovery : IDisposable
    {
        Task<RedisServiceEndpoint> DiscoverAsync(int timeoutMs, CancellationToken cancellationToken);
    }
}
