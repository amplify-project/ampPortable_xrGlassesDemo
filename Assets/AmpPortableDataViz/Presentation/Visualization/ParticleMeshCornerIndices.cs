namespace AmpPortableDataViz.Presentation.Visualization
{
    internal readonly struct ParticleMeshCornerIndices
    {
        public readonly int BottomLeft;
        public readonly int BottomRight;
        public readonly int TopLeft;
        public readonly int TopRight;

        public ParticleMeshCornerIndices(
            int bottomLeft,
            int bottomRight,
            int topLeft,
            int topRight)
        {
            BottomLeft = bottomLeft;
            BottomRight = bottomRight;
            TopLeft = topLeft;
            TopRight = topRight;
        }

        public int this[int cornerIndex]
        {
            get
            {
                switch (cornerIndex)
                {
                    case 0:
                        return BottomLeft;
                    case 1:
                        return BottomRight;
                    case 2:
                        return TopLeft;
                    case 3:
                        return TopRight;
                    default:
                        throw new System.ArgumentOutOfRangeException(nameof(cornerIndex));
                }
            }
        }
    }

    internal static class ParticleMeshCornerIndexMapper
    {
        public const int CornerCount = 4;

        public static ParticleMeshCornerIndices Resolve(int gridWidth, int gridHeight)
        {
            int width = UnityEngine.Mathf.Max(1, gridWidth);
            int height = UnityEngine.Mathf.Max(1, gridHeight);
            int topRowStart = (height - 1) * width;

            return new ParticleMeshCornerIndices(
                0,
                width - 1,
                topRowStart,
                topRowStart + width - 1);
        }
    }
}
