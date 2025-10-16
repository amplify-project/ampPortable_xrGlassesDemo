using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
#if TMP_PRESENT
using TMPro;
#endif

/// <summary>
/// Simple runtime browser that surfaces score PNGs as buttons and routes the selection to ScoreLoader.
/// Drop this on a panel, assign a Button prefab and a container, and call RefreshFileButtons when needed.
/// </summary>
public class RuntimeScoreImporter : MonoBehaviour
{
    [SerializeField] private ScoreLoader scoreLoader;
    [SerializeField] private Button fileButtonPrefab;
    [SerializeField] private Transform buttonParent;
    [SerializeField] private GameObject emptyStateRoot;
    [SerializeField] private bool includeStreamingAssets = true;
    [SerializeField] private bool includePersistentData = true;
    [SerializeField] private List<string> additionalDirectories = new List<string>();
    [SerializeField] private string searchPattern = "*.png";
    [SerializeField] private bool refreshOnEnable = true;

    private readonly List<Button> spawnedButtons = new List<Button>();
    private readonly List<string> discoveredFiles = new List<string>();

    private void OnEnable()
    {
        if (refreshOnEnable)
        {
            RefreshFileButtons();
        }
    }

    public void RefreshFileButtons()
    {
        if (scoreLoader == null)
        {
            Debug.LogWarning("[RuntimeScoreImporter] ScoreLoader reference is missing.");
            return;
        }

        if (fileButtonPrefab == null || buttonParent == null)
        {
            Debug.LogWarning("[RuntimeScoreImporter] Button prefab or parent is not assigned.");
            return;
        }

        ClearButtons();
        DiscoverFiles();

        bool hasFiles = discoveredFiles.Count > 0;
        if (emptyStateRoot != null)
        {
            emptyStateRoot.SetActive(!hasFiles);
        }

        if (!hasFiles)
        {
            return;
        }

        foreach (var path in discoveredFiles)
        {
            var button = Instantiate(fileButtonPrefab, buttonParent);
            spawnedButtons.Add(button);

            string fileName = Path.GetFileName(path);
            SetButtonLabel(button, fileName);

            string capturedPath = path;
            button.onClick.AddListener(() => scoreLoader.LoadScoreFromAbsolutePath(capturedPath, fileName));
        }
    }

    public void ClearButtons()
    {
        foreach (var button in spawnedButtons)
        {
            if (button != null)
            {
                Destroy(button.gameObject);
            }
        }

        spawnedButtons.Clear();
    }

    private void DiscoverFiles()
    {
        discoveredFiles.Clear();
        var roots = new List<string>();

        if (includeStreamingAssets && Directory.Exists(Application.streamingAssetsPath))
        {
            roots.Add(Application.streamingAssetsPath);
        }

        if (includePersistentData && Directory.Exists(Application.persistentDataPath))
        {
            roots.Add(Application.persistentDataPath);
        }

        foreach (var directory in additionalDirectories)
        {
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                roots.Add(directory);
            }
        }

        foreach (var root in roots)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(root, searchPattern, SearchOption.AllDirectories))
                {
                    if (!discoveredFiles.Contains(file))
                    {
                        discoveredFiles.Add(file);
                    }
                }
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"[RuntimeScoreImporter] Failed to search '{root}': {ex.Message}");
            }
            catch (System.UnauthorizedAccessException ex)
            {
                Debug.LogWarning($"[RuntimeScoreImporter] No access to '{root}': {ex.Message}");
            }
        }

        discoveredFiles.Sort();
    }

    private void SetButtonLabel(Button button, string label)
    {
        if (button == null)
        {
            return;
        }

        var legacyText = button.GetComponentInChildren<Text>();
        if (legacyText != null)
        {
            legacyText.text = label;
        }

#if TMP_PRESENT
        var tmpText = button.GetComponentInChildren<TMP_Text>();
        if (tmpText != null)
        {
            tmpText.text = label;
        }
#endif
    }
}

