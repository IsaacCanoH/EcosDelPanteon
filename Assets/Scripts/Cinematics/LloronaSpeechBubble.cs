using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LloronaSpeechBubble : MonoBehaviour
{
    [Header("Configuración del Bocadillo")]
    public float typingSpeed = 0.035f;

    private Canvas worldCanvas;
    private CanvasGroup canvasGroup;
    private TMP_Text speakerText;
    private TMP_Text bodyText;
    private TMP_Text hintText;
    private bool isTyping = false;
    public bool IsTyping => isTyping;
    private string targetFullText = "";
    private Coroutine typingRoutine;

    public void SetupBubble()
    {
        if (worldCanvas != null) return;

        // Crear Canvas en World Space sobre la cabeza del fantasma
        GameObject canvasObj = new GameObject("SpeechBubbleCanvas");
        canvasObj.transform.SetParent(transform, false);
        canvasObj.transform.localPosition = new Vector3(0f, 1.45f, 0f);

        worldCanvas = canvasObj.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.worldCamera = Camera.main ?? FindObjectOfType<Camera>();
        worldCanvas.sortingLayerName = "Layer 3";
        worldCanvas.sortingOrder = 995;

        RectTransform canvasRt = canvasObj.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(340f, 120f);
        canvasRt.localScale = Vector3.one * 0.0075f; // Escala nítida para World Space (2.55m x 0.9m)

        canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        // Fondo del globo de diálogo (Bocadillo estilo pergamino / cómic)
        GameObject bubbleBg = new GameObject("BubbleBackground");
        bubbleBg.transform.SetParent(canvasObj.transform, false);
        RectTransform bgRt = bubbleBg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;

        Image bgImg = bubbleBg.AddComponent<Image>();
        bgImg.color = new Color(0.98f, 0.97f, 0.92f, 0.98f); // Pergamino marfil limpio

        // Borde fino del bocadillo
        Outline outline = bubbleBg.AddComponent<Outline>();
        outline.effectColor = new Color(0.28f, 0.20f, 0.14f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // Cola del bocadillo (pico apuntando hacia abajo a la cabeza de La Llorona)
        GameObject tail = new GameObject("BubbleTail");
        tail.transform.SetParent(canvasObj.transform, false);
        RectTransform tailRt = tail.AddComponent<RectTransform>();
        tailRt.anchorMin = new Vector2(0.5f, 0f);
        tailRt.anchorMax = new Vector2(0.5f, 0f);
        tailRt.pivot = new Vector2(0.5f, 1f);
        tailRt.anchoredPosition = new Vector2(0f, 2f);
        tailRt.sizeDelta = new Vector2(16f, 16f);
        tailRt.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Image tailImg = tail.AddComponent<Image>();
        tailImg.color = new Color(0.98f, 0.97f, 0.92f, 0.98f);

        // Fuente pixel
        TMP_FontAsset pixelFont = null;
#if UNITY_EDITOR
        pixelFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/PressStart2P-Regular SDF.asset");
#endif

        // Nombre del Hablante
        GameObject nameObj = new GameObject("SpeakerName");
        nameObj.transform.SetParent(canvasObj.transform, false);
        RectTransform nameRt = nameObj.AddComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 1f);
        nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.pivot = new Vector2(0.5f, 1f);
        nameRt.anchoredPosition = new Vector2(0f, -8f);
        nameRt.sizeDelta = new Vector2(-20f, 22f);
        speakerText = nameObj.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) speakerText.font = pixelFont;
        speakerText.text = "LA LLORONA";
        speakerText.alignment = TextAlignmentOptions.Center;
        speakerText.fontSize = 12.5f;
        speakerText.fontStyle = FontStyles.Bold;
        speakerText.color = new Color(0.72f, 0.12f, 0.12f, 1f); // Carmesí oscuro

        // Texto del Diálogo
        GameObject textObj = new GameObject("SpeechText");
        textObj.transform.SetParent(canvasObj.transform, false);
        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 0f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.pivot = new Vector2(0.5f, 0.5f);
        textRt.anchoredPosition = new Vector2(0f, -4f);
        textRt.sizeDelta = new Vector2(-26f, -48f);
        bodyText = textObj.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) bodyText.font = pixelFont;
        bodyText.alignment = TextAlignmentOptions.Center;
        bodyText.fontSize = 10f;
        bodyText.color = new Color(0.20f, 0.15f, 0.12f, 1f);
        bodyText.lineSpacing = 10f;
        bodyText.enableWordWrapping = true;

        // Prompt para continuar
        GameObject hintObj = new GameObject("PromptHint");
        hintObj.transform.SetParent(canvasObj.transform, false);
        RectTransform hintRt = hintObj.AddComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(1f, 0f);
        hintRt.pivot = new Vector2(0.5f, 0f);
        hintRt.anchoredPosition = new Vector2(0f, 7f);
        hintRt.sizeDelta = new Vector2(-20f, 16f);
        hintText = hintObj.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) hintText.font = pixelFont;
        hintText.text = "[ESPACIO / P]";
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.fontSize = 8f;
        hintText.color = new Color(0.52f, 0.42f, 0.32f, 0.9f);
    }

    public IEnumerator SpeakRoutine(string text)
    {
        SetupBubble();
        targetFullText = text;
        isTyping = true;
        if (bodyText != null) bodyText.text = "";

        // Fade in suave del bocadillo
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t / 0.2f);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // Efecto máquina de escribir
        for (int i = 0; i < text.Length; i++)
        {
            if (bodyText != null) bodyText.text += text[i];
            yield return new WaitForSeconds(typingSpeed);
        }
        isTyping = false;

        // Esperar que el jugador confirme para continuar
        float elapsed = 0f;
        while (elapsed < 5.0f) // Avanza automáticamente si no presiona nada tras 5 segundos
        {
            elapsed += Time.deltaTime;
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.P) ||
                Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
            {
                break;
            }
            yield return null;
        }

        // Fade out
        t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(1f, 0f, t / 0.2f);
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }
}
