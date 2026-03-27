using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// One-time interactable that unlocks handler panel systems.
/// Wire your existing Interact input/event to Interact() or OnInteract().
/// </summary>
public class HandlerPanelUnlockInteractable : MonoBehaviour, IInteractable
{
    #region Inspector
    [Header("Targets")]
    [Tooltip("Map panel logic that controls TheHandler Select/Activate actions.")]
    [SerializeField] private LiveLevelMapPanel m_LiveLevelMapPanel;

    [Tooltip("Camera monitor grid that renders the handler screen feeds.")]
    [SerializeField] private SecurityCameraMonitorGrid m_SecurityCameraMonitorGrid;

    [Header("Behaviour")]
    [Tooltip("If true, this interactable can be used only once.")]
    [SerializeField] private bool m_OneTimeUse = true;

    [Tooltip("Prompt text shown when player can interact with this object.")]
    [SerializeField] private string m_InteractionPrompt = "Press [E] to unlock handler panel";

    [Tooltip("Optional callback fired after unlock succeeds.")]
    [SerializeField] private UnityEvent m_OnUnlocked;
    #endregion

    #region Public Properties
    public string InteractionPrompt => m_InteractionPrompt;
    public bool CanInteract => !m_OneTimeUse || !m_HasBeenUsed;
    #endregion

    #region Private Fields
    private bool m_HasBeenUsed;
    #endregion

    #region Public Methods
    public void Interact(GameObject interactor)
    {
        UnlockHandlerPanel();
    }

    public void Interact()
    {
        UnlockHandlerPanel();
    }

    public void OnInteract()
    {
        UnlockHandlerPanel();
    }

    public void UnlockHandlerPanel()
    {
        if (m_OneTimeUse && m_HasBeenUsed)
            return;

        if (m_LiveLevelMapPanel != null)
            m_LiveLevelMapPanel.UnlockHandlerControls();

        if (m_SecurityCameraMonitorGrid != null)
            m_SecurityCameraMonitorGrid.UnlockMonitor();

        m_HasBeenUsed = true;
        m_OnUnlocked?.Invoke();
    }
    #endregion
}
