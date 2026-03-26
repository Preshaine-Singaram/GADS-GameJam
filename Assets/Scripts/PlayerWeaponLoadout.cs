using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Handles weapon selection input and simple equipped-weapon UI feedback.
/// Weapon functionality (shooting/attacking/poisoning) is intentionally not included.
/// </summary>
public class PlayerWeaponLoadout : MonoBehaviour
{
    #region Enums
    public enum WeaponType
    {
        Pistol = 0,
        FibreWire = 1,
        PoisonVial = 2
    }
    #endregion

    #region Inspector
    [Header("Weapon Visual Roots (optional)")]
    [Tooltip("Optional weapon root object for Pistol. Enabled only when equipped.")]
    [SerializeField] private GameObject m_PistolRoot;

    [Tooltip("Optional pistol prefab to spawn under Pistol Mount at runtime.")]
    [SerializeField] private GameObject m_PistolPrefab;

    [Tooltip("Parent transform where the pistol prefab should be spawned.")]
    [SerializeField] private Transform m_PistolMount;

    [Tooltip("Optional weapon root object for Fibre Wire. Enabled only when equipped.")]
    [SerializeField] private GameObject m_FibreWireRoot;

    [Tooltip("Optional weapon root object for Poison Vial. Enabled only when equipped.")]
    [SerializeField] private GameObject m_PoisonVialRoot;

    [Header("UI")]
    [Tooltip("TextMeshPro label used to show currently equipped weapon.")]
    [SerializeField] private TMP_Text m_EquippedWeaponLabel;

    [Tooltip("Crosshair UI root shown only while the pistol is equipped.")]
    [SerializeField] private GameObject m_CrosshairRoot;

    [Header("Defaults")]
    [Tooltip("Weapon equipped on start.")]
    [SerializeField] private WeaponType m_StartingWeapon = WeaponType.Pistol;

    [Header("Input Actions (New Input System)")]
    [Tooltip("Button action for weapon slot 1 (Pistol).")]
    [SerializeField] private InputActionReference m_Weapon1Action;

    [Tooltip("Button action for weapon slot 2 (Fibre Wire).")]
    [SerializeField] private InputActionReference m_Weapon2Action;

    [Tooltip("Button action for weapon slot 3 (Poison).")]
    [SerializeField] private InputActionReference m_Weapon3Action;

    [Tooltip("Button action for firing the equipped weapon (for pistol, usually left mouse).")]
    [SerializeField] private InputActionReference m_FireAction;

    [Tooltip("If true, this script enables/disables the assigned input actions in OnEnable/OnDisable.")]
    [SerializeField] private bool m_AutoEnableActions = true;

    [Tooltip("Optional fallback to direct keyboard polling (useful while wiring actions).")]
    [SerializeField] private bool m_AllowKeyboardFallback = false;

    [Header("Pistol (Hitscan)")]
    [Tooltip("Origin point for pistol raycast. If null, uses Camera.main.")]
    [SerializeField] private Transform m_PistolMuzzleOrRayOrigin;

    [Tooltip("Maximum pistol hitscan distance.")]
    [SerializeField] private float m_PistolRange = 150f;

    [Tooltip("Layers that can be hit by pistol hitscan.")]
    [SerializeField] private LayerMask m_PistolHitLayers = ~0;

    [Tooltip("Only objects with this tag are considered valid kill targets.")]
    [SerializeField] private string m_TargetTag = "Target";

    [Tooltip("If true, destroy the rigidbody root object when target is hit.")]
    [SerializeField] private bool m_DestroyRigidbodyRoot = true;
    #endregion

    #region Public Properties
    public WeaponType CurrentWeapon { get; private set; }
    #endregion

    #region Private Fields
    private GameObject m_PistolRuntimeInstance;
    #endregion

    #region Unity Lifecycle
    private void OnEnable()
    {
        if (!m_AutoEnableActions)
            return;

        m_Weapon1Action?.action.Enable();
        m_Weapon2Action?.action.Enable();
        m_Weapon3Action?.action.Enable();
        m_FireAction?.action.Enable();
    }

    private void OnDisable()
    {
        if (!m_AutoEnableActions)
            return;

        m_Weapon1Action?.action.Disable();
        m_Weapon2Action?.action.Disable();
        m_Weapon3Action?.action.Disable();
        m_FireAction?.action.Disable();
    }

    private void Start()
    {
        EnsurePistolVisualReference();
        EquipWeapon(m_StartingWeapon);
    }

    private void Update()
    {
        if (WasWeaponOnePressed())
            EquipWeapon(WeaponType.Pistol);
        else if (WasWeaponTwoPressed())
            EquipWeapon(WeaponType.FibreWire);
        else if (WasWeaponThreePressed())
            EquipWeapon(WeaponType.PoisonVial);

        if (CurrentWeapon == WeaponType.Pistol && WasFirePressed())
            FirePistolHitscan();
    }
    #endregion

