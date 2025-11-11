using System;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmpPortableDataViz.Infra;
using AmpPortableDataViz.Presentation.Sources;

[DisallowMultipleComponent]
public sealed class RedisManager : MonoBehaviour
{
    [Serializable]
    private struct ChannelSubscription
    {
        public string ChannelName;
        public bool UseEmotionChannel;
        public EmotionChannelKind EmotionChannel;
        public int EmotionDeviceIndex;
        public RedisDataPump DataPump;
    }

    [SerializeField]
    private RedisDataPump dataPump;

    [SerializeField]
    private string host = "192.168.0.6";

    [SerializeField]
    private int port = 6379;

    [SerializeField]
    private string channelName = RedisEmotionChannels.BroadcastChannel;

    [SerializeField]
    private string[] emotionDeviceIds = RedisEmotionChannels.GetDefaultDeviceIds();

    [SerializeField]
    private ChannelSubscription[] additionalChannelSubscriptions = new[]
    {
        new ChannelSubscription
        {
            UseEmotionChannel = true,
            EmotionChannel = EmotionChannelKind.Valence,
            EmotionDeviceIndex = 0
        },
        new ChannelSubscription
        {
            UseEmotionChannel = true,
            EmotionChannel = EmotionChannelKind.Arousal,
            EmotionDeviceIndex = 0
        },
        new ChannelSubscription
        {
            UseEmotionChannel = true,
            EmotionChannel = EmotionChannelKind.Valence,
            EmotionDeviceIndex = 1
        },
        new ChannelSubscription
        {
            UseEmotionChannel = true,
            EmotionChannel = EmotionChannelKind.Arousal,
            EmotionDeviceIndex = 1
        }
    };

    private bool _isInitialized;

    private void Awake()
    {
        if (dataPump == null)
        {
            dataPump = GetComponent<RedisDataPump>();
        }

        ApplyConfiguration();
        _isInitialized = true;
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

    public void SetEmotionDeviceIds(IEnumerable<string> deviceIds)
    {
        if (deviceIds == null)
        {
            return;
        }

        var normalized = deviceIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalized.Length == 0)
        {
            return;
        }

        emotionDeviceIds = normalized;

        if (_isInitialized)
        {
            ApplyConfiguration();
        }
    }

    private void ApplyChannelNames(string[] channels)
    {
        if (channels.Length <= 1 || additionalChannelSubscriptions == null || additionalChannelSubscriptions.Length == 0)
        {
            return;
        }

        int nextChannelIndex = 1;

        for (int i = 0; i < additionalChannelSubscriptions.Length && nextChannelIndex < channels.Length; i++)
        {
            var binding = additionalChannelSubscriptions[i];
            if (binding.UseEmotionChannel)
            {
                continue;
            }

            binding.ChannelName = channels[nextChannelIndex];
            additionalChannelSubscriptions[i] = binding;
            nextChannelIndex++;
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
            if (subscription.DataPump == null)
            {
                continue;
            }

            if (subscription.UseEmotionChannel)
            {
                var deviceId = ResolveEmotionDeviceId(subscription.EmotionDeviceIndex);
                subscription.DataPump.ConfigureEmotionChannel(host, port, subscription.EmotionChannel, deviceId);
            }
            else if (!string.IsNullOrWhiteSpace(subscription.ChannelName))
            {
                subscription.DataPump.ConfigureConnection(host, port, subscription.ChannelName);
            }
        }
    }

    private string ResolveEmotionDeviceId(int index)
    {
        if (emotionDeviceIds == null || emotionDeviceIds.Length == 0)
        {
            return string.Empty;
        }

        if (index <= 0)
        {
            return emotionDeviceIds[0];
        }

        if (index >= emotionDeviceIds.Length)
        {
            return emotionDeviceIds[emotionDeviceIds.Length - 1];
        }

        return emotionDeviceIds[index];
    }
}
