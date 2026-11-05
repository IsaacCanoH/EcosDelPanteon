using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

/// <summary>
/// Garantiza la configuración de capas, colisiones sólidas de bancas y props,
/// altares traspasables (solo los 3 altares), inicialización de la puerta de salida superior
/// pegada al muro norte y de la puerta de inicio abierta en el muro izquierdo (Acceso Oeste) para el Nivel 3,
/// remodelación completa del mapa con esquinas redondas, mapa ampliado, camino central,
/// Almacén del Panteón, abundante población de prefabs y mayor cantidad de enemigos
/// (alternando duendes fuertes, arqueros blindados y duendes base, con predominio de duendes fuertes).
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
                    var camBounds = mainCam.GetComponent<CameraBounds2D>();
                    if (camBounds != null)
                    {
                        camBounds.minBounds = new Vector2(-22f, -17f);
                        camBounds.maxBounds = new Vector2(26f, 13f);
                    }
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
        //    Almacén del Panteón, puerta de inicio izquierda activa, objetos rituales y más enemigos.
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
            if (n.Contains("Bench") && n != "Bench ")
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

                // Colocar la mitad inferior del follaje verde de Lower en Layer 2 para que ningún enemigo ni La Bruja se vea a través de los árboles
                EnsureTreeLowerCanopyOverlayOnLayer2(t);
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
    /// Asegura que la Puerta de Inicio en el Lado Izquierdo (Acceso oeste) y todos sus contenedores
    /// padres (Decoration, Props, SCENE) estén activos y visibles en el Nivel 3, con sus dos pilares,
    /// arbustos y hojas de madera abiertas.
    /// </summary>
    public static GameObject RestoreAndOpenWestEntranceGate()
    {
        Transform accesoOesteTr = null;
        foreach (Transform t in Object.FindObjectsOfType<Transform>(true))
        {
            if (t != null && t.name == "Acceso oeste")
            {
                accesoOesteTr = t;
                break;
            }
        }

        if (accesoOesteTr == null) return null;

        // 1. Activar todos los padres jerárquicos (Decoration, Props, SCENE) para que Acceso oeste no quede oculto
        Transform p = accesoOesteTr;
        while (p != null)
        {
            if (!p.gameObject.activeSelf)
            {
                p.gameObject.SetActive(true);
            }
            p = p.parent;
        }

        // 2. Desactivar el componente Level2Exit en Nivel 3 para que la puerta izquierda sea únicamente la puerta de entrada
        var lvl2Exit = accesoOesteTr.GetComponent<Level2Exit>();
        if (lvl2Exit != null)
        {
            lvl2Exit.enabled = false;
        }
        foreach (var col in accesoOesteTr.GetComponents<Collider2D>())
        {
            if (col != null && col.isTrigger)
            {
                col.enabled = false;
            }
        }

        // 3. Activar los pilares, arbustos y las 2 hojas laterales abiertas; mantener desactivado el prefab duplicado cerrado/abierto
        for (int i = 0; i < accesoOesteTr.childCount; i++)
        {
            Transform ch = accesoOesteTr.GetChild(i);
            if (ch == null) continue;

            if (ch.name.Contains("Wooden Gate 01 Opened"))
            {
                ch.gameObject.SetActive(false);
            }
            else
            {
                ch.gameObject.SetActive(true);
                foreach (var sr in ch.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    sr.enabled = true;
                    sr.sortingLayerName = "Layer 1";
                    sr.sortingOrder = sr.name.Contains("Shadow") ? 1 : (ch.name.Contains("Sur") ? 3 : 2);
                }
            }
        }

        // 4. Asegurar que el hueco del pórtico izquierdo esté despejado en Layer 1 - Wall y Layer 1 - Wall Shadow
        GameObject wallObj = GameObject.Find("Layer 1 - Wall");
        if (wallObj != null)
        {
            Tilemap wallTm = wallObj.GetComponent<Tilemap>();
            if (wallTm != null)
            {
                for (int cx = -23; cx <= -20; cx++)
                    for (int cy = -6; cy <= -4; cy++)
                        wallTm.SetTile(new Vector3Int(cx, cy, 0), null);
            }
        }

        GameObject shadowObj = GameObject.Find("Layer 1 - Wall Shadow");
        if (shadowObj != null)
        {
            Tilemap shadowTm = shadowObj.GetComponent<Tilemap>();
            if (shadowTm != null)
            {
                for (int cx = -23; cx <= -19; cx++)
                    for (int cy = -6; cy <= -4; cy++)
                        shadowTm.SetTile(new Vector3Int(cx, cy, 0), null);
            }
        }

        return accesoOesteTr.gameObject;
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

        // 0. Restaurar y asegurar que la Puerta de Inicio Izquierda (Acceso oeste) esté activa y visible desde el inicio
        GameObject accesoOesteObj = RestoreAndOpenWestEntranceGate();

        // Posiciones de los 3 Altares en plazas amplias y despejadas
        Vector3 altar1Pos = new Vector3(-10.5f, -5.5f, 0f); // Suroeste (Panteón Izquierdo - Poción Morada)
        Vector3 altar2Pos = new Vector3( 10.5f, -4.5f, 0f); // Este (Jardín Funerario Derecho - Muñeco Vudú)
        Vector3 altar3Pos = new Vector3(  0.0f,  3.2f, 0f); // Centro-Norte (frente a la Puerta de Salida Norte - Vela Ritual)

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
                    PaintStoneRect(grassTm, grassTile, -30, 32, -22, 18);
                }
            }
        }

        // 2. Pintar el sistema de caminos de piedra (Layer 1 - Stone Ground) incluyendo las nuevas zonas Sur y Este ampliadas
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
                    for (int x = -14; x <= 25; x++)
                    {
                        for (int y = -16; y <= 13; y++)
                        {
                            stoneTm.SetTile(new Vector3Int(x, y, 0), null);
                        }
                    }

                    // A) GRAN CAMINO CENTRAL VERTICAL: atraviesa todo el mapa desde el nuevo Sur ampliado (Y = -15) hasta el Norte (Y = 14)
                    PaintStoneRect(stoneTm, stoneTile, -1, 1, -15, 14);
                    // Plaza Central de la Encrucijada
                    PaintStoneRect(stoneTm, stoneTile, -2, 2, -6, -4);
                    // Gran Plaza Ritual del Altar 3 en el Camino Central (0, 3.2)
                    PaintStoneRect(stoneTm, stoneTile, -3, 3, 1, 5);

                    // B) CALZADA DERECHA AMPLIADA (Conecta el Camino Central con el Altar 2, el Almacén y el nuevo sector Este X = 21):
                    PaintStoneRect(stoneTm, stoneTile, 1, 21, -5, -4);
                    // Plaza del Altar 2
                    PaintStoneRect(stoneTm, stoneTile, 8, 13, -6, -3);
                    // Calzada hacia la entrada del Almacén del Panteón (X = 14..15, Y = -4..4)
                    PaintStoneRect(stoneTm, stoneTile, 14, 15, -4, 4);
                    // Sendero Oriental en la nueva zona ampliada derecha (X = 20..21, Y = -13..6)
                    PaintStoneRect(stoneTm, stoneTile, 20, 21, -13, 6);
                    // Pasillo superior que conecta el Camino Central con el Almacén (X = 1..11, Y = 5..6)
                    PaintStoneRect(stoneTm, stoneTile, 1, 11, 5, 6);

                    // C) CALZADA SUR EN LA NUEVA ZONA AMPLIADA INFERIOR (Y = -13..-12, X = -14..21)
                    PaintStoneRect(stoneTm, stoneTile, -14, 21, -13, -12);

                    // D) CALZADA IZQUIERDA (Conecta la Puerta de Inicio en el Muro Izquierdo Acceso Oeste X=-22 con el Camino Central):
                    PaintStoneRect(stoneTm, stoneTile, -22, -1, -6, -4);
                    // Plaza del Altar 1
                    PaintStoneRect(stoneTm, stoneTile, -13, -8, -7, -4);
                    // Piso empedrado de la construcción del Pilar de Luz (cerca del Primer Altar en X = -12..-9, Y = -2..0)
                    PaintStoneRect(stoneTm, stoneTile, -12, -9, -2, 0);
                    // Sendero interior del Panteón Izquierdo entre las tumbas del noroeste y suroeste ampliado
                    PaintStoneRect(stoneTm, stoneTile, -11, -10, -13, 6);
                    PaintStoneRect(stoneTm, stoneTile, -10, -1, 5, 6);

                    // E) PISO EMPEDRADO DEL ALMACÉN DEL PANTEÓN (Noreste: X = 12..18, Y = 4..9)
                    PaintStoneRect(stoneTm, stoneTile, 12, 18, 4, 9);
                }
            }
        }

        // Desactivar cualquier barda interior rota heredada para que ninguna barda cruce el mapa
        GameObject bardasInterioresObj = GameObject.Find("Bardas interiores rotas");
        if (bardasInterioresObj != null)
        {
            bardasInterioresObj.SetActive(false);
        }

        // 3. Expandir el Muro Sur hacia abajo (de Y=-13 a Y=-17) y el Muro Este hacia la derecha (de X=21 a X=25),
        //    eliminando la barda horizontal intermedia en Layer 2 - Wall (Y=-12) y manteniendo las 4 esquinas RECTAS.
        GameObject wallObj = GameObject.Find("Layer 1 - Wall");
        GameObject wallShadowObj = GameObject.Find("Layer 1 - Wall Shadow");
        GameObject layer2WallObj = GameObject.Find("Layer 2 - Wall");
        Tilemap wallTm = wallObj != null ? wallObj.GetComponent<Tilemap>() : null;
        Tilemap shadowTm = wallShadowObj != null ? wallShadowObj.GetComponent<Tilemap>() : null;
        Tilemap layer2WallTm = layer2WallObj != null ? layer2WallObj.GetComponent<Tilemap>() : null;

        if (wallTm != null)
        {
            TileBase nwCornerTile = wallTm.GetTile(new Vector3Int(-22, 12, 0));
            TileBase neCornerTile = wallTm.GetTile(new Vector3Int(21, 12, 0));
            TileBase swCornerTile = wallTm.GetTile(new Vector3Int(-22, -13, 0));
            TileBase seCornerTile = wallTm.GetTile(new Vector3Int(21, -13, 0));

            TileBase northWallTile = wallTm.GetTile(new Vector3Int(-10, 12, 0));
            TileBase southWallTile = wallTm.GetTile(new Vector3Int(-10, -13, 0));
            if (southWallTile == null) southWallTile = northWallTile;
            TileBase westWallTile = wallTm.GetTile(new Vector3Int(-22, 2, 0));
            TileBase eastWallTile = wallTm.GetTile(new Vector3Int(21, 2, 0));
            if (eastWallTile == null) eastWallTile = westWallTile;

            if (nwCornerTile == null) nwCornerTile = northWallTile;
            if (neCornerTile == null) neCornerTile = northWallTile;
            if (swCornerTile == null) swCornerTile = southWallTile;
            if (seCornerTile == null) seCornerTile = southWallTile;

            TileBase northShadowTile = shadowTm != null ? shadowTm.GetTile(new Vector3Int(-10, 12, 0)) : null;
            TileBase southShadowTile = shadowTm != null ? shadowTm.GetTile(new Vector3Int(-10, -13, 0)) : null;
            TileBase westShadowTile = shadowTm != null ? shadowTm.GetTile(new Vector3Int(-22, 2, 0)) : null;
            TileBase eastShadowTile = shadowTm != null ? shadowTm.GetTile(new Vector3Int(21, 2, 0)) : null;

            // A) Limpiar el antiguo Muro Este (X = 20..22, Y = -14..12) y el antiguo Muro Sur (X = -22..22, Y = -14..-12)
            for (int x = 20; x <= 22; x++)
            {
                for (int y = -14; y <= 12; y++)
                {
                    wallTm.SetTile(new Vector3Int(x, y, 0), null);
                    if (shadowTm != null) shadowTm.SetTile(new Vector3Int(x, y, 0), null);
                }
            }
            for (int x = -22; x <= 22; x++)
            {
                for (int y = -14; y <= -12; y++)
                {
                    wallTm.SetTile(new Vector3Int(x, y, 0), null);
                    if (shadowTm != null) shadowTm.SetTile(new Vector3Int(x, y, 0), null);
                }
            }

            // B) Extender el Muro Norte hacia la derecha hasta X = 25 (Y = 12) con esquina recta en (-22, 12) y (25, 12)
            wallTm.SetTile(new Vector3Int(-22, 12, 0), nwCornerTile);
            for (int x = 18; x <= 24; x++)
            {
                if (northWallTile != null) wallTm.SetTile(new Vector3Int(x, 12, 0), northWallTile);
                if (shadowTm != null && northShadowTile != null) shadowTm.SetTile(new Vector3Int(x, 12, 0), northShadowTile);
            }
            wallTm.SetTile(new Vector3Int(25, 12, 0), neCornerTile);

            // C) Extender el Muro Oeste hacia abajo hasta Y = -16 (X = -22)
            for (int y = -16; y <= -11; y++)
            {
                if (westWallTile != null) wallTm.SetTile(new Vector3Int(-22, y, 0), westWallTile);
                if (shadowTm != null && westShadowTile != null) shadowTm.SetTile(new Vector3Int(-22, y, 0), westShadowTile);
            }

            // D) Dibujar el nuevo Muro Sur desplazado hacia abajo en Y = -17 (X = -22..25) con esquinas rectas
            wallTm.SetTile(new Vector3Int(-22, -17, 0), swCornerTile);
            for (int x = -21; x <= 24; x++)
            {
                if (southWallTile != null) wallTm.SetTile(new Vector3Int(x, -17, 0), southWallTile);
                if (shadowTm != null && southShadowTile != null) shadowTm.SetTile(new Vector3Int(x, -17, 0), southShadowTile);
            }
            wallTm.SetTile(new Vector3Int(25, -17, 0), seCornerTile);

            // E) Dibujar el nuevo Muro Este desplazado hacia la derecha en X = 25 (Y = -16..11)
            for (int y = -16; y <= 11; y++)
            {
                if (eastWallTile != null) wallTm.SetTile(new Vector3Int(25, y, 0), eastWallTile);
                if (shadowTm != null && eastShadowTile != null) shadowTm.SetTile(new Vector3Int(25, y, 0), eastShadowTile);
            }

            var compositeCol = wallObj.GetComponent<CompositeCollider2D>();
            if (compositeCol != null)
            {
                compositeCol.GenerateGeometry();
            }
        }

        // F) Mover la coronación superior del Muro Sur en "Layer 2 - Wall" (que estaba en Y=-12 cruzando el mapa) hacia Y=-16 (X=-22..25)
        if (layer2WallTm != null)
        {
            TileBase l2LeftCap = layer2WallTm.GetTile(new Vector3Int(-22, -12, 0));
            TileBase l2MidCap = layer2WallTm.GetTile(new Vector3Int(-10, -12, 0));
            TileBase l2RightCap = layer2WallTm.GetTile(new Vector3Int(21, -12, 0));
            if (l2LeftCap == null) l2LeftCap = l2MidCap;
            if (l2RightCap == null) l2RightCap = l2MidCap;

            // Borrar por completo la barda de Layer 2 - Wall que cruzaba el mapa en Y = -12
            for (int x = -24; x <= 26; x++)
            {
                for (int y = -14; y <= -10; y++)
                {
                    layer2WallTm.SetTile(new Vector3Int(x, y, 0), null);
                }
            }

            // Dibujar la coronación superior sobre el nuevo Muro Sur en Y = -16 (X = -22..25) con esquinas rectas
            if (l2MidCap != null)
            {
                layer2WallTm.SetTile(new Vector3Int(-22, -16, 0), l2LeftCap);
                for (int x = -21; x <= 24; x++)
                {
                    layer2WallTm.SetTile(new Vector3Int(x, -16, 0), l2MidCap);
                }
                layer2WallTm.SetTile(new Vector3Int(25, -16, 0), l2RightCap);
            }
        }

        // Actualizar los colisionadores perimetrales en Boundaries para coincidir con el mapa ampliado hacia abajo y a la derecha
        GameObject boundariesObj = GameObject.Find("Boundaries");
        if (boundariesObj != null)
        {
            Transform bardaSur = boundariesObj.transform.Find("Barda sur");
            if (bardaSur != null)
            {
                bardaSur.localPosition = new Vector3(1.5f, -16.3f, 0f);
                var bc = bardaSur.GetComponent<BoxCollider2D>();
                if (bc != null) bc.size = new Vector2(52f, 0.5f);
            }
            Transform bardaEste = boundariesObj.transform.Find("Barda este");
            if (bardaEste != null)
            {
                bardaEste.localPosition = new Vector3(25.7f, -2.0f, 0f);
                var bc = bardaEste.GetComponent<BoxCollider2D>();
                if (bc != null) bc.size = new Vector2(0.5f, 32f);
            }
            Transform bardaNorte = boundariesObj.transform.Find("Barda norte");
            if (bardaNorte != null)
            {
                bardaNorte.localPosition = new Vector3(1.5f, 12.2f, 0f);
                var bc = bardaNorte.GetComponent<BoxCollider2D>();
                if (bc != null) bc.size = new Vector2(52f, 0.5f);
            }
            Transform bardaOesteSur = boundariesObj.transform.Find("Barda oeste sur");
            if (bardaOesteSur != null)
            {
                bardaOesteSur.localPosition = new Vector3(-21.7f, -11.5f, 0f);
                var bc = bardaOesteSur.GetComponent<BoxCollider2D>();
                if (bc != null) bc.size = new Vector2(0.5f, 12.5f);
            }
        }

        // 4. Reubicar los 3 Altares de Encuentro y sus Objetos Rituales (con tamaño más compacto 0.68x) a las plazas despejadas
        foreach (var zone in Object.FindObjectsOfType<ToyEncounterZone>(true))
        {
            if (zone == null) continue;

            Vector3 newAltarPos = zone.encounterIndex == 0
                ? altar1Pos
                : (zone.encounterIndex == 1 ? altar2Pos : altar3Pos);

            zone.transform.position = newAltarPos;

            if (zone.targetToy != null)
            {
                zone.targetToy.toyId = Mathf.Clamp(zone.encounterIndex, 0, 2);
                zone.targetToy.ApplyLevel3VisualsIfNeeded();
                zone.targetToy.transform.localScale = new Vector3(0.68f, 0.68f, 1f);

                if (!zone.targetToy.transform.IsChildOf(zone.transform))
                {
                    zone.targetToy.transform.position = newAltarPos + new Vector3(0f, 0.55f, 0f);
                }
                else
                {
                    zone.targetToy.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                }
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

            // Nunca tocar ni desactivar "Acceso oeste" ni sus padres ("Decoration", "Props", "SCENE") ni sus hijos
            if (accesoOesteObj != null &&
                (t.gameObject == accesoOesteObj ||
                 t.IsChildOf(accesoOesteObj.transform) ||
                 accesoOesteObj.transform.IsChildOf(t)))
            {
                if (n.Contains("Pillar") && pillarTpl == null)
                {
                    pillarTpl = t.gameObject;
                    var sr = t.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null) { defaultPropMat = sr.sharedMaterial; nightTint = sr.color; }
                }
                else if (n.Contains("Bush") && bushTpl == null)
                {
                    bushTpl = t.gameObject;
                }
                continue;
            }

            // Nunca desactivar carpetas organizadoras sin SpriteRenderer propio (Decoration, Statues, Bench , Plant, Props)
            if (n == "Decoration" || n == "Statues" || n == "Bench " || n == "Plant" || n == "Props" || n == "Panteones familiares" || n == "SCENE")
            {
                continue;
            }

            if (n.Contains("Pillar") && pillarTpl == null)
            {
                pillarTpl = t.gameObject;
                var sr = t.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) { defaultPropMat = sr.sharedMaterial; nightTint = sr.color; }
            }

            bool isSceneProp = t.parent != null &&
                               (t.parent.name == "Panteones familiares" ||
                                t.parent.name == "Props" ||
                                t.parent.name == "Decoration" ||
                                t.parent.name == "Statues" ||
                                t.parent.name == "Bench " ||
                                t.parent.name == "Plant");
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
            bool inInterior = (p.x > -15.8f && p.x < 24.8f && p.y > -16.8f && p.y < 10.6f) ||
                              (p.y <= -9.5f && p.x > -20.5f) ||
                              (p.x >= 18.5f && p.y < 10.6f);
            bool inNorthGateCorridor = (p.x >= -3.8f && p.x <= 3.8f && p.y >= 7.0f);
            if (inInterior || inNorthGateCorridor)
            {
                interiorPropsToClear.Add(t.gameObject);
            }
        }

        GameObject crate1Pf = null, crate2Pf = null, barrelPf = null, chestPf = null, pot1Pf = null, pot2Pf = null, lanternPf = null, stoneCubePf = null, runePillarPf = null;
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
        runePillarPf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Props/PF Props - Rune Pillar X3.prefab");
        if (runePillarPf == null)
            runePillarPf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Props/PF Props - Rune Pillar X2.prefab");
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
            new OccupiedZone(new Vector2(-19.5f, -4.8f), 3.6f)  // Puerta Oeste de Inicio (Acceso Oeste)
        };

        // 7. CONSTRUCCIÓN DEL SANTUARIO Y PILAR DE LUZ CERCA DEL PRIMER ALTAR (en (-10.5, -1.2))
        BuildRunePillarShrine(
            mapRoot.transform,
            obstaclesLayer,
            defaultPropMat,
            nightTint,
            pillarTpl,
            stoneCubePf,
            lanternPf,
            runePillarPf,
            occupied);

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

        // 9. ARBOLEDA ABUNDANTE POR TODO EL PANTEÓN (Incluyendo los nuevos sectores ampliados Sur y Este)
        Vector2[] abundantTreePositions = new Vector2[]
        {
            // Arboleda Noroeste
            new Vector2(-15.8f,  5.2f), new Vector2(-15.8f,  1.2f), new Vector2(-15.8f, -2.2f),
            new Vector2(-14.2f, -8.2f), new Vector2( -4.5f,  8.8f), new Vector2( -4.5f,  1.2f),
            // Flanqueo del Camino Central (con paso amplio libre)
            new Vector2(-3.4f,  6.8f), new Vector2( 3.4f,  6.8f),
            new Vector2(-3.4f, -1.8f), new Vector2( 3.4f, -1.8f),
            new Vector2(-3.4f, -9.8f), new Vector2( 3.4f, -9.8f),
            // Arboleda Este y alrededor del Almacén
            new Vector2( 6.5f,  8.8f), new Vector2( 9.5f,  8.8f),
            new Vector2(10.2f,  1.5f), new Vector2(18.5f,  1.8f),
            new Vector2(18.5f, -7.8f), new Vector2( 6.5f, -8.2f),
            // Nueva Arboleda en el Sector Este Ampliado (X = 22.8..23.6)
            new Vector2(23.2f,  6.2f), new Vector2(23.2f,  1.2f),
            new Vector2(23.2f, -3.8f), new Vector2(23.2f, -9.2f),
            // Nueva Arboleda en el Sector Sur Ampliado (Y = -14.8..-15.2)
            new Vector2(-15.2f, -14.8f), new Vector2(-9.5f, -15.0f),
            new Vector2( -4.2f, -15.0f), new Vector2( 4.2f, -15.0f),
            new Vector2( 10.5f, -15.0f), new Vector2(16.8f, -15.0f)
        };
        for (int i = 0; i < abundantTreePositions.Length; i++)
        {
            GameObject tpl = (i % 3 == 0) ? tree1Tpl : ((i % 3 == 1) ? tree2Tpl : tree3Tpl);
            TrySpawnMapProp(tpl, abundantTreePositions[i], 1.45f, mapRoot.transform, $"Arbol_Panteon_{i + 1}", obstaclesLayer, defaultPropMat, nightTint, occupied);
        }

        // 10. POBLAR CON PREFABS: HILERAS DE TUMBAS, CRIPTAS Y ATAÚDES (Incluyendo zonas Sur y Este ampliadas)
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

            // LADO IZQUIERDO - Hileras Suroeste y Nuevo Sector Sur Izquierdo Ampliado
            new Vector2(-14.8f, -8.8f), new Vector2(-12.6f, -8.8f), new Vector2( -8.2f, -8.8f),
            new Vector2( -6.0f, -8.8f), new Vector2( -3.8f, -8.8f),
            new Vector2(-14.8f, -10.8f), new Vector2(-12.6f, -10.8f), new Vector2(-8.2f, -10.8f), new Vector2(-5.8f, -10.8f),
            new Vector2(-13.5f, -14.2f), new Vector2(-11.2f, -14.2f), new Vector2(-7.5f, -14.2f), new Vector2(-5.2f, -14.2f),

            // LADO DERECHO - Jardín Funerario Central
            new Vector2( 4.6f,  3.2f), new Vector2( 6.8f,  3.2f), new Vector2( 9.0f,  3.2f),
            new Vector2( 4.6f,  1.2f), new Vector2( 9.0f,  1.2f),
            new Vector2( 4.6f, -0.8f), new Vector2( 6.8f, -0.8f), new Vector2( 9.0f, -0.8f),

            // LADO DERECHO - Panteón Sureste y Nuevo Sector Sur Derecho Ampliado
            new Vector2( 6.4f, -7.2f), new Vector2( 8.6f, -7.2f), new Vector2(13.8f, -7.2f), new Vector2(16.2f, -7.2f),
            new Vector2( 6.4f, -9.0f), new Vector2( 8.6f, -9.0f), new Vector2(11.2f, -9.0f), new Vector2(13.8f, -9.0f), new Vector2(16.2f, -9.0f),
            new Vector2( 6.4f, -10.8f), new Vector2( 8.6f, -10.8f), new Vector2(11.2f, -10.8f), new Vector2(13.8f, -10.8f), new Vector2(16.2f, -10.8f),
            new Vector2( 6.4f, -14.2f), new Vector2( 8.8f, -14.2f), new Vector2(12.2f, -14.2f), new Vector2(14.8f, -14.2f), new Vector2(18.5f, -14.2f),

            // NUEVO SECTOR ESTE AMPLIADO (X = 19..23)
            new Vector2(19.5f,  8.2f), new Vector2(21.8f,  8.2f),
            new Vector2(22.4f,  4.2f), new Vector2(22.4f, -0.8f),
            new Vector2(22.4f, -6.5f), new Vector2(22.4f, -10.8f)
        };
        for (int i = 0; i < tombPositions.Length; i++)
        {
            GameObject tpl = gravePool[i % gravePool.Length];
            TrySpawnMapProp(tpl, tombPositions[i], 0.95f, mapRoot.transform, $"Tumba_Panteon_{i + 1}", obstaclesLayer, defaultPropMat, nightTint, occupied);
        }

        // 11. ESTATUAS MONUMENTALES DEL PANTEÓN (5 Estatuas)
        if (statuePf != null)
        {
            Vector2[] statueSpots = new Vector2[]
            {
                new Vector2(-7.8f,  7.5f),  // Plaza Memorial Noroeste
                new Vector2(-14.2f, -6.5f), // Cripta Antigua Suroeste
                new Vector2( 7.5f, -6.5f),  // Memorial Sureste
                new Vector2(10.5f,  3.5f),  // Entrada al sector del Almacén
                new Vector2(21.2f, -3.2f)   // Paseo Oriental Ampliado
            };
            for (int i = 0; i < statueSpots.Length; i++)
            {
                TrySpawnMapProp(statuePf, statueSpots[i], 1.25f, mapRoot.transform, $"Estatua_Panteon_{i + 1}", obstaclesLayer, defaultPropMat, nightTint, occupied);
            }
        }

        // 12. BANCAS DE PIEDRA DISTRIBUIDAS JUNTO A LOS CAMINOS (11 Bancas)
        Vector2[] benchSpots = new Vector2[]
        {
            new Vector2(-5.2f,  7.2f),
            new Vector2( 5.2f,  7.2f),
            new Vector2(-5.2f, -3.2f),
            new Vector2( 5.2f, -3.2f),
            new Vector2( 8.4f,  7.2f),
            new Vector2( 8.4f, -3.2f),
            new Vector2(-11.5f, 1.2f),
            new Vector2(15.2f, -3.2f),
            new Vector2(18.5f, -3.2f),
            new Vector2(-3.2f, -11.8f),
            new Vector2( 3.2f, -11.8f)
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

        // Desactivar props interiores obsoletos del nivel anterior (nunca Acceso oeste)
        for (int i = 0; i < interiorPropsToClear.Count; i++)
        {
            if (interiorPropsToClear[i] != null)
            {
                interiorPropsToClear[i].SetActive(false);
            }
        }

        // Volver a garantizar que Acceso Oeste siga activo tras limpiar props interiores
        RestoreAndOpenWestEntranceGate();

        // 15. POBLAR EL MAPA CON MÁS ENEMIGOS PATRULLEROS (Incluyendo Duendes con Aura Verde)
        //     Alternando entre Duendes Fuertes (Chubby), Arqueros Blindados (Archer) y Duendes Base (Normal),
        //     con mayor proporción de Duendes Fuertes que Arqueros y Duendes Base.
        SpawnAndConfigureLevel3PatrolEnemies(mapRoot.transform);

        // Reconstruir malla de navegación con el nuevo diseño
        GoblinNavigation2D nav = Object.FindObjectOfType<GoblinNavigation2D>();
        if (nav != null)
        {
            nav.SendMessage("Awake", SendMessageOptions.DontRequireReceiver);
        }
    }

    private static void SpawnAndConfigureLevel3PatrolEnemies(Transform mapRoot)
    {
        GoblinAI[] existingGoblins = Object.FindObjectsOfType<GoblinAI>(false);
        GameObject goblinTemplate = null;

        for (int i = 0; i < existingGoblins.Length; i++)
        {
            GoblinAI g = existingGoblins[i];
            if (g == null) continue;
            if (goblinTemplate == null) goblinTemplate = g.gameObject;

            GoblinVariantConfig cfg = g.GetComponent<GoblinVariantConfig>();
            if (cfg == null) cfg = g.gameObject.AddComponent<GoblinVariantConfig>();

            // Primeros dos: uno Fuerte (Chubby) con Aura Verde y un Arquero Blindado (Archer)
            if (i % 2 == 0)
            {
                cfg.SetVariant(true, false, true); // Duende Fuerte con Aura Verde (110 HP, igual que un arquero)
            }
            else
            {
                cfg.SetVariant(false, true, false); // Duende Arquero Blindado (110 HP, rápido, arco/cuchillo)
            }
        }

        if (goblinTemplate == null)
        {
#if UNITY_EDITOR
            goblinTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/PF_Goblin.prefab");
#endif
            if (goblinTemplate == null) goblinTemplate = Resources.Load<GameObject>("Prefabs/Enemies/PF_Goblin");
        }
        if (goblinTemplate == null) return;

        // Puntos estratégicos de patrulla por todo el panteón del Nivel 3 (incluyendo sectores Sur y Este ampliados)
        // Secuencia alternada con predominio de Duendes Fuertes (1 = Fuerte/Chubby, 2 = Arquero Blindado, 0 = Base)
        // y mezcla de enemigos con Aura Verde (más resistentes según su tipo):
        Vector2[] extraPatrolPositions = new Vector2[]
        {
            new Vector2(-10.5f,  2.6f),  // 0: Duende Fuerte con Aura Verde (Sendero Noroeste)
            new Vector2( -5.2f,  5.5f),  // 1: Duende Arquero con Aura Verde (Calzada Superior Izquierda)
            new Vector2(  0.0f, -8.2f),  // 2: Duende Fuerte (Camino Central Sur)
            new Vector2(  0.0f, -1.5f),  // 3: Duende Base con Aura Verde (Camino Central Medio)
            new Vector2(  5.8f, -4.5f),  // 4: Duende Fuerte con Aura Verde (Calzada Derecha hacia Altar 2)
            new Vector2( 14.5f, -1.2f),  // 5: Duende Arquero Blindado (Acceso al Almacén del Panteón)
            new Vector2( 14.8f,  6.2f),  // 6: Duende Fuerte (Interior del Almacén del Panteón)
            new Vector2(  5.5f,  5.6f),  // 7: Duende Base (Pasillo Noreste)
            new Vector2( 20.5f, -4.5f),  // 8: Duende Arquero con Aura Verde (Nuevo Paseo Oriental Ampliado)
            new Vector2(  0.0f, -12.8f)  // 9: Duende Fuerte con Aura Verde (Nueva Calzada Sur Ampliada)
        };

        int[] extraPatrolTypes = new int[] { 1, 2, 1, 0, 1, 2, 1, 0, 2, 1 };
        bool[] extraPatrolGreenAura = new bool[] { true, true, false, true, true, false, false, false, true, true };

        GoblinAwarenessUI awarenessUI = Object.FindObjectOfType<GoblinAwarenessUI>();

        for (int i = 0; i < extraPatrolPositions.Length; i++)
        {
            Vector3 spawnPos = new Vector3(extraPatrolPositions[i].x, extraPatrolPositions[i].y, 0f);
            GameObject newGoblin = Object.Instantiate(goblinTemplate, spawnPos, Quaternion.identity, mapRoot);
            newGoblin.name = $"Goblin_Patrulla_N3_{i + 1}";
            newGoblin.SetActive(true);

            EnemyHealth eh = newGoblin.GetComponent<EnemyHealth>();
            if (eh == null) eh = newGoblin.AddComponent<EnemyHealth>();

            GoblinVariantConfig cfg = newGoblin.GetComponent<GoblinVariantConfig>();
            if (cfg == null) cfg = newGoblin.AddComponent<GoblinVariantConfig>();

            bool isChubby = extraPatrolTypes[i] == 1;
            bool isArcher = extraPatrolTypes[i] == 2;
            bool hasGreenAura = extraPatrolGreenAura[i];
            cfg.SetVariant(isChubby, isArcher, hasGreenAura);

            GoblinAI ai = newGoblin.GetComponent<GoblinAI>();
            if (ai != null)
            {
                ai.waitForFirstCoin = false;
                ai.enablePatrol = true;
                ai.patrolRadius = 2.6f;

                if (isArcher)
                {
                    ai.moveSpeed = 1.85f;
                    ai.patrolSpeed = 1.15f;
                    ai.secondCoinSpeed = 2.40f;
                    var archerCtrl = newGoblin.GetComponent<GoblinArcherController>();
                    if (archerCtrl != null)
                    {
                        ai.stopDistance = archerCtrl.idealRangedStopDistance;
                        ai.attackRange = 0.25f;
                        ai.enableLungeAttack = false;
                    }
                }

                if (awarenessUI != null)
                {
                    awarenessUI.Track(ai);
                }
            }
        }
    }

    /// <summary>
    /// Construye el Santuario de Piedra con el Pilar de Luz (Rune Pillar) cerca del Primer Altar (en (-10.5, -1.2)):
    /// - Estructura abierta al sur hacia el Primer Altar con 4 columnas esquineras, muretes laterales/traseros y faroles de piedra.
    /// - En el centro se alza el Pilar Rúnico (PF Props - Rune Pillar X3) con RunePillarLanternUpgrade, que se ilumina de azul
    ///   al superar la primera oleada y otorga al Farol el ataque en radio de 360° con el daño del Cuchillo (28 DMG).
    /// </summary>
    private static void BuildRunePillarShrine(
        Transform parent,
        int obstaclesLayer,
        Material propMat,
        Color nightTint,
        GameObject pillarTpl,
        GameObject stoneCubePf,
        GameObject lanternPf,
        GameObject runePillarPf,
        List<OccupiedZone> occupied)
    {
        GameObject shrineRoot = new GameObject("Santuario_Pilar_De_Luz");
        shrineRoot.transform.SetParent(parent, false);
        shrineRoot.transform.position = Vector3.zero;

        Vector2 shrineCenter = new Vector2(-10.5f, -1.2f);
        occupied.Add(new OccupiedZone(shrineCenter, 2.75f));

        GameObject wallBlock = stoneCubePf != null ? stoneCubePf : pillarTpl;

        // 1. 4 Columnas esquineras de la construcción del santuario
        if (pillarTpl != null)
        {
            Vector2[] cornerPillars = new Vector2[]
            {
                new Vector2(-12.2f,  0.1f), // Noroeste
                new Vector2( -8.8f,  0.1f), // Noreste
                new Vector2(-12.2f, -2.3f), // Suroeste
                new Vector2( -8.8f, -2.3f)  // Sureste
            };
            for (int i = 0; i < cornerPillars.Length; i++)
            {
                GameObject p = SpawnWarehouseElement(pillarTpl, cornerPillars[i], shrineRoot.transform, $"Pilar_Santuario_{i + 1}", obstaclesLayer, propMat, nightTint);
                if (p != null) p.transform.localScale = new Vector3(0.76f, 0.76f, 1f);
                occupied.Add(new OccupiedZone(cornerPillars[i], 0.65f));
            }
        }

        // 2. Muretes de piedra traseros y laterales (dejando la entrada sur hacia el Primer Altar despejada)
        if (wallBlock != null)
        {
            Vector2[] shrineWalls = new Vector2[]
            {
                new Vector2(-11.3f,  0.1f),
                new Vector2( -9.7f,  0.1f),
                new Vector2(-12.2f, -1.1f),
                new Vector2( -8.8f, -1.1f)
            };
            for (int i = 0; i < shrineWalls.Length; i++)
            {
                SpawnWarehouseElement(wallBlock, shrineWalls[i], shrineRoot.transform, $"Muro_Santuario_{i + 1}", obstaclesLayer, propMat, nightTint);
                occupied.Add(new OccupiedZone(shrineWalls[i], 0.60f));
            }
        }

        // 3. Faroles de piedra flanqueando la entrada sur hacia el Primer Altar
        if (lanternPf != null)
        {
            SpawnWarehouseElement(lanternPf, new Vector2(-11.4f, -2.35f), shrineRoot.transform, "Farol_Santuario_Oeste", obstaclesLayer, propMat, Color.white);
            SpawnWarehouseElement(lanternPf, new Vector2( -9.6f, -2.35f), shrineRoot.transform, "Farol_Santuario_Este", obstaclesLayer, propMat, Color.white);
        }

        // 4. Instanciar el Pilar de Luz (PF Props - Rune Pillar X3, idéntico a la imagen 2) en el centro de la construcción
        GameObject runePillarTemplate = runePillarPf != null ? runePillarPf : pillarTpl;
        if (runePillarTemplate != null)
        {
            GameObject runePillarObj = Object.Instantiate(runePillarTemplate, shrineRoot.transform);
            runePillarObj.name = "Pilar_De_Luz_Runico";
            runePillarObj.transform.position = new Vector3(shrineCenter.x, shrineCenter.y, 0f);
            runePillarObj.SetActive(true);
            runePillarObj.layer = obstaclesLayer;

            foreach (var sr in runePillarObj.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sortingLayerName = "Layer 1";
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
                if (sr.name.Contains("Shadow"))
                {
                    sr.sortingOrder = 1;
                }
                else if (sr.name.Contains("Glow"))
                {
                    sr.sortingOrder = 3;
                }
                else
                {
                    sr.sortingOrder = 2;
                }
            }

            if (runePillarObj.GetComponent<RunePillarLanternUpgrade>() == null)
            {
                runePillarObj.AddComponent<RunePillarLanternUpgrade>();
            }
        }
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

        if (name.Contains("Arbol") || template.name.Contains("Tree"))
        {
            EnsureTreeLowerCanopyOverlayOnLayer2(obj.transform);
        }

        return true;
    }

    private static readonly Dictionary<Sprite, Sprite> cachedTreeLowerCanopySprites = new Dictionary<Sprite, Sprite>();

    private static void EnsureTreeLowerCanopyOverlayOnLayer2(Transform treeTransform)
    {
        if (treeTransform == null) return;

        Transform lowerTransform = treeTransform.Find("Lower");
        if (lowerTransform == null) return;

        SpriteRenderer lowerSr = lowerTransform.GetComponent<SpriteRenderer>();
        if (lowerSr == null || lowerSr.sprite == null || lowerSr.sprite.texture == null) return;

        Transform existingOverlay = lowerTransform.Find("LowerCanopy_Layer2");
        if (existingOverlay != null)
        {
            SpriteRenderer existingSr = existingOverlay.GetComponent<SpriteRenderer>();
            if (existingSr != null)
            {
                existingSr.sortingLayerName = "Layer 2";
                existingSr.sortingOrder = 1;
                existingSr.sharedMaterial = lowerSr.sharedMaterial;
                existingSr.color = lowerSr.color;
            }
            return;
        }

        Sprite lowerSprite = lowerSr.sprite;
        Rect rect = lowerSprite.rect;
        float ppu = lowerSprite.pixelsPerUnit > 0f ? lowerSprite.pixelsPerUnit : 32f;

        // El tronco ocupa ~41% inferior del sprite Lower; el 59% superior es copa/follaje verde
        int cutPixels = Mathf.RoundToInt(rect.height * 0.41f);
        int topHeight = Mathf.RoundToInt(rect.height) - cutPixels;
        if (topHeight <= 4) return;

        if (!cachedTreeLowerCanopySprites.TryGetValue(lowerSprite, out Sprite canopyOverlaySprite) || canopyOverlaySprite == null)
        {
            Rect topRect = new Rect(rect.x, rect.y + cutPixels, rect.width, topHeight);
            float normPivotX = rect.width > 0f ? (lowerSprite.pivot.x / rect.width) : 0.5f;
            canopyOverlaySprite = Sprite.Create(
                lowerSprite.texture,
                topRect,
                new Vector2(normPivotX, 0f),
                ppu,
                0,
                SpriteMeshType.FullRect);
            canopyOverlaySprite.name = lowerSprite.name + "_LowerCanopy_Layer2";
            cachedTreeLowerCanopySprites[lowerSprite] = canopyOverlaySprite;
        }

        GameObject overlayObj = new GameObject("LowerCanopy_Layer2");
        overlayObj.layer = treeTransform.gameObject.layer;
        overlayObj.transform.SetParent(lowerTransform, false);
        overlayObj.transform.localPosition = new Vector3(0f, (cutPixels - lowerSprite.pivot.y) / ppu, 0f);
        overlayObj.transform.localRotation = Quaternion.identity;
        overlayObj.transform.localScale = Vector3.one;

        SpriteRenderer overlaySr = overlayObj.AddComponent<SpriteRenderer>();
        overlaySr.sprite = canopyOverlaySprite;
        overlaySr.sortingLayerName = "Layer 2";
        overlaySr.sortingOrder = 1;
        overlaySr.spriteSortPoint = SpriteSortPoint.Pivot;
        overlaySr.sharedMaterial = lowerSr.sharedMaterial;
        overlaySr.color = lowerSr.color;
        overlaySr.flipX = lowerSr.flipX;
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
