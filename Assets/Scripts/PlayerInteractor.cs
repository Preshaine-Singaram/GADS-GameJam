using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Detects interactable targets in front of the camera and shows a prompt while aiming at them.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    #region Inspector
    [Header("Raycast")]
    [Tooltip("Optional camera used for interaction raycast. If null, uses Camera.main.")]
    [SerializeField] private Camera m_RaycastCamera;

    [Tooltip("Maximum interaction distance.")]
    [SerializeField] private float m_InteractDistance = 3.5f;

    [Tooltip("Layers that can be considered for interaction raycasts.")]
    [SerializeField] private LayerMask m_InteractMask = ~0;

    [Header("Input")]
    [Tooltip("Input action for interacting (TheHitman/Interact).")]
    [SerializeField] private InputActionReference m_InteractAction;

    [Tooltip("If true, this script enables/disables m_InteractAction in OnEnable/OnDisable.")]
    [SerializeField] private bool m_AutoEnableAction = true;

    [Tooltip("Optional fallback that allows E key interaction even if action reference is not wired.")]
    [SerializeField] private bool m_AllowKeyboardFallback = true;

    [Header("Prompt UI")]
    [Tooltip("Optional root object that is shown/hidden with prompt visibility.")]
    [SerializeField] private GameObject m_PromptRoot;

    [Tooltip("Optional text field used to display prompt text.")]
    [SerializeField] private TMP_Text m_PromptLabel;

    [Tooltip("Default prompt text when interactable does not provide one.")]
    [SerializeField] private string m_DefaultPromptText = "Press [E] to interact";
    #endregion

    #region Private Fields
    private IInteractable m_CurrentInteractable;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (m_RaycastCamera == null)
            m_RaycastCamera = Camera.main;

        EnsurePromptUI();
        SetPromptVisible(false, string.Empty);
    }

    private void OnEnable()
    {
        if (m_AutoEnableAction)
            m_InteractAction?.action.Enable();
    }

    private void OnDisable()
    {
        if (m_AutoEnableAction)
            m_InteractAction?.action.Disable();

        m_CurrentInteractable = null;
        SetPromptVisible(false, string.Empty);
    }

    private void Update()
    {
        RefreshCurrentInteractable();
        HandleInteractInput();
    }
    #endregion

    #region Private Methods
    private void RefreshCurrentInteractable()
    {
        if (!TryGetLookedAtInteractable(out IInteractable interactable))
        {
            m_CurrentInteractable = null;
            SetPromptVisible(false, string.Empty);
            return;
        }

        if (!interactable.CanInteract)
        {
            m_CurrentInteractable = null;
            SetPromptVisible(false, string.Empty);
            return;
        }

        m_CurrentInteractable = interactable;
        string promptText = string.IsNullOrWhiteSpace(interactable.InteractionPrompt)
            ? m_DefaultPromptText
            : interactable.InteractionPrompt;
        SetPromptVisible(true, promptText);
    }

    private void HandleInteractInput()
    {
        if (m_CurrentInteractable == null)
            return;

        bool interactPressed = m_InteractAction != null
                               && m_InteractAction.action != null
                               && m_InteractAction.action.WasPressedThisFrame();
        if (!interactPressed && m_AllowKeyboardFallback && Keyboard.current != null)
            interactPressed = Keyboard.current.eKey.wasPressedThisFrame;

        if (!interactPressed)
            return;

        m_CurrentInteractable.Interact(gameObject);
    }

    private bool TryGetLookedAtInteractable(out IInteractable interactable)
    {
        interactable = null;

        Camera activeCamera = m_RaycastCamera != null ? m_RaycastCamera : Camera.main;
        if (activeCamera == null)
            return false;

        Ray ray = activeCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Max(0.01f, m_InteractDistance), m_InteractMask, QueryTriggerInteraction.Collide))
            return false;

        MonoBehaviour[] behaviours = hit.collider.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IInteractable found)
            {
                interactable = found;
                return true;
            }
        }

        return false;
    }

    private void SetPromptVisible(bool isVisible, string text)
    {
        if (m_PromptRoot != null)
            m_PromptRoot.SetActive(isVisible);

        if (m_PromptLabel != null)
        {
            m_PromptLabel.gameObject.SetActive(isVisible);
            if (isVisible)
                m_PromptLabel.text = text;
        }
    }

    private void EnsurePromptUI()
    {
        if (m_PromptLabel != null)
            return;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("InteractionPromptCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        GameObject promptGo = new GameObject("InteractionPrompt", typeof(RectTransform), typeof(TextMeshProUGUI));
        promptGo.transform.SetParent(canvas.transform, false);

        RectTransform rt = promptGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 72f);
        rt.sizeDelta = new Vector2(900f, 80f);

        TextMeshProUGUI tmp = promptGo.GetComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 36f;
        tmp.color = Color.white;
        tmp.text = string.Empty;

        m_PromptLabel = tmp;
        m_PromptRoot = promptGo;
    }
    #endregion
}
