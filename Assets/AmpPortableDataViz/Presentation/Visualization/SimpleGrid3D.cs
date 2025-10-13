using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

public class SimpleGrid3D : MonoBehaviour
{
    [Header("Grid shape")]
    public int columns = 2;
    public int rows = 1;

    [Header("Cell layout (local units)")]
    public Vector2 cellSize = new Vector2(1f, 1f);
    public Vector2 spacing = new Vector2(0.1f, 0.1f);

    [Header("Origin & order")]
    public Vector2 origin = Vector2.zero; // local X (right), Y (up). Y will go negative per row.

    //void OnValidate() { Layout(); }
    void Start()
    {
        ScoreLoader.ImageBoardInstantiated.AddListener(OnImageBoardInstantiated);
        Layout(); 
    }

    void OnDisable()
    {
        ScoreLoader.ImageBoardInstantiated.RemoveListener(OnImageBoardInstantiated);
    }

    public void Layout()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (!child.gameObject.activeInHierarchy) continue;

            int col = i % columns;
            int row = i / columns;

            float x = origin.x + col * (cellSize.x + spacing.x);
            float y = origin.y - row * (cellSize.y + spacing.y); // downwards

            child.localPosition = new Vector3(x, y, 0f);

            // Optional: scale each quad to cell size assuming a 1x1 Unity quad
            child.localScale = new Vector3(cellSize.x, cellSize.y, 1f);
        }
        
        Debug.Log("Number of children: " + transform.childCount);
    }

    private void OnImageBoardInstantiated()
    {
        Layout();
    }
}
