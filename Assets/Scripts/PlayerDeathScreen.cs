using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loads the death scene when the player is caught. Place on a persistent object in gameplay scenes (e.g. Canvas root).
/// </summary>
public class PlayerDeathScreen : MonoBehaviour
{
    #region Inspector
    [Header("Scene")]
    [Tooltip("Build Settings name of the scene to load when the player dies.")]
    [SerializeField] private string m_DeathSceneName = "Death_Scene";
    #endregion

    #region Private Fields
    private bool m_HasTriggered;
    private static PlayerDeathScreen s_Instance;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (s_Instance != null && s_Instance != this)
        {
            Debug.LogWarning("Multiple PlayerDeathScreen components in the scene; only one should exist.", this);
            return;
        }

        s_Instance = this;
    }

    private void OnDestroy()
    {
        if (s_Instance == this)
            s_Instance = null;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Loads the death scene. Uses the in-scene PlayerDeathScreen settings if present; otherwise loads "Death_Scene".
    /// </summary>
    public static void TriggerPlayerDeath()
    {
        if (s_Instance != null)
        {
            s_Instance.LoadDeathScene();
            return;
        }

        SceneManager.LoadScene("Death_Scene", LoadSceneMode.Single);
    }
    #endregion

    #region Private Methods
    private void LoadDeathScene()
    {
        if (m_HasTriggered)
            return;

        m_HasTriggered = true;

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (string.IsNullOrWhiteSpace(m_DeathSceneName))
        {
            Debug.LogError("PlayerDeathScreen: Death scene name is empty.", this);
            return;
        }

        SceneManager.LoadScene(m_DeathSceneName, LoadSceneMode.Single);
    }
    #endregion
}
