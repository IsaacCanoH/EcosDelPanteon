using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossHealthBarUI : MonoBehaviour
{
    public static BossHealthBarUI Instance { get; private set; }

    [Header("UI References")]
    public CanvasGroup canvasGroup;
    public Image healthFill;
    public Image ghostFill;
    public TMP_Text bossNameText;
    public TMP_Text healthNumbersText;

    private EnemyHealth currentBossHealth;
    private Coroutine ghostFillRoutine;
    private float currentGhostRatio = 1f;
    private static Sprite cachedWhiteSprite;

    private static Sprite GetWhiteSprite()
    {
        if (cachedWhiteSprite != null) return cachedWhiteSprite;
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        cachedWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        return cachedWhiteSprite;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (canvasGroup == null)
        {
            SetupProceduralUIIfMissing();
        }

        HideImmediate();
    }

    public void BindBoss(EnemyHealth bossHealth, string bossName = "NIÑO ESPECTRAL")
    {
        if (currentBossHealth != null)
        {
            currentBossHealth.OnHealthChanged -= HandleHealthChanged;
            currentBossHealth.OnDied -= HandleBossDied;
        }

        currentBossHealth = bossHealth;
        if (currentBossHealth != null)
        {
            currentBossHealth.OnHealthChanged += HandleHealthChanged;
            currentBossHealth.OnDied += HandleBossDied;

            currentGhostRatio = Mathf.Clamp01((float)currentBossHealth.currentHealth / Mathf.Max(1, currentBossHealth.maxHealth));
            ApplyBarRatio(ghostFill, currentGhostRatio);

            if (bossNameText != null) bossNameText.text = bossName;
            UpdateDisplay(currentBossHealth.currentHealth, currentBossHealth.maxHealth);
            FadeIn();
        }
    }

    private void OnDestroy()
    {
        if (currentBossHealth != null)
        {
            currentBossHealth.OnHealthChanged -= HandleHealthChanged;
            currentBossHealth.OnDied -= HandleBossDied;
        }
    }

    private void HandleHealthChanged(int current, int max)
    {
        UpdateDisplay(current, max);
    }

    private void HandleBossDied(EnemyHealth _)
    {
        FadeOut();
    }

    private static void ApplyBarRatio(Image barImage, float ratio)
    {
        if (barImage == null) return;
        float clamped = Mathf.Clamp01(ratio);
        if (barImage.sprite == null) barImage.sprite = GetWhiteSprite();
        barImage.fillAmount = clamped;

        RectTransform rt = barImage.rectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(clamped, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    private void UpdateDisplay(int current, int max)
    {
        float ratio = Mathf.Clamp01((float)current / Mathf.Max(1, max));
        ApplyBarRatio(healthFill, ratio);

        if (healthNumbersText != null)
            healthNumbersText.text = $"{current} / {max}";

        if (ghostFill != null)
        {
            if (ghostFillRoutine != null) StopCoroutine(ghostFillRoutine);
            ghostFillRoutine = StartCoroutine(SmoothGhostFillRoutine(ratio));
        }
    }

    private IEnumerator SmoothGhostFillRoutine(float targetRatio)
    {
        yield return new WaitForSeconds(0.18f);
        if (ghostFill == null) yield break;

        float startRatio = currentGhostRatio;
        float t = 0f;
        while (t < 0.42f)
        {
            t += Time.deltaTime;
            currentGhostRatio = Mathf.Lerp(startRatio, targetRatio, t / 0.42f);
            ApplyBarRatio(ghostFill, currentGhostRatio);
            yield return null;
        }
        currentGhostRatio = targetRatio;
        ApplyBarRatio(ghostFill, targetRatio);
    }

    public void FadeIn()
    {
        if (canvasGroup != null) StartCoroutine(FadeRoutine(0f, 1f, 0.4f));
    }

    public void FadeOut()
    {
        if (canvasGroup != null) StartCoroutine(FadeRoutine(1f, 0f, 0.6f));
    }

    private IEnumerator FadeRoutine(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        canvasGroup.alpha = to;
    }

    public void HideImmediate()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    private void SetupProceduralUIIfMissing()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("BossBarCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();
            transform.SetParent(canvasObj.transform, false);
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        RectTransform rootRt = GetComponent<RectTransform>();
        if (rootRt == null) rootRt = gameObject.AddComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 1f);
        rootRt.anchorMax = new Vector2(0.5f, 1f);
        rootRt.pivot = new Vector2(0.5f, 1f);
        rootRt.anchoredPosition = new Vector2(0f, -22f);
        rootRt.sizeDelta = new Vector2(480f, 55f);

        Sprite whiteSprite = GetWhiteSprite();

        // Name
        GameObject nameObj = new GameObject("BossNameText");
        nameObj.transform.SetParent(transform, false);
        RectTransform nameRt = nameObj.AddComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 1f);
        nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.pivot = new Vector2(0.5f, 1f);
        nameRt.anchoredPosition = new Vector2(0f, 0f);
        nameRt.sizeDelta = new Vector2(0f, 22f);
        bossNameText = nameObj.AddComponent<TextMeshProUGUI>();
        bossNameText.text = "NIÑO ESPECTRAL";
        bossNameText.alignment = TextAlignmentOptions.Center;
        bossNameText.fontSize = 15;
        bossNameText.fontStyle = FontStyles.Bold;
        bossNameText.color = new Color(0.4f, 0.88f, 1f, 1f);

        // Outer Frame Border (for crisp contrast)
        GameObject frameObj = new GameObject("BarFrame");
        frameObj.transform.SetParent(transform, false);
        RectTransform frameRt = frameObj.AddComponent<RectTransform>();
        frameRt.anchorMin = new Vector2(0f, 0f);
        frameRt.anchorMax = new Vector2(1f, 0f);
        frameRt.pivot = new Vector2(0.5f, 0f);
        frameRt.anchoredPosition = new Vector2(0f, 4f);
        frameRt.sizeDelta = new Vector2(4f, 24f);
        Image frameImg = frameObj.AddComponent<Image>();
        frameImg.sprite = whiteSprite;
        frameImg.color = new Color(0.22f, 0.18f, 0.25f, 0.98f);

        // Bar Background (Pitch Black for lost HP contrast)
        GameObject barBgObj = new GameObject("BarBackground");
        barBgObj.transform.SetParent(transform, false);
        RectTransform barBgRt = barBgObj.AddComponent<RectTransform>();
        barBgRt.anchorMin = new Vector2(0f, 0f);
        barBgRt.anchorMax = new Vector2(1f, 0f);
        barBgRt.pivot = new Vector2(0.5f, 0f);
        barBgRt.anchoredPosition = new Vector2(0f, 6f);
        barBgRt.sizeDelta = new Vector2(0f, 20f);
        Image bgImg = barBgObj.AddComponent<Image>();
        bgImg.sprite = whiteSprite;
        bgImg.color = new Color(0.02f, 0.02f, 0.03f, 0.98f);

        // Ghost Damage Fill (brief orange-gold trail when taking damage)
        GameObject ghostObj = new GameObject("GhostFill");
        ghostObj.transform.SetParent(barBgObj.transform, false);
        RectTransform ghostRt = ghostObj.AddComponent<RectTransform>();
        ghostRt.anchorMin = Vector2.zero;
        ghostRt.anchorMax = Vector2.one;
        ghostRt.offsetMin = Vector2.zero;
        ghostRt.offsetMax = Vector2.zero;
        ghostFill = ghostObj.AddComponent<Image>();
        ghostFill.sprite = whiteSprite;
        ghostFill.color = new Color(1f, 0.72f, 0.18f, 0.85f);
        ghostFill.type = Image.Type.Filled;
        ghostFill.fillMethod = Image.FillMethod.Horizontal;
        ghostFill.fillOrigin = 0;

        // Health Fill (Bright Crimson Red for remaining HP)
        GameObject fillObj = new GameObject("HealthFill");
        fillObj.transform.SetParent(barBgObj.transform, false);
        RectTransform fillRt = fillObj.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        healthFill = fillObj.AddComponent<Image>();
        healthFill.sprite = whiteSprite;
        healthFill.color = new Color(0.88f, 0.15f, 0.20f, 1f);
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillOrigin = 0;

        // Health Numbers (Always spans full width of BarBackground)
        GameObject numObj = new GameObject("HealthNumbers");
        numObj.transform.SetParent(barBgObj.transform, false);
        RectTransform numRt = numObj.AddComponent<RectTransform>();
        numRt.anchorMin = Vector2.zero;
        numRt.anchorMax = Vector2.one;
        numRt.offsetMin = Vector2.zero;
        numRt.offsetMax = Vector2.zero;
        healthNumbersText = numObj.AddComponent<TextMeshProUGUI>();
        healthNumbersText.text = "250 / 250";
        healthNumbersText.alignment = TextAlignmentOptions.Center;
        healthNumbersText.fontSize = 12;
        healthNumbersText.fontStyle = FontStyles.Bold;
        healthNumbersText.color = Color.white;
    }
}
