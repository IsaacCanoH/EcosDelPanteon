using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Tilemaps;

public class MapGeneratorEditor : EditorWindow
{
    Tilemap targetTilemap;
    TileBase floorTile;
    Vector2Int mapSize = new Vector2Int(30, 30);
    Vector2Int startPosition = new Vector2Int(0, 0);

    Transform propsParent;
    GameObject[] propPrefabs = new GameObject[1];
    int propCount = 30;

    private const string NorthLeafAssetPath = "Assets/Cainos/Pixel Art Top Down - Basic/Texture/TX Puerta Lateral Hoja.png";
    private const string SouthLeafAssetPath = "Assets/Cainos/Pixel Art Top Down - Basic/Texture/TX Puerta Lateral Hoja Sur.png";

    [MenuItem("Herramientas/Generador de Mapa")]
    public static void ShowWindow()
    {
        GetWindow<MapGeneratorEditor>("Generador de Mapa");
    }

    [MenuItem("Herramientas/Acomodar Puerta Lateral (Nivel 2)")]
    public static void FixLevel2WestGateMenu()
    {
        FixLevel2WestGate(true);
    }

    [InitializeOnLoadMethod]
    private static void AutoFixGateOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            GameObject accesoOeste = GameObject.Find("Acceso oeste");
            if (accesoOeste != null)
            {
                // Si todavía existe la puerta rotada a 90 grados o no están creadas las hojas laterales, acomodarla automáticamente
                Transform oldGate = accesoOeste.transform.Find("PF Props - Wooden Gate 01 Opened");
                Transform hojaNorte = accesoOeste.transform.Find("Puerta Lateral - Hoja Norte");
                if ((oldGate != null && oldGate.gameObject.activeSelf && Mathf.Abs(oldGate.localEulerAngles.z - 90f) < 5f) || hojaNorte == null)
                {
                    FixLevel2WestGate(false);
                }
            }

            // Eliminar cualquier campo de fuerza que hubiese quedado en la escena
            ForceFieldBarrier[] barriers = Object.FindObjectsOfType<ForceFieldBarrier>(true);
            foreach (var b in barriers)
            {
                if (b != null && b.gameObject.scene.IsValid())
                {
                    Object.DestroyImmediate(b.gameObject);
                }
            }

            // Asegurar que la cinemática de entrada exista en Cementerio_Nivel2
            if (GameObject.Find("IntroCinematicDirector") == null && GameObject.Find("Acceso oeste") != null)
            {
                GameObject introObj = new GameObject("IntroCinematicDirector");
                introObj.AddComponent<Level2IntroSequence>();
                Undo.RegisterCreatedObjectUndo(introObj, "Crear IntroCinematicDirector");
            }

