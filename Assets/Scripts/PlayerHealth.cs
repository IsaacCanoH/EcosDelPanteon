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

    public static PlayerHealth ActiveInstance { get; private set; }

    public bool IsProtected { get { return Time.time < protectedUntil; } }
    public bool IsDead { get { return isDead; } }

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
        ActiveInstance = this;

        // En Nivel 2 y Nivel 3 (con ToyCollector o armas avanzadas), la vida base es 100 HP (5 corazones x 20 HP)
        // para soportar balanceadamente daños de 20 (duende normal = 1 corazón), 20 (proyectil/jefe) y 40 (duende gordito = 2 corazones).
        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if ((GetComponent<ToyCollector>() != null || GetComponent<PlayerWeaponController>() != null || activeScene.Contains("Nivel2") || activeScene.Contains("Nivel3")) && maxHealth <= 10)
        {
            maxHealth = 100;
            maxHearts = 5;
        }

        currentHealth = maxHealth;
        EnsureFullBodyHurtbox();
    }

    private void OnEnable()
    {
        ActiveInstance = this;
    }

    /// <summary>
    /// Crea un trigger hurtbox de cuerpo completo (desde los pies hasta la cabeza del sprite)
    /// para que las flechas y bolas de energía impacten con precisión tanto en la cabeza, torso o pies,
    /// sin alterar el colisionador sólido pequeño de los pies que se usa para caminar entre obstáculos.
    /// </summary>
    private void EnsureFullBodyHurtbox()
    {
        Transform hurtboxTr = transform.Find("PlayerProjectileHurtbox");
        if (hurtboxTr == null)
        {
            GameObject hurtboxObj = new GameObject("PlayerProjectileHurtbox");
            hurtboxObj.transform.SetParent(transform, false);
            hurtboxObj.transform.localPosition = Vector3.zero;
            hurtboxObj.transform.localRotation = Quaternion.identity;
            hurtboxObj.transform.localScale = Vector3.one;
            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycastLayer >= 0)
            {
                hurtboxObj.layer = ignoreRaycastLayer;
            }
            hurtboxTr = hurtboxObj.transform;
        }

        BoxCollider2D fullBodyHurtbox = hurtboxTr.GetComponent<BoxCollider2D>();
        if (fullBodyHurtbox == null)
        {
            fullBodyHurtbox = hurtboxTr.gameObject.AddComponent<BoxCollider2D>();
        }

        fullBodyHurtbox.isTrigger = true;
        float sx = Mathf.Max(0.1f, Mathf.Abs(transform.lossyScale.x));
        float sy = Mathf.Max(0.1f, Mathf.Abs(transform.lossyScale.y));
        fullBodyHurtbox.offset = new Vector2(0f, 0.50f / sy);
        fullBodyHurtbox.size = new Vector2(0.54f / sx, 1.10f / sy);
    }

    /// <summary>
    /// Verifica si un proyectil en coordenadas de mundo impacta en cualquier parte del cuerpo del jugador
    /// (pies, torso o cabeza).
    /// </summary>
    public bool IsProjectileTouchingBody(Vector2 projectileWorldPos, float projectileRadius = 0.18f)
    {
        if (isDead) return false;

        Vector2 pos = transform.position;
        float minX = pos.x - 0.27f - projectileRadius;
        float maxX = pos.x + 0.27f + projectileRadius;
        float minY = pos.y - 0.06f - projectileRadius;
        float maxY = pos.y + 1.06f + projectileRadius;

        return projectileWorldPos.x >= minX && projectileWorldPos.x <= maxX &&
               projectileWorldPos.y >= minY && projectileWorldPos.y <= maxY;
    }

    public void TakeDamage(int damage) => ApplyDamage(damage, false);

    internal void TakeDarkRainDamage() => ApplyDamage(HealthPerHeart, true);

    private void ApplyDamage(int damage, bool bypassProtection)
    {
        if (isDead || damage <= 0 || Time.timeScale <= 0f || (!bypassProtection && IsProtected))
            return;

        protectedUntil = Mathf.Max(protectedUntil, Time.time + invulnerabilitySeconds);
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
    /// Usado por los Orbes Rojos de Vida que sueltan los Duendes Gorditos.
    /// Solo se consume si al jugador le falta al menos 1 corazón visible.
    /// </summary>
    public bool HealHearts(int heartsToRestore = 1)
    {
        if (isDead || heartsToRestore <= 0 || currentHealth >= maxHealth || ActiveHearts >= maxHearts)
            return false;

        return Heal(heartsToRestore * HealthPerHeart);
    }

    /// <summary>
    /// Cura toda la vida al 100%.
    /// Usado por la Poción de Vida que suelta el Niño Espectral al morir.
    /// </summary>
    public bool HealFull()
    {
        if (isDead || currentHealth >= maxHealth || ActiveHearts >= maxHearts)
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
