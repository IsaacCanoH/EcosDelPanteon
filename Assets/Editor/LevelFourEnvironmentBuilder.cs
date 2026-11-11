using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

// Herramienta de autoría: todo el escenario queda guardado como objetos editables.
public static class LevelFourEnvironmentBuilder
{
    private const string ScenePath = "Assets/Scenes/Cementerio_Nivel4.unity";
    private const string RootName = "NIVEL_4_Panteon_de_las_Animas";
    private const string Pack = "Assets/Cainos/Pixel Art Top Down - Basic/";
    private static readonly Dictionary<string, TileBase> Tiles = new Dictionary<string, TileBase>();
    private static System.Random random;
    private static Material lit;
    private static Transform root;
    private static Transform vegetation;
    private static Tilemap paving;
    private static Tilemap stone;
    private static Tilemap masonry;

    [MenuItem("Herramientas/Nivel 4/Construir propuesta de escenario")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != ScenePath)
            throw new InvalidOperationException("Abre Cementerio_Nivel4 fuera de Play antes de construir.");
        if (scene.GetRootGameObjects().Any(g => g.name == RootName))
            throw new InvalidOperationException("La propuesta ya existe. Edita sus grupos directamente o deshaz la creación antes de generar otra.");

        // Validar las dependencias antes de crear objetos en la escena.
        lit = AssetDatabase.LoadAssetAtPath<Material>("Assets/MT_Escenario_Luz2D.mat");
        if (lit == null) throw new InvalidOperationException("Falta el material de iluminación 2D del escenario.");
        Tile("TP Grass/TX Tileset Grass 0.asset");
        Tile("TP Wall/TX Tileset Wall_1.asset");
        random = new System.Random(41031);
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Construir escenario Nivel 4");
        root = new GameObject(RootName).transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Crear escenario Nivel 4");
        var terrain = Group("01_Terreno_y_caminos", root);
        var grid = new GameObject("Grid", typeof(Grid)).transform;
        grid.SetParent(terrain, false);
        grid.GetComponent<Grid>().cellSize = new Vector3(1, 1, 0);
        var grass = Map("Suelo_Hierba", grid, 0);
        paving = Map("Caminos_Empedrado", grid, 1);
        stone = Map("Plaza_y_atrios", grid, 1);
        masonry = Map("Bardas_y_mausoleos", grid, 2);

        var bounds = new BoundsInt(-28, -22, 0, 56, 44, 1);
        var fill = new TileBase[bounds.size.x * bounds.size.y];
        string[] flowers = {"2", "6", "12", "13"};
        for (int i = 0; i < fill.Length; i++)
            fill[i] = random.Next(100) < 9
                ? Tile("TP Grass/TX Tileset Grass Flower " + flowers[random.Next(flowers.Length)] + ".asset")
                : Tile("TP Grass/TX Tileset Grass 0.asset");
        grass.SetTilesBlock(bounds, fill);

        // Eje de entrada, cruz central y un circuito que evita callejones obligatorios.
        PathRect(-2, -22, 4, 35);
        PathRect(-24, -2, 48, 4);
        PathRect(-23, -13, 46, 3);
        PathRect(-23, 7, 46, 3);
        PathRect(-23, -13, 3, 26);
        PathRect(20, -13, 3, 26);
        StoneRect(-7, -7, 14, 14);
        StoneRect(-6, 9, 12, 3);
        StoneRect(-23, 9, 8, 3);
        StoneRect(15, 9, 8, 3);

        var architecture = Group("02_Arquitectura", root);
        Perimeter(architecture);
        Building("Capilla_de_las_Animas", -5, 11, 10, 6, architecture, true);
        Building("Casa_del_Sepulturero", -23, 11, 6, 5, architecture, false);
        Building("Mausoleo_de_los_Fundadores", 17, 11, 6, 5, architecture, false);

