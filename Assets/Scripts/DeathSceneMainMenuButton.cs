using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Wire the public methods to UI Buttons (On Click) on the death screen — main menu or retry (SampleScene).
/// </summary>
public class DeathSceneMainMenuButton : MonoBehaviour
{
    #region Inspector
    [Header("Scenes")]
    [Tooltip("Exact name as in File → Build Settings (no path).")]
    [SerializeField] private string m_MainMenuSceneName = "MainMenu_Scene";

    [Tooltip("Gameplay scene to load for Retry / Play Again.")]
    [SerializeField] private string m_SampleSceneName = "SampleScene";
    #endregion

    #region Public Methods
    /// <summary>
    /// Assign to a Button OnClick to open the main menu.
    /// </summary>
    public void LoadMainMenu()
    {
        LoadScene(m_MainMenuSceneName, "Main menu");
    }

    /// <summary>
    /// Assign to a Button OnClick to reload gameplay (SampleScene).
    /// </summary>
    public void LoadSampleScene()
    {
        LoadScene(m_SampleSceneName, "Sample");
    }
    #endregion

    #region Private Methods
    private void LoadScene(string _sceneName, string _labelForError)
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (string.IsNullOrWhiteSpace(_sceneName))
        {
            Debug.LogError($"DeathSceneMainMenuButton: {_labelForError} scene name is empty.", this);
            return;
        }

        SceneManager.LoadScene(_sceneName, LoadSceneMode.Single);
    }
    #endregion
}
