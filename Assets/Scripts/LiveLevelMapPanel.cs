using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Renders a live top-down "map" of the level into a UI panel via RenderTexture.
/// Optionally places a player marker relative to the chosen world bounds.
/// </summary>
[DisallowMultipleComponent]
public class LiveLevelMapPanel : MonoBehaviour
{
    #region Inspector
    [Header("UI")]
    [Tooltip("Panel/container RectTransform to fill with the map. If null, can auto-find by name.")]
    [SerializeField] private RectTransform m_MapPanel;

    [Tooltip("RawImage that displays the RenderTexture. If null, a RawImage is created under m_MapPanel.")]
    [SerializeField] private RawImage m_MapRawImage;

    [Tooltip("If m_MapPanel is null, tries to find a RectTransform with this GameObject name.")]
    [SerializeField] private bool m_AutoFindMapPanel = true;
    [SerializeField] private string m_AutoFindMapPanelName = "HandlerScreen_Panel";

    [Tooltip("Optional marker RectTransform to show player position on the map.")]
    [SerializeField] private RectTransform m_PlayerMarker;

    [Tooltip("Player transform whose position is mapped into UI coordinates.")]
    [SerializeField] private Transform m_Player;
    #endregion

    #region Door Controls
    [Header("Doors")]
    [Tooltip("Optional root containing door GameObjects to show/toggle from the map screen.")]
    [SerializeField] private Transform m_DoorsRoot;

    [Tooltip("If m_DoorsRoot is null, tries to find a Transform by this GameObject name.")]
    [SerializeField] private bool m_AutoFindDoorsRoot = true;
    [SerializeField] private string m_AutoFindDoorsRootName = "Doors";

    [Tooltip("When enabled, pressing the toggle key while map panel is visible will enable/disable all tracked doors.")]
    [SerializeField] private bool m_AllowDoorToggleFromMap = true;

    [Tooltip("Keyboard key used to toggle mesh/collider on the selected door while map screen is visible.")]
    [SerializeField] private Key m_ToggleDoorsKey = Key.T;

    [Tooltip("Marker size in UI units for each door on the map.")]
    [SerializeField] private Vector2 m_DoorMarkerSize = new Vector2(18f, 18f);

    [Tooltip("Default marker color for unselected doors.")]
    [SerializeField] private Color m_DoorMarkerColor = new Color(0.95f, 0.3f, 0.3f, 0.95f);

    [Tooltip("Marker color for the currently selected door.")]
    [SerializeField] private Color m_SelectedDoorMarkerColor = new Color(1f, 0.95f, 0.25f, 1f);

    [Header("Handler Input Actions")]
    [Tooltip("TheHandler/select action used to select a door marker under the cursor.")]
    [SerializeField] private InputActionReference m_SelectDoorAction;

    [Tooltip("TheHandler/Activate action used to toggle selected door visibility/collision.")]
    [SerializeField] private InputActionReference m_ActivateDoorAction;

    [Tooltip("If true, this component enables/disables handler actions with its lifecycle.")]
    [SerializeField] private bool m_AutoEnableHandlerActions = true;

    [Tooltip("Max cursor distance (UI units) from a marker to allow selection.")]
    [SerializeField] private float m_SelectDoorMaxDistance = 32f;

    [Header("Handler Unlock Gate")]
    [Tooltip("If true, Select/Activate controls stay disabled until UnlockHandlerControls() is called.")]
    [SerializeField] private bool m_RequireUnlockForHandlerControls = true;

    [Tooltip("Initial runtime state for handler controls when unlock gating is enabled.")]
    [SerializeField] private bool m_HandlerControlsUnlockedAtStart = false;
    #endregion

    #region Roof Controls
    [Header("Roofs")]
    [Tooltip("Optional root containing roof objects that should be hidden while the map panel is visible.")]
    [SerializeField] private Transform m_RoofsRoot;

    [Tooltip("If m_RoofsRoot is null, tries to find a Transform by this GameObject name.")]
    [SerializeField] private bool m_AutoFindRoofsRoot = true;
    [SerializeField] private string m_AutoFindRoofsRootName = "Roofs";

