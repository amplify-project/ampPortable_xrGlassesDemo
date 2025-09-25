using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

[RequireComponent(typeof(MeshRenderer))]
public class RuntimeImageBoard : MonoBehaviour
{
    [Tooltip("Desired width in meters of the board when first shown.")]
    public float initialWidthMeters = 0.6f;

    [Tooltip("Optional: assign in Inspector for testing.")]
    public Texture2D initialTexture;

    MeshRenderer _mr;

    void Awake() => _mr = GetComponent<MeshRenderer>();

    void Start()
    {
        if (initialTexture != null)
            ApplyTexture(initialTexture);
    }

    public void ApplyTexture(Texture2D tex)
    {
        if (tex == null) return;

        // Ensure the material uses an alpha-friendly shader.
        var mat = _mr.material;
        if (mat.shader.name.Contains("Unlit") == false)
            mat.shader = Shader.Find("Unlit/Transparent");

        mat.mainTexture = tex;

        // Keep aspect by scaling the Quad (1x1) in X and Y.
        float aspect = (float)tex.height / tex.width; // h/w
        transform.localScale = new Vector3(initialWidthMeters, initialWidthMeters * aspect, 1f);
    }

    // Example: load a PNG from StreamingAssets or an absolute file path on Android.
    public void LoadFromPath(string path)
    {
        StartCoroutine(LoadPngCo(path));
    }

    IEnumerator LoadPngCo(string path)
    {
        using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(path))
        {
            yield return uwr.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
            if (uwr.result != UnityWebRequest.Result.Success)
#else
            if (uwr.isNetworkError || uwr.isHttpError)
#endif
            {
                Debug.LogError($"Failed to load image: {uwr.error} ({path})");
                yield break;
            }
            Texture2D tex = DownloadHandlerTexture.GetContent(uwr);
            ApplyTexture(tex);
        }
    }
}
