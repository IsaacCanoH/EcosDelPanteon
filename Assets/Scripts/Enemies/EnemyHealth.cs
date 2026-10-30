using System;
using System.Collections;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    public enum EnemyVariant
    {
        NormalGoblin = 0,       // 50 HP, 10 de daño
        ChubbyGoblin = 1,       // 80 HP, 20 de daño, suelta 1 orbe de vida (1 corazón) al morir
        SpectralChildBoss = 2   // 180 HP, 15 proyectil / 20 contacto
    }

    [Header("Variante y Estadísticas")]
    public EnemyVariant variant = EnemyVariant.NormalGoblin;
    [Min(1)] public int maxHealth = 50;
    public int currentHealth;
    public float knockbackForce = 2.2f;

    [Header("Recompensas (Drops)")]
    public bool dropsHealthOrbOnDeath = false;
    public GameObject healthOrbPrefab;

    public bool IsDead { get; private set; }
    public event Action<int, int> OnHealthChanged; // (actual, maximo)
    public event Action<int> OnDamageTaken;        // daño recibido en este impacto
    public event Action<EnemyHealth> OnDied;

    private SpriteRenderer visual;
    private Rigidbody2D rb;
    private GoblinAI goblinAI;
    private GoblinCombat legacyGoblinCombat;
    private Color originalColor = Color.white;
    private float flashUntil;
    private static Sprite fallbackOrbSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticHealthCache()
    {
        fallbackOrbSprite = null;
    }

    private void Awake()
    {
        ApplyVariantPresetIfDefault();
        currentHealth = maxHealth;

        visual = GetComponent<SpriteRenderer>();
        if (visual != null) originalColor = visual.color;

        rb = GetComponent<Rigidbody2D>();
        goblinAI = GetComponent<GoblinAI>();
        legacyGoblinCombat = GetComponent<GoblinCombat>();

        if (goblinAI != null)
        {
            goblinAI.attackDamage = variant == EnemyVariant.ChubbyGoblin ? 20 : 10;
        }
    }

    public void ConfigureVariant(EnemyVariant newVariant)
    {
        variant = newVariant;
        switch (variant)
        {
            case EnemyVariant.NormalGoblin:
                maxHealth = 50;
                knockbackForce = 2.4f;
                dropsHealthOrbOnDeath = false;
                if (goblinAI != null)
                {
                    goblinAI.attackDamage = 10;
                    goblinAI.moveSpeed = 1.4f;
                }
                break;

            case EnemyVariant.ChubbyGoblin:
                maxHealth = 80;
                knockbackForce = 1.1f; // Más pesado y resistente al empuje
                dropsHealthOrbOnDeath = false; // Probabilidad reducida gestionada en Die() (no 100%)
                if (goblinAI != null)
                {
                    goblinAI.attackDamage = 20;
                    goblinAI.moveSpeed = 0.95f;
                }
                break;

            case EnemyVariant.SpectralChildBoss:
                maxHealth = 180;
                knockbackForce = 0f;
                dropsHealthOrbOnDeath = false; // El jefe tiene su propia lógica de drops
                break;
        }
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void ApplyVariantPresetIfDefault()
    {
        if (variant == EnemyVariant.ChubbyGoblin)
        {
            if (maxHealth == 50) maxHealth = 80;
        }
        else if (variant == EnemyVariant.SpectralChildBoss)
        {
            if (maxHealth == 50) maxHealth = 180;
        }
    }

    public void TakeDamage(int amount, Vector2 hitDirection)
    {
        if (IsDead || amount <= 0 || !isActiveAndEnabled)
            return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        flashUntil = Time.time + 0.14f;

        if (goblinAI != null && goblinAI.isActiveAndEnabled)
        {
            goblinAI.Alert();
        }

        if (rb != null && knockbackForce > 0f && hitDirection.sqrMagnitude > 0.001f)
        {
            rb.velocity = Vector2.zero;
            rb.AddForce(hitDirection.normalized * knockbackForce, ForceMode2D.Impulse);
        }

        OnDamageTaken?.Invoke(amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void LateUpdate()
    {
        if (visual == null || IsDead) return;

        if (Time.time < flashUntil)
        {
            visual.color = new Color(1f, 0.35f, 0.35f, originalColor.a);
        }
        else if (goblinAI == null || !goblinAI.IsWindingUp)
        {
            visual.color = originalColor;
        }
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;

        // Exclusión estricta de Nivel 1: solo del Nivel 2 en adelante se permiten orbes de vida
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        bool isLevel1 = sceneName.IndexOf("Nivel1", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        FindObjectOfType<GoldCoinCollector>() != null;

        if (!isLevel1)
        {
            // Drop reducido en Nivel 2: solo los duendes gorditos tienen probabilidad baja (15%), duendes normales 0%
            bool dropsOrb = dropsHealthOrbOnDeath || (variant == EnemyVariant.ChubbyGoblin && UnityEngine.Random.value < 0.15f);
            if (dropsOrb)
            {
                SpawnOneHeartOrb(transform.position);
            }
        }

        OnDied?.Invoke(this);

        if (goblinAI != null) goblinAI.enabled = false;
        if (legacyGoblinCombat != null) legacyGoblinCombat.enabled = false;

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.simulated = false;
        }

        foreach (var col in GetComponentsInChildren<Collider2D>())
        {
            col.enabled = false;
        }

        var sound = GetComponent<GoblinAudio>();
        if (sound != null) sound.StopForStun();

        var animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetBool("IsMoving", false);
            animator.SetInteger("ActionState", 3);
            if (animator.HasState(0, Animator.StringToHash("Goblin_Death")))
            {
                animator.Play("Goblin_Death", 0, 0f);
            }
        }

        StartCoroutine(FadeOutAndDestroy());
    }

    public static GameObject SpawnOneHeartOrb(Vector3 position, GameObject customPrefab = null)
    {
        // En Nivel 1 nunca dropea orbes de vida
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName.IndexOf("Nivel1", StringComparison.OrdinalIgnoreCase) >= 0 ||
            FindObjectOfType<GoldCoinCollector>() != null)
        {
            return null;
        }

        if (customPrefab != null)
        {
            GameObject instance = Instantiate(customPrefab, position, Quaternion.identity);
            HealthPickup hp = instance.GetComponent<HealthPickup>();
            if (hp == null) hp = instance.AddComponent<HealthPickup>();
            hp.healMode = HealthPickup.HealMode.OneHeart;
            hp.heartsAmount = 1;
            return instance;
        }

        GameObject orb = new GameObject("Orbe_Vida_Rojo");
        orb.transform.position = position;

        SpriteRenderer sr = orb.AddComponent<SpriteRenderer>();
        sr.sprite = GetOrCreateRedOrbSprite();
        sr.sortingLayerName = "Layer 1";
        sr.sortingOrder = 250; // Visible sobre suelo, pasto y props

        CircleCollider2D col = orb.AddComponent<CircleCollider2D>();
        col.radius = 0.32f;
        col.isTrigger = true;

        HealthPickup pickup = orb.AddComponent<HealthPickup>();
        pickup.healMode = HealthPickup.HealMode.OneHeart;
        pickup.heartsAmount = 1;
        pickup.LaunchPop(position);

        return orb;
    }

    private static Sprite GetOrCreateRedOrbSprite()
    {
        if (fallbackOrbSprite != null) return fallbackOrbSprite;

        Sprite loaded = Resources.Load<Sprite>("Sprites/Orbe_Vida_Rojo");
#if UNITY_EDITOR
        if (loaded == null)
        {
            loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Items/SP_HealthOrb_Red.png");
        }
#endif
        if (loaded != null)
        {
            fallbackOrbSprite = loaded;
            return fallbackOrbSprite;
        }

        int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Vector2 center = new Vector2(7.5f, 7.5f);

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                if (d > 6.2f)
                    tex.SetPixel(x, y, Color.clear);
                else if (d > 5.0f)
                    tex.SetPixel(x, y, new Color(0.45f, 0.05f, 0.12f, 0.9f)); // Borde carmesí oscuro
                else if (d > 2.4f)
                    tex.SetPixel(x, y, new Color(0.95f, 0.16f, 0.26f, 1f));   // Cuerpo rojo vivo
                else
                    tex.SetPixel(x, y, new Color(1f, 0.72f, 0.78f, 1f));      // Núcleo brillante
            }
        }
        tex.Apply();
        fallbackOrbSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        return fallbackOrbSprite;
    }

    private IEnumerator FadeOutAndDestroy()
    {
        float duration = 0.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (visual != null)
            {
                float alpha = Mathf.Clamp01(1f - (elapsed / duration));
                visual.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}

