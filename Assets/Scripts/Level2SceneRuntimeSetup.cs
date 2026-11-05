using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

/// <summary>
/// Garantiza la configuración de capas, colisiones sólidas de bancas y props,
/// altares traspasables (solo los 3 altares), inicialización de la puerta de salida superior
/// pegada al muro norte y de la puerta de inicio abierta en el muro derecho para el Nivel 3,
/// remodelación completa del mapa con esquinas redondas, mapa ampliado, camino central,
/// Almacén del Panteón, y abundante población de árboles, lápidas, estatuas, faroles y bancas.
/// </summary>
public static class Level2SceneRuntimeSetup
{
    private struct OccupiedZone
    {
        public Vector2 position;
        public float radius;

        public OccupiedZone(Vector2 pos, float rad)
        {
            position = pos;
            radius = rad;
        }
    }

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
        if (string.IsNullOrEmpty(scene.name))
            return;

        bool isLevel2 = scene.name.IndexOf("Nivel2", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        scene.name.IndexOf("Level2", System.StringComparison.OrdinalIgnoreCase) >= 0;
        bool isLevel3 = scene.name.IndexOf("Nivel3", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        scene.name.IndexOf("Level3", System.StringComparison.OrdinalIgnoreCase) >= 0;

        if (!isLevel2 && !isLevel3)
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

            if (isLevel3)
            {
                var weapons = player.GetComponent<PlayerWeaponController>();
                if (weapons != null)
                {
                    weapons.hasLantern = true;
                    weapons.hasBigKnife = true;
                    weapons.hasMachete = true;
                    weapons.UpdateFarolHUD();
                    weapons.UpdateEquippedHandWeaponVisual();
                }

                // En el Nivel 3 el jugador inicia en la puerta del lado izquierdo (Acceso Oeste)
                player.transform.position = new Vector3(-18.2f, -4.8f, 0f);
                if (sr != null) sr.flipX = false; // Mirando hacia la derecha (hacia adentro del panteón)
                var anim = player.GetComponent<Animator>();
                if (anim != null) anim.SetInteger("Direction", 2); // 2 = Derecha

                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    mainCam.transform.position = new Vector3(player.transform.position.x, player.transform.position.y, mainCam.transform.position.z);
                }
            }
        }

        // 2. Limpieza de SortingGroup y WorldDepth2D en props que alteran el orden natural Y
        //    (Preservando SortingGroup en el Jugador, en UI y en los Duendes para que sus armas no se vean sobre los árboles)
        foreach (var depth in Object.FindObjectsOfType<WorldDepth2D>(true))
        {
            if (depth != null) Object.Destroy(depth);
        }
        foreach (var sg in Object.FindObjectsOfType<SortingGroup>(true))
        {
            if (sg == null) continue;
            bool isUi = sg.GetComponentInParent<Canvas>() != null;
            bool isPlayer = sg.CompareTag("Player") || sg.name == "PF Player";
            bool isEnemy = sg.GetComponent<GoblinAI>() != null ||
                           sg.GetComponent<EnemyHealth>() != null ||
                           sg.GetComponentInParent<GoblinAI>() != null;

            if (!isUi && !isPlayer && !isEnemy)
            {
                Object.Destroy(sg);
            }
        }

        // 3. Si estamos en el Nivel 3 (La Bruja), construir el mapa ampliado con esquinas redondas,
        //    Almacén del Panteón, y abundante población de árboles, tumbas, estatuas y bancas.
        if (isLevel3)
        {
            RemodelLevel3Map(obstaclesLayer);
        }

        // 4. Configuración sólida y de capas para Bancas, Altares (traspasables), Árboles y Props
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

        // 5. Asegurar que los Avisos de Goblin estén activos para GoblinAwarenessUI
        foreach (var t in Object.FindObjectsOfType<Transform>(true))
        {
            if (t != null && t.name.StartsWith("Aviso_Goblin_"))
            {
                t.gameObject.SetActive(true);
                var cg = t.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0f;
            }
        }

        // 6. Limpieza de tarjetas dobles de objetivo y mensajes huérfanos
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