        var gardens = Group("03_Cuarteles_de_tumbas", root);
        GraveGarden("Tumbas_del_Ocaso", -17.5f, -8.5f, 4, 2, gardens, false);
        GraveGarden("Tumbas_del_Alba", 9f, -8.5f, 4, 2, gardens, false);
        GraveGarden("Sepulturas_Antiguas", -17.5f, 3f, 4, 2, gardens, true);
        GraveGarden("Sepulturas_de_los_Olvidados", 9f, 3f, 4, 2, gardens, true);
        // Los sarcófagos quedan al norte de los jardines; los accesos laterales siguen abiertos.
        var north = Group("Criptas_del_Norte", gardens);
        foreach (float x in new[] {-13.5f, -10f, 10f, 13.5f})
        {
            Prop("Stone Coffin 01 V", x, 12.2f, north);
            Prop("Gravestone 02", x, 15.5f, north);
        }

        var plaza = Group("04_Plaza_del_Ultimo_Rito", root);
        Prop("Altar 01", 0, 1.9f, plaza);
        foreach (float x in new[] {-5f, 5f})
        {
            Prop("Pillar 01", x, 4.6f, plaza);
            Prop("Stone Bench 01 S", x, -5.6f, plaza);
        }
        Prop("Rune Pillar Broken", -4.9f, -0.8f, plaza);
        Prop("Rune Pillar X2", 4.9f, -0.8f, plaza);
        Prop("Statue 01", -5.5f, 9.7f, plaza);
        Prop("Statue 01", 5.5f, 9.7f, plaza);

        var details = Group("05_Rincones_y_utileria", root);
        Prop("Well 01", -16f, -14.6f, details);
        Prop("Stone Bench 01 E", -19.2f, -14.6f, details);
        Prop("Pot 01", -18.1f, -15.5f, details);
        Prop("Stone Coffin 01 H", 15.7f, -14.7f, details);
        Prop("Statue 01", 19.2f, -14.4f, details);
        Prop("Pot 02", 12.5f, -15.2f, details);
        Prop("Barrel 01", -23.3f, 9.7f, details);
        Prop("Crate 01", -21.8f, 9.7f, details);
        Prop("Pot 03", 23.5f, 9.8f, details);
        Prop("Stone Bench 01 S", -10.8f, 9.3f, details);
        Prop("Stone Bench 01 S", 10.8f, 9.3f, details);
        Prop("Road Sign 01 E", 3.3f, -12.1f, details);

        vegetation = Group("06_Arboles_y_maleza", root);
        float[,] trees = {
            {-24,15.6f},{-16,14.7f},{-7.2f,15.1f},{7.2f,15.1f},{16,14.7f},{24,15.6f},
            {-24.1f,6},{24.1f,5.5f},{-24.1f,-3.8f},{24.1f,-4.5f},
            {-23.8f,-15.3f},{-9.5f,-15.8f},{9.5f,-15.8f},{23.5f,-15.4f},
            {-8.4f,4.5f},{8.2f,-8.8f},{-18.8f,-3.4f},{18.7f,3.2f}
        };
        for (int i = 0; i < trees.GetLength(0); i++)
            Plant("Tree 0" + (i % 3 + 1), trees[i, 0], trees[i, 1], vegetation);
        for (int i = 0; i < 46; i++)
        {
            bool left = i % 2 == 0;
            float x = left ? -24.2f + (float)random.NextDouble() * 0.8f : 23.4f + (float)random.NextDouble() * 0.8f;
            float y = -15.5f + (float)random.NextDouble() * 31f;
            Plant("Bush 0" + (i % 6 + 1), x, y, vegetation);
        }
        for (int i = 0; i < 56; i++)
        {
            int quadrant = i % 4;
            float x = (quadrant % 2 == 0 ? -19f : 8f) + (float)random.NextDouble() * 10f;
            float y = (quadrant < 2 ? -9.7f : 2.4f) + (float)random.NextDouble() * 3.7f;
            Plant("Grass " + (i % 15 + 1).ToString("00"), x, y, vegetation);
        }

