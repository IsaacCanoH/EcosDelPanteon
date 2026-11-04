using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

/// <summary>
/// Garantiza la configuración de capas, colisiones sólidas de bancas y props,
/// altares traspasables (solo los 3 altares), inicialización de puerta derecha de salida,
/// indicadores de alerta/sospecha, y eliminación de tarjetas dobles en el Nivel 2.
/// </summary>
public static class Level2SceneRuntimeSetup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyFixes(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyFixes(scene);
    }

    public static void ApplyFixes(Scene scene)
    {
        if (string.IsNullOrEmpty(scene.name) || !scene.name.Contains("Nivel2"))
            return;

        Time.timeScale = 1f;
        AudioListener.pause = false;
        ToyEncounterZone.ResetAllEncounterStatics();

        int obstaclesLayer = LayerMask.NameToLayer("Obstacles");
        if (obstaclesLayer < 0) obstaclesLayer = 6;

        // 1. Configuración del Jugador
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            var depth = player.GetComponent<WorldDepth2D>();
            if (depth != null) Object.Destroy(depth);

            var group = player.GetComponent<SortingGroup>();
            if (group == null) group = player.AddComponent<SortingGroup>();
            group.sortingLayerName = "Layer 1";
            group.sortingOrder = 2;

            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }

            var sr = player.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sortingLayerName = "Layer 1";
                sr.sortingOrder = 2;
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
            }
        }

        // 2. Limpieza de SortingGroup y WorldDepth2D en props que alteran el orden natural Y
        foreach (var depth in Object.FindObjectsOfType<WorldDepth2D>(true))
        {
            if (depth != null) Object.Destroy(depth);
        }
        foreach (var sg in Object.FindObjectsOfType<SortingGroup>(true))
        {
            // No destruir SortingGroup en UI ni en el Jugador
            if (sg != null && sg.GetComponentInParent<Canvas>() == null &&
                !sg.CompareTag("Player") && sg.name != "PF Player")
            {
                Object.Destroy(sg);
            }
        }

        // 3. Configuración sólida y de capas para Bancas, Altares (traspasables) y Props
        foreach (var t in Object.FindObjectsOfType<Transform>(true))
        {
            if (t == null) continue;
            string n = t.name;

            // Bancas de piedra (sólidas)
            if (n.Contains("Bench"))
            {
                t.gameObject.layer = obstaclesLayer;

                var box = t.GetComponent<BoxCollider2D>();
                if (box == null) box = t.gameObject.AddComponent<BoxCollider2D>();
                box.enabled = true;
                box.isTrigger = false;

                if (n.Contains("Bench 01 S") || n.Contains("Stone Bench 01 S"))
                {
                    box.offset = new Vector2(0f, 0.45f);
                    box.size = new Vector2(1.70f, 0.90f);
                }
                else if (n.Contains("Bench 01 E") || n.Contains("Stone Bench 01 E"))
                {
                    box.offset = new Vector2(0.05f, 0.65f);
                    box.size = new Vector2(0.70f, 1.30f);
                }
                else if (n.Contains("Bench 01 W") || n.Contains("Stone Bench 01 W"))
                {
                    box.offset = new Vector2(-0.05f, 0.65f);
                    box.size = new Vector2(0.70f, 1.30f);
                }

                var sr = t.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sortingLayerName = "Layer 1";
                    sr.sortingOrder = 2;
                    sr.spriteSortPoint = SpriteSortPoint.Pivot;
                }

                Transform shadow = t.Find("Shadow");
                if (shadow != null)
                {
                    var shadowSr = shadow.GetComponent<SpriteRenderer>();
                    if (shadowSr != null)
                    {
                        shadowSr.sortingLayerName = "Layer 1";
                        shadowSr.sortingOrder = 1;
                        shadowSr.spriteSortPoint = SpriteSortPoint.Pivot;
                    }
                }
            }
            // Los 3 Altares de encuentro: completamente traspasables para el jugador y los enemigos
            else if (n.StartsWith("Altar_Encounter_"))
            {
                t.gameObject.layer = 0; // Default (no Obstacles)

                foreach (var col in t.GetComponents<Collider2D>())
                {
                    if (col != null && !col.isTrigger && col.GetComponent<ToyEncounterZone>() == null)
                    {
                        col.enabled = false;
                        Object.Destroy(col);
                    }
                }

                foreach (var sr in t.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    sr.sortingLayerName = "Layer 1";
                    sr.spriteSortPoint = SpriteSortPoint.Pivot;
                    // sortingOrder = 1 para que el jugador y los enemigos (sortingOrder = 2) pasen por encima del altar
                    sr.sortingOrder = sr.name.Contains("Shadow") ? 0 : 1;
                }
            }
            // Árboles
            else if (n.Contains("Tree") && !n.Contains("Trunk"))
            {
                Transform lower = t.Find("Lower");
                if (lower != null)
                {
                    var lowerSr = lower.GetComponent<SpriteRenderer>();
                    if (lowerSr != null)
                    {
                        lowerSr.sortingLayerName = "Layer 1";
                        lowerSr.sortingOrder = 2;
                        lowerSr.spriteSortPoint = SpriteSortPoint.Pivot;
                    }
                }
                Transform upper = t.Find("Upper");
                if (upper != null)
                {
                    var upperSr = upper.GetComponent<SpriteRenderer>();
                    if (upperSr != null)
                    {
                        upperSr.sortingLayerName = "Layer 2";
                        upperSr.sortingOrder = 2;
                        upperSr.spriteSortPoint = SpriteSortPoint.Pivot;
                    }
                }
                Transform shadow = t.Find("Shadow");
                if (shadow != null)
                {
                    var shadowSr = shadow.GetComponent<SpriteRenderer>();
                    if (shadowSr != null)
                    {
                        shadowSr.sortingLayerName = "Layer 1";
                        shadowSr.sortingOrder = 1;
                        shadowSr.spriteSortPoint = SpriteSortPoint.Pivot;
                    }
                }
                var sr = t.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sortingLayerName = "Layer 1";
                    sr.sortingOrder = 2;
                    sr.spriteSortPoint = SpriteSortPoint.Pivot;
                }
            }
            // Arbustos y vegetación
            else if (n.Contains("Bush") || n.Contains("Plant") || n.Contains("Flower"))
            {
                var sr = t.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sortingLayerName = "Layer 1";
                    sr.sortingOrder = 2;
                    sr.spriteSortPoint = SpriteSortPoint.Pivot;
                }
            }
        }

        // 4. Asegurar que los Avisos de Goblin estén activos para GoblinAwarenessUI
        foreach (var t in Object.FindObjectsOfType<Transform>(true))
        {
            if (t != null && t.name.StartsWith("Aviso_Goblin_"))
            {
                t.gameObject.SetActive(true);
                var cg = t.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0f;
            }
        }

        // 5. Limpieza de tarjetas dobles de objetivo y mensajes huérfanos en Nivel 2
        var hud = GameObject.Find("Canvas_HUD");
        if (hud != null)
        {
            var controlMensajes = hud.transform.Find("Control_Mensajes");
            if (controlMensajes != null)
            {
                Object.Destroy(controlMensajes.gameObject);
            }

            var pMsgUI = hud.GetComponentInChildren<ParchmentMessageUI>(true);
            if (pMsgUI != null)
            {
                for (int i = pMsgUI.transform.childCount - 1; i >= 0; i--)
                {
                    Transform ch = pMsgUI.transform.GetChild(i);
                    ch.gameObject.SetActive(false);
                    Object.Destroy(ch.gameObject);
                }
                pMsgUI.SetupProceduralUIIfMissing();
            }

            foreach (var t in hud.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name == "ParchmentCard")
                {
                    t.gameObject.SetActive(false);
                    Object.Destroy(t.gameObject);
                }
            }

            var busqueda = hud.transform.Find("BusquedaMonedas");
            if (busqueda != null)
            {
                busqueda.gameObject.SetActive(true);
                var coinSearch = busqueda.GetComponent<CoinSearchUI>();
                if (coinSearch != null) coinSearch.enabled = false;

                var indicador = busqueda.Find("Indicador_Busqueda");
                if (indicador != null) indicador.gameObject.SetActive(false);

                var acceso = busqueda.Find("Acceso_Pista_P");
                if (acceso != null) acceso.gameObject.SetActive(true);

                var tarjetaPista = busqueda.Find("Tarjeta_Pista");
                if (tarjetaPista != null)
                {
                    var cg = tarjetaPista.GetComponent<CanvasGroup>();
                    if (cg != null)
                    {
                        cg.alpha = 0f;
                        cg.interactable = false;
                        cg.blocksRaycasts = false;
                    }
                }
            }
        }

        // 6. Asegurar gestor de Puerta de Salida Este y Cinemática Final de Victoria
        if (Object.FindObjectOfType<Level2VictoryManager>() == null)
        {
            GameObject victoryMgrObj = new GameObject("Level2VictoryManager");
            victoryMgrObj.AddComponent<Level2VictoryManager>();
        }
    }
}
