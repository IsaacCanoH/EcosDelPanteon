using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class ToyPickup : MonoBehaviour
{
    [Range(0, 2)] public int toyId;
    private bool collected;
    private Transform playerTransform;
    private Vector3 initialLocalPos;
    private Vector3 initialLocalScale;
    private float floatTimer;
    private SpriteRenderer sr;
    private Collider2D col;
    private static Sprite glowSprite;

    private void Awake()
    {
        initialLocalPos = transform.localPosition;
        initialLocalScale = transform.localScale;
        col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
        sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingLayerName = "Layer 1";
            sr.sortingOrder = 5;
        }
    }

    private void Start()
    {
        initialLocalPos = transform.localPosition;
        initialLocalScale = transform.localScale;
    }

    private void OnEnable()
    {
        collected = false;
        floatTimer = Random.Range(0f, Mathf.PI * 2f);
        if (initialLocalScale.sqrMagnitude > 0.001f)
            transform.localScale = initialLocalScale;
    }

    private void Update()
    {
        if (collected) return;

        // Efecto visual de flotación mágica suave
        floatTimer += Time.deltaTime * 3.5f;
        transform.localPosition = initialLocalPos + new Vector3(0f, Mathf.Sin(floatTimer) * 0.08f, 0f);

        // Chequeo de proximidad directa con el jugador
        if (playerTransform == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
            return;
        }

        float dist = Vector2.Distance(transform.position, playerTransform.position);
        if (dist <= 1.45f)
        {
            TryCollect(playerTransform.gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;
        TryCollect(other.gameObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (collected) return;
        TryCollect(other.gameObject);
    }

    public bool TryCollect(GameObject target)
    {
        if (collected || target == null) return false;

        var collector = target.GetComponentInParent<ToyCollector>();
        if (collector == null) collector = FindObjectOfType<ToyCollector>();

        var health = target.GetComponentInParent<PlayerHealth>();
        if (health == null) health = FindObjectOfType<PlayerHealth>();

        if (collector == null || (health != null && health.currentHealth <= 0)) return false;

        if (!collector.Collect(toyId)) return false;

        collected = true;
        if (col != null) col.enabled = false;

        string toyName = toyId == 0 ? "Osito de Peluche" : (toyId == 1 ? "Pelota Vintage" : "Trencito de Madera");
        Debug.Log($"[JUGUETE] ¡{toyName} recogido con éxito! Total: {collector.Count}/3");

        var hud = FindObjectOfType<Level2HUD>();
        if (hud != null)
        {
            hud.ShowNotification("¡JUGUETE ENCONTRADO!", $"{toyName} purificado ({collector.Count}/3)", 2.5f);
        }

        StartCoroutine(CollectAnimationRoutine(collector.transform));
        return true;
    }

    private IEnumerator CollectAnimationRoutine(Transform collectorTransform)
    {
        Vector3 startPos = transform.position;
        Vector3 startScale = transform.localScale;

        GameObject ringObj = new GameObject("ToyPickupGlow");
        ringObj.transform.position = startPos;
        SpriteRenderer ringSr = ringObj.AddComponent<SpriteRenderer>();
        ringSr.sprite = GetOrCreateGlowSprite();
        ringSr.sortingLayerName = "Layer 1";
        ringSr.sortingOrder = 6;
        ringSr.color = new Color(1f, 0.9f, 0.35f, 0.85f);

        float duration = 0.42f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
            elapsed += dt;
            float p = Mathf.Clamp01(elapsed / duration);

            Vector3 targetPos = collectorTransform != null
                ? collectorTransform.position + new Vector3(0f, 0.65f, 0f)
                : startPos + new Vector3(0f, 0.65f, 0f);

            transform.position = Vector3.Lerp(startPos, targetPos, Mathf.SmoothStep(0f, 1f, p))
                                 + new Vector3(0f, Mathf.Sin(p * Mathf.PI) * 0.32f, 0f);
            transform.rotation = Quaternion.Euler(0f, 0f, p * 360f);

            float scaleCurve = p < 0.4f
                ? Mathf.Lerp(1f, 1.35f, p / 0.4f)
                : Mathf.Lerp(1.35f, 0.15f, (p - 0.4f) / 0.6f);
            transform.localScale = startScale * scaleCurve;

            if (sr != null)
            {
                Color c = sr.color;
                c.a = 1f - Mathf.Pow(p, 2f);
                sr.color = c;
            }

            if (ringSr != null)
            {
                ringObj.transform.position = transform.position;
                ringObj.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 2.2f, p);
                ringSr.color = new Color(1f, 0.92f, 0.4f, (1f - p) * 0.85f);
            }

            yield return null;
        }

        if (ringObj != null) Destroy(ringObj);
        gameObject.SetActive(false);
        Destroy(gameObject, 0.05f);
    }

    private static Sprite GetOrCreateGlowSprite()
    {
        if (glowSprite != null) return glowSprite;

        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxR = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / maxR;
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }
        tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 24f);
        return glowSprite;
    }
}
