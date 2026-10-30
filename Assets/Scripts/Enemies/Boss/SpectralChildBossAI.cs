using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyHealth), typeof(Rigidbody2D), typeof(Collider2D))]
public class SpectralChildBossAI : MonoBehaviour
{
    public enum BossState
    {
        Spawning,
        Hovering,
        Attacking,
        Overcharging,
        Recovering,
        Dying
    }

    [Header("Estadísticas de Combate")]
    public int contactDamage = 20;
    public int projectileDamage = 15;
    public float baseMoveSpeed = 2.4f;
    public float overchargeSpeed = 4.8f;
    public float idealDistanceMin = 3.2f;
    public float idealDistanceMax = 6.2f;

    [Header("Ataques y Proyectiles")]
    public GameObject projectilePrefab;
    public float attackCooldown = 2.0f;

    [Header("Efectos Visuales y Audio")]
    public SpriteRenderer visualRenderer;
    public Color normalAuraColor = new Color(0.7f, 0.9f, 1f, 0.95f);
    public Color overchargeAuraColor = new Color(0.2f, 0.7f, 1f, 1f);
    public float bobAmplitude = 0.18f;
    public float bobFrequency = 3.6f;

    public AudioClip projectileClip;
    public AudioClip laughClip;
    public AudioClip overchargeClip;
    public AudioClip stepClip;

    [Header("Invocación de Refuerzos Durante la Pelea")]
    public GameObject goblinPrefab;
    public float minionSpawnInterval = 13.0f;
    public int maxActiveMinions = 4;

    public BossState CurrentState { get; private set; } = BossState.Spawning;

    public void TakeDamage(int damage)
    {
        if (health != null) health.TakeDamage(damage, Vector2.zero);
    }

    private EnemyHealth health;
    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private Animator animator;
    private AudioSource audioSource;
    private Transform playerTransform;
    private PlayerHealth playerHealth;

    private float nextAttackTime;
    private float contactDamageCooldown;
    private float nextMinionSpawnTime;
    private float nextStepSoundTime;
    private int attackCycleIndex = 0;
    private readonly List<EnemyHealth> activeMinions = new List<EnemyHealth>();

    // Reglas de Sobrecarga (Overcharge)
    private int overchargesTriggered = 0;
    private bool isInOvercharge = false;
    private float overchargeEndTime = 0f;
    private float nextDesperationOverchargeTime = 0f;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        rb.gravityScale = 0f;
        rb.isKinematic = true;

        if (visualRenderer == null)
            visualRenderer = GetComponentInChildren<SpriteRenderer>();

        LoadAudioClips();

        if (health != null)
        {
            health.ConfigureVariant(EnemyHealth.EnemyVariant.SpectralChildBoss);
            health.OnDamageTaken += HandleDamageTaken;
            health.OnDied += HandleDeath;
        }

