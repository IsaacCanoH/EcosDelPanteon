using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Vida del jugador")]
    public int maxHealth = 5;

    public int currentHealth;
    public event System.Action OnDamaged;

    [Min(0f)] public float invulnerabilitySeconds;
    private float protectedUntil;
    private bool isDead = false;
    public bool IsProtected { get { return Time.time < protectedUntil; } }

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        if (isDead || damage <= 0 || Time.timeScale <= 0f || IsProtected)
            return;

        protectedUntil = Time.time + invulnerabilitySeconds;
        int previousHealth = currentHealth;
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        // La busqueda se interrumpe en el mismo instante del impacto.
        if (currentHealth < previousHealth)
            OnDamaged?.Invoke();

        Debug.Log("Vida del Player: " + currentHealth);

        if (currentHealth <= 0)
        {
            isDead = true;
            var movement = GetComponent<Cainos.PixelArtTopDown_Basic.TopDownCharacterController>();
            if (movement != null) movement.enabled = false;
            var body = GetComponent<Rigidbody2D>();
            if (body != null) body.velocity = Vector2.zero;
            Debug.Log("¡El Player ha muerto!");
        }
    }
}
