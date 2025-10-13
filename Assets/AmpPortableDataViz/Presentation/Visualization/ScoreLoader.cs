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

    private bool acordeaoVisible;
    private bool baritoneVisible;
    private bool sopranoVisible;
    private static readonly Dictionary<string, List<GameObject>> activeBoards = new Dictionary<string, List<GameObject>>();

    private const string AcordeaoKey = "Acordeao";
    private const string BaritoneKey = "Baritone Sax";
    private const string SopranoKey = "Soprano Sax";

    void Start()
    {
        // acordeaoVisible = false;
        // baritoneVisible = false;
        // sopranoVisible = false;
    }
    
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
            case AcordeaoKey:

                //acordeaoVisible = !acordeaoVisible;
                if (acordeaoVisible)
                {
                    DestroyBoards(AcordeaoKey);
                    acordeaoVisible = false;
                    break;
                }
                else if(!acordeaoVisible)
                {
                    acordeaoVisible = true;
                }

                DestroyBoards(BaritoneKey);
                DestroyBoards(SopranoKey);
                baritoneVisible = false;
                sopranoVisible = false;

                // Load Acordeao boards
                fileName = "Corridinho do Algarve - Acordeao_1.png";
                fileName2 = "Corridinho do Algarve - Acordeao_2.png";

                GameObject acordeaoInstance = Instantiate(imageBoardPrefab, spawnParent);
                //acordeaoInstance.transform.SetParent(spawnParent, worldPositionStays:false);

                //imageBoardInstantiated?.Invoke();
                RegisterBoard(AcordeaoKey, acordeaoInstance);
                GameObject acordeaoInstance2 = Instantiate(imageBoardPrefab, spawnParent);
                //acordeaoInstance2.transform.SetParent(spawnParent, worldPositionStays:false);

                //imageBoardInstantiated?.Invoke();
                RegisterBoard(AcordeaoKey, acordeaoInstance2);
                RuntimePngLoader pngLoader = acordeaoInstance.GetComponent<RuntimePngLoader>();
                RuntimePngLoader pngLoader2 = acordeaoInstance2.GetComponent<RuntimePngLoader>();

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

            case BaritoneKey:

                //baritoneVisible = !baritoneVisible;
                if (baritoneVisible)
                {
                    DestroyBoards(BaritoneKey);
                    break;
                }
                else if(!baritoneVisible)
                {
                    baritoneVisible = true;
                }

                DestroyBoards(AcordeaoKey);
                DestroyBoards(SopranoKey);
                acordeaoVisible = false;
                sopranoVisible = false;

                // Load Baritone Sax board
                fileName = "Corridinho do Algarve - Baritone Sax.png";

                GameObject baritoneInstance = Instantiate(imageBoardPrefab, spawnParent);
                //baritoneInstance.transform.SetParent(spawnParent, worldPositionStays:false);
                //imageBoardInstantiated?.Invoke();
                RegisterBoard(BaritoneKey, baritoneInstance);
                pngLoader = baritoneInstance.GetComponent<RuntimePngLoader>();

                if (pngLoader == null)
                {
                    Debug.LogError("[ScoreLoader] RuntimePngLoader component not found on ImageBoard instance.");
                    return;
                }

                pngLoader.pathKind = pathKind;
                pngLoader.pathOrFileName = fileName;
                break;

            case SopranoKey:

                //sopranoVisible = !sopranoVisible;
                if (sopranoVisible)
                {
                    DestroyBoards(SopranoKey);
                    break;
                }
                else if(!sopranoVisible)
                {
                    sopranoVisible = true;
                }

                DestroyBoards(AcordeaoKey);
                DestroyBoards(BaritoneKey);
                acordeaoVisible = false;
                baritoneVisible = false;

                // Load Soprano Sax board
                fileName = "Corridinho do Algarve - Soprano Sax.png";

                GameObject sopranoInstance = Instantiate(imageBoardPrefab, spawnParent);
                //sopranoInstance.transform.SetParent(spawnParent, worldPositionStays:false);
                //imageBoardInstantiated?.Invoke();
                RegisterBoard(SopranoKey, sopranoInstance);
                pngLoader = sopranoInstance.GetComponent<RuntimePngLoader>();

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

    private void RegisterBoard(string key, GameObject board)
    {
        if (board == null)
        {
            return;
        }

        if (!activeBoards.TryGetValue(key, out var boards))
        {
            boards = new List<GameObject>();
            activeBoards[key] = boards;
        }

        boards.Add(board);
    }

    private void DestroyBoards(string key)
    {
        if (!activeBoards.TryGetValue(key, out var boards) || boards.Count == 0)
        {
            return;
        }

        foreach (var board in boards)
        {
            if (board != null)
            {
                Destroy(board);
                //imageBoardRemoved?.Invoke();
            }
        }

        activeBoards.Remove(key);
    }
}
