using System.Collections.Generic;
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
    private static readonly List<ToggleButtonColor> allButtons = new List<ToggleButtonColor>();

    void Awake()
    {
        _btn = GetComponent<Button>();
        if (!targetGraphic) targetGraphic = GetComponent<Graphic>();

        // Prevent the Button's ColorBlock from fighting our tint.
        _btn.transition = Selectable.Transition.None;

        allButtons.Add(this);
        _btn.onClick.AddListener(Toggle);
    }

    void Start()
    {
        SetOn(isOn);
    }

    void OnDestroy()
    {
        allButtons.Remove(this);
        _btn.onClick.RemoveListener(Toggle);
    }

    public void Toggle()
    {
        SetOn(!isOn);
    }

    void Apply()
    {
        if (targetGraphic) targetGraphic.color = isOn ? onColor : offColor;
    }

    // Optional: let other scripts set it explicitly
    public void SetOn(bool value)
    {
        if (isOn == value)
        {
            Apply();
            return;
        }

        isOn = value;

        if (isOn)
        {
            // Ensure this is the only active button
            foreach (var button in allButtons)
            {
                if (button != this)
                {
                    button.SetOn(false);
                }
            }
        }

        Apply();
    }
}
