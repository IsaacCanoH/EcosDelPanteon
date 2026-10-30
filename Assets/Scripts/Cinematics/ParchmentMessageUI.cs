using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ParchmentMessageUI : MonoBehaviour
{
    public static ParchmentMessageUI Instance { get; private set; }

    [Header("Referencias Visuales")]
    public CanvasGroup cardCanvasGroup;
    public Image cardBackground;
    public Image itemIcon;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public TMP_Text pauseNoticeText;
    public TMP_Text continueHintText;
    public Image backdropDimmer;

    [Header("Sprites y Fuentes")]
    public Sprite parchmentSprite;
    public TMP_FontAsset pixelFont;

    private bool isOpen = false;
    public bool IsOpen => isOpen;
    private Action onClosedCallback;
    private float previousTimeScale = 1f;
    private Coroutine fadeRoutine;
    private Coroutine autoDismissRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        SetupProceduralUIIfMissing();
        HideImmediate();
    }

    private void Update()
    {
        if (!isOpen) return;

        // Cerrar al presionar P, Espacio, Enter, Escape o Clic Izquierdo
        if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape) ||
            Input.GetMouseButtonDown(0))
        {
            Close();
        }
    }

    public void Show(string title, string message, Sprite icon = null, Action onClosed = null, bool pauseGame = true)
    {
        if (cardCanvasGroup == null)
        {
            SetupProceduralUIIfMissing();
        }

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        if (autoDismissRoutine != null)
        {
            StopCoroutine(autoDismissRoutine);
            autoDismissRoutine = null;
        }

        onClosedCallback = onClosed;
        isOpen = true;

        if (titleText != null) titleText.text = title != null ? title.ToUpper() : "";
        if (bodyText != null) bodyText.text = message ?? "";

        if (itemIcon != null)
        {
            itemIcon.gameObject.SetActive(icon != null);
            if (icon != null) itemIcon.sprite = icon;
        }

        if (pauseGame)
        {
            if (Time.timeScale > 0f) previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (pauseNoticeText != null) pauseNoticeText.gameObject.SetActive(true);
        }
        else
        {
            if (pauseNoticeText != null) pauseNoticeText.gameObject.SetActive(false);
            // Mensajes informativos breves: auto-desvanecer en 5.5s
            autoDismissRoutine = StartCoroutine(AutoDismissRoutine(5.5f));
        }

        fadeRoutine = StartCoroutine(FadeRoutine(cardCanvasGroup.alpha, 1f, 0.18f, true));
    }

    private IEnumerator AutoDismissRoutine(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (isOpen)
        {
            Close();
        }
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        if (autoDismissRoutine != null)
        {
            StopCoroutine(autoDismissRoutine);
            autoDismissRoutine = null;
        }

        Time.timeScale = 1f;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(cardCanvasGroup != null ? cardCanvasGroup.alpha : 1f, 0f, 0.15f, false, () =>
        {
            Action cb = onClosedCallback;
            onClosedCallback = null;
            cb?.Invoke();
        }));
    }

    private IEnumerator FadeRoutine(float from, float to, float duration, bool enableInteraction, Action onComplete = null)
    {
        if (cardCanvasGroup == null) yield break;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(from, to, t / duration);
            cardCanvasGroup.alpha = alpha;
            yield return null;
        }

        cardCanvasGroup.alpha = to;
        cardCanvasGroup.interactable = enableInteraction;
        cardCanvasGroup.blocksRaycasts = enableInteraction;
        onComplete?.Invoke();
    }

    private void HideImmediate()
    {
        if (cardCanvasGroup != null)
        {
            cardCanvasGroup.alpha = 0f;
            cardCanvasGroup.interactable = false;
            cardCanvasGroup.blocksRaycasts = false;
        }
        if (backdropDimmer != null)
        {
            backdropDimmer.color = new Color(0f, 0f, 0f, 0f);
        }
    }

    public void SetupProceduralUIIfMissing()
    {
#if UNITY_EDITOR
        if (parchmentSprite == null)
            parchmentSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/CartaAntigua_Sello.png");
        if (pixelFont == null)
            pixelFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/PressStart2P-Regular SDF.asset");
#endif

        // Intentar emparentar a Canvas_HUD para heredar la escala de resolución exacta del Nivel 1
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject hudObj = GameObject.Find("Canvas_HUD");
            if (hudObj != null)
            {
                transform.SetParent(hudObj.transform, false);
                canvas = hudObj.GetComponent<Canvas>();
            }
        }

        // Si no hay Canvas_HUD, crear uno con CanvasScaler escalado a 1920x1080 (igual que Nivel 1)
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("ParchmentMessageCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 970;
            var cs = canvasObj.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            cs.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();
            transform.SetParent(canvasObj.transform, false);
        }

        // Si ya teníamos hijos generados previamente, limpiarlos para aplicar el nuevo tamaño
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform ch = transform.GetChild(i);
            if (Application.isPlaying) Destroy(ch.gameObject);
            else DestroyImmediate(ch.gameObject);
        }

        // 1. Dimmer sutil (no opaca ni oscurece el juego por completo)
        GameObject dimObj = new GameObject("BackdropDimmer");
        dimObj.transform.SetParent(transform, false);
        RectTransform dimRt = dimObj.AddComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.sizeDelta = Vector2.zero;
        backdropDimmer = dimObj.AddComponent<Image>();
        backdropDimmer.color = new Color(0f, 0f, 0f, 0f);
        backdropDimmer.raycastTarget = false;

        // 2. Tarjeta de Pergamino (Tamaño visible, amplio y cómodo para leer)
        GameObject cardObj = new GameObject("ParchmentCard");
        cardObj.transform.SetParent(transform, false);
        RectTransform cardRt = cardObj.AddComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 1f);
        cardRt.anchorMax = new Vector2(0.5f, 1f);
        cardRt.pivot = new Vector2(0.5f, 1f);
        cardRt.anchoredPosition = new Vector2(0f, -16f);
        cardRt.sizeDelta = new Vector2(740f, 175f);

        cardCanvasGroup = cardObj.AddComponent<CanvasGroup>();
        cardBackground = cardObj.AddComponent<Image>();
        if (parchmentSprite != null) cardBackground.sprite = parchmentSprite;
        cardBackground.color = Color.white;
        cardBackground.raycastTarget = false;

        // 3. Icono opcional (sello o arma)
        GameObject iconObj = new GameObject("ItemIcon");
        iconObj.transform.SetParent(cardObj.transform, false);
        RectTransform iconRt = iconObj.AddComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.anchoredPosition = new Vector2(46f, 0f);
        iconRt.sizeDelta = new Vector2(48f, 48f);
        itemIcon = iconObj.AddComponent<Image>();
        itemIcon.preserveAspect = true;
        itemIcon.raycastTarget = false;
        itemIcon.gameObject.SetActive(false);

        // 4. Título (Tipografía pixel nítida y legible)
        GameObject titleObj = new GameObject("CardTitle");
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform titleRt = titleObj.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(20f, -16f);
        titleRt.sizeDelta = new Vector2(-70f, 30f);
        titleText = titleObj.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) titleText.font = pixelFont;
        titleText.text = "OBJETIVO";
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 17f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(0.29f, 0.22f, 0.16f, 1f); // #4a3828 sepia oscuro
        titleText.raycastTarget = false;

        // 5. Mensaje / Cuerpo (Tamaño de lectura cómodo y nítido)
        GameObject bodyObj = new GameObject("CardBody");
        bodyObj.transform.SetParent(cardObj.transform, false);
        RectTransform bodyRt = bodyObj.AddComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.pivot = new Vector2(0.5f, 0.5f);
        bodyRt.anchoredPosition = new Vector2(20f, -8f);
        bodyRt.sizeDelta = new Vector2(-70f, -64f);
        bodyText = bodyObj.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) bodyText.font = pixelFont;
        bodyText.fontSize = 13.5f;
        bodyText.alignment = TextAlignmentOptions.Center;
        bodyText.color = new Color(0.18f, 0.14f, 0.11f, 1f); // #2e241c sepia texto
        bodyText.lineSpacing = 12f;
        bodyText.enableWordWrapping = true;
        bodyText.raycastTarget = false;

        // 6. Pie: [P / ESPACIO] CONTINUAR (Discreto en esquina inferior derecha)
        GameObject hintObj = new GameObject("ContinueHint");
        hintObj.transform.SetParent(cardObj.transform, false);
        RectTransform hintRt = hintObj.AddComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(1f, 0f);
        hintRt.anchorMax = new Vector2(1f, 0f);
        hintRt.pivot = new Vector2(1f, 0f);
        hintRt.anchoredPosition = new Vector2(-22f, 10f);
        hintRt.sizeDelta = new Vector2(240f, 20f);
        continueHintText = hintObj.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) continueHintText.font = pixelFont;
        continueHintText.text = "[P / ESPACIO] CONTINUAR";
        continueHintText.alignment = TextAlignmentOptions.BottomRight;
        continueHintText.fontSize = 11f;
        continueHintText.color = new Color(0.42f, 0.32f, 0.24f, 0.85f);
        continueHintText.raycastTarget = false;

        // 7. Aviso de pausa opcional
        GameObject pauseObj = new GameObject("PauseNotice");
        pauseObj.transform.SetParent(cardObj.transform, false);
        RectTransform pauseRt = pauseObj.AddComponent<RectTransform>();
        pauseRt.anchorMin = new Vector2(0f, 0f);
        pauseRt.anchorMax = new Vector2(0f, 0f);
        pauseRt.pivot = new Vector2(0f, 0f);
        pauseRt.anchoredPosition = new Vector2(40f, 9f);
        pauseRt.sizeDelta = new Vector2(120f, 18f);
        pauseNoticeText = pauseObj.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) pauseNoticeText.font = pixelFont;
        pauseNoticeText.text = "PAUSA";
        pauseNoticeText.fontSize = 9.5f;
        pauseNoticeText.color = new Color(0.55f, 0.45f, 0.36f, 0.85f);
        pauseNoticeText.raycastTarget = false;
        pauseNoticeText.gameObject.SetActive(false);
    }
}
