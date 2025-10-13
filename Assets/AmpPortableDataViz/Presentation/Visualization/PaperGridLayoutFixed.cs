using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class PaperGridLayoutFixed : MonoBehaviour
{
    [Header("Parent holding the 4 sheets")]
    public Transform content;

    [Header("Grid config")]
    public int columns = 2;
    public int rows = 1;

    [Tooltip("Size of ONE sheet in meters (X=width, Y=height). Keep constant.")]
    public Vector2 cellSize = new Vector2(0.30f, 0.21f);  // example; set to your sheet’s real size

    [Tooltip("Spacing between sheet edges (meters).")]
    public Vector2 spacing = new Vector2(0.65f, 0.65f);   // your 0.65/0.65

    [Tooltip("Center the grid on the root pivot.")]
    public bool centerPivot = true;

    [Header("Root grab collider")]
    public float colliderDepth = 0.03f;

    BoxCollider _gridCol;

    void Awake()
    {
        _gridCol = GetComponent<BoxCollider>();
        _gridCol.isTrigger = false;

        ScoreLoader.ImageBoardInstantiated.AddListener(OnImageBoardInstantiated);
        ScoreLoader.ImageBoardRemoved.AddListener(OnImageBoardRemoved);
    }

    void OnEnable()
    {
        // Re-apply layout when re-shown
        RebuildLayout();
    }

    // Call this after adding/removing/toggling children
    public void RebuildLayout()
    {
        if (!content) { Debug.LogWarning("PaperGridLayoutFixed: Content not assigned"); return; }

        // Use sibling order for deterministic placement
        var items = new List<Transform>();
        for (int i = 0; i < content.childCount; i++)
        {
            var t = content.GetChild(i);
            if (t.gameObject.activeSelf) items.Add(t); // include only visible items
        }
        if (items.Count == 0)
        {
            _gridCol.size = new Vector3(0.01f, 0.01f, colliderDepth);
            return;
        }

        int cols = Mathf.Max(1, columns);
        int rCount = Mathf.Max(1, rows);

        // Place in row-major order using LOCAL space only
        for (int i = 0; i < items.Count; i++)
        {
            int r = i / cols;
            int c = i % cols;

            float x = c * (cellSize.x + spacing.x);
            float y = -r * (cellSize.y + spacing.y);

            var t = items[i];
            t.localRotation = Quaternion.identity;   // keep sheets aligned
            t.localPosition = new Vector3(x, y, 0f); // LOCAL placement
        }

        float totalW = cols * cellSize.x + (cols - 1) * spacing.x;
        float totalH = rCount * cellSize.y + (rCount - 1) * spacing.y;

        // Center the whole grid around the root’s origin (nice pivot for grab/follow)
        if (centerPivot)
        {
            content.localPosition = new Vector3(
                -totalW * 0.5f + cellSize.x * 0.5f,
                 totalH * 0.5f - cellSize.y * 0.5f,
                 0f
            );
        }
        else
        {
            content.localPosition = Vector3.zero;
        }

        // Resize the root’s BoxCollider so the grid is easy to grab
        _gridCol.center = Vector3.zero;
        _gridCol.size = new Vector3(totalW, totalH, colliderDepth);
    }

    // Helpers to show/hide as a set and keep layout correct
    public void ShowAllSheets(bool show)
    {
        for (int i = 0; i < content.childCount; i++)
            content.GetChild(i).gameObject.SetActive(show);

        if (show) RebuildLayout();
    }

    // If you create sheets at runtime:
    public void AddSheet(Transform sheet)
    {
        sheet.SetParent(content, worldPositionStays: false);
        sheet.localRotation = Quaternion.identity;
        sheet.localPosition = Vector3.zero; // layout will move it
        RebuildLayout();
    }
    
    private void OnImageBoardInstantiated()
    {
        RebuildLayout();
    }

    private void OnImageBoardRemoved()
    {
        RebuildLayout();
    }

    void OnDisable()
    {
        ScoreLoader.ImageBoardInstantiated.RemoveListener(OnImageBoardInstantiated);
        ScoreLoader.ImageBoardRemoved.RemoveListener(OnImageBoardRemoved);
    }
}
