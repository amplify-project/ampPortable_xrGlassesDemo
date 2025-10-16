using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Listens for an assigned Input Action (e.g., an XREAL controller button) and quits the app when pressed.
/// Hook this up to the button action exposed by the XREAL plugin or any other XR controller action.
/// </summary>
public class XRealControllerQuitter : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Input action that represents the controller interaction that should trigger quitting.")]
    private InputActionReference quitAction;

    [SerializeField]
    [Tooltip("Require holding the action for at least this many seconds before quitting. Set to 0 for instant quit.")]
    private float holdDuration = 1.0f;

    [SerializeField]
    [Tooltip("If true, also stops Play Mode inside the Unity Editor.")]
    private bool stopEditorPlayMode = true;

    InputAction _boundAction;
    double _pressStartTime;
    bool _pressed;

    void OnEnable()
    {
        if (quitAction == null || quitAction.action == null)
        {
            Debug.LogWarning("XRealControllerQuitter: No quit action assigned. Assign an InputActionReference mapped to the desired controller button.");
            return;
        }

        _boundAction = quitAction.action;
        _boundAction.performed += OnActionPerformed;
        _boundAction.canceled += OnActionCanceled;

        if (!_boundAction.enabled)
        {
            _boundAction.Enable();
        }
    }

    void OnDisable()
    {
        if (_boundAction == null) return;

        _boundAction.performed -= OnActionPerformed;
        _boundAction.canceled -= OnActionCanceled;
    }

    void OnActionPerformed(InputAction.CallbackContext context)
    {
        _pressed = true;
        _pressStartTime = context.time;

        if (holdDuration <= 0f)
        {
            ExecuteQuit();
        }
    }

    void OnActionCanceled(InputAction.CallbackContext context)
    {
        if (!_pressed) return;
        _pressed = false;

        if (holdDuration > 0f)
        {
            var heldFor = context.time - _pressStartTime;
            if (heldFor >= holdDuration)
            {
                ExecuteQuit();
            }
        }
    }

    void ExecuteQuit()
    {
        Debug.Log("XRealControllerQuitter: Quit requested via assigned controller action.");

#if UNITY_EDITOR
        if (stopEditorPlayMode)
        {
            UnityEditor.EditorApplication.isPlaying = false;
            return;
        }
#endif

        Application.Quit();
    }
}
