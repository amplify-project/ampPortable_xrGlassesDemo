using System;
using System.Collections.Generic;
using AmpPortableDataViz.Core;

namespace AmpPortableDataViz.Infra
{
    public sealed class LocalLoopbackNetworkSync : INetworkSync
    {
        private static readonly Dictionary<string, List<Action<object, long>>> _topicSubscribers =
            new Dictionary<string, List<Action<object, long>>>();

        public void Publish<TPayload>(string topic, TPayload payload, long timestampTicksUtc)
        {
            if (_topicSubscribers.TryGetValue(topic, out var subscriberHandlers))
            {
                var subscriberSnapshot = subscriberHandlers.ToArray();
                foreach (var handler in subscriberSnapshot)
                {
                    handler?.Invoke(payload!, timestampTicksUtc);
                }
            }
        }

        public IDisposable Subscribe(string topic, Action<object, long> messageHandler)
        {
            if (!_topicSubscribers.TryGetValue(topic, out var subscriberHandlers))
            {
                subscriberHandlers = new List<Action<object, long>>();
                _topicSubscribers[topic] = subscriberHandlers;
            }
            subscriberHandlers.Add(messageHandler);
            return new Unsubscribe(() => subscriberHandlers.Remove(messageHandler));
        }

        private sealed class Unsubscribe : IDisposable
        {
            private readonly Action _unsubscribeAction;
            public Unsubscribe(Action unsubscribeAction) => _unsubscribeAction = unsubscribeAction;
            public void Dispose() => _unsubscribeAction();
        }
    }
}
