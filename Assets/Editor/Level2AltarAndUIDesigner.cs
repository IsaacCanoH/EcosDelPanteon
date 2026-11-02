using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class Level2AltarAndUIDesigner
{
    private const string AutoFixSessionKey = "Level2AltarAndUIDesigner_AutoFixed_v5";

    static Level2AltarAndUIDesigner()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SessionState.GetBool(AutoFixSessionKey, false)) return;
            SessionState.SetBool(AutoFixSessionKey, true);

            Scene active = SceneManager.GetActiveScene();
            if (active.name == "Cementerio_Nivel2")
            {
                FixAltarsAndUI();
            }
        };
    }

    [MenuItem("Herramientas/Nivel 2/Corregir Altares y UI")]
    public static void FixAltarsAndUI()
    {
        Debug.Log("[Level2AltarAndUIDesigner] Iniciando corrección integral de capas, cámara y UI en Nivel 2...");

        // 1. Limpiar SortingGroup y WorldDepth2D de TODOS los prefabs en Assets/Prefabs/Level2/Props/
        string propsDir = "Assets/Prefabs/Level2/Props";
        if (Directory.Exists(propsDir))
        {
            foreach (string file in Directory.GetFiles(propsDir, "*.prefab"))
            {
                using (var scope = new PrefabUtility.EditPrefabContentsScope(file))
                {
                    GameObject root = scope.prefabContentsRoot;

                    var sgs = root.GetComponentsInChildren<SortingGroup>(true);
                    foreach (var sg in sgs) Object.DestroyImmediate(sg);

                    var wds = root.GetComponentsInChildren<WorldDepth2D>(true);
                    foreach (var wd in wds) Object.DestroyImmediate(wd);

                    foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        sr.sortingLayerName = "Layer 1";
                        sr.spriteSortPoint = SpriteSortPoint.Pivot;
                        if (sr.gameObject.name.Contains("Shadow"))
                            sr.sortingOrder = 1;
                        else
                            sr.sortingOrder = 2;
                    }

                    // Asegurar collider sólido habilitado
                    foreach (var col in root.GetComponents<Collider2D>())
                    {
                        col.enabled = true;
                        if (!root.name.Contains("Trigger"))
                            col.isTrigger = false;
                    }
                }
            }
            Debug.Log("[Level2AltarAndUIDesigner] Prefabs en Assets/Prefabs/Level2/Props limpiados de SortingGroup y WorldDepth2D.");
        }

        // 2. Abrir la escena Cementerio_Nivel2
        string scenePath = "Assets/Scenes/Cementerio_Nivel2.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // 3. Quitar SortingGroup y WorldDepth2D de TODOS los objetos de la escena
        foreach (var sg in Object.FindObjectsOfType<SortingGroup>(true))
        {
            Object.DestroyImmediate(sg);
        }
        foreach (var wd in Object.FindObjectsOfType<WorldDepth2D>(true))
        {
            Object.DestroyImmediate(wd);
        }

        // 4. Configurar todos los props (bancas, lápidas, árboles, altares, etc.) con Layer 1, orden 2 y pivot
        int obstacleLayer = LayerMask.NameToLayer("Obstacles");
        foreach (var sr in Object.FindObjectsOfType<SpriteRenderer>(true))
        {
            if (sr == null) continue;
            GameObject go = sr.gameObject;
            string n = go.name;

            if (n.StartsWith("PF Props") || n.StartsWith("PF Village") || n.Contains("Bench") ||
                n.Contains("Gravestone") || n.Contains("Cross") || n.Contains("Well") ||
                n.Contains("Tree") || n.Contains("Statue") || n.Contains("Coffin") ||
                n.StartsWith("Altar_Encounter_"))
            {
                sr.sortingLayerName = "Layer 1";
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
                sr.sortingOrder = n.Contains("Shadow") ? 1 : 2;
                EditorUtility.SetDirty(sr);

                if (obstacleLayer >= 0 && !n.Contains("Shadow") && !n.Contains("Trigger"))
                    go.layer = obstacleLayer;

                foreach (var col in go.GetComponents<Collider2D>())
                {
                    col.enabled = true;
                    if (!n.Contains("Trigger") && !col.isTrigger)
                        col.isTrigger = false;
                    EditorUtility.SetDirty(col);
                }
            }
        }

        // 5. Configurar los 3 Altares con colisión sólida para que el jugador no los traspase
        var allAltars = Object.FindObjectsOfType<Transform>(true)
            .Where(t => t != null && t.name.StartsWith("Altar_Encounter_"))
            .Select(t => t.gameObject)
            .ToList();

        foreach (var altar in allAltars)
        {
            var solid = altar.GetComponent<CircleCollider2D>();
            if (solid == null) solid = altar.AddComponent<CircleCollider2D>();
            solid.enabled = true;
            solid.isTrigger = false;
            solid.offset = new Vector2(0f, 0.75f);
            solid.radius = 1.15f;
            EditorUtility.SetDirty(solid);

            var trigger = altar.GetComponent<BoxCollider2D>();
            if (trigger == null) trigger = altar.AddComponent<BoxCollider2D>();
            trigger.enabled = true;
            trigger.isTrigger = true;
            trigger.offset = new Vector2(0f, 0.75f);
            trigger.size = new Vector2(2.8f, 2.2f);
            EditorUtility.SetDirty(trigger);

            foreach (var sr in altar.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sortingLayerName = "Layer 1";
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
                sr.sortingOrder = sr.name.Contains("Shadow") ? 1 : 2;
                EditorUtility.SetDirty(sr);
            }
            EditorUtility.SetDirty(altar);
        }

        // 6. Configurar PF Player (sin SortingGroup, Layer 1, orden 2, pivot)
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var pSr = player.GetComponent<SpriteRenderer>();
            if (pSr != null)
            {
                pSr.sortingLayerName = "Layer 1";
                pSr.sortingOrder = 2;
                pSr.spriteSortPoint = SpriteSortPoint.Pivot;
                EditorUtility.SetDirty(pSr);
            }
            Transform pShadow = player.transform.Find("Shadow");
            if (pShadow != null)
            {
                var shadowSr = pShadow.GetComponent<SpriteRenderer>();
                if (shadowSr != null)
                {
                    shadowSr.sortingLayerName = "Layer 1";
                    shadowSr.sortingOrder = 1;
                    shadowSr.spriteSortPoint = SpriteSortPoint.Pivot;
                    EditorUtility.SetDirty(shadowSr);
                }
            }

            var l2Controller = player.GetComponent<Cainos.PixelArtTopDown_Basic.Level2CharacterController>();
            if (l2Controller != null)
            {
                l2Controller.speed = 3.0f;
                l2Controller.enabled = true;
                EditorUtility.SetDirty(l2Controller);
            }

            var lantern = player.GetComponent<PlayerLantern>();
            if (lantern != null)
            {
                lantern.range = 2.0f;
                lantern.enabled = true;
                EditorUtility.SetDirty(lantern);
            }

            var weapons = player.GetComponent<PlayerWeaponController>();
            if (weapons != null)
            {
                weapons.lanternRange = 2.0f;
                weapons.lanternDamage = 20;
                weapons.currentWeapon = WeaponType.Lantern;
                weapons.enabled = true;
                EditorUtility.SetDirty(weapons);
            }

            var coinCollector = player.GetComponent<GoldCoinCollector>();
            if (coinCollector != null)
            {
                coinCollector.enabled = false;
                EditorUtility.SetDirty(coinCollector);
            }

            // 7. Configurar Main Camera centrada y con límites de mapa exactos (-22 a 22, -13 a 13)
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.transform.position = new Vector3(player.transform.position.x, player.transform.position.y, -10f);
                EditorUtility.SetDirty(mainCam.transform);

                var camFollow = mainCam.GetComponent<Cainos.PixelArtTopDown_Basic.CameraFollow>();
                if (camFollow != null)
                {
                    camFollow.target = player.transform;
                    camFollow.lerpSpeed = 5.0f;
                    EditorUtility.SetDirty(camFollow);
                }

                var camBounds = mainCam.GetComponent<CameraBounds2D>();
                if (camBounds != null)
                {
                    camBounds.minBounds = new Vector2(-22f, -13f);
                    camBounds.maxBounds = new Vector2(22f, 13f);
                    EditorUtility.SetDirty(camBounds);
                }
            }

            EditorUtility.SetDirty(player);
        }

        // 8. Configurar duendes y UI de vida y alerta en Canvas_HUD
        List<GoblinAI> activeGoblins = new List<GoblinAI>();
        foreach (var goblin in Object.FindObjectsOfType<GoblinAI>(true))
        {
            if (goblin == null) continue;
            if (goblin.gameObject.activeInHierarchy)
            {
                goblin.enablePatrol = true;
                var sr = goblin.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sortingLayerName = "Layer 1";
                    sr.sortingOrder = 2;
                    sr.spriteSortPoint = SpriteSortPoint.Pivot;
                    EditorUtility.SetDirty(sr);
                }

                var eh = goblin.GetComponent<EnemyHealth>();
                if (eh == null) eh = goblin.gameObject.AddComponent<EnemyHealth>();
                eh.maxHealth = 50;
                eh.currentHealth = 50;
                EditorUtility.SetDirty(eh);

                var gc = goblin.GetComponent<GoblinCombat>();
                if (gc == null) gc = goblin.gameObject.AddComponent<GoblinCombat>();
                gc.maxHealth = 3;
                EditorUtility.SetDirty(gc);

                var oldBar = goblin.GetComponent<EnemyFloatingHealthBar>();
                if (oldBar != null) Object.DestroyImmediate(oldBar);

                EditorUtility.SetDirty(goblin);
                activeGoblins.Add(goblin);
            }
        }

        var awarenessUI = Object.FindObjectOfType<GoblinAwarenessUI>(true);
        if (awarenessUI != null)
        {
            awarenessUI.gameObject.SetActive(true);
            awarenessUI.worldCamera = Camera.main;
            awarenessUI.goblins = activeGoblins.ToArray();
            EditorUtility.SetDirty(awarenessUI);
        }

        var healthUI = Object.FindObjectOfType<GoblinHealthUI>(true);
        if (healthUI != null)
        {
            healthUI.gameObject.SetActive(true);
            if (awarenessUI != null) healthUI.awareness = awarenessUI;
            EditorUtility.SetDirty(healthUI);
        }

        // 9. Configurar HUD tecla F y tarjeta de pistas
        GameObject hudCanvas = GameObject.Find("Canvas_HUD");
        if (hudCanvas != null)
        {
            Transform farolHud = hudCanvas.transform.Find("HUD/Farol_HUD");
            if (farolHud != null)
            {
                var nombreFarol = farolHud.Find("Nombre_Farol")?.GetComponent<TMP_Text>();
                if (nombreFarol != null) { nombreFarol.text = "FAROL DE ANIMAS"; EditorUtility.SetDirty(nombreFarol); }

                var ayudaFarol = farolHud.Find("Ayuda_Farol")?.GetComponent<TMP_Text>();
                if (ayudaFarol != null) { ayudaFarol.text = "DESTELLO"; EditorUtility.SetDirty(ayudaFarol); }

                var teclaTexto = farolHud.Find("Tecla_Farol")?.GetComponentInChildren<TMP_Text>(true);
                if (teclaTexto != null) { teclaTexto.text = "F"; EditorUtility.SetDirty(teclaTexto); }
            }

            Transform busqueda = hudCanvas.transform.Find("BusquedaMonedas");
            if (busqueda != null)
            {
                busqueda.gameObject.SetActive(true);
                var coinSearchUI = busqueda.GetComponent<CoinSearchUI>();
                if (coinSearchUI != null)
                {
                    coinSearchUI.enabled = false;
                    EditorUtility.SetDirty(coinSearchUI);
                }

                Transform indBusqueda = busqueda.Find("Indicador_Busqueda");
                if (indBusqueda != null) indBusqueda.gameObject.SetActive(false);

                Transform accesoPista = busqueda.Find("Acceso_Pista_P");
                if (accesoPista != null) accesoPista.gameObject.SetActive(true);

                Transform fondoPausa = busqueda.Find("Fondo_Pausa_Pista");
                if (fondoPausa != null)
                {
                    fondoPausa.gameObject.SetActive(true);
                    var cg = fondoPausa.GetComponent<CanvasGroup>();
                    if (cg != null) { cg.alpha = 0f; cg.blocksRaycasts = false; EditorUtility.SetDirty(cg); }
                }

                Transform tarjetaPista = busqueda.Find("Tarjeta_Pista");
                if (tarjetaPista != null)
                {
                    tarjetaPista.gameObject.SetActive(true);
                    var cg = tarjetaPista.GetComponent<CanvasGroup>();
                    if (cg != null) { cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false; EditorUtility.SetDirty(cg); }
                }

                EditorUtility.SetDirty(busqueda.gameObject);
            }
        }

        var parchment = Object.FindObjectOfType<ParchmentMessageUI>(true);
        if (parchment != null)
        {
            for (int i = parchment.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(parchment.transform.GetChild(i).gameObject);
            }
            parchment.SetupProceduralUIIfMissing();
            EditorUtility.SetDirty(parchment);
            EditorUtility.SetDirty(parchment.gameObject);
        }

        // Guardar escena y assets
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[Level2AltarAndUIDesigner] ¡Corrección completada exitosamente!");
    }
}
