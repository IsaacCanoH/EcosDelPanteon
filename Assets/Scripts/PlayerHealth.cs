using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Vida del jugador")]
    public int maxHealth = 5;
    public int maxHearts = 5;

    public int currentHealth;
    public event Action OnDamaged;
    public event Action<int, int> OnHealthChanged;
    public event Action OnDied;

    [Min(0f)] public float invulnerabilitySeconds = 0.45f;
    private float protectedUntil;
    private bool isDead = false;

    public bool IsProtected { get { return Time.time < protectedUntil; } }
    public bool IsDead { get { return isDead; } }

    // En Nivel 2 (100 de vida y 5 corazones), cada corazón equivale a 20 de vida.
    // En Nivel 1 (5 de vida y 5 corazones), cada corazón equivale a 1 de vida.
    public int HealthPerHeart
    {
        get { return Mathf.Max(1, maxHealth / Mathf.Max(1, maxHearts)); }
    }

    public int ActiveHearts
    {
        get
        {
            if (currentHealth <= 0) return 0;
            return Mathf.Clamp(Mathf.CeilToInt((float)currentHealth / HealthPerHeart), 1, maxHearts);
        }
    }

    private void Awake()
    {
        // En Nivel 2 (con ToyCollector o armas avanzadas), la vida base es 100 HP (5 corazones x 20 HP)
        // para soportar balanceadamente daños de 10 (duende), 15 (proyectil jefe) y 20 (duende gordito).
        if ((GetComponent<ToyCollector>() != null || GetComponent<PlayerWeaponController>() != null || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Nivel2")) && maxHealth <= 10)
        {
            maxHealth = 100;
            maxHearts = 5;
        }

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
        {
            OnDamaged?.Invoke();
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        Debug.Log($"Vida del Player: {currentHealth}/{maxHealth} ({ActiveHearts}/{maxHearts} corazones)");

        if (currentHealth <= 0)
        {
            isDead = true;
            var movementL1 = GetComponent<Cainos.PixelArtTopDown_Basic.TopDownCharacterController>();
            if (movementL1 != null) movementL1.enabled = false;

            var movementL2 = GetComponent<Cainos.PixelArtTopDown_Basic.Level2CharacterController>();
            if (movementL2 != null) movementL2.enabled = false;

            var body = GetComponent<Rigidbody2D>();
            if (body != null) body.velocity = Vector2.zero;
            Debug.Log("¡El Player ha muerto!");

            OnDied?.Invoke();
            PlayerDeathScreenUI.ShowDeathScreen();
        }
    }

    /// <summary>
    /// Cura una cantidad exacta de corazones (por defecto 1 corazón = 20 HP en Nivel 2).
    /// Usado por los Orbes Rojos de Vida que sueltan los Duendes Gorditos y la Sobrecarga del Jefe.
    /// </summary>
    public bool HealHearts(int heartsToRestore = 1)
    {
        if (isDead || heartsToRestore <= 0 || currentHealth >= maxHealth)
            return false;

        return Heal(heartsToRestore * HealthPerHeart);
    }

    /// <summary>
    /// Cura toda la vida al 100%.
    /// Usado por la Poción de Vida que suelta el Niño Espectral al morir.
    /// </summary>
    public bool HealFull()
    {
        if (isDead || currentHealth >= maxHealth)
            return false;

        return Heal(maxHealth);
    }

    public bool Heal(int amount)
    {
        if (isDead || amount <= 0 || currentHealth >= maxHealth)
            return false;

        int previous = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        if (currentHealth > previous)
        {
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            Debug.Log($"Player curado (+{currentHealth - previous}): {currentHealth}/{maxHealth}");
            return true;
        }
        return false;
    }
}
