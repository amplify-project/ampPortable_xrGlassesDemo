using System;
using UnityEngine;

namespace AmpPortableDataViz.Presentation.ArtisticElements
{
    /// <summary>Explicit scene wiring for the five imported artistic test elements.</summary>
    [DisallowMultipleComponent]
    public sealed class ArtisticElementsTestSetup : MonoBehaviour
    {
        [SerializeField] private Transform[] elementRoots = Array.Empty<Transform>();
        [SerializeField] private Transform thicknessElement;

        private bool _initialized;
        // Unity mirrors the OBJ's X coordinate; its long edge is slanted within the XY plane.
        private static readonly Vector3 BlockLengthDirection = new Vector3(-0.3935771f, 0.9192916f, 0f);
        public ArtisticElementsSelectionController Selection { get; private set; }
        public ArtisticElementsControlPanel Panel { get; private set; }

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (_initialized) return;
            if (elementRoots == null || elementRoots.Length == 0)
            {
                Debug.LogError("Artistic elements panel requires scene element references.", this);
                return;
            }
            bool thicknessFound = false;
            foreach (Transform root in elementRoots)
            {
                if (root == null)
                {
                    Debug.LogError("Artistic elements panel has a missing scene element reference.", this);
                    return;
                }
                if (root == thicknessElement) thicknessFound = true;
            }
            if (!thicknessFound)
            {
                Debug.LogError("Artistic elements panel requires its thickness target among the scene elements.", this);
                return;
            }

            var controllers = new ArtisticElementController[elementRoots.Length];
            for (int i = 0; i < elementRoots.Length; i++)
            {
                Transform root = elementRoots[i];
                ArtisticElementController controller = root.GetComponent<ArtisticElementController>();
                if (controller == null) controller = root.gameObject.AddComponent<ArtisticElementController>();
                controller.ConfigureThickness(root == thicknessElement, ArtisticElementController.ThicknessAxis.Z);
                controller.ConfigureLength(root == thicknessElement, BlockLengthDirection);
                controllers[i] = controller;
            }
            Selection = GetComponent<ArtisticElementsSelectionController>();
            if (Selection == null) Selection = gameObject.AddComponent<ArtisticElementsSelectionController>();
            Selection.Configure(controllers);
            Panel = GetComponent<ArtisticElementsControlPanel>();
            if (Panel == null) Panel = gameObject.AddComponent<ArtisticElementsControlPanel>();
            Panel.Configure(Selection);
            _initialized = true;
        }
    }
}
