using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class SpectralProjectile : MonoBehaviour
{
    [Header("Parámetros del Proyectil")]
    public float speed = 5.5f;
    public int damage = 15;
    public float lifetime = 5.0f;
    public Sprite projectileSprite;
    public bool isPurpleOrb = false;

    private Vector2 moveDirection = Vector2.right;
    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private UnityEngine.Rendering.Universal.Light2D orbLight;
    private float spawnTime;
    private bool hitSomething = false;

    private static Sprite cachedPurpleOrbSprite;

    private void Awake()
    {
        spawnTime = Time.time;
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.isKinematic = true;
        rb.gravityScale = 0f;

        CircleCollider2D col = GetComponent<CircleCollider2D>();
        if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.28f;

        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();

        if (projectileSprite == null)
        {
#if UNITY_EDITOR
            projectileSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Enemies/Boss/TX_SpectralProjectile.png");
#endif
        }

        if (projectileSprite != null) sr.sprite = projectileSprite;
        sr.sortingLayerName = "Layer 1";
        sr.sortingOrder = 4;

        // Añadir luz puntual al proyectil
        orbLight = GetComponent<UnityEngine.Rendering.Universal.Light2D>();
        if (orbLight == null) orbLight = gameObject.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
        orbLight.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
        orbLight.color = new Color(0.25f, 0.75f, 1f, 1f);
        orbLight.intensity = 1.1f;
        orbLight.pointLightOuterRadius = 1.6f;

        if (isPurpleOrb)
        {
            ConfigurePurpleOrb();
        }
    }

    /// <summary>
    /// Convierte la bola de energía al color morado oscuro/magenta de La Bruja (Nivel 3).
    /// </summary>
    public void ConfigurePurpleOrb()
    {
        isPurpleOrb = true;
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = GetOrCreatePurpleOrbSprite();
            sr.color = Color.white;
        }

        if (orbLight == null) orbLight = GetComponent<UnityEngine.Rendering.Universal.Light2D>();
        if (orbLight != null)
        {
            orbLight.color = new Color(0.78f, 0.22f, 1.0f, 1f);
            orbLight.intensity = 1.25f;
        }
    }

    private static Sprite GetOrCreatePurpleOrbSprite()
    {
        if (cachedPurpleOrbSprite != null) return cachedPurpleOrbSprite;

        int size = 20;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                if (d > 8.6f)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
                else if (d > 6.8f)
                {
                    // Halo exterior morado oscuro
                    tex.SetPixel(x, y, new Color(0.42f, 0.08f, 0.68f, 0.65f));
                }
                else if (d > 4.6f)
                {
                    // Cuerpo morado vibrante
                    tex.SetPixel(x, y, new Color(0.68f, 0.18f, 0.96f, 0.95f));
                }
                else if (d > 2.4f)
                {
                    // Anillo interior magenta brillante
                    tex.SetPixel(x, y, new Color(0.88f, 0.42f, 1.0f, 1.0f));
                }
                else
                {
                    // Núcleo blanco-violeta incandescente
                    tex.SetPixel(x, y, new Color(0.98f, 0.85f, 1.0f, 1.0f));
                }
            }
        }

        tex.Apply();
        cachedPurpleOrbSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        return cachedPurpleOrbSprite;
    }

    public void Launch(Vector2 direction, float customSpeed = -1f, int customDamage = -1)
    {
        moveDirection = direction.normalized;
        if (customSpeed > 0f) speed = customSpeed;
        if (customDamage > 0) damage = customDamage;

        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Update()
    {
        if (hitSomething) return;

        transform.position += (Vector3)(moveDirection * (speed * Time.deltaTime));

        // Verificación directa contra la hitbox de cuerpo completo del Player (cabeza, torso y pies)
        PlayerHealth activePlayer = PlayerHealth.ActiveInstance;
        if (activePlayer != null && !activePlayer.IsDead && activePlayer.IsProjectileTouchingBody(transform.position, 0.24f))
        {
            hitSomething = true;
            activePlayer.TakeDamage(damage);
            StartCoroutine(HitImpactRoutine());
            return;
        }

        // Pulso suave de escala
        float pulse = 1f + Mathf.Sin((Time.time - spawnTime) * 12f) * 0.12f;
        transform.localScale = Vector3.one * pulse;

        if (Time.time - spawnTime >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hitSomething) return;

        // No dañar a otros enemigos ni al jefe
        if (other.GetComponentInParent<EnemyHealth>() != null || other.GetComponentInParent<SpectralChildBossAI>() != null)
            return;

        // Si impacta al jugador
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null && !playerHealth.IsDead)
        {
            hitSomething = true;
            playerHealth.TakeDamage(damage);
            StartCoroutine(HitImpactRoutine());
            return;
        }

        // Si impacta un muro o obstáculo sólido
        if (!other.isTrigger && other.gameObject.layer != LayerMask.NameToLayer("Ignore Raycast"))
        {
            hitSomething = true;
            StartCoroutine(HitImpactRoutine());
        }
    }

    private System.Collections.IEnumerator HitImpactRoutine()
    {
        float t = 0f;
        Vector3 startScale = transform.localScale;
        Color flashCol = isPurpleOrb ? new Color(0.88f, 0.45f, 1f, 1f) : Color.white;
        while (t < 0.12f)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(startScale, startScale * 1.6f, t / 0.12f);
            if (sr != null) sr.color = new Color(flashCol.r, flashCol.g, flashCol.b, 1f - (t / 0.12f));
            yield return null;
        }
        Destroy(gameObject);
    }
}
