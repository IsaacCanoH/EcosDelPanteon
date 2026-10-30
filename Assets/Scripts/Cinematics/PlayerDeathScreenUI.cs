using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerDeathScreenUI : MonoBehaviour
{
    public static PlayerDeathScreenUI Instance { get; private set; }

    [Header("Configuración de Escenas")]
    public string menuSceneName = "MenuPrincipal";

    [Header("Referencias Visuales")]
    public CanvasGroup deathCanvasGroup;
    public Image backdropOverlay;
    public Image cardBackground;
    public TMP_Text titleText;
    public TMP_Text subtitleText;
    public TMP_Text hintPromptText;
    public Button retryButton;
    public Button menuButton;
    public Button quitButton;

    [Header("Sprites y Fuentes")]
    public Sprite parchmentSprite;
    public TMP_FontAsset pixelFont;

    private bool isShowing = false;
    private Coroutine fadeRoutine;

    // Referencias directas para interacción por ratón a prueba de fallos de EventSystem
    private RectTransform retryRt;
    private RectTransform menuRt;
    private RectTransform quitRt;
    private Image retryImg;
    private Image menuImg;
    private Image quitImg;
    private Color retryNorm = new Color(0.58f, 0.16f, 0.18f, 1f);
    private Color retryHov = new Color(0.85f, 0.22f, 0.25f, 1f);
    private Color menuNorm = new Color(0.24f, 0.28f, 0.35f, 1f);
    private Color menuHov = new Color(0.38f, 0.45f, 0.55f, 1f);
    private Color quitNorm = new Color(0.18f, 0.16f, 0.18f, 1f);
    private Color quitHov = new Color(0.32f, 0.26f, 0.32f, 1f);

    public static void ShowDeathScreen()
    {
        if (Instance == null)
        {
            GameObject hud = GameObject.Find("Canvas_HUD");
            GameObject uiObj = new GameObject("PlayerDeathScreenUI");
            if (hud != null) uiObj.transform.SetParent(hud.transform, false);
            Instance = uiObj.AddComponent<PlayerDeathScreenUI>();
        }

        Instance.Show();
    }

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
        if (!isShowing) return;

        // Atajos de teclado inmediatos (funcionan siempre con Time.timeScale = 0)
        if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Space))
        {
            OnRetryClicked();
            return;
        }
        else if (Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.Escape))
        {
            OnMenuClicked();
            return;
        }
        else if (Input.GetKeyDown(KeyCode.Q))
        {
            OnQuitClicked();
            return;
        }

        // Detección directa de ratón por coordenadas de pantalla (100% independiente de EventSystem y módulos UI)
        Vector2 mousePos = Input.mousePosition;
        bool hoverRetry = retryRt != null && RectTransformUtility.RectangleContainsScreenPoint(retryRt, mousePos, null);
        bool hoverMenu = menuRt != null && RectTransformUtility.RectangleContainsScreenPoint(menuRt, mousePos, null);
        bool hoverQuit = quitRt != null && RectTransformUtility.RectangleContainsScreenPoint(quitRt, mousePos, null);

        if (retryImg != null) retryImg.color = hoverRetry ? retryHov : retryNorm;
        if (menuImg != null) menuImg.color = hoverMenu ? menuHov : menuNorm;
        if (quitImg != null) quitImg.color = hoverQuit ? quitHov : quitNorm;

        if (Input.GetMouseButtonDown(0))
        {
            if (hoverRetry) OnRetryClicked();
            else if (hoverMenu) OnMenuClicked();
            else if (hoverQuit) OnQuitClicked();
        }
    }

    public void Show()
    {
        if (isShowing) return;
        isShowing = true;

        if (deathCanvasGroup == null)
        {
            SetupProceduralUIIfMissing();
        }

        // 1. Detener tiempo de juego
        Time.timeScale = 0f;

        // 2. Liberar y mostrar cursor para permitir clics inmediatos
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // 3. Desactivar controles y movimiento del jugador
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var cc1 = player.GetComponent<Cainos.PixelArtTopDown_Basic.TopDownCharacterController>();
            if (cc1 != null) cc1.enabled = false;
            var cc2 = player.GetComponent<Cainos.PixelArtTopDown_Basic.Level2CharacterController>();
            if (cc2 != null) cc2.enabled = false;
            var wp = player.GetComponent<PlayerWeaponController>();
            if (wp != null) wp.enabled = false;
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.velocity = Vector2.zero;
        }

        // 4. Activar interactividad y visibilidad inmediata
        if (deathCanvasGroup != null)
        {
            deathCanvasGroup.alpha = 1f;
            deathCanvasGroup.interactable = true;
            deathCanvasGroup.blocksRaycasts = true;
        }
    }

    public void OnRetryClicked()
    {
        Time.timeScale = 1f;
        Scene currentScene = SceneManager.GetActiveScene();
        string sceneName = string.IsNullOrEmpty(currentScene.name) ? "Cementerio_Nivel2" : currentScene.name;
        SceneManager.LoadScene(sceneName);
    }

    public void OnMenuClicked()
    {
        Time.timeScale = 1f;
        string targetMenu = string.IsNullOrEmpty(menuSceneName) ? "MenuPrincipal" : menuSceneName;
        SceneManager.LoadScene(targetMenu);
    }

    public void OnQuitClicked()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void HideImmediate()
    {
        if (deathCanvasGroup != null)
        {
            deathCanvasGroup.alpha = 0f;
            deathCanvasGroup.interactable = false;
            deathCanvasGroup.blocksRaycasts = false;
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

        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("DeathScreenCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 990;
            var cs = canvasObj.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            cs.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();
            transform.SetParent(canvasObj.transform, false);
        }

        // CanvasGroup en la raíz de la pantalla de muerte
        deathCanvasGroup = GetComponent<CanvasGroup>();
        if (deathCanvasGroup == null) deathCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        RectTransform rootRt = GetComponent<RectTransform>();
        if (rootRt == null) rootRt = gameObject.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.sizeDelta = Vector2.zero;

        // Limpiar hijos existentes si se está reconfigurando
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform ch = transform.GetChild(i);
            if (Application.isPlaying) Destroy(ch.gameObject);
            else DestroyImmediate(ch.gameObject);
        }

        // 1. Fondo oscuro ambiental (viñeta de muerte en el cementerio)
        GameObject dimObj = new GameObject("DeathDimmer");
        dimObj.transform.SetParent(transform, false);
        RectTransform dimRt = dimObj.AddComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.sizeDelta = Vector2.zero;
        backdropOverlay = dimObj.AddComponent<Image>();
        backdropOverlay.color = new Color(0.04f, 0.02f, 0.04f, 0.88f);
        backdropOverlay.raycastTarget = false;

        // 2. Tarjeta central de Muerte
        GameObject cardObj = new GameObject("DeathCard");
        cardObj.transform.SetParent(transform, false);
        RectTransform cardRt = cardObj.AddComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(580f, 320f);

        cardBackground = cardObj.AddComponent<Image>();
        cardBackground.color = new Color(0.11f, 0.09f, 0.11f, 0.98f); // Tumba oscura
        cardBackground.raycastTarget = false;

        // Borde superior carmesí
        GameObject borderTop = new GameObject("CrimsonBorder");
        borderTop.transform.SetParent(cardObj.transform, false);
        RectTransform borderRt = borderTop.AddComponent<RectTransform>();
        borderRt.anchorMin = new Vector2(0f, 1f);
        borderRt.anchorMax = new Vector2(1f, 1f);
        borderRt.pivot = new Vector2(0.5f, 1f);
        borderRt.sizeDelta = new Vector2(0f, 4f);
        Image borderImg = borderTop.AddComponent<Image>();
        borderImg.color = new Color(0.85f, 0.18f, 0.18f, 1f);

        // 3. Título de Muerte
        GameObject titleObj = new GameObject("DeathTitle");
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform titleRt = titleObj.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -24f);
        titleRt.sizeDelta = new Vector2(-40f, 36f);
        titleText = titleObj.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) titleText.font = pixelFont;
        titleText.text = "HAS CAÍDO EN COMBATE";
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 18;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(0.92f, 0.22f, 0.22f, 1f); // Carmesí oscuro

        // 4. Subtítulo / Epitafio
        GameObject subObj = new GameObject("DeathSubtitle");
        subObj.transform.SetParent(cardObj.transform, false);
        RectTransform subRt = subObj.AddComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0f, 1f);
        subRt.anchorMax = new Vector2(1f, 1f);
        subRt.pivot = new Vector2(0.5f, 1f);
        subRt.anchoredPosition = new Vector2(0f, -68f);
        subRt.sizeDelta = new Vector2(-50f, 26f);
        subtitleText = subObj.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) subtitleText.font = pixelFont;
        subtitleText.text = "Las sombras del panteón te han consumido...";
        subtitleText.alignment = TextAlignmentOptions.Center;
        subtitleText.fontSize = 11;
        subtitleText.color = new Color(0.78f, 0.72f, 0.72f, 0.95f);

        // 5. Texto de indicación
        GameObject hintObj = new GameObject("DeathPrompt");
        hintObj.transform.SetParent(cardObj.transform, false);
        RectTransform hintRt = hintObj.AddComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0f, 0.5f);
        hintRt.anchorMax = new Vector2(1f, 0.5f);
        hintRt.pivot = new Vector2(0.5f, 0.5f);
        hintRt.anchoredPosition = new Vector2(0f, 12f);
        hintRt.sizeDelta = new Vector2(-50f, 30f);
        hintPromptText = hintObj.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) hintPromptText.font = pixelFont;
        hintPromptText.text = "¿Deseas intentar purificar el panteón de nuevo?";
        hintPromptText.alignment = TextAlignmentOptions.Center;
        hintPromptText.fontSize = 10;
        hintPromptText.color = new Color(0.85f, 0.78f, 0.65f, 0.9f);

        // 6. Fila de Botones
        GameObject btnRow = new GameObject("ButtonRow");
        btnRow.transform.SetParent(cardObj.transform, false);
        RectTransform rowRt = btnRow.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0f, 0f);
        rowRt.anchorMax = new Vector2(1f, 0f);
        rowRt.pivot = new Vector2(0.5f, 0f);
        rowRt.anchoredPosition = new Vector2(0f, 24f);
        rowRt.sizeDelta = new Vector2(-40f, 48f);

        var layout = btnRow.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        // Botón 1: Reintentar
        retryButton = CreateButton(btnRow.transform, "BtnReintentar", "REINTENTAR [R]",
            retryNorm, retryHov, OnRetryClicked, out retryRt, out retryImg);

        // Botón 2: Menú Principal
        menuButton = CreateButton(btnRow.transform, "BtnMenu", "MENÚ [M]",
            menuNorm, menuHov, OnMenuClicked, out menuRt, out menuImg);

        // Botón 3: Cerrar Juego
        quitButton = CreateButton(btnRow.transform, "BtnSalir", "SALIR [Q]",
            quitNorm, quitHov, OnQuitClicked, out quitRt, out quitImg);
    }

    private Button CreateButton(Transform parent, string name, string label, Color normalCol, Color highlightCol,
        UnityEngine.Events.UnityAction action, out RectTransform buttonRt, out Image buttonImg)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        buttonRt = btnObj.GetComponent<RectTransform>() ?? btnObj.AddComponent<RectTransform>();

        buttonImg = btnObj.AddComponent<Image>();
        buttonImg.color = normalCol;
        buttonImg.raycastTarget = true;

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = normalCol;
        cb.highlightedColor = highlightCol;
        cb.pressedColor = normalCol * 0.7f;
        cb.selectedColor = highlightCol;
        cb.fadeDuration = 0.05f;
        btn.colors = cb;

        if (action != null) btn.onClick.AddListener(action);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) tmp.font = pixelFont;
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 11;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        return btn;
    }
}
