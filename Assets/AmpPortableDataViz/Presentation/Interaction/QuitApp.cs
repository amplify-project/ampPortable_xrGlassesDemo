using RayNeo;
using UnityEngine;

public class QuitApp : MonoBehaviour
{
    /// <summary>
    /// Quit the application
    /// </summary>
    public void ToQuitApp()
    {
        Application.Quit();
    }

    void Start()
    {
        // Add double-tap event
        SimpleTouchForLite.Instance.OnDoubleFingerTap.AddListener(ToQuitApp);
    }

    private void OnDestroy()
    {
        if (SimpleTouchForLite.SingletonExist)
        {
            // Remove double-tap event
            SimpleTouchForLite.Instance.OnDoubleFingerTap.RemoveListener(ToQuitApp);
        }
    }
}
