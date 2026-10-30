using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CinematicDialogueUI : MonoBehaviour
{
    public static CinematicDialogueUI Instance { get; private set; }

    [Header("UI References")]
    public CanvasGroup dialogueCanvasGroup;
    public TMP_Text speakerText;
    public TMP_Text bodyText;
    public TMP_Text promptText;
    public Image speakerIcon;

    [Header("Configuración")]
    public float typingSpeed = 0.025f;

    private Coroutine typingRoutine;
    private Action onDialogueCompleted;
    private bool isTyping = false;
    private string fullTargetText = "";
    private bool isShowing = false;

    private Cainos.PixelArtTopDown_Basic.Level2CharacterController cachedPlayerMove;
    private PlayerWeaponController cachedPlayerWeapon;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (dialogueCanvasGroup == null)
        {
            SetupProceduralUIIfMissing();
        }

        HideImmediate();
    }

    private void Update()
    {
        if (!isShowing) return;

        // Avanzar o completar texto al presionar Espacio, Enter o Clic Izquierdo
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
        {
            if (isTyping)
            {
                // Completar inmediatamente el texto
                if (typingRoutine != null) StopCoroutine(typingRoutine);
                isTyping = false;
                if (bodyText != null) bodyText.text = fullTargetText;
            }
            else
            {
                // Cerrar diálogo
                CloseDialogue();
            }
        }
    }

    public void ShowDialogue(string speakerName, string message, Sprite icon = null, Action onComplete = null)
    {
        onDialogueCompleted = onComplete;
        isShowing = true;
        fullTargetText = message;

        LockPlayerInput(true);

        if (speakerText != null) speakerText.text = speakerName.ToUpper();
        if (speakerIcon != null)
        {
            speakerIcon.gameObject.SetActive(icon != null);
            speakerIcon.sprite = icon;
        }
        if (promptText != null) promptText.text = "Presiona [Espacio / Clic] para continuar";

        if (dialogueCanvasGroup != null)
        {
            dialogueCanvasGroup.alpha = 1f;
            dialogueCanvasGroup.interactable = true;
            dialogueCanvasGroup.blocksRaycasts = true;
        }

        if (typingRoutine != null) StopCoroutine(typingRoutine);
        typingRoutine = StartCoroutine(TypewriterRoutine(message));
    }

    private IEnumerator TypewriterRoutine(string message)
    {
        isTyping = true;
        if (bodyText != null) bodyText.text = "";

        for (int i = 0; i < message.Length; i++)
        {
            if (bodyText != null) bodyText.text += message[i];
            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;
    }

    public void CloseDialogue()
    {
        isShowing = false;
        if (typingRoutine != null) StopCoroutine(typingRoutine);

        StartCoroutine(FadeOutRoutine(() =>
        {
            LockPlayerInput(false);
            Action callback = onDialogueCompleted;
            onDialogueCompleted = null;
            callback?.Invoke();
        }));
    }

    private IEnumerator FadeOutRoutine(Action onDone)
    {
        if (dialogueCanvasGroup != null)
        {
            float t = 0f;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                dialogueCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t / 0.2f);
                yield return null;
            }
            dialogueCanvasGroup.alpha = 0f;
            dialogueCanvasGroup.interactable = false;
            dialogueCanvasGroup.blocksRaycasts = false;
        }
        onDone?.Invoke();
    }

    private void HideImmediate()
    {
        if (dialogueCanvasGroup != null)
        {
            dialogueCanvasGroup.alpha = 0f;
            dialogueCanvasGroup.interactable = false;
            dialogueCanvasGroup.blocksRaycasts = false;
        }
    }

    private void LockPlayerInput(bool locked)
    {
        if (cachedPlayerMove == null)
            cachedPlayerMove = FindObjectOfType<Cainos.PixelArtTopDown_Basic.Level2CharacterController>();
        if (cachedPlayerWeapon == null)
            cachedPlayerWeapon = FindObjectOfType<PlayerWeaponController>();

        if (cachedPlayerMove != null) cachedPlayerMove.enabled = !locked;
        if (cachedPlayerWeapon != null) cachedPlayerWeapon.enabled = !locked;

        if (locked)
        {
            var rb = cachedPlayerMove != null ? cachedPlayerMove.GetComponent<Rigidbody2D>() : null;
            if (rb != null) rb.velocity = Vector2.zero;
        }
    }

    private void SetupProceduralUIIfMissing()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("DialogueCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 950;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            transform.SetParent(canvasObj.transform, false);
        }

        dialogueCanvasGroup = GetComponent<CanvasGroup>();
        if (dialogueCanvasGroup == null) dialogueCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        RectTransform rootRt = GetComponent<RectTransform>();
        if (rootRt == null) rootRt = gameObject.AddComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 0f);
        rootRt.anchorMax = new Vector2(0.5f, 0f);
        rootRt.pivot = new Vector2(0.5f, 0f);
        rootRt.anchoredPosition = new Vector2(0f, 32f);
        rootRt.sizeDelta = new Vector2(720f, 150f);

        Image bg = GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.08f, 0.12f, 0.94f);

        // Portrait
        GameObject iconObj = new GameObject("SpeakerIcon");
        iconObj.transform.SetParent(transform, false);
        RectTransform iconRt = iconObj.AddComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.anchoredPosition = new Vector2(65f, 0f);
        iconRt.sizeDelta = new Vector2(88f, 110f);
        speakerIcon = iconObj.AddComponent<Image>();
        speakerIcon.preserveAspect = true;

        // Speaker Name
        GameObject nameObj = new GameObject("SpeakerName");
        nameObj.transform.SetParent(transform, false);
        RectTransform nameRt = nameObj.AddComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 1f);
        nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.pivot = new Vector2(0f, 1f);
        nameRt.anchoredPosition = new Vector2(130f, -14f);
        nameRt.sizeDelta = new Vector2(-150f, 30f);
        speakerText = nameObj.AddComponent<TextMeshProUGUI>();
        speakerText.fontSize = 20;
        speakerText.fontStyle = FontStyles.Bold;
        speakerText.color = new Color(0.35f, 0.85f, 1f, 1f);

        // Message Body
        GameObject bodyObj = new GameObject("MessageBody");
        bodyObj.transform.SetParent(transform, false);
        RectTransform bodyRt = bodyObj.AddComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.pivot = new Vector2(0f, 0.5f);
        bodyRt.anchoredPosition = new Vector2(130f, -12f);
        bodyRt.sizeDelta = new Vector2(-150f, -60f);
        bodyText = bodyObj.AddComponent<TextMeshProUGUI>();
        bodyText.fontSize = 17;
        bodyText.color = Color.white;
        bodyText.enableWordWrapping = true;

        // Prompt
        GameObject promptObj = new GameObject("PromptText");
        promptObj.transform.SetParent(transform, false);
        RectTransform promptRt = promptObj.AddComponent<RectTransform>();
        promptRt.anchorMin = new Vector2(1f, 0f);
        promptRt.anchorMax = new Vector2(1f, 0f);
        promptRt.pivot = new Vector2(1f, 0f);
        promptRt.anchoredPosition = new Vector2(-16f, 10f);
        promptRt.sizeDelta = new Vector2(300f, 20f);
        promptText = promptObj.AddComponent<TextMeshProUGUI>();
        promptText.fontSize = 12;
        promptText.alignment = TextAlignmentOptions.BottomRight;
        promptText.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
    }
}
