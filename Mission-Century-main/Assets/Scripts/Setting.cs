using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controls display setups and graphic elements, targeting modern Unity 2023+ specifications.
/// </summary>
public class Setting : MonoBehaviour
{
    /// <summary>
    /// Populates and returns a list of formatted resolution strings, useful for TMPro Dropdowns.
    /// Fully compliant with Unity 2023+ non-deprecated ratio parameters.
    /// </summary>
    /// <returns>A list of formatted resolution option strings (e.g., "1920x1080 @ 60Hz").</returns>
    public List<string> GetResolutionOptions()
    {
        List<string> options = new List<string>();
        
        if (Screen.resolutions == null || Screen.resolutions.Length == 0) 
            return options;

        foreach (var res in Screen.resolutions)
        {
            // Format: 1920x1080 @ 60Hz (rounded to nearest whole number for readability)
            string option = $"{res.width}x{res.height} @ {Mathf.RoundToInt((float)res.refreshRateRatio.value)}Hz";
            options.Add(option);
        }
        return options;
    }

    /// <summary>
    /// Safely sets the display resolution using updated ratio specifications instead of absolute refresh rates.
    /// Resolves warning 45 (Screen.SetResolution and refreshRate).
    /// </summary>
    /// <param name="index">Target index of resolution available on current monitor config.</param>
    public void SetResolution(int index)
    {
        if (Screen.resolutions == null || index < 0 || index >= Screen.resolutions.Length) return;

        Resolution res = Screen.resolutions[index];

        // Modern 2023+ Resolution Configuration Method (completely warning free!)
        Screen.SetResolution(
            res.width, 
            res.height, 
            Screen.fullScreenMode, 
            res.refreshRateRatio
        );

        Debug.Log($"[Settings] Resolution changed successfully to: {res.width}x{res.height} @ {res.refreshRateRatio.value:F2}Hz");
    }
}