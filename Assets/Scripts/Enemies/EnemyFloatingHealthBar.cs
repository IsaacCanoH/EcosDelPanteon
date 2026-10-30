using UnityEngine;

public class EnemyFloatingHealthBar : MonoBehaviour
{
    [Header("Configuración")]
    public bool isBoss = false; // true = Azul (Niño Espectral), false = Verde (Duendes)
    public Vector3 headOffset = new Vector3(0f, 1.15f, 0f);
    public float barWidth = 0.75f;
    public float barHeight = 0.09f;

    private EnemyHealth enemyHealth;
    private Transform barContainer;
    private SpriteRenderer bgRenderer;
    private SpriteRenderer fillRenderer;
    private static Sprite pixelSprite;

    private float currentRatio = 1f;
    private float targetRatio = 1f;

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth == null)
            enemyHealth = GetComponentInParent<EnemyHealth>() ?? GetComponentInChildren<EnemyHealth>();

        var bossAI = GetComponent<SpectralChildBossAI>() ?? GetComponentInParent<SpectralChildBossAI>();
        if (bossAI != null || (enemyHealth != null && enemyHealth.variant == EnemyHealth.EnemyVariant.SpectralChildBoss))
        {
            isBoss = true;
            headOffset = new Vector3(0f, 1.45f, 0f);
            barWidth = 1.1f;
            barHeight = 0.12f;
        }

        BuildVisualBar();
    }

    private void Start()
    {
        if (enemyHealth == null)
            enemyHealth = GetComponent<EnemyHealth>() ?? GetComponentInParent<EnemyHealth>() ?? GetComponentInChildren<EnemyHealth>();

        if (enemyHealth != null)
        {
            enemyHealth.OnHealthChanged += HandleHealthChanged;
            enemyHealth.OnDied += HandleDied;
            if (enemyHealth.maxHealth > 0)
            {
                currentRatio = targetRatio = Mathf.Clamp01((float)enemyHealth.currentHealth / enemyHealth.maxHealth);
            }
        }

        UpdateFillVisual(currentRatio);
    }

    private void OnDestroy()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnHealthChanged -= HandleHealthChanged;
            enemyHealth.OnDied -= HandleDied;
        }
    }

    private void HandleHealthChanged(int current, int max)
    {
        targetRatio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
    }

    private void HandleDied(EnemyHealth h)
    {
        if (barContainer != null)
        {
            barContainer.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (barContainer == null || !barContainer.gameObject.activeSelf) return;

        if (enemyHealth != null && enemyHealth.IsDead)
        {
            barContainer.gameObject.SetActive(false);
            return;
        }

        // Mantener la barra derecha e inmóvil respecto a la rotación del personaje
        barContainer.position = transform.position + headOffset;
        barContainer.rotation = Quaternion.identity;

        // Suavizar la transición de la barra
        if (Mathf.Abs(currentRatio - targetRatio) > 0.002f)
        {
            currentRatio = Mathf.MoveTowards(currentRatio, targetRatio, Time.deltaTime * 3.5f);
            UpdateFillVisual(currentRatio);
        }
    }

    private void UpdateFillVisual(float ratio)
    {
        if (fillRenderer == null) return;

        float clamped = Mathf.Clamp01(ratio);
        float fillW = barWidth * clamped;

        // Posicionar el relleno para que encoja de derecha a izquierda
        fillRenderer.transform.localScale = new Vector3(fillW, barHeight, 1f);
        fillRenderer.transform.localPosition = new Vector3(-barWidth * 0.5f + fillW * 0.5f, 0f, 0f);

        if (isBoss)
        {
            // Barra azul espectral para el Niño
            fillRenderer.color = new Color(0.2f, 0.75f, 1f, 1f);
        }
        else
        {
            // Barra verde para los Duendes (idéntica al Nivel 1)
            fillRenderer.color = clamped > 0.6f
                ? new Color(0.28f, 0.85f, 0.35f, 1f)  // Verde vivo
                : (clamped > 0.3f
                    ? new Color(0.95f, 0.72f, 0.25f, 1f) // Ámbar
                    : new Color(0.92f, 0.28f, 0.25f, 1f)); // Rojo
        }
    }

    private void BuildVisualBar()
    {
        if (pixelSprite == null)
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                    tex.SetPixel(x, y, Color.white);
            tex.Apply();
            pixelSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1f);
        }

        GameObject containerObj = new GameObject("FloatingHealthBar");
        barContainer = containerObj.transform;
        barContainer.SetParent(transform, false);
        barContainer.localPosition = headOffset;

        // Fondo / Borde negro
        GameObject bgObj = new GameObject("Bar_BG");
        bgObj.transform.SetParent(barContainer, false);
        bgObj.transform.localScale = new Vector3(barWidth + 0.05f, barHeight + 0.04f, 1f);
        bgRenderer = bgObj.AddComponent<SpriteRenderer>();
        bgRenderer.sprite = pixelSprite;
        bgRenderer.color = new Color(0.08f, 0.08f, 0.1f, 0.9f);
        bgRenderer.sortingOrder = 590;

        // Relleno de vida (Verde para Duende / Azul para Niño)
        GameObject fillObj = new GameObject("Bar_Fill");
        fillObj.transform.SetParent(barContainer, false);
        fillRenderer = fillObj.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = pixelSprite;
        fillRenderer.sortingOrder = 591;

        UpdateFillVisual(1f);
    }
}
