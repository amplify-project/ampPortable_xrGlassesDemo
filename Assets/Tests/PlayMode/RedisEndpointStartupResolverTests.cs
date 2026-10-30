using System;
using System.Threading;
using System.Threading.Tasks;
using AmpPortableDataViz.Application;
using AmpPortableDataViz.Core;
using NUnit.Framework;

namespace AmpPortableDataViz.Tests.PlayMode
{
    public sealed class RedisEndpointStartupResolverTests
    {
        [Test]
        public async Task ResolveAsync_WhenCachedEndpointResponds_UsesCachedEndpoint()
        {
            var discovery = new FakeDiscovery(async token =>
            {
                await Task.Delay(5000, token);
                return new RedisServiceEndpoint("discovered", 6379);
            });
            var resolver = new RedisEndpointStartupResolver(
                discovery,
                (endpoint, _, __) => Task.FromResult(endpoint.Host == "cached"));

            var result = await resolver.ResolveAsync(
                new RedisServiceEndpoint("cached", 6379),
                true,
                750,
                5000,
                2000,
                CancellationToken.None);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(RedisEndpointResolutionSource.Cached, result.Source);
            Assert.AreEqual("cached", result.Endpoint.Host);
        }

        [Test]
        public async Task ResolveAsync_WhenCachedEndpointFails_UsesAlreadyRunningDiscovery()
        {
            var discoveredEndpoint = new RedisServiceEndpoint("discovered", 6380);
            var discoveryStarted = new TaskCompletionSource<bool>();
            var discovery = new FakeDiscovery(async token =>
            {
                discoveryStarted.TrySetResult(true);
                await Task.Yield();
                return discoveredEndpoint;
            });
            var resolver = new RedisEndpointStartupResolver(
                discovery,
                async (endpoint, _, __) =>
                {
                    await discoveryStarted.Task;
                    return endpoint.Host == "discovered";
                });

            var result = await resolver.ResolveAsync(
                new RedisServiceEndpoint("stale", 6379),
                true,
                750,
                5000,
                2000,
                CancellationToken.None);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(RedisEndpointResolutionSource.Discovered, result.Source);
            Assert.AreSame(discoveredEndpoint, result.Endpoint);
        }

        [Test]
        public async Task ResolveAsync_WhenDiscoveryWinsRace_UsesDiscoveredEndpoint()
        {
            var discovery = new FakeDiscovery(_ => Task.FromResult(new RedisServiceEndpoint("discovered", 6379)));
            var resolver = new RedisEndpointStartupResolver(
                discovery,
                async (endpoint, _, token) =>
                {
                    if (endpoint.Host == "cached")
                    {
                        await Task.Delay(5000, token);
                    }

                    return true;
                });

            var result = await resolver.ResolveAsync(
                new RedisServiceEndpoint("cached", 6379),
                true,
                750,
                5000,
                2000,
                CancellationToken.None);

            Assert.AreEqual(RedisEndpointResolutionSource.Discovered, result.Source);
        }

        [Test]
        public async Task ResolveAsync_WhenNeitherCandidateResponds_ReturnsFailure()
        {
            var discovery = new FakeDiscovery(_ => Task.FromResult<RedisServiceEndpoint>(null));
            var resolver = new RedisEndpointStartupResolver(
                discovery,
                (_, __, ___) => Task.FromResult(false));

            var result = await resolver.ResolveAsync(
                new RedisServiceEndpoint("stale", 6379),
                true,
                750,
                20,
                20,
                CancellationToken.None);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(RedisEndpointResolutionSource.None, result.Source);
        }

        private sealed class FakeDiscovery : IRedisEndpointDiscovery
        {
            private readonly Func<CancellationToken, Task<RedisServiceEndpoint>> _discover;

            public FakeDiscovery(Func<CancellationToken, Task<RedisServiceEndpoint>> discover)
            {
                _discover = discover;
            }

            public Task<RedisServiceEndpoint> DiscoverAsync(int timeoutMs, CancellationToken cancellationToken)
            {
                return _discover(cancellationToken);
            }

            public void Dispose()
            {
            }
        }
    }
}