        var lighting = Group("07_Iluminacion", root);
        var ambient = new GameObject("Luz_Ambiente_Azul").AddComponent<Light2D>();
        ambient.transform.SetParent(lighting, false);
        ambient.lightType = Light2D.LightType.Global;
        ambient.color = new Color(0.76f, 0.84f, 1f);
        ambient.intensity = 0.76f;
        LightLayers(ambient);
        Lantern(-2.7f, -16.4f, lighting);
        Lantern(2.7f, -16.4f, lighting);
        Lantern(-6f, -6f, lighting);
        Lantern(6f, -6f, lighting);
        Lantern(-6f, 5.7f, lighting);
        Lantern(6f, 5.7f, lighting);
        Lantern(-3.3f, 10f, lighting);
        Lantern(3.3f, 10f, lighting);
        Lantern(-19.7f, 10.1f, lighting);
        Lantern(20.1f, 10.1f, lighting);
        Lantern(-19.6f, -11.2f, lighting);
        Lantern(19.6f, -11.2f, lighting);
        // Una luz direccional mantiene la escena preparada para futuros elementos 3D.
        var sun = new GameObject("Luz_Direccional").AddComponent<Light>();
        sun.transform.SetParent(lighting, false);
        sun.type = LightType.Directional;
        sun.intensity = 0.15f;
        sun.color = new Color(0.7f, 0.8f, 1f);
        sun.transform.rotation = Quaternion.Euler(50, -30, 0);

