using System;
using TMPro;
using UnityEngine;

public class PaperFollowerManager : MonoBehaviour
{
    [SerializeField]
    private GameObject imageBoardParent;

    public void PassFollowerEnum(int dropdownIndex)
    {
        Debug.Log($"PaperFollowerManager: Dropdown index selected: {dropdownIndex}");

        TMP_Dropdown dropdown = GetComponent<TMP_Dropdown>();
        if (dropdown == null)
        {
            Debug.LogWarning("PaperFollowerManager: No TMP_Dropdown component found on the same GameObject.");
            return;
        }

        if (dropdownIndex < 0 || dropdownIndex >= dropdown.options.Count)
        {
            Debug.LogWarning($"PaperFollowerManager: Dropdown index {dropdownIndex} out of range.");
            return;
        }

        string selectedOption = dropdown.options[dropdownIndex].text;

        if (imageBoardParent == null)
        {
            Debug.LogWarning("PaperFollowerManager: imageBoardParent is not assigned.");
            return;
        }

        if (!Enum.TryParse(selectedOption, out PaperPlacementMode.FollowMode mode))
        {
            Debug.LogWarning($"PaperFollowerManager: Unable to parse '{selectedOption}' into FollowMode.");
            return;
        }

        Debug.Log($"PaperFollowerManager: Setting follow mode to {mode} for all papers.");

        // foreach (Transform child in imageBoardParent.transform)
        // {
        //     PaperPlacementMode ppm = child.GetComponent<PaperPlacementMode>();
        //     if (ppm == null) continue;

        //     ppm.followMode = mode;
        // }

        PaperPlacementMode ppm = imageBoardParent.GetComponent<PaperPlacementMode>();
        if (ppm != null)
        {
            ppm.followMode = mode;
        }
    }
}
