using System.Collections.Generic;
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

    private static readonly string[] acordeaoFileNames =
    {
        "Corridinho do Algarve - Acordeao_1.png",
        "Corridinho do Algarve - Acordeao_2.png"
    };

    private const string baritoneFileName = "Corridinho do Algarve - Baritone Sax.png";
    private const string sopranoFileName = "Corridinho do Algarve - Soprano Sax.png";

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
                    DestroyBoardsByNames(acordeaoFileNames);
                    break;
                }

                DestroyBoardsByNames(baritoneFileName, sopranoFileName);
                baritoneVisible = false;
                sopranoVisible = false;

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
                    DestroyBoardsByNames(baritoneFileName);
                    break;
                }

                DestroyBoardsByNames(acordeaoFileNames);
                DestroyBoardsByNames(sopranoFileName);
                acordeaoVisible = false;
                sopranoVisible = false;

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
                    DestroyBoardsByNames(sopranoFileName);
                    break;
                }

                DestroyBoardsByNames(acordeaoFileNames);
                DestroyBoardsByNames(baritoneFileName);
                acordeaoVisible = false;
                baritoneVisible = false;

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

    private void DestroyBoardsByNames(params string[] fileNames)
    {
        if (spawnParent == null || fileNames == null || fileNames.Length == 0)
        {
            return;
        }

        var targets = new HashSet<string>(fileNames);
        List<GameObject> boardsToDestroy = new List<GameObject>();

        foreach (Transform child in spawnParent)
        {
            RuntimePngLoader currentPngLoader = child.GetComponent<RuntimePngLoader>();
            if (currentPngLoader != null && targets.Contains(currentPngLoader.pathOrFileName))
            {
                boardsToDestroy.Add(child.gameObject);
            }
        }

        foreach (GameObject board in boardsToDestroy)
        {
            Destroy(board);
            imageBoardRemoved?.Invoke();
        }
    }
}
