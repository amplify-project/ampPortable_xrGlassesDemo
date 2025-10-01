using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ToggleButtonColor : MonoBehaviour
{
    [Header("Graphic to tint (defaults to this object)")]
    public Graphic targetGraphic; // e.g., the Button's Image

    [Header("Colors")]
    public Color offColor = new Color32(200, 200, 200, 255); // grey
    public Color onColor  = new Color32( 50, 200,  90, 255); // green

    [Header("Initial State")]
    public bool isOn = false;

    Button _btn;

    void Awake()
    {
        _btn = GetComponent<Button>();
        if (!targetGraphic) targetGraphic = GetComponent<Graphic>();

        // Prevent the Button's ColorBlock from fighting our tint.
        _btn.transition = Selectable.Transition.None;

        Apply();
        _btn.onClick.AddListener(Toggle);
    }

    void OnDestroy()
    {
        _btn.onClick.RemoveListener(Toggle);
    }

    public void Toggle()
    {
        isOn = !isOn;
        Apply();
    }

    void Apply()
    {
        if (targetGraphic) targetGraphic.color = isOn ? onColor : offColor;
    }

    // Optional: let other scripts set it explicitly
    public void SetOn(bool value) { isOn = value; Apply(); }
}
