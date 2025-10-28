using UnityEngine;

public class RedisDataPump : MonoBehaviour
{
    void Update()
    {
        while (RedisSubscriber.TryDequeue(out var message))
        {
            Debug.Log($"Redis value received: {message.Value:F4}");
        }
    }
}
