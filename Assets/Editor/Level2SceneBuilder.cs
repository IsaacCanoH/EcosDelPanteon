using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public static class Level2SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Cementerio_Nivel2.unity";
    private const string Toy0Path = "Assets/Prefabs/Items/PF_Toy_0.prefab";
    private const string Toy1Path = "Assets/Prefabs/Items/PF_Toy_1.prefab";
    private const string Toy2Path = "Assets/Prefabs/Items/PF_Toy_2.prefab";
    private const string GoblinPath = "Assets/Prefabs/Enemies/PF_Goblin.prefab";
    private const string BossPath = "Assets/Prefabs/Enemies/PF_GhostChild.prefab";

    [MenuItem("Juego/Nivel 2/1. Reconstruir Nivel 2 Completo", false, 1)]
    [MenuItem("Herramientas/Nivel 2/Reconstruir Nivel 2 Completo", false, 1)]
    public static void RebuildLevel2Menu()
    {
        BuildLevel2(true);
    }

    public static void BuildLevel2(bool showDialog)
    {
        // Siempre recargar la escena limpia desde el disco para no conservar estados vacíos en RAM
        Scene currentScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Undo.IncrementCurrentGroup();
        string undoGroupName = "Reconstruir Nivel 2 Completo";
        Undo.SetCurrentGroupName(undoGroupName);

        Debug.Log("[Level2SceneBuilder] Iniciando configuración de Cementerio_Nivel2 respetando Canvas y animaciones originales...");

        // 0. Si la escena no cuenta con la estructura del mapa SCENE, generarla
        if (GameObject.Find("SCENE") == null)
        {
            try
            {
                Level2MapDesigner.Rebuild();
                currentScene = EditorSceneManager.GetActiveScene();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Level2SceneBuilder] Aviso al generar terreno: " + ex.Message);
            }
        }

        // 1. Desactivar elementos obsoletos de la copia del Nivel 1 (búsqueda de monedas, etc.)
        DeactivateLegacyLevel1Objects();

        // 1b. Configurar duendes centinelas patrullando los caminos principales
        SetupPatrolSentinels();

        // 2. Configurar y posicionar al Jugador (manteniendo TopDownCharacterController original)
        GameObject playerObj = SetupPlayer();

        // 3. Crear los 3 juguetes en sus ubicaciones exactas
        ToyPickup[] toys = SetupToys();

        // 4. Crear las Arenas de Combate (ENCOUNTERS) alrededor de los 3 juguetes
        SetupEncounters(toys);

        // 5. Configurar el Canvas_HUD ORIGINAL del Nivel 1 sin destruirlo ni reemplazarlo
        SetupHUD(playerObj);

        // 6. Configurar la Puerta Lateral, Salida y Cortina Iris
        SetupGateAndCinematics(playerObj);

        // 7. Forzar jerarquía de capas estricta (suelo al fondo, pasto decorativo encima, tumbas/árboles/jugador en frente)
        EnforceSceneLayers();

        // 8. Guardar escena
        if (playerObj != null) EditorUtility.SetDirty(playerObj);
        EditorSceneManager.MarkSceneDirty(currentScene);
        EditorSceneManager.SaveScene(currentScene);

        Debug.Log("[Level2SceneBuilder] ¡Nivel 2 configurado con éxito y guardado en Assets/Scenes/Cementerio_Nivel2.unity!");

        if (showDialog)
        {
            EditorUtility.DisplayDialog("Nivel 2 Listo",
                "Se configuró el Nivel 2 conservando el Canvas original del Nivel 1 y controles:\n\n" +
                "• Canvas_HUD 100% original con vidas, piedras y mensaje de pergamino.\n" +
                "• Armas: [1] Farol, [2] Puños, [3] Cuchillo Grande, [4] Machete.\n" +
                "• Piedras arrojadizas (6/6) con barra de espacio.\n" +
                "• Jefe Niño Espectral en el último juguete con barra azul.\n" +
                "• Barras de vida flotantes verdes para duendes.\n" +
                "• Orbes de vida con salto para recuperación de salud.",
                "Excelente");
        }
    }

    private static void DeactivateLegacyLevel1Objects()
    {
        string[] legacyNames = new string[]
        {
            "BusquedaMonedas",
            "Aviso_Goblin_01",
            "Aviso_Goblin_02",
            "Aviso_Goblin_03",
            "Nota_1",
            "Nota_2",
            "Nota_3",
            "Nota_Mensaje",
            "MensajesNivel",
            "CinematicDialogueUI",
            "IntroCinematicDirector"
        };

        foreach (var name in legacyNames)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null)
            {
                Undo.RecordObject(obj, "Desactivar " + name);
                var cg = obj.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0f;
                obj.SetActive(false);
            }
        }

        // Desactivar monedas del suelo
        GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>(true);
        foreach (var obj in allObjects)
        {
            if (obj == null) continue;
            if (obj.name.Contains("GoldCoin") || obj.name.StartsWith("Moneda_") || obj.name.StartsWith("PF_GoldCoin"))
            {
                if (obj.GetComponent<RectTransform>() == null)
                {
                    Undo.RecordObject(obj, "Desactivar Moneda Mundo");
                    obj.SetActive(false);
                }
            }
            if (obj.name == "Goblin_03")
            {
                Undo.RecordObject(obj, "Desactivar Duende 3");
                obj.SetActive(false);
            }
        }
    }

    private static void SetupPatrolSentinels()
    {
        GameObject goblinPf = AssetDatabase.LoadAssetAtPath<GameObject>(GoblinPath);

        // Sentinel 1: En el cruce oeste (-11.5, -4.5)
        GameObject g1 = GameObject.Find("Goblin_01");
        if (g1 == null && goblinPf != null)
        {
            g1 = (GameObject)PrefabUtility.InstantiatePrefab(goblinPf);
            g1.name = "Goblin_01";
            Undo.RegisterCreatedObjectUndo(g1, "Crear Goblin_01");
        }
        if (g1 != null)
        {
            g1.SetActive(true);
            g1.transform.position = new Vector3(-11.5f, -4.5f, 0f);
            ConfigureSentinel(g1);
        }

        // Sentinel 2: En el sendero hacia el noroeste (-13.5, 0.8)
        GameObject g2 = GameObject.Find("Goblin_02");
        if (g2 == null && goblinPf != null)
        {
            g2 = (GameObject)PrefabUtility.InstantiatePrefab(goblinPf);
            g2.name = "Goblin_02";
            Undo.RegisterCreatedObjectUndo(g2, "Crear Goblin_02");
        }
        if (g2 != null)
        {
            g2.SetActive(true);
            g2.transform.position = new Vector3(-13.5f, 0.8f, 0f);
            ConfigureSentinel(g2);
        }
    }

    private static void ConfigureSentinel(GameObject goblin)
    {
        GoblinAI ai = goblin.GetComponent<GoblinAI>();
        if (ai != null)
        {
            ai.waitForFirstCoin = false;
            ai.enablePatrol = true;
            ai.patrolRadius = 2.4f;
            ai.patrolSpeed = 0.85f;
            ai.noticeRadius = 4.2f;
            ai.trackingRadius = 7.5f;
            ai.attackDamage = 10;
            ai.attackRange = 1.2f;
        }

        EnemyHealth health = goblin.GetComponent<EnemyHealth>();
        if (health == null) health = goblin.AddComponent<EnemyHealth>();
        health.ConfigureVariant(EnemyHealth.EnemyVariant.NormalGoblin);

        EnemyFloatingHealthBar bar = goblin.GetComponent<EnemyFloatingHealthBar>();
        if (bar == null) bar = goblin.AddComponent<EnemyFloatingHealthBar>();
        bar.isBoss = false;

        foreach (var sr in goblin.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.sortingLayerName = "Layer 1";
            sr.sortingOrder = 2;
        }
    }

    private static GameObject SetupPlayer()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) player = GameObject.Find("PF Player");
        if (player == null) player = GameObject.Find("PF_LevelPlayer");

        if (player == null)
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Player/PF Player.prefab")
                            ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2/Player/PF Player.prefab")
                            ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PF_LevelPlayer.prefab");
            if (playerPrefab != null)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.name = "PF Player";
                Undo.RegisterCreatedObjectUndo(player, "Crear PF Player");
            }
        }

        if (player == null)
        {
            Debug.LogError("[Level2SceneBuilder] No se encontró PF Player en la escena ni su prefab.");
            return null;
        }

        player.tag = "Player";
        // Posicionar en la entrada del panteón (Acceso oeste)
        player.transform.position = new Vector3(-18.2f, -4.8f, 0f);

        // 1. Salud (100 HP, 5 corazones de 20 HP como especifica Nivel 2)
        PlayerHealth ph = player.GetComponent<PlayerHealth>();
        if (ph == null) ph = player.AddComponent<PlayerHealth>();
        ph.maxHealth = 100;
        ph.maxHearts = 5;
        ph.currentHealth = 100;

        // 2. Recolector de Juguetes
        ToyCollector toys = player.GetComponent<ToyCollector>();
        if (toys == null) toys = player.AddComponent<ToyCollector>();

        // 3. Controlador de Armas
        PlayerWeaponController weapons = player.GetComponent<PlayerWeaponController>();
        if (weapons == null) weapons = player.AddComponent<PlayerWeaponController>();
        weapons.hasLantern = true;
        weapons.hasBigKnife = false;
        weapons.hasMachete = false;
        weapons.currentWeapon = WeaponType.Lantern; // [1] Farol por defecto
        weapons.knifeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Items/Weapons/TX_CuchilloGrande.png");
        weapons.macheteSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Items/Weapons/TX_Machete.png");
        weapons.fistsHudSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Items/Weapons/TX_PunosHUD.png");
        weapons.lanternHudSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Gameplay/Farol/Farol_Animas.png");
        weapons.knifeHudSprite = weapons.knifeSprite;
        weapons.macheteHudSprite = weapons.macheteSprite;
        weapons.UpdateFarolHUD();
        EditorUtility.SetDirty(weapons);

        // 4. Lanzador de Piedras (6 piedras como Nivel 1)
        PlayerStoneThrow stones = player.GetComponent<PlayerStoneThrow>();
        if (stones == null) stones = player.AddComponent<PlayerStoneThrow>();
        stones.startingStones = 6;

        // 5. Mantener TopDownCharacterController original ACTIVO y configurado
        var legacyCc = player.GetComponent<Cainos.PixelArtTopDown_Basic.TopDownCharacterController>();
        if (legacyCc == null) legacyCc = player.AddComponent<Cainos.PixelArtTopDown_Basic.TopDownCharacterController>();
        legacyCc.enabled = true;
        legacyCc.speed = 3.5f;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        // Remover Level2CharacterController si existe para que no interfiera
        var cc2 = player.GetComponent<Cainos.PixelArtTopDown_Basic.Level2CharacterController>();
        if (cc2 != null)
        {
            Undo.DestroyObjectImmediate(cc2);
        }

        // 6. Farol del Jugador
        PlayerLantern lantern = player.GetComponent<PlayerLantern>();
        if (lantern == null) lantern = player.AddComponent<PlayerLantern>();

        return player;
    }

    private static ToyPickup[] SetupToys()
    {
        GameObject oldToysRoot = GameObject.Find("TOYS");
        if (oldToysRoot != null) Undo.DestroyObjectImmediate(oldToysRoot);

        GameObject toysRoot = new GameObject("TOYS");
        Undo.RegisterCreatedObjectUndo(toysRoot, "Crear TOYS Root");

        // Coordenadas basadas en las 3 explanadas del cementerio:
        // Juguete 1: Explanada Noroeste (-13.8, 6.2)
        // Juguete 2: Explanada Noreste (14.0, 6.5)
        // Juguete 3: Explanada Sureste (14.2, -6.5)
        Vector3[] toyPositions = new Vector3[]
        {
            new Vector3(-13.8f, 6.2f, 0f),
            new Vector3(14.0f, 6.5f, 0f),
            new Vector3(14.2f, -6.5f, 0f)
        };

        string[] toyPrefabs = new string[] { Toy0Path, Toy1Path, Toy2Path };
        ToyPickup[] result = new ToyPickup[3];

        for (int i = 0; i < 3; i++)
        {
            GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>(toyPrefabs[i]);
            GameObject toyObj;
            if (pf != null)
            {
                toyObj = (GameObject)PrefabUtility.InstantiatePrefab(pf, toysRoot.transform);
            }
            else
            {
                toyObj = new GameObject($"Toy_{i}");
                toyObj.transform.SetParent(toysRoot.transform);
            }

            toyObj.name = $"Toy_{i}";
            toyObj.transform.position = toyPositions[i];

            ToyPickup tp = toyObj.GetComponent<ToyPickup>();
            if (tp == null) tp = toyObj.AddComponent<ToyPickup>();
            tp.toyId = i;

            CircleCollider2D col = toyObj.GetComponent<CircleCollider2D>();
            if (col == null) col = toyObj.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;

            // Asegurar que el juguete se dibuje claramente sobre la piedra del altar
            foreach (var sr in toyObj.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sortingLayerName = "Layer 1";
                sr.sortingOrder = 5;
            }

            result[i] = tp;
        }

        return result;
    }

    private static void SetupEncounters(ToyPickup[] toys)
    {
        GameObject oldEncRoot = GameObject.Find("ENCOUNTERS");
        if (oldEncRoot != null) Undo.DestroyObjectImmediate(oldEncRoot);

        foreach (var b in UnityEngine.Object.FindObjectsOfType<ForceFieldBarrier>(true))
        {
            Undo.DestroyObjectImmediate(b.gameObject);
        }

        GameObject encRoot = new GameObject("ENCOUNTERS");
        Undo.RegisterCreatedObjectUndo(encRoot, "Crear ENCOUNTERS Root");

        GameObject goblinPf = AssetDatabase.LoadAssetAtPath<GameObject>(GoblinPath);
        GameObject bossPf = AssetDatabase.LoadAssetAtPath<GameObject>(BossPath);
        GameObject altarPf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2/Props/PF Props - Altar 01.prefab");

        // Asegurar Navigation con GoblinNavigation2D en la escena
        GameObject navObj = GameObject.Find("Navigation");
        if (navObj == null)
        {
            navObj = new GameObject("Navigation");
            Undo.RegisterCreatedObjectUndo(navObj, "Crear Navigation");
        }
        GoblinNavigation2D nav = navObj.GetComponent<GoblinNavigation2D>();
        if (nav == null) nav = navObj.AddComponent<GoblinNavigation2D>();
        nav.areaSize = new Vector2(44f, 26f);
        nav.cellSize = 0.25f;
        nav.agentSize = new Vector2(0.52f, 0.28f);
        nav.clearance = 0.02f;
        nav.obstacleMask = 1 << 6;
        navObj.transform.position = Vector3.zero;

        for (int i = 0; i < toys.Length; i++)
        {
            ToyPickup toy = toys[i];
            Vector3 pos = toy.transform.position;

            GameObject zoneObj = new GameObject($"EncounterZone_Toy_{i + 1}");
            Undo.RegisterCreatedObjectUndo(zoneObj, "Crear Zona Encuentro " + (i + 1));
            zoneObj.transform.SetParent(encRoot.transform, false);
            zoneObj.transform.position = pos;

            // Instanciar Altar visible con runas mágicas para que el jugador identifique la arena
            if (altarPf != null)
            {
                GameObject altar = (GameObject)PrefabUtility.InstantiatePrefab(altarPf, zoneObj.transform);
                altar.name = $"Altar_Encounter_{i + 1}";
                altar.transform.localPosition = Vector3.zero;

                // Remover WorldDepth2D y SortingGroup para que el altar NO quede en orden 938 sobre el jugador
                foreach (var wd in altar.GetComponentsInChildren<WorldDepth2D>(true))
                {
                    Undo.DestroyObjectImmediate(wd);
                }
                foreach (var sg in altar.GetComponentsInChildren<UnityEngine.Rendering.SortingGroup>(true))
                {
                    Undo.DestroyObjectImmediate(sg);
                }

                // El altar es una plataforma de piedra en el suelo: debe estar debajo del jugador (-50)
                foreach (var sr in altar.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    sr.sortingLayerName = "Layer 1";
                    if (sr.name.Contains("Shadow"))
                    {
                        sr.sortingOrder = -60;
                    }
                    else if (sr.name.Contains("Rune") || (sr.transform.parent != null && sr.transform.parent.name.Contains("Rune")))
                    {
                        sr.sortingOrder = -40; // Runas sobre la piedra
                    }
                    else
                    {
                        sr.sortingOrder = -50; // Base de piedra
                    }
                    EditorUtility.SetDirty(sr);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(sr);
                }
            }

            // Trigger circular amplio alrededor del juguete y altar
            CircleCollider2D col = zoneObj.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = (i == 2 ? 4.8f : 4.2f);

            // Spawners en círculo
            GameObject spawnersParent = new GameObject("SpawnPoints");
            spawnersParent.transform.SetParent(zoneObj.transform, false);

            Transform[] spArray = new Transform[4];
            float ringDist = (i == 2 ? 5.8f : 4.6f);
            for (int s = 0; s < 4; s++)
            {
                GameObject sp = new GameObject($"SpawnPoint_{s + 1}");
                sp.transform.SetParent(spawnersParent.transform, false);
                float angle = (s * 90f + 45f) * Mathf.Deg2Rad;
                sp.transform.localPosition = new Vector3(Mathf.Cos(angle) * ringDist, Mathf.Sin(angle) * ringDist, 0f);
                spArray[s] = sp.transform;
            }

            // Wave Manager
            EncounterWaveManager wm = zoneObj.AddComponent<EncounterWaveManager>();
            wm.goblinPrefab = goblinPf;
            wm.spawnPoints = spArray;

            // Toy Encounter Zone
            ToyEncounterZone zoneComp = zoneObj.AddComponent<ToyEncounterZone>();
            zoneComp.encounterIndex = i;
            zoneComp.targetToy = toy;
            zoneComp.waveManager = wm;
            zoneComp.bossPrefab = bossPf;
            zoneComp.searchRadius = 4.2f;
            zoneComp.searchDuration = 1.0f;
            if (i == 0) zoneComp.dropBigKnifeOnClear = true;
            zoneComp.forceField = null;
            zoneComp.AutoConfigureDefaultWavesIfEmpty();

            // Los juguetes inician invisibles e inactivos hasta que se derroten las oleadas
            if (toy != null)
            {
                toy.gameObject.SetActive(false);
            }
        }
    }

    private static void SetupHUD(GameObject player)
    {
        // 1. Conservar el Canvas_HUD ORIGINAL del Nivel 1
        GameObject canvasObj = GameObject.Find("Canvas_HUD");
        if (canvasObj == null)
        {
            Debug.LogError("[Level2SceneBuilder] No se encontró Canvas_HUD en la escena.");
            return;
        }

        // Si por accidente tuviera Level2HUD, removerlo
        var extraL2Hud = canvasObj.GetComponent<Level2HUD>();
        if (extraL2Hud != null) Undo.DestroyObjectImmediate(extraL2Hud);

        // 2. Conectar PlayerHUD en el objeto HUD original
        PlayerHUD playerHud = canvasObj.GetComponentInChildren<PlayerHUD>(true);
        if (playerHud != null && player != null)
        {
            playerHud.playerHealth = player.GetComponent<PlayerHealth>();
            playerHud.toyCollector = player.GetComponent<ToyCollector>();
            playerHud.coinCollector = null;

            playerHud.spritesJuguetes = new Sprite[3]
            {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Items/Toys/Toy_Bear.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Items/Toys/Toy_Ball.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Items/Toys/Toy_Train.png")
            };
            playerHud.CheckInitToys();
            EditorUtility.SetDirty(playerHud);
        }

        // 3. Conectar Piedras_HUD en el objeto Piedras_HUD original
        PlayerStoneHUD stoneHud = canvasObj.GetComponentInChildren<PlayerStoneHUD>(true);
        if (stoneHud != null && player != null)
        {
            stoneHud.thrower = player.GetComponent<PlayerStoneThrow>();
        }

        // 4. Ajustar Nota_Mensaje para que nunca se sobreponga a los corazones
        Transform noteTransform = canvasObj.transform.Find("Nota_Mensaje");
        if (noteTransform != null)
        {
            RectTransform noteRt = noteTransform.GetComponent<RectTransform>();
            if (noteRt != null)
            {
                noteRt.anchoredPosition = new Vector2(0f, -95f);
            }
        }

        // 5. Asegurar que Farol_HUD refleje el arma activa
        PlayerWeaponController weaponCtrl = player != null ? player.GetComponent<PlayerWeaponController>() : null;
        if (weaponCtrl != null)
        {
            weaponCtrl.UpdateFarolHUD();
        }

        // 6. Asegurar que ParchmentMessageUI esté dentro de Canvas_HUD con el tamaño idéntico a Nivel 1
        ParchmentMessageUI pUi = canvasObj.GetComponentInChildren<ParchmentMessageUI>(true);
        if (pUi == null)
        {
            GameObject existing = GameObject.Find("ParchmentMessageUI");
            if (existing != null)
            {
                existing.transform.SetParent(canvasObj.transform, false);
                pUi = existing.GetComponent<ParchmentMessageUI>();
            }
            else
            {
                GameObject newObj = new GameObject("ParchmentMessageUI");
                newObj.transform.SetParent(canvasObj.transform, false);
                pUi = newObj.AddComponent<ParchmentMessageUI>();
                Undo.RegisterCreatedObjectUndo(newObj, "Crear ParchmentMessageUI");
            }
        }
        if (pUi != null)
        {
            pUi.SetupProceduralUIIfMissing();
        }

        // 7. Asegurar que PlayerDeathScreenUI esté disponible dentro de Canvas_HUD
        PlayerDeathScreenUI deathUi = canvasObj.GetComponentInChildren<PlayerDeathScreenUI>(true);
        if (deathUi == null)
        {
            GameObject existingDeath = GameObject.Find("PlayerDeathScreenUI");
            if (existingDeath != null)
            {
                existingDeath.transform.SetParent(canvasObj.transform, false);
                deathUi = existingDeath.GetComponent<PlayerDeathScreenUI>();
            }
            else
            {
                GameObject newDeath = new GameObject("PlayerDeathScreenUI");
                newDeath.transform.SetParent(canvasObj.transform, false);
                deathUi = newDeath.AddComponent<PlayerDeathScreenUI>();
                Undo.RegisterCreatedObjectUndo(newDeath, "Crear PlayerDeathScreenUI");
            }
        }
        if (deathUi != null)
        {
            deathUi.SetupProceduralUIIfMissing();
        }

        // 8. Asegurar ToySearchUI en la escena
        if (UnityEngine.Object.FindObjectOfType<ToySearchUI>() == null)
        {
            GameObject searchObj = new GameObject("ToySearchUI");
            searchObj.AddComponent<ToySearchUI>();
            Undo.RegisterCreatedObjectUndo(searchObj, "Crear ToySearchUI");
        }

        // 9. Asegurar Level2ClueSystem en la escena
        if (UnityEngine.Object.FindObjectOfType<Level2ClueSystem>() == null)
        {
            GameObject clueObj = new GameObject("Level2ClueSystem");
            clueObj.AddComponent<Level2ClueSystem>();
            Undo.RegisterCreatedObjectUndo(clueObj, "Crear Level2ClueSystem");
        }
    }

    private static void SetupGateAndCinematics(GameObject player)
    {
        // Asegurar que la Main Camera enfoque al acceso oeste
        Camera mainCam = Camera.main ?? UnityEngine.Object.FindObjectOfType<Camera>();
        if (mainCam != null)
        {
            mainCam.transform.position = new Vector3(-13f, -4.7f, -10f);
            var bounds = mainCam.GetComponent<CameraBounds2D>();
            if (bounds != null)
            {
                bounds.minBounds = new Vector2(-22f, -13f);
                bounds.maxBounds = new Vector2(22f, 13f);
            }
        }

        // Alinear la puerta lateral oeste
        MapGeneratorEditor.FixLevel2WestGate(false);

        // Asegurar que la Cortina Iris apunte al jugador
        var iris = UnityEngine.Object.FindObjectOfType<IrisInNivel>();
        if (iris != null && player != null)
        {
            iris.jugador = player.transform;
            iris.gameObject.SetActive(true);
        }

        // Asegurar Level2Exit en la reja del acceso oeste
        GameObject accesoOeste = GameObject.Find("Acceso oeste");
        if (accesoOeste != null)
        {
            BoxCollider2D col = accesoOeste.GetComponent<BoxCollider2D>();
            if (col == null) col = accesoOeste.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2.5f, 4.0f);
            col.offset = new Vector2(-21.8f, -4.8f);

            Level2Exit exit = accesoOeste.GetComponent<Level2Exit>();
            if (exit == null) exit = accesoOeste.AddComponent<Level2Exit>();
        }
    }

    private static void EnforceSceneLayers()
    {
        // 1. Suelo base (Pasto) en lo más profundo (-200)
        var floorObj = GameObject.Find("Layer 1 - Grass");
        if (floorObj != null)
        {
            var tr = floorObj.GetComponent<TilemapRenderer>();
            if (tr != null)
            {
                tr.sortingLayerName = "Layer 1";
                tr.sortingOrder = -200;
            }
        }

        // 2. Senderos de piedra sobre el pasto base (-190)
        var stoneObj = GameObject.Find("Layer 1 - Stone Ground");
        if (stoneObj != null)
        {
            var tr = stoneObj.GetComponent<TilemapRenderer>();
            if (tr != null)
            {
                tr.sortingLayerName = "Layer 1";
                tr.sortingOrder = -190;
            }
        }

        // 3. Sombra de bardas (-180)
        var shadowObj = GameObject.Find("Layer 1 - Wall Shadow");
        if (shadowObj != null)
        {
            var tr = shadowObj.GetComponent<TilemapRenderer>();
            if (tr != null)
            {
                tr.sortingLayerName = "Layer 1";
                tr.sortingOrder = -180;
            }
        }

        // 4. Limpiar y desactivar Layer 2 - Stone Ground para que NUNCA dibuje piedra encima de objetos
        var l2StoneObj = GameObject.Find("Layer 2 - Stone Ground");
        if (l2StoneObj != null)
        {
            var tm = l2StoneObj.GetComponent<Tilemap>();
            if (tm != null) tm.ClearAllTiles();
            l2StoneObj.SetActive(false);
        }

        // 5. Pasto decorativo y flores sobre el suelo (-10)
        var grassParent = GameObject.Find("LAYER 1/Grass");
        if (grassParent != null)
        {
            foreach (var sr in grassParent.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sortingLayerName = "Layer 1";
                sr.sortingOrder = -10;
            }
        }

        // 6. Props, tumbas, árboles y elementos sólidos
        var sceneParent = GameObject.Find("SCENE");
        if (sceneParent != null)
        {
            // Remover cualquier WorldDepth2D y SortingGroup que altere el orden Y natural
            foreach (var depth in sceneParent.GetComponentsInChildren<WorldDepth2D>(true))
            {
                Undo.DestroyObjectImmediate(depth);
            }
            foreach (var sg in sceneParent.GetComponentsInChildren<UnityEngine.Rendering.SortingGroup>(true))
            {
                Undo.DestroyObjectImmediate(sg);
            }

            foreach (var sr in sceneParent.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.name == "Upper")
                {
                    // Copas de árboles siempre en Layer 2 por encima del jugador y tumbas
                    sr.sortingLayerName = "Layer 2";
                    sr.sortingOrder = 2;
                }
                else if (sr.name == "Shadow")
                {
                    sr.sortingLayerName = "Layer 1";
                    sr.sortingOrder = -1;
                }
                else if (grassParent != null && sr.transform.IsChildOf(grassParent.transform))
                {
                    sr.sortingLayerName = "Layer 1";
                    sr.sortingOrder = -10;
                }
                else
                {
                    sr.sortingLayerName = "Layer 1";
                    sr.sortingOrder = 2;
                }
            }
        }

        // 7. Jugador en Layer 1, orden 2
        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            foreach (var sr in player.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sortingLayerName = "Layer 1";
                sr.sortingOrder = 2;
            }
        }

        // 8. Altares en el suelo (debajo del jugador: piedra en -50, runas en -40)
        var encRoot = GameObject.Find("ENCOUNTERS");
        if (encRoot != null)
        {
            foreach (var wd in encRoot.GetComponentsInChildren<WorldDepth2D>(true))
            {
                Undo.DestroyObjectImmediate(wd);
            }
            foreach (var sg in encRoot.GetComponentsInChildren<UnityEngine.Rendering.SortingGroup>(true))
            {
                Undo.DestroyObjectImmediate(sg);
            }
            foreach (var sr in encRoot.GetComponentsInChildren<SpriteRenderer>(true))
            {
                bool isToy = sr.GetComponentInParent<ToyPickup>() != null || sr.gameObject.name.StartsWith("Toy_");
                if (isToy)
                {
                    sr.sortingLayerName = "Layer 1";
                    sr.sortingOrder = 5;
                }
                else
                {
                    sr.sortingLayerName = "Layer 1";
                    if (sr.name.Contains("Shadow")) sr.sortingOrder = -60;
                    else if (sr.name.Contains("Rune") || (sr.transform.parent != null && sr.transform.parent.name.Contains("Rune"))) sr.sortingOrder = -40;
                    else sr.sortingOrder = -50;
                }
                EditorUtility.SetDirty(sr);
                PrefabUtility.RecordPrefabInstancePropertyModifications(sr);
            }
        }
    }
}
