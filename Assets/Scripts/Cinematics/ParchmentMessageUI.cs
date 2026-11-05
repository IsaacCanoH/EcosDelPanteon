using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ParchmentMessageUI : MonoBehaviour
{
    public static ParchmentMessageUI Instance { get; private set; }

    [Header("Referencias Visuales (Tarjeta_Pista Nivel 1)")]
    public CanvasGroup cardCanvasGroup;
    public CanvasGroup backdropCanvasGroup;
    public Image cardBackground;
    public Image itemIcon;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public TMP_Text pauseNoticeText;
    public TMP_Text continueHintText;
    public Image backdropDimmer;
    public GameObject topRightClueAccess;

    [Header("Sprites y Fuentes")]
    public Sprite parchmentSprite;
    public TMP_FontAsset pixelFont;

    private bool isOpen = false;
    public bool IsOpen => isOpen;
    private int lastStateChangeFrame = -1;
    public bool ChangedThisFrame => lastStateChangeFrame == Time.frameCount;

    private Action onClosedCallback;
    private float previousTimeScale = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Limpiar cualquier hijo huérfano interno (ej. ParchmentCard duplicado que se queda congelado)
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform ch = transform.GetChild(i);
            ch.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(ch.gameObject);
            else DestroyImmediate(ch.gameObject);
        }

        SetupProceduralUIIfMissing();
        HideImmediate();
    }

    private void Update()
    {
        if (!isOpen || ChangedThisFrame) return;

        // Se quita ÚNICAMENTE con la letra P (sin Espacio, sin Enter, sin Escape ni clicks)
        if (GameControls.CluePressed || Input.GetKeyDown(KeyCode.P))
        {
            Close();
        }
    }

    public void Show(string title, string message, Sprite icon = null, Action onClosed = null, bool pauseGame = true)
    {
        if (cardCanvasGroup == null || titleText == null || bodyText == null)
        {
            SetupProceduralUIIfMissing();
        }

        onClosedCallback = onClosed;
        isOpen = true;
        lastStateChangeFrame = Time.frameCount;

        ApplyCardTextLayout();

        if (titleText != null) titleText.text = title != null ? title.ToUpper() : "";
        if (bodyText != null)
        {
            bodyText.text = message ?? "";
            var inlinePrompt = bodyText.GetComponent<InlineKeyPromptUI>();
            if (inlinePrompt != null) inlinePrompt.enabled = true;
        }

        if (itemIcon != null)
        {
            itemIcon.gameObject.SetActive(icon != null);
            if (icon != null) itemIcon.sprite = icon;
        }

        if (topRightClueAccess != null)
        {
            topRightClueAccess.SetActive(false);
        }

        // Todo mensaje pone el juego en pausa obligatoria
        if (Time.timeScale > 0f) previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        if (pauseNoticeText != null)
        {
            pauseNoticeText.text = "JUEGO EN PAUSA";
            pauseNoticeText.gameObject.SetActive(true);
        }

        if (backdropCanvasGroup != null)
        {
            backdropCanvasGroup.alpha = 1f;
            backdropCanvasGroup.blocksRaycasts = true;
        }
        if (backdropDimmer != null)
        {
            backdropDimmer.color = new Color(0.02f, 0.03f, 0.05f, 0.56f);
        }

        if (cardCanvasGroup != null)
        {
            cardCanvasGroup.alpha = 1f;
            cardCanvasGroup.interactable = true;
            cardCanvasGroup.blocksRaycasts = true;
        }
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        lastStateChangeFrame = Time.frameCount;

        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        AudioListener.pause = false;

        if (cardCanvasGroup != null)
        {
            cardCanvasGroup.alpha = 0f;
            cardCanvasGroup.interactable = false;
            cardCanvasGroup.blocksRaycasts = false;
        }
        if (backdropCanvasGroup != null)
        {
            backdropCanvasGroup.alpha = 0f;
            backdropCanvasGroup.blocksRaycasts = false;
        }
        if (backdropDimmer != null)
        {
            backdropDimmer.color = new Color(0f, 0f, 0f, 0f);
        }
        if (topRightClueAccess != null)
        {
            topRightClueAccess.SetActive(true);
        }

        Action cb = onClosedCallback;
        onClosedCallback = null;
        cb?.Invoke();
    }

    private void HideImmediate()
    {
        if (cardCanvasGroup != null)
        {
            cardCanvasGroup.alpha = 0f;
            cardCanvasGroup.interactable = false;
            cardCanvasGroup.blocksRaycasts = false;
        }
        if (backdropCanvasGroup != null)
        {
            backdropCanvasGroup.alpha = 0f;
            backdropCanvasGroup.blocksRaycasts = false;
        }
        if (backdropDimmer != null)
        {
            backdropDimmer.color = new Color(0f, 0f, 0f, 0f);
        }
        if (topRightClueAccess != null)
        {
            topRightClueAccess.SetActive(true);
        }
    }

    public void SetupProceduralUIIfMissing()
    {
        // Limpiar cualquier hijo huérfano de este GameObject
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform ch = transform.GetChild(i);
            ch.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(ch.gameObject);
            else DestroyImmediate(ch.gameObject);
        }

