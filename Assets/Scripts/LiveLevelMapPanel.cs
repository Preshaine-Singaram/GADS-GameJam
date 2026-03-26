using System;
using UnityEngine;
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
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (m_MapCamera == null)
            m_MapCamera = GetComponentInChildren<Camera>(true);

        if (m_MapPanel == null && m_AutoFindMapPanel)
            m_MapPanel = FindMapPanelByName(m_AutoFindMapPanelName);

        InitializeIfNeeded();
    }

    private void OnEnable()
    {
        InitializeIfNeeded();
        ApplyCameraAndTextureState();
    }

    private void Update()
    {
        if (!m_IsInitialized)
            return;

        if (m_RenderOnlyWhenPanelVisible)
            ApplyCameraAndTextureState();

        UpdatePlayerMarker();
    }

    private void OnDisable()
    {
        if (!m_IsInitialized)
            return;

        if (m_RenderOnlyWhenPanelVisible && m_MapCamera != null)
            m_MapCamera.enabled = false;
    }

    private void OnDestroy()
    {
        if (m_RenderTexture != null)
        {
            m_RenderTexture.Release();
            m_RenderTexture = null;
        }
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

        SetupMapCameraTopDown();
        SetupRenderTexture();
        m_IsInitialized = true;
    }
    #endregion

    #region Camera / Texture Setup
    private void SetupMapCameraTopDown()
    {
        if (m_MapCamera == null)
            return;

        m_MapCamera.cullingMask = m_MapCullingMask;

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
    #endregion
}

