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

    [Header("Action Names")]
    [Tooltip("Action in TheHitman map used to switch into TheHandler mode.")]
    [SerializeField] private string m_HitmanSwitchActionName = "Switch";

    [Tooltip("Action in TheHandler map used to switch back to TheHitman mode.")]
    [SerializeField] private string m_HandlerSwitchBackActionName = "Switch";

    [Header("Optional: Disable player controller while overlay is shown")]
    [SerializeField] private Behaviour m_PlayerControllerToDisable;

    [Header("Cursor")]
    [SerializeField] private bool m_UnlockCursorWhenOverlayShown = true;
    #endregion

    #region State
    private bool m_IsOverlayShown;
    private InputActionMap m_HitmanMap;
    private InputActionMap m_HandlerMap;
    private InputAction m_SwitchToHandlerAction;
    private InputAction m_SwitchToHitmanAction;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (m_OverlayPanel != null)
            m_OverlayPanel.SetActive(false);

        CacheActionMapsAndActions();
        ApplyMode(isOverlayShown: false);
    }

    private void OnEnable()
    {
        if (m_SwitchToHandlerAction != null)
            m_SwitchToHandlerAction.performed += OnSwitchToHandlerPerformed;

        if (m_SwitchToHitmanAction != null)
            m_SwitchToHitmanAction.performed += OnSwitchToHitmanPerformed;
    }

    private void OnDisable()
    {
        if (m_SwitchToHandlerAction != null)
            m_SwitchToHandlerAction.performed -= OnSwitchToHandlerPerformed;

        if (m_SwitchToHitmanAction != null)
            m_SwitchToHitmanAction.performed -= OnSwitchToHitmanPerformed;
    }

    private void Update()
    {
        // Intentionally left empty:
        // switching is handled by the TheHitman/Switch input action callback.
    }
    #endregion

    #region Private Methods
    private void CacheActionMapsAndActions()
    {
        if (m_InputActions == null)
            return;

        m_HitmanMap = m_InputActions.FindActionMap(c_HitmanMapName, throwIfNotFound: false);
        m_HandlerMap = m_InputActions.FindActionMap(c_HandlerMapName, throwIfNotFound: false);
        m_SwitchToHandlerAction = m_HitmanMap != null
            ? m_HitmanMap.FindAction(m_HitmanSwitchActionName, throwIfNotFound: false)
            : null;
        m_SwitchToHitmanAction = m_HandlerMap != null
            ? m_HandlerMap.FindAction(m_HandlerSwitchBackActionName, throwIfNotFound: false)
            : null;
    }

    private void OnSwitchToHandlerPerformed(InputAction.CallbackContext _context)
    {
        // Ignore if already in handler mode or if this switch action fires from a stale state.
        if (m_IsOverlayShown || m_HitmanMap == null || !m_HitmanMap.enabled)
            return;

        ToggleOverlayAndMaps();
    }

    private void OnSwitchToHitmanPerformed(InputAction.CallbackContext _context)
    {
        // Ignore if already in hitman mode or if this switch action fires from a stale state.
        if (!m_IsOverlayShown || m_HandlerMap == null || !m_HandlerMap.enabled)
            return;

        ToggleOverlayAndMaps();
    }

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

        if (m_HitmanMap == null || m_HandlerMap == null)
            CacheActionMapsAndActions();

        if (m_HitmanMap == null || m_HandlerMap == null)
            return;

        if (activeMapName == c_HandlerMapName)
        {
            m_HitmanMap.Disable();
            m_HandlerMap.Enable();
        }
        else
        {
            m_HandlerMap.Disable();
            m_HitmanMap.Enable();
        }
    }
    #endregion
}

