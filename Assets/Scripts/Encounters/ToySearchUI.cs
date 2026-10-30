using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ToySearchUI : MonoBehaviour
{
    public static ToySearchUI Instance { get; private set; }

    [Header("Recursos y Fuentes")]
    public TMP_FontAsset pixelFont;

    private CanvasGroup searchCanvasGroup;
    private RectTransform panelRt;
    private TMP_Text promptText;
    private Image progressFill;
    private Transform currentTarget;
    private float currentProgress = 0f;
    private bool isSearching = false;
    private Camera mainCam;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        SetupUI();
        HideImmediate();
    }

    private void Start()
    {
        mainCam = Camera.main ?? FindObjectOfType<Camera>();
    }

    private void LateUpdate()
    {
        if (currentTarget == null || searchCanvasGroup == null)
        {
            if (searchCanvasGroup != null && searchCanvasGroup.alpha > 0f)
                searchCanvasGroup.alpha = 0f;
            return;
        }

        if (mainCam == null) mainCam = Camera.main ?? FindObjectOfType<Camera>();
        if (mainCam == null) return;

        Canvas c = GetComponent<Canvas>();
        if (c != null && c.worldCamera == null)
        {
            c.worldCamera = mainCam;
        }

        // Posicionar el indicador flotante 1.25m encima del altar/juguete
        Vector3 worldPos = currentTarget.position + Vector3.up * 1.25f;
        panelRt.position = worldPos;

        if (progressFill != null)
        {
            progressFill.fillAmount = currentProgress;
        }

        if (promptText != null)
        {
            promptText.text = isSearching
                ? $"<color=#FFE082>BUSCANDO...</color> {Mathf.RoundToInt(currentProgress * 100)}%"
                : "<color=#FFE082>[E]</color> RECOGER JUGUETE";
        }
    }

    public void SetTarget(Transform target, float progress, bool searching)
    {
        currentTarget = target;
        currentProgress = Mathf.Clamp01(progress);
        isSearching = searching;

        if (searchCanvasGroup != null)
        {
            searchCanvasGroup.alpha = target != null ? 1f : 0f;
        }
    }

    public void Hide()
    {
        currentTarget = null;
        currentProgress = 0f;
        isSearching = false;
        if (searchCanvasGroup != null)
        {
            searchCanvasGroup.alpha = 0f;
        }
    }

    private void HideImmediate()
    {
        if (searchCanvasGroup != null)
        {
            searchCanvasGroup.alpha = 0f;
        }
    }

    private void SetupUI()
    {
#if UNITY_EDITOR
        if (pixelFont == null)
            pixelFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/PressStart2P-Regular SDF.asset");
#endif

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main ?? FindObjectOfType<Camera>();
        canvas.sortingLayerName = "Layer 3"; // Por encima de todo el escenario (Layer 1 y Layer 2)
        canvas.sortingOrder = 990;

        searchCanvasGroup = GetComponent<CanvasGroup>();
        if (searchCanvasGroup == null) searchCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        panelRt = GetComponent<RectTransform>();
        panelRt.sizeDelta = new Vector2(240f, 50f);
        panelRt.localScale = Vector3.one * 0.02f; // Escala nítida para WorldSpace en 2D

        // Fondo del panel
        GameObject bgObj = new GameObject("SearchBG");
        bgObj.transform.SetParent(transform, false);
        RectTransform bgRt = bgObj.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.08f, 0.07f, 0.08f, 0.88f);

        // Borde dorado fino
        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(bgObj.transform, false);
        RectTransform borderRt = borderObj.AddComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.sizeDelta = Vector2.zero;
        Outline outline = borderObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.78f, 0.62f, 0.35f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // Texto Prompt
        GameObject textObj = new GameObject("PromptText");
        textObj.transform.SetParent(bgObj.transform, false);
        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 0.45f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.offsetMin = new Vector2(4f, 0f);
        textRt.offsetMax = new Vector2(-4f, -2f);
        promptText = textObj.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) promptText.font = pixelFont;
        promptText.text = "[E] MANTÉN PARA BUSCAR";
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.fontSize = 11;
        promptText.color = Color.white;

        // Barra de progreso (fondo)
        GameObject barBgObj = new GameObject("ProgressBarBG");
        barBgObj.transform.SetParent(bgObj.transform, false);
        RectTransform barBgRt = barBgObj.AddComponent<RectTransform>();
        barBgRt.anchorMin = new Vector2(0.08f, 0.12f);
        barBgRt.anchorMax = new Vector2(0.92f, 0.40f);
        barBgRt.offsetMin = Vector2.zero;
        barBgRt.offsetMax = Vector2.zero;
        Image barBgImg = barBgObj.AddComponent<Image>();
        barBgImg.color = new Color(0.2f, 0.18f, 0.2f, 0.95f);

        // Barra de progreso (fill)
        GameObject fillObj = new GameObject("ProgressBarFill");
        fillObj.transform.SetParent(barBgObj.transform, false);
        RectTransform fillRt = fillObj.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        progressFill = fillObj.AddComponent<Image>();
        progressFill.color = new Color(0.95f, 0.78f, 0.32f, 1f); // Dorado brillante
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFill.fillAmount = 0f;
    }
}