        // 7. Asegurar gestor de Puertas de Salida y Entrada y Cinemáticas
        if (Object.FindObjectOfType<Level2VictoryManager>() == null)
        {
            GameObject victoryMgrObj = new GameObject("Level2VictoryManager");
            victoryMgrObj.AddComponent<Level2VictoryManager>();
        }
    }

    /// <summary>
    /// Construye el mapa exclusivo del Nivel 3 (La Bruja):
    /// - Mapa ampliado con piso de hierba extendido y las 4 esquinas exteriores redondeadas.
    /// - Puerta de Inicio abierta en el muro izquierdo (Acceso Oeste) y Puerta de Salida en el muro superior (Acceso Norte).
    /// - Gran Camino Central de piedra de Sur a Norte conectando con ambas puertas y con el Almacén.
    /// - Almacén del Panteón en el Noreste amurallado con pilares, cajas, barriles, cofres, vasijas y lápidas.
    /// - Altamente poblado de árboles frondosos, hileras de tumbas, estatuas de piedra, faroles y bancas.
    /// - Los 3 Altares en amplias plazas con radio de >= 3.45m garantizado sin superposiciones.
    /// </summary>
    private static void RemodelLevel3Map(int obstaclesLayer)
    {
        GameObject oldCustomRoot = GameObject.Find("Nivel3_Mapa_Decoracion");
        if (oldCustomRoot != null) Object.Destroy(oldCustomRoot);

        GameObject mapRoot = new GameObject("Nivel3_Mapa_Decoracion");
        mapRoot.transform.position = Vector3.zero;

        // Posiciones de los 3 Altares en plazas amplias y despejadas
        Vector3 altar1Pos = new Vector3(-10.5f, -5.5f, 0f); // Suroeste (Panteón Izquierdo)
        Vector3 altar2Pos = new Vector3( 10.5f, -4.5f, 0f); // Este (Jardín Funerario Derecho)
        Vector3 altar3Pos = new Vector3(  0.0f,  3.2f, 0f); // Centro-Norte (frente a la Puerta de Salida Norte)

        // 1. Ampliar el piso de hierba (Layer 1 - Grass) para que el mapa sea más grande y sin huecos exteriores
        GameObject grassObj = GameObject.Find("Layer 1 - Grass");
        if (grassObj != null)
        {
            Tilemap grassTm = grassObj.GetComponent<Tilemap>();
            if (grassTm != null)
            {
                TileBase grassTile = grassTm.GetTile(new Vector3Int(0, 0, 0));
                if (grassTile == null) grassTile = grassTm.GetTile(new Vector3Int(-5, -5, 0));
                if (grassTile != null)
                {
                    PaintStoneRect(grassTm, grassTile, -28, 28, -17, 17);
                }
            }
        }

        // 2. Pintar el sistema de caminos de piedra (Layer 1 - Stone Ground)
        GameObject stoneObj = GameObject.Find("Layer 1 - Stone Ground");
        if (stoneObj != null)
        {
            Tilemap stoneTm = stoneObj.GetComponent<Tilemap>();
            if (stoneTm != null)
            {
                TileBase stoneTile = stoneTm.GetTile(new Vector3Int(-18, -5, 0));
                if (stoneTile == null)
                {
                    for (int sx = -18; sx <= 15 && stoneTile == null; sx++)
                        for (int sy = -6; sy <= 6 && stoneTile == null; sy++)
                            stoneTile = stoneTm.GetTile(new Vector3Int(sx, sy, 0));
                }

                if (stoneTile != null)
                {
                    // Limpiar caminos antiguos heredados del nivel anterior
                    for (int x = -14; x <= 22; x++)
                    {
                        for (int y = -11; y <= 13; y++)
                        {
                            stoneTm.SetTile(new Vector3Int(x, y, 0), null);
                        }
                    }

                    // A) GRAN CAMINO CENTRAL VERTICAL: atraviesa todo el mapa de Sur a Norte (X = -1..1, Y = -11..14)
                    //    hasta cruzar la Puerta de Salida en el Muro Norte
                    PaintStoneRect(stoneTm, stoneTile, -1, 1, -11, 14);
                    // Plaza Central de la Encrucijada
                    PaintStoneRect(stoneTm, stoneTile, -2, 2, -6, -4);
                    // Gran Plaza Ritual del Altar 3 en el Camino Central (0, 3.2)
                    PaintStoneRect(stoneTm, stoneTile, -3, 3, 1, 5);

                    // B) CALZADA DERECHA (Conecta el Camino Central con el Altar 2 y el Almacén):
                    PaintStoneRect(stoneTm, stoneTile, 1, 15, -5, -4);
                    // Plaza del Altar 2
                    PaintStoneRect(stoneTm, stoneTile, 8, 13, -6, -3);
                    // Calzada hacia la entrada del Almacén del Panteón (X = 14..15, Y = -4..4)
                    PaintStoneRect(stoneTm, stoneTile, 14, 15, -4, 4);
                    // Pasillo superior que conecta el Camino Central con el Almacén (X = 1..11, Y = 5..6)
                    PaintStoneRect(stoneTm, stoneTile, 1, 11, 5, 6);

                    // C) CALZADA IZQUIERDA (Conecta la Puerta de Inicio en el Muro Izquierdo Acceso Oeste con el Camino Central):
                    //    Acceso Oeste (X = -18, Y = -6..-4) -> Altar 1 (-10.5, -5.5) -> Camino Central (X = 0)
                    PaintStoneRect(stoneTm, stoneTile, -18, -1, -6, -4);
                    // Plaza del Altar 1
                    PaintStoneRect(stoneTm, stoneTile, -13, -8, -7, -4);
                    // Sendero interior del Panteón Izquierdo entre las tumbas del noroeste
                    PaintStoneRect(stoneTm, stoneTile, -11, -10, -4, 6);
                    PaintStoneRect(stoneTm, stoneTile, -10, -1, 5, 6);

                    // D) PISO EMPEDRADO DEL ALMACÉN DEL PANTEÓN (Noreste: X = 12..18, Y = 4..9)
                    PaintStoneRect(stoneTm, stoneTile, 12, 18, 4, 9);
                }
            }
        }

        // 3. Redondear las 4 esquinas del muro exterior en Layer 1 - Wall y Layer 1 - Wall Shadow
        GameObject wallObj = GameObject.Find("Layer 1 - Wall");
        GameObject wallShadowObj = GameObject.Find("Layer 1 - Wall Shadow");
        Tilemap wallTm = wallObj != null ? wallObj.GetComponent<Tilemap>() : null;
        Tilemap shadowTm = wallShadowObj != null ? wallShadowObj.GetComponent<Tilemap>() : null;

        if (wallTm != null)
        {
            // Recortar las esquinas cuadradas afiladas (90°) para formar las curvas redondeadas
            Vector3Int[] cornerTilesToClear = new Vector3Int[]
            {
                // Noroeste
                new Vector3Int(-22, 12, 0), new Vector3Int(-22, 11, 0), new Vector3Int(-21, 12, 0),
                new Vector3Int(-22, 10, 0), new Vector3Int(-20, 12, 0), new Vector3Int(-21, 11, 0),
                // Noreste
                new Vector3Int( 21, 12, 0), new Vector3Int( 21, 11, 0), new Vector3Int( 20, 12, 0),
                new Vector3Int( 21, 10, 0), new Vector3Int( 19, 12, 0), new Vector3Int( 20, 11, 0),
                // Suroeste
                new Vector3Int(-22, -13, 0), new Vector3Int(-22, -12, 0), new Vector3Int(-21, -13, 0),
                new Vector3Int(-22, -11, 0), new Vector3Int(-20, -13, 0), new Vector3Int(-21, -12, 0),
                // Sureste
                new Vector3Int( 21, -13, 0), new Vector3Int( 21, -12, 0), new Vector3Int( 20, -13, 0),
                new Vector3Int( 21, -11, 0), new Vector3Int( 19, -13, 0), new Vector3Int( 20, -12, 0)
            };

            for (int i = 0; i < cornerTilesToClear.Length; i++)
            {
                wallTm.SetTile(cornerTilesToClear[i], null);
                if (shadowTm != null) shadowTm.SetTile(cornerTilesToClear[i], null);
            }
        }

        // 4. Reubicar los 3 Altares de Encuentro y sus juguetes a las plazas despejadas
        foreach (var zone in Object.FindObjectsOfType<ToyEncounterZone>(true))
        {
            if (zone == null) continue;

            Vector3 newAltarPos = zone.encounterIndex == 0
                ? altar1Pos
                : (zone.encounterIndex == 1 ? altar2Pos : altar3Pos);

            zone.transform.position = newAltarPos;

            if (zone.targetToy != null && !zone.targetToy.transform.IsChildOf(zone.transform))
            {
                zone.targetToy.transform.position = newAltarPos + new Vector3(0f, 0.55f, 0f);
            }
            else if (zone.targetToy != null)
            {
                zone.targetToy.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            }

            if (zone.waveManager != null && zone.waveManager.spawnPoints != null)
            {
                Vector3[] offsets = new Vector3[]
                {
                    new Vector3(-2.4f,  1.6f, 0f),
                    new Vector3( 2.4f,  1.6f, 0f),
                    new Vector3(-2.4f, -1.6f, 0f),
                    new Vector3( 2.4f, -1.6f, 0f)
                };
                for (int i = 0; i < zone.waveManager.spawnPoints.Length; i++)
                {
                    Transform sp = zone.waveManager.spawnPoints[i];
                    if (sp != null && !sp.IsChildOf(zone.transform))
                    {
                        sp.position = newAltarPos + offsets[i % offsets.Length];
                    }
                }
            }
        }

        // 5. Cargar prefabs de Cainos para poblar densamente el panteón
        GameObject grave1Tpl = null, grave2Tpl = null, grave3Tpl = null, coffinTpl = null, crossTpl = null;
        GameObject tree1Tpl = null, tree2Tpl = null, tree3Tpl = null, benchSTpl = null, benchWTpl = null, bushTpl = null;
        GameObject pillarTpl = null, statuePf = null;
        Material defaultPropMat = null;
        Color nightTint = new Color(0.6117647f, 0.6666667f, 0.78431374f, 1f);

        List<GameObject> interiorPropsToClear = new List<GameObject>();

        foreach (Transform t in Object.FindObjectsOfType<Transform>(true))
        {
            if (t == null || t.gameObject == null) continue;
            string n = t.name;

            if (n.Contains("Pillar") && pillarTpl == null)
            {
                pillarTpl = t.gameObject;
                var sr = t.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) { defaultPropMat = sr.sharedMaterial; nightTint = sr.color; }
            }

            bool isSceneProp = t.parent != null && (t.parent.name == "Panteones familiares" || t.parent.name == "Props");
            if (!isSceneProp) continue;

            if (n.Contains("Gravestone 01") && grave1Tpl == null) grave1Tpl = t.gameObject;
            else if (n.Contains("Gravestone 02") && grave2Tpl == null) grave2Tpl = t.gameObject;
            else if (n.Contains("Gravestone 03") && grave3Tpl == null) grave3Tpl = t.gameObject;
            else if (n.Contains("Stone Coffin") && coffinTpl == null) coffinTpl = t.gameObject;
            else if (n.Contains("Cross") && crossTpl == null) crossTpl = t.gameObject;
            else if (n.Contains("Tree 01") && tree1Tpl == null) tree1Tpl = t.gameObject;
            else if (n.Contains("Tree 02") && tree2Tpl == null) tree2Tpl = t.gameObject;
            else if (n.Contains("Tree 03") && tree3Tpl == null) tree3Tpl = t.gameObject;
            else if ((n.Contains("Bench 01 S") || n.Contains("Stone Bench 01 S")) && benchSTpl == null) benchSTpl = t.gameObject;
            else if ((n.Contains("Bench 01 W") || n.Contains("Stone Bench 01 W") || n.Contains("Bench 01 E")) && benchWTpl == null) benchWTpl = t.gameObject;
            else if (n.Contains("Bush") && bushTpl == null) bushTpl = t.gameObject;

            Vector3 p = t.position;
            bool inInterior = (p.x > -15.8f && p.x < 19.6f && p.y > -9.6f && p.y < 10.6f);
            bool inNorthGateCorridor = (p.x >= -3.8f && p.x <= 3.8f && p.y >= 7.0f);
            if (inInterior || inNorthGateCorridor)
            {
                interiorPropsToClear.Add(t.gameObject);
            }
        }

        GameObject crate1Pf = null, crate2Pf = null, barrelPf = null, chestPf = null, pot1Pf = null, pot2Pf = null, lanternPf = null, stoneCubePf = null;
