using UnityEngine;

public class RedisManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    async void Start()
    {
        await RedisSubscriber.Begin();
    }

    async void OnDestroy()
    {
        await RedisSubscriber.CleanupAsync();
    }
}
