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
        Dying,
        Retreating
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
    public const float MaxMapX = 24.5f;
    public const float MinMapY = -15.5f;
    public const float MaxMapY = 7.6f;

    private float CurrentMaxMapX => isWitchBoss ? 24.5f : 16.8f;
    private float CurrentMinMapY => isWitchBoss ? -15.5f : -7.6f;

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
    public AudioClip bossBattleMusicClip;

    [Header("Invocación de Refuerzos Durante la Pelea")]
    public GameObject goblinPrefab;
    public float minionSpawnInterval = 14.0f;
    public int maxActiveMinions = 4;

    [Header("Modo Jefe Nivel 3: La Bruja")]
    public bool isWitchBoss = false;
    public float teleportInterval = 9.5f;           // Teletransporte más leve y espaciado
    public float overchargeTeleportInterval = 6.0f; // Menos frecuente incluso en sobrecarga

    public BossState CurrentState { get; private set; } = BossState.Spawning;
    public event Action<Vector3> OnDeathCinematicFinished;
    internal event Action<bool> OnRetreatChanged;
    private bool retreatUsed;

    private static Sprite softGlowSprite;
    private static Sprite[] cachedWitchSprites; // 0..3 = Idle/Flotando, 4..7 = Canalizando/Atacando
    private static AudioClip cachedProceduralBossMusic;
    private static AudioClip cachedProceduralWitchBossMusic;
    private static AudioClip cachedWitchProjectileClip;
    private static AudioClip cachedWitchOverchargeClip;
    private static AudioClip cachedWitchLaughClip;

    private float nextTeleportTime = 0f;
    private bool isTeleporting = false;
    private float witchCastAnimUntil = 0f;
    private SpriteRenderer witchAuraRenderer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticWitchCache()
    {
        softGlowSprite = null;
        cachedWitchSprites = null;
        cachedProceduralBossMusic = null;
        cachedProceduralWitchBossMusic = null;
        cachedWitchProjectileClip = null;
        cachedWitchOverchargeClip = null;
        cachedWitchLaughClip = null;
    }

    // Estado guardado de la música de fondo original del nivel para restaurarla al terminar el combate
    private AudioSource savedBgMusicSource;
    private AudioClip savedBgMusicClip;
    private float savedBgMusicTime;
    private float savedBgMusicVolume;
    private float savedBgMusicPitch = 1f;
    private bool bossMusicActive = false;

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
    private Level4BossNavigation encounterNavigation;
    private bool approachingFromStatue;
    internal bool OwnsEncounterDeath => encounterNavigation != null;

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
        encounterNavigation = GetComponent<Level4BossNavigation>();
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

        // Eliminar WorldDepth2D heredado del prefab para que no ponga sortingOrder=1000+ y evitar que el jefe se vea a través de los árboles
        WorldDepth2D depth2D = GetComponent<WorldDepth2D>();
        if (depth2D != null) Destroy(depth2D);

        UnityEngine.Rendering.SortingGroup rootGroup = GetComponent<UnityEngine.Rendering.SortingGroup>();
        if (rootGroup == null) rootGroup = gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
        rootGroup.sortingLayerName = "Layer 1";
        rootGroup.sortingOrder = 2;

        if (visualRenderer != null)
        {
            visualRenderer.sortingLayerName = "Layer 1";
            visualRenderer.sortingOrder = 2;
            visualRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        }

        LoadAudioClips();

        // Detectar automáticamente si estamos en el Nivel 3 (La Bruja)
        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeScene.IndexOf("Nivel3", StringComparison.OrdinalIgnoreCase) >= 0 ||
            activeScene.IndexOf("Level3", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            isWitchBoss = true;
        }

        if (isWitchBoss)
        {
            normalAuraColor = Color.white; // Preservar los colores realistas del pixel-art de La Bruja
            overchargeAuraColor = new Color(0.92f, 0.58f, 1.0f, 1.0f);
            attackCooldown = 1.65f;
            teleportInterval = 9.5f;
            overchargeTeleportInterval = 6.0f;
            if (animator != null) animator.enabled = false;
            EnsureWitchSprites();
            if (visualRenderer != null && cachedWitchSprites != null && cachedWitchSprites.Length > 0)
            {
                visualRenderer.sprite = cachedWitchSprites[0];
                visualRenderer.color = Color.white;
            }
            SetupWitchMysticAura();
            if (encounterNavigation != null && visualRenderer != null && witchAuraRenderer != null)
                witchAuraRenderer.sharedMaterial = visualRenderer.sharedMaterial;

            // Sonidos exclusivos de ataque, sobrecarga, teletransporte y música de combate para La Bruja
            projectileClip = GetOrCreateWitchProjectileClip();
            overchargeClip = GetOrCreateWitchOverchargeClip();
            laughClip = GetOrCreateWitchLaughClip();
            bossBattleMusicClip = GetOrCreateProceduralWitchBossBattleMusicClip();
        }

        if (health != null)
        {
            health.ConfigureVariant(EnemyHealth.EnemyVariant.SpectralChildBoss);
            int targetMaxHp = isWitchBoss ? 1250 : 250; // La Bruja tiene 1250 de vida
            health.maxHealth = targetMaxHp;
            health.currentHealth = targetMaxHp;
            health.OnDamageTaken += HandleDamageTaken;
            health.OnDied += HandleDeath;
        }

        // Velocidad moderada: un poquito más rápido pero contenido
        baseMoveSpeed = 1.65f;
        overchargeSpeed = 2.05f;

        // Si existe GhostChildAI residual, desactivarlo
        var oldAi = GetComponent<GhostChildAI>();
        if (oldAi != null) oldAi.enabled = false;

        if (encounterNavigation != null)
        {
            bodyCollider.enabled = false;
            health.enabled = false;
        }
    }

    private void SetupWitchMysticAura()
    {
        Transform existingAura = transform.Find("WitchMysticAura");
        if (existingAura != null)
        {
            witchAuraRenderer = existingAura.GetComponent<SpriteRenderer>();
            return;
        }

        GameObject auraObj = new GameObject("WitchMysticAura");
        auraObj.transform.SetParent(transform, false);
        auraObj.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        auraObj.transform.localScale = new Vector3(1.85f, 2.25f, 1f);
        witchAuraRenderer = auraObj.AddComponent<SpriteRenderer>();
        witchAuraRenderer.sprite = GetOrCreateSoftGlowSprite();
        witchAuraRenderer.sortingLayerName = "Layer 1";
        witchAuraRenderer.sortingOrder = visualRenderer != null ? visualRenderer.sortingOrder - 1 : 1;
        witchAuraRenderer.color = new Color(0.62f, 0.14f, 0.88f, 0.42f);
    }

    private void PlayBossEffect(AudioClip clip, float volume)
    {
        if (audioSource == null || clip == null) return;

        // La sobrecarga del Niño viene grabada con más ganancia que sus demás efectos.
        if (encounterNavigation != null)
        {
            if (isWitchBoss) volume *= 0.62f;
            else volume *= clip == overchargeClip ? 0.18f : 0.42f;
        }

        audioSource.PlayOneShot(clip, volume * AudioManager.EnemyVolume);
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
        if (encounterNavigation != null) return;

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

        if (isWitchBoss && health != null && health.maxHealth < 1250)
        {
            health.maxHealth = 1250;
            health.currentHealth = 1250;
        }

        // Conectar barra de vida del jefe en pantalla
        if (BossHealthBarUI.Instance == null)
        {
            GameObject barObj = new GameObject("BossHealthBarUI");
            barObj.AddComponent<BossHealthBarUI>();
        }
        BossHealthBarUI.Instance.BindBoss(health, isWitchBoss ? "LA BRUJA" : "NIÑO ESPECTRAL");

        // Cambiar la música de fondo a un tema de batalla emocionante durante el combate
        StartBossBattleMusic();

        nextMinionSpawnTime = Time.time + 9.0f;
        nextTeleportTime = Time.time + teleportInterval;
        StartCoroutine(SpawnIntroRoutine());
    }

    public void BeginEncounterCombat(PlayerHealth target, GoblinNavigation2D navigation, Transform minionParent,
        Sprite projectileSprite, bool approachFromStatue = false)
    {
        if (encounterNavigation == null || CurrentState != BossState.Spawning) return;
        playerHealth = target;
        playerTransform = target.transform;
        bodyCollider.enabled = true;
        health.enabled = true;
        approachingFromStatue = approachFromStatue;
        encounterNavigation.Begin(target, navigation, minionParent, projectileSprite, !approachFromStatue);
        altarOrigin = rb.position;

        if (visualRenderer != null) visualRenderer.color = normalAuraColor;
        if (audioSource != null && laughClip != null)
            PlayBossEffect(laughClip, 0.85f);

        CurrentState = BossState.Hovering;
        nextAttackTime = Time.time + 1.2f;
        nextMinionSpawnTime = Time.time + 9f;
        nextTeleportTime = Time.time + teleportInterval;
        StartBossBattleMusic();
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
        if (bossMusicActive)
        {
            RestoreOriginalBackgroundMusic();
        }

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
            PlayBossEffect(laughClip, 0.85f);

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
        if (CurrentState == BossState.Spawning || CurrentState == BossState.Dying ||
            CurrentState == BossState.Retreating)
            return;

        if (encounterNavigation != null && (isTeleporting || (playerHealth != null && playerHealth.IsDead)))
        {
            encounterNavigation.Stop();
            return;
        }

        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
            return;
        }

        if (approachingFromStatue)
        {
            if (!encounterNavigation.IsInAttackRange(6f))
            {
                UpdateNormalMovement();
                return;
            }

            approachingFromStatue = false;
            nextAttackTime = Time.time + 0.45f;
            nextMinionSpawnTime = Time.time + 9f;
            nextTeleportTime = Time.time + teleportInterval;
        }

        // Invocación periódica de refuerzos durante la batalla
        if (Time.time >= nextMinionSpawnTime && !isInOvercharge)
        {
            nextMinionSpawnTime = Time.time + minionSpawnInterval;
            TrySpawnBattleMinions();
        }

        // Habilidad de La Bruja (Nivel 3): Teletransporte místico leve y espaciado
        if (isWitchBoss && !isTeleporting && playerTransform != null)
        {
            float distToPlayer = Vector2.Distance(rb.position, playerTransform.position);
            bool emergencyBlink = distToPlayer < 1.15f && Time.time >= nextTeleportTime - 1.2f;
            if (Time.time >= nextTeleportTime || emergencyBlink)
            {
                float interval = isInOvercharge ? overchargeTeleportInterval : teleportInterval;
                nextTeleportTime = Time.time + interval;
                StartCoroutine(WitchTeleportRoutine());
                return;
            }
        }

        // Manejo del estado de sobrecarga
        if (isInOvercharge)
        {
            UpdateOvercharge();
            return;
        }

        if (isWitchBoss)
        {
            // Reglas de sobrecarga de La Bruja (1250 HP):
            // - Al perder 1/4 de vida (<= 937 HP): carga de 8 segundos, luego se calma.
            // - Al perder 2/4 de vida (<= 625 HP): carga de 8 segundos, luego se calma.
            // - Al perder 3/4 de vida (<= 312 HP): carga de 10 segundos, luego se calma 8 segundos y repite 10s de carga cada 8s hasta ser derrotada.
            if (CheckPendingWitchOverchargeThreshold())
            {
                return;
            }
            if (overchargesTriggered >= 3 && Time.time >= nextDesperationOverchargeTime)
            {
                TriggerOvercharge(10.0f);
                return;
            }
        }
        else
        {
            // Verificación de fase de desesperación del Niño Espectral (Nivel 2)
            if (health.currentHealth <= 45 && Time.time >= nextDesperationOverchargeTime)
            {
                TriggerOvercharge(5.0f);
                return;
            }
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

        if (encounterNavigation != null)
        {
            encounterNavigation.SetMovement(moveDir, baseMoveSpeed, idealDistanceMax);
        }
        else
        {
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
        }

        if (animator != null && animator.enabled)
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
                    PlayBossEffect(stepClip, 0.35f);
                }
            }
        }
    }

    private bool CheckPendingWitchOverchargeThreshold()
    {
        if (!isWitchBoss || health == null || health.currentHealth <= 0 || isInOvercharge || CurrentState == BossState.Dying)
            return false;

        int maxHp = Mathf.Max(1, health.maxHealth); // 1250 HP
        int q1Threshold = Mathf.RoundToInt(maxHp * 0.75f); // Perdió 1/4 (938 HP)
        int q2Threshold = Mathf.RoundToInt(maxHp * 0.50f); // Perdió 2/4 (625 HP)
        int q3Threshold = Mathf.RoundToInt(maxHp * 0.25f); // Perdió 3/4 (313 HP)

        if (overchargesTriggered == 0 && health.currentHealth <= q1Threshold)
        {
            overchargesTriggered = 1;
            TriggerOvercharge(8.0f);
            return true;
        }
        if (overchargesTriggered == 1 && health.currentHealth <= q2Threshold)
        {
            overchargesTriggered = 2;
            TriggerOvercharge(8.0f);
            return true;
        }
        if (overchargesTriggered == 2 && health.currentHealth <= q3Threshold)
        {
            overchargesTriggered = 3;
            TriggerOvercharge(10.0f);
            return true;
        }
        return false;
    }

    private void HandleDamageTaken(int _)
    {
        if (encounterNavigation != null && !retreatUsed && health.currentHealth > 0 &&
            health.currentHealth * 2 <= health.maxHealth)
        {
            StartCoroutine(RetreatRoutine());
            return;
        }
        if (isInOvercharge || CurrentState == BossState.Dying || CurrentState == BossState.Retreating) return;

        if (isWitchBoss)
        {
            CheckPendingWitchOverchargeThreshold();
            return;
        }

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

    private IEnumerator RetreatRoutine()
    {
        retreatUsed = true;
        isInOvercharge = false;
        witchCastAnimUntil = 0f;
        CurrentState = BossState.Retreating;
        encounterNavigation.Stop();
        bodyCollider.enabled = false;
        health.enabled = false;
        OnRetreatChanged?.Invoke(true);

        if (animator != null && animator.enabled)
        {
            animator.SetBool("IsMoving", false);
            animator.SetInteger("ActionState", 0);
        }

        yield return AnimateRetreat(false);
        var light = GetComponent<UnityEngine.Rendering.Universal.Light2D>();
        bool restoreLight = light != null && light.enabled;
        if (restoreLight) light.enabled = false;
        if (witchAuraRenderer != null) witchAuraRenderer.enabled = false;
        visualRenderer.enabled = false;

        yield return new WaitForSeconds(0.25f);
        var waves = encounterNavigation.MinionParent.GetComponent<EncounterWaveManager>();
        while (waves != null && !waves.CanBossReturn)
            yield return null;

        encounterNavigation.ReturnNearPlayer();
        var warning = Level4SpiritEffects.CreateVisual("Aviso_Reaparicion_Jefe", Level4SpiritEffects.Ring, 10, transform);
        warning.transform.position = transform.position + Vector3.up * 0.16f;
        warning.sharedMaterial = visualRenderer.sharedMaterial;
        yield return Level4SpiritEffects.PulseRing(warning, 0.32f, Color.white);

        visualRenderer.color = new Color(normalAuraColor.r, normalAuraColor.g, normalAuraColor.b, 0f);
        visualRenderer.enabled = true;
        if (restoreLight) light.enabled = true;
        yield return AnimateRetreat(true);

        visualRenderer.color = normalAuraColor;
        if (witchAuraRenderer != null) witchAuraRenderer.enabled = true;
        bodyCollider.enabled = true;
        health.enabled = true;
        OnRetreatChanged?.Invoke(false);

        if (animator != null && animator.enabled) animator.SetBool("IsMoving", true);
        yield return encounterNavigation.BackstepFromPlayer();
        if (animator != null && animator.enabled) animator.SetBool("IsMoving", false);

        CurrentState = BossState.Hovering;
        nextAttackTime = Time.time + 0.65f;
        nextTeleportTime = Time.time + teleportInterval;
        HandleDamageTaken(0);
    }

    private IEnumerator AnimateRetreat(bool appearing)
    {
        if (!isWitchBoss)
        {
            yield return Level4SpiritEffects.AnimateChild(visualRenderer, transform, appearing ? 0.42f : 0.22f, appearing);
            yield break;
        }

        SpawnTeleportRuneBurst(transform.position);
        float duration = appearing ? 0.3f : 0.22f;
        float startAlpha = visualRenderer.color.a;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            visualRenderer.color = new Color(0.85f, 0.35f, 1f, appearing ? progress : Mathf.Lerp(startAlpha, 0f, progress));
            yield return null;
        }
    }

    private void TriggerOvercharge(float duration = 5.0f)
    {
        isInOvercharge = true;
        CurrentState = BossState.Overcharging;
        overchargeEndTime = Time.time + duration;
        // En La Bruja: tras la sobrecarga del 3/4 de vida (10s), se calma por 8 segundos y luego vuelve a entrar en carga por 10s
        nextDesperationOverchargeTime = overchargeEndTime + (isWitchBoss ? 8.0f : 7.0f);

        if (visualRenderer != null)
        {
            visualRenderer.color = overchargeAuraColor;
        }

        if (audioSource != null)
        {
            if (laughClip != null) PlayBossEffect(laughClip, 0.95f);
            if (overchargeClip != null) PlayBossEffect(overchargeClip, 0.9f);
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

        if (encounterNavigation != null)
        {
            encounterNavigation.SetMovement(moveDir, overchargeSpeed, 3f);
        }
        else
        {
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
        }

        if (animator != null && animator.enabled)
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

        while (Time.time < overchargeEndTime && health.currentHealth > 0 && CurrentState != BossState.Retreating)
        {
            if (Time.time >= nextVolleyTime && playerTransform != null)
            {
                nextVolleyTime = Time.time + volleyInterval;
                ExecuteCombinedOverchargeBarrage();
            }
            yield return null;
        }

        if (CurrentState == BossState.Retreating) yield break;

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
        if (CurrentState == BossState.Retreating) yield break;

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

        // 3. Ráfaga directa de proyectiles rápidos dirigidos al jugador
        StartCoroutine(BurstRoutine(isWitchBoss ? 5 : 3, isWitchBoss ? 0.09f : 0.12f));
    }

    private void LateUpdate()
    {
        if (!isWitchBoss || visualRenderer == null || CurrentState == BossState.Dying) return;

        EnsureWitchSprites();
        if (cachedWitchSprites != null && cachedWitchSprites.Length >= 8)
        {
            bool isCasting = CurrentState == BossState.Attacking || isInOvercharge || Time.time < witchCastAnimUntil;
            int subFrame = Mathf.FloorToInt(Time.time * (isCasting ? 9.5f : 6.5f)) % 4;
            int frameIdx = (isCasting ? 4 : 0) + subFrame;
            visualRenderer.sprite = cachedWitchSprites[frameIdx];

            if (playerTransform != null)
            {
                float dx = playerTransform.position.x - transform.position.x;
                if (Mathf.Abs(dx) > 0.12f)
                {
                    visualRenderer.flipX = dx < 0f;
                }
            }

            if (witchAuraRenderer != null)
            {
                float auraPulse = 0.35f + Mathf.Sin(Time.time * 4.5f) * 0.12f + (isCasting ? 0.22f : 0f);
                witchAuraRenderer.color = new Color(0.68f, 0.18f, 0.96f, auraPulse);
                float sc = 1.85f + Mathf.Sin(Time.time * 3.2f) * 0.14f + (isCasting ? 0.25f : 0f);
                witchAuraRenderer.transform.localScale = new Vector3(sc, sc * 1.2f, 1f);
            }
        }
    }

    /// <summary>
    /// Teletransporte místico leve de La Bruja: se desplaza una distancia corta y táctica (1.6m a 2.6m)
    /// respecto a su posición actual, con menor frecuencia para que el combate sea justo y fluido.
    /// </summary>
    private IEnumerator WitchTeleportRoutine()
    {
        if (isTeleporting || CurrentState == BossState.Dying) yield break;
        isTeleporting = true;
        if (encounterNavigation != null) encounterNavigation.Stop();

        Vector3 oldPos = transform.position;
        SpawnTeleportRuneBurst(oldPos);

        if (audioSource != null && laughClip != null)
        {
            PlayBossEffect(laughClip, 0.65f);
        }

        // Desvanecimiento suave de teletransporte
        float vanishTime = 0.22f;
        float t = 0f;
        Vector3 origScale = transform.localScale;
        while (t < vanishTime)
        {
            if (CurrentState == BossState.Retreating)
            {
                transform.localScale = origScale;
                isTeleporting = false;
                yield break;
            }

            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / vanishTime);
            transform.localScale = new Vector3(Mathf.Lerp(origScale.x, 0.25f, p), Mathf.Lerp(origScale.y, 1.32f, p), 1f);
            if (visualRenderer != null)
            {
                visualRenderer.color = new Color(0.85f, 0.35f, 1.0f, 1f - p);
            }
            yield return null;
        }

        // Elegir nueva posición cercana (desplazamiento corto y leve de 1.6m a 2.6m desde donde estaba)
        Vector2 bestTarget = oldPos;
        Vector2 playerPos = playerTransform != null ? (Vector2)playerTransform.position : altarOrigin;
        for (int attempt = 0; attempt < 16; attempt++)
        {
            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float stepDist = UnityEngine.Random.Range(1.6f, 2.65f);
            Vector2 candidate = (Vector2)oldPos + new Vector2(Mathf.Cos(angle) * stepDist, Mathf.Sin(angle) * stepDist);

            if (encounterNavigation == null)
            {
                if (Vector2.Distance(candidate, altarOrigin) > maxAltarLeash * 0.9f)
                {
                    Vector2 toAltar = (altarOrigin - (Vector2)oldPos).normalized;
                    candidate = (Vector2)oldPos + toAltar * stepDist;
                }

                candidate.x = Mathf.Clamp(candidate.x, MinMapX, MaxMapX);
                candidate.y = Mathf.Clamp(candidate.y, MinMapY, MaxMapY);
            }

            bool clear = encounterNavigation != null
                ? encounterNavigation.CanStandAt(candidate)
                : !IsPositionBlocked(candidate, 0.58f);
            if (clear &&
                Vector2.Distance(candidate, playerPos) >= 2.0f &&
                Vector2.Distance(candidate, oldPos) >= 1.2f)
            {
                bestTarget = candidate;
                break;
            }
        }

        if (encounterNavigation == null) bestTarget = FindClearPosition(bestTarget);
        transform.position = bestTarget;
        if (rb != null) rb.position = bestTarget;

        SpawnTeleportRuneBurst(bestTarget);

        // Reaparición mística
        float appearTime = 0.22f;
        t = 0f;
        while (t < appearTime)
        {
            if (CurrentState == BossState.Retreating)
            {
                transform.localScale = origScale;
                isTeleporting = false;
                yield break;
            }

            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / appearTime);
            transform.localScale = new Vector3(Mathf.Lerp(0.25f, origScale.x, p), Mathf.Lerp(1.32f, origScale.y, p), 1f);
            if (visualRenderer != null)
            {
                Color baseCol = isInOvercharge ? overchargeAuraColor : normalAuraColor;
                visualRenderer.color = new Color(baseCol.r, baseCol.g, baseCol.b, p);
            }
            yield return null;
        }

        transform.localScale = origScale;
        if (visualRenderer != null)
        {
            visualRenderer.color = isInOvercharge ? overchargeAuraColor : normalAuraColor;
        }

        isTeleporting = false;

        // Al salir del teletransporte leve, lanza una breve ráfaga de 2 orbes morados
        if (health != null && health.currentHealth > 0 && playerTransform != null)
        {
            StartCoroutine(BurstRoutine(2, 0.14f));
        }
    }

    private void SpawnTeleportRuneBurst(Vector3 pos)
    {
        StartCoroutine(TeleportRuneBurstEffectRoutine(pos));
    }

    private IEnumerator TeleportRuneBurstEffectRoutine(Vector3 pos)
    {
        GameObject ringObj = new GameObject("WitchTeleportFlash");
        ringObj.transform.position = pos + new Vector3(0f, 0.35f, 0f);
        SpriteRenderer ringSr = ringObj.AddComponent<SpriteRenderer>();
        ringSr.sprite = GetOrCreateSoftGlowSprite();
        if (encounterNavigation != null && visualRenderer != null)
            ringSr.sharedMaterial = visualRenderer.sharedMaterial;
        ringSr.sortingLayerName = "Layer 1";
        ringSr.sortingOrder = 2;
        ringSr.color = new Color(0.82f, 0.25f, 1.0f, 0.9f);

        float dur = 0.35f;
        float el = 0f;
        while (el < dur)
        {
            el += Time.deltaTime;
            float p = Mathf.Clamp01(el / dur);
            float sc = Mathf.Lerp(0.8f, 2.2f, p);
            ringObj.transform.localScale = new Vector3(sc, sc, 1f);
            ringSr.color = new Color(0.82f, 0.25f, 1.0f, (1f - p) * 0.85f);
            yield return null;
        }

        Destroy(ringObj);
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

        // En Nivel 3 priorizar duendes fuertes (Chubby) alternados con arqueros y base
        SpawnMinion(true);
        if (activeMinions.Count < maxActiveMinions)
        {
            SpawnMinion(false);
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

        Vector2 spawnPos;
        Transform minionParent = null;
        if (encounterNavigation != null)
        {
            if (!encounterNavigation.TryFindMinionSpawn(transform.position, out spawnPos)) return;
            minionParent = encounterNavigation.MinionParent;
        }
        else
        {
            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float dist = UnityEngine.Random.Range(2.8f, 4.5f);
            Vector2 candidate = (Vector2)transform.position + new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
            candidate.x = Mathf.Clamp(candidate.x, MinMapX, MaxMapX);
            candidate.y = Mathf.Clamp(candidate.y, MinMapY, MaxMapY);
            spawnPos = FindClearPosition(candidate);
        }

        GameObject minionObj = Instantiate(pf, spawnPos, Quaternion.identity, minionParent);
        EnemyHealth eh = minionObj.GetComponent<EnemyHealth>();
        if (eh == null) eh = minionObj.AddComponent<EnemyHealth>();

        GoblinVariantConfig cfg = minionObj.GetComponent<GoblinVariantConfig>();
        if (cfg == null) cfg = minionObj.AddComponent<GoblinVariantConfig>();
        bool spawnArcher = isWitchBoss && !isChubby && UnityEngine.Random.value < 0.6f;
        bool hasGreenAura = isWitchBoss && UnityEngine.Random.value < 0.55f;
        cfg.SetVariant(isChubby, spawnArcher, hasGreenAura);

        GoblinAI ai = minionObj.GetComponent<GoblinAI>();
        if (ai != null)
        {
            ai.waitForFirstCoin = false;
            ai.enablePatrol = false;
            ai.BeginWaveCombat(1);
            if (spawnArcher)
            {
                var archerCtrl = minionObj.GetComponent<GoblinArcherController>();
                if (archerCtrl != null)
                {
                    ai.stopDistance = archerCtrl.idealRangedStopDistance;
                    ai.attackRange = 0.25f;
                    ai.enableLungeAttack = false;
                }
            }
        }

        EnemyTeleportSpawn teleport = minionObj.GetComponent<EnemyTeleportSpawn>();
        if (teleport == null) teleport = minionObj.AddComponent<EnemyTeleportSpawn>();
        teleport.BeginSpawnSequence();

        activeMinions.Add(eh);
    }

    #region Ataques Normales y Ráfagas Místicas

    private void ExecuteNextAttackPattern()
    {
        CurrentState = BossState.Attacking;
        witchCastAnimUntil = Time.time + 0.75f;

        if (isWitchBoss)
        {
            // Ataques de La Bruja (Nivel 3): orientados a múltiples ráfagas de bolas de energía moradas
            switch (attackCycleIndex)
            {
                case 0:
                    // Ráfaga rápida de 5 bolas moradas
                    StartCoroutine(BurstRoutine(5, 0.11f));
                    nextAttackTime = Time.time + attackCooldown;
                    break;
                case 1:
                    // Ráfaga triple en abanico repetida 3 veces seguidas
                    StartCoroutine(TripleSpreadBurstRoutine(3, 0.22f));
                    nextAttackTime = Time.time + attackCooldown * 1.15f;
                    break;
                case 2:
                    // Ráfaga intensa de 7 proyectiles morados con oscilación angular
                    StartCoroutine(AlternatingBurstRoutine(7, 0.09f));
                    nextAttackTime = Time.time + attackCooldown * 1.25f;
                    break;
                case 3:
                    // Nova circular morada + Ráfaga directa de 4 bolas
                    SpawnNova360(10);
                    StartCoroutine(BurstRoutine(4, 0.11f));
                    nextAttackTime = Time.time + attackCooldown * 1.35f;
                    break;
            }
        }
        else
        {
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
        }

        attackCycleIndex = (attackCycleIndex + 1) % 4;
        StartCoroutine(ResetAttackStateRoutine(0.55f));
    }

    private IEnumerator TripleSpreadBurstRoutine(int wavesCount, float waveInterval)
    {
        for (int i = 0; i < wavesCount; i++)
        {
            if (health == null || health.currentHealth <= 0 || playerTransform == null || CurrentState == BossState.Retreating)
                yield break;
            SpawnTripleSpread();
            yield return new WaitForSeconds(waveInterval);
        }
    }

    private IEnumerator AlternatingBurstRoutine(int shots, float delay)
    {
        for (int i = 0; i < shots; i++)
        {
            if (health == null || health.currentHealth <= 0 || playerTransform == null || CurrentState == BossState.Retreating)
                yield break;
            Vector2 baseDir = (playerTransform.position - transform.position).normalized;
            float spreadAngle = Mathf.Sin(i * 1.35f) * 16f;
            Vector2 shotDir = Quaternion.Euler(0f, 0f, spreadAngle) * baseDir;
            PlayAttackAnimationAndSound(baseDir);
            CreateProjectile(transform.position + (Vector3)shotDir * 0.45f, shotDir, 6.2f, projectileDamage);
            yield return new WaitForSeconds(delay);
        }
    }

    private void PlayAttackAnimationAndSound(Vector2 dir)
    {
        witchCastAnimUntil = Time.time + 0.45f;

        if (animator != null && animator.enabled)
        {
            int dirInt = Mathf.Abs(dir.x) > Mathf.Abs(dir.y)
                ? (dir.x > 0 ? 2 : 3)
                : (dir.y > 0 ? 1 : 0);
            animator.SetInteger("Direction", dirInt);
            animator.SetInteger("ActionState", 2);
        }

        if (audioSource != null && projectileClip != null)
        {
            audioSource.pitch = isWitchBoss ? UnityEngine.Random.Range(0.92f, 1.08f) : 1.0f;
            PlayBossEffect(projectileClip, (isWitchBoss ? 0.90f : 0.85f));
        }
    }

    private IEnumerator ResetAttackStateRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (CurrentState == BossState.Retreating) yield break;
        if (animator != null && animator.enabled) animator.SetInteger("ActionState", 0);
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
            if (health.currentHealth <= 0 || playerTransform == null || CurrentState == BossState.Retreating)
                yield break;
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

        if (encounterNavigation != null)
            projObj.transform.SetParent(encounterNavigation.MinionParent, true);

        SpectralProjectile proj = projObj.GetComponent<SpectralProjectile>();
        if (proj == null) proj = projObj.AddComponent<SpectralProjectile>();
        if (isWitchBoss)
        {
            proj.ConfigurePurpleOrb();
        }
        else if (encounterNavigation != null)
        {
            proj.GetComponent<SpriteRenderer>().sprite = encounterNavigation.ProjectileSprite;
        }
        proj.CanBeDispelledByLantern = encounterNavigation != null;
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
        if (encounterNavigation != null)
        {
            encounterNavigation.Stop();
            rb.simulated = false;
        }

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
        var projectiles = encounterNavigation != null
            ? encounterNavigation.MinionParent.GetComponentsInChildren<SpectralProjectile>()
            : FindObjectsOfType<SpectralProjectile>();
        foreach (var proj in projectiles)
        {
            if (proj != null) Destroy(proj.gameObject);
        }

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        if (bodyCollider != null) bodyCollider.enabled = false;
        if (rb != null) rb.velocity = Vector2.zero;
        if (animator != null && animator.enabled)
        {
            animator.SetBool("IsMoving", false);
            animator.SetInteger("ActionState", 0);
            animator.SetInteger("Direction", 0);
        }

        if (audioSource != null)
        {
            if (overchargeClip != null) PlayBossEffect(overchargeClip, 0.9f);
            if (laughClip != null) PlayBossEffect(laughClip, 0.75f);
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

            // Pulso luminoso entre cian/morado espectral y blanco antes de desvanecerse lentamente
            float alpha = Mathf.Pow(1f - p, 1.25f);
            float flash = Mathf.PingPong(elapsed * 6f, 1f);
            Color baseDeathCol = isWitchBoss ? new Color(0.85f, 0.32f, 1f, 1f) : new Color(0.3f, 0.88f, 1f, 1f);
            if (visualRenderer != null)
            {
                Color c = Color.Lerp(baseDeathCol, Color.white, flash * 0.65f);
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
                auraSr.color = new Color(baseDeathCol.r, baseDeathCol.g, baseDeathCol.b, (1f - p) * 0.75f);
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
                wispRenderers[i].color = new Color(baseDeathCol.r, baseDeathCol.g, baseDeathCol.b, (1f - p) * 0.85f);
            }

            yield return null;
        }

        foreach (var w in wisps)
        {
            if (w != null) Destroy(w.gameObject);
        }

        // Restaurar la canción de fondo original del nivel cuando finaliza el combate
        RestoreOriginalBackgroundMusic();

        OnDeathCinematicFinished?.Invoke(startPos);
        Destroy(gameObject);
    }

    public void StartBossBattleMusic()
    {
        if (bossMusicActive) return;

        GameObject bgmObj = GameObject.Find("BackgroundMusic");
        if (bgmObj != null)
        {
            savedBgMusicSource = bgmObj.GetComponent<AudioSource>();
        }

        if (savedBgMusicSource == null)
        {
            foreach (var src in FindObjectsOfType<AudioSource>())
            {
                if (src != null && src.loop && src.isPlaying && src.gameObject != gameObject)
                {
                    savedBgMusicSource = src;
                    break;
                }
            }
        }

        if (savedBgMusicSource == null) return;

        savedBgMusicClip = savedBgMusicSource.clip;
        savedBgMusicTime = savedBgMusicSource.time;
        savedBgMusicVolume = savedBgMusicSource.volume;
        savedBgMusicPitch = savedBgMusicSource.pitch;

        AudioClip battleClip = isWitchBoss
            ? GetOrCreateProceduralWitchBossBattleMusicClip()
            : (bossBattleMusicClip != null ? bossBattleMusicClip : GetOrCreateProceduralBossBattleMusicClip());

        if (battleClip != null)
        {
            bossMusicActive = true;
            savedBgMusicSource.Stop();
            savedBgMusicSource.clip = battleClip;
            savedBgMusicSource.loop = true;
            savedBgMusicSource.pitch = 1.0f;
            savedBgMusicSource.volume = Mathf.Max(savedBgMusicVolume, AudioManager.MusicVolume * 0.65f);
            savedBgMusicSource.time = 0f;
            savedBgMusicSource.Play();
        }
    }

    public void RestoreOriginalBackgroundMusic()
    {
        if (!bossMusicActive) return;
        bossMusicActive = false;

        if (savedBgMusicSource != null && savedBgMusicClip != null)
        {
            savedBgMusicSource.Stop();
            savedBgMusicSource.clip = savedBgMusicClip;
            savedBgMusicSource.loop = true;
            savedBgMusicSource.pitch = savedBgMusicPitch;
            savedBgMusicSource.volume = savedBgMusicVolume > 0.01f ? savedBgMusicVolume : (AudioManager.MusicVolume * 0.55f);
            if (savedBgMusicClip.length > 0.2f)
            {
                savedBgMusicSource.time = Mathf.Clamp(savedBgMusicTime, 0f, savedBgMusicClip.length - 0.1f);
            }
            savedBgMusicSource.Play();
        }
    }

    private static AudioClip GetOrCreateProceduralBossBattleMusicClip()
    {
        if (cachedProceduralBossMusic != null) return cachedProceduralBossMusic;

        const int sampleRate = 22050;
        const float stepDuration = 0.10f; // 150 BPM -> 1 semicorchea = 0.10s
        const int totalSteps = 128;       // 8 compases de 16 semicorcheas = 12.8s loop exacto
        int totalSamples = Mathf.RoundToInt(totalSteps * stepDuration * sampleRate);

        float[] samples = new float[totalSamples];

        int[] bassRoots = { 38, 38, 34, 34, 43, 43, 37, 33 }; // D2, D2, Bb1, Bb1, G2, G2, C#2, A1

        int[][] arpChords = new int[][]
        {
            new int[] { 62, 65, 69, 74 }, // Dm: D4, F4, A4, D5
            new int[] { 62, 69, 74, 77 }, // Dm: D4, A4, D5, F5
            new int[] { 58, 62, 65, 70 }, // Bb: Bb3, D4, F4, Bb4
            new int[] { 58, 65, 70, 74 }, // Bb: Bb3, F4, Bb4, D5
            new int[] { 55, 58, 62, 67 }, // Gm: G3, Bb3, D4, G4
            new int[] { 58, 62, 67, 70 }, // Gm: Bb3, D4, G4, Bb4
            new int[] { 55, 58, 61, 64 }, // Edim7: G3, Bb3, C#4, E4
            new int[] { 57, 61, 64, 69 }  // A: A3, C#4, E4, A4
        };

        int[] leadMelody64 = new int[]
        {
            77, 76, 77, 74,  76, 73, 74, 69,   77, 79, 81, 79,  77, 76, 74, 74,
            74, 72, 74, 70,  72, 69, 70, 65,   74, 76, 77, 76,  74, 72, 70, 70,
            79, 77, 79, 74,  77, 74, 70, 67,   74, 77, 79, 82,  81, 79, 77, 74,
            76, 73, 76, 79,  82, 79, 76, 73,   81, 79, 76, 73,  74, 76, 79, 81
        };

        uint rngState = 0x13579BDFu;

        for (int i = 0; i < totalSamples; i++)
        {
            float t = i / (float)sampleRate;
            int step = Mathf.Clamp((int)(t / stepDuration), 0, totalSteps - 1);
            float stepTime = t - step * stepDuration;
            float stepNorm = stepTime / stepDuration;

            int bar = (step / 16) % 8;
            int stepInBar = step % 16;

            int bassMidi = bassRoots[bar] + ((stepInBar % 2 == 1) ? 12 : 0);
            if (stepInBar == 14 || stepInBar == 15) bassMidi += 7;
            float bassFreq = 440f * Mathf.Pow(2f, (bassMidi - 69) / 12f);
            float bassEnv = Mathf.Exp(-stepNorm * 3.2f);
            float bassWave = (Mathf.Sin(2f * Mathf.PI * bassFreq * t) * 0.65f +
                              Mathf.Sign(Mathf.Sin(2f * Mathf.PI * bassFreq * t)) * 0.35f) * bassEnv * 0.28f;

            int[] chord = arpChords[bar];
            int arpIdx = (stepInBar % 4 == 3) ? 1 : (stepInBar % 4);
            int arpMidi = chord[arpIdx];
            float arpFreq = 440f * Mathf.Pow(2f, (arpMidi - 69) / 12f);
            float arpEnv = Mathf.Exp(-stepNorm * 4.5f);
            float arpWave = (Mathf.Sin(2f * Mathf.PI * arpFreq * t) +
                             0.5f * Mathf.Sin(4f * Mathf.PI * arpFreq * t) +
                             0.25f * Mathf.Sin(6f * Mathf.PI * arpFreq * t)) * arpEnv * 0.16f;

            int leadStep = (step / 2) % 64;
            int leadMidi = leadMelody64[leadStep];
            float eighthTime = t - (step / 2) * (stepDuration * 2f);
            float eighthNorm = eighthTime / (stepDuration * 2f);
            float vibrato = 1f + Mathf.Sin(2f * Mathf.PI * 6.5f * t) * 0.0045f;
            float leadFreq = 440f * Mathf.Pow(2f, (leadMidi - 69) / 12f) * vibrato;
            float leadEnv = Mathf.Clamp01(eighthNorm * 14f) * Mathf.Exp(-eighthNorm * 1.85f);
            float leadWave = (Mathf.Sin(2f * Mathf.PI * leadFreq * t) * 0.6f +
                              0.35f * Mathf.Sin(4f * Mathf.PI * leadFreq * t) +
                              0.15f * Mathf.Sin(6f * Mathf.PI * leadFreq * t)) * leadEnv * 0.24f;

            float drumWave = 0f;

            if (stepInBar == 0 || stepInBar == 6 || stepInBar == 8 || stepInBar == 14)
            {
                float kickPitch = Mathf.Lerp(135f, 42f, Mathf.Clamp01(stepTime * 18f));
                float kickEnv = Mathf.Exp(-stepTime * 22f);
                drumWave += Mathf.Sin(2f * Mathf.PI * kickPitch * stepTime) * kickEnv * 0.34f;
            }

            rngState = rngState * 1664525u + 1013904223u;
            float whiteNoise = ((rngState & 0xFFFFu) / 32768f) - 1f;
            if (stepInBar == 4 || stepInBar == 12)
            {
                float snareEnv = Mathf.Exp(-stepTime * 28f);
                float snareBody = Mathf.Sin(2f * Mathf.PI * 185f * stepTime) * Mathf.Exp(-stepTime * 35f);
                drumWave += (whiteNoise * 0.75f * snareEnv + snareBody * 0.25f) * 0.26f;
            }

            float hatEnv = Mathf.Exp(-stepTime * (stepInBar % 2 == 0 ? 55f : 75f));
            drumWave += whiteNoise * hatEnv * 0.06f;

            float mix = bassWave + arpWave + leadWave + drumWave;
            samples[i] = Mathf.Clamp(mix * 0.92f, -0.95f, 0.95f);
        }

        cachedProceduralBossMusic = AudioClip.Create("BossBattleTheme_NinoEspectral", totalSamples, 1, sampleRate, false);
        cachedProceduralBossMusic.SetData(samples, 0);
        return cachedProceduralBossMusic;
    }

    /// <summary>
    /// Sonido de ataque mágico oscuro de La Bruja (descarga de energía morada / fuego arcano).
    /// </summary>
    private static AudioClip GetOrCreateWitchProjectileClip()
    {
        if (cachedWitchProjectileClip != null) return cachedWitchProjectileClip;

        const int sampleRate = 22050;
        const float duration = 0.30f;
        int totalSamples = Mathf.RoundToInt(duration * sampleRate);
        float[] samples = new float[totalSamples];

        uint rng = 0x7A4B9C13u;
        float phaseMain = 0f;
        float phaseSub = 0f;

        for (int i = 0; i < totalSamples; i++)
        {
            float t = i / (float)sampleRate;
            float norm = t / duration;

            // Barrido descendente de hechizo arcano oscuro (490 Hz -> 135 Hz) con modulación mística
            float fm = Mathf.Sin(2f * Mathf.PI * 34f * t) * 18f;
            float freqMain = Mathf.Lerp(490f, 135f, Mathf.Pow(norm, 0.58f)) + fm;
            float freqSub = freqMain * 0.5f;

            phaseMain += 2f * Mathf.PI * freqMain / sampleRate;
            phaseSub += 2f * Mathf.PI * freqSub / sampleRate;

            float envAttack = Mathf.Clamp01(t / 0.012f);
            float envDecay = Mathf.Exp(-t * 9.5f);
            float env = envAttack * envDecay;

            // Pulso armónico violeta y sub-bajo oscuro
            float spellCore = (Mathf.Sin(phaseMain) * 0.55f +
                               Mathf.Sin(phaseMain * 2f) * 0.25f +
                               Mathf.Sin(phaseSub) * 0.45f) * env;

            // Destello cristalino del báculo al inicio del lanzamiento
            float chimeEnv = Mathf.Exp(-t * 28f);
            float crystalChime = (Mathf.Sin(2f * Mathf.PI * 1140f * t) * 0.5f +
                                  Mathf.Sin(2f * Mathf.PI * 1710f * t) * 0.35f) * chimeEnv * 0.32f;

            // Soplo de llama sombría (ruido filtrado envolvente)
            rng = rng * 1664525u + 1013904223u;
            float noise = ((rng & 0xFFFFu) / 32768f) - 1f;
            float whooshEnv = Mathf.Sin(Mathf.Clamp01(norm * 1.4f) * Mathf.PI) * Mathf.Exp(-t * 6.5f);
            float darkFlameWhoosh = noise * whooshEnv * 0.24f;

            samples[i] = Mathf.Clamp((spellCore * 0.68f + crystalChime + darkFlameWhoosh) * 0.90f, -0.95f, 0.95f);
        }

        cachedWitchProjectileClip = AudioClip.Create("Witch_ArcaneBolt_SFX", totalSamples, 1, sampleRate, false);
        cachedWitchProjectileClip.SetData(samples, 0);
        return cachedWitchProjectileClip;
    }

    /// <summary>
    /// Sonido de sobrecarga de La Bruja (estallido de vórtice arcano y acorde de órgano oscuro).
    /// </summary>
    private static AudioClip GetOrCreateWitchOverchargeClip()
    {
        if (cachedWitchOverchargeClip != null) return cachedWitchOverchargeClip;

        const int sampleRate = 22050;
        const float duration = 1.10f;
        int totalSamples = Mathf.RoundToInt(duration * sampleRate);
        float[] samples = new float[totalSamples];

        // Acorde disminuido oscuro (E2, Bb2, G3, C#4, F#4)
        float[] chordFreqs = { 82.41f, 116.54f, 196.00f, 277.18f, 369.99f };
        uint rng = 0x9E3779B9u;

        for (int i = 0; i < totalSamples; i++)
        {
            float t = i / (float)sampleRate;
            float norm = t / duration;

            float env = Mathf.Clamp01(t / 0.06f) * Mathf.Exp(-norm * 2.1f);
            float tremolo = 0.78f + 0.22f * Mathf.Sin(2f * Mathf.PI * 14f * t);

            float chordSum = 0f;
            for (int c = 0; c < chordFreqs.Length; c++)
            {
                float f = chordFreqs[c] * (1f + norm * 0.08f);
                chordSum += Mathf.Sin(2f * Mathf.PI * f * t) + 0.4f * Mathf.Sin(4f * Mathf.PI * f * t);
            }
            chordSum /= chordFreqs.Length;

            // Vórtice ascendente místico
            float vortexFreq = Mathf.Lerp(120f, 640f, Mathf.Pow(norm, 0.7f));
            float vortex = Mathf.Sin(2f * Mathf.PI * vortexFreq * t) * 0.35f;

            rng = rng * 1664525u + 1013904223u;
            float noise = ((rng & 0xFFFFu) / 32768f) - 1f;
            float thunder = noise * Mathf.Exp(-t * 4.5f) * 0.22f;

            samples[i] = Mathf.Clamp((chordSum * 0.65f + vortex + thunder) * env * tremolo, -0.95f, 0.95f);
        }

        cachedWitchOverchargeClip = AudioClip.Create("Witch_Overcharge_SFX", totalSamples, 1, sampleRate, false);
        cachedWitchOverchargeClip.SetData(samples, 0);
        return cachedWitchOverchargeClip;
    }

    /// <summary>
    /// Sonido místico de teletransporte y conjuro sombrío de La Bruja.
    /// </summary>
    private static AudioClip GetOrCreateWitchLaughClip()
    {
        if (cachedWitchLaughClip != null) return cachedWitchLaughClip;

        const int sampleRate = 22050;
        const float duration = 0.68f;
        int totalSamples = Mathf.RoundToInt(duration * sampleRate);
        float[] samples = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = i / (float)sampleRate;
            float norm = t / duration;

            // Modulación rítmica en 4 pulsos arcanos de teletransporte/eco de bruja
            float pulseGate = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 6.2f * t);
            float env = Mathf.Clamp01(t / 0.02f) * Mathf.Exp(-norm * 2.4f) * pulseGate;

            float baseFreq = Mathf.Lerp(360f, 220f, norm) + Mathf.Sin(2f * Mathf.PI * 18f * t) * 24f;
            float wave = Mathf.Sin(2f * Mathf.PI * baseFreq * t) * 0.55f +
                         Mathf.Sin(2f * Mathf.PI * (baseFreq * 1.5f) * t) * 0.30f +
                         Mathf.Sin(2f * Mathf.PI * (baseFreq * 2.5f) * t) * 0.15f;

            samples[i] = Mathf.Clamp(wave * env * 0.85f, -0.95f, 0.95f);
        }

        cachedWitchLaughClip = AudioClip.Create("Witch_TeleportSpell_SFX", totalSamples, 1, sampleRate, false);
        cachedWitchLaughClip.SetData(samples, 0);
        return cachedWitchLaughClip;
    }

    /// <summary>
    /// Canción de fondo exclusiva para el combate contra La Bruja (Nivel 3):
    /// "Aquelarre del Panteón" en Mi Menor Armónico / Frigio Dominante (160 BPM),
    /// con órgano gótico de catedral, clavecín arcano, bajo galopante ritual y percusión de guerra.
    /// </summary>
    private static AudioClip GetOrCreateProceduralWitchBossBattleMusicClip()
    {
        if (cachedProceduralWitchBossMusic != null) return cachedProceduralWitchBossMusic;

        const int sampleRate = 22050;
        const float stepDuration = 0.09375f; // 160 BPM -> 1 semicorchea = 0.09375s
        const int totalSteps = 128;          // 8 compases de 16 semicorcheas = 12.0s loop exacto
        int totalSamples = Mathf.RoundToInt(totalSteps * stepDuration * sampleRate);

        float[] samples = new float[totalSamples];

        // Progresión gótica en Mi Menor Armónico (Em - Cmaj7 - Am - B7 - Em - F - B7 - Em)
        int[] bassRoots = { 40, 36, 33, 35, 40, 41, 35, 40 }; // E2, C2, A1, B1, E2, F2, B1, E2

        // Arpegios de clavecín oscuro (Harpsichord)
        int[][] covenChords = new int[][]
        {
            new int[] { 64, 67, 71, 76 }, // Em: E4, G4, B4, E5
            new int[] { 60, 64, 67, 71 }, // Cmaj7: C4, E4, G4, B4
            new int[] { 57, 60, 64, 69 }, // Am: A3, C4, E4, A4
            new int[] { 59, 63, 66, 71 }, // B7: B3, D#4, F#4, B4
            new int[] { 64, 67, 71, 79 }, // Em: E4, G4, B4, G5
            new int[] { 60, 65, 69, 72 }, // F (Napolitano oscuro): C4, F4, A4, C5
            new int[] { 59, 63, 68, 71 }, // B7(b9): B3, D#4, G4, B4
            new int[] { 64, 71, 75, 76 }  // Em(maj7)/B -> E5 resolución dramática
        };

        // Melodía principal gótica de La Bruja (64 corcheas = 8 compases)
        int[] witchMelody64 = new int[]
        {
            // Compás 1 (Em) & Compás 2 (Cmaj7)
            76, 79, 83, 79,  76, 75, 76, 71,   72, 76, 79, 76,  72, 71, 69, 67,
            // Compás 3 (Am) & Compás 4 (B7)
            69, 72, 76, 81,  79, 76, 72, 69,   71, 75, 78, 75,  71, 75, 78, 83,
            // Compás 5 (Em) & Compás 6 (F)
            83, 84, 83, 79,  76, 79, 76, 71,   72, 77, 81, 77,  72, 69, 72, 77,
            // Compás 7 (B7) & Compás 8 (Em)
            78, 75, 78, 83,  81, 78, 75, 71,   76, 79, 75, 76,  71, 75, 76, 76
        };

        uint rngState = 0xCAFEBABEu;

        for (int i = 0; i < totalSamples; i++)
        {
            float t = i / (float)sampleRate;
            int step = Mathf.Clamp((int)(t / stepDuration), 0, totalSteps - 1);
            float stepTime = t - step * stepDuration;
            float stepNorm = stepTime / stepDuration;

            int bar = (step / 16) % 8;
            int stepInBar = step % 16;

            // 1. Bajo ritual en octavas galopantes con armónicos oscuros
            int bassMidi = bassRoots[bar] + ((stepInBar % 2 == 1) ? 12 : 0);
            if (stepInBar == 12 || stepInBar == 14) bassMidi += 7;
            float bassFreq = 440f * Mathf.Pow(2f, (bassMidi - 69) / 12f);
            float bassEnv = Mathf.Exp(-stepNorm * 3.6f);
            float bassWave = (Mathf.Sin(2f * Mathf.PI * bassFreq * t) * 0.60f +
                              0.30f * Mathf.Sin(4f * Mathf.PI * bassFreq * t) +
                              0.20f * Mathf.Sign(Mathf.Sin(2f * Mathf.PI * bassFreq * t))) * bassEnv * 0.27f;

            // 2. Órgano de Catedral Gótico (acordes tenidos con registro de tubos)
            int[] chord = covenChords[bar];
            float organWave = 0f;
            float organVib = 1f + Mathf.Sin(2f * Mathf.PI * 5.2f * t) * 0.003f;
            for (int c = 0; c < 3; c++)
            {
                float cFreq = 440f * Mathf.Pow(2f, (chord[c] - 69) / 12f) * organVib;
                organWave += Mathf.Sin(2f * Mathf.PI * cFreq * t) * 0.5f +
                             Mathf.Sin(4f * Mathf.PI * cFreq * t) * 0.3f +
                             Mathf.Sin(6f * Mathf.PI * cFreq * t) * 0.2f;
            }
            organWave = (organWave / 3f) * 0.14f;

            // 3. Clavecín Arcano (Harpsichord ostinato rápido en semicorcheas)
            int[] arpOrder = { 0, 2, 1, 3, 2, 3, 1, 2 };
            int arpMidi = chord[arpOrder[stepInBar % 8]];
            float arpFreq = 440f * Mathf.Pow(2f, (arpMidi - 69) / 12f);
            float arpEnv = Mathf.Exp(-stepNorm * 6.2f);
            float harpWave = (Mathf.Sin(2f * Mathf.PI * arpFreq * t) * 0.45f +
                              Mathf.Sin(4f * Mathf.PI * arpFreq * t) * 0.35f +
                              Mathf.Sin(6f * Mathf.PI * arpFreq * t) * 0.20f) * arpEnv * 0.17f;

            // 4. Voz solista mística de La Bruja (violín/sintetizador gótico con vibrato expresivo)
            int leadStep = (step / 2) % 64;
            int leadMidi = witchMelody64[leadStep];
            float eighthTime = t - (step / 2) * (stepDuration * 2f);
            float eighthNorm = eighthTime / (stepDuration * 2f);
            float vibrato = 1f + Mathf.Sin(2f * Mathf.PI * 7.2f * t) * 0.0055f;
            float leadFreq = 440f * Mathf.Pow(2f, (leadMidi - 69) / 12f) * vibrato;
            float leadEnv = Mathf.Clamp01(eighthNorm * 16f) * Mathf.Exp(-eighthNorm * 1.65f);
            float leadWave = (Mathf.Sin(2f * Mathf.PI * leadFreq * t) * 0.52f +
                              0.32f * Mathf.Sin(4f * Mathf.PI * leadFreq * t) +
                              0.18f * Mathf.Sin(6f * Mathf.PI * leadFreq * t)) * leadEnv * 0.25f;

            // 5. Percusión ritual de aquelarre (timbales profundos + redobles)
            float drumWave = 0f;
            if (stepInBar == 0 || stepInBar == 3 || stepInBar == 6 || stepInBar == 8 || stepInBar == 11 || stepInBar == 14)
            {
                float timpaniPitch = Mathf.Lerp(118f, 46f, Mathf.Clamp01(stepTime * 16f));
                float timpaniEnv = Mathf.Exp(-stepTime * 20f);
                drumWave += Mathf.Sin(2f * Mathf.PI * timpaniPitch * stepTime) * timpaniEnv * 0.35f;
            }

            rngState = rngState * 1664525u + 1013904223u;
            float whiteNoise = ((rngState & 0xFFFFu) / 32768f) - 1f;
            bool isSnareHit = (stepInBar == 4 || stepInBar == 12) || (bar % 4 == 3 && stepInBar >= 13);
            if (isSnareHit)
            {
                float snareEnv = Mathf.Exp(-stepTime * 26f);
                float snareBody = Mathf.Sin(2f * Mathf.PI * 195f * stepTime) * Mathf.Exp(-stepTime * 32f);
                drumWave += (whiteNoise * 0.72f * snareEnv + snareBody * 0.28f) * 0.25f;
            }

            float cymbalEnv = Mathf.Exp(-stepTime * (stepInBar % 4 == 0 ? 40f : 70f));
            drumWave += whiteNoise * cymbalEnv * 0.055f;

            float mix = bassWave + organWave + harpWave + leadWave + drumWave;
            samples[i] = Mathf.Clamp(mix * 0.92f, -0.95f, 0.95f);
        }

        cachedProceduralWitchBossMusic = AudioClip.Create("BossBattleTheme_LaBruja", totalSamples, 1, sampleRate, false);
        cachedProceduralWitchBossMusic.SetData(samples, 0);
        return cachedProceduralWitchBossMusic;
    }

    /// <summary>
    /// Genera 8 frames de animación pixel-art realista de alta definición (64x80) para La Bruja:
    /// - Frames 0..3: Levitación acechante con ondeo de manto de terciopelo, vestido rasgado, mechones de cabello plateado y báculo arcano.
    /// - Frames 4..7: Pose de canalización y ataque lanzando ráfagas de magia oscura con runas y destellos violetas en ambas manos.
    /// </summary>
    private static void EnsureWitchSprites()
    {
        if (cachedWitchSprites != null && cachedWitchSprites.Length == 8 && cachedWitchSprites[0] != null)
            return;

        cachedWitchSprites = new Sprite[8];
        for (int f = 0; f < 4; f++)
        {
            cachedWitchSprites[f] = CreateWitchSpriteFrame(f, isCasting: false);
            cachedWitchSprites[4 + f] = CreateWitchSpriteFrame(f, isCasting: true);
        }
    }

    private static Sprite CreateWitchSpriteFrame(int subFrame, bool isCasting)
    {
        int w = 64;
        int h = 80;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, Color.clear);

        // Paleta realista de fantasía oscura para La Bruja del Panteón
        Color outline = new Color(0.07f, 0.03f, 0.11f, 1f);
        Color cloakShadow = new Color(0.12f, 0.07f, 0.20f, 1f);
        Color cloakMid = new Color(0.20f, 0.11f, 0.32f, 1f);
        Color cloakHighlight = new Color(0.32f, 0.18f, 0.48f, 1f);
        Color dressCrimsonDark = new Color(0.28f, 0.07f, 0.24f, 1f);
        Color dressCrimsonMid = new Color(0.46f, 0.12f, 0.38f, 1f);
        Color dressCrimsonLight = new Color(0.66f, 0.20f, 0.54f, 1f);
        Color corsetGold = new Color(0.86f, 0.68f, 0.24f, 1f);

        Color skinShadow = new Color(0.56f, 0.62f, 0.54f, 1f);
        Color skinMid = new Color(0.72f, 0.78f, 0.68f, 1f);
        Color skinHighlight = new Color(0.84f, 0.88f, 0.80f, 1f);
        Color hairDark = new Color(0.42f, 0.44f, 0.50f, 1f);
        Color hairSilver = new Color(0.72f, 0.75f, 0.82f, 1f);
        Color eyeViolet = new Color(0.96f, 0.26f, 1.0f, 1f);
        Color eyeCore = new Color(1.0f, 0.92f, 1.0f, 1f);

        Color hatDark = new Color(0.10f, 0.05f, 0.16f, 1f);
        Color hatMid = new Color(0.18f, 0.10f, 0.28f, 1f);
        Color hatBand = new Color(0.64f, 0.14f, 0.48f, 1f);
        Color buckleGold = new Color(0.98f, 0.82f, 0.28f, 1f);

        Color woodDark = new Color(0.26f, 0.14f, 0.07f, 1f);
        Color woodMid = new Color(0.44f, 0.25f, 0.12f, 1f);
        Color woodLight = new Color(0.60f, 0.36f, 0.18f, 1f);

        Color orbGlowOuter = new Color(0.68f, 0.16f, 0.96f, 0.55f);
        Color orbMid = new Color(0.88f, 0.32f, 1.0f, 1f);
        Color orbCore = new Color(1.0f, 0.90f, 1.0f, 1f);

        int cx = 29;
        float phase = subFrame * (Mathf.PI * 0.5f);
        int bobY = (subFrame == 1 || subFrame == 2) ? 1 : 0;

        // 1. Sombra mística flotante en el suelo (Y = 2..5)
        for (int y = 2; y <= 5; y++)
        {
            for (int x = cx - 11; x <= cx + 11; x++)
            {
                float nx = (x - cx) / 11f;
                float ny = (y - 3.5f) / 2.0f;
                if (nx * nx + ny * ny <= 1f)
                {
                    tex.SetPixel(x, y, new Color(0.32f, 0.08f, 0.48f, 0.42f));
                }
            }
        }

        // 2. Manto exterior ondeante y falda en capas con pliegues realistas (Y = 7..45)
        for (int y = 7 + bobY; y <= 45 + bobY; y++)
        {
            float t = (y - (7f + bobY)) / 38f; // 0 en el borde inferior, 1 en los hombros
            float flare = isCasting ? 1.15f : 1.0f;
            float halfW = Mathf.Lerp(14.5f * flare, 6.0f, Mathf.Pow(t, 0.75f));
            float wave = Mathf.Sin(phase + y * 0.22f) * Mathf.Lerp(2.0f, 0.25f, t);

            int minX = Mathf.RoundToInt(cx + wave - halfW);
            int maxX = Mathf.RoundToInt(cx + wave + halfW);

            for (int x = minX; x <= maxX; x++)
            {
                if (x < 2 || x >= w - 2) continue;

                // Dobladillo inferior desgarrado con picos de tela
                int hemCut = 7 + bobY + Mathf.RoundToInt(Mathf.Sin(x * 0.9f + subFrame) * 1.6f + 1.6f);
                if (y < hemCut) continue;

                float relX = (x - (cx + wave)) / Mathf.Max(1f, halfW);
                float absRelX = Mathf.Abs(relX);

                if (absRelX >= 0.90f || y == hemCut)
                {
                    tex.SetPixel(x, y, outline);
                }
                else if (absRelX >= 0.46f)
                {
                    // Manto exterior oscuro con pliegues verticales sombreados
                    float fold = Mathf.Sin(x * 0.85f + phase * 0.5f);
                    Color c = (fold > 0.35f && relX < 0f) ? cloakHighlight : ((fold < -0.25f || relX > 0.2f) ? cloakShadow : cloakMid);
                    tex.SetPixel(x, y, c);
                }
                else
                {
                    // Vestido central carmesí/púrpura y corsé con cordones dorados
                    if (y >= 31 + bobY && y <= 42 + bobY)
                    {
                        if (Mathf.Abs(x - cx) <= 1 && (y % 2 == 0))
                            tex.SetPixel(x, y, corsetGold);
                        else
                            tex.SetPixel(x, y, (x < cx) ? dressCrimsonMid : dressCrimsonDark);
                    }
                    else
                    {
                        float skirtFold = Mathf.Cos(x * 0.95f + phase);
                        Color sc = skirtFold > 0.4f ? dressCrimsonLight : (skirtFold < -0.3f ? dressCrimsonDark : dressCrimsonMid);
                        tex.SetPixel(x, y, sc);
                    }
                }
            }
        }

        // 3. Cuello alto del manto y hombreras (Y = 43..49)
        for (int y = 43 + bobY; y <= 49 + bobY; y++)
        {
            int collarW = (y >= 47 + bobY) ? 8 : 7;
            for (int x = cx - collarW; x <= cx + collarW; x++)
            {
                if (Mathf.Abs(x - cx) >= collarW - 1)
                    tex.SetPixel(x, y, outline);
                else if (Mathf.Abs(x - cx) >= 4)
                    tex.SetPixel(x, y, (x < cx) ? cloakHighlight : cloakShadow);
            }
        }

        // 4. Rostro realista de La Bruja (pómulos marcados, barbilla afilada, nariz ganchuda y ojos brillantes) (Y = 45..55)
        for (int y = 45 + bobY; y <= 55 + bobY; y++)
        {
            float ft = (y - (45f + bobY)) / 10f;
            int faceHalfW = ft < 0.3f ? Mathf.RoundToInt(Mathf.Lerp(2f, 4.5f, ft / 0.3f)) : 4;
            for (int x = cx - faceHalfW; x <= cx + faceHalfW; x++)
            {
                if (x == cx - faceHalfW || x == cx + faceHalfW)
                    tex.SetPixel(x, y, skinShadow);
                else if (x < cx)
                    tex.SetPixel(x, y, skinHighlight);
                else
                    tex.SetPixel(x, y, skinMid);
            }
        }

        // Nariz aguileña sombreada y sonrisa siniestra
        tex.SetPixel(cx + 1, 50 + bobY, skinHighlight);
        tex.SetPixel(cx + 2, 49 + bobY, skinShadow);
        tex.SetPixel(cx - 1, 47 + bobY, outline);
        tex.SetPixel(cx, 47 + bobY, outline);
        tex.SetPixel(cx + 1, 48 + bobY, outline);

        // Ojos místicos con pupila brillante
        tex.SetPixel(cx - 2, 51 + bobY, eyeViolet);
        tex.SetPixel(cx - 1, 51 + bobY, isCasting ? eyeCore : eyeViolet);
        tex.SetPixel(cx + 2, 51 + bobY, eyeViolet);
        tex.SetPixel(cx + 3, 51 + bobY, isCasting ? eyeCore : eyeViolet);

        // 5. Cabello largo canoso/plateado cayendo sobre los hombros con movimiento (Y = 36..55)
        for (int y = 36 + bobY; y <= 54 + bobY; y++)
        {
            int hairWave = Mathf.RoundToInt(Mathf.Sin(phase + y * 0.3f) * (isCasting ? 1.6f : 0.9f));
            int lx = cx - 5 + hairWave;
            int rx = cx + 5 - hairWave;
            tex.SetPixel(lx, y, hairSilver);
            tex.SetPixel(lx - 1, y, hairDark);
            tex.SetPixel(rx, y, hairDark);
            tex.SetPixel(rx + 1, y, hairSilver);
        }

        // 6. Sombrero puntiagudo realista con ala curva, pliegues y hebilla de oro (Y = 54..75)
        // Ala ancha curva (Y = 54..57)
        for (int dx = -14; dx <= 14; dx++)
        {
            int brimCurve = Mathf.RoundToInt((dx * dx) / 65f);
            int by = 55 + bobY - brimCurve;
            tex.SetPixel(cx + dx, by, outline);
            tex.SetPixel(cx + dx, by + 1, dx < 0 ? cloakHighlight : hatMid);
            if (Mathf.Abs(dx) <= 11)
                tex.SetPixel(cx + dx, by + 2, hatDark);
        }

        // Cinta carmesí y hebilla de oro en la base de la copa (Y = 57..59)
        for (int x = cx - 7; x <= cx + 7; x++)
        {
            tex.SetPixel(x, 57 + bobY, hatBand);
            tex.SetPixel(x, 58 + bobY, hatBand);
        }
        for (int y = 57 + bobY; y <= 59 + bobY; y++)
        {
            for (int x = cx - 2; x <= cx + 2; x++)
            {
                if (x == cx - 2 || x == cx + 2 || y == 57 + bobY || y == 59 + bobY)
                    tex.SetPixel(x, y, buckleGold);
            }
        }

        // Copa cónica alta con pliegues y punta doblada hacia la izquierda (Y = 59..75)
        for (int y = 59 + bobY; y <= 75 + bobY; y++)
        {
            float ht = (y - (59f + bobY)) / 16f;
            float coneHalfW = Mathf.Lerp(6.8f, 0.9f, ht);
            float bend = ht * ht * (4.5f + Mathf.Sin(phase) * 0.8f);
            int minX = Mathf.RoundToInt(cx - bend - coneHalfW);
            int maxX = Mathf.RoundToInt(cx - bend + coneHalfW);

            for (int x = minX; x <= maxX; x++)
            {
                if (x < 2 || x >= w - 2) continue;
                if (x == minX || x == maxX || y == 75 + bobY)
                    tex.SetPixel(x, y, outline);
                else if (x < minX + 2)
                    tex.SetPixel(x, y, cloakHighlight);
                else if ((y - bobY) % 5 == 0)
                    tex.SetPixel(x, y, hatDark); // Pliegues del sombrero
                else
                    tex.SetPixel(x, y, hatMid);
            }
        }

        // 7. Mano izquierda canalizando magia oscura (cuando ataca o flota)
        int leftHandX = isCasting ? (cx - 13) : (cx - 10);
        int leftHandY = isCasting ? (39 + bobY) : (33 + bobY);
        // Manga izquierda
        for (int i = 0; i <= 6; i++)
        {
            int sx = Mathf.RoundToInt(Mathf.Lerp(cx - 6, leftHandX, i / 6f));
            int sy = Mathf.RoundToInt(Mathf.Lerp(42 + bobY, leftHandY, i / 6f));
            tex.SetPixel(sx, sy, cloakMid);
            tex.SetPixel(sx, sy - 1, cloakShadow);
            tex.SetPixel(sx, sy + 1, outline);
        }
        // Mano izquierda pálida
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                tex.SetPixel(leftHandX + dx, leftHandY + dy, skinHighlight);

        if (isCasting)
        {
            // Fuego arcano violeta en la mano izquierda al atacar
            for (int dy = -3; dy <= 4; dy++)
            {
                for (int dx = -3; dx <= 3; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= 1.5f) tex.SetPixel(leftHandX + dx, leftHandY + 3 + dy, orbCore);
                    else if (d <= 3.2f) tex.SetPixel(leftHandX + dx, leftHandY + 3 + dy, orbMid);
                }
            }
        }

        // 8. Báculo nudoso de madera antigua en la mano derecha con Orbe de Cristal Morado
        int staffBaseX = isCasting ? (cx + 15) : (cx + 13);
        int staffTopY = isCasting ? (56 + bobY) : (52 + bobY);
        int staffBotY = 10 + bobY;

        for (int y = staffBotY; y <= staffTopY; y++)
        {
            int crookCurve = 0;
            if (y >= staffTopY - 6)
            {
                float ct = (y - (staffTopY - 6f)) / 6f;
                crookCurve = Mathf.RoundToInt(Mathf.Sin(ct * Mathf.PI) * 2.2f);
            }
            int sx = staffBaseX + crookCurve + Mathf.RoundToInt(Mathf.Sin(y * 0.25f) * 0.6f);
            tex.SetPixel(sx - 1, y, woodLight);
            tex.SetPixel(sx, y, woodMid);
            tex.SetPixel(sx + 1, y, woodDark);
        }

        // Manga y mano derecha sujetando el báculo
        int rightHandY = isCasting ? (38 + bobY) : (34 + bobY);
        for (int i = 0; i <= 6; i++)
        {
            int sx = Mathf.RoundToInt(Mathf.Lerp(cx + 6, staffBaseX, i / 6f));
            int sy = Mathf.RoundToInt(Mathf.Lerp(42 + bobY, rightHandY, i / 6f));
            tex.SetPixel(sx, sy, cloakMid);
            tex.SetPixel(sx, sy - 1, cloakShadow);
        }
        tex.SetPixel(staffBaseX - 1, rightHandY, skinHighlight);
        tex.SetPixel(staffBaseX, rightHandY, skinMid);
        tex.SetPixel(staffBaseX - 1, rightHandY + 1, skinHighlight);

        // Orbe de cristal morado pulsante en la punta del báculo + partículas rúnicas
        int orbX = staffBaseX + 1;
        int orbY = staffTopY + 3;
        float orbRadius = (isCasting ? 4.8f : 3.6f) + (subFrame % 2) * 0.5f;

        for (int dy = -6; dy <= 6; dy++)
        {
            for (int dx = -6; dx <= 6; dx++)
            {
                int px = orbX + dx;
                int py = orbY + dy;
                if (px < 0 || px >= w || py < 0 || py >= h) continue;

                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d <= 1.7f)
                    tex.SetPixel(px, py, orbCore);
                else if (d <= orbRadius * 0.72f)
                    tex.SetPixel(px, py, orbMid);
                else if (d <= orbRadius)
                    tex.SetPixel(px, py, orbGlowOuter);
            }
        }

        // Partículas rúnicas flotando alrededor del báculo y hombros
        for (int m = 0; m < 4; m++)
        {
            float mAngle = phase + m * (Mathf.PI * 0.5f);
            int mx = orbX + Mathf.RoundToInt(Mathf.Cos(mAngle) * 5.2f);
            int my = orbY + Mathf.RoundToInt(Mathf.Sin(mAngle) * 5.2f);
            if (mx >= 1 && mx < w - 1 && my >= 1 && my < h - 1)
            {
                tex.SetPixel(mx, my, orbCore);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.46f, 0.06f), 40f);
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
