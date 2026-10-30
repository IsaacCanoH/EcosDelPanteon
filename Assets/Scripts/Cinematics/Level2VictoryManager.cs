using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Level2VictoryManager : MonoBehaviour
{
    public static Level2VictoryManager Instance { get; private set; }

    [Header("Escenas de Destino")]
    public string nextSceneName = "MenuPrincipal"; // Si existe un Nivel 3 futuro se puede configurar aquí
    public string menuSceneName = "MenuPrincipal";

    [Header("Audio")]
    public AudioClip victoryFanfare;

    [Header("UI Procedural / Referencias")]
    public CanvasGroup victoryCanvasGroup;
    public TMP_Text victoryTitleText;
    public TMP_Text victorySubtitleText;
    public TMP_Text victoryDescriptionText;
    public Button continueButton;
    public Button retryButton;
    public Button menuButton;

    private bool victoryTriggered = false;
    private Cainos.PixelArtTopDown_Basic.Level2CharacterController playerController;
    private PlayerWeaponController playerWeapons;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (victoryCanvasGroup == null)
        {
            SetupProceduralUIIfMissing();
        }

        HideImmediate();
    }

    private void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerController = player.GetComponent<Cainos.PixelArtTopDown_Basic.Level2CharacterController>();
            playerWeapons = player.GetComponent<PlayerWeaponController>();
        }

        if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
        if (retryButton != null) retryButton.onClick.AddListener(OnRetryClicked);
        if (menuButton != null) menuButton.onClick.AddListener(OnMenuClicked);
    }

    public void TriggerVictorySequence()
    {
        if (victoryTriggered) return;
        victoryTriggered = true;

        StartCoroutine(VictorySequenceRoutine());
    }

    private IEnumerator VictorySequenceRoutine()
    {
        // 1. Bloquear controles del jugador
        if (playerController != null)
        {
            playerController.enabled = false;
            var rb = playerController.GetComponent<Rigidbody2D>();
            if (rb != null) rb.velocity = Vector2.zero;
        }
        if (playerWeapons != null) playerWeapons.enabled = false;

        // 2. Reproducir fanfarria si está disponible
        if (victoryFanfare != null)
        {
            AudioSource.PlayClipAtPoint(victoryFanfare, Camera.main.transform.position, 1.0f);
        }

        // Breve pausa dramática
        yield return new WaitForSeconds(0.6f);

        // 3. Mostrar ventana de victoria con animación suave
        if (victoryCanvasGroup != null)
        {
            float t = 0f;
            while (t < 0.65f)
            {
                t += Time.deltaTime;
                victoryCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t / 0.65f);
                yield return null;
            }
            victoryCanvasGroup.alpha = 1f;
            victoryCanvasGroup.interactable = true;
            victoryCanvasGroup.blocksRaycasts = true;
        }
    }

    private void OnContinueClicked()
    {
        StartCoroutine(TransitionRoutine(nextSceneName));
    }

    private void OnRetryClicked()
    {
        StartCoroutine(TransitionRoutine(SceneManager.GetActiveScene().name));
    }

    private void OnMenuClicked()
    {
        StartCoroutine(TransitionRoutine(menuSceneName));
    }

    private IEnumerator TransitionRoutine(string targetScene)
    {
        if (victoryCanvasGroup != null)
        {
            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                victoryCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t / 0.4f);
                yield return null;
            }
        }

        SceneManager.LoadScene(targetScene);
    }

    private void HideImmediate()
    {
        if (victoryCanvasGroup != null)
        {
            victoryCanvasGroup.alpha = 0f;
            victoryCanvasGroup.interactable = false;
            victoryCanvasGroup.blocksRaycasts = false;
        }
    }

    private void SetupProceduralUIIfMissing()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("VictoryCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 980;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            transform.SetParent(canvasObj.transform, false);
        }

        victoryCanvasGroup = GetComponent<CanvasGroup>();
        if (victoryCanvasGroup == null) victoryCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        RectTransform rootRt = GetComponent<RectTransform>();
        if (rootRt == null) rootRt = gameObject.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.sizeDelta = Vector2.zero;

        // Fondo oscuro semitransparente
        Image bgImg = GetComponent<Image>();
        if (bgImg == null) bgImg = gameObject.AddComponent<Image>();
        bgImg.color = new Color(0.04f, 0.05f, 0.08f, 0.92f);

        // Panel central
        GameObject card = new GameObject("VictoryCard");
        card.transform.SetParent(transform, false);
        RectTransform cardRt = card.AddComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(620f, 380f);
        Image cardBg = card.AddComponent<Image>();
        cardBg.color = new Color(0.1f, 0.12f, 0.18f, 0.98f);

        // Título Principal
        GameObject titleObj = new GameObject("VictoryTitle");
        titleObj.transform.SetParent(card.transform, false);
        RectTransform titleRt = titleObj.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -24f);
        titleRt.sizeDelta = new Vector2(0f, 40f);
        victoryTitleText = titleObj.AddComponent<TextMeshProUGUI>();
        victoryTitleText.text = "¡VICTORIA EN EL PANTEÓN!";
        victoryTitleText.alignment = TextAlignmentOptions.Center;
        victoryTitleText.fontSize = 24;
        victoryTitleText.fontStyle = FontStyles.Bold;
        victoryTitleText.color = new Color(1f, 0.85f, 0.35f, 1f);

        // Subtítulo
        GameObject subObj = new GameObject("VictorySubtitle");
        subObj.transform.SetParent(card.transform, false);
        RectTransform subRt = subObj.AddComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0f, 1f);
        subRt.anchorMax = new Vector2(1f, 1f);
        subRt.pivot = new Vector2(0.5f, 1f);
        subRt.anchoredPosition = new Vector2(0f, -65f);
        subRt.sizeDelta = new Vector2(0f, 25f);
        victorySubtitleText = subObj.AddComponent<TextMeshProUGUI>();
        victorySubtitleText.text = "Nivel 2 Superado con Éxito";
        victorySubtitleText.alignment = TextAlignmentOptions.Center;
        victorySubtitleText.fontSize = 15;
        victorySubtitleText.color = new Color(0.4f, 0.85f, 1f, 1f);

        // Descripción
        GameObject descObj = new GameObject("VictoryDescription");
        descObj.transform.SetParent(card.transform, false);
        RectTransform descRt = descObj.AddComponent<RectTransform>();
        descRt.anchorMin = new Vector2(0f, 0.5f);
        descRt.anchorMax = new Vector2(1f, 0.5f);
        descRt.pivot = new Vector2(0.5f, 0.5f);
        descRt.anchoredPosition = new Vector2(0f, 10f);
        descRt.sizeDelta = new Vector2(-60f, 120f);
        victoryDescriptionText = descObj.AddComponent<TextMeshProUGUI>();
        victoryDescriptionText.text =
            "Has derrotado a las huestes de La Llorona y superado el combate contra el Niño Espectral.\n\n" +
            "Los 3 juguetes perdidos han sido recuperados y purificados.\n" +
            "¡El portal hacia la siguiente fase está abierto!";
        victoryDescriptionText.alignment = TextAlignmentOptions.Center;
        victoryDescriptionText.fontSize = 14;
        victoryDescriptionText.color = Color.white;
        victoryDescriptionText.enableWordWrapping = true;

        // Fila de botones
        GameObject btnRow = new GameObject("ButtonRow");
        btnRow.transform.SetParent(card.transform, false);
        RectTransform rowRt = btnRow.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0f, 0f);
        rowRt.anchorMax = new Vector2(1f, 0f);
        rowRt.pivot = new Vector2(0.5f, 0f);
        rowRt.anchoredPosition = new Vector2(0f, 24f);
        rowRt.sizeDelta = new Vector2(-40f, 44f);
        var layout = btnRow.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        continueButton = CreateButton(btnRow.transform, "BtnContinuar", "CONTINUAR", new Color(0.2f, 0.65f, 0.35f, 1f));
        retryButton = CreateButton(btnRow.transform, "BtnReintentar", "REINTENTAR", new Color(0.35f, 0.45f, 0.6f, 1f));
        menuButton = CreateButton(btnRow.transform, "BtnMenu", "MENÚ", new Color(0.55f, 0.25f, 0.25f, 1f));
    }

    private Button CreateButton(Transform parent, string name, string label, Color color)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        Image img = btnObj.AddComponent<Image>();
        img.color = color;
        Button btn = btnObj.AddComponent<Button>();

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 14;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;

        return btn;
    }
}
