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

        public int TotalEnemies => normalGoblins + chubbyGoblins;
    }

    [Header("Prefabs de Enemigos")]
    public GameObject goblinPrefab; // Si solo se tiene el prefab base, GoblinVariantConfig se encarga de convertirlo en Gordito

    [Header("Oleadas del Encuentro")]
    public WaveDefinition[] waves;

    [Header("Puntos de Spawn y Concurrencia")]
    public Transform[] spawnPoints;
    [Range(1, 8)] public int maxSimultaneousEnemies = 4;
    public float spawnInterval = 1.0f;
    public float waveClearDelay = 1.2f;

    [Header("Audio")]
    public AudioClip waveStartSound;
    public AudioClip waveClearSound;

    public event Action<int, int> OnWaveStarted;    // (waveIndex, totalWaves)
    public event Action<int> OnWaveCleared;          // (waveIndex)
    public event Action OnAllWavesCleared;

    public int CurrentWaveIndex { get; private set; } = -1;
    public bool IsEncounterActive { get; private set; }
    public int ActiveEnemyCount => activeEnemies.Count;
    public int RemainingInCurrentWave => pendingSpawnQueue.Count + activeEnemies.Count;

    private readonly List<EnemyHealth> activeEnemies = new List<EnemyHealth>();
    private readonly Queue<bool> pendingSpawnQueue = new Queue<bool>(); // true = chubby, false = normal
    private Coroutine encounterRoutine;

    public void StartEncounter()
    {
        if (IsEncounterActive) return;
        IsEncounterActive = true;
        CurrentWaveIndex = -1;
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

            if (waveStartSound != null)
                AudioSource.PlayClipAtPoint(waveStartSound, transform.position, 0.85f);

            OnWaveStarted?.Invoke(CurrentWaveIndex + 1, waves.Length);
            Debug.Log($"[Encuentro] Iniciando Oleada {CurrentWaveIndex + 1}/{waves.Length} ({currentWave.TotalEnemies} enemigos)");

            // Llenar y mezclar aleatoriamente la cola de spawns (duendes normales y gorditos)
            pendingSpawnQueue.Clear();
            var queueList = new System.Collections.Generic.List<bool>();
            for (int n = 0; n < currentWave.normalGoblins; n++) queueList.Add(false);
            for (int c = 0; c < currentWave.chubbyGoblins; c++) queueList.Add(true);

            // Barajado Fisher-Yates para distribución aleatoria
            for (int k = queueList.Count - 1; k > 0; k--)
            {
                int rnd = UnityEngine.Random.Range(0, k + 1);
                bool temp = queueList[k];
                queueList[k] = queueList[rnd];
                queueList[rnd] = temp;
            }
            foreach (var isChubby in queueList) pendingSpawnQueue.Enqueue(isChubby);

            // Spawneo progresivo según concurrencia
            while (pendingSpawnQueue.Count > 0 || activeEnemies.Count > 0)
            {
                // Limpiar referencias muertas
                activeEnemies.RemoveAll(e => e == null || e.IsDead);

                while (activeEnemies.Count < maxSimultaneousEnemies && pendingSpawnQueue.Count > 0)
                {
                    bool isChubby = pendingSpawnQueue.Dequeue();
                    SpawnEnemy(isChubby);
                    yield return new WaitForSeconds(spawnInterval);
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

    private void SpawnEnemy(bool isChubby)
    {
        Vector3 spawnPos = GetRandomVariedSpawnPosition();

        GameObject enemyObj;
        if (goblinPrefab != null)
        {
            enemyObj = Instantiate(goblinPrefab, spawnPos, Quaternion.identity, transform);
        }
        else
        {
            // Intentar cargar el prefab estándar del proyecto
            GameObject loadedPrefab = Resources.Load<GameObject>("Prefabs/Enemies/PF_Goblin");
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
        variant.SetVariant(isChubby);

        GoblinAI ai = enemyObj.GetComponent<GoblinAI>();
        if (ai != null)
        {
            ai.waitForFirstCoin = false;
            ai.enablePatrol = false;
            ai.BeginWaveCombat(Mathf.Max(1, CurrentWaveIndex + 1));
        }

        // Barra flotante de salud verde para los duendes (estilo Nivel 1)
        EnemyFloatingHealthBar healthBar = enemyObj.GetComponent<EnemyFloatingHealthBar>();
        if (healthBar == null) healthBar = enemyObj.AddComponent<EnemyFloatingHealthBar>();
        healthBar.isBoss = false;

        // Añadir componente de teleportación para la animación de aviso
        EnemyTeleportSpawn teleport = enemyObj.GetComponent<EnemyTeleportSpawn>();
        if (teleport == null) teleport = enemyObj.AddComponent<EnemyTeleportSpawn>();

        activeEnemies.Add(health);
        health.OnDied += (h) => activeEnemies.Remove(h);

        teleport.BeginSpawnSequence();
    }

    private const float MinMapX = -17.5f;
    private const float MaxMapX = 17.5f;
    private const float MinMapY = -8.5f;
    private const float MaxMapY = 8.5f;

    private Vector3 GetRandomVariedSpawnPosition()
    {
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

