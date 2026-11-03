using System;
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
    public int contactDamage = 15;
    public int projectileDamage = 15;
    public float baseMoveSpeed = 1.65f;
    public float overchargeSpeed = 2.05f;
    public float idealDistanceMin = 2.0f;
    public float idealDistanceMax = 3.8f;
    public float maxAltarLeash = 6.2f;

    [Header("Límites del Mapa (Bardas Perimetrales)")]
    public const float MinMapX = -16.8f;
    public const float MaxMapX = 16.8f;
    public const float MinMapY = -7.6f;
    public const float MaxMapY = 7.6f;

    [Header("Ataques y Proyectiles")]
    public GameObject projectilePrefab;
    public float attackCooldown = 2.0f;

    [Header("Efectos Visuales y Audio")]
    public SpriteRenderer visualRenderer;
    public Color normalAuraColor = new Color(0.7f, 0.9f, 1f, 0.95f);
    public Color overchargeAuraColor = new Color(0.2f, 0.7f, 1f, 1f);
    public float bobAmplitude = 0.14f;
    public float bobFrequency = 3.2f;

    public AudioClip projectileClip;
    public AudioClip laughClip;
    public AudioClip overchargeClip;
    public AudioClip stepClip;

    [Header("Invocación de Refuerzos Durante la Pelea")]
    public GameObject goblinPrefab;
    public float minionSpawnInterval = 14.0f;
    public int maxActiveMinions = 4;

    public BossState CurrentState { get; private set; } = BossState.Spawning;
    public event Action<Vector3> OnDeathCinematicFinished;

    private static Sprite softGlowSprite;

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

    private Vector2 altarOrigin;
    private float nextAttackTime;
    private float nextMinionSpawnTime;
    private float nextStepSoundTime;
    private int attackCycleIndex = 0;
    private int obstaclesMask;
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

        int obsLayer = LayerMask.NameToLayer("Obstacles");
        obstaclesMask = obsLayer >= 0 ? (1 << obsLayer) : (1 << 6);

        // Configuración física para colisionar de manera sólida con los muros perimetrales
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (visualRenderer == null)
            visualRenderer = GetComponentInChildren<SpriteRenderer>();

        if (visualRenderer != null)
        {
            visualRenderer.sortingLayerName = "Layer 1";
            visualRenderer.sortingOrder = 2;
        }

        LoadAudioClips();

        if (health != null)
        {
            health.ConfigureVariant(EnemyHealth.EnemyVariant.SpectralChildBoss);
            health.maxHealth = 250;
            health.currentHealth = 250;
            health.OnDamageTaken += HandleDamageTaken;
            health.OnDied += HandleDeath;
        }

        // Velocidad moderada: un poquito más rápido pero contenido
        baseMoveSpeed = 1.65f;
        overchargeSpeed = 2.05f;

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
        // Anclar punto de origen en el tercer altar y garantizar posición libre de tumbas/obstáculos
        altarOrigin = transform.position;
        foreach (var zone in FindObjectsOfType<ToyEncounterZone>())
        {
            if (zone != null && zone.encounterIndex == 2)
            {
                altarOrigin = zone.transform.position;
                break;
            }
        }

        Vector2 safeSpawn = FindClearPosition(transform.position);
        transform.position = safeSpawn;
        rb.position = safeSpawn;

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

        nextMinionSpawnTime = Time.time + 9.0f;
        StartCoroutine(SpawnIntroRoutine());
    }

    public Vector2 FindClearPosition(Vector2 desiredPos)
    {
        // Verificar si la posición deseada está libre de tumbas/obstáculos
        if (!IsPositionBlocked(desiredPos, 0.55f))
            return desiredPos;

        // Candidatos prioritarios alrededor del centro/norte del Altar 3 (lejos de las tumbas del sur)
        Vector2[] offsets = new Vector2[]
        {
            new Vector2(0f, 0.85f),
            new Vector2(0f, 1.35f),
            new Vector2(-1.1f, 0.7f),
            new Vector2(1.1f, 0.7f),
            new Vector2(-1.5f, 1.2f),
            new Vector2(1.5f, 1.2f),
            new Vector2(0f, 2.0f)
        };

        foreach (var off in offsets)
        {
            Vector2 candidate = desiredPos + off;
            candidate.x = Mathf.Clamp(candidate.x, MinMapX, MaxMapX);
            candidate.y = Mathf.Clamp(candidate.y, MinMapY, MaxMapY);
            if (!IsPositionBlocked(candidate, 0.55f))
                return candidate;
        }

        return desiredPos + new Vector2(0f, 1.0f);
    }

    private bool IsPositionBlocked(Vector2 pos, float radius)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(pos, radius, obstaclesMask);
        foreach (var h in hits)
        {
            if (h != null && !h.isTrigger && h.gameObject != gameObject)
                return true;
        }
        return false;
    }

    private Vector2 AdjustDirectionForObstacles(Vector2 currentPos, Vector2 desiredDir, float stepDist)
    {
        if (desiredDir.sqrMagnitude < 0.0001f) return Vector2.zero;

        Vector2 normDir = desiredDir.normalized;
        float checkRadius = 0.42f;
        RaycastHit2D hit = Physics2D.CircleCast(currentPos, checkRadius, normDir, stepDist + 0.35f, obstaclesMask);
        if (hit.collider == null || hit.collider.isTrigger || hit.collider.gameObject == gameObject)
            return desiredDir;

        // Deslizarse suavemente rodeando la tumba u obstáculo
        Vector2 slideA = new Vector2(-hit.normal.y, hit.normal.x);
        Vector2 slideB = -slideA;
        Vector2 chosenSlide = Vector2.Dot(slideA, normDir) >= Vector2.Dot(slideB, normDir) ? slideA : slideB;

        RaycastHit2D checkSlide = Physics2D.CircleCast(currentPos, checkRadius * 0.9f, chosenSlide, stepDist + 0.25f, obstaclesMask);
        if (checkSlide.collider == null || checkSlide.collider.isTrigger)
            return chosenSlide * desiredDir.magnitude;

        return (-normDir + hit.normal).normalized * desiredDir.magnitude;
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
            audioSource.PlayOneShot(laughClip, 0.85f * AudioManager.EnemyVolume);

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

        // Verificación de fase de desesperación (últimos 45 de vida: sobrecarga cada 7s que dura 5s)
        if (health.currentHealth <= 45 && Time.time >= nextDesperationOverchargeTime)
        {
            TriggerOvercharge(5.0f);
            return;
        }

        // Movimiento de acecho lento y contenido cerca del altar y del jugador
        UpdateNormalMovement();

        // Ciclo de ataques a distancia
        if (Time.time >= nextAttackTime && CurrentState == BossState.Hovering)
        {
            ExecuteNextAttackPattern();
        }
    }

    private void UpdateNormalMovement()
    {
        Vector2 currentPos = rb.position;
        Vector2 toPlayer = (Vector2)playerTransform.position - currentPos;
        float distToPlayer = toPlayer.magnitude;

        Vector2 moveDir;
        if (distToPlayer > idealDistanceMax)
        {
            moveDir = toPlayer.normalized;
        }
        else if (distToPlayer < idealDistanceMin)
        {
            moveDir = -toPlayer.normalized * 0.8f;
        }
        else
        {
            Vector2 tangent = new Vector2(-toPlayer.y, toPlayer.x).normalized;
            moveDir = tangent * 0.82f;
        }

        float step = baseMoveSpeed * Time.deltaTime;
        moveDir = AdjustDirectionForObstacles(currentPos, moveDir, step);
        Vector2 targetPos = currentPos + moveDir * step;

        float distFromAltar = Vector2.Distance(targetPos, altarOrigin);
        if (distFromAltar > maxAltarLeash)
        {
            Vector2 toAltar = (altarOrigin - currentPos).normalized;
            toAltar = AdjustDirectionForObstacles(currentPos, toAltar, step);
            targetPos = currentPos + toAltar * (baseMoveSpeed * 1.1f * Time.deltaTime);
        }

        targetPos.x = Mathf.Clamp(targetPos.x, MinMapX, MaxMapX);
        targetPos.y = Mathf.Clamp(targetPos.y, MinMapY, MaxMapY);

        rb.MovePosition(targetPos);

        if (animator != null)
        {
            bool isMoving = moveDir.sqrMagnitude > 0.001f;
            animator.SetBool("IsMoving", isMoving);

            if (isMoving)
            {
                int dir = Mathf.Abs(toPlayer.x) > Mathf.Abs(toPlayer.y)
                    ? (toPlayer.x > 0 ? 2 : 3)
                    : (toPlayer.y > 0 ? 1 : 0);
                animator.SetInteger("Direction", dir);

                if (Time.time >= nextStepSoundTime && audioSource != null && stepClip != null)
                {
                    nextStepSoundTime = Time.time + 0.48f;
                    audioSource.PlayOneShot(stepClip, 0.35f * AudioManager.EnemyVolume);
                }
            }
        }
    }

    private void HandleDamageTaken(int _)
    {
        if (isInOvercharge || CurrentState == BossState.Dying) return;

        // Con 250 HP máximos: sobrecargas a 190 HP, 130 HP y 75 HP
        if (overchargesTriggered == 0 && health.currentHealth <= 190)
        {
            overchargesTriggered = 1;
            TriggerOvercharge(5.0f);
        }
        else if (overchargesTriggered == 1 && health.currentHealth <= 130)
        {
            overchargesTriggered = 2;
            TriggerOvercharge(5.0f);
        }
        else if (overchargesTriggered == 2 && health.currentHealth <= 75)
        {
            overchargesTriggered = 3;
            TriggerOvercharge(5.0f);
        }
    }

    private void TriggerOvercharge(float duration = 5.0f)
    {
        isInOvercharge = true;
        CurrentState = BossState.Overcharging;
        overchargeEndTime = Time.time + duration;
        nextDesperationOverchargeTime = overchargeEndTime + 7.0f;

        if (visualRenderer != null)
        {
            visualRenderer.color = overchargeAuraColor;
        }

        if (audioSource != null)
        {
            if (laughClip != null) audioSource.PlayOneShot(laughClip, 0.95f * AudioManager.EnemyVolume);
            if (overchargeClip != null) audioSource.PlayOneShot(overchargeClip, 0.9f * AudioManager.EnemyVolume);
        }

        StartCoroutine(OverchargeRoutine(duration));
    }

    private void UpdateOvercharge()
    {
        if (playerTransform == null) return;

        Vector2 currentPos = rb.position;
        Vector2 toPlayer = (Vector2)playerTransform.position - currentPos;
        float dist = toPlayer.magnitude;

        Vector2 dirToPlayer = toPlayer.normalized;
        Vector2 tangent = new Vector2(-dirToPlayer.y, dirToPlayer.x);
        float orbitSign = Mathf.Sin(Time.time * 2.2f);
        Vector2 moveDir = (tangent * orbitSign * 0.75f + (dist > 3.0f ? dirToPlayer * 0.45f : (dist < 1.8f ? -dirToPlayer * 0.45f : Vector2.zero))).normalized;

        float step = overchargeSpeed * Time.deltaTime;
        moveDir = AdjustDirectionForObstacles(currentPos, moveDir, step);
        Vector2 targetPos = currentPos + moveDir * step;

        if (Vector2.Distance(targetPos, altarOrigin) > maxAltarLeash)
        {
            Vector2 toAltar = AdjustDirectionForObstacles(currentPos, (altarOrigin - currentPos).normalized, step);
            targetPos = currentPos + toAltar * step;
        }

        targetPos.x = Mathf.Clamp(targetPos.x, MinMapX, MaxMapX);
        targetPos.y = Mathf.Clamp(targetPos.y, MinMapY, MaxMapY);

        rb.MovePosition(targetPos);

        if (animator != null)
        {
            animator.SetBool("IsMoving", true);
            int dir = Mathf.Abs(toPlayer.x) > Mathf.Abs(toPlayer.y)
                ? (toPlayer.x > 0 ? 2 : 3)
                : (toPlayer.y > 0 ? 1 : 0);
            animator.SetInteger("Direction", dir);
        }

        if (visualRenderer != null)
        {
            float pulse = Mathf.PingPong(Time.time * 8f, 1f);
            visualRenderer.color = Color.Lerp(overchargeAuraColor, Color.white, pulse);
        }
    }

    private IEnumerator OverchargeRoutine(float duration)
    {
        float volleyInterval = 0.95f;
        float nextVolleyTime = Time.time;

        while (Time.time < overchargeEndTime && health.currentHealth > 0)
        {
            if (Time.time >= nextVolleyTime && playerTransform != null)
            {
                nextVolleyTime = Time.time + volleyInterval;
                ExecuteCombinedOverchargeBarrage();
            }
            yield return null;
        }

        isInOvercharge = false;
        CurrentState = BossState.Recovering;

        if (visualRenderer != null)
            visualRenderer.color = normalAuraColor;

        if (health != null && health.currentHealth > 0 && overchargesTriggered <= 2)
        {
            SpawnOverchargeOrbs(1);
        }

        TrySpawnBattleMinions();

        yield return new WaitForSeconds(0.75f);

        CurrentState = BossState.Hovering;
        nextAttackTime = Time.time + 1.0f;
    }

    private void ExecuteCombinedOverchargeBarrage()
    {
        if (playerTransform == null || health.currentHealth <= 0) return;

        Vector2 dirToPlayer = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
        PlayAttackAnimationAndSound(dirToPlayer);

        // 1. Disparo circular de 8 esferas en 360 grados
        SpawnNova360(8);

        // 2. Disparo triple en abanico dirigido al jugador
        CreateProjectile(transform.position + (Vector3)dirToPlayer * 0.45f, dirToPlayer, 5.2f, projectileDamage);
        CreateProjectile(transform.position + (Vector3)dirToPlayer * 0.45f, Quaternion.Euler(0, 0, 20f) * dirToPlayer, 5.0f, projectileDamage);
        CreateProjectile(transform.position + (Vector3)dirToPlayer * 0.45f, Quaternion.Euler(0, 0, -20f) * dirToPlayer, 5.0f, projectileDamage);

        // 3. Ráfaga directa de 3 proyectiles rápidos dirigidos al jugador
        StartCoroutine(BurstRoutine(3, 0.12f));
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

        float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float dist = UnityEngine.Random.Range(2.8f, 4.5f);
        Vector2 candidate = (Vector2)transform.position + new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
        candidate.x = Mathf.Clamp(candidate.x, MinMapX, MaxMapX);
        candidate.y = Mathf.Clamp(candidate.y, MinMapY, MaxMapY);
        Vector2 spawnPos = FindClearPosition(candidate);

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
                SpawnNova360(10);
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
            animator.SetInteger("ActionState", 2);
        }

        if (audioSource != null && projectileClip != null)
        {
            audioSource.PlayOneShot(projectileClip, 0.85f * AudioManager.EnemyVolume);
        }
    }

    private IEnumerator ResetAttackStateRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (animator != null) animator.SetInteger("ActionState", 0);
        if (CurrentState == BossState.Attacking)
            CurrentState = BossState.Hovering;
    }

    private void SpawnSingleShot()
    {
        if (playerTransform == null) return;
        Vector2 dir = (playerTransform.position - transform.position).normalized;
        PlayAttackAnimationAndSound(dir);
        CreateProjectile(transform.position + (Vector3)dir * 0.45f, dir, 5.5f, projectileDamage);
    }

    private void SpawnTripleSpread()
    {
        if (playerTransform == null) return;
        Vector2 baseDir = (playerTransform.position - transform.position).normalized;
        PlayAttackAnimationAndSound(baseDir);

        CreateProjectile(transform.position + (Vector3)baseDir * 0.45f, baseDir, 5.2f, projectileDamage);
        CreateProjectile(transform.position + (Vector3)baseDir * 0.45f, Quaternion.Euler(0, 0, 22f) * baseDir, 5.0f, projectileDamage);
        CreateProjectile(transform.position + (Vector3)baseDir * 0.45f, Quaternion.Euler(0, 0, -22f) * baseDir, 5.0f, projectileDamage);
    }

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

    private IEnumerator BurstRoutine(int shots, float delay)
    {
        for (int i = 0; i < shots; i++)
        {
            if (health.currentHealth <= 0 || playerTransform == null) yield break;
            Vector2 dir = (playerTransform.position - transform.position).normalized;
            PlayAttackAnimationAndSound(dir);
            CreateProjectile(transform.position + (Vector3)dir * 0.45f, dir, 6.0f, projectileDamage);
            yield return new WaitForSeconds(delay);
        }
    }

    private void CreateProjectile(Vector3 pos, Vector2 direction, float speed, int damage)
    {
        GameObject projObj;
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

    #region Cinemática de Derrota y Desvanecimiento Espectral

    private void OnTriggerStay2D(Collider2D other)
    {
        if (CurrentState == BossState.Spawning || CurrentState == BossState.Dying) return;
    }

    private void HandleDeath(EnemyHealth _)
    {
        if (CurrentState == BossState.Dying) return;
        CurrentState = BossState.Dying;
        StopAllCoroutines();

        // Limpiar esbirros invocados activos para que no interrumpan la cinemática
        foreach (var minion in activeMinions)
        {
            if (minion != null && minion.gameObject != null)
            {
                Destroy(minion.gameObject);
            }
        }
        activeMinions.Clear();

        // Eliminar proyectiles espectrales en vuelo
        foreach (var proj in FindObjectsOfType<SpectralProjectile>())
        {
            if (proj != null) Destroy(proj.gameObject);
        }

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        if (bodyCollider != null) bodyCollider.enabled = false;
        if (rb != null) rb.velocity = Vector2.zero;
        if (animator != null)
        {
            animator.SetBool("IsMoving", false);
            animator.SetInteger("ActionState", 0);
            animator.SetInteger("Direction", 0);
        }

        if (audioSource != null)
        {
            if (overchargeClip != null) audioSource.PlayOneShot(overchargeClip, 0.9f * AudioManager.EnemyVolume);
            if (laughClip != null) audioSource.PlayOneShot(laughClip, 0.75f * AudioManager.EnemyVolume);
        }

        Vector3 startPos = transform.position;
        Vector3 startScale = transform.localScale;

        // Crear halo espectral y partículas de alma ascendentes para la cinemática de desvanecimiento
        GameObject auraObj = new GameObject("BossDeathAura");
        auraObj.transform.SetParent(transform, false);
        auraObj.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        SpriteRenderer auraSr = auraObj.AddComponent<SpriteRenderer>();
        auraSr.sprite = GetOrCreateSoftGlowSprite();
        auraSr.sortingLayerName = "Layer 1";
        auraSr.sortingOrder = visualRenderer != null ? visualRenderer.sortingOrder - 1 : 1;
        auraSr.color = new Color(0.35f, 0.9f, 1f, 0.75f);

        List<Transform> wisps = new List<Transform>();
        List<SpriteRenderer> wispRenderers = new List<SpriteRenderer>();
        for (int i = 0; i < 8; i++)
        {
            GameObject w = new GameObject($"SoulWisp_{i}");
            w.transform.position = startPos + new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), UnityEngine.Random.Range(0f, 0.5f), 0f);
            w.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.22f, 0.38f);
            SpriteRenderer wsr = w.AddComponent<SpriteRenderer>();
            wsr.sprite = GetOrCreateSoftGlowSprite();
            wsr.sortingLayerName = "Layer 1";
            wsr.sortingOrder = visualRenderer != null ? visualRenderer.sortingOrder + 1 : 3;
            wsr.color = new Color(0.55f, 0.95f, 1f, 0.85f);
            wisps.Add(w.transform);
            wispRenderers.Add(wsr);
        }

        // Cinemática de desaparición lenta (2.4 segundos)
        float duration = 2.4f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
            elapsed += dt;
            float p = Mathf.Clamp01(elapsed / duration);

            // Temblor etéreo que disminuye conforme se eleva y se disipa
            float tremor = Mathf.Sin(elapsed * 42f) * 0.055f * (1f - p * 0.6f);
            float riseY = Mathf.SmoothStep(0f, 0.65f, p);
            transform.position = startPos + new Vector3(tremor, riseY, 0f);

            // Pulso luminoso entre cian espectral y blanco antes de desvanecerse lentamente
            float alpha = Mathf.Pow(1f - p, 1.25f);
            float flash = Mathf.PingPong(elapsed * 6f, 1f);
            if (visualRenderer != null)
            {
                Color c = Color.Lerp(new Color(0.3f, 0.88f, 1f, 1f), Color.white, flash * 0.65f);
                c.a = alpha;
                visualRenderer.color = c;
            }

            // Ligero estiramiento ascendente al evaporarse
            transform.localScale = new Vector3(
                startScale.x * Mathf.Lerp(1f, 0.72f, p),
                startScale.y * Mathf.Lerp(1f, 1.28f, p),
                1f
            );

            if (auraSr != null)
            {
                float auraScale = Mathf.Lerp(1.2f, 2.8f, p);
                auraObj.transform.localScale = new Vector3(auraScale, auraScale, 1f);
                auraSr.color = new Color(0.4f, 0.95f, 1f, (1f - p) * 0.75f);
            }

            for (int i = 0; i < wisps.Count; i++)
            {
                if (wisps[i] == null) continue;
                float angle = elapsed * 3.2f + i * (Mathf.PI * 2f / wisps.Count);
                float radius = Mathf.Lerp(0.45f, 0.12f, p);
                wisps[i].position = transform.position + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0.25f + (elapsed * (0.45f + (i % 3) * 0.15f)) % 1.1f,
                    0f
                );
                wispRenderers[i].color = new Color(0.6f, 0.96f, 1f, (1f - p) * 0.85f);
            }

            yield return null;
        }

        foreach (var w in wisps)
        {
            if (w != null) Destroy(w.gameObject);
        }

        OnDeathCinematicFinished?.Invoke(startPos);
        Destroy(gameObject);
    }

    private static Sprite GetOrCreateSoftGlowSprite()
    {
        if (softGlowSprite != null) return softGlowSprite;

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
                a = a * a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        softGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 24f);
        return softGlowSprite;
    }

    #endregion
}
