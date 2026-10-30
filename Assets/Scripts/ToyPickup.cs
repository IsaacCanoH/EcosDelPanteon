using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class ToyPickup : MonoBehaviour
{
    [Range(0, 2)] public int toyId;
    private bool collected;
    private Transform playerTransform;
    private Vector3 initialLocalPos;
    private float floatTimer;

    private void Start()
    {
        initialLocalPos = transform.localPosition;
    }

    private void OnEnable()
    {
        collected = false;
        floatTimer = Random.Range(0f, Mathf.PI * 2f);
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

        string toyName = toyId == 0 ? "Osito de Peluche" : (toyId == 1 ? "Pelota Vintage" : "Trencito de Madera");
        Debug.Log($"[JUGUETE] ¡{toyName} recogido con éxito! Total: {collector.Count}/3");

        var hud = FindObjectOfType<Level2HUD>();
        if (hud != null)
        {
            hud.ShowNotification("¡JUGUETE ENCONTRADO!", $"{toyName} purificado ({collector.Count}/3)", 2.5f);
        }

        gameObject.SetActive(false);
        Destroy(gameObject, 0.1f);
        return true;
    }
}
