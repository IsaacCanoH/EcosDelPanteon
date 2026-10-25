using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-140)]
public class GoblinWaveDirector : MonoBehaviour
{
    public GoldCoinCollector collector;
    public PlayerHealth playerHealth;
    public GoblinSpawnManager spawnManager;
    public GoblinCombat reinforcementPrefab;
    public GoblinCombat[] initialGoblins;
    public GoblinAwarenessUI awarenessUI;
    public AudioSource waveAudio;
    public AudioClip waveBell;
    public float minimumSpawnDistance = 4.5f;
    public float spawnInterval = 1.1f;
    [Min(0.6f)] public float waveClearDelay = 1.2f;

    private readonly HashSet<GoblinCombat> alive = new HashSet<GoblinCombat>();
    private readonly List<Vector2> route = new List<Vector2>();
    private int spawned;
    private int defeated;
    private float nextSpawnAt;
    private bool running;
    private float clearAt = -1f;

    public int CompletedWaves { get; private set; }
    public int CurrentWave { get; private set; }
    public bool IsWaveActive { get; private set; }
    public bool IsComplete { get; private set; }
    public int WaveSize { get { return CurrentWave + 2; } }
    public int RemainingEnemies { get { return IsWaveActive ? Mathf.Max(0, WaveSize - defeated) : 0; } }
    public int ActiveEnemies { get { return alive.Count; } }

    private void OnEnable()
    {
        running = true;
        if (collector != null) collector.OnCoinCollected += BeginWave;
    }

    private void Start()
    {
        if (initialGoblins != null)
            foreach (var goblin in initialGoblins) Register(goblin);
    }

    public void Register(GoblinCombat goblin)
    {
        if (goblin == null || goblin.IsBanished) return;
        alive.Add(goblin);
        if (awarenessUI != null) awarenessUI.Track(goblin.GetComponent<GoblinAI>());
    }

    public void NotifyBanished(GoblinCombat goblin)
    {
        if (!alive.Remove(goblin)) return;
        if (IsWaveActive) defeated++;
    }

    public void Unregister(GoblinCombat goblin)
    {
        if (!alive.Remove(goblin)) return;
        // Un objeto retirado sin ser derrotado debe poder ser reemplazado.
        if (running && IsWaveActive) spawned = Mathf.Max(defeated, spawned - 1);
    }

    private void BeginWave(int coinNumber)
    {
        if (!running || IsWaveActive || IsComplete ||
            coinNumber != CompletedWaves + 1 || coinNumber > 3) return;
        CurrentWave = coinNumber;
        defeated = 0;
        clearAt = -1f;
        spawned = alive.Count;
        IsWaveActive = true;
        nextSpawnAt = Time.time + 0.7f;
        foreach (var goblin in alive)
            goblin.GetComponent<GoblinAI>().BeginWaveCombat(CurrentWave);
        if (waveAudio != null && waveBell != null) waveAudio.PlayOneShot(waveBell);
    }

    private void Update()
    {
        if (!running || !IsWaveActive || Time.timeScale <= 0f ||
            (playerHealth != null && playerHealth.currentHealth <= 0)) return;
        if (defeated >= WaveSize && alive.Count == 0)
        {
            if (clearAt < 0f) clearAt = Time.time + waveClearDelay;
            if (Time.time < clearAt) return;
            IsWaveActive = false;
            CompletedWaves = CurrentWave;
            IsComplete = CompletedWaves == 3;
            return;
        }
        if (spawned < WaveSize && alive.Count < 3 && Time.time >= nextSpawnAt)
        {
            if (TrySpawn()) spawned++;
            nextSpawnAt = Time.time + spawnInterval;
        }
    }

    private bool TrySpawn()
    {
        if (reinforcementPrefab == null || spawnManager == null ||
            spawnManager.navigation == null || collector == null) return false;
        var points = spawnManager.spawnPoints;
        if (points == null || points.Length == 0) return false;
        var shape = reinforcementPrefab.GetComponent<BoxCollider2D>();
        Vector2 offset = shape.offset;
        Vector2 size = shape.size + Vector2.one * spawnManager.navigation.clearance * 2f;
        Vector2 goal = (Vector2)collector.transform.position + offset;
        int first = Random.Range(0, points.Length);
        for (int i = 0; i < points.Length; i++)
        {
            Transform point = points[(first + i) % points.Length];
            if (point == null || !point.gameObject.activeInHierarchy ||
                Vector2.Distance(point.position, collector.transform.position) < minimumSpawnDistance)
                continue;
            Vector2 center = (Vector2)point.position + offset;
            if (!spawnManager.navigation.Clear(center, center)) continue;
            bool blocked = false;
            foreach (var collider in Physics2D.OverlapBoxAll(center, size, 0f))
                if (!collider.isTrigger) { blocked = true; break; }
            if (blocked) continue;
            foreach (var other in alive)
                if (other != null && Vector2.Distance(point.position, other.transform.position) < 1f)
                { blocked = true; break; }
            if (blocked || !spawnManager.navigation.FindPath(center, goal, route) ||
                route.Count == 0 || Vector2.Distance(route[route.Count - 1], goal) > 0.35f)
                continue;
            GoblinCombat goblin = Instantiate(reinforcementPrefab, point.position, Quaternion.identity, transform);
            goblin.name = "Duende_Oleada_" + CurrentWave + "_" + (spawned + 1);
            Register(goblin);
            goblin.GetComponent<GoblinAI>().BeginWaveCombat(CurrentWave);
            return true;
        }
        return false;
    }

    private void OnDisable()
    {
        running = false;
        if (collector != null) collector.OnCoinCollected -= BeginWave;
    }
}
