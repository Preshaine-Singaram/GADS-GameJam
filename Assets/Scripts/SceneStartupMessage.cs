using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Shows a screen message for a limited time when the scene loads (e.g. SampleScene).
/// If no TMP is assigned, creates a simple overlay under the first canvas or a new overlay canvas.
/// </summary>
public class SceneStartupMessage : MonoBehaviour
{
    #region Inspector
    [SerializeField, TextArea(2, 4)]
    private string m_Message = "Welcome to the level. Complete your objective.";

    [SerializeField, Range(0.5f, 15f)]
    private float m_VisibleSeconds = 4f;

    [Tooltip("Optional: assign a TextMeshProUGUI to use instead of runtime-created UI.")]
    [SerializeField] private TMP_Text m_MessageText;

    [Tooltip("Optional: root to enable/disable with the message. Defaults to m_MessageText.gameObject.")]
    [SerializeField] private GameObject m_MessageRoot;

    [Tooltip("If non-empty, only runs when SceneManager.GetActiveScene().name matches.")]
    [SerializeField] private string m_OnlyInSceneName = "SampleScene";
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        if (!string.IsNullOrEmpty(m_OnlyInSceneName)
            && SceneManager.GetActiveScene().name != m_OnlyInSceneName)
            return;

        StartCoroutine(ShowTemporarilyRoutine());
    }
    #endregion

    #region Private Methods
    private IEnumerator ShowTemporarilyRoutine()
    {
        EnsureMessageUI();

        if (m_MessageText == null)
            yield break;

        GameObject root = m_MessageRoot != null ? m_MessageRoot : m_MessageText.gameObject;
        m_MessageText.text = m_Message;
        root.SetActive(true);

        yield return new WaitForSeconds(m_VisibleSeconds);

        root.SetActive(false);
    }

    private void EnsureMessageUI()
    {
        if (m_MessageText != null)
            return;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("SceneStartupMessageCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        GameObject messageGo = new GameObject("SceneStartupMessage_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        messageGo.transform.SetParent(canvas.transform, false);

        RectTransform rt = messageGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -48f);
        rt.sizeDelta = new Vector2(960f, 140f);

        TextMeshProUGUI tmp = messageGo.GetComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 30f;
        tmp.color = Color.white;
        tmp.text = string.Empty;

        m_MessageText = tmp;
        m_MessageRoot = messageGo;
    }
    #endregion
}
