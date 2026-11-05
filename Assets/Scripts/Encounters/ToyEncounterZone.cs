using System;
using System.Collections;
using UnityEngine;

public class ToyEncounterZone : MonoBehaviour
{
    public static int GlobalEncountersCompleted = 0;
    public static bool IsAnyEncounterActive = false;
    private static GameObject globalCachedBossPrefab;
    private static int lastSceneResetFrame = -1;

    public static void ResetAllEncounterStatics()
    {
        GlobalEncountersCompleted = 0;
        IsAnyEncounterActive = false;
        Level2HUD.IntroSequencePlaying = false;
    }

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
    private bool bossDefeatSequenceStarted = false;
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
        if (lastSceneResetFrame != Time.frameCount)
        {
            lastSceneResetFrame = Time.frameCount;
            ResetAllEncounterStatics();
        }

        triggered = false;
        bossDefeatSequenceStarted = false;
        currentSearchTimer = 0f;
        isPlayerSearching = false;

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

        if (ToySearchUI.Instance == null)
        {
            GameObject uiObj = new GameObject("ToySearchUI");
            uiObj.AddComponent<ToySearchUI>();
        }

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
            bool holdingSearchKey = Input.GetKey(KeyCode.E) || Input.GetKeyDown(KeyCode.E) || GameControls.SearchHeld;
            bool centerAltarContact = dist <= 1.6f;

