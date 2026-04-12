using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Main menu utility: press a key to wipe all <see cref="PlayerPrefs"/> (full reset for testing).
/// </summary>
public class MainMenuClearPlayerPrefsHotkey : MonoBehaviour
{
    public KeyCode hotkey = KeyCode.Home;

    [Tooltip("Only reacts when the active scene name matches (default: MainMenu). Leave empty to allow any scene (not recommended).")]
    public string requiredSceneName = "MainMenu";

    [Tooltip("If true, hotkey only works in the Editor.")]
    public bool onlyInEditor;

    private void Update()
    {
        if (onlyInEditor && !Application.isEditor)
            return;

        if (!string.IsNullOrEmpty(requiredSceneName))
        {
            var active = SceneManager.GetActiveScene().name;
            if (!string.Equals(active, requiredSceneName, System.StringComparison.Ordinal))
                return;
        }

        if (Input.GetKeyDown(hotkey))
            ClearAllPlayerPrefs();
    }

    /// <summary>Same as hotkey; can be wired from UI Button if needed.</summary>
    public void ClearAllPlayerPrefs()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("[MainMenuClearPlayerPrefsHotkey] PlayerPrefs.DeleteAll + Save completed.");
    }
}
