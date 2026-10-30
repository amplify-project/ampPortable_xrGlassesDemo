using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AmpPortableDataViz.Core;

namespace AmpPortableDataViz.Application
{
    public enum RedisEndpointResolutionSource
    {
        None,
        Cached,
        Discovered
    }

    public readonly struct RedisEndpointResolution
    {
        public RedisEndpointResolution(RedisServiceEndpoint endpoint, RedisEndpointResolutionSource source)
        {
            Endpoint = endpoint;
            Source = source;
        }

        public RedisServiceEndpoint Endpoint { get; }
        public RedisEndpointResolutionSource Source { get; }
        public bool Succeeded => Endpoint != null && Endpoint.IsValid && Source != RedisEndpointResolutionSource.None;

        public static RedisEndpointResolution Failed =>
            new RedisEndpointResolution(null, RedisEndpointResolutionSource.None);
    }

    public sealed class RedisEndpointStartupResolver
    {
        private readonly IRedisEndpointDiscovery _discovery;
        private readonly Func<RedisServiceEndpoint, int, CancellationToken, Task<bool>> _probe;

        public RedisEndpointStartupResolver(
            IRedisEndpointDiscovery discovery,
            Func<RedisServiceEndpoint, int, CancellationToken, Task<bool>> probe)
        {
            _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
            _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        }

        public async Task<RedisEndpointResolution> ResolveAsync(
            RedisServiceEndpoint cachedEndpoint,
            bool hasCachedEndpoint,
            int cachedProbeTimeoutMs,
            int discoveryTimeoutMs,
            int discoveredProbeTimeoutMs,
            CancellationToken cancellationToken)
        {
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var candidates = new List<Task<RedisEndpointResolution>>(2);

            if (hasCachedEndpoint && cachedEndpoint != null && cachedEndpoint.IsValid)
            {
                candidates.Add(ProbeAsync(
                    cachedEndpoint,
                    RedisEndpointResolutionSource.Cached,
                    cachedProbeTimeoutMs,
                    linkedCancellation.Token));
            }

            candidates.Add(DiscoverAndProbeAsync(
                discoveryTimeoutMs,
                discoveredProbeTimeoutMs,
                linkedCancellation.Token));

            while (candidates.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var completedTask = await Task.WhenAny(candidates);
                candidates.Remove(completedTask);

                var result = await completedTask;
                if (!result.Succeeded)
                {
                    continue;
                }

                linkedCancellation.Cancel();
                return result;
            }

            return RedisEndpointResolution.Failed;
        }

        private async Task<RedisEndpointResolution> DiscoverAndProbeAsync(
            int discoveryTimeoutMs,
            int probeTimeoutMs,
            CancellationToken cancellationToken)
        {
            try
            {
                var endpoint = await _discovery.DiscoverAsync(discoveryTimeoutMs, cancellationToken);
                if (endpoint == null || !endpoint.IsValid)
                {
                    return RedisEndpointResolution.Failed;
                }

                return await ProbeAsync(
                    endpoint,
                    RedisEndpointResolutionSource.Discovered,
                    probeTimeoutMs,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return RedisEndpointResolution.Failed;
            }
            catch
            {
                return RedisEndpointResolution.Failed;
            }
        }

        private async Task<RedisEndpointResolution> ProbeAsync(
            RedisServiceEndpoint endpoint,
            RedisEndpointResolutionSource source,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            try
            {
                bool canConnect = await _probe(endpoint, Math.Max(1, timeoutMs), cancellationToken);
                return canConnect
                    ? new RedisEndpointResolution(endpoint, source)
                    : RedisEndpointResolution.Failed;
            }
            catch (OperationCanceledException)
            {
                return RedisEndpointResolution.Failed;
            }
            catch
            {
                return RedisEndpointResolution.Failed;
            }
        }
    }
}
