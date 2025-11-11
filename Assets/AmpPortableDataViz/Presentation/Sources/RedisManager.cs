using System;
using System.Linq;
using UnityEngine;
using AmpPortableDataViz.Presentation.Sources;

[DisallowMultipleComponent]
public sealed class RedisManager : MonoBehaviour
{
    [Serializable]
    private struct ChannelSubscription
    {
        public string ChannelName;
        public RedisDataPump DataPump;
    }

    [SerializeField]
    private RedisDataPump dataPump;

    [SerializeField]
    private string host = "192.168.0.6";

    [SerializeField]
    private int port = 6379;

    [SerializeField]
    private string channelName = "amplify.engagement.engagement";

    [SerializeField]
    private ChannelSubscription[] additionalChannelSubscriptions = new[]
    {
        new ChannelSubscription { ChannelName = "amplify.engagement.boredom" },
        new ChannelSubscription { ChannelName = "amplify.engagement.confusion" },
        new ChannelSubscription { ChannelName = "amplify.engagement.frustration" }
    };

    private void Awake()
    {
        if (dataPump == null)
        {
            dataPump = GetComponent<RedisDataPump>();
        }

        ApplyConfiguration();
    }

    private void Reset()
    {
        dataPump = GetComponent<RedisDataPump>();
    }

    public void Configure(string newHost, int newPort, params string[] channels)
    {
        host = newHost;
        port = newPort;

        var normalizedChannels = channels?
            .Where(channel => !string.IsNullOrWhiteSpace(channel))
            .Select(channel => channel.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedChannels != null && normalizedChannels.Length > 0)
        {
            channelName = normalizedChannels[0];
            ApplyChannelNames(normalizedChannels);
        }

        ApplyConfiguration();
    }

    private void ApplyChannelNames(string[] channels)
    {
        if (channels.Length <= 1 || additionalChannelSubscriptions == null || additionalChannelSubscriptions.Length == 0)
        {
            return;
        }

        for (int i = 0; i < additionalChannelSubscriptions.Length; i++)
        {
            int channelIndex = i + 1;
            if (channelIndex >= channels.Length)
            {
                break;
            }

            var binding = additionalChannelSubscriptions[i];
            binding.ChannelName = channels[channelIndex];
            additionalChannelSubscriptions[i] = binding;
        }
    }

    private void ApplyConfiguration()
    {
        if (dataPump != null && !string.IsNullOrWhiteSpace(channelName))
        {
            dataPump.ConfigureConnection(host, port, channelName);
        }

        if (additionalChannelSubscriptions == null)
        {
            return;
        }

        foreach (var subscription in additionalChannelSubscriptions)
        {
            if (subscription.DataPump == null || string.IsNullOrWhiteSpace(subscription.ChannelName))
            {
                continue;
            }

            subscription.DataPump.ConfigureConnection(host, port, subscription.ChannelName);
        }
    }
}