        // Si existe GhostChildAI residual, desactivarlo
        var oldAi = GetComponent<GhostChildAI>();
        if (oldAi != null) oldAi.enabled = false;
    }

    private void LoadAudioClips()
    {
#if UNITY_EDITOR
        if (projectileClip == null)
            projectileClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/GhostChild/ghost_projectile.wav");
        if (laughClip == null)
            laughClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/GhostChild/ghost_laugh.wav");
        if (overchargeClip == null)
            overchargeClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/GhostChild/ghost_overcharge.wav");
        if (stepClip == null)
            stepClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/GhostChild/ghost_step.wav");
#endif
    }

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            playerTransform = p.transform;
            playerHealth = p.GetComponent<PlayerHealth>();
        }

        // Conectar barra de vida del jefe en pantalla
        if (BossHealthBarUI.Instance == null)
        {
            GameObject barObj = new GameObject("BossHealthBarUI");
            barObj.AddComponent<BossHealthBarUI>();
        }
        BossHealthBarUI.Instance.BindBoss(health, "NIÑO ESPECTRAL");

        nextMinionSpawnTime = Time.time + 8.0f;
        StartCoroutine(SpawnIntroRoutine());
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamageTaken -= HandleDamageTaken;
            health.OnDied -= HandleDeath;
        }
    }

    private IEnumerator SpawnIntroRoutine()
    {
        CurrentState = BossState.Spawning;
        if (bodyCollider != null) bodyCollider.enabled = false;

        if (audioSource != null && laughClip != null)
            audioSource.PlayOneShot(laughClip, 0.85f);

        // Aparición espectral con fade-in
        float t = 0f;
        while (t < 1.0f)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(t / 1.0f);
            if (visualRenderer != null)
                visualRenderer.color = new Color(normalAuraColor.r, normalAuraColor.g, normalAuraColor.b, alpha);
            yield return null;
        }

        if (bodyCollider != null) bodyCollider.enabled = true;
        CurrentState = BossState.Hovering;
        nextAttackTime = Time.time + 1.2f;
    }

    private void Update()
    {
        if (CurrentState == BossState.Spawning || CurrentState == BossState.Dying)
            return;

        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
            return;
        }

        // Invocación periódica de refuerzos durante la batalla
        if (Time.time >= nextMinionSpawnTime && !isInOvercharge)
        {
            nextMinionSpawnTime = Time.time + minionSpawnInterval;
            TrySpawnBattleMinions();
        }

        // Manejo del estado de sobrecarga
        if (isInOvercharge)
        {
            UpdateOvercharge();
            return;
        }

        // Verificación de fase de desesperación (últimos 30 de vida: sobrecarga cada 5s que dura 3s)
        if (health.currentHealth <= 30 && Time.time >= nextDesperationOverchargeTime)
        {
            TriggerOvercharge(3.0f);
            return;
        }

        // Movimiento de acecho normal con animaciones
        UpdateNormalMovement();

        // Ciclo de ataques a distancia
        if (Time.time >= nextAttackTime && CurrentState == BossState.Hovering)
        {
            ExecuteNextAttackPattern();
        }
    }

    private void UpdateNormalMovement()
    {
        Vector2 toPlayer = (Vector2)playerTransform.position - (Vector2)transform.position;
        float dist = toPlayer.magnitude;

        Vector2 targetPos = transform.position;
        bool isMoving = false;

        if (dist > idealDistanceMax)
        {
            // Acercarse
            targetPos = Vector2.MoveTowards(transform.position, playerTransform.position, baseMoveSpeed * Time.deltaTime);
            isMoving = true;
        }
        else if (dist < idealDistanceMin)
        {
            // Alejarse suavemente manteniendo distancia
            targetPos = Vector2.MoveTowards(transform.position, (Vector2)transform.position - toPlayer.normalized, (baseMoveSpeed * 0.9f) * Time.deltaTime);
            isMoving = true;
        }
        else
        {
            // Flotar lateralmente en órbita
            Vector2 tangent = new Vector2(-toPlayer.y, toPlayer.x).normalized;
            targetPos = (Vector2)transform.position + tangent * (baseMoveSpeed * 0.55f * Time.deltaTime);
            isMoving = true;
        }

        // Aplicar movimiento y animaciones del niño
        Vector2 moveDelta = targetPos - (Vector2)transform.position;
        transform.position = new Vector3(targetPos.x, targetPos.y + Mathf.Sin(Time.time * bobFrequency) * (bobAmplitude * Time.deltaTime), 0f);

        if (animator != null)
        {
            animator.SetBool("IsMoving", isMoving && moveDelta.sqrMagnitude > 0.00001f);
            if (isMoving && moveDelta.sqrMagnitude > 0.00001f)
            {
                int dir = Mathf.Abs(toPlayer.x) > Mathf.Abs(toPlayer.y)
                    ? (toPlayer.x > 0 ? 2 : 3)
                    : (toPlayer.y > 0 ? 1 : 0);
                animator.SetInteger("Direction", dir);

                if (Time.time >= nextStepSoundTime && audioSource != null && stepClip != null)
                {
                    nextStepSoundTime = Time.time + 0.42f;
                    audioSource.PlayOneShot(stepClip, 0.35f);
                }
            }
        }
    }

    private void HandleDamageTaken(int _)
    {
        if (isInOvercharge || CurrentState == BossState.Dying) return;

        // Activación de sobrecarga cada 50 de vida perdida:
        // Vida max = 180.
        // Primer umbral: <= 130 HP (ha perdido 50 HP) -> 5s duración
        // Segundo umbral: <= 80 HP (ha perdido 100 HP) -> 8s duración
        if (overchargesTriggered == 0 && health.currentHealth <= 130)
        {
            overchargesTriggered = 1;
            TriggerOvercharge(5.0f);
        }
        else if (overchargesTriggered == 1 && health.currentHealth <= 80)
        {
            overchargesTriggered = 2;
            TriggerOvercharge(8.0f);
        }
    }

    private void TriggerOvercharge(float duration)
    {
        isInOvercharge = true;
        CurrentState = BossState.Overcharging;
        overchargeEndTime = Time.time + duration;
        nextDesperationOverchargeTime = overchargeEndTime + 5.0f;

        if (visualRenderer != null)
        {
            visualRenderer.color = overchargeAuraColor;
        }

        if (audioSource != null)
        {
            if (laughClip != null) audioSource.PlayOneShot(laughClip, 0.95f);
            if (overchargeClip != null) audioSource.PlayOneShot(overchargeClip, 0.9f);
        }

        StartCoroutine(OverchargeRoutine(duration));
    }

    private void UpdateOvercharge()
    {
        // Movimiento descontrolado y rápido en zigzag alrededor del jugador
        Vector2 dirToPlayer = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
        Vector2 chaoticDir = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 8f) * 85f) * dirToPlayer;

        transform.position += (Vector3)(chaoticDir * (overchargeSpeed * Time.deltaTime));

        if (animator != null)
        {
            animator.SetBool("IsMoving", true);
            int dir = Mathf.Abs(chaoticDir.x) > Mathf.Abs(chaoticDir.y)
                ? (chaoticDir.x > 0 ? 2 : 3)
                : (chaoticDir.y > 0 ? 1 : 0);
            animator.SetInteger("Direction", dir);
        }

        // Pulso visual agresivo
        if (visualRenderer != null)
        {
            float pulse = Mathf.PingPong(Time.time * 10f, 1f);
            visualRenderer.color = Color.Lerp(overchargeAuraColor, Color.white, pulse);
        }
    }

    private IEnumerator OverchargeRoutine(float duration)
    {
        float shootInterval = 0.50f;
        float nextShoot = Time.time;

        while (Time.time < overchargeEndTime && health.currentHealth > 0)
        {
            if (Time.time >= nextShoot)
            {
                nextShoot = Time.time + shootInterval;
                int rand = Random.Range(0, 3);
                if (rand == 0) SpawnTripleSpread();
                else if (rand == 1) SpawnNova360(8);
                else SpawnSingleShot();
            }
            yield return null;
        }

        isInOvercharge = false;
        CurrentState = BossState.Recovering;

        if (visualRenderer != null)
            visualRenderer.color = normalAuraColor;

        // Fin de la sobrecarga: Dropea solo en las 2 sobrecargas principales por umbral (130 y 80 HP),
        // evitando dropear orbes repetitivos cada 5 segundos durante la fase de desesperación (<= 30 HP)
        if (health != null && health.currentHealth > 30 && overchargesTriggered <= 2)
        {
            SpawnOverchargeOrbs(1);
        }

        // Invocar refuerzos tras la sobrecarga
        TrySpawnBattleMinions();

        // Breve pausa de recuperación
        yield return new WaitForSeconds(0.8f);

        CurrentState = BossState.Hovering;
        nextAttackTime = Time.time + 1.0f;
    }

    private void SpawnOverchargeOrbs(int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 offset = new Vector3((i == 0 ? 0f : 0.8f), -0.4f, 0f);
            HealthPickup.SpawnDrop(transform.position + offset, HealthPickup.HealMode.OneHeart, 1);
        }
    }

    private void TrySpawnBattleMinions()
    {
        activeMinions.RemoveAll(m => m == null || m.IsDead);
        if (activeMinions.Count >= maxActiveMinions) return;

        // Spawnea 1 duende normal y 1 duende gordito en puntos seguros
        SpawnMinion(false);
        if (activeMinions.Count < maxActiveMinions)
        {
            SpawnMinion(true);
        }
    }

    private void SpawnMinion(bool isChubby)
    {
        GameObject pf = goblinPrefab;
        if (pf == null)
        {
#if UNITY_EDITOR
            pf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/PF_Goblin.prefab");
#endif
            if (pf == null) pf = Resources.Load<GameObject>("Prefabs/Enemies/PF_Goblin");
        }
        if (pf == null) return;

        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float dist = Random.Range(3.2f, 5.0f);
        Vector3 spawnPos = transform.position + new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist, 0f);
        spawnPos.x = Mathf.Clamp(spawnPos.x, -17.5f, 17.5f);
        spawnPos.y = Mathf.Clamp(spawnPos.y, -8.5f, 8.5f);

        GameObject minionObj = Instantiate(pf, spawnPos, Quaternion.identity);
        EnemyHealth eh = minionObj.GetComponent<EnemyHealth>();
        if (eh == null) eh = minionObj.AddComponent<EnemyHealth>();

        GoblinVariantConfig cfg = minionObj.GetComponent<GoblinVariantConfig>();
        if (cfg == null) cfg = minionObj.AddComponent<GoblinVariantConfig>();
        cfg.SetVariant(isChubby);

        GoblinAI ai = minionObj.GetComponent<GoblinAI>();
        if (ai != null)
        {
            ai.waitForFirstCoin = false;
            ai.enablePatrol = false;
            ai.BeginWaveCombat(1);
        }

        EnemyTeleportSpawn teleport = minionObj.GetComponent<EnemyTeleportSpawn>();
        if (teleport == null) teleport = minionObj.AddComponent<EnemyTeleportSpawn>();
        teleport.BeginSpawnSequence();

        activeMinions.Add(eh);
    }

    #region Ataques Normales con Energía Azul

    private void ExecuteNextAttackPattern()
    {
        CurrentState = BossState.Attacking;

        switch (attackCycleIndex)
        {
            case 0:
                SpawnSingleShot();
                nextAttackTime = Time.time + attackCooldown;
                break;
            case 1:
                SpawnTripleSpread();
                nextAttackTime = Time.time + attackCooldown * 1.15f;
                break;
            case 2:
                StartCoroutine(BurstRoutine(4, 0.18f));
                nextAttackTime = Time.time + attackCooldown * 1.3f;
                break;
            case 3:
                SpawnNova360(12);
                nextAttackTime = Time.time + attackCooldown * 1.4f;
                break;
        }

        attackCycleIndex = (attackCycleIndex + 1) % 4;
        StartCoroutine(ResetAttackStateRoutine(0.45f));
    }

    private void PlayAttackAnimationAndSound(Vector2 dir)
    {
        if (animator != null)
        {
            int dirInt = Mathf.Abs(dir.x) > Mathf.Abs(dir.y)
                ? (dir.x > 0 ? 2 : 3)
                : (dir.y > 0 ? 1 : 0);
            animator.SetInteger("Direction", dirInt);
            animator.SetInteger("ActionState", 2); // Animación Child_Throw_...
        }

        if (audioSource != null && projectileClip != null)
        {
            audioSource.PlayOneShot(projectileClip, 0.85f);
        }
    }

    private IEnumerator ResetAttackStateRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (animator != null) animator.SetInteger("ActionState", 0);
        if (CurrentState == BossState.Attacking)
            CurrentState = BossState.Hovering;
    }

    // 1. Disparo simple
    private void SpawnSingleShot()
    {
        if (playerTransform == null) return;
        Vector2 dir = (playerTransform.position - transform.position).normalized;
        PlayAttackAnimationAndSound(dir);
        CreateProjectile(transform.position + (Vector3)dir * 0.45f, dir, 5.8f, projectileDamage);
    }

    // 2. Disparo triple en abanico
    private void SpawnTripleSpread()
    {
        if (playerTransform == null) return;
        Vector2 baseDir = (playerTransform.position - transform.position).normalized;
        PlayAttackAnimationAndSound(baseDir);

        CreateProjectile(transform.position + (Vector3)baseDir * 0.45f, baseDir, 5.5f, projectileDamage);
        CreateProjectile(transform.position + (Vector3)baseDir * 0.45f, Quaternion.Euler(0, 0, 22f) * baseDir, 5.2f, projectileDamage);
        CreateProjectile(transform.position + (Vector3)baseDir * 0.45f, Quaternion.Euler(0, 0, -22f) * baseDir, 5.2f, projectileDamage);
    }

    // 3. Disparo circular (Nova 360)
    private void SpawnNova360(int count)
    {
        Vector2 toPlayer = playerTransform != null ? (playerTransform.position - transform.position).normalized : Vector2.down;
        PlayAttackAnimationAndSound(toPlayer);

        float angleStep = 360f / count;
        for (int i = 0; i < count; i++)
        {
            float angle = i * angleStep;
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            CreateProjectile(transform.position + (Vector3)dir * 0.45f, dir, 4.4f, projectileDamage);
        }
    }

    // 4. Ráfaga corta rápida
    private IEnumerator BurstRoutine(int shots, float delay)
    {
        for (int i = 0; i < shots; i++)
        {
            if (health.currentHealth <= 0 || playerTransform == null) yield break;
            Vector2 dir = (playerTransform.position - transform.position).normalized;
            PlayAttackAnimationAndSound(dir);
            CreateProjectile(transform.position + (Vector3)dir * 0.45f, dir, 6.4f, projectileDamage);
            yield return new WaitForSeconds(delay);
        }
    }

    private void CreateProjectile(Vector3 pos, Vector2 direction, float speed, int damage)
    {
        GameObject projObj = null;
        if (projectilePrefab != null)
        {
            projObj = Instantiate(projectilePrefab, pos, Quaternion.identity);
        }
        else
        {
            projObj = new GameObject("SpectralProjectile");
            projObj.transform.position = pos;
        }

        SpectralProjectile proj = projObj.GetComponent<SpectralProjectile>();
        if (proj == null) proj = projObj.AddComponent<SpectralProjectile>();
        proj.Launch(direction, speed, damage);
    }

    #endregion

    #region Contacto y Muerte

    private void OnTriggerStay2D(Collider2D other)
    {
        if (CurrentState == BossState.Spawning || CurrentState == BossState.Dying) return;
        if (Time.time < contactDamageCooldown) return;

        PlayerHealth ph = other.GetComponentInParent<PlayerHealth>();
        if (ph != null && !ph.IsDead)
        {
            contactDamageCooldown = Time.time + 0.9f;
            if (animator != null)
            {
                animator.SetInteger("ActionState", 1); // Child_Melee_...
                StartCoroutine(ResetAttackStateRoutine(0.35f));
            }
            ph.TakeDamage(contactDamage);
        }
    }

    private void HandleDeath(EnemyHealth _)
    {
        CurrentState = BossState.Dying;
        StopAllCoroutines();
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        if (bodyCollider != null) bodyCollider.enabled = false;
        if (animator != null)
        {
            animator.SetBool("IsMoving", false);
            animator.SetInteger("ActionState", 0);
        }

        // Destello de derrota y desvanecimiento
        float t = 0f;
        while (t < 1.2f)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - (t / 1.2f));
            if (visualRenderer != null)
            {
                visualRenderer.color = new Color(0.3f, 0.8f, 1f, alpha);
            }
            transform.localScale = Vector3.one * (1f + t * 0.35f);
            yield return null;
        }

        // Dropear Poción de Vida Completa
        HealthPickup.SpawnDrop(transform.position + Vector3.left * 0.9f, HealthPickup.HealMode.FullHealth);

        // Dropear Machete (25 DMG)
        WeaponPickup.SpawnWeaponDrop(WeaponType.Machete, transform.position + Vector3.right * 0.9f);

        Destroy(gameObject);
    }

    #endregion
}
