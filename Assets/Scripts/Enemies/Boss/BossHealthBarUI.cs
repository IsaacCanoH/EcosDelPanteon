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

    private void UpdateDisplay(int current, int max)
    {
        float ratio = Mathf.Clamp01((float)current / Mathf.Max(1, max));
        if (healthFill != null) healthFill.fillAmount = ratio;
        if (healthNumbersText != null) healthNumbersText.text = $"{current} / {max}";

        if (ghostFill != null)
        {
            if (ghostFillRoutine != null) StopCoroutine(ghostFillRoutine);
            ghostFillRoutine = StartCoroutine(SmoothGhostFillRoutine(ratio));
        }
    }

    private IEnumerator SmoothGhostFillRoutine(float targetRatio)
    {
        yield return new WaitForSeconds(0.2f);
        if (ghostFill == null) yield break;

        float startRatio = ghostFill.fillAmount;
        float t = 0f;
        while (t < 0.45f)
        {
            t += Time.deltaTime;
            ghostFill.fillAmount = Mathf.Lerp(startRatio, targetRatio, t / 0.45f);
            yield return null;
        }
        ghostFill.fillAmount = targetRatio;
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
            canvasObj.AddComponent<CanvasScaler>();
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

        // Bar Background
        GameObject barBgObj = new GameObject("BarBackground");
        barBgObj.transform.SetParent(transform, false);
        RectTransform barBgRt = barBgObj.AddComponent<RectTransform>();
        barBgRt.anchorMin = new Vector2(0f, 0f);
        barBgRt.anchorMax = new Vector2(1f, 0f);
        barBgRt.pivot = new Vector2(0.5f, 0f);
        barBgRt.anchoredPosition = new Vector2(0f, 6f);
        barBgRt.sizeDelta = new Vector2(0f, 20f);
        Image bgImg = barBgObj.AddComponent<Image>();
        bgImg.color = new Color(0.08f, 0.08f, 0.12f, 0.9f);

        // Ghost Damage Fill
        GameObject ghostObj = new GameObject("GhostFill");
        ghostObj.transform.SetParent(barBgObj.transform, false);
        RectTransform ghostRt = ghostObj.AddComponent<RectTransform>();
        ghostRt.anchorMin = Vector2.zero;
        ghostRt.anchorMax = Vector2.one;
        ghostRt.sizeDelta = Vector2.zero;
        ghostFill = ghostObj.AddComponent<Image>();
        ghostFill.color = new Color(1f, 0.75f, 0.2f, 0.8f);
        ghostFill.type = Image.Type.Filled;
        ghostFill.fillMethod = Image.FillMethod.Horizontal;
        ghostFill.fillOrigin = 0;

        // Health Fill
        GameObject fillObj = new GameObject("HealthFill");
        fillObj.transform.SetParent(barBgObj.transform, false);
        RectTransform fillRt = fillObj.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.sizeDelta = Vector2.zero;
        healthFill = fillObj.AddComponent<Image>();
        healthFill.color = new Color(0.85f, 0.18f, 0.22f, 1f);
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillOrigin = 0;

        // Health Numbers
        GameObject numObj = new GameObject("HealthNumbers");
        numObj.transform.SetParent(barBgObj.transform, false);
        RectTransform numRt = numObj.AddComponent<RectTransform>();
        numRt.anchorMin = Vector2.zero;
        numRt.anchorMax = Vector2.one;
        numRt.sizeDelta = Vector2.zero;
        healthNumbersText = numObj.AddComponent<TextMeshProUGUI>();
        healthNumbersText.text = "180 / 180";
        healthNumbersText.alignment = TextAlignmentOptions.Center;
        healthNumbersText.fontSize = 12;
        healthNumbersText.fontStyle = FontStyles.Bold;
        healthNumbersText.color = Color.white;
    }
}
