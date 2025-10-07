using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(BoxCollider))]
public class PaperGridManager : MonoBehaviour
{
    [Header("Assign your Content transform (parent of sheets)")]
    public Transform content;

    [Header("Layout")]
    public int columns = 3;
    public Vector2 spacing = new Vector2(0.02f, 0.02f);   // meters between sheets
    public bool centerPivot = true;                       // keep grid centered on root

    [Header("Sheet size (fallback if auto fail)")]
    public Vector2 defaultCellSize = new Vector2(0.21f, 0.297f); // ~A4 aspect at 1:1 (meters)

    private BoxCollider _gridCollider;

    void Awake()
    {
        _gridCollider = GetComponent<BoxCollider>();
        if (!_gridCollider) _gridCollider = gameObject.AddComponent<BoxCollider>();
        _gridCollider.isTrigger = false;

        ScoreLoader.ImageBoardInstantiated.AddListener(OnImageBoardInstantiated);
        ScoreLoader.ImageBoardRemoved.AddListener(OnImageBoardRemoved);
    }

    public void RebuildLayout()
    {
        if (!content) { Debug.LogWarning("PaperGridManager: Content not set."); return; }

        var children = new List<Transform>();
        foreach (Transform t in content)
            if (t.gameObject.activeInHierarchy) children.Add(t);

        if (children.Count == 0) return;

        int cols = Mathf.Max(1, columns);
        int rows = Mathf.CeilToInt(children.Count / (float)cols);

        // Estimate a cell size from the first child’s renderer if possible
        Vector2 cell = GetChildSizeApprox(children[0]);
        if (cell.x <= 1e-4f || cell.y <= 1e-4f) cell = defaultCellSize;

        // Position children (top-left origin, then recenter if requested)
        for (int i = 0; i < children.Count; i++)
        {
            int r = i / cols;
            int c = i % cols;

            float x = c * (cell.x + spacing.x);
            float y = -r * (cell.y + spacing.y);

            children[i].localPosition = new Vector3(x, y, transform.localPosition.z);
            children[i].localRotation = Quaternion.identity;
        }

        float totalW = cols * cell.x + (cols - 1) * spacing.x;
        float totalH = rows * cell.y + (rows - 1) * spacing.y;

        if (centerPivot)
        {
            // Move content so that the root’s origin is the grid’s center
            content.localPosition = new Vector3(
                -totalW * 0.5f + cell.x * 0.5f,
                 totalH * 0.5f - cell.y * 0.5f,
                 transform.localPosition.z
            );
        }

        UpdateRootCollider(totalW, totalH);
    }

    Vector2 GetChildSizeApprox(Transform child)
    {
        var rend = child.GetComponentInChildren<MeshRenderer>();
        if (!rend) return Vector2.zero;
        // Works best if root has no rotation/scale when you call RebuildLayout
        Vector3 s = rend.bounds.size;
        return new Vector2(s.x, s.y);
    }

    void UpdateRootCollider(float width, float height)
    {
        // Thin depth so it’s easy to grab from either side
        _gridCollider.center = Vector3.zero;
        _gridCollider.size = new Vector3(width, height, 0.03f);
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
