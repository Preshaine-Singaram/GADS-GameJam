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

    [Tooltip("Optional weapon root object for Fibre Wire. Enabled only when equipped.")]
    [SerializeField] private GameObject m_FibreWireRoot;

    [Tooltip("Optional weapon root object for Poison Vial. Enabled only when equipped.")]
    [SerializeField] private GameObject m_PoisonVialRoot;

    [Header("UI")]
    [Tooltip("TextMeshPro label used to show currently equipped weapon.")]
    [SerializeField] private TMP_Text m_EquippedWeaponLabel;

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

    [Tooltip("If true, this script enables/disables the assigned input actions in OnEnable/OnDisable.")]
    [SerializeField] private bool m_AutoEnableActions = true;

    [Tooltip("Optional fallback to direct keyboard polling (useful while wiring actions).")]
    [SerializeField] private bool m_AllowKeyboardFallback = false;
    #endregion

    #region Public Properties
    public WeaponType CurrentWeapon { get; private set; }
    #endregion

    #region Unity Lifecycle
    private void OnEnable()
    {
        if (!m_AutoEnableActions)
            return;

        m_Weapon1Action?.action.Enable();
        m_Weapon2Action?.action.Enable();
        m_Weapon3Action?.action.Enable();
    }

    private void OnDisable()
    {
        if (!m_AutoEnableActions)
            return;

        m_Weapon1Action?.action.Disable();
        m_Weapon2Action?.action.Disable();
        m_Weapon3Action?.action.Disable();
    }

    private void Start()
    {
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
        if (m_PistolRoot != null)
            m_PistolRoot.SetActive(CurrentWeapon == WeaponType.Pistol);

        if (m_FibreWireRoot != null)
            m_FibreWireRoot.SetActive(CurrentWeapon == WeaponType.FibreWire);

        if (m_PoisonVialRoot != null)
            m_PoisonVialRoot.SetActive(CurrentWeapon == WeaponType.PoisonVial);
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
    #endregion
}