        var gameplay = Group("08_Exploracion_y_referencias", root);
        var player = Prefab("Prefab/Player/PF Player.prefab", "Jugador_Exploracion", 0, -15.5f, gameplay);
        player.GetComponent<Cainos.PixelArtTopDown_Basic.TopDownCharacterController>().speed = 4f;
        player.GetComponent<Rigidbody2D>().gravityScale = 0;
        var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c => c.CompareTag("MainCamera"));
        if (camera == null)
        {
            camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            Undo.RegisterCreatedObjectUndo(camera.gameObject, "Crear cámara");
        }
        Undo.RecordObject(camera, "Configurar cámara Nivel 4");
        Undo.RecordObject(camera.transform, "Posicionar cámara Nivel 4");
        camera.orthographic = true;
        camera.orthographicSize = 6.8f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.05f, 0.065f);
        camera.transform.position = new Vector3(0, -13.6f, -10);
        camera.transparencySortMode = TransparencySortMode.CustomAxis;
        camera.transparencySortAxis = Vector3.up;
        var follow = camera.GetComponent<CameraFollow>() ?? Undo.AddComponent<CameraFollow>(camera.gameObject);
        follow.target = player.transform;
        var limits = camera.GetComponent<CameraBounds2D>() ?? Undo.AddComponent<CameraBounds2D>(camera.gameObject);
        limits.minBounds = new Vector2(-27, -21);
        limits.maxBounds = new Vector2(27, 21);
        if (camera.GetComponent<UniversalAdditionalCameraData>() == null)
            Undo.AddComponent<UniversalAdditionalCameraData>(camera.gameObject);
        Marker("Inicio_Jugador", 0, -15.5f, gameplay);
        Marker("Centro_Encuentro_Final", 0, -1.5f, gameplay);
        Marker("Referencia_Capilla", 0, 9.3f, gameplay);
        Marker("Referencia_Cementerio_Oeste", -13, -4, gameplay);
        Marker("Referencia_Cementerio_Este", 13, 4, gameplay);

        // Colliders simples y continuos en la barda, independientes de la forma de cada tile.
        var wallBody = Group("Colisiones_Perimetro", architecture);
        Box("Barda_Oeste", -25.6f, 0, 0.8f, 37, wallBody);
        Box("Barda_Este", 25.6f, 0, 0.8f, 37, wallBody);
        Box("Barda_Norte", 0, 18.5f, 52, 0.8f, wallBody);
        Box("Barda_Sur_Oeste", -13.6f, -17.4f, 24.8f, 0.8f, wallBody);
        Box("Barda_Sur_Este", 13.6f, -17.4f, 24.8f, 0.8f, wallBody);
        Box("Limite_Exterior_Entrada", 0, -20.5f, 54, 0.5f, wallBody);
        Polish();
        foreach (var t in new[] {grass, paving, stone, masonry}) t.CompressBounds();
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root.gameObject;
        Debug.Log("Nivel 4: escenario de 52 x 38 celdas construido y guardado.");
    }

    private static Transform Group(string name, Transform parent)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    private static TileBase Tile(string relative)
    {
        TileBase result;
        if (Tiles.TryGetValue(relative, out result) && result != null) return result;
        result = AssetDatabase.LoadAssetAtPath<TileBase>(Pack + "Tile Palette/" + relative);
        if (result == null) throw new InvalidOperationException("No se encontró tile: " + relative);
        Tiles[relative] = result;
        return result;
    }

    private static Tilemap Map(string name, Transform parent, int order)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(parent, false);
        var renderer = go.GetComponent<TilemapRenderer>();
        renderer.sortingLayerName = "Layer 1";
        renderer.sortingOrder = order;
        renderer.sharedMaterial = lit;
        renderer.mode = order == 2 ? TilemapRenderer.Mode.Individual : TilemapRenderer.Mode.Chunk;
        return go.GetComponent<Tilemap>();
    }

    private static void Set(Tilemap map, int x, int y, string tile)
    {
        map.SetTile(new Vector3Int(x, y, 0), Tile(tile));
    }

    private static void PathRect(int x, int y, int width, int height)
    {
        for (int ix = x; ix < x + width; ix++)
            for (int iy = y; iy < y + height; iy++)
                Set(paving, ix, iy, "TP Grass/TX Tileset Grass Pavement " + (2 + Math.Abs(ix) % 2) + ".asset");
    }

    public static void Polish()
    {
        if (Application.isPlaying || EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Esta herramienta se aplica solo a la escena del Nivel 4 fuera de Play.");
        var environment = GameObject.Find(RootName);
        if (environment == null) throw new InvalidOperationException("No existe la propuesta de escenario.");
        // El dintel está por encima del personaje; solo las dos columnas frenan el paso.
        foreach (var collider in environment.GetComponentsInChildren<Collider2D>())
            if (collider.name == "Collider T" && collider.transform.parent.name == "PF Struct - Gate 01")
                collider.enabled = false;
        var maps = environment.GetComponentsInChildren<Tilemap>();
        var paths = maps.First(t => t.name == "Caminos_Empedrado");
        var plaza = maps.First(t => t.name == "Plaza_y_atrios");
        var ground = maps.First(t => t.name == "Suelo_Hierba");
        int[,] buildings = {{-5, 11, 10, 6}, {-23, 11, 6, 5}, {17, 11, 6, 5}};
        for (int b = 0; b < buildings.GetLength(0); b++)
            for (int x = buildings[b, 0]; x < buildings[b, 0] + buildings[b, 2]; x++)
                for (int y = buildings[b, 1] + 2; y < buildings[b, 1] + buildings[b, 3]; y++)
                    plaza.SetTile(new Vector3Int(x, y, 0), Tile("TP Stone Ground/TX Tileset Stone Ground_9.asset"));
        foreach (var p in paths.cellBounds.allPositionsWithin)
        {
            if (!paths.HasTile(p)) continue;
            int hash = Math.Abs(p.x * 73 + p.y * 137);
            string asset = hash % 13 == 0
                ? "TP Grass/TX Tileset Grass_" + (50 + hash % 6) + ".asset"
                : "TP Grass/TX Tileset Grass Pavement " + (2 + Math.Abs(p.x) % 2) + ".asset";
            paths.SetTile(p, Tile(asset));
        }
        // Bordes y un sello geométrico discretos dan escala al espacio de combate.
        foreach (var p in plaza.cellBounds.allPositionsWithin)
        {
            if (!plaza.HasTile(p)) continue;
            Color shade = Color.white;
            if (p.x >= -7 && p.x <= 6 && p.y >= -7 && p.y <= 6)
            {
                bool border = p.x == -7 || p.x == 6 || p.y == -7 || p.y == 6;
                bool seal = (p.x >= -3 && p.x <= 2 && (p.y == -2 || p.y == 4)) ||
                    (p.y >= -2 && p.y <= 4 && (p.x == -3 || p.x == 2));
                shade = border ? new Color(0.82f, 0.86f, 0.92f) :
                    seal ? new Color(0.85f, 0.88f, 0.93f) : Color.white;
                if (!border)
                {
                    plaza.SetTile(p, Tile("TP Stone Ground/TX Tileset Stone Ground_9.asset"));
                    if (!seal && (Math.Abs(p.x + 8) / 2 + Math.Abs(p.y + 8) / 2) % 2 == 0)
                        shade = new Color(0.97f, 0.98f, 1f);
                }
            }
            if (p.y >= 13) shade = new Color(0.62f, 0.68f, 0.78f);
            plaza.SetTileFlags(p, TileFlags.None);
            plaza.SetColor(p, shade);
        }
        foreach (var cell in ground.cellBounds.allPositionsWithin)
        {
            ground.SetTileFlags(cell, TileFlags.None);
            ground.SetColor(cell, Color.white);
        }
        var player = environment.GetComponentsInChildren<Cainos.PixelArtTopDown_Basic.TopDownCharacterController>().First();
        var animator = player.GetComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Sprites/Player/Animations/AC_MainPlayer.controller");
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Sprites/Player/Animations/Player_Idle_Down.anim");
        var spriteBinding = AnimationUtility.GetObjectReferenceCurveBindings(idle).First(b => b.propertyName == "m_Sprite");
        var sprite = (Sprite)AnimationUtility.GetObjectReferenceCurve(idle, spriteBinding)[0].value;
        player.GetComponent<SpriteRenderer>().drawMode = SpriteDrawMode.Simple;
        player.GetComponent<SpriteRenderer>().sprite = sprite;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private static void StoneRect(int x, int y, int width, int height)
    {
        for (int ix = 0; ix < width; ix++)
            for (int iy = 0; iy < height; iy++)
            {
                int col = ix == 0 ? 0 : ix == width - 1 ? 2 : 1;
                int row = iy == height - 1 ? 0 : iy == 0 ? 2 : 1;
                Set(stone, x + ix, y + iy, "TP Stone Ground/TX Tileset Stone Ground_" + (row * 8 + col) + ".asset");
                paving.SetTile(new Vector3Int(x + ix, y + iy, 0), null);
            }
    }

    private static void Wall(int x, int y, int index)
    {
        Set(masonry, x, y, "TP Wall/TX Tileset Wall_" + index + ".asset");
    }

    private static void Perimeter(Transform architecture)
    {
        for (int x = -26; x <= 25; x++)
        {
            Wall(x, 18, x == -26 ? 0 : x == 25 ? 2 : 1);
            if (x == -1 || x == 0) continue;
            Wall(x, -17, x == -26 ? 15 : x == 25 ? 17 : 16);
            Wall(x, -18, x == -26 ? 23 : x == 25 ? 25 : 24);
        }
        for (int y = -16; y <= 17; y++)
        {
            Wall(-26, y, 8);
            Wall(25, y, 9);
        }
        Prop("PF Struct - Gate 01", 0, -18.2f, architecture);
    }

    private static void Building(string name, int x, int y, int width, int height, Transform parent, bool chapel)
    {
        var building = Group(name, parent);
        for (int ix = 0; ix < width; ix++)
            for (int iy = 0; iy < height; iy++)
            {
                int index = iy == 0 ? (ix == 0 ? 23 : ix == width - 1 ? 25 : 24)
                    : iy == 1 ? (ix == 0 ? 15 : ix == width - 1 ? 17 : 16)
                    : iy == height - 1 ? (ix == 0 ? 0 : ix == width - 1 ? 2 : 1)
                    : ix == 0 ? 8 : ix == width - 1 ? 9 : 4;
                if (iy >= 2 && iy < height - 1 && ix > 0 && ix < width - 1)
                    Set(stone, x + ix, y + iy, "TP Stone Ground/TX Tileset Stone Ground_9.asset");
                else
                    Wall(x + ix, y + iy, index);
            }
        // Fachada y puerta usan los mismos sprites de piedra y madera del paquete original.
        for (int ix = 2; ix < width - 1; ix += chapel ? 5 : width)
        {
            Wall(x + ix, y, 56);
            Wall(x + ix, y + 1, 55);
        }
        Prop("Wooden Gate 01", x + width * 0.5f, y, building);
        Box("Volumen_Construccion", x + width * 0.5f, y + height * 0.5f, width, height, building);
        if (chapel) Prop("Gravestone 02", x + width * 0.5f, y + height - 0.3f, building);
    }

    private static void GraveGarden(string name, float x, float y, int columns, int rows, Transform parent, bool old)
    {
        var sector = Group(name, parent);
        for (int col = 0; col < columns; col++)
            for (int row = 0; row < rows; row++)
            {
                float px = x + col * 2.25f;
                float py = y + row * 2.05f;
                int variant = (col + row * 2 + (old ? 1 : 0)) % 3 + 1;
                Prop("Gravestone 0" + variant, px, py, sector);
                if ((col + row) % 3 == 0)
                    Prop("Stone 0" + (col % 6 + 1), px + 0.6f, py - 0.6f, sector);
                if (old && col % 2 == 0)
                    Plant("Bush 0" + (col + 1), px - 0.75f, py + 0.2f, sector);
            }
        Prop("Pillar 02", x - 1.3f, y - 0.3f, sector);
    }

    private static GameObject Prefab(string path, string name, float x, float y, Transform parent)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + path);
        if (asset == null) throw new InvalidOperationException("Falta prefab " + path);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        instance.name = name;
        instance.transform.position = new Vector3(x, y, 0);
        foreach (var r in instance.GetComponentsInChildren<SpriteRenderer>(true))
            if (r.sharedMaterial != null && !r.sharedMaterial.name.Contains("Shadow"))
                r.sharedMaterial = lit;
        return instance;
    }

    private static GameObject Prop(string name, float x, float y, Transform parent)
    {
        string filename = name.StartsWith("PF Struct") ? name : "PF Props - " + name;
        return Prefab("Prefab/Props/" + filename + ".prefab", filename, x, y, parent);
    }

    private static void Plant(string name, float x, float y, Transform parent)
    {
        Prefab("Prefab/Plant/PF Plant - " + name + ".prefab", "PF Plant - " + name, x, y, parent);
    }

    private static void Lantern(float x, float y, Transform parent)
    {
        var lantern = Prop("Stone Lantern 01", x, y, parent);
        var light = new GameObject("Luz_Calida").AddComponent<Light2D>();
        light.transform.SetParent(lantern.transform, false);
        light.transform.localPosition = new Vector3(0, 0.85f, -1);
        light.lightType = Light2D.LightType.Point;
        light.color = new Color(1f, 0.73f, 0.38f);
        light.intensity = 0.8f;
        light.pointLightInnerRadius = 0.4f;
        light.pointLightOuterRadius = 3.2f;
        light.falloffIntensity = 0.6f;
        LightLayers(light);
    }

    private static void LightLayers(Light2D light)
    {
        var serialized = new SerializedObject(light);
        var layers = serialized.FindProperty("m_ApplyToSortingLayers");
        var sorting = SortingLayer.layers;
        layers.arraySize = sorting.Length;
        for (int i = 0; i < sorting.Length; i++)
            layers.GetArrayElementAtIndex(i).intValue = sorting[i].id;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Box(string name, float x, float y, float width, float height, Transform parent)
    {
        var go = new GameObject(name, typeof(BoxCollider2D));
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x, y, 0);
        go.layer = 6;
        go.GetComponent<BoxCollider2D>().size = new Vector2(width, height);
    }

    private static void Marker(string name, float x, float y, Transform parent)
    {
        var marker = Group(name, parent);
        marker.position = new Vector3(x, y, 0);
    }
}