#if UNITY_EDITOR
        if (tree1Tpl == null) tree1Tpl = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Plant/PF Plant - Tree 01.prefab");
        if (tree2Tpl == null) tree2Tpl = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Plant/PF Plant - Tree 02.prefab");
        if (tree3Tpl == null) tree3Tpl = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Plant/PF Plant - Tree 03.prefab");
        if (grave1Tpl == null) grave1Tpl = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Props/PF Props - Gravestone 01.prefab");
        if (grave2Tpl == null) grave2Tpl = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Props/PF Props - Gravestone 02.prefab");
        if (grave3Tpl == null) grave3Tpl = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Props/PF Props - Gravestone 03.prefab");
        if (coffinTpl == null) coffinTpl = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Props/PF Props - Stone Coffin 01.prefab");
        if (benchSTpl == null) benchSTpl = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Props/PF Props - Stone Bench 01 S.prefab");
        if (bushTpl == null) bushTpl = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Plant/PF Plant - Bush 01.prefab");
        statuePf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Props/PF Props - Statue 01.prefab");
        crate1Pf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Props/PF Props - Crate 01.prefab");
        crate2Pf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Props/PF Props - Crate 02.prefab");
        barrelPf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Props/PF Props - Barrel 01.prefab");
        chestPf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Props/PF Props - Chest 01.prefab");
        pot1Pf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Props/PF Props - Pot 01.prefab");
        pot2Pf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Props/PF Props - Pot 02.prefab");
        lanternPf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Props/PF Props - Stone Lantern 01.prefab");
        stoneCubePf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Props/PF Props - Stone Cube 01.prefab");
        if (pillarTpl == null)
            pillarTpl = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Props/PF Props - Pillar 01.prefab");
