using UnityEngine;

/// <summary>
/// Shows or hides a UI panel from Button OnClick events. Add to a Canvas or UI root and assign the panel reference.
/// </summary>
public class UIPanelShowHide : MonoBehaviour
{
    #region Inspector
    [Header("Panel")]
    [SerializeField] private GameObject m_Panel;

    [Tooltip("If true, the panel is hidden when the scene starts (typical for Settings / Credits overlays).")]
    [SerializeField] private bool m_HidePanelOnAwake = true;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (m_HidePanelOnAwake && m_Panel != null)
            m_Panel.SetActive(false);
    }
    #endregion

    #region Public Methods
    /// <summary>Wire to a Button OnClick to open the panel.</summary>
    public void ShowPanel()
    {
        if (m_Panel == null)
        {
            Debug.LogWarning("UIPanelShowHide: No panel assigned.", this);
            return;
        }

        m_Panel.SetActive(true);
    }

    /// <summary>Wire to a Button OnClick to close the panel.</summary>
    public void HidePanel()
    {
        if (m_Panel == null)
        {
            Debug.LogWarning("UIPanelShowHide: No panel assigned.", this);
            return;
        }

        m_Panel.SetActive(false);
    }
    #endregion
}
