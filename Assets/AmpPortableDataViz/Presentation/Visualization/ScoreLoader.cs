using System.Collections;
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
    private Coroutine imageBoardNotificationCoroutine;
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable parentGrabInteractable;
    private readonly List<Collider> registeredBoardColliders = new List<Collider>();

    private const string AcordeaoKey = "Acordeao";
    private const string BaritoneKey = "Baritone Sax";
    private const string SopranoKey = "Soprano Sax";

    private void Awake()
    {
        if (parentGrabInteractable != null)
        {
            return;
        }

        if (spawnParent != null)
        {
            parentGrabInteractable = spawnParent.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>() ?? spawnParent.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        }

        if (parentGrabInteractable == null)
        {
            parentGrabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>() ?? GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        }
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

                acordeaoVisible = !acordeaoVisible;
                if (!acordeaoVisible)
                {
                    DestroyBoards(AcordeaoKey);
                    break;
                }

                var acordeaoPendingDestruction = new List<GameObject>();
                DestroyBoards(BaritoneKey, acordeaoPendingDestruction);
                DestroyBoards(SopranoKey, acordeaoPendingDestruction);
                baritoneVisible = false;
                sopranoVisible = false;

                // Load Acordeao boards
                fileName = "Corridinho do Algarve - Acordeao_1.png";
                fileName2 = "Corridinho do Algarve - Acordeao_2.png";

                GameObject acordeaoInstance = Instantiate(imageBoardPrefab, spawnParent);
                RegisterBoard(AcordeaoKey, acordeaoInstance);
                AttachBoardColliders(acordeaoInstance);
                GameObject acordeaoInstance2 = Instantiate(imageBoardPrefab, spawnParent);

                RegisterBoard(AcordeaoKey, acordeaoInstance2);
                AttachBoardColliders(acordeaoInstance2);
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

                NotifyAfterBoardsDestroyed(acordeaoPendingDestruction);
                break;

            case BaritoneKey:

                baritoneVisible = !baritoneVisible;
                if (!baritoneVisible)
                {
                    DestroyBoards(BaritoneKey);
                    break;
                }

                var baritonePendingDestruction = new List<GameObject>();
                DestroyBoards(AcordeaoKey, baritonePendingDestruction);
                DestroyBoards(SopranoKey, baritonePendingDestruction);
                acordeaoVisible = false;
                sopranoVisible = false;

                // Load Baritone Sax board
                fileName = "Corridinho do Algarve - Baritone Sax.png";

                GameObject baritoneInstance = Instantiate(imageBoardPrefab, spawnParent);
                
                RegisterBoard(BaritoneKey, baritoneInstance);
                AttachBoardColliders(baritoneInstance);
                pngLoader = baritoneInstance.GetComponent<RuntimePngLoader>();

                if (pngLoader == null)
                {
                    Debug.LogError("[ScoreLoader] RuntimePngLoader component not found on ImageBoard instance.");
                    return;
                }

                pngLoader.pathKind = pathKind;
                pngLoader.pathOrFileName = fileName;

                NotifyAfterBoardsDestroyed(baritonePendingDestruction);
                break;

            case SopranoKey:

                sopranoVisible = !sopranoVisible;
                if (!sopranoVisible)
                {
                    DestroyBoards(SopranoKey);
                    break;
                }

                var sopranoPendingDestruction = new List<GameObject>();
                DestroyBoards(AcordeaoKey, sopranoPendingDestruction);
                DestroyBoards(BaritoneKey, sopranoPendingDestruction);
                acordeaoVisible = false;
                baritoneVisible = false;

                // Load Soprano Sax board
                fileName = "Corridinho do Algarve - Soprano Sax.png";

                GameObject sopranoInstance = Instantiate(imageBoardPrefab, spawnParent);
                
                RegisterBoard(SopranoKey, sopranoInstance);
                AttachBoardColliders(sopranoInstance);
                pngLoader = sopranoInstance.GetComponent<RuntimePngLoader>();

                if (pngLoader == null)
                {
                    Debug.LogError("[ScoreLoader] RuntimePngLoader component not found on ImageBoard instance.");
                    return;
                }

                pngLoader.pathKind = pathKind;
                pngLoader.pathOrFileName = fileName;

                NotifyAfterBoardsDestroyed(sopranoPendingDestruction);
                break;
        }
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

    private void DestroyBoards(string key, List<GameObject> pendingDestruction = null)
    {
        if (!activeBoards.TryGetValue(key, out var boards) || boards.Count == 0)
        {
            return;
        }

        foreach (var board in boards)
        {
            if (board != null)
            {
                DetachBoardColliders(board);
                pendingDestruction?.Add(board);
                Destroy(board);
            }
        }

        activeBoards.Remove(key);
    }

    private void NotifyAfterBoardsDestroyed(List<GameObject> pendingDestruction)
    {
        if (imageBoardNotificationCoroutine != null)
        {
            StopCoroutine(imageBoardNotificationCoroutine);
        }

        imageBoardNotificationCoroutine = StartCoroutine(WaitForBoardsToBeDestroyed(pendingDestruction));
    }

    private IEnumerator WaitForBoardsToBeDestroyed(List<GameObject> pendingDestruction)
    {
        if (pendingDestruction == null || pendingDestruction.Count == 0)
        {
            imageBoardInstantiated?.Invoke();
            imageBoardNotificationCoroutine = null;
            yield break;
        }

        bool allDestroyed = false;
        while (!allDestroyed)
        {
            allDestroyed = true;

            for (int i = 0; i < pendingDestruction.Count; i++)
            {
                if (pendingDestruction[i] != null)
                {
                    allDestroyed = false;
                    break;
                }
            }

            if (!allDestroyed)
            {
                yield return null;
            }
        }

        imageBoardInstantiated?.Invoke();
        imageBoardNotificationCoroutine = null;
    }

    private void AttachBoardColliders(GameObject board)
    {
        if (board == null || parentGrabInteractable == null)
        {
            return;
        }

        var colliders = board.GetComponentsInChildren<Collider>();
        if (colliders == null || colliders.Length == 0)
        {
            return;
        }

        var interactableColliders = parentGrabInteractable.colliders;
        if (interactableColliders == null)
        {
            return;
        }

        foreach (var collider in colliders)
        {
            if (collider == null || registeredBoardColliders.Contains(collider))
            {
                continue;
            }

            if (!interactableColliders.Contains(collider))
            {
                interactableColliders.Add(collider);
            }

            registeredBoardColliders.Add(collider);
        }
    }

    private void DetachBoardColliders(GameObject board)
    {
        if (board == null || parentGrabInteractable == null)
        {
            return;
        }

        var colliders = board.GetComponentsInChildren<Collider>();
        if (colliders == null || colliders.Length == 0)
        {
            return;
        }

        var interactableColliders = parentGrabInteractable.colliders;
        if (interactableColliders == null)
        {
            return;
        }

        foreach (var collider in colliders)
        {
            if (collider == null || !registeredBoardColliders.Contains(collider))
            {
                continue;
            }

            interactableColliders.Remove(collider);
            registeredBoardColliders.Remove(collider);
        }
    }
}