    #region Public Methods
    public void EquipWeapon(WeaponType weaponToEquip)
    {
        CurrentWeapon = weaponToEquip;
        ApplyWeaponVisualState();
        UpdateEquippedWeaponLabel();
    }
    #endregion

    #region Private Methods
    private bool WasWeaponOnePressed()
    {
        if (m_Weapon1Action != null && m_Weapon1Action.action != null && m_Weapon1Action.action.WasPressedThisFrame())
            return true;

        if (!m_AllowKeyboardFallback || Keyboard.current == null)
            return false;

        return Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame;
    }

    private bool WasWeaponTwoPressed()
    {
        if (m_Weapon2Action != null && m_Weapon2Action.action != null && m_Weapon2Action.action.WasPressedThisFrame())
            return true;

        if (!m_AllowKeyboardFallback || Keyboard.current == null)
            return false;

        return Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame;
    }

    private bool WasWeaponThreePressed()
    {
        if (m_Weapon3Action != null && m_Weapon3Action.action != null && m_Weapon3Action.action.WasPressedThisFrame())
            return true;

        if (!m_AllowKeyboardFallback || Keyboard.current == null)
            return false;

        return Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame;
    }

    private void ApplyWeaponVisualState()
    {
        EnsurePistolVisualReference();
        bool pistolEquipped = CurrentWeapon == WeaponType.Pistol;

        if (m_PistolRoot != null)
            m_PistolRoot.SetActive(pistolEquipped);

        if (m_FibreWireRoot != null)
            m_FibreWireRoot.SetActive(CurrentWeapon == WeaponType.FibreWire);

        if (m_PoisonVialRoot != null)
            m_PoisonVialRoot.SetActive(CurrentWeapon == WeaponType.PoisonVial);

        if (m_CrosshairRoot != null)
            m_CrosshairRoot.SetActive(pistolEquipped);
    }

    private void UpdateEquippedWeaponLabel()
    {
        if (m_EquippedWeaponLabel == null)
            return;

        m_EquippedWeaponLabel.text = $"Equipped: {GetWeaponDisplayName(CurrentWeapon)}";
    }

    private static string GetWeaponDisplayName(WeaponType weaponType)
    {
        switch (weaponType)
        {
            case WeaponType.Pistol:
                return "Pistol";
            case WeaponType.FibreWire:
                return "Fibre Wire";
            case WeaponType.PoisonVial:
                return "Poison";
            default:
                return "Unknown";
        }
    }

    private bool WasFirePressed()
    {
        if (m_FireAction != null && m_FireAction.action != null && m_FireAction.action.WasPressedThisFrame())
            return true;

        if (!m_AllowKeyboardFallback || Mouse.current == null)
            return false;

        return Mouse.current.leftButton.wasPressedThisFrame;
    }

    private void FirePistolHitscan()
    {
        Camera activeCamera = Camera.main;
        if (activeCamera == null && m_PistolMuzzleOrRayOrigin == null)
            return;

        Vector3 origin;
        Vector3 direction;

        if (activeCamera != null)
        {
            origin = activeCamera.transform.position;
            direction = activeCamera.transform.forward;
        }
        else
        {
            origin = m_PistolMuzzleOrRayOrigin.position;
            direction = m_PistolMuzzleOrRayOrigin.forward;
        }

        if (!Physics.Raycast(origin, direction, out RaycastHit hitInfo, m_PistolRange, m_PistolHitLayers, QueryTriggerInteraction.Ignore))
            return;

        if (!string.IsNullOrWhiteSpace(m_TargetTag) && !hitInfo.collider.CompareTag(m_TargetTag))
            return;

        GameObject targetToDestroy = hitInfo.collider.gameObject;
        if (m_DestroyRigidbodyRoot && hitInfo.rigidbody != null)
            targetToDestroy = hitInfo.rigidbody.gameObject;

        Destroy(targetToDestroy);
    }

    private void EnsurePistolVisualReference()
    {
        // If m_PistolRoot points to a prefab asset (not a scene instance), ignore it and spawn a runtime instance.
        if (m_PistolRoot != null && !m_PistolRoot.scene.IsValid())
            m_PistolRoot = null;

        if (m_PistolRoot != null)
            return;

        if (m_PistolPrefab == null)
            return;

        Transform pistolParent = m_PistolMount != null ? m_PistolMount : transform;
        m_PistolRuntimeInstance = Instantiate(m_PistolPrefab, pistolParent);
        m_PistolRuntimeInstance.name = m_PistolPrefab.name;
        m_PistolRuntimeInstance.transform.localPosition = Vector3.zero;
        m_PistolRuntimeInstance.transform.localRotation = Quaternion.identity;
        m_PistolRuntimeInstance.transform.localScale = Vector3.one;

        m_PistolRoot = m_PistolRuntimeInstance;
    }

    private void SetPistolVisualVisible(bool _visible)
    {
        if (m_PistolRoot == null)
            return;

        if (m_PistolRoot.activeSelf != _visible)
            m_PistolRoot.SetActive(_visible);
    }
    #endregion
}
