using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Loads a victory scene when the player uses the interact prompt on this object.
/// Requires a collider (trigger or solid) on the same GameObject or children so <see cref="PlayerInteractor"/> raycasts can hit it.
/// </summary>
public class VictorySceneInteractable : MonoBehaviour, IInteractable
{
    #region Inspector
    [Header("Visibility")]
    [Tooltip("When set, this interactable stays hidden until that NPC is killed.")]
    [SerializeField] private KillablePatrolTarget m_RequiredKillTarget;

    [Tooltip("Optional: mesh/collider child to enable when the target dies. Put this script on a parent, or leave empty to toggle renderers/colliders on this object.")]
    [SerializeField] private GameObject m_VisibilityRoot;

    [SerializeField] private string m_SceneName = "Victory_Scene";

    [Tooltip("If true, interaction only works once.")]
    [SerializeField] private bool m_OneTimeUse = true;

    [SerializeField] private string m_InteractionPrompt = "Press [E] to complete";

    [SerializeField] private UnityEvent m_OnVictoryTriggered;
    #endregion

    #region Public Properties
    public string InteractionPrompt => m_InteractionPrompt;

    public bool CanInteract =>
        m_IsRevealed
        && (!m_OneTimeUse || !m_HasBeenUsed);
    #endregion

    #region Private Fields
    private bool m_HasBeenUsed;
    private bool m_IsRevealed = true;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (m_RequiredKillTarget != null)
            m_IsRevealed = m_RequiredKillTarget.IsDead;
        else
            m_IsRevealed = true;
    }

    private void OnEnable()
    {
        if (m_RequiredKillTarget != null)
            m_RequiredKillTarget.Killed += OnRequiredTargetKilled;

        ApplyVisibility();
    }

    private void OnDisable()
    {
        if (m_RequiredKillTarget != null)
            m_RequiredKillTarget.Killed -= OnRequiredTargetKilled;
    }
    #endregion

    #region Private Methods
    private void OnRequiredTargetKilled()
    {
        m_IsRevealed = true;
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        bool visible = m_IsRevealed;

        if (m_VisibilityRoot != null)
        {
            if (m_VisibilityRoot == gameObject)
            {
                Debug.LogWarning(
                    $"{nameof(VictorySceneInteractable)}: {nameof(m_VisibilityRoot)} should be a child (not this object), or leave it empty. Toggling renderers/colliders instead.",
                    this);
                SetRenderersAndCollidersEnabled(visible);
                return;
            }

            m_VisibilityRoot.SetActive(visible);
            return;
        }

        SetRenderersAndCollidersEnabled(visible);
    }

    private void SetRenderersAndCollidersEnabled(bool _enabled)
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = _enabled;
        }

        var colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = _enabled;
        }
    }
    #endregion

    #region Public Methods
    public void Interact(GameObject interactor)
    {
        if (!m_IsRevealed)
            return;

        if (m_OneTimeUse && m_HasBeenUsed)
            return;

        if (string.IsNullOrEmpty(m_SceneName))
        {
            Debug.LogError($"{nameof(VictorySceneInteractable)} on {name}: scene name is empty.", this);
            return;
        }

        m_HasBeenUsed = true;
        m_OnVictoryTriggered?.Invoke();
        SceneManager.LoadScene(m_SceneName, LoadSceneMode.Single);
    }
    #endregion
}
