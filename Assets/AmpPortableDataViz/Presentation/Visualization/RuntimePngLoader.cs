using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class RuntimePngLoader : MonoBehaviour
{
    public enum TargetType
    {
        UIRawImage,
        UIImageSprite,
        SpriteRenderer,
        MeshRendererMaterial
    }

    public enum PathKind
    {
        AbsoluteOrUri,     // e.g., /sdcard/..., file://..., content://..., http(s)://...
        PersistentData,    // relative to Application.persistentDataPath
        StreamingAssets    // relative to Application.streamingAssetsPath (handles Android jar path)
    }

    [Header("Display Target")]
    public TargetType targetType = TargetType.UIRawImage;
    public RawImage uiRawImage;             // if TargetType.UIRawImage
    public Image uiImage;                   // if TargetType.UIImageSprite
    public SpriteRenderer spriteRenderer;   // if TargetType.SpriteRenderer
    public Renderer meshRenderer;           // if TargetType.MeshRendererMaterial
    public string materialTextureProperty = "_MainTex";

    [Header("Loading")]
    public PathKind pathKind = PathKind.AbsoluteOrUri;
    [Tooltip("If PathKind is PersistentData or StreamingAssets, provide a relative file name, e.g., photo.png")]
    public string pathOrFileName;

    [Header("Sprite Options (when using UIImageSprite or SpriteRenderer)")]
    public float spritePixelsPerUnit = 100f;
    public Vector2 spritePivot01 = new Vector2(0.5f, 0.5f); // 0..1 pivot

    [Header("Optional: Auto-load on Start")]
    public bool loadOnStart = false;

    private void Start()
    {
        if (loadOnStart && !string.IsNullOrEmpty(pathOrFileName))
        {
            LoadFromPath(pathOrFileName, pathKind);
        }
    }

    /// <summary>
    /// Public API: call this to load and display a PNG at runtime.
    /// </summary>
    public void LoadFromPath(string input, PathKind kind = PathKind.AbsoluteOrUri)
    {
        StartCoroutine(LoadPngCoroutine(input, kind));
    }

    private IEnumerator LoadPngCoroutine(string input, PathKind kind)
    {
        string uri = BuildUri(input, kind);

        if (string.IsNullOrEmpty(uri))
        {
            Debug.LogError("[RuntimePngLoader] Invalid path/URI.");
            yield break;
        }

        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(uri))
        {
            // On Android, content:// and jar: URIs need this path untouched.
            req.downloadHandler = new DownloadHandlerTexture(true);
            yield return req.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                Debug.LogError($"[RuntimePngLoader] Failed to load texture from '{uri}' : {req.error}");
                yield break;
            }

            Texture2D tex = DownloadHandlerTexture.GetContent(req);
            if (tex == null)
            {
                Debug.LogError("[RuntimePngLoader] Loaded but texture is null.");
                yield break;
            }

            ApplyTexture(tex);
        }
    }

    private string BuildUri(string input, PathKind kind)
    {
        // If it already looks like a URI, return as-is.
        if (kind == PathKind.AbsoluteOrUri &&
            (input.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
             input.StartsWith("content://", StringComparison.OrdinalIgnoreCase) ||
             input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
             input.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
             input.StartsWith("jar:", StringComparison.OrdinalIgnoreCase)))
        {
            return input;
        }

        switch (kind)
        {
            case PathKind.AbsoluteOrUri:
                // Treat as a file path
#if UNITY_ANDROID
                // Android external storage absolute path -> file://
                return "file://" + input;
#else
                return "file:///" + input.Replace("\\", "/");
#endif

            case PathKind.PersistentData:
            {
                string fullPath = System.IO.Path.Combine(Application.persistentDataPath, input);
#if UNITY_ANDROID
                return "file://" + fullPath;
#else
                return "file:///" + fullPath.Replace("\\", "/");
#endif
            }

            case PathKind.StreamingAssets:
            {
                string basePath = Application.streamingAssetsPath;
                string combined = CombinePathsUnix(basePath, input);

                // On Android, StreamingAssets live inside the APK and must be accessed via jar:file://
                // UnityWebRequestTexture can read this jar path directly.
#if UNITY_ANDROID
                if (!combined.StartsWith("jar:", StringComparison.OrdinalIgnoreCase) &&
                    !combined.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    // Application.streamingAssetsPath on Android already resolves to a jar path,
                    // e.g., jar:file:///data/app/xxx.apk!/assets
                    // So just return the combined as-is.
                    return combined;
                }
                return combined;
#else
                if (!combined.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    combined = "file:///" + combined.Replace("\\", "/");
                }
                return combined;
#endif
            }
        }

        return null;
    }

    private static string CombinePathsUnix(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b;
        if (string.IsNullOrEmpty(b)) return a;
        a = a.Replace("\\", "/");
        b = b.Replace("\\", "/");
        if (a.EndsWith("/")) return a + b;
        return a + "/" + b;
    }

    private void ApplyTexture(Texture2D tex)
    {
        switch (targetType)
        {
            case TargetType.UIRawImage:
                if (uiRawImage == null)
                {
                    Debug.LogError("[RuntimePngLoader] TargetType is UIRawImage but uiRawImage is not assigned.");
                    return;
                }
                uiRawImage.texture = tex;
                uiRawImage.SetNativeSize();
                break;

            case TargetType.UIImageSprite:
                if (uiImage == null)
                {
                    Debug.LogError("[RuntimePngLoader] TargetType is UIImageSprite but uiImage is not assigned.");
                    return;
                }
                uiImage.sprite = TextureToSprite(tex);
                // Optionally preserve aspect with a ContentSizeFitter or AspectRatioFitter on the Image
                break;

            case TargetType.SpriteRenderer:
                if (spriteRenderer == null)
                {
                    Debug.LogError("[RuntimePngLoader] TargetType is SpriteRenderer but spriteRenderer is not assigned.");
                    return;
                }
                spriteRenderer.sprite = TextureToSprite(tex);
                break;

            case TargetType.MeshRendererMaterial:
                if (meshRenderer == null)
                {
                    Debug.LogError("[RuntimePngLoader] TargetType is MeshRendererMaterial but meshRenderer is not assigned.");
                    return;
                }
                // Make an instance so we don't overwrite a shared material in editor
                var mat = meshRenderer.material;
                mat.SetTexture(materialTextureProperty, tex);
                break;
        }
    }

    private Sprite TextureToSprite(Texture2D tex)
    {
        return Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(Mathf.Clamp01(spritePivot01.x), Mathf.Clamp01(spritePivot01.y)),
            spritePixelsPerUnit
        );
    }
}
