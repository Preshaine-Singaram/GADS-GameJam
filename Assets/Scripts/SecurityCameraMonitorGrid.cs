using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders multiple security cameras to a tiled UI grid (Freddy's-style "all cameras at once").
/// Put this on a GameObject under your monitor canvas/panel, then assign camera references.
/// </summary>
[DisallowMultipleComponent]
public class SecurityCameraMonitorGrid : MonoBehaviour
{
    #region Inspector
    [Header("Cameras")]
    [SerializeField] private Camera[] m_Cameras;

    [Tooltip("UI panel/container that receives the RawImages (e.g., HandlerScreen_Panel). If null, tries to find by name.")]
    [SerializeField] private RectTransform m_MonitorPanel;

    [Header("Render Texture Quality")]
    [Tooltip("Width of each camera feed RenderTexture.")]
    [SerializeField] private int m_TextureWidth = 480;

    [Tooltip("Height of each camera feed RenderTexture.")]
    [SerializeField] private int m_TextureHeight = 270;

    [Tooltip("Depth buffer for each camera feed RenderTexture.")]
    [SerializeField] private int m_DepthBufferBits = 16;

    [Tooltip("Optional anti-aliasing for each camera feed.")]
    [SerializeField] private int m_AntiAliasing = 1;

    [Header("Grid Layout")]
    [Tooltip("If > 0, forces number of columns. Rows are derived from camera count.")]
    [SerializeField] private int m_GridColumns = 4;

    [Tooltip("Optional spacing between tiles (in UI units).")]
    [SerializeField] private Vector2 m_TilePadding = new Vector2(2f, 2f);

    [Tooltip("If true, adds AspectRatioFitter to each tile to preserve RenderTexture aspect.")]
    [SerializeField] private bool m_UseAspectRatioFitter = true;
    #endregion

    #region Private State
    private RenderTexture[] m_RenderTextures;
    private bool[] m_OriginalCameraEnabled;
    private bool m_IsInitialized;
    private RawImage[] m_RawImages;
    private int m_Rows;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (m_MonitorPanel == null)
        {
            // Fallback for quick setup; prefer assigning in Inspector for safety.
            var found = GameObject.Find("HandlerScreen_Panel");
            if (found != null && found.TryGetComponent(out RectTransform rt))
                m_MonitorPanel = rt;
        }

        InitializeIfNeeded();
    }

    private void OnEnable()
    {
        InitializeIfNeeded();
        SetFeedRenderingActive(ShouldRenderNow());
    }

    private void Update()
    {
        // Keep rendering aligned to monitor visibility to reduce overhead.
        if (!m_IsInitialized || m_Cameras == null || m_Cameras.Length == 0)
            return;

        bool shouldRender = ShouldRenderNow();
        // We don't have a direct flag for camera state, so we just check panel state by toggling enabled
        // when it changes by comparing with current enabled state.
        for (int i = 0; i < m_Cameras.Length; i++)
        {
            if (m_Cameras[i] == null)
                continue;

            bool currentlyEnabled = m_Cameras[i].enabled;
            bool desiredEnabled = shouldRender;
            if (currentlyEnabled != desiredEnabled)
                m_Cameras[i].enabled = desiredEnabled;
        }
    }

    private void OnDisable()
    {
        if (!m_IsInitialized || m_Cameras == null)
            return;

        // Stop rendering when the monitor object is disabled.
        SetFeedRenderingActive(false);
    }

    private void OnDestroy()
    {
        if (m_RenderTextures != null)
        {
            for (int i = 0; i < m_RenderTextures.Length; i++)
            {
                if (m_RenderTextures[i] != null)
                    m_RenderTextures[i].Release();
            }
        }

        // Restore original enabled states.
        if (m_OriginalCameraEnabled != null && m_Cameras != null)
        {
            for (int i = 0; i < Mathf.Min(m_OriginalCameraEnabled.Length, m_Cameras.Length); i++)
            {
                if (m_Cameras[i] == null)
                    continue;

                m_Cameras[i].enabled = m_OriginalCameraEnabled[i];
            }
        }
    }
    #endregion

    #region Initialization
    private void InitializeIfNeeded()
    {
        if (m_IsInitialized)
            return;

        if (m_Cameras == null || m_Cameras.Length == 0)
        {
            Debug.LogWarning($"{nameof(SecurityCameraMonitorGrid)} on '{name}' has no cameras assigned.");
            m_IsInitialized = true;
            return;
        }

        if (m_MonitorPanel == null)
        {
            Debug.LogWarning($"{nameof(SecurityCameraMonitorGrid)} on '{name}' has no monitor panel assigned.");
            m_IsInitialized = true;
            return;
        }

        m_RenderTextures = new RenderTexture[m_Cameras.Length];
        m_OriginalCameraEnabled = new bool[m_Cameras.Length];
        m_RawImages = new RawImage[m_Cameras.Length];

        // Derived grid.
        m_GridColumns = Mathf.Max(1, m_GridColumns);
        m_GridColumns = Mathf.Min(m_GridColumns, m_Cameras.Length);
        m_Rows = Mathf.CeilToInt(m_Cameras.Length / (float)m_GridColumns);

        EnsureNoDuplicateTiles();

        // Build UI tiles + render textures.
        for (int i = 0; i < m_Cameras.Length; i++)
        {
            var cam = m_Cameras[i];
            if (cam == null)
            {
                m_OriginalCameraEnabled[i] = false;
                continue;
            }

            m_OriginalCameraEnabled[i] = cam.enabled;

            var rt = CreateRenderTexture(i);
            m_RenderTextures[i] = rt;

            cam.targetTexture = rt;
            // Enabled state will be handled by SetFeedRenderingActive/Update based on visibility.
        }

        BuildTiles();

        m_IsInitialized = true;
    }

    private void EnsureNoDuplicateTiles()
    {
        // If you hit Play multiple times, runtime-instantiated children are destroyed.
        // Still, when using Enter Play Mode options, we want to avoid duplicate tiles.
        const string k_Prefix = "SecurityCamTile_";
        for (int i = m_MonitorPanel.childCount - 1; i >= 0; i--)
        {
            var child = m_MonitorPanel.GetChild(i);
            if (child != null && child.name != null && child.name.StartsWith(k_Prefix, StringComparison.Ordinal))
                Destroy(child.gameObject);
        }
    }
    #endregion

    #region Rendering Activation
    private bool ShouldRenderNow()
    {
        // Render only when both this component and the panel are active.
        // (If panel is toggled by your overlay code, this saves a lot of GPU time.)
        return isActiveAndEnabled && m_MonitorPanel.gameObject.activeInHierarchy;
    }

    private void SetFeedRenderingActive(bool active)
    {
        if (m_Cameras == null)
            return;

        for (int i = 0; i < m_Cameras.Length; i++)
        {
            if (m_Cameras[i] == null)
                continue;

            m_Cameras[i].enabled = active;
        }
    }
    #endregion

    #region UI Construction
    private void BuildTiles()
    {
        // Use anchors so the tiles fill the panel area.
        for (int i = 0; i < m_Cameras.Length; i++)
        {
            var go = new GameObject($"SecurityCamTile_{i:D2}", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(m_MonitorPanel, false);
            // Put tiles behind existing monitor decorations (borders/dividers) so those remain visible.
            go.transform.SetSiblingIndex(0);

            var rt = m_RenderTextures != null && i < m_RenderTextures.Length ? m_RenderTextures[i] : null;
            var raw = go.GetComponent<RawImage>();
            raw.texture = rt;
            raw.raycastTarget = false;
            m_RawImages[i] = raw;

            if (m_UseAspectRatioFitter)
            {
                var fitter = go.GetComponent<AspectRatioFitter>();
                if (fitter == null)
                    fitter = go.AddComponent<AspectRatioFitter>();

                // Keep the same aspect ratio as the source RenderTexture.
                fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                fitter.aspectRatio = (float)m_TextureWidth / Mathf.Max(1f, m_TextureHeight);
            }

            SetTileLayout(raw.rectTransform, i);
        }
    }

    private void SetTileLayout(RectTransform tileRect, int index)
    {
        if (tileRect == null)
            return;

        int col = index % m_GridColumns;
        int row = index / m_GridColumns;

        // Top-to-bottom rows.
        float xMin = (float)col / m_GridColumns;
        float xMax = (float)(col + 1) / m_GridColumns;
        float yMax = 1f - (float)row / m_Rows;
        float yMin = 1f - (float)(row + 1) / m_Rows;

        // Apply padding by shrinking anchors.
        float padX = Mathf.Max(0f, m_TilePadding.x);
        float padY = Mathf.Max(0f, m_TilePadding.y);

        // Convert padding to normalized coordinates. This is approximate because we don't know panel pixel size.
        // Still, it's good enough for a simple layout.
        float padXNorm = padX / Mathf.Max(1f, m_MonitorPanel.rect.width);
        float padYNorm = padY / Mathf.Max(1f, m_MonitorPanel.rect.height);

        tileRect.anchorMin = new Vector2(xMin + padXNorm, yMin + padYNorm);
        tileRect.anchorMax = new Vector2(xMax - padXNorm, yMax - padYNorm);
        tileRect.anchoredPosition = Vector2.zero;
        tileRect.localRotation = Quaternion.identity;
        tileRect.localScale = Vector3.one;
        tileRect.offsetMin = Vector2.zero;
        tileRect.offsetMax = Vector2.zero;
    }
    #endregion

    #region RenderTexture Creation
    private RenderTexture CreateRenderTexture(int index)
    {
        int width = Mathf.Max(64, m_TextureWidth);
        int height = Mathf.Max(64, m_TextureHeight);

        var rt = new RenderTexture(width, height, Mathf.Max(0, m_DepthBufferBits), RenderTextureFormat.ARGB32)
        {
            name = $"{nameof(SecurityCameraMonitorGrid)}_RT_{index:D2}",
            antiAliasing = Mathf.Max(1, m_AntiAliasing),
            useMipMap = false
        };
        rt.Create();
        return rt;
    }
    #endregion
}

