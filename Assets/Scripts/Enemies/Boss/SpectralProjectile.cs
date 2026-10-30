using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class SpectralProjectile : MonoBehaviour
{
    [Header("Parámetros del Proyectil")]
    public float speed = 5.5f;
    public int damage = 15;
    public float lifetime = 5.0f;
    public Sprite projectileSprite;

    private Vector2 moveDirection = Vector2.right;
    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private float spawnTime;
    private bool hitSomething = false;

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
        sr.sortingOrder = 780;

        // Añadir luz azul celestial tenue al proyectil
        var light = GetComponent<UnityEngine.Rendering.Universal.Light2D>();
        if (light == null) light = gameObject.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
        light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
        light.color = new Color(0.25f, 0.75f, 1f, 1f);
        light.intensity = 1.1f;
        light.pointLightOuterRadius = 1.6f;
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
        while (t < 0.12f)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(startScale, startScale * 1.6f, t / 0.12f);
            if (sr != null) sr.color = new Color(1f, 1f, 1f, 1f - (t / 0.12f));
            yield return null;
        }
        Destroy(gameObject);
    }
}
