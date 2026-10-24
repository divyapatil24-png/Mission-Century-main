using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles scene transitions smoothly from menus to actual cricket gameplay loops.
/// Cleans up obsolete API warnings.
/// </summary>
public class ScenceController : MonoBehaviour
{
    /// <summary>
    /// Loads a scene dynamically by name.
    /// </summary>
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// Safe modern method to query the active game manager inside scene actions.
    /// Resolves warning 14 of old SceneController.
    /// </summary>
    public void ResetActiveGameManager()
    {
        // Clean warning-free lookup using FindFirstObjectByType
        GameManager manager = Object.FindFirstObjectByType<GameManager>();
        if (manager != null)
        {
            manager.StartGame();
        }
    }
}