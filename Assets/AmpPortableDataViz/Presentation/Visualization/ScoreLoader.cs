using UnityEngine;
using UnityEngine.Events;

public class ScoreLoader : MonoBehaviour
{
    [Header("Prefab Setup")]
    [SerializeField] public GameObject imageBoardPrefab;
    [SerializeField] private Transform spawnParent;

    [Header("Image Loading")]
    [SerializeField] private RuntimePngLoader.PathKind pathKind = RuntimePngLoader.PathKind.StreamingAssets;

    [Header("Events")]
    [SerializeField] private static UnityEvent imageBoardInstantiated = new UnityEvent();
    [SerializeField] private static UnityEvent imageBoardRemoved = new UnityEvent();

    public static UnityEvent ImageBoardInstantiated => imageBoardInstantiated;
    public static UnityEvent ImageBoardRemoved => imageBoardRemoved;

    private bool acordeaoVisible = false;
    private bool baritoneVisible = false;
    private bool sopranoVisible = false;

    /// <summary>
    /// Instantiates an ImageBoard and points its RuntimePngLoader at the requested file.
    /// Hook this up to a button's OnClick event and pass the desired file name as the argument.
    /// </summary>
    public void LoadScore(string buttonName)
    {
        Debug.Log(buttonName);

        if (imageBoardPrefab == null)
        {
            Debug.LogError("[ScoreLoader] ImageBoard prefab is not assigned.");
            return;
        }

        if (string.IsNullOrEmpty(buttonName))
        {
            Debug.LogWarning("[ScoreLoader] No file name provided to LoadScore.");
            return;
        }

        string fileName = "";
        string fileName2 = "";
        switch (buttonName)
        {
            case "Acordeao":

                acordeaoVisible = !acordeaoVisible;
                if (!acordeaoVisible)
                {
                    // Destroy existing Acordeao boards
                    foreach (Transform child in spawnParent)
                    {
                        RuntimePngLoader currentPngLoader = child.GetComponent<RuntimePngLoader>();
                        if (currentPngLoader != null && (currentPngLoader.pathOrFileName == "Corridinho do Algarve - Acordeao_1.png" || currentPngLoader.pathOrFileName == "Corridinho do Algarve - Acordeao_2.png"))
                        {
                            Destroy(child.gameObject);
                            imageBoardRemoved?.Invoke();
                        }
                    }
                    break;
                }

                // Load Acordeao boards
                fileName = "Corridinho do Algarve - Acordeao_1.png";
                fileName2 = "Corridinho do Algarve - Acordeao_2.png";

                GameObject boardInstance = Instantiate(imageBoardPrefab, spawnParent);
                //boardInstance.transform.SetParent(spawnParent, worldPositionStays:false);

                imageBoardInstantiated?.Invoke();
                GameObject boardInstance2 = Instantiate(imageBoardPrefab, spawnParent);
                //boardInstance2.transform.SetParent(spawnParent, worldPositionStays:false);

                imageBoardInstantiated?.Invoke();
                RuntimePngLoader pngLoader = boardInstance.GetComponent<RuntimePngLoader>();
                RuntimePngLoader pngLoader2 = boardInstance2.GetComponent<RuntimePngLoader>();

                if (pngLoader == null || pngLoader2 == null)
                {
                    Debug.LogError("[ScoreLoader] RuntimePngLoader component not found on ImageBoard instance.");
                    return;
                }

                pngLoader.pathKind = pathKind;
                pngLoader.pathOrFileName = fileName;
                pngLoader2.pathKind = pathKind;
                pngLoader2.pathOrFileName = fileName2;
                break;

            case "Baritone Sax":

                baritoneVisible = !baritoneVisible;
                if (!baritoneVisible)
                {
                    // Destroy existing Baritone Sax boards
                    foreach (Transform child in spawnParent)
                    {
                        RuntimePngLoader currentPngLoader = child.GetComponent<RuntimePngLoader>();
                        if (currentPngLoader != null && currentPngLoader.pathOrFileName == "Corridinho do Algarve - Baritone Sax.png")
                        {
                            Destroy(child.gameObject);
                            imageBoardRemoved?.Invoke();
                        }
                    }
                    break;
                }

                // Load Baritone Sax board
                fileName = "Corridinho do Algarve - Baritone Sax.png";

                boardInstance = Instantiate(imageBoardPrefab, spawnParent);
                //boardInstance.transform.SetParent(spawnParent, worldPositionStays:false);
                imageBoardInstantiated?.Invoke();
                pngLoader = boardInstance.GetComponent<RuntimePngLoader>();

                if (pngLoader == null)
                {
                    Debug.LogError("[ScoreLoader] RuntimePngLoader component not found on ImageBoard instance.");
                    return;
                }

                pngLoader.pathKind = pathKind;
                pngLoader.pathOrFileName = fileName;
                break;

            case "Soprano Sax":
            
                sopranoVisible = !sopranoVisible;
                if (!sopranoVisible)
                {
                    // Destroy existing Soprano Sax boards
                    foreach (Transform child in spawnParent)
                    {
                        RuntimePngLoader currentPngLoader = child.GetComponent<RuntimePngLoader>();
                        if (currentPngLoader != null && currentPngLoader.pathOrFileName == "Corridinho do Algarve - Soprano Sax.png")
                        {
                            Destroy(child.gameObject);
                            imageBoardRemoved?.Invoke();
                        }
                    }
                    break;
                }

                // Load Soprano Sax board
                fileName = "Corridinho do Algarve - Soprano Sax.png";

                boardInstance = Instantiate(imageBoardPrefab, spawnParent);
                //boardInstance.transform.SetParent(spawnParent, worldPositionStays:false);
                imageBoardInstantiated?.Invoke();
                pngLoader = boardInstance.GetComponent<RuntimePngLoader>();

                if (pngLoader == null)
                {
                    Debug.LogError("[ScoreLoader] RuntimePngLoader component not found on ImageBoard instance.");
                    return;
                }

                pngLoader.pathKind = pathKind;
                pngLoader.pathOrFileName = fileName;
                break;
        }

        // If the prefab does not auto-load on Start, trigger loading immediately.
        // if (!pngLoader.loadOnStart)
        // {
        //     pngLoader.LoadFromPath(fileName, pathKind);
        // }
    }
}