            if (holdingSearchKey || centerAltarContact)
            {
                isPlayerSearching = true;
                currentSearchTimer += Time.deltaTime * (holdingSearchKey ? 1.0f : 1.4f);

                if (holdingSearchKey && playerBody != null) playerBody.velocity = Vector2.zero;

                if (currentSearchTimer >= searchDuration)
                {
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

        ConfigureWavesForStep(encounterIndex);

        OnEncounterTriggered?.Invoke();
        StartCoroutine(EncounterIntroRoutine());
    }

    public void AutoConfigureDefaultWavesIfEmpty()
    {
        ConfigureWavesForStep(encounterIndex);
    }

    private static bool IsLevel3Scene()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        return sceneName.IndexOf("Nivel3", StringComparison.OrdinalIgnoreCase) >= 0 ||
               sceneName.IndexOf("Level3", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void ConfigureWavesForStep(int step)
    {
        if (waveManager == null) return;
        bool isLvl3 = IsLevel3Scene();

        if (step == 0)
        {
            dropBigKnifeOnClear = !isLvl3;
            waveManager.waves = isLvl3
                ? new EncounterWaveManager.WaveDefinition[]
                {
                    new EncounterWaveManager.WaveDefinition { waveName = "Oleada 1", normalGoblins = 2, chubbyGoblins = 0, archerGoblins = 1 },
                    new EncounterWaveManager.WaveDefinition { waveName = "Oleada 2", normalGoblins = 2, chubbyGoblins = 1, archerGoblins = 2 }
                }
                : new EncounterWaveManager.WaveDefinition[]
                {
                    new EncounterWaveManager.WaveDefinition { waveName = "Oleada 1", normalGoblins = 3, chubbyGoblins = 0 },
                    new EncounterWaveManager.WaveDefinition { waveName = "Oleada 2", normalGoblins = 3, chubbyGoblins = 1 }
                };
            waveManager.maxSimultaneousEnemies = 4;
        }
        else if (step == 1)
        {
            dropBigKnifeOnClear = false;
            waveManager.waves = isLvl3
                ? new EncounterWaveManager.WaveDefinition[]
                {
                    new EncounterWaveManager.WaveDefinition { waveName = "Oleada 1", normalGoblins = 3, chubbyGoblins = 1, archerGoblins = 2 },
                    new EncounterWaveManager.WaveDefinition { waveName = "Oleada 2", normalGoblins = 3, chubbyGoblins = 2, archerGoblins = 3 }
                }
                : new EncounterWaveManager.WaveDefinition[]
                {
                    new EncounterWaveManager.WaveDefinition { waveName = "Oleada 1", normalGoblins = 3, chubbyGoblins = 2 },
                    new EncounterWaveManager.WaveDefinition { waveName = "Oleada 2", normalGoblins = 4, chubbyGoblins = 3 }
                };
            waveManager.maxSimultaneousEnemies = isLvl3 ? 5 : 4;
        }
        else if (step == 2)
        {
            dropBigKnifeOnClear = false;
            waveManager.waves = isLvl3
                ? new EncounterWaveManager.WaveDefinition[]
                {
                    new EncounterWaveManager.WaveDefinition { waveName = "Vanguardia de la Bruja", normalGoblins = 5, chubbyGoblins = 3, archerGoblins = 4 },
                    new EncounterWaveManager.WaveDefinition { waveName = "Aquelarre del Panteón", normalGoblins = 6, chubbyGoblins = 4, archerGoblins = 4 }
                }
                : new EncounterWaveManager.WaveDefinition[]
                {
                    new EncounterWaveManager.WaveDefinition { waveName = "Vanguardia Espectral", normalGoblins = 8, chubbyGoblins = 4 },
                    new EncounterWaveManager.WaveDefinition { waveName = "Gran Horda del Panteón", normalGoblins = 10, chubbyGoblins = 6 }
                };
            waveManager.maxSimultaneousEnemies = 7;
        }
    }

    private IEnumerator EncounterIntroRoutine()
    {
        if (targetToy != null)
        {
            targetToy.gameObject.SetActive(false);
        }

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
                    "OLEADA 1 DE 2",
                    "Los espíritus del panteón protegen el segundo juguete.\nExpulsa a los duendes con <link=\"key:F\"><color=#00000000> F </color></link> para purificar el altar.",
                    null,
                    null,
                    true
                );
            }
            yield return new WaitForSeconds(1.0f);
        }
        else if (encounterIndex == 2)
        {
            if (forceField != null)
            {
                forceField.DeactivateBarrier();
                forceField.gameObject.SetActive(false);
            }

            if (ParchmentMessageUI.Instance != null)
            {
                bool isLvl3 = IsLevel3Scene();
                ParchmentMessageUI.Instance.Show(
                    isLvl3 ? "HORDA DEL AQUELARRE" : "HORDA ESPECTRAL",
                    isLvl3
                        ? "El panteón despierta bajo el conjuro oscuro.\nDerrota a las 2 hordas con <link=\"key:F\"><color=#00000000> F </color></link> para desafiar a La Bruja."
                        : "El panteón despierta en cólera total.\nDerrota a las 2 hordas con <link=\"key:F\"><color=#00000000> F </color></link> para desafiar al Niño Espectral.",
                    null,
                    null,
                    true
                );
            }
            yield return new WaitForSeconds(1.0f);
        }

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
            StartCoroutine(RestoreToyRoutine());
        }
        else
        {
            StartCoroutine(SpawnBossRoutine());
        }
    }

    private IEnumerator RestoreToyRoutine()
    {
        yield return new WaitForSeconds(0.5f);

        Vector3 toyCenter = targetToy != null ? targetToy.transform.position : (transform.position + new Vector3(0f, 0.55f, 0f));

        // 1. Reaparece el juguete purificado en el altar
        if (targetToy != null)
        {
            targetToy.gameObject.SetActive(true);
        }

        bool isLvl3 = IsLevel3Scene();

        // 2. En el Nivel 2: en el primer juguete aparece también el Cuchillo tirado al lado del juguete.
        //    En el Nivel 3: ya se tiene el Cuchillo y Machete desde el inicio, así que solo se revela el juguete y un orbe de vida.
        if (!isLvl3 && (dropBigKnifeOnClear || encounterIndex == 0))
        {
            Vector3 knifeDropPos = toyCenter + new Vector3(0.82f, 0f, 0f);
            WeaponPickup.SpawnWeaponDrop(WeaponType.BigKnife, knifeDropPos);

            if (ParchmentMessageUI.Instance != null)
            {
                ParchmentMessageUI.Instance.Show(
                    "CUCHILLO Y JUGUETE REVELADOS",
                    "Has derrotado a los soldados de La Llorona.\nRecoge el primer juguete y el Cuchillo junto a él en el altar.\nPulsa 1: Farol (17 DMG) o 2: Cuchillo (28 DMG).",
                    null,
                    null,
                    true
                );
            }
        }
        else if (isLvl3 && encounterIndex == 0)
        {
            HealthPickup.SpawnDrop(toyCenter + new Vector3(0.82f, 0f, 0f), HealthPickup.HealMode.OneHeart);

            if (ParchmentMessageUI.Instance != null)
            {
                ParchmentMessageUI.Instance.Show(
                    "PRIMER JUGUETE REVELADO",
                    "Has derrotado a los soldados del panteón.\nRecoge el primer juguete en el altar.\nRecuerda alternar con 1: Farol, 2: Cuchillo y 3: Machete.",
                    null,
                    null,
                    true
                );
            }
        }
        else
        {
            if (ParchmentMessageUI.Instance != null)
            {
                ParchmentMessageUI.Instance.Show(
                    "ALTAR PURIFICADO",
                    "Las sombras del panteón retroceden.\nYa puedes recoger el segundo juguete en el altar.",
                    null,
                    null,
                    true
                );
            }
        }

        GlobalEncountersCompleted++;
        IsAnyEncounterActive = false;
        OnEncounterCleared?.Invoke();
    }

    private IEnumerator SpawnBossRoutine()
    {
        // Aparecer en el centro despejado del Altar 3 (nunca abajo cerca de las tumbas)
        Vector3 altarCenter = targetToy != null
            ? targetToy.transform.position
            : transform.position + new Vector3(0f, 0.65f, 0f);
        Vector3 spawnPos = altarCenter + new Vector3(0f, 0.45f, 0f);

        bool bossParchmentDone = false;
        bool isLvl3 = IsLevel3Scene();
        if (ParchmentMessageUI.Instance != null)
        {
            ParchmentMessageUI.Instance.Show(
                isLvl3 ? "LA BRUJA" : "NIÑO ESPECTRAL",
                isLvl3
                    ? "«¡Mis hechizos y sombras te consumirán!\n¡Jamás cruzarás la puerta del norte!»"
                    : "«La Llorona me envió...\n¡Nunca saldrás con vida de este panteón!»",
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

        yield return new WaitForSeconds(0.25f);

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

        EnemyHealth bossHealth = bossInstance.GetComponent<EnemyHealth>();
        if (bossHealth == null) bossHealth = bossInstance.AddComponent<EnemyHealth>();
        bossHealth.variant = EnemyHealth.EnemyVariant.SpectralChildBoss;
        bossHealth.maxHealth = 250;
        bossHealth.currentHealth = 250;

        SpectralChildBossAI bossAI = bossInstance.GetComponent<SpectralChildBossAI>();
        if (bossAI == null) bossAI = bossInstance.AddComponent<SpectralChildBossAI>();

        var oldAi = bossInstance.GetComponent<GhostChildAI>();
        if (oldAi != null)
        {
            oldAi.enabled = false;
            Destroy(oldAi);
        }

        bossHealth.OnDied += _ =>
        {
            if (!bossDefeatSequenceStarted)
            {
                bossDefeatSequenceStarted = true;
                StartCoroutine(BossDefeatedCinematicRoutine(bossAI));
            }
        };
    }

    private IEnumerator BossDefeatedCinematicRoutine(SpectralChildBossAI bossAI)
    {
        if (forceField != null)
        {
            forceField.DeactivateBarrier();
        }

        // Pausar movimiento del jugador para observar la cinemática donde el Niño Espectral desaparece lentamente
        Cainos.PixelArtTopDown_Basic.TopDownCharacterController playerController = null;
        Animator playerAnim = null;
        if (playerTransform != null)
        {
            playerController = playerTransform.GetComponent<Cainos.PixelArtTopDown_Basic.TopDownCharacterController>();
            playerAnim = playerTransform.GetComponent<Animator>();
            if (playerController != null) playerController.enabled = false;
            if (playerBody != null) playerBody.velocity = Vector2.zero;
            if (playerAnim != null) playerAnim.SetBool("IsMoving", false);
        }

        bool bossFinishedVanishing = false;
        if (bossAI != null)
        {
            bossAI.OnDeathCinematicFinished += _ => { bossFinishedVanishing = true; };
        }
        else
        {
            bossFinishedVanishing = true;
        }

        // Esperar a que termine la animación lenta de desaparición (máximo 2.6s de respaldo)
        float waitTimer = 0f;
        while (!bossFinishedVanishing && bossAI != null && waitTimer < 2.6f)
        {
            waitTimer += Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.2f);

        // Restaurar el control del jugador
        if (playerController != null) playerController.enabled = true;

        // Aparecer el último juguete en el altar y el Orbe de Vida Completa (y el Machete en Nivel 2)
        bool isLvl3 = IsLevel3Scene();
        Vector3 toyCenter = targetToy != null ? targetToy.transform.position : (transform.position + new Vector3(0f, 0.55f, 0f));
        if (targetToy != null)
        {
            targetToy.gameObject.SetActive(true);
        }

        if (!isLvl3)
        {
            WeaponPickup.SpawnWeaponDrop(WeaponType.Machete, toyCenter + new Vector3(0.85f, 0f, 0f));
        }
        HealthPickup.SpawnDrop(toyCenter + new Vector3(-0.85f, 0f, 0f), HealthPickup.HealMode.FullHealth);

        GlobalEncountersCompleted++;
        IsAnyEncounterActive = false;
        OnEncounterCleared?.Invoke();

        // Mostrar el diálogo de derrota del Jefe después de que desapareció lentamente
        if (ParchmentMessageUI.Instance != null)
        {
            ParchmentMessageUI.Instance.Show(
                isLvl3 ? "¡LA BRUJA DERROTADA!" : "¡NIÑO ESPECTRAL DERROTADO!",
                isLvl3
                    ? "La Bruja del Panteón se ha desvanecido.\nRecoge el último juguete en el altar para abrir la puerta superior y escapar del panteón."
                    : "El espíritu del Niño Espectral se ha desvanecido.\nRecoge el último juguete y el Machete (50 DMG) en el altar para abrir la puerta derecha y escapar del panteón.",
                null,
                null,
                true
            );
        }
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