    [Tooltip("When enabled, roof renderers under m_RoofsRoot are hidden while the map panel is open.")]
    [SerializeField] private bool m_HideRoofsWhileMapVisible = true;
    #endregion

    #region RenderTexture
    [Header("Render Texture")]
    [Tooltip("RenderTexture width.")]
    [SerializeField, Range(64, 2048)] private int m_TextureWidth = 512;

    [Tooltip("RenderTexture height.")]
    [SerializeField, Range(64, 2048)] private int m_TextureHeight = 512;

    [Tooltip("Depth buffer bits for the camera render.")]
    [SerializeField, Range(0, 24)] private int m_DepthBufferBits = 16;

    [Tooltip("Optional anti-aliasing for the camera render.")]
    [SerializeField, Range(1, 8)] private int m_AntiAliasing = 1;

    [Tooltip("If true, disables the map camera when the panel is hidden to save GPU time.")]
    [SerializeField] private bool m_RenderOnlyWhenPanelVisible = true;
    #endregion

    #region World Bounds / Camera
    [Header("World Bounds & Camera")]
    [Tooltip("Top-down orthographic camera used for the map.")]
    [SerializeField] private Camera m_MapCamera;

    [Tooltip("If provided, combined Renderer bounds under this root are used to fit the map.")]
    [SerializeField] private Transform m_BoundsRoot;

    [Tooltip("If true, includes inactive renderers when computing bounds.")]
    [SerializeField] private bool m_IncludeInactiveRenderers = true;

    [Tooltip("Extra padding added to computed bounds (world units).")]
    [SerializeField] private float m_BoundsPadding = 2f;

    [Tooltip("Vertical distance above the computed bounds' top used to position the map camera.")]
    [SerializeField] private float m_CameraHeightAboveBounds = 50f;

    [Tooltip("Extra margin multiplier applied to orthographic size calculations.")]
    [SerializeField, Range(0.8f, 2f)] private float m_OrthographicPaddingMultiplier = 1.05f;

    [Tooltip("Optional layer mask for what the map camera renders.")]
    [SerializeField] private LayerMask m_MapCullingMask = ~0;
    #endregion

    #region Private State
    private RenderTexture m_RenderTexture;
    private bool m_IsInitialized;
    private Bounds m_WorldBounds;
    private readonly Vector3[] m_MapWorldCorners = new Vector3[4];
    private DoorEntry[] m_DoorEntries = Array.Empty<DoorEntry>();
    private RectTransform m_DoorMarkersRoot;
    private int m_SelectedDoorIndex = -1;
    private RoofRendererState[] m_RoofRendererStates = Array.Empty<RoofRendererState>();
    private bool m_AreRoofsHiddenForMap;
    private bool m_IsHandlerControlsUnlocked;

    private sealed class DoorEntry
    {
        public Transform DoorRoot;
        public Renderer[] Renderers;
        public Collider[] Colliders;
        public RectTransform MarkerRect;
        public Image MarkerImage;
        public bool IsEnabled = true;
    }

    private struct RoofRendererState
    {
        public Renderer Renderer;
        public bool WasEnabled;
    }
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        m_IsHandlerControlsUnlocked = !m_RequireUnlockForHandlerControls || m_HandlerControlsUnlockedAtStart;

        if (m_MapCamera == null)
            m_MapCamera = GetComponentInChildren<Camera>(true);

        if (m_MapPanel == null && m_AutoFindMapPanel)
            m_MapPanel = FindMapPanelByName(m_AutoFindMapPanelName);

        if (m_DoorsRoot == null && m_AutoFindDoorsRoot)
            m_DoorsRoot = FindTransformByName(m_AutoFindDoorsRootName);

        if (m_RoofsRoot == null && m_AutoFindRoofsRoot)
            m_RoofsRoot = FindTransformByName(m_AutoFindRoofsRootName);

