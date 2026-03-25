using UnityEngine;
using UnityEngine.InputSystem;

public class CanvasPanelToggleAndMapSwitch : MonoBehaviour
{
    #region Constants
    private const string c_HitmanMapName = "TheHitman";
    private const string c_HandlerMapName = "TheHandler";
    #endregion

    #region Inspector
    [Header("UI")]
    [SerializeField] private GameObject m_OverlayPanel;

    [Header("Input Asset")]
    [Tooltip("Reference to the Input Actions asset (Assets/Player_IA.inputactions).")]
    [SerializeField] private InputActionAsset m_InputActions;

    [Header("Optional: Disable player controller while overlay is shown")]
    [SerializeField] private Behaviour m_PlayerControllerToDisable;

    [Header("Cursor")]
    [SerializeField] private bool m_UnlockCursorWhenOverlayShown = true;
    #endregion

    #region State
    private bool m_IsOverlayShown;
    private bool m_WantsCursorLockStateChange;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (m_OverlayPanel != null)
            m_OverlayPanel.SetActive(false);

        ApplyMode(isOverlayShown: false);
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.qKey.wasPressedThisFrame)
            ToggleOverlayAndMaps();
    }
    #endregion

    #region Private Methods
    private void ToggleOverlayAndMaps()
    {
        m_IsOverlayShown = !m_IsOverlayShown;
        ApplyMode(m_IsOverlayShown);
    }

    private void ApplyMode(bool isOverlayShown)
    {
        if (m_OverlayPanel != null)
            m_OverlayPanel.SetActive(isOverlayShown);

        if (m_PlayerControllerToDisable != null)
            m_PlayerControllerToDisable.enabled = !isOverlayShown;

        SwitchActionMaps(isOverlayShown ? c_HandlerMapName : c_HitmanMapName);

        if (m_UnlockCursorWhenOverlayShown)
        {
            if (isOverlayShown)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    private void SwitchActionMaps(string activeMapName)
    {
        if (m_InputActions == null)
            return;

        var hitmanMap = m_InputActions.FindActionMap(c_HitmanMapName, throwIfNotFound: false);
        var handlerMap = m_InputActions.FindActionMap(c_HandlerMapName, throwIfNotFound: false);

        if (hitmanMap == null || handlerMap == null)
            return;

        if (activeMapName == c_HandlerMapName)
        {
            hitmanMap.Disable();
            handlerMap.Enable();
        }
        else
        {
            handlerMap.Disable();
            hitmanMap.Enable();
        }
    }
    #endregion
}