#endif

        if (grave2Tpl == null) grave2Tpl = grave1Tpl;
        if (grave3Tpl == null) grave3Tpl = grave1Tpl;
        if (coffinTpl == null) coffinTpl = grave1Tpl;
        if (crossTpl == null) crossTpl = grave1Tpl;
        if (tree2Tpl == null) tree2Tpl = tree1Tpl;
        if (tree3Tpl == null) tree3Tpl = tree1Tpl;
        if (benchWTpl == null) benchWTpl = benchSTpl;

        // 6. Registrar zonas reservadas para los 3 Altares (>= 3.45m), la Puerta Norte de Salida y la Puerta Oeste de Inicio
        List<OccupiedZone> occupied = new List<OccupiedZone>
        {
            new OccupiedZone(altar1Pos, 3.45f),                 // Altar 1 totalmente despejado
            new OccupiedZone(altar2Pos, 3.45f),                 // Altar 2 totalmente despejado
            new OccupiedZone(altar3Pos, 3.60f),                 // Altar 3 totalmente despejado
            new OccupiedZone(new Vector2(0f, 12.0f), 3.2f),     // Puerta Norte de Salida
            new OccupiedZone(new Vector2(-18.2f, -4.8f), 3.0f)  // Puerta Oeste de Inicio (Acceso Oeste)
        };

        // 7. CONSTRUCCIÓN DE LAS 4 ESQUINAS REDONDEADAS DEL MAPA
        //    (Muros diagonales de piedra, pilares esquineros y arboledas curvas)
        BuildRoundedCorners(mapRoot.transform, obstaclesLayer, defaultPropMat, nightTint, pillarTpl, stoneCubePf, tree1Tpl, tree2Tpl, tree3Tpl, bushTpl, occupied);

        // 8. CONSTRUIR EL "ALMACÉN DEL PANTEÓN" (Sector Noreste)
        BuildCemeteryWarehouse(
            mapRoot.transform,
            obstaclesLayer,
            defaultPropMat,
            nightTint,
            pillarTpl,
            stoneCubePf,
            crate1Pf,
            crate2Pf,
            barrelPf,
            chestPf,
            pot1Pf,
            pot2Pf,
            lanternPf,
            coffinTpl,
            grave2Tpl,
            occupied);

        // 9. ARBOLEDA ABUNDANTE POR TODO EL PANTEÓN (Más de 24 árboles frondosos)
        Vector2[] abundantTreePositions = new Vector2[]
        {
            // Arboleda Noroeste
            new Vector2(-15.8f,  5.2f), new Vector2(-15.8f,  1.2f), new Vector2(-15.8f, -2.5f),
            new Vector2(-14.2f, -8.2f), new Vector2( -4.5f,  8.8f), new Vector2( -4.5f,  1.2f),
            // Flanqueo del Camino Central (con paso amplio libre)
            new Vector2(-3.4f,  6.8f), new Vector2( 3.4f,  6.8f),
            new Vector2(-3.4f, -1.8f), new Vector2( 3.4f, -1.8f),
            // Arboleda Este y alrededor del Almacén
            new Vector2( 6.5f,  8.8f), new Vector2( 9.5f,  8.8f),
            new Vector2(10.2f,  1.5f), new Vector2(17.8f,  1.8f),
            new Vector2(17.8f, -7.8f), new Vector2( 6.5f, -8.2f)
        };
        for (int i = 0; i < abundantTreePositions.Length; i++)
        {
            GameObject tpl = (i % 3 == 0) ? tree1Tpl : ((i % 3 == 1) ? tree2Tpl : tree3Tpl);
            TrySpawnMapProp(tpl, abundantTreePositions[i], 1.45f, mapRoot.transform, $"Arbol_Panteon_{i + 1}", obstaclesLayer, defaultPropMat, nightTint, occupied);
        }

        // 10. POBLAR CON PREFABS: HILERAS DE TUMBAS, CRIPTAS Y ATAÚDES
        GameObject[] gravePool = new GameObject[] { grave1Tpl, grave2Tpl, grave3Tpl, coffinTpl, crossTpl };
        Vector2[] tombPositions = new Vector2[]
        {
            // LADO IZQUIERDO - Hileras Noroeste
            new Vector2(-15.0f,  8.4f), new Vector2(-13.0f,  8.4f), new Vector2(-11.0f,  8.4f),
            new Vector2( -8.6f,  8.4f), new Vector2( -6.4f,  8.4f), new Vector2( -4.2f,  8.4f),
            new Vector2(-15.0f,  6.6f), new Vector2(-13.0f,  6.6f),
            new Vector2(-15.0f,  3.8f), new Vector2(-13.0f,  3.8f),
            new Vector2( -8.2f,  3.8f), new Vector2( -6.2f,  3.8f), new Vector2( -4.2f,  3.8f),
            new Vector2(-15.0f,  1.6f), new Vector2(-13.0f,  1.6f),
            new Vector2( -8.2f,  1.6f), new Vector2( -6.2f,  1.6f), new Vector2( -4.2f,  1.6f),
            new Vector2(-15.0f, -0.6f), new Vector2(-13.0f, -0.6f),
            new Vector2( -8.2f, -0.6f), new Vector2( -6.2f, -0.6f), new Vector2( -4.2f, -0.6f),

            // LADO IZQUIERDO - Hileras Suroeste
            new Vector2(-14.8f, -8.8f), new Vector2(-12.6f, -8.8f), new Vector2(-10.4f, -8.8f),
            new Vector2( -8.2f, -8.8f), new Vector2( -6.0f, -8.8f), new Vector2( -3.8f, -8.8f),
            new Vector2(-14.8f, -10.8f), new Vector2(-12.6f, -10.8f), new Vector2(-8.2f, -10.8f),

            // LADO DERECHO - Jardín Funerario Central
            new Vector2( 4.6f,  3.2f), new Vector2( 6.8f,  3.2f), new Vector2( 9.0f,  3.2f),
            new Vector2( 4.6f,  1.2f), new Vector2( 9.0f,  1.2f),
            new Vector2( 4.6f, -0.8f), new Vector2( 6.8f, -0.8f), new Vector2( 9.0f, -0.8f),

            // LADO DERECHO - Panteón Sureste
            new Vector2( 6.4f, -7.2f), new Vector2( 8.6f, -7.2f), new Vector2(13.8f, -7.2f),
            new Vector2( 6.4f, -9.0f), new Vector2( 8.6f, -9.0f), new Vector2(11.2f, -9.0f), new Vector2(13.8f, -9.0f),
            new Vector2( 6.4f, -10.8f), new Vector2( 8.6f, -10.8f), new Vector2(11.2f, -10.8f),
            new Vector2(17.8f,  2.0f), new Vector2(17.8f, -5.5f)
        };
        for (int i = 0; i < tombPositions.Length; i++)
        {
            GameObject tpl = gravePool[i % gravePool.Length];
            TrySpawnMapProp(tpl, tombPositions[i], 0.95f, mapRoot.transform, $"Tumba_Panteon_{i + 1}", obstaclesLayer, defaultPropMat, nightTint, occupied);
        }

        // 11. ESTATUAS MONUMENTALES DEL PANTEÓN (4 Estatuas)
        if (statuePf != null)
        {
            Vector2[] statueSpots = new Vector2[]
            {
                new Vector2(-7.8f,  7.5f),  // Plaza Memorial Noroeste
                new Vector2(-14.2f, -6.5f), // Cripta Antigua Suroeste
                new Vector2( 7.5f, -6.5f),  // Memorial Sureste
                new Vector2(10.5f,  3.5f)   // Entrada al sector del Almacén
            };
            for (int i = 0; i < statueSpots.Length; i++)
            {
                TrySpawnMapProp(statuePf, statueSpots[i], 1.25f, mapRoot.transform, $"Estatua_Panteon_{i + 1}", obstaclesLayer, defaultPropMat, nightTint, occupied);
            }
        }

        // 12. BANCAS DE PIEDRA DISTRIBUIDAS JUNTO A LOS CAMINOS (8 Bancas)
        Vector2[] benchSpots = new Vector2[]
        {
            new Vector2(-5.2f,  7.2f),
            new Vector2( 5.2f,  7.2f),
            new Vector2(-5.2f, -3.2f),
            new Vector2( 5.2f, -3.2f),
            new Vector2( 8.4f,  7.2f),
            new Vector2( 8.4f, -3.2f),
            new Vector2(-11.5f, 1.2f),
            new Vector2(15.2f, -3.2f)
        };
        for (int i = 0; i < benchSpots.Length; i++)
        {
            TrySpawnMapProp(benchSTpl, benchSpots[i], 1.15f, mapRoot.transform, $"Banca_Panteon_{i + 1}", obstaclesLayer, defaultPropMat, nightTint, occupied);
        }

        // 13. FAROLES DE PIEDRA EN INTERSECCIONES Y ACCESOS (6 Faroles)
        if (lanternPf != null)
        {
            Vector2[] lanternSpots = new Vector2[]
            {
                new Vector2(-2.4f, -4.5f), new Vector2( 2.4f, -4.5f), // Cruce Central
                new Vector2(-2.6f,  1.2f), new Vector2( 2.6f,  1.2f), // Aproximación a la Plaza Norte
                new Vector2(-2.6f, 10.6f), new Vector2( 2.6f, 10.6f)  // Pórtico de la Puerta Norte
            };
            for (int i = 0; i < lanternSpots.Length; i++)
            {
                TrySpawnMapProp(lanternPf, lanternSpots[i], 0.75f, mapRoot.transform, $"Farol_Calzada_{i + 1}", obstaclesLayer, defaultPropMat, Color.white, occupied);
            }
        }

        // 14. ARBUSTOS Y VEGETACIÓN NATURAL
        if (bushTpl != null)
        {
            Vector2[] bushSpots = new Vector2[]
            {
                new Vector2(-2.4f, -1.8f), new Vector2( 2.4f, -1.8f),
                new Vector2(-2.4f,  7.2f), new Vector2( 2.4f,  7.2f),
                new Vector2(10.8f,  3.8f), new Vector2(17.6f,  3.4f),
                new Vector2(-14.5f, 7.2f), new Vector2(14.5f, -7.2f),
                new Vector2(-6.8f, -1.8f), new Vector2( 6.8f, -1.8f)
            };
            for (int i = 0; i < bushSpots.Length; i++)
            {
                TrySpawnMapProp(bushTpl, bushSpots[i], 0.75f, mapRoot.transform, $"Arbusto_N3_{i + 1}", obstaclesLayer, defaultPropMat, nightTint, occupied);
            }
        }

        // Desactivar props interiores obsoletos del nivel anterior
        for (int i = 0; i < interiorPropsToClear.Count; i++)
        {
            if (interiorPropsToClear[i] != null)
            {
                interiorPropsToClear[i].SetActive(false);
            }
        }

        // 15. Configurar duendes patrulleros iniciales del Nivel 3 (Arquero y Grande)
        GoblinAI[] existingGoblins = Object.FindObjectsOfType<GoblinAI>(false);
        for (int i = 0; i < existingGoblins.Length; i++)
        {
            GoblinAI g = existingGoblins[i];
            if (g == null) continue;

            GoblinVariantConfig cfg = g.GetComponent<GoblinVariantConfig>();
            if (cfg == null) cfg = g.gameObject.AddComponent<GoblinVariantConfig>();

            if (i % 2 == 0)
            {
                cfg.SetVariant(false, true); // Duende Arquero (50 HP, arco/cuchillo, orbe de vida)
            }
            else
            {
                cfg.SetVariant(true, false); // Duende Grande (80 HP, orbe de vida)
            }
        }

        // Reconstruir malla de navegación con el nuevo diseño
        GoblinNavigation2D nav = Object.FindObjectOfType<GoblinNavigation2D>();
        if (nav != null)
        {
            nav.SendMessage("Awake", SendMessageOptions.DontRequireReceiver);
        }
    }

    /// <summary>
    /// Construye las 4 esquinas redondeadas del perímetro del panteón mediante bastiones
    /// de piedra curvados, pilares esquineros, arboledas densas en arco y colisionadores diagonales.
    /// </summary>
    private static void BuildRoundedCorners(
        Transform parent,
        int obstaclesLayer,
        Material propMat,
        Color nightTint,
        GameObject pillarTpl,
        GameObject stoneCubePf,
        GameObject tree1Tpl,
        GameObject tree2Tpl,
        GameObject tree3Tpl,
        GameObject bushTpl,
        List<OccupiedZone> occupied)
    {
        GameObject cornerRoot = new GameObject("Esquinas_Redondeadas");
        cornerRoot.transform.SetParent(parent, false);
        cornerRoot.transform.position = Vector3.zero;

        GameObject wallBlock = stoneCubePf != null ? stoneCubePf : pillarTpl;

        // 1. Bastiones de piedra y pilares a lo largo de las 4 curvas diagonales
        Vector2[] cornerPillarSpots = new Vector2[]
        {
            // Curva Noroeste
            new Vector2(-18.5f, 11.2f), new Vector2(-20.0f,  9.8f), new Vector2(-21.2f,  8.2f),
            // Curva Noreste
            new Vector2( 17.5f, 11.2f), new Vector2( 19.2f,  9.8f), new Vector2( 20.4f,  8.2f),
            // Curva Suroeste
            new Vector2(-18.5f, -12.0f), new Vector2(-20.0f, -10.6f), new Vector2(-21.2f, -9.0f),
            // Curva Sureste
            new Vector2( 17.5f, -12.0f), new Vector2( 19.2f, -10.6f), new Vector2( 20.4f, -9.0f)
        };

        for (int i = 0; i < cornerPillarSpots.Length; i++)
        {
            GameObject p = SpawnWarehouseElement(pillarTpl, cornerPillarSpots[i], cornerRoot.transform, $"Pilar_Esquina_{i + 1}", obstaclesLayer, propMat, nightTint);
            if (p != null) p.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
            occupied.Add(new OccupiedZone(cornerPillarSpots[i], 0.85f));
        }

        // Muros de piedra completando las curvas diagonales
        if (wallBlock != null)
        {
            Vector2[] cornerCubeSpots = new Vector2[]
            {
                new Vector2(-19.2f, 10.5f), new Vector2(-20.6f,  9.0f),
                new Vector2( 18.3f, 10.5f), new Vector2( 19.8f,  9.0f),
                new Vector2(-19.2f, -11.3f), new Vector2(-20.6f, -9.8f),
                new Vector2( 18.3f, -11.3f), new Vector2( 19.8f, -9.8f)
            };
            for (int i = 0; i < cornerCubeSpots.Length; i++)
            {
                SpawnWarehouseElement(wallBlock, cornerCubeSpots[i], cornerRoot.transform, $"Muro_Esquina_{i + 1}", obstaclesLayer, propMat, nightTint);
                occupied.Add(new OccupiedZone(cornerCubeSpots[i], 0.75f));
            }
        }

        // 2. Arboleda monumental y densa en arco en las 4 esquinas redondeadas
        Vector2[] cornerTreeSpots = new Vector2[]
        {
            // Noroeste
            new Vector2(-19.8f, 10.2f), new Vector2(-17.5f, 11.2f),
            // Noreste
            new Vector2( 19.5f, 10.2f), new Vector2( 17.2f, 11.2f),
            // Suroeste
            new Vector2(-19.8f, -11.2f), new Vector2(-17.2f, -11.8f),
            // Sureste
            new Vector2( 19.5f, -11.2f), new Vector2( 17.2f, -11.8f)
        };
        for (int i = 0; i < cornerTreeSpots.Length; i++)
        {
            GameObject tpl = (i % 3 == 0) ? tree1Tpl : ((i % 3 == 1) ? tree2Tpl : tree3Tpl);
            TrySpawnMapProp(tpl, cornerTreeSpots[i], 1.45f, cornerRoot.transform, $"Arbol_Esquina_{i + 1}", obstaclesLayer, propMat, nightTint, occupied);
        }

        // 3. Colisionadores físicos diagonales en las 4 esquinas bajo Boundaries
        GameObject boundaries = GameObject.Find("Boundaries");
        if (boundaries != null)
        {
            CreateCornerDiagonalCollider(boundaries, "Noroeste_Diagonal", new Vector2(-19.8f,  9.8f),  45f, new Vector2(5.2f, 0.6f));
            CreateCornerDiagonalCollider(boundaries, "Noreste_Diagonal",  new Vector2( 19.2f,  9.8f), -45f, new Vector2(5.2f, 0.6f));
            CreateCornerDiagonalCollider(boundaries, "Suroeste_Diagonal", new Vector2(-19.8f, -10.8f), -45f, new Vector2(5.2f, 0.6f));
            CreateCornerDiagonalCollider(boundaries, "Sureste_Diagonal",  new Vector2( 19.2f, -10.8f),  45f, new Vector2(5.2f, 0.6f));
        }
    }

    private static void CreateCornerDiagonalCollider(GameObject parent, string name, Vector2 pos, float rotDeg, Vector2 size)
    {
        Transform existing = parent.transform.Find(name);
        if (existing != null) Object.Destroy(existing.gameObject);

        GameObject colObj = new GameObject(name);
        colObj.transform.SetParent(parent.transform, false);
        colObj.transform.position = new Vector3(pos.x, pos.y, 0f);
        colObj.transform.rotation = Quaternion.Euler(0f, 0f, rotDeg);
        colObj.layer = parent.layer;

        BoxCollider2D bc = colObj.AddComponent<BoxCollider2D>();
        bc.isTrigger = false;
        bc.size = size;
    }

    /// <summary>
    /// Construye el "Almacén del Panteón" en el sector Noreste (X = 11.8..18.4, Y = 4.2..9.4):
    /// Recinto amurallado con pilares y bloques de piedra, pórtico de entrada al sur flanqueado por faroles de piedra,
    /// y en su interior cajas de madera, barriles, cofres, vasijas y ataúdes/lápidas de reserva.
    /// </summary>
    private static void BuildCemeteryWarehouse(
        Transform parent,
        int obstaclesLayer,
        Material propMat,
        Color nightTint,
        GameObject pillarTpl,
        GameObject stoneCubePf,
        GameObject crate1Pf,
        GameObject crate2Pf,
        GameObject barrelPf,
        GameObject chestPf,
        GameObject pot1Pf,
        GameObject pot2Pf,
        GameObject lanternPf,
        GameObject coffinTpl,
        GameObject graveTpl,
        List<OccupiedZone> occupied)
    {
        GameObject warehouseRoot = new GameObject("Almacen_Del_Panteon");
        warehouseRoot.transform.SetParent(parent, false);
        warehouseRoot.transform.position = Vector3.zero;

        GameObject wallBlockTpl = stoneCubePf != null ? stoneCubePf : pillarTpl;

        // 1. Pilares esquineros y del pórtico de entrada del Almacén (X = 11.8 a 18.2, Y = 4.3 a 9.3)
        if (pillarTpl != null)
        {
            Vector2[] pillarSpots = new Vector2[]
            {
                new Vector2(11.8f, 9.2f), // Esquina Noroeste
                new Vector2(15.0f, 9.2f), // Columna Central Norte
                new Vector2(18.2f, 9.2f), // Esquina Noreste
                new Vector2(11.8f, 6.7f), // Columna Media Oeste
                new Vector2(18.2f, 6.7f), // Columna Media Este
                new Vector2(11.8f, 4.3f), // Esquina Suroeste
                new Vector2(13.4f, 4.3f), // Pórtico Entrada Izquierda
                new Vector2(16.0f, 4.3f), // Pórtico Entrada Derecha
                new Vector2(18.2f, 4.3f)  // Esquina Sureste
            };

            for (int i = 0; i < pillarSpots.Length; i++)
            {
                GameObject p = SpawnWarehouseElement(pillarTpl, pillarSpots[i], warehouseRoot.transform, $"Pilar_Almacen_{i + 1}", obstaclesLayer, propMat, nightTint);
                if (p != null) p.transform.localScale = new Vector3(0.78f, 0.78f, 1f);
                occupied.Add(new OccupiedZone(pillarSpots[i], 0.75f));
            }
        }

        // 2. Muros perimetrales de piedra entre los pilares
        if (wallBlockTpl != null)
        {
            Vector2[] wallCubeSpots = new Vector2[]
            {
                // Muro Norte del Almacén
                new Vector2(12.7f, 9.3f), new Vector2(13.6f, 9.3f), new Vector2(14.3f, 9.3f),
                new Vector2(15.8f, 9.3f), new Vector2(16.6f, 9.3f), new Vector2(17.4f, 9.3f),
                // Muro Oeste superior
                new Vector2(11.8f, 8.3f), new Vector2(11.8f, 7.5f),
                // Muro Este completo
                new Vector2(18.2f, 8.3f), new Vector2(18.2f, 7.5f), new Vector2(18.2f, 5.8f), new Vector2(18.2f, 5.0f),
                // Muro Sur (a ambos lados de la entrada principal)
                new Vector2(12.6f, 4.3f), new Vector2(16.8f, 4.3f), new Vector2(17.5f, 4.3f)
            };

            for (int i = 0; i < wallCubeSpots.Length; i++)
            {
                SpawnWarehouseElement(wallBlockTpl, wallCubeSpots[i], warehouseRoot.transform, $"Muro_Almacen_{i + 1}", obstaclesLayer, propMat, nightTint);
                occupied.Add(new OccupiedZone(wallCubeSpots[i], 0.65f));
            }
        }

        // 3. Faroles de piedra flanqueando la entrada sur del Almacén del Panteón
        if (lanternPf != null)
        {
            SpawnWarehouseElement(lanternPf, new Vector2(13.4f, 3.55f), warehouseRoot.transform, "Farol_Almacen_Oeste", obstaclesLayer, propMat, Color.white);
            SpawnWarehouseElement(lanternPf, new Vector2(16.0f, 3.55f), warehouseRoot.transform, "Farol_Almacen_Este", obstaclesLayer, propMat, Color.white);
            occupied.Add(new OccupiedZone(new Vector2(13.4f, 3.55f), 0.65f));
            occupied.Add(new OccupiedZone(new Vector2(16.0f, 3.55f), 0.65f));
        }

        // 4. Mobiliario interior del Almacén (cajas, barriles, cofres, vasijas y lápidas/ataúdes almacenados)
        if (crate1Pf != null)
        {
            SpawnWarehouseElement(crate1Pf, new Vector2(12.7f, 8.4f), warehouseRoot.transform, "Caja_Almacen_1", obstaclesLayer, propMat, nightTint);
            SpawnWarehouseElement(crate1Pf, new Vector2(13.5f, 8.4f), warehouseRoot.transform, "Caja_Almacen_2", obstaclesLayer, propMat, nightTint);
            occupied.Add(new OccupiedZone(new Vector2(13.1f, 8.4f), 0.9f));
        }
        if (crate2Pf != null)
        {
            SpawnWarehouseElement(crate2Pf, new Vector2(12.7f, 7.5f), warehouseRoot.transform, "Caja_Almacen_3", obstaclesLayer, propMat, nightTint);
            occupied.Add(new OccupiedZone(new Vector2(12.7f, 7.5f), 0.7f));
        }
        if (barrelPf != null)
        {
            SpawnWarehouseElement(barrelPf, new Vector2(14.4f, 8.4f), warehouseRoot.transform, "Barril_Almacen_1", obstaclesLayer, propMat, nightTint);
            SpawnWarehouseElement(barrelPf, new Vector2(15.2f, 8.4f), warehouseRoot.transform, "Barril_Almacen_2", obstaclesLayer, propMat, nightTint);
            occupied.Add(new OccupiedZone(new Vector2(14.8f, 8.4f), 0.85f));
        }
        if (chestPf != null)
        {
            SpawnWarehouseElement(chestPf, new Vector2(16.3f, 8.4f), warehouseRoot.transform, "Cofre_Almacen_1", obstaclesLayer, propMat, nightTint);
            occupied.Add(new OccupiedZone(new Vector2(16.3f, 8.4f), 0.75f));
        }
        if (pot1Pf != null)
        {
            SpawnWarehouseElement(pot1Pf, new Vector2(17.3f, 8.4f), warehouseRoot.transform, "Vasija_Almacen_1", obstaclesLayer, propMat, nightTint);
        }
        if (pot2Pf != null)
        {
            SpawnWarehouseElement(pot2Pf, new Vector2(17.4f, 7.6f), warehouseRoot.transform, "Vasija_Almacen_2", obstaclesLayer, propMat, nightTint);
        }
        if (coffinTpl != null)
        {
            SpawnWarehouseElement(coffinTpl, new Vector2(17.1f, 5.8f), warehouseRoot.transform, "Ataud_En_Resguardo", obstaclesLayer, propMat, nightTint);
            occupied.Add(new OccupiedZone(new Vector2(17.1f, 5.8f), 1.0f));
        }
        if (graveTpl != null)
        {
            SpawnWarehouseElement(graveTpl, new Vector2(12.7f, 5.2f), warehouseRoot.transform, "Lapida_En_Resguardo", obstaclesLayer, propMat, nightTint);
            occupied.Add(new OccupiedZone(new Vector2(12.7f, 5.2f), 0.85f));
        }

        // Reservar también el área general del Almacén para que ningún árbol o tumba exterior se coloque encima
        occupied.Add(new OccupiedZone(new Vector2(15.0f, 6.8f), 4.2f));
    }

    private static GameObject SpawnWarehouseElement(
        GameObject template,
        Vector2 pos,
        Transform parent,
        string name,
        int obstaclesLayer,
        Material propMat,
        Color tint)
    {
        if (template == null) return null;

        GameObject obj = Object.Instantiate(template, parent);
        obj.name = name;
        obj.transform.position = new Vector3(pos.x, pos.y, 0f);
        obj.SetActive(true);
        obj.layer = obstaclesLayer;

        var sg = obj.GetComponent<SortingGroup>();
        if (sg != null) Object.Destroy(sg);
        var wd = obj.GetComponent<WorldDepth2D>();
        if (wd != null) Object.Destroy(wd);

        foreach (var sr in obj.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.sortingLayerName = "Layer 1";
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            if (sr.name.Contains("Shadow"))
            {
                sr.sortingOrder = 1;
            }
            else
            {
                sr.sortingOrder = 2;
                if (propMat != null) sr.sharedMaterial = propMat;
                sr.color = tint;
            }
        }

        if (obj.GetComponent<Collider2D>() == null)
        {
            var box = obj.AddComponent<BoxCollider2D>();
            box.isTrigger = false;
            box.offset = new Vector2(0f, 0.35f);
            box.size = new Vector2(0.75f, 0.65f);
        }

        return obj;
    }

    private static bool TrySpawnMapProp(
        GameObject template,
        Vector2 pos,
        float radius,
        Transform parent,
        string name,
        int obstaclesLayer,
        Material propMat,
        Color tint,
        List<OccupiedZone> occupied)
    {
        if (template == null) return false;

        // Verificar que no se sobreponga con ningún Altar ni con ningún otro elemento ya colocado
        for (int i = 0; i < occupied.Count; i++)
        {
            float minDist = occupied[i].radius + radius * 0.65f;
            if (Vector2.Distance(pos, occupied[i].position) < minDist)
            {
                return false;
            }
        }

        occupied.Add(new OccupiedZone(pos, radius));

        GameObject obj = Object.Instantiate(template, parent);
        obj.name = name;
        obj.transform.position = new Vector3(pos.x, pos.y, 0f);
        obj.SetActive(true);
        obj.layer = obstaclesLayer;

        var sg = obj.GetComponent<SortingGroup>();
        if (sg != null) Object.Destroy(sg);
        var wd = obj.GetComponent<WorldDepth2D>();
        if (wd != null) Object.Destroy(wd);

        foreach (var sr in obj.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.name == "Upper")
            {
                sr.sortingLayerName = "Layer 2";
                sr.sortingOrder = 2;
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
            }
            else if (sr.name.Contains("Shadow"))
            {
                sr.sortingLayerName = "Layer 1";
                sr.sortingOrder = 1;
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
            }
            else
            {
                sr.sortingLayerName = "Layer 1";
                sr.sortingOrder = 2;
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
                if (propMat != null) sr.sharedMaterial = propMat;
                sr.color = tint;
            }
        }

        return true;
    }

    private static void PaintStoneRect(Tilemap tm, TileBase tile, int minX, int maxX, int minY, int maxY)
    {
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                tm.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }
    }
}