#if UNITY_EDITOR
        if (parchmentSprite == null)
            parchmentSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/CartaAntigua_Sello.png");
        if (pixelFont == null)
            pixelFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/PressStart2P-Regular SDF.asset");
#endif

        // 1. Vincular a la estructura original Tarjeta_Pista de Canvas_HUD/BusquedaMonedas (idéntica a Nivel 1)
        GameObject hudObj = GameObject.Find("Canvas_HUD");
        if (hudObj != null)
        {
            Transform busqueda = hudObj.transform.Find("BusquedaMonedas");
            if (busqueda != null)
            {
                busqueda.gameObject.SetActive(true);
                var coinSearchUI = busqueda.GetComponent<CoinSearchUI>();
                if (coinSearchUI != null) coinSearchUI.enabled = false;

                Transform indicadorBusqueda = busqueda.Find("Indicador_Busqueda");
                if (indicadorBusqueda != null) indicadorBusqueda.gameObject.SetActive(false);

                Transform accesoPista = busqueda.Find("Acceso_Pista_P");
                if (accesoPista != null)
                {
                    topRightClueAccess = accesoPista.gameObject;
                    topRightClueAccess.SetActive(true);
                    var hintText = accesoPista.Find("Texto_Acceso_Pista")?.GetComponent<TMP_Text>();
                    if (hintText != null)
                    {
                        hintText.text = "[P] VER PISTA";
                        hintText.enabled = true;
                    }
                }

                Transform fondoPausa = busqueda.Find("Fondo_Pausa_Pista");
                if (fondoPausa != null)
                {
                    fondoPausa.gameObject.SetActive(true);
                    backdropCanvasGroup = fondoPausa.GetComponent<CanvasGroup>();
                    backdropDimmer = fondoPausa.GetComponent<Image>();
                    if (backdropCanvasGroup != null)
                    {
                        backdropCanvasGroup.alpha = 0f;
                        backdropCanvasGroup.blocksRaycasts = false;
                    }
                }

                Transform tarjetaPista = busqueda.Find("Tarjeta_Pista");
                if (tarjetaPista != null)
                {
                    tarjetaPista.gameObject.SetActive(true);
                    cardCanvasGroup = tarjetaPista.GetComponent<CanvasGroup>();
                    if (cardCanvasGroup == null) cardCanvasGroup = tarjetaPista.gameObject.AddComponent<CanvasGroup>();
                    cardCanvasGroup.alpha = 0f;
                    cardCanvasGroup.interactable = false;
                    cardCanvasGroup.blocksRaycasts = false;

                    cardBackground = tarjetaPista.GetComponent<Image>();
                    if (cardBackground != null && parchmentSprite != null)
                    {
                        cardBackground.sprite = parchmentSprite;
                    }

                    titleText = tarjetaPista.Find("Titulo_Pista")?.GetComponent<TMP_Text>();
                    bodyText = tarjetaPista.Find("Texto_Pista")?.GetComponent<TMP_Text>();
                    pauseNoticeText = tarjetaPista.Find("Estado_Pausa")?.GetComponent<TMP_Text>();
                    continueHintText = tarjetaPista.Find("Ayuda_Pista")?.GetComponent<TMP_Text>();
                    Transform teclaContinuar = tarjetaPista.Find("Tecla_Continuar");

                    if (teclaContinuar != null)
                    {
                        teclaContinuar.gameObject.SetActive(true);
                    }
                    if (continueHintText != null)
                    {
                        continueHintText.gameObject.SetActive(true);
                        continueHintText.text = "CONTINUAR";
                        if (pixelFont != null) continueHintText.font = pixelFont;
                        continueHintText.color = new Color(0.545f, 0.369f, 0.2f, 1f);
                    }
                    if (pauseNoticeText != null)
                    {
                        pauseNoticeText.gameObject.SetActive(true);
                        pauseNoticeText.text = "JUEGO EN PAUSA";
                        if (pixelFont != null) pauseNoticeText.font = pixelFont;
                        pauseNoticeText.color = new Color(0.545f, 0.369f, 0.2f, 1f);
                    }
                    if (titleText != null)
                    {
                        if (pixelFont != null) titleText.font = pixelFont;
                        titleText.color = new Color(0.545f, 0.369f, 0.2f, 1f);
                    }
                    if (bodyText != null)
                    {
                        if (pixelFont != null) bodyText.font = pixelFont;
                        bodyText.color = new Color(0.286f, 0.204f, 0.098f, 1f);

                        // Configurar Tecla_F_Inline para renderizar la tecla F pixelada en el texto
                        Transform teclaF = bodyText.transform.Find("Tecla_F_Inline");
                        if (teclaF == null && teclaContinuar != null)
                        {
                            GameObject copy = Instantiate(teclaContinuar.gameObject, bodyText.transform);
                            copy.name = "Tecla_F_Inline";
                            teclaF = copy.transform;
                            teclaF.localPosition = Vector3.zero;
                            teclaF.localScale = Vector3.one;

                            var texts = teclaF.GetComponentsInChildren<TMP_Text>(true);
                            foreach (var txt in texts)
                            {
                                if (txt.text == "P" || txt.name.Contains("Letra") || txt.name.Contains("Text"))
                                {
                                    txt.text = "F";
                                }
                            }
                            teclaF.gameObject.SetActive(false);
                        }

                        var inlinePrompt = bodyText.GetComponent<InlineKeyPromptUI>();
                        if (inlinePrompt == null) inlinePrompt = bodyText.gameObject.AddComponent<InlineKeyPromptUI>();
                        if (teclaF != null)
                        {
                            inlinePrompt.keycap = teclaF.GetComponent<RectTransform>();
                            inlinePrompt.key = "F";
                        }
                        inlinePrompt.enabled = true;
                    }

                    if (cardCanvasGroup != null && titleText != null && bodyText != null)
                    {
                        ApplyCardTextLayout();
                        return;
                    }
                }
            }
        }

        // 2. Fallback de emergencia si no existiera BusquedaMonedas
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null && hudObj != null)
        {
            transform.SetParent(hudObj.transform, false);
            canvas = hudObj.GetComponent<Canvas>();
        }
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

        GameObject dimObj = new GameObject("Fondo_Pausa_Pista");
        dimObj.transform.SetParent(transform, false);
        RectTransform dimRt = dimObj.AddComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.sizeDelta = Vector2.zero;
        backdropDimmer = dimObj.AddComponent<Image>();
        backdropDimmer.color = new Color(0.02f, 0.03f, 0.05f, 0.56f);
        backdropCanvasGroup = dimObj.AddComponent<CanvasGroup>();
        backdropCanvasGroup.alpha = 0f;
        backdropCanvasGroup.blocksRaycasts = false;

        GameObject cardObj = new GameObject("Tarjeta_Pista");
        cardObj.transform.SetParent(transform, false);
        RectTransform cardRt = cardObj.AddComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = new Vector2(0f, 10f);
        cardRt.sizeDelta = new Vector2(620f, 252f);

        cardCanvasGroup = cardObj.AddComponent<CanvasGroup>();
        cardCanvasGroup.alpha = 0f;
        cardCanvasGroup.interactable = false;
        cardCanvasGroup.blocksRaycasts = false;

        cardBackground = cardObj.AddComponent<Image>();
        if (parchmentSprite != null) cardBackground.sprite = parchmentSprite;
        cardBackground.color = Color.white;

        GameObject titleObj = new GameObject("Titulo_Pista");
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform titleRt = titleObj.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(18f, -24f);
        titleRt.sizeDelta = new Vector2(-196f, 28f);
        titleText = titleObj.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) titleText.font = pixelFont;
        titleText.alignment = TextAlignmentOptions.Left;
        titleText.fontSize = 14f;
        titleText.color = new Color(0.545f, 0.369f, 0.2f, 1f);

        GameObject bodyObj = new GameObject("Texto_Pista");
        bodyObj.transform.SetParent(cardObj.transform, false);
        RectTransform bodyRt = bodyObj.AddComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.pivot = new Vector2(0.5f, 0.5f);
        bodyRt.anchoredPosition = new Vector2(18f, 8f);
        bodyRt.sizeDelta = new Vector2(-196f, -126f);
        bodyText = bodyObj.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) bodyText.font = pixelFont;
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.fontSize = 12f;
        bodyText.lineSpacing = 12f;
        bodyText.color = new Color(0.286f, 0.204f, 0.098f, 1f);
        bodyText.enableWordWrapping = true;

        GameObject pauseObj = new GameObject("Estado_Pausa");
        pauseObj.transform.SetParent(cardObj.transform, false);
        RectTransform pauseRt = pauseObj.AddComponent<RectTransform>();
        pauseRt.anchorMin = new Vector2(0f, 0f);
        pauseRt.anchorMax = new Vector2(0f, 0f);
        pauseRt.pivot = new Vector2(0f, 0f);
        pauseRt.anchoredPosition = new Vector2(116f, 21f);
        pauseRt.sizeDelta = new Vector2(210f, 22f);
        pauseNoticeText = pauseObj.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) pauseNoticeText.font = pixelFont;
        pauseNoticeText.text = "JUEGO EN PAUSA";
        pauseNoticeText.fontSize = 9f;
        pauseNoticeText.color = new Color(0.545f, 0.369f, 0.2f, 1f);

        ApplyCardTextLayout();
    }

    /// <summary>
    /// Asegura que el título (Titulo_Pista) y el cuerpo del mensaje (Texto_Pista) tengan
    /// regiones separadas, alineación superior-izquierda y auto-ajuste de fuente para que
    /// el texto nunca se encime con el título del pergamino.
    /// </summary>
    private void ApplyCardTextLayout()
    {
        if (titleText != null)
        {
            RectTransform titleRt = titleText.rectTransform;
            if (titleRt != null)
            {
                titleRt.anchorMin = new Vector2(0.5f, 0.5f);
                titleRt.anchorMax = new Vector2(0.5f, 0.5f);
                titleRt.pivot = new Vector2(0.5f, 0.5f);
                titleRt.anchoredPosition = new Vector2(34f, 88f);
                titleRt.sizeDelta = new Vector2(524f, 30f);
            }
            titleText.alignment = TextAlignmentOptions.TopLeft;
            titleText.enableWordWrapping = false;
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 14f;
            titleText.fontSizeMax = 18.5f;
            titleText.fontSize = 18.5f;
        }

        if (bodyText != null)
        {
            RectTransform bodyRt = bodyText.rectTransform;
            if (bodyRt != null)
            {
                bodyRt.anchorMin = new Vector2(0.5f, 0.5f);
                bodyRt.anchorMax = new Vector2(0.5f, 0.5f);
                bodyRt.pivot = new Vector2(0.5f, 0.5f);
                bodyRt.anchoredPosition = new Vector2(34f, -2f);
                bodyRt.sizeDelta = new Vector2(524f, 124f);
            }
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.enableWordWrapping = true;
            bodyText.enableAutoSizing = true;
            bodyText.fontSizeMin = 13.5f;
            bodyText.fontSizeMax = 16.5f;
            bodyText.fontSize = 16.5f;
            bodyText.lineSpacing = 8f;
        }

        if (pauseNoticeText != null)
        {
            pauseNoticeText.enableAutoSizing = false;
            pauseNoticeText.fontSize = 11.5f;
        }

        if (continueHintText != null)
        {
            continueHintText.enableAutoSizing = false;
            continueHintText.fontSize = 13.5f;
        }
    }
}
