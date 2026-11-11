using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EncounterWaveManager : MonoBehaviour
{
    [System.Serializable]
    public class WaveDefinition
    {
        public string waveName = "Oleada";
        public int normalGoblins = 3;
        public int chubbyGoblins = 0;
        public int archerGoblins = 0;

        public int TotalEnemies => normalGoblins + chubbyGoblins + archerGoblins;
    }

    [Header("Prefabs de Enemigos")]
    public GameObject goblinPrefab; // GoblinVariantConfig configura Normal, Gordito o Arquero

    [Header("Oleadas del Encuentro")]
    public WaveDefinition[] waves;

    [Header("Puntos de Spawn y Concurrencia")]
    public Transform[] spawnPoints;
    [SerializeField] private bool useFixedSpawnPoints;
    [Range(1, 8)] public int maxSimultaneousEnemies = 4;
    public float spawnInterval = 1.0f;
    public float waveClearDelay = 1.2f;

    [Header("Audio")]
    public AudioClip waveStartSound;
    public AudioClip waveClearSound;
    [SerializeField] private AudioSource waveAudio;
    [SerializeField] private bool limitHealingDrops;

    public event Action<int, int> OnWaveStarted;    // (waveIndex, totalWaves)
    public event Action<int> OnWaveCleared;          // (waveIndex)
    public event Action OnAllWavesCleared;

    public int CurrentWaveIndex { get; private set; } = -1;
    public bool IsEncounterActive { get; private set; }
    public int ActiveEnemyCount => activeEnemies.Count;
    public int RemainingInCurrentWave => pendingSpawnQueue.Count + activeEnemies.Count;

    private const int SecondWaveIndex = 1;
    private const int BossReturnEnemyThreshold = 4;

    internal bool CanBossReturn => !IsEncounterActive || CurrentWaveIndex > SecondWaveIndex ||
        (CurrentWaveIndex == SecondWaveIndex && RemainingInCurrentWave <= BossReturnEnemyThreshold);
    internal bool RetreatPressureActive => retreatPressure;
    internal Func<int, Vector3?> ReinforcementSpawnPoint { get; set; }

    private bool retreatPressure;
    private float nextPressureCharge;
    private int CurrentEnemyLimit => retreatPressure ? Mathf.Min(8, maxSimultaneousEnemies + 2) : maxSimultaneousEnemies;

    private readonly List<EnemyHealth> activeEnemies = new List<EnemyHealth>();
    private enum SpawnRole { Normal, Heavy, Archer, LeftFlank, RightFlank }
    private readonly Queue<SpawnRole> pendingSpawnQueue = new Queue<SpawnRole>();
    private Coroutine encounterRoutine;
    private int spawnedCounterInWave = 0;
    private static readonly bool[] HealingDropPattern = { true, true, true, false, true };
    private int healingCandidates;

    private static bool IsLevel3Scene()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        return sceneName.IndexOf("Nivel3", StringComparison.OrdinalIgnoreCase) >= 0 ||
               sceneName.IndexOf("Level3", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    internal void SetRetreatPressure(bool active)
    {
        retreatPressure = active;
        nextPressureCharge = Time.time;
    }

    internal bool TryReserveBowCharge()
    {
        if (!retreatPressure && ReinforcementSpawnPoint == null) return true;
        if (Time.time < nextPressureCharge) return false;
        nextPressureCharge = Time.time + (retreatPressure ? 0.85f : 1.1f);
        return true;
    }

    public void StartEncounter()
    {
        if (IsEncounterActive) return;
        IsEncounterActive = true;
        CurrentWaveIndex = -1;
        healingCandidates = 0;
        encounterRoutine = StartCoroutine(RunEncounterRoutine());
    }

    public void StopEncounter()
    {
        if (encounterRoutine != null)
        {
            StopCoroutine(encounterRoutine);
            encounterRoutine = null;
        }

        foreach (var enemy in activeEnemies)
        {
            if (enemy != null) Destroy(enemy.gameObject);
        }
        activeEnemies.Clear();
        pendingSpawnQueue.Clear();
        IsEncounterActive = false;
    }

    private IEnumerator RunEncounterRoutine()
    {
        if (waves == null || waves.Length == 0)
        {
            Debug.LogWarning("[EncounterWaveManager] No hay oleadas configuradas.");
            IsEncounterActive = false;
            OnAllWavesCleared?.Invoke();
            yield break;
        }

        for (int i = 0; i < waves.Length; i++)
        {
            CurrentWaveIndex = i;
            WaveDefinition currentWave = waves[i];

            PlayWaveStartSound();

            OnWaveStarted?.Invoke(CurrentWaveIndex + 1, waves.Length);
            Debug.Log($"[Encuentro] Iniciando Oleada {CurrentWaveIndex + 1}/{waves.Length} ({currentWave.TotalEnemies} enemigos)");

            pendingSpawnQueue.Clear();
            EnqueueWave(currentWave);

            // Spawneo progresivo según concurrencia
            while (pendingSpawnQueue.Count > 0 || activeEnemies.Count > 0)
            {
                // Limpiar referencias muertas
                activeEnemies.RemoveAll(e => e == null || e.IsDead);

                while (activeEnemies.Count < CurrentEnemyLimit && pendingSpawnQueue.Count > 0)
                {
                    SpawnRole enemyType = pendingSpawnQueue.Dequeue();
                    SpawnEnemy(enemyType);
                    yield return new WaitForSeconds(retreatPressure ? spawnInterval * 0.7f : spawnInterval);
                }

                yield return new WaitForSeconds(0.3f);
            }

            // Oleada superada
            if (waveClearSound != null)
                AudioSource.PlayClipAtPoint(waveClearSound, transform.position, 0.85f);

            OnWaveCleared?.Invoke(CurrentWaveIndex + 1);
            Debug.Log($"[Encuentro] Oleada {CurrentWaveIndex + 1} superada.");

            if (i < waves.Length - 1)
            {
                yield return new WaitForSeconds(waveClearDelay);
            }
        }

        IsEncounterActive = false;
        Debug.Log("[Encuentro] ¡Todas las oleadas del encuentro han sido superadas!");
        OnAllWavesCleared?.Invoke();
    }

    internal void AddReinforcements(WaveDefinition wave)
    {
        if (IsEncounterActive)
        {
            if (waveAudio != null) PlayWaveStartSound();
            EnqueueWave(wave);
            return;
        }

        waves = new[] { wave };
        StartEncounter();
    }

    private void PlayWaveStartSound()
    {
        if (waveStartSound == null) return;

        if (waveAudio != null)
            waveAudio.PlayOneShot(waveStartSound, 0.85f);
        else
            AudioSource.PlayClipAtPoint(waveStartSound, transform.position, 0.85f);
    }

    private void EnqueueWave(WaveDefinition wave)
    {
        int remChubby = wave.chubbyGoblins;
        int remArcher = wave.archerGoblins;
        int remNormal = wave.normalGoblins;

        if (ReinforcementSpawnPoint != null && remNormal >= 2)
        {
            pendingSpawnQueue.Enqueue(SpawnRole.LeftFlank);
            pendingSpawnQueue.Enqueue(SpawnRole.RightFlank);
            remNormal -= 2;
        }

        while (remChubby > 0 || remArcher > 0 || remNormal > 0)
        {
            if (remChubby > 0)
            {
                pendingSpawnQueue.Enqueue(SpawnRole.Heavy);
                remChubby--;
            }
            if (remArcher > 0)
            {
                pendingSpawnQueue.Enqueue(SpawnRole.Archer);
                remArcher--;
            }
            if (remChubby > 0 && remChubby >= remNormal && remChubby >= remArcher)
            {
                pendingSpawnQueue.Enqueue(SpawnRole.Heavy);
                remChubby--;
            }
            if (remNormal > 0)
            {
                pendingSpawnQueue.Enqueue(SpawnRole.Normal);
                remNormal--;
            }
        }
    }

    private void SpawnEnemy(SpawnRole role)
    {
        bool isChubby = role == SpawnRole.Heavy;
        bool isArcher = role == SpawnRole.Archer;
        int flank = role == SpawnRole.LeftFlank ? -1 : role == SpawnRole.RightFlank ? 1 : 0;
        Vector3 spawnPos = ReinforcementSpawnPoint?.Invoke(flank) ?? GetRandomVariedSpawnPosition();

        GameObject enemyObj;
        if (goblinPrefab != null)
        {
            enemyObj = Instantiate(goblinPrefab, spawnPos, Quaternion.identity, transform);
        }
        else
        {
            // Intentar cargar el prefab estándar del proyecto
            GameObject loadedPrefab = Resources.Load<GameObject>("Prefabs/Enemies/PF_Goblin");
#if UNITY_EDITOR
            if (loadedPrefab == null)
                loadedPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/PF_Goblin.prefab");
#endif
            if (loadedPrefab != null)
                enemyObj = Instantiate(loadedPrefab, spawnPos, Quaternion.identity, transform);
            else
            {
                Debug.LogError("[EncounterWaveManager] Falta asignar el prefab de Goblin.");
                return;
            }
        }

        // Asegurar componentes de salud y variante
        EnemyHealth health = enemyObj.GetComponent<EnemyHealth>();
        if (health == null) health = enemyObj.AddComponent<EnemyHealth>();

        GoblinVariantConfig variant = enemyObj.GetComponent<GoblinVariantConfig>();
        if (variant == null) variant = enemyObj.AddComponent<GoblinVariantConfig>();

        // En Nivel 3: una parte de los duendes base, duendes fuertes y arqueros aparecen con el Aura Verde
        // otorgada por el hechizo de La Llorona (más resistentes según su tipo).
        spawnedCounterInWave++;
        bool hasGreenAura = IsLevel3Scene() && ((spawnedCounterInWave % 2 == 0) || (CurrentWaveIndex >= 1 && spawnedCounterInWave % 3 == 0));
        variant.SetVariant(isChubby, isArcher, hasGreenAura);
        if (limitHealingDrops)
        {
            bool canDropHealing = isChubby || isArcher;
            health.EncounterHealingDrop = canDropHealing &&
                HealingDropPattern[healingCandidates++ % HealingDropPattern.Length];
        }

        GoblinAI ai = enemyObj.GetComponent<GoblinAI>();
        if (ai != null)
        {
            ai.waitForFirstCoin = false;
            ai.enablePatrol = true;
            ai.BeginWaveCombat(Mathf.Max(1, CurrentWaveIndex + 1));

            // Si es Duende Arquero, volver a aplicar su configuración de distancia y mayor velocidad después de BeginWaveCombat
            if (isArcher)
            {
                ai.moveSpeed = 1.85f;
                ai.patrolSpeed = 1.15f;
                ai.secondCoinSpeed = 2.40f;
                var archerCtrl = enemyObj.GetComponent<GoblinArcherController>();
                if (archerCtrl != null)
                {
                    ai.stopDistance = archerCtrl.idealRangedStopDistance;
                    ai.attackRange = 0.25f;
                    ai.enableLungeAttack = false;
                }
            }

            GoblinAwarenessUI awarenessUI = FindObjectOfType<GoblinAwarenessUI>();
            if (awarenessUI != null)
            {
                awarenessUI.Track(ai);
            }
        }

        // Si tenía barra flotante duplicada, desactivarla para usar la barra segmentada oficial del HUD (GoblinHealthUI)
        EnemyFloatingHealthBar healthBar = enemyObj.GetComponent<EnemyFloatingHealthBar>();
        if (healthBar != null) Destroy(healthBar);

        // Añadir componente de teleportación para la animación de aviso
        EnemyTeleportSpawn teleport = enemyObj.GetComponent<EnemyTeleportSpawn>();
        if (teleport == null) teleport = enemyObj.AddComponent<EnemyTeleportSpawn>();

        activeEnemies.Add(health);
        health.OnDied += (h) => activeEnemies.Remove(h);

        teleport.BeginSpawnSequence();
    }

    private const float MinMapX = -17.5f;
    private const float MaxMapX = 24.5f;
    private const float MinMapY = -15.5f;
    private const float MaxMapY = 10.5f;

    private Vector3 GetRandomVariedSpawnPosition()
    {
        if (useFixedSpawnPoints && spawnPoints != null && spawnPoints.Length > 0)
        {
            var player = PlayerHealth.ActiveInstance;
            for (int offset = 0; offset < spawnPoints.Length; offset++)
            {
                Transform point = spawnPoints[(spawnedCounterInWave + offset) % spawnPoints.Length];
                if (point != null && (player == null || Vector2.Distance(player.transform.position, point.position) >= 2f))
                    return point.position;
            }
            return spawnPoints[spawnedCounterInWave % spawnPoints.Length].position;
        }

        Vector3 basePos = transform.position;
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform sp = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
            if (sp != null) basePos = sp.position;
        }

        // Obtener máscara de colisiones sólida (incluyendo Obstacles que contiene tumbas y árboles)
        int solidMask = LayerMask.GetMask("Obstacles", "Wall", "Default");
        if (solidMask == 0) solidMask = ~LayerMask.GetMask("Ignore Raycast", "UI");

        // Intentar hasta 16 posiciones libres de colisión y dentro del mapa
        for (int attempt = 0; attempt < 16; attempt++)
        {
            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float dist = UnityEngine.Random.Range(1.8f, 3.2f);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist, 0f);
            Vector3 candidate = basePos + offset;

            // Clampear estrictamente dentro del perímetro caminable del cementerio
            candidate.x = Mathf.Clamp(candidate.x, MinMapX, MaxMapX);
            candidate.y = Mathf.Clamp(candidate.y, MinMapY, MaxMapY);

            // Verificar si hay algún obstáculo sólido (árbol, tumba, barda) en un radio de 0.48f
            Collider2D[] hits = Physics2D.OverlapCircleAll(candidate, 0.48f, solidMask);
            bool blocked = false;
            foreach (var h in hits)
            {
                if (h != null && !h.isTrigger && !h.transform.IsChildOf(transform))
                {
                    blocked = true;
                    break;
                }
            }

            if (!blocked)
            {
                return candidate;
            }
        }

        // Si todos los puntos exteriores colisionan, usar el círculo interior despejado del altar (1.2m a 1.4m)
        float fallbackAngle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector3 fallbackPos = transform.position + new Vector3(Mathf.Cos(fallbackAngle) * 1.3f, Mathf.Sin(fallbackAngle) * 1.3f, 0f);
        fallbackPos.x = Mathf.Clamp(fallbackPos.x, MinMapX, MaxMapX);
        fallbackPos.y = Mathf.Clamp(fallbackPos.y, MinMapY, MaxMapY);
        return fallbackPos;
    }

    private Transform GetRandomSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return transform;
        return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
    }
}
