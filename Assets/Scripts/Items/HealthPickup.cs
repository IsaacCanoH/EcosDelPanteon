using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class HealthPickup : MonoBehaviour
{
    public enum HealMode
    {
        OneHeart = 0,   // Orbe Rojo de Vida: recupera 1 corazón
        FullHealth = 1  // Poción del Niño Espectral: cura toda la vida
    }

    [Header("Configuración de Curación")]
    public HealMode healMode = HealMode.OneHeart;
    [Min(1)] public int heartsAmount = 1;
    public bool allowPickupAtFullHealth = false;

    [Header("Efecto Visual y Atracción")]
    public float bobAmplitude = 0.08f;
    public float bobFrequency = 3.2f;
    public float magnetRadius = 2.0f;
    public float magnetSpeed = 4.8f;
    public float pickupDelay = 0.55f; // Retardo intencional para que el jugador vea el orbe rebotar en el suelo
    public AudioClip pickupSound;

    private Vector3 basePosition;
    private Transform targetPlayer;
    private bool consumed;
    private float spawnTime;
    private bool isPopping;

    private void Awake()
    {
        basePosition = transform.position;
        spawnTime = Time.time;
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingLayerName = "Layer 1";
            sr.sortingOrder = 250;
        }
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) targetPlayer = playerObj.transform;
    }

    public void LaunchPop(Vector3 origin)
    {
        StartCoroutine(PopRoutine(origin));
    }

    private System.Collections.IEnumerator PopRoutine(Vector3 origin)
    {
        isPopping = true;
        Vector2 randomScatter = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(0.35f, 0.75f);
        Vector3 targetGround = origin + new Vector3(randomScatter.x, randomScatter.y, 0f);

        float duration = 0.38f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float height = Mathf.Sin(t * Mathf.PI) * 0.45f;
            basePosition = Vector3.Lerp(origin, targetGround, t);
            transform.position = basePosition + Vector3.up * height;
            yield return null;
        }

        basePosition = targetGround;
        transform.position = targetGround;
        isPopping = false;
    }

    private void Update()
    {
        if (consumed) return;

        // Efecto de escala pulsante para mayor visibilidad
        float pulse = 1f + 0.16f * Mathf.Sin((Time.time - spawnTime) * 6.5f);
        transform.localScale = Vector3.one * pulse;

        // Atracción magnética solo tras cumplirse el retardo y finalizar el salto
        if (!isPopping && Time.time >= spawnTime + pickupDelay && targetPlayer != null)
        {
            var health = targetPlayer.GetComponent<PlayerHealth>();
            bool needsHeal = health != null && !health.IsDead &&
                             (allowPickupAtFullHealth || health.currentHealth < health.maxHealth);

            if (needsHeal)
            {
                float dist = Vector2.Distance(transform.position, targetPlayer.position);
                if (dist <= magnetRadius)
                {
                    basePosition = Vector3.MoveTowards(basePosition, targetPlayer.position, magnetSpeed * Time.deltaTime);
                }
            }
        }

        if (!isPopping)
        {
            float bobOffset = Mathf.Sin((Time.time - spawnTime) * bobFrequency) * bobAmplitude;
            transform.position = basePosition + Vector3.up * bobOffset;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (Time.time < spawnTime + pickupDelay) return;
        TryCollect(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (Time.time < spawnTime + pickupDelay) return;
        TryCollect(other);
    }

    private void TryCollect(Collider2D other)
    {
        if (consumed) return;

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null || playerHealth.IsDead) return;

        if (!allowPickupAtFullHealth && playerHealth.currentHealth >= playerHealth.maxHealth)
            return;

        bool healed;
        Level2HUD hud = FindObjectOfType<Level2HUD>();
        if (healMode == HealMode.FullHealth)
        {
            healed = playerHealth.HealFull();
            if (healed && hud != null)
            {
                hud.ShowNotification("¡POCIÓN ESPECTRAL!", "¡Salud completamente restaurada al 100%!", 4.5f);
            }
        }
        else
        {
            healed = playerHealth.HealHearts(heartsAmount);
            if (healed && hud != null)
            {
                hud.ShowNotification("ORBE DE VIDA", "+1 Corazón de salud recuperado", 2.0f);
            }
        }

        if (!healed && !allowPickupAtFullHealth) return;

        consumed = true;
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, 0.85f);
        }

        Destroy(gameObject);
    }

    private static Sprite fallbackPotionSprite;

    public static GameObject SpawnDrop(Vector3 position, HealMode mode = HealMode.OneHeart, int hearts = 1)
    {
        // En Nivel 1 nunca dropea orbes ni pociones
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName.IndexOf("Nivel1", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            FindObjectOfType<GoldCoinCollector>() != null)
        {
            return null;
        }

        if (mode == HealMode.OneHeart)
        {
            return EnemyHealth.SpawnOneHeartOrb(position);
        }

        // Poción de Vida Completa
        GameObject potion = new GameObject("Pocion_Vida_Completa");
        potion.transform.position = position;

        SpriteRenderer sr = potion.AddComponent<SpriteRenderer>();
        sr.sprite = GetOrCreatePotionSprite();
        sr.sortingLayerName = "Layer 1";
        sr.sortingOrder = 250;

        CircleCollider2D col = potion.AddComponent<CircleCollider2D>();
        col.radius = 0.35f;
        col.isTrigger = true;

        HealthPickup pickup = potion.AddComponent<HealthPickup>();
        pickup.healMode = HealMode.FullHealth;
        pickup.heartsAmount = hearts > 1 ? hearts : 5;

        return potion;
    }

    private static Sprite GetOrCreatePotionSprite()
    {
        if (fallbackPotionSprite != null) return fallbackPotionSprite;

        Sprite loaded = Resources.Load<Sprite>("Sprites/Pocion_Vida");
        if (loaded != null)
        {
            fallbackPotionSprite = loaded;
            return fallbackPotionSprite;
        }

        int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                bool isCork = (x >= 6 && x <= 9 && y >= 13 && y <= 15);
                bool isNeck = (x >= 6 && x <= 9 && y >= 10 && y <= 12);
                float distBody = Vector2.Distance(new Vector2(x, y), new Vector2(7.5f, 5.5f));
                bool isBody = distBody <= 5.2f && y <= 10;

                if (isCork)
                {
                    tex.SetPixel(x, y, new Color(0.6f, 0.4f, 0.2f, 1f));
                }
                else if (isNeck)
                {
                    if (x == 6 || x == 9)
                        tex.SetPixel(x, y, new Color(0.7f, 0.9f, 1f, 0.85f));
                    else
                        tex.SetPixel(x, y, new Color(0.2f, 0.8f, 1f, 0.9f));
                }
                else if (isBody)
                {
                    if (distBody > 4.4f)
                        tex.SetPixel(x, y, new Color(0.2f, 0.3f, 0.45f, 0.95f));
                    else if (distBody > 2.0f)
                        tex.SetPixel(x, y, new Color(0.15f, 0.85f, 0.95f, 1f));
                    else
                        tex.SetPixel(x, y, new Color(0.85f, 1f, 1f, 1f));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }

        tex.Apply();
        fallbackPotionSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
        return fallbackPotionSprite;
    }
}

