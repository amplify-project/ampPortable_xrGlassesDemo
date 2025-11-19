using UnityEngine;

public class HudSquareBuilder : MonoBehaviour
{
    public float size = 0.15f;
    public float lineWidth = 0.005f;
    public Material lineMaterial;

    public LineRenderer topSide;
    public LineRenderer bottomSide;
    public LineRenderer leftSide;
    public LineRenderer rightSide;

    void Reset()
    {
        // Try to auto-find child line renderers if you’ve named them
        topSide    = transform.Find("TopSide")?.GetComponent<LineRenderer>();
        bottomSide = transform.Find("BottomSide")?.GetComponent<LineRenderer>();
        leftSide   = transform.Find("LeftSide")?.GetComponent<LineRenderer>();
        rightSide  = transform.Find("RightSide")?.GetComponent<LineRenderer>();
    }

    void Awake()
    {
        ConfigureSide(topSide,
            new Vector3(-size * 0.5f,  size * 0.5f, 0f),
            new Vector3( size * 0.5f,  size * 0.5f, 0f));

        ConfigureSide(bottomSide,
            new Vector3(-size * 0.5f, -size * 0.5f, 0f),
            new Vector3( size * 0.5f, -size * 0.5f, 0f));

        ConfigureSide(leftSide,
            new Vector3(-size * 0.5f, -size * 0.5f, 0f),
            new Vector3(-size * 0.5f,  size * 0.5f, 0f));

        ConfigureSide(rightSide,
            new Vector3( size * 0.5f, -size * 0.5f, 0f),
            new Vector3( size * 0.5f,  size * 0.5f, 0f));
    }

    void ConfigureSide(LineRenderer lr, Vector3 p0, Vector3 p1)
    {
        if (lr == null) return;

        lr.useWorldSpace = false;
        lr.positionCount = 2;
        lr.startWidth = lineWidth;
        lr.endWidth   = lineWidth;
        if (lineMaterial != null) lr.material = lineMaterial;

        lr.SetPosition(0, p0);
        lr.SetPosition(1, p1);
    }
}
