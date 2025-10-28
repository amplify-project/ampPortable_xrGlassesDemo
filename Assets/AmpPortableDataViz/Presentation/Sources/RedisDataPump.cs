using UnityEngine;

public class RedisDataPump : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private string emissionProperty = "_Emission";
    [SerializeField] private Vector2 emissionRange = new Vector2(0.5f, 5f);
    [SerializeField] private bool logValues;

    private MaterialPropertyBlock _propertyBlock;

    void Awake()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }

        _propertyBlock = new MaterialPropertyBlock();
    }

    void Update()
    {
        var hasMessage = false;
        RedisSubscriber.RedisMessage latestMessage = default;

        while (RedisSubscriber.TryDequeue(out var message))
        {
            latestMessage = message;
            hasMessage = true;
        }

        if (!hasMessage || targetRenderer == null)
        {
            return;
        }

        var normalizedValue = Mathf.Clamp01((float)latestMessage.Value);
        var emission = Mathf.Lerp(emissionRange.x, emissionRange.y, normalizedValue);

        targetRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(emissionProperty, emission);
        targetRenderer.SetPropertyBlock(_propertyBlock);

        if (logValues)
        {
            Debug.Log($"Redis value {latestMessage.Value:F4} -> Emission {emission:F4}");
        }
    }
}
