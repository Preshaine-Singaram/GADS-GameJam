using TMPro;
using UnityEngine;

/// <summary>
/// Shows or hides a TextMeshPro block on the handler screen (e.g. under HandlerScreen_Panel).
/// Place on the same GameObject as the panel or on a child; assign the TMP and optional root to toggle.
/// </summary>
[DisallowMultipleComponent]
public class HandlerPanelMessageDisplay : MonoBehaviour
{
    #region Inspector
    [Header("UI")]
    [Tooltip("Text shown when ShowMessage is called.")]
    [SerializeField] private TMP_Text m_MessageText;

    [Tooltip("If set, this object is enabled/disabled with the message. If null, m_MessageText.gameObject is used.")]
    [SerializeField] private GameObject m_MessageBlockRoot;

    [Tooltip("If true, the message block starts hidden in Awake.")]
    [SerializeField] private bool m_HideMessageAtStart = true;
    #endregion

    #region Static Access
    private static HandlerPanelMessageDisplay s_Instance;
    public static HandlerPanelMessageDisplay Instance => s_Instance;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (s_Instance != null && s_Instance != this)
        {
            Debug.LogWarning($"{nameof(HandlerPanelMessageDisplay)}: multiple instances in scene; keeping first.", this);
            return;
        }

        s_Instance = this;

        if (m_MessageText == null)
            m_MessageText = GetComponentInChildren<TMP_Text>(true);

        if (m_HideMessageAtStart)
            HideMessage();
    }

    private void OnDestroy()
    {
        if (s_Instance == this)
            s_Instance = null;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Sets message text and makes the block visible.
    /// </summary>
    public void ShowMessage(string _message)
    {
        if (m_MessageText != null)
        {
            m_MessageText.text = _message ?? string.Empty;
            GameObject root = m_MessageBlockRoot != null ? m_MessageBlockRoot : m_MessageText.gameObject;
            root.SetActive(true);
            return;
        }

        Debug.LogWarning($"{nameof(HandlerPanelMessageDisplay)} on '{name}': no TMP_Text assigned.", this);
    }

    /// <summary>
    /// Hides the message block (and clears text if a TMP is assigned).
    /// </summary>
    public void HideMessage()
    {
        if (m_MessageText != null)
            m_MessageText.text = string.Empty;

        GameObject root = m_MessageBlockRoot != null
            ? m_MessageBlockRoot
            : m_MessageText != null ? m_MessageText.gameObject : null;

        if (root != null)
            root.SetActive(false);
    }

    /// <summary>
    /// Finds the display in the loaded scene (first instance) and shows a message, if any exists.
    /// </summary>
    public static void ShowMessageGlobal(string _message)
    {
        HandlerPanelMessageDisplay display = s_Instance != null
            ? s_Instance
            : FindObjectOfType<HandlerPanelMessageDisplay>(true);

        if (display != null)
            display.ShowMessage(_message);
        else
            Debug.LogWarning($"{nameof(HandlerPanelMessageDisplay)}: no instance found to show message.");
    }
    #endregion
}