            // Si estamos en Cementerio_Nivel2 y aún no existe la estructura de arenas, configurarla automáticamente
            if (GameObject.Find("ENCOUNTERS") == null)
            {
                ToyPickup[] toys = Object.FindObjectsOfType<ToyPickup>(true);
                if (toys != null && toys.Length > 0)
                {
                    SetupToyEncounterArenas(false);
                }
            }
        };
    }

    void OnGUI()
    {
        GUILayout.Label("1. Expandir Suelo (Tilemap)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Arrastra aquí el Tilemap (ej. 'Layer 1 - Grass') y el bloque de suelo que quieres pintar.", MessageType.Info);
        
        targetTilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap a expandir", targetTilemap, typeof(Tilemap), true);
        floorTile = (TileBase)EditorGUILayout.ObjectField("Tile a usar", floorTile, typeof(TileBase), false);
        
        startPosition = EditorGUILayout.Vector2IntField("Posición de Inicio (X,Y)", startPosition);
        mapSize = EditorGUILayout.Vector2IntField("Tamaño a expandir (Ancho, Alto)", mapSize);

        if (GUILayout.Button("Pintar Suelo", GUILayout.Height(30)))
        {
            PaintFloor();
        }

        GUILayout.Space(20);
        
        GUILayout.Label("2. Esparcir Objetos (Tumbas, Árboles, etc)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Arrastra aquí el Objeto 'Props' de tu jerarquía para agruparlos, y luego añade los Prefabs del asset Cainos.", MessageType.Info);
        
        propsParent = (Transform)EditorGUILayout.ObjectField("Objeto Padre ('Props')", propsParent, typeof(Transform), true);
        
        int arraySize = EditorGUILayout.IntField("Tipos de Prefabs distintos", propPrefabs.Length);
        if (arraySize != propPrefabs.Length)
        {
            System.Array.Resize(ref propPrefabs, Mathf.Max(0, arraySize));
        }

        for (int i = 0; i < propPrefabs.Length; i++)
        {
            propPrefabs[i] = (GameObject)EditorGUILayout.ObjectField("Prefab " + (i + 1), propPrefabs[i], typeof(GameObject), false);
        }

        propCount = EditorGUILayout.IntField("Cantidad total a generar", propCount);

        if (GUILayout.Button("Esparcir Objetos al Azar", GUILayout.Height(30)))
        {
            ScatterProps();
        }

        GUILayout.Space(20);
        GUILayout.Label("3. Reparar Entrada Oeste (Nivel 2)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Alinea los pilares con la barda lateral (X = -22) y coloca las dos hojas grandes de la puerta abiertas hacia adentro del mapa.", MessageType.Info);
        if (GUILayout.Button("Acomodar Puerta Lateral (Nivel 2)", GUILayout.Height(32)))
        {
            FixLevel2WestGate(true);
        }

        GUILayout.Space(15);
        GUILayout.Label("4. Configurar Arenas de los 3 Juguetes (Fase 3 y 4)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Crea automáticamente los Triggers, Spawners de Teleportación y Oleadas en los 3 juguetes del Nivel 2.", MessageType.Info);
        if (GUILayout.Button("Configurar Arenas de Combate (Nivel 2)", GUILayout.Height(34)))
        {
            SetupToyEncounterArenas(true);
        }

        GUILayout.Space(15);
        GUILayout.Label("5. Reconstrucción Total de Nivel 2 (Desde Copia Nivel 1)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Limpia elementos de monedas del Nivel 1, coloca los 3 juguetes, las 3 zonas de combate dinámicas con horda y Jefe Niño Espectral, y configura el HUD y armas ([1] Farol, [2] Puños, [3] Cuchillo, [4] Machete).", MessageType.Info);
        if (GUILayout.Button("★ Reconstruir Nivel 2 Completo ★", GUILayout.Height(40)))
        {
            Level2SceneBuilder.BuildLevel2(true);
        }
    }

    private static Sprite EnsureGateSpriteImported(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
            if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            if (Mathf.Abs(importer.spritePixelsPerUnit - 32f) > 0.01f) { importer.spritePixelsPerUnit = 32f; changed = true; }

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteAlignment != (int)SpriteAlignment.Custom || Vector2.Distance(settings.spritePivot, new Vector2(0f, 0.14f)) > 0.01f)
            {
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(0f, 0.14f);
                importer.SetTextureSettings(settings);
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    public static void FixLevel2WestGate(bool showDialog)
    {
        GameObject accesoOeste = GameObject.Find("Acceso oeste");
        if (accesoOeste == null)
        {
            if (showDialog)
                EditorUtility.DisplayDialog("Aviso", "No se encontró el objeto 'Acceso oeste' en la escena actual. Abre la escena Cementerio_Nivel2.", "OK");
            return;
        }

        AssetDatabase.Refresh();
        Sprite northSprite = EnsureGateSpriteImported(NorthLeafAssetPath);
        Sprite southSprite = EnsureGateSpriteImported(SouthLeafAssetPath);

        if (northSprite == null || southSprite == null)
        {
            if (showDialog)
                EditorUtility.DisplayDialog("Error", "No se encontraron las texturas de las hojas laterales en Assets/Cainos/Pixel Art Top Down - Basic/Texture/.", "OK");
            return;
        }

        Transform parent = accesoOeste.transform;
        Undo.RegisterFullObjectHierarchyUndo(accesoOeste, "Acomodar Puerta Lateral Nivel 2");

        // 1. Alinear los dos pilares a la barda lateral (X = -21.85) y escalarlos a tamaño de pórtico grande
        Material propsMat = null;
        int sortingLayerID = 0;
        Color nightTint = new Color(0.6117647f, 0.6666667f, 0.78431374f, 1f);

        var pillars = new System.Collections.Generic.List<Transform>();
        var bushes = new System.Collections.Generic.List<Transform>();

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name.Contains("Pillar"))
            {
                pillars.Add(child);
                SpriteRenderer sr = child.GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    propsMat = sr.sharedMaterial;
                    sortingLayerID = sr.sortingLayerID;
                    nightTint = sr.color;
                }
            }
            else if (child.name.Contains("Bush"))
            {
                bushes.Add(child);
            }
            else if (child.name.Contains("Wooden Gate"))
            {
                // Desactivar la puerta vieja que estaba acostada a 90° en medio del camino
                child.gameObject.SetActive(false);
            }
        }

        // Ordenar pilares: el que tenga mayor Y es el superior, el de menor Y es el inferior
        if (pillars.Count >= 2)
        {
            pillars.Sort((a, b) => b.localPosition.y.CompareTo(a.localPosition.y));
            Transform topPillar = pillars[0];
            Transform bottomPillar = pillars[1];

            // Pegados a las bardas laterales (X = -21.85 conecta directo con el muro en X = -22)
            topPillar.localPosition = new Vector3(-21.85f, -3.15f, 0f);
            topPillar.localScale = new Vector3(0.85f, 0.85f, 1f);
            SpriteRenderer topSr = topPillar.GetComponent<SpriteRenderer>();
            if (topSr != null) topSr.sortingOrder = 315;

            bottomPillar.localPosition = new Vector3(-21.85f, -6.75f, 0f);
            bottomPillar.localScale = new Vector3(0.85f, 0.85f, 1f);
            SpriteRenderer bottomSr = bottomPillar.GetComponent<SpriteRenderer>();
            if (bottomSr != null) bottomSr.sortingOrder = 675;
        }

        // Reacomodar arbustos para que enmarquen las hojas abiertas sin taparlas
        if (bushes.Count >= 4)
        {
            bushes.Sort((a, b) => b.localPosition.y.CompareTo(a.localPosition.y));
            // Dos arbustos superiores
            bushes[0].localPosition = new Vector3(-19.4f, -2.55f, 0f);
            bushes[1].localPosition = new Vector3(-17.9f, -2.65f, 0f);
            // Dos arbustos inferiores
            bushes[2].localPosition = new Vector3(-19.4f, -6.95f, 0f);
            bushes[3].localPosition = new Vector3(-17.9f, -6.95f, 0f);
        }

        // 2. Crear o actualizar las 2 hojas grandes abiertas hacia adentro del mapa
        CreateOrUpdateGateLeaf(
            parent,
            "Puerta Lateral - Hoja Norte",
            northSprite,
            new Vector3(-21.52f, -3.05f, 0f),
            new Vector3(1.18f, 1.22f, 1f),
            propsMat,
            sortingLayerID,
            325,
            nightTint,
            accesoOeste.layer
        );

        CreateOrUpdateGateLeaf(
            parent,
            "Puerta Lateral - Hoja Sur",
            southSprite,
            new Vector3(-21.52f, -6.68f, 0f),
            new Vector3(1.18f, 1.22f, 1f),
            propsMat,
            sortingLayerID,
            685,
            nightTint,
            accesoOeste.layer
        );

        EditorUtility.SetDirty(accesoOeste);
        EditorSceneManager.MarkSceneDirty(accesoOeste.scene);
        Debug.Log("Puerta lateral de Nivel 2 acomodada: pilares pegados a las bardas laterales y hojas grandes abiertas hacia adentro.");

        if (showDialog)
        {
            EditorUtility.DisplayDialog("Puerta Lateral Actualizada", "Se alinearon los pilares a las bardas laterales y se colocaron las dos hojas grandes abiertas hacia el interior del mapa.", "Excelente");
        }
    }

    private static void CreateOrUpdateGateLeaf(
        Transform parent,
        string leafName,
        Sprite sprite,
        Vector3 localPos,
        Vector3 localScale,
        Material mat,
        int sortingLayerID,
        int sortingOrder,
        Color colorTint,
        int layer)
    {
        Transform existing = parent.Find(leafName);
        GameObject leafObj;
        if (existing != null)
        {
            leafObj = existing.gameObject;
        }
        else
        {
            leafObj = new GameObject(leafName);
            Undo.RegisterCreatedObjectUndo(leafObj, "Crear Hoja Puerta Lateral");
            leafObj.transform.SetParent(parent, false);
        }

        leafObj.layer = layer;
        leafObj.transform.localPosition = localPos;
        leafObj.transform.localRotation = Quaternion.identity;
        leafObj.transform.localScale = localScale;

        SpriteRenderer sr = leafObj.GetComponent<SpriteRenderer>();
        if (sr == null) sr = leafObj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        if (mat != null) sr.sharedMaterial = mat;
        if (sortingLayerID != 0) sr.sortingLayerID = sortingLayerID;
        sr.sortingOrder = sortingOrder;
        sr.color = colorTint;
        sr.spriteSortPoint = SpriteSortPoint.Pivot;

        BoxCollider2D col = leafObj.GetComponent<BoxCollider2D>();
        if (col == null) col = leafObj.AddComponent<BoxCollider2D>();
        col.offset = new Vector2(0.75f, 0.18f);
        col.size = new Vector2(1.35f, 0.28f);
    }

    void PaintFloor()
    {
        if (targetTilemap == null || floorTile == null)
        {
            EditorUtility.DisplayDialog("Error", "Falta asignar el Tilemap o el Tile de suelo.", "OK");
            return;
        }

        // Permite usar Ctrl+Z para deshacer
        Undo.RecordObject(targetTilemap, "Pintar Suelo");

        for (int x = 0; x < mapSize.x; x++)
        {
            for (int y = 0; y < mapSize.y; y++)
            {
                Vector3Int pos = new Vector3Int(startPosition.x + x, startPosition.y + y, 0);
                targetTilemap.SetTile(pos, floorTile);
            }
        }
        
        EditorUtility.SetDirty(targetTilemap);
        Debug.Log($"Se pintaron {mapSize.x * mapSize.y} tiles correctamente en {targetTilemap.name}.");
    }

    void ScatterProps()
    {
        if (propsParent == null)
        {
            EditorUtility.DisplayDialog("Error", "Falta asignar el Objeto Padre donde irán los props.", "OK");
            return;
        }
        if (propPrefabs.Length == 0 || propPrefabs[0] == null)
        {
            EditorUtility.DisplayDialog("Error", "Falta asignar al menos un Prefab.", "OK");
            return;
        }

        for (int i = 0; i < propCount; i++)
        {
            GameObject prefabToSpawn = propPrefabs[Random.Range(0, propPrefabs.Length)];
            if (prefabToSpawn == null) continue;

            // Calcular posición aleatoria basada en el área del suelo definido
            float randomX = Random.Range((float)startPosition.x, startPosition.x + mapSize.x);
            float randomY = Random.Range((float)startPosition.y, startPosition.y + mapSize.y);
            
            Vector3 spawnPos = new Vector3(randomX, randomY, 0);

            GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(prefabToSpawn, propsParent);
            newObj.transform.position = spawnPos;
            
            // Permite usar Ctrl+Z para deshacer la creación de los objetos
            Undo.RegisterCreatedObjectUndo(newObj, "Crear Prop Aleatorio");
        }
        
        Debug.Log($"Se generaron {propCount} props aleatoriamente dentro de {propsParent.name}.");
    }

    [MenuItem("Herramientas/Configurar Arenas de Combate Nivel 2")]
    public static void SetupToyEncounterArenasMenu()
    {
        SetupToyEncounterArenas(true);
    }

    public static void SetupToyEncounterArenas(bool showDialog)
    {
        ToyPickup[] allToys = Object.FindObjectsOfType<ToyPickup>(true);
        if (allToys == null || allToys.Length == 0)
        {
            if (showDialog)
                EditorUtility.DisplayDialog("Aviso", "No se encontraron juguetes (ToyPickup) en la escena actual. Asegúrate de abrir Cementerio_Nivel2.", "OK");
            return;
        }

        System.Array.Sort(allToys, (a, b) => a.toyId.CompareTo(b.toyId));

        GameObject encountersRoot = GameObject.Find("ENCOUNTERS");
        if (encountersRoot == null)
        {
            encountersRoot = new GameObject("ENCOUNTERS");
            Undo.RegisterCreatedObjectUndo(encountersRoot, "Crear Grupo ENCOUNTERS");
        }

        GameObject goblinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/PF_Goblin.prefab");

        int configuredCount = 0;
        for (int i = 0; i < allToys.Length; i++)
        {
            ToyPickup toy = allToys[i];
            int encId = Mathf.Clamp(toy.toyId, 0, 2);

            string zoneName = $"EncounterZone_Toy_{encId + 1}";
            Transform existingZone = encountersRoot.transform.Find(zoneName);
            GameObject zoneObj;
            if (existingZone != null)
            {
                zoneObj = existingZone.gameObject;
            }
            else
            {
                zoneObj = new GameObject(zoneName);
                Undo.RegisterCreatedObjectUndo(zoneObj, "Crear Zona de Encuentro");
                zoneObj.transform.SetParent(encountersRoot.transform, false);
            }

            zoneObj.transform.position = toy.transform.position;

            // Trigger circular alrededor del juguete
            CircleCollider2D col = zoneObj.GetComponent<CircleCollider2D>();
            if (col == null) col = zoneObj.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = encId == 2 ? 4.2f : 3.2f;

            // Manager de Oleadas
            EncounterWaveManager wm = zoneObj.GetComponent<EncounterWaveManager>();
            if (wm == null) wm = zoneObj.AddComponent<EncounterWaveManager>();
            wm.goblinPrefab = goblinPrefab;

            // Crear 4 spawners en círculo alrededor del área
            Transform spawnersParent = zoneObj.transform.Find("SpawnPoints");
            if (spawnersParent == null)
            {
                GameObject spObj = new GameObject("SpawnPoints");
                spObj.transform.SetParent(zoneObj.transform, false);
                spawnersParent = spObj.transform;
            }

            if (spawnersParent.childCount < 4)
            {
                for (int s = spawnersParent.childCount; s < 4; s++)
                {
                    GameObject sp = new GameObject($"SpawnPoint_{s + 1}");
                    sp.transform.SetParent(spawnersParent, false);
                    float angle = (s * 90f + 45f) * Mathf.Deg2Rad;
                    float dist = encId == 2 ? 6.2f : 5.0f;
                    sp.transform.localPosition = new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist, 0f);
                }
            }

            Transform[] spArray = new Transform[spawnersParent.childCount];
            for (int s = 0; s < spawnersParent.childCount; s++) spArray[s] = spawnersParent.GetChild(s);
            wm.spawnPoints = spArray;

            // Componente del Encuentro del Juguete
            ToyEncounterZone zoneComp = zoneObj.GetComponent<ToyEncounterZone>();
            if (zoneComp == null) zoneComp = zoneObj.AddComponent<ToyEncounterZone>();
            zoneComp.encounterIndex = encId;
            zoneComp.targetToy = toy;
            zoneComp.waveManager = wm;
            if (encId == 0) zoneComp.dropBigKnifeOnClear = true;
            zoneComp.AutoConfigureDefaultWavesIfEmpty();

            // Encuentro 3: Prefab del jefe (campo de fuerza eliminado para combate abierto)
            if (encId == 2)
            {
                GameObject bossPf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/PF_GhostChild.prefab");
                if (bossPf != null) zoneComp.bossPrefab = bossPf;

                Transform existingBarrier = zoneObj.transform.Find("ForceFieldBarrier");
                if (existingBarrier != null)
                {
                    Undo.DestroyObjectImmediate(existingBarrier.gameObject);
                }
                zoneComp.forceField = null;
            }

            configuredCount++;
        }

        // Desactivar cualquier Niño Fantasma antiguo que estuviera suelto en la escena (de la versión de sigilo anterior)
        // para que el Jefe Niño Espectral aparezca únicamente en el Encuentro 3.
        GhostChildAI[] legacyGhosts = Object.FindObjectsOfType<GhostChildAI>(true);
        foreach (var lg in legacyGhosts)
        {
            if (lg != null && lg.gameObject.scene.IsValid() && lg.gameObject.activeSelf)
            {
                Undo.RecordObject(lg.gameObject, "Desactivar Niño Fantasma Antiguo");
                lg.gameObject.SetActive(false);
            }
        }

        EditorUtility.SetDirty(encountersRoot);
        EditorSceneManager.MarkSceneDirty(encountersRoot.scene);
        Debug.Log($"[Arenas Nivel 2] Se configuraron exitosamente {configuredCount} zonas de encuentro con sus oleadas.");

        if (showDialog)
        {
            EditorUtility.DisplayDialog("Arenas Configuradas", $"Se configuraron las 3 zonas de combate en los juguetes:\n\n• Encuentro 1: 2 oleadas + La Llorona + Drop Cuchillo Grande.\n• Encuentro 2: 2 oleadas (4 normales + 3 gorditos).\n• Encuentro 3: Campo de fuerza + Gran oleada de 10 enemigos.", "Excelente");
        }
    }
}
