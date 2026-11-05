using System.Collections;
using UnityEngine;

[RequireComponent(typeof(GoblinAI))]
public class GoblinCombat : MonoBehaviour
{
    [Min(1)] public int maxHealth = 3;
    public int CurrentHealth { get; private set; }
    public bool IsBanished { get; private set; }
    private GoblinWaveDirector director;
    private GoblinAI ai;
    private EnemyHealth enemyHealth;
    private SpriteRenderer visual;
    private Color originalColor;
    private float flashUntil;

    private void Awake()
    {
        CurrentHealth = maxHealth;
        ai = GetComponent<GoblinAI>();
        enemyHealth = GetComponent<EnemyHealth>();
        visual = GetComponent<SpriteRenderer>();
        if (visual != null) originalColor = visual.color;
        director = FindObjectOfType<GoblinWaveDirector>();

        // En Nivel 2 y Nivel 3 los duendes usan EnemyHealth (barra de vida real: 50, 80, 110, 150 HP).
        // Desactivar la lógica de muerte en 3 golpes del Nivel 1 para que nunca mueran prematuramente.
        if (enemyHealth != null)
        {
            enabled = false;
        }
    }

    private void Start()
    {
        if (enemyHealth != null)
        {
            enabled = false;
            return;
        }
        if (director != null) director.Register(this);
    }

    public bool TakeLanternHit()
    {
        if (enemyHealth == null) enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            if (ai != null) ai.Alert();
            return false;
        }
        return TakeDamage(10);
    }

    public bool TakeDamage(int damage, Vector2 knockbackDir = default)
    {
        if (enemyHealth == null) enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            if (ai != null) ai.Alert();
            return false;
        }

        if (IsBanished || !isActiveAndEnabled || (ai != null && !ai.isActiveAndEnabled)) return false;
        int hits = damage >= 25 ? 3 : (damage >= 15 ? 2 : 1);
        CurrentHealth = Mathf.Max(0, CurrentHealth - hits);
        flashUntil = Time.time + 0.14f;
        if (ai != null) ai.Alert();
        if (CurrentHealth == 0) Banish();
        return true;
    }

    private void LateUpdate()
    {
        if (enemyHealth != null || visual == null || IsBanished) return;
        visual.color = Time.time < flashUntil
            ? new Color(1f, 0.8f, 0.35f, originalColor.a)
            : (ai.IsWindingUp ? new Color(1f, 0.38f, 0.25f, originalColor.a) : originalColor);
    }

    private void Banish()
    {
        if (enemyHealth != null) return;

        IsBanished = true;
        ai.enabled = false;
        var body = GetComponent<Rigidbody2D>();
        body.velocity = Vector2.zero;
        body.simulated = false;
        foreach (var collider in GetComponents<Collider2D>()) collider.enabled = false;
        var sound = GetComponent<GoblinAudio>();
        if (sound != null) sound.StopForStun();
        var animator = GetComponent<Animator>();
        animator.SetBool("IsMoving", false);
        animator.SetInteger("ActionState", 3);
        animator.Play("Goblin_Death", 0, 0f);

        if (director != null) director.NotifyBanished(this);
        StartCoroutine(FadeAway());
    }

    private IEnumerator FadeAway()
    {
        float elapsed = 0f;
        while (elapsed < 0.55f)
        {
            elapsed += Time.deltaTime;
            if (visual != null)
                visual.color = new Color(1f, 0.77f, 0.4f, Mathf.Clamp01(1f - elapsed / 0.55f));
            yield return null;
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (director != null) director.Unregister(this);
    }
}
