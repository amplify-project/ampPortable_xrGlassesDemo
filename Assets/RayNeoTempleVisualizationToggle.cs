using RayNeo;
using UnityEngine;

/// <summary>
/// Toggles between the particle mesh and image board when the RayNeo right-temple touchpad receives a single tap.
/// Drop this in the scene and assign the two root GameObjects in the Inspector.
/// </summary>
public sealed class RayNeoTempleVisualizationToggle : MonoBehaviour
{
    [Header("Visual Roots")]
    [SerializeField] private GameObject particleMeshRoot;
    [SerializeField] private GameObject imageBoardRoot;

    [Header("Startup")]
    [SerializeField] private bool showParticleMeshOnStart = true;
    [SerializeField] private bool applyStartupState = true;

    private bool _showingParticleMesh;
    private bool _subscribed;

    private void Awake()
    {
        _showingParticleMesh = showParticleMeshOnStart;

        if (applyStartupState)
        {
            ApplyVisibility();
        }
    }

    private void OnEnable()
    {
        SubscribeToTempleTap();
    }

    private void OnDisable()
    {
        UnsubscribeFromTempleTap();
    }

    private void OnDestroy()
    {
        UnsubscribeFromTempleTap();
    }

    public void ToggleVisualization()
    {
        _showingParticleMesh = !_showingParticleMesh;
        ApplyVisibility();
    }

    public void ShowParticleMesh()
    {
        _showingParticleMesh = true;
        ApplyVisibility();
    }

    public void ShowImageBoard()
    {
        _showingParticleMesh = false;
        ApplyVisibility();
    }

    private void SubscribeToTempleTap()
    {
        if (_subscribed)
        {
            return;
        }

        SimpleTouchForLite.Instance.OnSimpleTap.AddListener(ToggleVisualization);
        _subscribed = true;
    }

    private void UnsubscribeFromTempleTap()
    {
        if (!_subscribed || !SimpleTouchForLite.SingletonExist)
        {
            _subscribed = false;
            return;
        }

        SimpleTouchForLite.Instance.OnSimpleTap.RemoveListener(ToggleVisualization);
        _subscribed = false;
    }

    private void ApplyVisibility()
    {
        if (particleMeshRoot == null)
        {
            Debug.LogWarning("RayNeoTempleVisualizationToggle: ParticleMeshRoot is not assigned.");
        }
        else
        {
            particleMeshRoot.SetActive(_showingParticleMesh);
        }

        if (imageBoardRoot == null)
        {
            Debug.LogWarning("RayNeoTempleVisualizationToggle: ImageBoardRoot is not assigned.");
        }
        else
        {
            imageBoardRoot.SetActive(!_showingParticleMesh);
        }
    }
}
