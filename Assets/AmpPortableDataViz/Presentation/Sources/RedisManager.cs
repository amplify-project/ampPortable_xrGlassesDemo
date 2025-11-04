using UnityEngine;
using AmpPortableDataViz.Presentation.Sources;

[DisallowMultipleComponent]
public sealed class RedisManager : MonoBehaviour
{
    [SerializeField]
    private RedisDataPump dataPump;

    [SerializeField]
    private string host = "192.168.0.6";

    [SerializeField]
    private int port = 6379;

    [SerializeField]
    private string channelName = "sensor_data";

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

    public void Configure(string newHost, int newPort, string newChannel)
    {
        host = newHost;
        port = newPort;
        channelName = newChannel;
        ApplyConfiguration();
    }

    private void ApplyConfiguration()
    {
        if (dataPump == null)
        {
            return;
        }

        dataPump.ConfigureConnection(host, port, channelName);
    }
}
