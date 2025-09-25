using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

[RequireComponent(typeof(MeshRenderer))]
public class URPImageBoardApplier : MonoBehaviour
{
    [Tooltip("Initial width in meters when first shown.")]
    public float initialWidthMeters = 0.6f;

    [Tooltip("Set true to show both sides (disables back-face culling).")]
    public bool doubleSided = false;

    // URP Unlit main texture property:
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int CullModeId = Shader.PropertyToID("_Cull");

    MeshRenderer _mr;
    MaterialPropertyBlock _mpb;

    void Awake()
    {
        _mr = GetComponent<MeshRenderer>();
        _mpb = new MaterialPropertyBlock();

        // Optional: enforce URP Unlit Transparent at runtime (if needed)
        var mat = _mr.sharedMaterial;
        if (mat == null || !mat.shader || !mat.shader.name.Contains("Universal Render Pipeline/Unlit"))
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            // Transparent surface type
            mat.SetFloat("_Surface", 1f); // 0=Opaque, 1=Transparent
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            _mr.sharedMaterial = mat;
        }

        if (doubleSided)
        {
            // 0 = Off, 1 = Front, 2 = Back in some shaders; URP uses 2 for Back, 0 for Off.
            // Setting 0 disables culling (two-sided).
            mat.SetFloat(CullModeId, 0f);
        }
    }

    /// <summary>Apply a Texture2D directly.</summary>
    public void ApplyTexture(Texture2D tex)
    {
        if (tex == null) return;

        // Write the texture via MPB so we don’t clone materials per instance.
        _mr.GetPropertyBlock(_mpb);
        _mpb.SetTexture(BaseMapId, tex);
        _mr.SetPropertyBlock(_mpb);

        // Keep aspect ratio: scale quad’s Y from width * (h/w).
        float aspect = (float)tex.height / tex.width;
        transform.localScale = new Vector3(initialWidthMeters, initialWidthMeters * aspect, 1f);
    }

    /// <summary>Load a PNG from an absolute path or StreamingAssets.</summary>
    public void LoadFromPath(string path)
    {
        StartCoroutine(LoadFromPathCo(path));
    }

    IEnumerator LoadFromPathCo(string path)
    {
        // If you pass Application.streamingAssetsPath + "/my.png" on Android,
        // UnityWebRequest will handle the correct jar/file scheme automatically.
        using (var req = UnityWebRequestTexture.GetTexture(path))
        {
            yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                Debug.LogError($"Failed to load image: {req.error} ({path})");
                yield break;
            }
            var tex = DownloadHandlerTexture.GetContent(req);
            ApplyTexture(tex);
        }
    }

    /// <summary>Apply a PNG from raw bytes you obtained elsewhere.</summary>
    public void ApplyPngBytes(byte[] pngBytes)
    {
        if (pngBytes == null || pngBytes.Length == 0) return;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        if (tex.LoadImage(pngBytes))
            ApplyTexture(tex);
        else
            Debug.LogError("Failed to decode PNG bytes.");
    }
}
