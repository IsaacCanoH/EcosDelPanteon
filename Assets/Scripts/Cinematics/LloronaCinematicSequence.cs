using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LloronaCinematicSequence : MonoBehaviour
{
    [Header("Configuración Visual de La Llorona")]
    public Sprite lloronaSprite;
    public Color spectralColor = new Color(0.82f, 0.96f, 1.0f, 0.94f);
    public float appearDuration = 1.25f;
    public float vanishDuration = 1.15f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip wailClip;

    private static Sprite[] animatedLloronaFrames;
    private static Sprite softAuraSprite;

    public void PlayEncounter1Cinematic(Vector3 spawnCenter, Action onComplete)
    {
        StartCoroutine(SequenceRoutine(spawnCenter, onComplete));
    }

    public void PlayLevel3GreenAuraIntroCinematic(Vector3 playerPos, Action onComplete)
    {
        StartCoroutine(Level3GreenAuraIntroRoutine(playerPos, onComplete));
    }

    private IEnumerator Level3GreenAuraIntroRoutine(Vector3 playerPos, Action onComplete)
    {
        EnsureAnimatedFrames();

        // 1. Pausar control del jugador durante la cinemática inicial
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Cainos.PixelArtTopDown_Basic.TopDownCharacterController playerController = null;
        Cainos.PixelArtTopDown_Basic.Level2CharacterController l2Controller = null;
        Rigidbody2D playerRb = null;
        Animator playerAnim = null;

        if (player != null)
        {
            playerController = player.GetComponent<Cainos.PixelArtTopDown_Basic.TopDownCharacterController>();
            l2Controller = player.GetComponent<Cainos.PixelArtTopDown_Basic.Level2CharacterController>();
            playerRb = player.GetComponent<Rigidbody2D>();
            playerAnim = player.GetComponent<Animator>();

            if (playerController != null) playerController.enabled = false;
            if (l2Controller != null) l2Controller.enabled = false;
            if (playerRb != null) playerRb.velocity = Vector2.zero;
            if (playerAnim != null)
            {
                playerAnim.SetBool("IsMoving", false);
                playerAnim.SetInteger("Direction", 2); // Mirando hacia la derecha donde aparece La Llorona
            }
        }

        // 2. Instanciar a La Llorona frente al jugador en la entrada izquierda
        Vector3 basePos = playerPos + new Vector3(2.85f, 0.25f, 0f);
        GameObject ghostRoot = new GameObject("LaLlorona_Level3Intro");
        ghostRoot.transform.position = basePos + new Vector3(0f, 0.65f, 0f);

        GameObject auraObj = new GameObject("SpectralAura");
        auraObj.transform.SetParent(ghostRoot.transform, false);
        auraObj.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        SpriteRenderer auraSr = auraObj.AddComponent<SpriteRenderer>();
        auraSr.sprite = GetOrCreateSoftAuraSprite();
        auraSr.sortingLayerName = "Layer 2";
        auraSr.sortingOrder = 18;
        auraSr.color = new Color(0.25f, 0.85f, 1f, 0f);
        auraObj.transform.localScale = new Vector3(2.4f, 3.0f, 1f);

        GameObject echoObj = new GameObject("SpectralEcho");
        echoObj.transform.SetParent(ghostRoot.transform, false);
        echoObj.transform.localPosition = Vector3.zero;
        SpriteRenderer echoSr = echoObj.AddComponent<SpriteRenderer>();
        echoSr.sprite = animatedLloronaFrames[0];
        echoSr.sortingLayerName = "Layer 2";
        echoSr.sortingOrder = 19;
        echoSr.color = new Color(0.35f, 0.9f, 1f, 0f);
        echoObj.transform.localScale = new Vector3(1.12f, 1.12f, 1f);

        GameObject bodyObj = new GameObject("SpectralBody");
        bodyObj.transform.SetParent(ghostRoot.transform, false);
        bodyObj.transform.localPosition = Vector3.zero;
        SpriteRenderer sr = bodyObj.AddComponent<SpriteRenderer>();
        sr.sprite = animatedLloronaFrames[0];
        sr.sortingLayerName = "Layer 2";
        sr.sortingOrder = 20;
        sr.color = new Color(spectralColor.r, spectralColor.g, spectralColor.b, 0f);

        List<Transform> mistParticles = new List<Transform>();
        List<SpriteRenderer> mistRenderers = new List<SpriteRenderer>();
        for (int i = 0; i < 6; i++)
        {
            GameObject mist = new GameObject($"LloronaMist_{i}");
            mist.transform.SetParent(ghostRoot.transform, false);
            mist.transform.localPosition = new Vector3((i - 2.5f) * 0.28f, 0.2f + (i % 3) * 0.35f, 0f);
            mist.transform.localScale = Vector3.one * (0.45f + (i % 2) * 0.2f);
            SpriteRenderer msr = mist.AddComponent<SpriteRenderer>();
            msr.sprite = GetOrCreateSoftAuraSprite();
            msr.sortingLayerName = "Layer 2";
            msr.sortingOrder = 21;
            msr.color = new Color(0.6f, 0.95f, 1f, 0f);
            mistParticles.Add(mist.transform);
            mistRenderers.Add(msr);
        }

        if (wailClip == null)
        {
#if UNITY_EDITOR
            wailClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/GhostChild/ghost_overcharge.wav");
#endif
        }
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        if (wailClip != null)
        {
            audioSource.PlayOneShot(wailClip, 0.85f * AudioManager.EnemyVolume);
        }

        // 3. Aparición de La Llorona
        float elapsed = 0f;
        while (elapsed < appearDuration)
        {
            float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
            elapsed += dt;
            float p = Mathf.Clamp01(elapsed / appearDuration);
            float smoothP = Mathf.SmoothStep(0f, 1f, p);

            UpdateGhostAnimation(ghostRoot.transform, sr, echoSr, auraSr, mistParticles, mistRenderers, basePos, smoothP, elapsed);
            ghostRoot.transform.position += new Vector3(0f, Mathf.Lerp(0.65f, 0f, smoothP), 0f);
            yield return null;
        }

        // 4. Diálogo 1 (avanza con P): La Llorona expresa su furia por la derrota del Espectro del Niño
        bool firstDialogClosed = false;
        if (ParchmentMessageUI.Instance != null)
        {
            ParchmentMessageUI.Instance.Show(
                "LA LLORONA",
                "«¡Estoy muy molesta contigo!\n¡Has derrotado a uno de mis sirvientes más fuertes, el Espectro del Niño!»",
                null,
                () => { firstDialogClosed = true; },
                true
            );
        }
        else
        {
            firstDialogClosed = true;
        }

        while (!firstDialogClosed)
        {
            float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
            elapsed += dt;
            UpdateGhostAnimation(ghostRoot.transform, sr, echoSr, auraSr, mistParticles, mistRenderers, basePos, 1f, elapsed);
            yield return null;
        }

        // 5. Después de presionar P: aparecen 3 duendes reales (Base, Fuerte y Arquero) que al terminar la cinemática se quedarán en el mapa para luchar
        List<GameObject> demoGoblins = new List<GameObject>();
        Vector3[] goblinOffsets = new Vector3[]
        {
            new Vector3(1.15f,  0.65f, 0f), // 0: Duende Base
            new Vector3(1.85f, -0.25f, 0f), // 1: Duende Fuerte
            new Vector3(1.15f, -1.05f, 0f)  // 2: Duende Arquero Blindado
        };

        GameObject goblinTemplate = null;
        GoblinAI anySceneGoblin = FindObjectOfType<GoblinAI>(false);
        if (anySceneGoblin != null)
        {
            goblinTemplate = anySceneGoblin.gameObject;
        }
#if UNITY_EDITOR
        if (goblinTemplate == null)
            goblinTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/PF_Goblin.prefab");
#endif
        if (goblinTemplate == null)
            goblinTemplate = Resources.Load<GameObject>("Prefabs/Enemies/PF_Goblin");

        for (int i = 0; i < goblinOffsets.Length; i++)
        {
            Vector3 spawnPos = basePos + goblinOffsets[i];
            GameObject realGoblin;
            if (goblinTemplate != null)
            {
                realGoblin = Instantiate(goblinTemplate, spawnPos, Quaternion.identity);
            }
            else
            {
                realGoblin = new GameObject($"Goblin_Encantado_Intro_{i + 1}");
                realGoblin.transform.position = spawnPos;
                realGoblin.AddComponent<SpriteRenderer>();
            }

            realGoblin.name = $"Goblin_Encantado_Llorona_{i + 1}";
            realGoblin.SetActive(true);

            // Configurar inicialmente SIN aura verde para que el jugador vea cómo el hechizo de La Llorona se las activa
            EnemyHealth eh = realGoblin.GetComponent<EnemyHealth>();
            if (eh == null) eh = realGoblin.AddComponent<EnemyHealth>();

            GoblinVariantConfig cfg = realGoblin.GetComponent<GoblinVariantConfig>();
            if (cfg == null) cfg = realGoblin.AddComponent<GoblinVariantConfig>();

            bool isChubby = (i == 1);
            bool isArcher = (i == 2);
            cfg.SetVariant(isChubby, isArcher, false);

            // Pausar su IA durante la cinemática para que permanezcan frente a La Llorona hasta que termine
            GoblinAI ai = realGoblin.GetComponent<GoblinAI>();
            if (ai != null) ai.enabled = false;
            GoblinArcherController archerCtrl = realGoblin.GetComponent<GoblinArcherController>();
            if (archerCtrl != null) archerCtrl.enabled = false;
            Rigidbody2D gRb = realGoblin.GetComponent<Rigidbody2D>();
            if (gRb != null) gRb.velocity = Vector2.zero;

            realGoblin.transform.localScale = Vector3.zero;
            demoGoblins.Add(realGoblin);
        }

        // Animación de aparición de los 3 duendes (0.55s)
        float spawnTimer = 0f;
        while (spawnTimer < 0.55f)
        {
            float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
            spawnTimer += dt;
            elapsed += dt;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(spawnTimer / 0.55f));

            UpdateGhostAnimation(ghostRoot.transform, sr, echoSr, auraSr, mistParticles, mistRenderers, basePos, 1f, elapsed);
            for (int i = 0; i < demoGoblins.Count; i++)
            {
                if (demoGoblins[i] == null) continue;
                Vector3 targetScale = (i == 1) ? new Vector3(1.26f, 1.15f, 1f) : ((i == 2) ? new Vector3(1.10f, 1.10f, 1f) : Vector3.one);
                demoGoblins[i].transform.localScale = targetScale * p;
            }
            yield return null;
        }

        // 6. La Llorona lanza un hechizo esmeralda hacia los duendes y les enciende el Aura Verde
        if (wailClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(wailClip, 0.9f * AudioManager.EnemyVolume);
        }

        List<GameObject> spellBolts = new List<GameObject>();
        for (int i = 0; i < demoGoblins.Count; i++)
        {
            GameObject bolt = new GameObject($"SpellBolt_Green_{i}");
            bolt.transform.position = basePos + new Vector3(0f, 0.85f, 0f);
            bolt.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
            SpriteRenderer bSr = bolt.AddComponent<SpriteRenderer>();
            bSr.sprite = GetOrCreateSoftAuraSprite();
            bSr.sortingLayerName = "Layer 2";
            bSr.sortingOrder = 25;
            bSr.color = new Color(0.15f, 1.0f, 0.45f, 1f);
            spellBolts.Add(bolt);
        }

        float spellTimer = 0f;
        float spellDuration = 0.65f;
        Vector3 spellOrigin = basePos + new Vector3(0f, 0.85f, 0f);
        while (spellTimer < spellDuration)
        {
            float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
            spellTimer += dt;
            elapsed += dt;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(spellTimer / spellDuration));

            UpdateGhostAnimation(ghostRoot.transform, sr, echoSr, auraSr, mistParticles, mistRenderers, basePos, 1f, elapsed);
            // El aura de La Llorona brilla en verde intenso mientras lanza el conjuro
            if (auraSr != null)
            {
                auraSr.color = Color.Lerp(new Color(0.25f, 0.85f, 1f, 0.7f), new Color(0.15f, 1.0f, 0.45f, 0.95f), Mathf.Sin(p * Mathf.PI));
            }

            for (int i = 0; i < spellBolts.Count; i++)
            {
                if (demoGoblins[i] == null) continue;
                Vector3 targetPos = demoGoblins[i].transform.position + new Vector3(0f, 0.35f, 0f);
                Vector3 arcOffset = new Vector3(0f, Mathf.Sin(p * Mathf.PI) * 0.45f, 0f);
                spellBolts[i].transform.position = Vector3.Lerp(spellOrigin, targetPos, p) + arcOffset;
                float pulse = 0.55f + Mathf.Sin(p * Mathf.PI) * 0.45f;
                spellBolts[i].transform.localScale = new Vector3(pulse, pulse, 1f);
            }
            yield return null;
        }

        foreach (var b in spellBolts)
        {
            if (b != null) Destroy(b);
        }

        // Encender el Aura Verde mejorada y aumentar la resistencia real de los 3 duendes encantados
        for (int i = 0; i < demoGoblins.Count; i++)
        {
            if (demoGoblins[i] == null) continue;
            GoblinVariantConfig cfg = demoGoblins[i].GetComponent<GoblinVariantConfig>();
            if (cfg != null)
            {
                bool isChubby = (i == 1);
                bool isArcher = (i == 2);
                cfg.SetVariant(isChubby, isArcher, true);
            }
        }

        // Pausa breve (0.55s) para que el jugador vea encenderse el aura verde antes del segundo diálogo
        float postSpellWait = 0f;
        while (postSpellWait < 0.55f)
        {
            float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
            postSpellWait += dt;
            elapsed += dt;
            UpdateGhostAnimation(ghostRoot.transform, sr, echoSr, auraSr, mistParticles, mistRenderers, basePos, 1f, elapsed);
            yield return null;
        }

        // 7. Diálogo 2 (avanza con P): La Llorona anuncia que ahora sus mejores guerreros serán más fuertes que antes
        bool secondDialogClosed = false;
        if (ParchmentMessageUI.Instance != null)
        {
            ParchmentMessageUI.Instance.Show(
                "LA LLORONA",
                "«¡Con este hechizo maldito, ahora mis mejores guerreros serán más fuertes y resistentes que antes!»",
                null,
                () => { secondDialogClosed = true; },
                true
            );
        }
        else
        {
            secondDialogClosed = true;
        }

        while (!secondDialogClosed)
        {
            float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
            elapsed += dt;
            UpdateGhostAnimation(ghostRoot.transform, sr, echoSr, auraSr, mistParticles, mistRenderers, basePos, 1f, elapsed);
            yield return null;
        }

        // 8. Desvanecer únicamente a La Llorona; los 3 duendes encantados SE QUEDAN EN EL MAPA para luchar
        float vanishElapsed = 0f;
        while (vanishElapsed < vanishDuration)
        {
            float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
            vanishElapsed += dt;
            elapsed += dt;
            float p = Mathf.Clamp01(vanishElapsed / vanishDuration);
            float alphaFactor = 1f - Mathf.SmoothStep(0f, 1f, p);

            UpdateGhostAnimation(ghostRoot.transform, sr, echoSr, auraSr, mistParticles, mistRenderers, basePos, alphaFactor, elapsed);
            ghostRoot.transform.position += new Vector3(0f, p * 0.85f, 0f);
            yield return null;
        }

        Destroy(ghostRoot);

        // Activar la IA de los 3 duendes encantados para que se queden luchando en el mapa
        GoblinAwarenessUI awarenessUI = FindObjectOfType<GoblinAwarenessUI>();
        for (int i = 0; i < demoGoblins.Count; i++)
        {
            GameObject gob = demoGoblins[i];
            if (gob == null) continue;

            GoblinArcherController archerCtrl = gob.GetComponent<GoblinArcherController>();
            if (archerCtrl != null) archerCtrl.enabled = true;

            GoblinAI ai = gob.GetComponent<GoblinAI>();
            if (ai != null)
            {
                ai.waitForFirstCoin = false;
                ai.enablePatrol = true;
                ai.patrolRadius = 2.8f;
                if (i == 2 && archerCtrl != null)
                {
                    ai.moveSpeed = 1.85f;
                    ai.patrolSpeed = 1.15f;
                    ai.secondCoinSpeed = 2.40f;
                    ai.stopDistance = archerCtrl.idealRangedStopDistance;
                    ai.attackRange = 0.25f;
                    ai.enableLungeAttack = false;
                }
                ai.enabled = true;
                if (awarenessUI != null) awarenessUI.Track(ai);
            }
        }

        // 9. Restaurar control del jugador
        if (playerController != null) playerController.enabled = true;
        if (l2Controller != null) l2Controller.enabled = true;

        onComplete?.Invoke();
    }

    private IEnumerator SequenceRoutine(Vector3 spawnCenter, Action onComplete)
    {
        EnsureAnimatedFrames();

        // 1. Pausar control del jugador durante la aparición
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Cainos.PixelArtTopDown_Basic.TopDownCharacterController playerController = null;
        Rigidbody2D playerRb = null;
        Animator playerAnim = null;

        if (player != null)
        {
            playerController = player.GetComponent<Cainos.PixelArtTopDown_Basic.TopDownCharacterController>();
            playerRb = player.GetComponent<Rigidbody2D>();
            playerAnim = player.GetComponent<Animator>();

            if (playerController != null) playerController.enabled = false;
            if (playerRb != null) playerRb.velocity = Vector2.zero;
            if (playerAnim != null) playerAnim.SetBool("IsMoving", false);
        }

        // 2. Instanciar a La Llorona con múltiples capas espectrales (Aura, Velo Exterior, Cuerpo Animado y Niebla)
        Vector3 basePos = spawnCenter + new Vector3(0f, 1.05f, 0f);
        GameObject ghostRoot = new GameObject("LaLlorona_Cinematic");
        ghostRoot.transform.position = basePos + new Vector3(0f, 0.65f, 0f);

        // Halo espectral trasero
        GameObject auraObj = new GameObject("SpectralAura");
        auraObj.transform.SetParent(ghostRoot.transform, false);
        auraObj.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        SpriteRenderer auraSr = auraObj.AddComponent<SpriteRenderer>();
        auraSr.sprite = GetOrCreateSoftAuraSprite();
        auraSr.sortingLayerName = "Layer 2";
        auraSr.sortingOrder = 18;
        auraSr.color = new Color(0.25f, 0.85f, 1f, 0f);
        auraObj.transform.localScale = new Vector3(2.4f, 3.0f, 1f);

        // Sombra/eco etéreo secundario que se desfasa suavemente
        GameObject echoObj = new GameObject("SpectralEcho");
        echoObj.transform.SetParent(ghostRoot.transform, false);
        echoObj.transform.localPosition = Vector3.zero;
        SpriteRenderer echoSr = echoObj.AddComponent<SpriteRenderer>();
        echoSr.sprite = animatedLloronaFrames[0];
        echoSr.sortingLayerName = "Layer 2";
        echoSr.sortingOrder = 19;
        echoSr.color = new Color(0.35f, 0.9f, 1f, 0f);
        echoObj.transform.localScale = new Vector3(1.12f, 1.12f, 1f);

        // Cuerpo principal animado (4 frames de vestido ondeante, cabello y lágrimas espectrales)
        GameObject bodyObj = new GameObject("SpectralBody");
        bodyObj.transform.SetParent(ghostRoot.transform, false);
        bodyObj.transform.localPosition = Vector3.zero;
        SpriteRenderer sr = bodyObj.AddComponent<SpriteRenderer>();
        sr.sprite = animatedLloronaFrames[0];
        sr.sortingLayerName = "Layer 2";
        sr.sortingOrder = 20;
        sr.color = new Color(spectralColor.r, spectralColor.g, spectralColor.b, 0f);

        // Partículas de niebla y almas alrededor de La Llorona
        List<Transform> mistParticles = new List<Transform>();
        List<SpriteRenderer> mistRenderers = new List<SpriteRenderer>();
        for (int i = 0; i < 6; i++)
        {
            GameObject mist = new GameObject($"LloronaMist_{i}");
            mist.transform.SetParent(ghostRoot.transform, false);
            mist.transform.localPosition = new Vector3((i - 2.5f) * 0.28f, 0.2f + (i % 3) * 0.35f, 0f);
            mist.transform.localScale = Vector3.one * (0.45f + (i % 2) * 0.2f);
            SpriteRenderer msr = mist.AddComponent<SpriteRenderer>();
            msr.sprite = GetOrCreateSoftAuraSprite();
            msr.sortingLayerName = "Layer 2";
            msr.sortingOrder = 21;
            msr.color = new Color(0.6f, 0.95f, 1f, 0f);
            mistParticles.Add(mist.transform);
            mistRenderers.Add(msr);
        }

        // 3. Reproducir lamento de La Llorona
        if (wailClip == null)
        {
#if UNITY_EDITOR
            wailClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/GhostChild/ghost_overcharge.wav");
#endif
        }
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        if (wailClip != null)
        {
            audioSource.PlayOneShot(wailClip, 0.85f * AudioManager.EnemyVolume);
        }

        // 4. Fase 1: Aparición descendiendo entre niebla espectral
        float elapsed = 0f;
        while (elapsed < appearDuration)
        {
            float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
            elapsed += dt;
            float p = Mathf.Clamp01(elapsed / appearDuration);
            float smoothP = Mathf.SmoothStep(0f, 1f, p);

            UpdateGhostAnimation(ghostRoot.transform, sr, echoSr, auraSr, mistParticles, mistRenderers, basePos, smoothP, elapsed);
            ghostRoot.transform.position += new Vector3(0f, Mathf.Lerp(0.65f, 0f, smoothP), 0f);
            yield return null;
        }

        // 5. Mostrar diálogo en pergamino mientras La Llorona sigue levitando y animándose con Time.unscaledTime
        bool parchmentClosed = false;
        if (ParchmentMessageUI.Instance != null)
        {
            ParchmentMessageUI.Instance.Show(
                "LA LLORONA",
                "«¡Aaaay, mis hijos...!\n¡Nadie me arrebatará sus recuerdos!\n¡Mis soldados te detendrán!»",
                null,
                () => { parchmentClosed = true; },
                true
            );
        }
        else
        {
            parchmentClosed = true;
        }

        while (!parchmentClosed)
        {
            float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
            elapsed += dt;
            UpdateGhostAnimation(ghostRoot.transform, sr, echoSr, auraSr, mistParticles, mistRenderers, basePos, 1f, elapsed);
            yield return null;
        }

        // 6. Fase 3: Desvanecimiento ascendente disolviéndose en volutas de niebla
        float vanishElapsed = 0f;
        while (vanishElapsed < vanishDuration)
        {
            float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
            vanishElapsed += dt;
            elapsed += dt;
            float p = Mathf.Clamp01(vanishElapsed / vanishDuration);
            float alphaFactor = 1f - Mathf.SmoothStep(0f, 1f, p);

            UpdateGhostAnimation(ghostRoot.transform, sr, echoSr, auraSr, mistParticles, mistRenderers, basePos, alphaFactor, elapsed);
            ghostRoot.transform.position += new Vector3(0f, p * 0.85f, 0f);
            ghostRoot.transform.localScale = new Vector3(Mathf.Lerp(1f, 0.78f, p), Mathf.Lerp(1f, 1.32f, p), 1f);
            yield return null;
        }

        Destroy(ghostRoot);

        // 7. Restaurar control del jugador
        if (playerController != null) playerController.enabled = true;

        onComplete?.Invoke();
    }

    private void UpdateGhostAnimation(
        Transform root,
        SpriteRenderer bodySr,
        SpriteRenderer echoSr,
        SpriteRenderer auraSr,
        List<Transform> mists,
        List<SpriteRenderer> mistRenderers,
        Vector3 basePos,
        float alphaMultiplier,
        float time)
    {
        if (root == null) return;

        // Frame de animación de sprite (8 FPS para ondeo fluido del vestido, velo y lágrimas)
        int frameIdx = Mathf.FloorToInt(time * 8f) % animatedLloronaFrames.Length;
        int echoFrameIdx = (frameIdx + 2) % animatedLloronaFrames.Length;

        if (bodySr != null)
        {
            bodySr.sprite = animatedLloronaFrames[frameIdx];
            bodySr.color = new Color(spectralColor.r, spectralColor.g, spectralColor.b, spectralColor.a * alphaMultiplier);
        }

        if (echoSr != null)
        {
            echoSr.sprite = animatedLloronaFrames[echoFrameIdx];
            echoSr.color = new Color(0.35f, 0.92f, 1f, 0.38f * alphaMultiplier);
            echoSr.transform.localPosition = new Vector3(Mathf.Sin(time * 3.5f) * 0.08f, Mathf.Cos(time * 2.8f) * 0.06f, 0f);
        }

        if (auraSr != null)
        {
            float pulse = 0.42f + Mathf.Sin(time * 4.2f) * 0.14f;
            auraSr.color = new Color(0.25f, 0.85f, 1f, pulse * alphaMultiplier);
            float scalePulse = 1f + Mathf.Sin(time * 3.0f) * 0.08f;
            auraSr.transform.localScale = new Vector3(2.4f * scalePulse, 3.0f * scalePulse, 1f);
        }

        // Levitación en ocho (figure-8) con ligero balanceo espectral
        float swayX = Mathf.Sin(time * 2.1f) * 0.14f;
        float bobY = Mathf.Sin(time * 3.6f) * 0.12f;
        root.position = basePos + new Vector3(swayX, bobY, 0f);
        root.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 2.1f) * 3.5f);

        for (int i = 0; i < mists.Count; i++)
        {
            if (mists[i] == null) continue;
            float angle = time * 1.8f + i * (Mathf.PI * 2f / mists.Count);
            float rx = Mathf.Cos(angle) * 0.48f;
            float ry = 0.15f + ((time * 0.45f + i * 0.22f) % 1.35f);
            mists[i].localPosition = new Vector3(rx, ry, 0f);
            float mistAlpha = Mathf.Sin(Mathf.Clamp01(ry / 1.35f) * Mathf.PI) * 0.55f * alphaMultiplier;
            mistRenderers[i].color = new Color(0.65f, 0.96f, 1f, mistAlpha);
        }
    }

    private static void EnsureAnimatedFrames()
    {
        if (animatedLloronaFrames != null && animatedLloronaFrames.Length == 4 && animatedLloronaFrames[0] != null)
            return;

        animatedLloronaFrames = new Sprite[4];
        for (int f = 0; f < 4; f++)
        {
            animatedLloronaFrames[f] = CreateDetailedLloronaFrame(f);
        }
    }

    private static Sprite CreateDetailedLloronaFrame(int frame)
    {
        int w = 48;
        int h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, Color.clear);

        Color hairDark = new Color(0.08f, 0.13f, 0.22f, 0.96f);
        Color hairHighlight = new Color(0.16f, 0.26f, 0.40f, 0.95f);
        Color facePale = new Color(0.78f, 0.92f, 0.98f, 0.96f);
        Color gownLight = new Color(0.86f, 0.97f, 1.0f, 0.92f);
        Color gownMid = new Color(0.62f, 0.86f, 0.96f, 0.88f);
        Color gownShadow = new Color(0.38f, 0.68f, 0.86f, 0.82f);
        Color veilColor = new Color(0.75f, 0.95f, 1.0f, 0.52f);
        Color eyeGlow = new Color(0.35f, 1.0f, 1.0f, 1.0f);
        Color tearGlow = new Color(0.25f, 0.92f, 1.0f, 0.92f);
        Color darkMouth = new Color(0.06f, 0.12f, 0.20f, 0.95f);

        int cx = w / 2; // 24
        float phase = frame * (Mathf.PI * 0.5f);

        // 1. Vestido largo espectral con pliegues y bordes desgarrados que ondean según el frame
        for (int y = 6; y <= 44; y++)
        {
            float t = (y - 6f) / 38f; // 0 abajo, 1 en los hombros
            float halfWidth = Mathf.Lerp(13.5f, 5.5f, Mathf.Pow(t, 0.75f));
            float waveOffset = Mathf.Sin(phase + y * 0.22f) * Mathf.Lerp(2.2f, 0.2f, t);

            // Borde inferior desgarrado en jirones etéreos
            int minX = Mathf.RoundToInt(cx + waveOffset - halfWidth);
            int maxX = Mathf.RoundToInt(cx + waveOffset + halfWidth);

            for (int x = minX; x <= maxX; x++)
            {
                if (x < 1 || x >= w - 1) continue;

                // Recorte de jirones en la falda baja (y < 15)
                float pleat = Mathf.Sin((x - cx) * 0.75f + phase);
                int bottomCut = 6 + Mathf.RoundToInt((Mathf.Sin(x * 0.9f + frame) + 1f) * 2.5f);
                if (y < bottomCut) continue;

                float edgeDist = Mathf.Abs(x - (cx + waveOffset)) / Mathf.Max(1f, halfWidth);
                Color c = edgeDist > 0.72f ? gownShadow : (pleat > 0.25f ? gownLight : gownMid);

                // Desvanecimiento suave hacia los pies
                if (y < 14) c.a *= Mathf.Clamp01((y - 5f) / 9f);
                tex.SetPixel(x, y, c);
            }
        }

        // 2. Mangas largas flotantes y manos pálidas
        int armOffsetY = (frame == 1 || frame == 2) ? 1 : 0;
        for (int side = -1; side <= 1; side += 2)
        {
            for (int s = 0; s < 10; s++)
            {
                int ax = cx + side * (6 + s / 2);
                int ay = 39 - s / 2 + armOffsetY;
                for (int dy = -3; dy <= 1; dy++)
                {
                    Color sc = dy == -3 ? gownShadow : gownMid;
                    sc.a *= 0.85f;
                    tex.SetPixel(ax, ay + dy, sc);
                }
            }
            // Manos espectrales cerca del rostro/pecho
            tex.SetPixel(cx + side * 5, 42 + armOffsetY, facePale);
            tex.SetPixel(cx + side * 6, 42 + armOffsetY, facePale);
        }

        // 3. Cabello largo oscuro cayendo sobre los hombros y ondeando
        for (int y = 30; y <= 55; y++)
        {
            float ht = (y - 30f) / 25f;
            float hWidth = y >= 45 ? Mathf.Lerp(7.5f, 5.0f, (y - 45f) / 10f) : Mathf.Lerp(6.5f, 8.0f, ht);
            float hairWave = Mathf.Sin(phase + y * 0.25f) * (y < 44 ? 1.2f : 0.3f);

            for (int x = Mathf.RoundToInt(cx - hWidth + hairWave); x <= Mathf.RoundToInt(cx + hWidth + hairWave); x++)
            {
                if (x < 1 || x >= w - 1) continue;
                // Dejar hueco para el torso central debajo del cuello
                if (y < 43 && Mathf.Abs(x - cx) <= 3) continue;
                Color hc = ((x + y) % 5 == 0) ? hairHighlight : hairDark;
                tex.SetPixel(x, y, hc);
            }
        }

        // 4. Rostro pálido de La Llorona
        for (int y = 44; y <= 52; y++)
        {
            int rw = (y == 44 || y == 52) ? 3 : 4;
            for (int x = cx - rw; x <= cx + rw; x++)
            {
                tex.SetPixel(x, y, facePale);
            }
        }

        // 5. Ojos brillantes cian y lágrimas animadas cayendo por las mejillas
        tex.SetPixel(cx - 2, 49, eyeGlow);
        tex.SetPixel(cx + 2, 49, eyeGlow);

        // Boca en expresión de lamento ("¡Aaaay mis hijos!")
        tex.SetPixel(cx, 46, darkMouth);
        tex.SetPixel(cx - 1, 46, darkMouth);
        if (frame % 2 == 0) tex.SetPixel(cx, 45, darkMouth);

        // Lágrimas espectrales animadas
        for (int tStep = 0; tStep < 4; tStep++)
        {
            int ty = 48 - ((tStep + frame) % 5);
            if (ty >= 44)
            {
                tex.SetPixel(cx - 2, ty, tearGlow);
                tex.SetPixel(cx + 2, ty, tearGlow);
            }
        }

        // 6. Velo nupcial translúcido sobre la cabeza y hombros
        for (int y = 38; y <= 57; y++)
        {
            float vt = (y - 38f) / 19f;
            float vWidth = Mathf.Lerp(10.5f, 4.5f, vt * vt);
            float vWave = Mathf.Sin(phase + y * 0.2f) * 0.8f;
            int minVX = Mathf.RoundToInt(cx - vWidth + vWave);
            int maxVX = Mathf.RoundToInt(cx + vWidth + vWave);

            for (int x = minVX; x <= maxVX; x++)
            {
                if (x < 1 || x >= w - 1) continue;
                bool isBorder = (x == minVX || x == maxVX || y >= 56);
                if (isBorder || y >= 53)
                {
                    Color existing = tex.GetPixel(x, y);
                    Color blended = Color.Lerp(existing, veilColor, isBorder ? 0.75f : 0.4f);
                    blended.a = Mathf.Max(existing.a, veilColor.a);
                    tex.SetPixel(x, y, blended);
                }
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.10f), 32f);
    }

    private static Sprite GetOrCreateSoftAuraSprite()
    {
        if (softAuraSprite != null) return softAuraSprite;

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
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }
        tex.Apply();
        softAuraSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 24f);
        return softAuraSprite;
    }
}