        InitializeIfNeeded();
    }

    private void OnEnable()
    {
        InitializeIfNeeded();
        ApplyCameraAndTextureState();
        RefreshHandlerActionState();
    }

    private void Update()
    {
        if (!m_IsInitialized)
            return;

        if (m_RenderOnlyWhenPanelVisible)
            ApplyCameraAndTextureState();

        UpdateDoorMarkerPositions();
        HandleDoorSelectionInput();
        HandleDoorToggleInput();
        UpdatePlayerMarker();
    }

    private void OnDisable()
    {
        if (!m_IsInitialized)
            return;

        SetRoofsHiddenForMap(false);

        if (m_RenderOnlyWhenPanelVisible && m_MapCamera != null)
            m_MapCamera.enabled = false;
        DisableHandlerActions();
    }

    private void OnDestroy()
    {
        SetRoofsHiddenForMap(false);

        if (m_RenderTexture != null)
        {
            m_RenderTexture.Release();
            m_RenderTexture = null;
        }

        if (m_DoorMarkersRoot != null)
            Destroy(m_DoorMarkersRoot.gameObject);
    }
    #endregion

    #region Initialization
    private void InitializeIfNeeded()
    {
        if (m_IsInitialized)
            return;

        if (m_MapCamera == null)
        {
            Debug.LogWarning($"{nameof(LiveLevelMapPanel)} on '{name}' has no map camera assigned.");
            m_IsInitialized = true;
            return;
        }

        if (m_MapPanel == null)
        {
            Debug.LogWarning($"{nameof(LiveLevelMapPanel)} on '{name}' has no UI panel assigned.");
            m_IsInitialized = true;
            return;
        }

        if (m_MapRawImage == null)
        {
            m_MapRawImage = CreateRawImageUnderPanel(m_MapPanel);
        }

        if (!TryComputeWorldBounds(out m_WorldBounds))
        {
            Debug.LogWarning($"{nameof(LiveLevelMapPanel)} could not compute bounds; using a default centered bounds.");
            m_WorldBounds = new Bounds(Vector3.zero, new Vector3(50f, 50f, 50f));
        }

        CacheDoorObjects();
        CacheRoofRenderers();
        SetupMapCameraTopDown();
        SetupRenderTexture();
        BuildDoorMarkers();
        m_IsInitialized = true;
    }
    #endregion

    #region Camera / Texture Setup
    private void SetupMapCameraTopDown()
    {
        if (m_MapCamera == null)
            return;

        m_MapCamera.cullingMask = m_MapCullingMask | GetDoorLayersMask();

        // Ensure this camera renders as a true top-down map.
        if (!m_MapCamera.orthographic)
            m_MapCamera.orthographic = true;

        m_MapCamera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);

        // Place camera above the bounds.
        float y = m_WorldBounds.max.y + Mathf.Max(0f, m_CameraHeightAboveBounds);
        m_MapCamera.transform.position = new Vector3(m_WorldBounds.center.x, y, m_WorldBounds.center.z);

        // Fit orthographic size using bounds and expected render aspect.
        float rtAspect = Mathf.Max(0.01f, m_TextureWidth / (float)m_TextureHeight);
        m_MapCamera.aspect = rtAspect;

        Vector3 size = m_WorldBounds.size;
        float halfZ = 0.5f * size.z;
        float halfX = 0.5f * size.x;

        float orthographicHalfHeightNeeded = halfZ;
        float orthographicHalfWidthNeeded = halfX;

        // For an orthographic camera:
        // - vertical span = 2 * orthographicSize
        // - horizontal span = 2 * orthographicSize * aspect
        float orthographicSizeByHeight = orthographicHalfHeightNeeded;
        float orthographicSizeByWidth = orthographicHalfWidthNeeded / rtAspect;
        float orthographicSize = Mathf.Max(orthographicSizeByHeight, orthographicSizeByWidth);

        orthographicSize *= Mathf.Max(0.01f, m_OrthographicPaddingMultiplier);
        m_MapCamera.orthographicSize = Mathf.Max(0.01f, orthographicSize);
    }

    private void SetupRenderTexture()
    {
        if (m_RenderTexture != null)
        {
            m_RenderTexture.Release();
            m_RenderTexture = null;
        }

        m_RenderTexture = new RenderTexture(
            Mathf.Max(64, m_TextureWidth),
            Mathf.Max(64, m_TextureHeight),
            Mathf.Max(0, m_DepthBufferBits),
            RenderTextureFormat.ARGB32
        )
        {
            name = $"{nameof(LiveLevelMapPanel)}_RT",
            antiAliasing = Mathf.Max(1, m_AntiAliasing),
            useMipMap = false
        };

        m_RenderTexture.Create();

        m_MapCamera.targetTexture = m_RenderTexture;
        m_MapRawImage.texture = m_RenderTexture;
    }

    private void ApplyCameraAndTextureState()
    {
        if (m_MapCamera == null || m_MapPanel == null)
            return;

        bool shouldRender = isActiveAndEnabled && m_MapPanel.gameObject.activeInHierarchy;
        m_MapCamera.enabled = shouldRender;
        SetRoofsHiddenForMap(shouldRender);
    }
    #endregion

    #region Door Controls
    public void ToggleDoorsActive()
    {
        if (!AreHandlerControlsAvailable())
            return;

        ToggleSelectedDoorEnabled();
    }

    public void ToggleSelectedDoorEnabled()
    {
        if (!AreHandlerControlsAvailable())
            return;

        if (m_SelectedDoorIndex < 0 || m_SelectedDoorIndex >= m_DoorEntries.Length)
            return;

        DoorEntry entry = m_DoorEntries[m_SelectedDoorIndex];
        SetDoorEnabled(entry, !entry.IsEnabled);
    }

    private void HandleDoorToggleInput()
    {
        if (!m_AllowDoorToggleFromMap || !IsMapPanelVisible() || !AreHandlerControlsAvailable())
            return;

        bool activatePressed = m_ActivateDoorAction != null
            && m_ActivateDoorAction.action != null
            && m_ActivateDoorAction.action.WasPressedThisFrame();
        bool keyboardFallbackPressed = Keyboard.current != null && Keyboard.current[m_ToggleDoorsKey].wasPressedThisFrame;

        if (!activatePressed && !keyboardFallbackPressed)
            return;

        ToggleSelectedDoorEnabled();
    }

    private void HandleDoorSelectionInput()
    {
        if (!IsMapPanelVisible() || !AreHandlerControlsAvailable())
            return;

        bool selectPressed = m_SelectDoorAction != null
            && m_SelectDoorAction.action != null
            && m_SelectDoorAction.action.WasPressedThisFrame();
        if (!selectPressed)
            return;

        TrySelectDoorAtCursor();
    }

    private void TrySelectDoorAtCursor()
    {
        if (m_DoorEntries == null || m_DoorEntries.Length == 0 || m_DoorMarkersRoot == null || Mouse.current == null)
            return;

        Canvas panelCanvas = m_MapPanel != null ? m_MapPanel.GetComponentInParent<Canvas>() : null;
        Camera uiCamera = null;
        if (panelCanvas != null && panelCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            uiCamera = panelCanvas.worldCamera;

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_DoorMarkersRoot, mouseScreenPosition, uiCamera, out Vector2 cursorLocalPoint))
            return;

        float maxDistance = Mathf.Max(1f, m_SelectDoorMaxDistance);
        float bestDistanceSqr = maxDistance * maxDistance;
        int bestIndex = -1;

        for (int i = 0; i < m_DoorEntries.Length; i++)
        {
            DoorEntry entry = m_DoorEntries[i];
            if (entry == null || entry.MarkerRect == null || !entry.MarkerRect.gameObject.activeInHierarchy)
                continue;

            float distanceSqr = (entry.MarkerRect.anchoredPosition - cursorLocalPoint).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
                continue;

            bestDistanceSqr = distanceSqr;
            bestIndex = i;
        }

        if (bestIndex >= 0)
            SelectDoor(bestIndex);
    }

    private void CacheDoorObjects()
    {
        if (m_DoorsRoot == null)
        {
            m_DoorEntries = Array.Empty<DoorEntry>();
            return;
        }

        int childCount = m_DoorsRoot.childCount;
        if (childCount <= 0)
        {
            m_DoorEntries = Array.Empty<DoorEntry>();
            return;
        }

        m_DoorEntries = new DoorEntry[childCount];
        int index = 0;
        for (int i = 0; i < childCount; i++)
        {
            Transform doorTransform = m_DoorsRoot.GetChild(i);
            if (doorTransform == null)
                continue;

            DoorEntry entry = new DoorEntry
            {
                DoorRoot = doorTransform,
                Renderers = doorTransform.GetComponentsInChildren<Renderer>(true),
                Colliders = doorTransform.GetComponentsInChildren<Collider>(true),
                IsEnabled = IsDoorEnabledByComponents(doorTransform)
            };

            m_DoorEntries[index] = entry;
            index++;
        }

        if (index < m_DoorEntries.Length)
            Array.Resize(ref m_DoorEntries, index);

        if (m_DoorEntries.Length > 0)
            SelectDoor(0);
    }

    private void BuildDoorMarkers()
    {
        if (m_MapPanel == null)
            return;

        if (m_DoorMarkersRoot != null)
            Destroy(m_DoorMarkersRoot.gameObject);

        GameObject rootGo = new GameObject("DoorMarkers", typeof(RectTransform));
        rootGo.transform.SetParent(m_MapPanel, false);
        m_DoorMarkersRoot = rootGo.GetComponent<RectTransform>();
        m_DoorMarkersRoot.anchorMin = Vector2.zero;
        m_DoorMarkersRoot.anchorMax = Vector2.one;
        m_DoorMarkersRoot.offsetMin = Vector2.zero;
        m_DoorMarkersRoot.offsetMax = Vector2.zero;
        m_DoorMarkersRoot.anchoredPosition = Vector2.zero;
        m_DoorMarkersRoot.SetSiblingIndex(m_MapPanel.childCount - 1);

        for (int i = 0; i < m_DoorEntries.Length; i++)
        {
            DoorEntry entry = m_DoorEntries[i];
            if (entry == null || entry.DoorRoot == null)
                continue;

            GameObject markerGo = new GameObject($"DoorMarker_{i:D2}", typeof(RectTransform), typeof(Image), typeof(Button));
            markerGo.transform.SetParent(m_DoorMarkersRoot, false);

            RectTransform markerRect = markerGo.GetComponent<RectTransform>();
            markerRect.anchorMin = new Vector2(0.5f, 0.5f);
            markerRect.anchorMax = new Vector2(0.5f, 0.5f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = m_DoorMarkerSize;

            Image markerImage = markerGo.GetComponent<Image>();
            markerImage.color = m_DoorMarkerColor;

            int localIndex = i;
            Button button = markerGo.GetComponent<Button>();
            button.onClick.AddListener(() => SelectDoor(localIndex));

            entry.MarkerRect = markerRect;
            entry.MarkerImage = markerImage;
        }

        UpdateDoorSelectionVisuals();
    }

    public void SelectDoor(int doorIndex)
    {
        if (m_DoorEntries == null || m_DoorEntries.Length == 0)
        {
            m_SelectedDoorIndex = -1;
            return;
        }

        m_SelectedDoorIndex = Mathf.Clamp(doorIndex, 0, m_DoorEntries.Length - 1);
        UpdateDoorSelectionVisuals();
    }

    public void SetSelectedDoorEnabled(bool isEnabled)
    {
        if (!AreHandlerControlsAvailable())
            return;

        if (m_SelectedDoorIndex < 0 || m_SelectedDoorIndex >= m_DoorEntries.Length)
            return;

        SetDoorEnabled(m_DoorEntries[m_SelectedDoorIndex], isEnabled);
    }

    private void UpdateDoorSelectionVisuals()
    {
        if (m_DoorEntries == null)
            return;

        for (int i = 0; i < m_DoorEntries.Length; i++)
        {
            DoorEntry entry = m_DoorEntries[i];
            if (entry?.MarkerImage == null)
                continue;

            entry.MarkerImage.color = i == m_SelectedDoorIndex ? m_SelectedDoorMarkerColor : m_DoorMarkerColor;
        }
    }

    private void UpdateDoorMarkerPositions()
    {
        if (m_DoorEntries == null || m_DoorEntries.Length == 0 || m_MapRawImage == null)
            return;

        for (int i = 0; i < m_DoorEntries.Length; i++)
        {
            DoorEntry entry = m_DoorEntries[i];
            if (entry == null || entry.DoorRoot == null || entry.MarkerRect == null)
                continue;

            if (!TryGetMapAnchoredPosition(entry.DoorRoot.position, out Vector2 anchoredPos, out bool inFront))
            {
                entry.MarkerRect.gameObject.SetActive(false);
                continue;
            }

            entry.MarkerRect.anchoredPosition = anchoredPos;
            entry.MarkerRect.gameObject.SetActive(inFront);
        }
    }

    private bool TryGetMapAnchoredPosition(Vector3 worldPosition, out Vector2 anchoredPosition, out bool inFront)
    {
        anchoredPosition = Vector2.zero;
        inFront = false;

        if (m_MapCamera == null || m_MapRawImage == null)
            return false;

        Vector3 viewport = m_MapCamera.WorldToViewportPoint(worldPosition);
        inFront = viewport.z >= 0f;

        float vx = Mathf.Clamp01(viewport.x);
        float vy = Mathf.Clamp01(viewport.y);

        RectTransform mapRect = m_MapRawImage.rectTransform;
        Rect rect = mapRect.rect;
        anchoredPosition = new Vector2(
            (vx - mapRect.pivot.x) * rect.width,
            (vy - mapRect.pivot.y) * rect.height
        );

        return true;
    }

    private static bool IsDoorEnabledByComponents(Transform doorTransform)
    {
        if (doorTransform == null)
            return false;

        Renderer[] renderers = doorTransform.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].enabled)
                return true;
        }

        Collider[] colliders = doorTransform.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].enabled)
                return true;
        }

        return false;
    }

    private static void SetDoorEnabled(DoorEntry entry, bool enabled)
    {
        if (entry == null)
            return;

        if (entry.Renderers != null)
        {
            for (int i = 0; i < entry.Renderers.Length; i++)
            {
                if (entry.Renderers[i] != null)
                    entry.Renderers[i].enabled = enabled;
            }
        }

        if (entry.Colliders != null)
        {
            for (int i = 0; i < entry.Colliders.Length; i++)
            {
                if (entry.Colliders[i] != null)
                    entry.Colliders[i].enabled = enabled;
            }
        }

        entry.IsEnabled = enabled;
    }

    private int GetDoorLayersMask()
    {
        if (m_DoorsRoot == null)
            return 0;

        Renderer[] doorRenderers = m_DoorsRoot.GetComponentsInChildren<Renderer>(true);
        int layersMask = 0;
        for (int i = 0; i < doorRenderers.Length; i++)
        {
            if (doorRenderers[i] == null)
                continue;

            layersMask |= 1 << doorRenderers[i].gameObject.layer;
        }

        return layersMask;
    }

    private bool IsMapPanelVisible()
    {
        return m_MapPanel != null && m_MapPanel.gameObject.activeInHierarchy;
    }

    private bool AreHandlerControlsAvailable()
    {
        return !m_RequireUnlockForHandlerControls || m_IsHandlerControlsUnlocked;
    }

    private void RefreshHandlerActionState()
    {
        if (!m_AutoEnableHandlerActions)
            return;

        if (isActiveAndEnabled && AreHandlerControlsAvailable())
        {
            m_SelectDoorAction?.action.Enable();
            m_ActivateDoorAction?.action.Enable();
            return;
        }

        DisableHandlerActions();
    }

    private void DisableHandlerActions()
    {
        if (!m_AutoEnableHandlerActions)
            return;

        m_SelectDoorAction?.action.Disable();
        m_ActivateDoorAction?.action.Disable();
    }

    private void CacheRoofRenderers()
    {
        if (m_RoofsRoot == null)
        {
            m_RoofRendererStates = Array.Empty<RoofRendererState>();
            return;
        }

        Renderer[] renderers = m_RoofsRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            m_RoofRendererStates = Array.Empty<RoofRendererState>();
            return;
        }

        m_RoofRendererStates = new RoofRendererState[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            m_RoofRendererStates[i] = new RoofRendererState
            {
                Renderer = renderers[i],
                WasEnabled = renderers[i] != null && renderers[i].enabled
            };
        }
    }

    private void SetRoofsHiddenForMap(bool shouldHide)
    {
        if (!m_HideRoofsWhileMapVisible)
            shouldHide = false;

        if (m_AreRoofsHiddenForMap == shouldHide)
            return;

        if (m_RoofRendererStates == null || m_RoofRendererStates.Length == 0)
            return;

        if (shouldHide)
        {
            for (int i = 0; i < m_RoofRendererStates.Length; i++)
            {
                RoofRendererState state = m_RoofRendererStates[i];
                if (state.Renderer == null)
                    continue;

                state.WasEnabled = state.Renderer.enabled;
                state.Renderer.enabled = false;
                m_RoofRendererStates[i] = state;
            }

            m_AreRoofsHiddenForMap = true;
            return;
        }

        for (int i = 0; i < m_RoofRendererStates.Length; i++)
        {
            RoofRendererState state = m_RoofRendererStates[i];
            if (state.Renderer == null)
                continue;

            state.Renderer.enabled = state.WasEnabled;
            m_RoofRendererStates[i] = state;
        }

        m_AreRoofsHiddenForMap = false;
    }
    #endregion

    #region Bounds / Marker
    private bool TryComputeWorldBounds(out Bounds bounds)
    {
        bounds = default;

        if (m_BoundsRoot == null)
            return false;

        Renderer[] renderers = m_BoundsRoot.GetComponentsInChildren<Renderer>(m_IncludeInactiveRenderers);
        if (renderers == null || renderers.Length == 0)
            return false;

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            combined.Encapsulate(renderers[i].bounds);
        }

        // Add padding to avoid clipping edges.
        combined.Expand(Mathf.Max(0f, m_BoundsPadding));
        bounds = combined;
        return true;
    }

    private void UpdatePlayerMarker()
    {
        if (m_PlayerMarker == null || m_Player == null || m_MapCamera == null || m_MapRawImage == null)
            return;

        RectTransform markerParent = m_PlayerMarker.parent as RectTransform;
        if (markerParent == null)
            return;

        // Use camera projection so marker position exactly matches what the map camera renders.
        Vector3 viewport = m_MapCamera.WorldToViewportPoint(m_Player.position);
        float vx = Mathf.Clamp01(viewport.x);
        float vy = Mathf.Clamp01(viewport.y);

        RectTransform mapRect = m_MapRawImage.rectTransform;
        mapRect.GetWorldCorners(m_MapWorldCorners);

        Vector3 bottomLeft = m_MapWorldCorners[0];
        Vector3 topRight = m_MapWorldCorners[2];
        Vector3 markerWorld = new Vector3(
            Mathf.Lerp(bottomLeft.x, topRight.x, vx),
            Mathf.Lerp(bottomLeft.y, topRight.y, vy),
            Mathf.Lerp(bottomLeft.z, topRight.z, 0.5f)
        );

        Canvas canvas = m_PlayerMarker.GetComponentInParent<Canvas>();
        Camera uiCamera = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            uiCamera = canvas.worldCamera;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, markerWorld);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(markerParent, screenPoint, uiCamera, out Vector2 localPoint))
        {
            m_PlayerMarker.anchoredPosition = localPoint;
        }

        // Hide marker if the target is behind the map camera.
        m_PlayerMarker.gameObject.SetActive(viewport.z >= 0f);
    }
    #endregion

    #region UI Helpers
    private RectTransform FindMapPanelByName(string panelName)
    {
        if (string.IsNullOrEmpty(panelName))
            return null;

        GameObject found = GameObject.Find(panelName);
        if (found == null)
            return null;

        found.TryGetComponent(out RectTransform rt);
        return rt;
    }

    private Transform FindTransformByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        GameObject found = GameObject.Find(objectName);
        return found != null ? found.transform : null;
    }

    private RawImage CreateRawImageUnderPanel(RectTransform panel)
    {
        if (panel == null)
            return null;

        GameObject go = new GameObject("LiveLevelMap_RawImage", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(panel, false);
        go.transform.SetSiblingIndex(0); // Keep map visuals behind other UI elements.

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        RawImage raw = go.GetComponent<RawImage>();
        raw.raycastTarget = false;
        return raw;
    }

    #region Public Unlock API
    public void UnlockHandlerControls()
    {
        m_IsHandlerControlsUnlocked = true;
        RefreshHandlerActionState();
    }

    public void LockHandlerControls()
    {
        if (!m_RequireUnlockForHandlerControls)
            return;

        m_IsHandlerControlsUnlocked = false;
        RefreshHandlerActionState();
    }
    #endregion
    #endregion
}

