using System;
using System.Collections;
using UnityEngine;

public class ToyEncounterZone : MonoBehaviour
{
    public static int GlobalEncountersCompleted = 0;
    public static bool IsAnyEncounterActive = false;
    private static GameObject globalCachedBossPrefab;

    [Header("Identificación del Encuentro")]
    [Range(0, 2)] public int encounterIndex = 0; // 0 = Encuentro 1, 1 = Encuentro 2, 2 = Encuentro 3

    [Header("Referencias del Encuentro")]
    public ToyPickup targetToy;
    public EncounterWaveManager waveManager;
    public ForceFieldBarrier forceField;

    [Header("Parámetros de Búsqueda con [E]")]
    public float searchRadius = 4.2f;
    public float searchDuration = 1.0f;

    [Header("Recompensas Especiales")]
    public bool dropBigKnifeOnClear = false;

    [Header("Jefe Final (Solo Encuentro 3)")]
    public GameObject bossPrefab;
    public Transform bossSpawnPoint;

    public event Action OnEncounterTriggered;
    public event Action OnEncounterCleared;

    private bool triggered = false;
    private float currentSearchTimer = 0f;
    private bool isPlayerSearching = false;
    private Transform playerTransform;
    private Rigidbody2D playerBody;
    private Level2HUD hud;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetStaticState()
    {
        GlobalEncountersCompleted = 0;
        IsAnyEncounterActive = false;
        globalCachedBossPrefab = null;
    }

    private void Awake()
    {
        // El juguete inicia oculto hasta que se derroten las oleadas de este encuentro
        if (targetToy != null)
        {
            targetToy.gameObject.SetActive(false);
        }

        if (bossPrefab != null)
        {
            globalCachedBossPrefab = bossPrefab;
        }
    }

    private void Start()
    {
        hud = FindObjectOfType<Level2HUD>();

        if (targetToy != null)
        {
            targetToy.gameObject.SetActive(false);
        }

        if (waveManager != null)
        {
            waveManager.OnAllWavesCleared += HandleWavesCleared;
            waveManager.OnWaveStarted += HandleWaveStarted;
        }

        // Asegurar que ToySearchUI esté disponible en la escena
        if (ToySearchUI.Instance == null)
        {
            GameObject uiObj = new GameObject("ToySearchUI");
            uiObj.AddComponent<ToySearchUI>();
        }

        // Asegurar que Level2ClueSystem esté disponible en la escena
        if (Level2ClueSystem.Instance == null)
        {
            GameObject clueObj = new GameObject("Level2ClueSystem");
            clueObj.AddComponent<Level2ClueSystem>();
        }
    }

    private void OnDestroy()
    {
        if (waveManager != null)
        {
            waveManager.OnAllWavesCleared -= HandleWavesCleared;
            waveManager.OnWaveStarted -= HandleWaveStarted;
        }
    }

    private void Update()
    {
        if (triggered || IsAnyEncounterActive) return;

        // Solo permitir interactuar con el encuentro que toca según el progreso
        if (encounterIndex != GlobalEncountersCompleted) return;

        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                playerTransform = p.transform;
                playerBody = p.GetComponent<Rigidbody2D>();
            }
            return;
        }

        float dist = Vector2.Distance(transform.position, playerTransform.position);
        if (dist <= searchRadius)
        {
            // El jugador está en rango de interacción
            bool holdingSearchKey = Input.GetKey(KeyCode.E) || Input.GetKeyDown(KeyCode.E) || GameControls.SearchHeld;
            bool centerAltarContact = dist <= 1.6f;

            if (holdingSearchKey || centerAltarContact)
            {
                isPlayerSearching = true;
                currentSearchTimer += Time.deltaTime * (holdingSearchKey ? 1.0f : 1.4f);

                // Evitar que el jugador resbale mientras busca si sostiene la tecla
                if (holdingSearchKey && playerBody != null) playerBody.velocity = Vector2.zero;

                if (currentSearchTimer >= searchDuration)
                {
                    // ¡Búsqueda completada! Iniciar cinemática y oleada
                    currentSearchTimer = 0f;
                    isPlayerSearching = false;
                    if (ToySearchUI.Instance != null) ToySearchUI.Instance.Hide();
                    StartEncounterSequence();
                    return;
                }
            }
            else
            {
                isPlayerSearching = false;
                currentSearchTimer = Mathf.Max(0f, currentSearchTimer - Time.deltaTime * 2.0f);
            }

            if (ToySearchUI.Instance != null)
            {
                ToySearchUI.Instance.SetTarget(transform, currentSearchTimer / searchDuration, isPlayerSearching);
            }
        }
        else
        {
            if (currentSearchTimer > 0f)
            {
                currentSearchTimer = 0f;
                isPlayerSearching = false;
                if (ToySearchUI.Instance != null) ToySearchUI.Instance.Hide();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered || IsAnyEncounterActive) return;
        if (encounterIndex != GlobalEncountersCompleted) return;
        if (other.CompareTag("Player") || other.GetComponent<Cainos.PixelArtTopDown_Basic.TopDownCharacterController>() != null)
        {
            if (ToySearchUI.Instance != null)
            {
                ToySearchUI.Instance.SetTarget(transform, 0f, false);
            }
        }
    }

    public void StartEncounterSequence()
    {
        if (triggered || IsAnyEncounterActive) return;
        triggered = true;
        IsAnyEncounterActive = true;

        // Auto-configurar las oleadas y recompensas correspondientes al paso actual
        ConfigureWavesForStep(encounterIndex);

        OnEncounterTriggered?.Invoke();
        StartCoroutine(EncounterIntroRoutine());
    }

    public void AutoConfigureDefaultWavesIfEmpty()
    {
        ConfigureWavesForStep(encounterIndex);
    }

    private void ConfigureWavesForStep(int step)
    {
        if (waveManager == null) return;

        if (step == 0)
        {
            // Encuentro 1:
            // Oleada 1: 3 duendes normales
            // Oleada 2: 3 duendes normales + 1 duende gordito (4 enemigos)
            dropBigKnifeOnClear = true;
            waveManager.waves = new EncounterWaveManager.WaveDefinition[]
            {
                new EncounterWaveManager.WaveDefinition { waveName = "Oleada 1", normalGoblins = 3, chubbyGoblins = 0 },
                new EncounterWaveManager.WaveDefinition { waveName = "Oleada 2", normalGoblins = 3, chubbyGoblins = 1 }
            };
            waveManager.maxSimultaneousEnemies = 4;
        }
        else if (step == 1)
        {
            // Encuentro 2:
            // Oleada 1: 3 duendes normales + 2 duendes gorditos (5 enemigos)
            // Oleada 2: 4 duendes normales + 3 duendes gorditos (7 enemigos)
            dropBigKnifeOnClear = false;
            waveManager.waves = new EncounterWaveManager.WaveDefinition[]
            {
                new EncounterWaveManager.WaveDefinition { waveName = "Oleada 1", normalGoblins = 3, chubbyGoblins = 2 },
                new EncounterWaveManager.WaveDefinition { waveName = "Oleada 2", normalGoblins = 4, chubbyGoblins = 3 }
            };
            waveManager.maxSimultaneousEnemies = 4;
        }
        else if (step == 2)
        {
            // Encuentro 3: Gran Horda Masiva antes del Niño Espectral
            // Oleada 1: 8 duendes normales + 4 duendes gorditos (12 enemigos)
            // Oleada 2: 10 duendes normales + 6 duendes gorditos (16 enemigos)
            // Total: 28 enemigos con alta intensidad (hasta 7 simultáneos)
            dropBigKnifeOnClear = false;
            waveManager.waves = new EncounterWaveManager.WaveDefinition[]
            {
                new EncounterWaveManager.WaveDefinition { waveName = "Vanguardia Espectral", normalGoblins = 8, chubbyGoblins = 4 },
                new EncounterWaveManager.WaveDefinition { waveName = "Gran Horda del Panteón", normalGoblins = 10, chubbyGoblins = 6 }
            };
            waveManager.maxSimultaneousEnemies = 7;
        }
    }

    private IEnumerator EncounterIntroRoutine()
    {
        // 1. Asegurar que el juguete permanezca oculto durante el combate
        if (targetToy != null)
        {
            targetToy.gameObject.SetActive(false);
        }

        // 2. Cinemática / aviso según el número de encuentro
        if (encounterIndex == 0)
        {
            var seq = GetComponent<LloronaCinematicSequence>();
            if (seq == null) seq = gameObject.AddComponent<LloronaCinematicSequence>();

            bool cinematicDone = false;
            Vector3 pos = targetToy != null ? targetToy.transform.position : transform.position;
            seq.PlayEncounter1Cinematic(pos, () => { cinematicDone = true; });

            while (!cinematicDone)
            {
                yield return null;
            }
        }
        else if (encounterIndex == 1)
        {
            if (ParchmentMessageUI.Instance != null)
            {
                ParchmentMessageUI.Instance.Show(
                    "SEGUNDO RECUERDO",
                    "¡Los espíritus del panteón se resisten a soltar este juguete!\nRepele el asalto espectral y limpia la zona.\nAtaca con [Clic / Espacio].",
                    null,
                    null,
                    false
                );
            }
            yield return new WaitForSeconds(1.0f);
        }
        else if (encounterIndex == 2)
        {
            // Encuentro 3: Sin campo de fuerza para que el jugador y el jefe se muevan por todo el mapa
            if (forceField != null)
            {
                forceField.DeactivateBarrier();
                forceField.gameObject.SetActive(false);
            }

            if (ParchmentMessageUI.Instance != null)
            {
                ParchmentMessageUI.Instance.Show(
                    "HORDA ESPECTRAL",
                    "¡El panteón despierta en cólera total!\nDerrota a las 2 grandes hordas para desafiar al Niño Espectral.\nAtaca con [J / Clic].",
                    null,
                    null,
                    false
                );
            }
            yield return new WaitForSeconds(1.0f);
        }

        // 3. Iniciar las oleadas de enemigos con teleportación
        if (waveManager != null)
        {
            waveManager.StartEncounter();
        }
    }

    private void HandleWaveStarted(int currentWave, int totalWaves)
    {
        if (hud != null)
        {
            hud.ShowNotification($"ENCUENTRO {encounterIndex + 1}", $"Oleada {currentWave} de {totalWaves}", 2.5f);
        }
    }

    private void HandleWavesCleared()
    {
        if (GlobalEncountersCompleted < 2)
        {
            // Encuentros 1 y 2: Reaparece el juguete purificado
            StartCoroutine(RestoreToyRoutine());
        }
        else
        {
            // Encuentro 3: Tras vencer la horda de 10 enemigos, aparece el Niño Espectral
            StartCoroutine(SpawnBossRoutine());
        }
    }

    private IEnumerator RestoreToyRoutine()
    {
        yield return new WaitForSeconds(0.6f);

        // 1. Reaparece el juguete purificado
        if (targetToy != null)
        {
            targetToy.gameObject.SetActive(true);
        }

        // 2. Drop de arma si corresponde (Encuentro 1 = Cuchillo Grande)
        if (dropBigKnifeOnClear)
        {
            Vector3 dropPos = targetToy != null
                ? targetToy.transform.position + Vector3.right * 0.9f
                : transform.position + Vector3.right * 0.9f;

            WeaponPickup.SpawnWeaponDrop(WeaponType.BigKnife, dropPos);

            if (ParchmentMessageUI.Instance != null)
            {
                ParchmentMessageUI.Instance.Show(
                    "¡CUCHILLO GRANDE LIBERADO!",
                    "Has derrotado a los soldados de La Llorona.\nPasa sobre el Cuchillo Grande en el suelo para equiparlo (18 DMG).\nRecupera el juguete y pulsa [3] para seleccionarlo.\n(1: Farol  |  2: Puños  |  3: Cuchillo)",
                    null,
                    null,
                    false
                );
            }
        }
        else
        {
            if (ParchmentMessageUI.Instance != null)
            {
                ParchmentMessageUI.Instance.Show(
                    "¡ÁREA PURIFICADA!",
                    "Las sombras del panteón retroceden.\nYa puedes recoger el segundo juguete.",
                    null,
                    null,
                    false
                );
            }
        }

        GlobalEncountersCompleted++;
        IsAnyEncounterActive = false;
        OnEncounterCleared?.Invoke();
    }

    private IEnumerator SpawnBossRoutine()
    {
        Vector3 spawnPos = bossSpawnPoint != null
            ? bossSpawnPoint.position
            : (targetToy != null ? targetToy.transform.position : transform.position);

        // Diálogo del Niño Espectral en pergamino antiguo con pausa
        bool bossParchmentDone = false;
        if (ParchmentMessageUI.Instance != null)
        {
            ParchmentMessageUI.Instance.Show(
                "NIÑO ESPECTRAL",
                "«La Llorona me envió...\n¡Nunca saldrás con vida de este panteón!»",
                null,
                () => { bossParchmentDone = true; },
                true
            );

            while (!bossParchmentDone)
            {
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(1.5f);
        }

        yield return new WaitForSeconds(0.35f);

        GameObject bossInstance = null;
        if (bossPrefab != null)
        {
            bossInstance = Instantiate(bossPrefab, spawnPos, Quaternion.identity);
        }
        else if (globalCachedBossPrefab != null)
        {
            bossInstance = Instantiate(globalCachedBossPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            GameObject existingPf = null;
#if UNITY_EDITOR
            existingPf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/PF_GhostChild.prefab");
#endif
            if (existingPf != null)
            {
                bossInstance = Instantiate(existingPf, spawnPos, Quaternion.identity);
            }
            else
            {
                bossInstance = new GameObject("Boss_NiñoEspectral");
                bossInstance.transform.position = spawnPos;
            }
        }

        // Asegurar componentes esenciales del Jefe
        EnemyHealth bossHealth = bossInstance.GetComponent<EnemyHealth>();
        if (bossHealth == null) bossHealth = bossInstance.AddComponent<EnemyHealth>();
        bossHealth.variant = EnemyHealth.EnemyVariant.SpectralChildBoss;
        bossHealth.maxHealth = 180;
        bossHealth.currentHealth = 180;

        SpectralChildBossAI bossAI = bossInstance.GetComponent<SpectralChildBossAI>();
        if (bossAI == null) bossAI = bossInstance.AddComponent<SpectralChildBossAI>();

        var oldAi = bossInstance.GetComponent<GhostChildAI>();
        if (oldAi != null)
        {
            oldAi.enabled = false;
            Destroy(oldAi);
        }

        bossHealth.OnDied += HandleBossDefeated;
    }

    private void HandleBossDefeated(EnemyHealth _)
    {
        // 1. Desactivar campo de fuerza de la arena
        if (forceField != null)
        {
            forceField.DeactivateBarrier();
        }

        // 2. Reaparecer el tercer juguete para que el jugador lo recoja
        if (targetToy != null)
        {
            targetToy.gameObject.SetActive(true);
        }

        // 3. Drop de Machete (25 DMG) y Poción de Vida Completa
        Vector3 dropPos = targetToy != null
            ? targetToy.transform.position + Vector3.right * 1.1f
            : transform.position + Vector3.right * 1.1f;

        WeaponPickup.SpawnWeaponDrop(WeaponType.Machete, dropPos);

        if (ParchmentMessageUI.Instance != null)
        {
            ParchmentMessageUI.Instance.Show(
                "¡JEFE DERROTADO!",
                "Has vencido al Niño Espectral.\nRecoge el Machete (25 DMG) y la Poción de Vida del suelo.\nRecupera el último juguete para desbloquear la salida del panteón.",
                null,
                null,
                false
            );
        }

        GlobalEncountersCompleted++;
        IsAnyEncounterActive = false;
        OnEncounterCleared?.Invoke();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = encounterIndex == 0
            ? new Color(0.2f, 0.9f, 0.4f, 0.65f)
            : (encounterIndex == 1 ? new Color(1f, 0.75f, 0.2f, 0.65f) : new Color(0.2f, 0.85f, 1f, 0.8f));

        Gizmos.DrawWireSphere(transform.position, searchRadius);

        if (waveManager != null && waveManager.spawnPoints != null)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.85f);
            foreach (var sp in waveManager.spawnPoints)
            {
                if (sp != null)
                {
                    Gizmos.DrawSphere(sp.position, 0.25f);
                    Gizmos.DrawLine(transform.position, sp.position);
                }
            }
        }
    }
}
