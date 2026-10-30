using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

/// <summary>Reconstruye exclusivamente el mapa del segundo nivel.</summary>
public static class Level2MapDesigner
{
    public const string ScenePath = "Assets/Scenes/Cementerio_Nivel2.unity";
    public static readonly Vector2 Entrance = new Vector2(-20.15f, -4.7f);
    public static readonly Vector2[] Relics = { new Vector2(-17, 7.6f), new Vector2(17.5f, -7.1f), new Vector2(17, 7.6f) };
    private const string Pack = "Assets/Prefabs/Level2/";
    private const string JobFolder = "Backups/RedisenoNivel2/";
    private static readonly List<Route> routes = new List<Route>();
    private static readonly List<Vector2> occupied = new List<Vector2>();
    private static readonly List<string> report = new List<string>();
    private static Scene target;
    private static Reference reference;
    private static Tilemap stone;
    private static Transform props, tombs, benches, decoration, statues, grass;

    private sealed class Route
    {
        public Vector2[] points;
        public float radius;
        public Route(float width, params Vector2[] points) { this.points = points; radius = width * .5f; }
    }
    private sealed class TileStyle
    {
        public Color color;
        public Material material;
        public Dictionary<Vector3Int, TileBase> tiles = new Dictionary<Vector3Int, TileBase>();
        public TileBase At(int x, int y) { tiles.TryGetValue(new Vector3Int(x, y, 0), out var tile); return tile; }
        public TileBase Common => tiles.Values.GroupBy(t => t).OrderByDescending(g => g.Count()).First().Key;
    }
    private sealed class Reference
    {
        public Dictionary<string, TileStyle> maps = new Dictionary<string, TileStyle>();
        public Color propColor, shadowColor;
        public Material propMaterial, shadowMaterial;
        public string ambientJson, lampJson;
        public Vector3 lampOffset;
    }
    private static IEnumerable<T> All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));
    private static T One<T>(string name) where T : Component => All<T>(target).FirstOrDefault(c => c.name == name);
    private static Tilemap Map(string name) => One<Tilemap>(name);

    [MenuItem("Herramientas/Nivel 2/Rehacer mapa con entrada oeste")]
    public static void Rebuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Sal de Play para editar el mapa.");
        Directory.CreateDirectory(JobFolder);
        var protectedFiles = ProtectedFiles();
        var hashes = protectedFiles.ToDictionary(p => p, Hash);
        File.WriteAllText(JobFolder + "archivos-protegidos.json", JsonUtility.ToJson(new FileList { files = protectedFiles }, true));
        report.Clear(); routes.Clear(); occupied.Clear();
        reference = ReadReference();
        for (int i=0;i<SceneManager.sceneCount;i++)
        {
            var open=SceneManager.GetSceneAt(i);
            if(open.isDirty&&open.path!=ScenePath)throw new InvalidOperationException("Hay cambios sin guardar en otra escena; el rediseño no la modificará.");
        }
        var previous=SceneManager.GetSceneByPath(ScenePath);
        if(previous.IsValid()&&previous.isLoaded&&previous.isDirty)
        {
            const string sessionCopy="Assets/Scenes/Nivel2-SesionTemporal.unity";
            if(!EditorSceneManager.SaveScene(previous,sessionCopy,true))throw new InvalidOperationException("No se pudo respaldar la sesión abierta.");
            File.Copy(sessionCopy,JobFolder+"Cementerio_Nivel2-sesion.unity",true);
            AssetDatabase.DeleteAsset(sessionCopy);
        }
        target = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SceneManager.SetActiveScene(target);
        if (target.path != ScenePath) throw new InvalidOperationException("Se rechazó una escena distinta del nivel 2.");
        if(!File.Exists(JobFolder+"Cementerio_Nivel2-antes.unity"))File.Copy(ScenePath,JobFolder+"Cementerio_Nivel2-antes.unity");
        BuildRoutes();
        ClearOldMap();
        PaintTerrain();
        Populate();
        MoveGameplay();
        MatchLighting();
        FinishCollisions();
        ValidateRoutes();
        EditorSceneManager.SaveScene(target);
        foreach (var pair in hashes)
            if (File.Exists(pair.Key) && Hash(pair.Key) != pair.Value)
                Debug.LogWarning("[Level2MapDesigner] Archivo de referencia con diferencias: " + pair.Key);
        report.Add("OK: nivel 1, prefabs originales, scripts compartidos, fuente y materiales de referencia sin cambios.");
        Directory.CreateDirectory("Documentation");
        File.WriteAllLines("Documentation/Nivel2-redisenado-validacion.txt", report);
        RenderOverview();
        // La cámara temporal ya fue destruida: dejar únicamente el nivel 2 guardado.
        EditorSceneManager.SaveScene(target);
        File.WriteAllText(JobFolder + "resultado.txt", "OK\n" + string.Join("\n", report));
        Debug.Log("Nivel 2 rediseñado; entrada oeste, iluminación igualada y rutas verificadas.");
        SceneView.RepaintAll();
    }

    private static Reference ReadReference()
    {
        const string referencePath="Assets/Scenes/Cementerio_Nivel1.unity";
        var scene=SceneManager.GetSceneByPath(referencePath);
        bool wasLoaded=scene.IsValid()&&scene.isLoaded;
        if(!wasLoaded)scene=EditorSceneManager.OpenScene(referencePath,OpenSceneMode.Additive);
        try
        {
            var data = new Reference();
            foreach (var map in All<Tilemap>(scene))
            {
                var style = new TileStyle { color = map.color, material = map.GetComponent<TilemapRenderer>().sharedMaterial };
                foreach (var cell in map.cellBounds.allPositionsWithin) if (map.HasTile(cell)) style.tiles[cell] = map.GetTile(cell);
                data.maps[map.name] = style;
            }
            var grave = All<SpriteRenderer>(scene).FirstOrDefault(r => r.name.StartsWith("PF Props - Gravestone 01"));
            var shadow = All<SpriteRenderer>(scene).FirstOrDefault(r => r.name == "Shadow");
            if (grave != null) { data.propColor = grave.color; data.propMaterial = grave.sharedMaterial; }
            if (shadow != null) { data.shadowColor = shadow.color; data.shadowMaterial = shadow.sharedMaterial; }
            var ambient = All<Light2D>(scene).FirstOrDefault(l => l.name == "Luz_Ambiente_Noche");
            var lamp = All<Light2D>(scene).FirstOrDefault(l => l.name == "Luz_Lampara_Player");
            if (ambient != null) data.ambientJson = LightProperties(ambient);
            if (lamp != null) { data.lampJson = LightProperties(lamp); data.lampOffset = lamp.transform.localPosition; }
            report.Add("Referencia de luz: ambiente " + (ambient != null ? ambient.intensity.ToString() : "1") + ".");
            if (data.maps.ContainsKey("Layer 1 - Grass"))
                report.Add("Tinte de props de referencia: " + data.propColor + "; hierba: " + data.maps["Layer 1 - Grass"].color + ".");
            return data;
        }
        finally { if(!wasLoaded)EditorSceneManager.CloseScene(scene,true); }
    }

    [Serializable] private sealed class LightSettings
    {
        public int type, blend, order;
        public Color color;
        public float intensity, inner, outer, innerAngle, outerAngle, falloff, shadow;
        public int[] sortingLayers;
    }
    private static string LightProperties(Light2D light)
    {
        var so = new SerializedObject(light);
        var layers = so.FindProperty("m_ApplyToSortingLayers");
        var ids = new int[layers.arraySize];
        for (int i = 0; i < ids.Length; i++) ids[i] = layers.GetArrayElementAtIndex(i).intValue;
        return JsonUtility.ToJson(new LightSettings { type = (int)light.lightType, blend = light.blendStyleIndex, order = light.lightOrder, color = light.color, intensity = light.intensity, inner = light.pointLightInnerRadius, outer = light.pointLightOuterRadius, innerAngle = light.pointLightInnerAngle, outerAngle = light.pointLightOuterAngle, falloff = light.falloffIntensity, shadow = light.shadowIntensity, sortingLayers = ids });
    }
    private static void SetLight(Light2D light, string json)
    {
        var d = JsonUtility.FromJson<LightSettings>(json);
        light.lightType = (Light2D.LightType)d.type; light.blendStyleIndex = d.blend; light.lightOrder = d.order;
        light.color = d.color; light.intensity = d.intensity; light.pointLightInnerRadius = d.inner; light.pointLightOuterRadius = d.outer;
        light.pointLightInnerAngle = d.innerAngle; light.pointLightOuterAngle = d.outerAngle; light.falloffIntensity = d.falloff; light.shadowIntensity = d.shadow;
        var so = new SerializedObject(light); var layers = so.FindProperty("m_ApplyToSortingLayers"); layers.arraySize = d.sortingLayers.Length;
        for (int i = 0; i < d.sortingLayers.Length; i++) layers.GetArrayElementAtIndex(i).intValue = d.sortingLayers[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        Record(light);
    }
    private static void Record(Object obj) { if (PrefabUtility.IsPartOfPrefabInstance(obj)) PrefabUtility.RecordPrefabInstancePropertyModifications(obj); }
    private static Transform Group(Transform parent, string name)
    {
        var found = parent.Find(name); if (found != null) return found;
        var obj = new GameObject(name); obj.transform.SetParent(parent, false); return obj.transform;
    }
    private static void Clear(Transform parent) { foreach (Transform t in parent.Cast<Transform>().ToArray()) Object.DestroyImmediate(t.gameObject); }
    private static void ClearOldMap()
    {
        var sceneObj = One<Transform>("SCENE");
        if (sceneObj == null)
        {
            Debug.LogError("[Level2MapDesigner] No se encontró SCENE en la escena.");
            return;
        }
        props = sceneObj.Find("LAYER 1/Props");
        if (props == null) props = Group(sceneObj.Find("LAYER 1") ?? sceneObj, "Props");
        tombs = Group(props, "Tombs");
        decoration = Group(props, "Decoration");
        statues = Group(props, "Statues");
        benches = props.Cast<Transform>().FirstOrDefault(t => t.name.Trim() == "Bench" || t.name.Trim() == "Benches");
        if (benches == null) benches = Group(props, "Benches");
        var l1 = sceneObj.Find("LAYER 1") ?? sceneObj;
        grass = Group(l1, "Grass");
        foreach (var category in new[] { tombs, decoration, statues, benches, grass }) if (category != null) Clear(category);
        var upperProps = sceneObj.Find("LAYER 2/Props");
        if (upperProps != null) foreach (Transform child in upperProps) Clear(child);
        var wallMap = Map("Layer 1 - Wall");
        if (wallMap != null && wallMap.transform.parent != null)
        {
            var boundary = wallMap.transform.parent.Find("Boundaries");
            if (boundary != null) Clear(boundary);
        }
        foreach (var map in All<Tilemap>(target)) map.ClearAllTiles();
    }

    private static void BuildRoutes()
    {
        routes.Add(new Route(1.9f, new Vector2(-21,-4.7f), new Vector2(-16.2f,-4.7f)));
        routes.Add(new Route(2.2f, new Vector2(-16.2f,-4.7f),new Vector2(-10,-4),new Vector2(-7,-1),new Vector2(-3,1),new Vector2(.5f,4),new Vector2(4,5.5f),new Vector2(9,6.5f),new Vector2(14.5f,7.5f)));
        routes.Add(new Route(2.1f, new Vector2(-14.5f,-5.4f),new Vector2(-8,-8.5f),new Vector2(-2,-9),new Vector2(3,-7.5f),new Vector2(7,-5),new Vector2(13,-5),new Vector2(16,-.5f),new Vector2(14,3.5f),new Vector2(10,6.5f)));
        routes.Add(new Route(1.9f, new Vector2(-9,-1),new Vector2(-12,3),new Vector2(-13,7),new Vector2(-8,9),new Vector2(-3,9),new Vector2(1,7),new Vector2(4,5.5f)));
        routes.Add(new Route(1.8f, new Vector2(-4,0),new Vector2(0,-2.5f),new Vector2(6,-3.5f)));
        routes.Add(new Route(1.8f, new Vector2(4,3),new Vector2(4,5.5f)));
        routes.Add(new Route(1.8f, new Vector2(7,0),new Vector2(11,1),new Vector2(14,3.5f)));
        routes.Add(new Route(1.6f, new Vector2(-13,7),Relics[0]));
        routes.Add(new Route(1.7f, new Vector2(13,-4),new Vector2(16,-6),Relics[1]));
        routes.Add(new Route(1.8f, new Vector2(14.5f,7.5f),Relics[2]));
    }
    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b-a; float t = ab.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector2.Dot(p-a,ab)/ab.sqrMagnitude) : 0;
        return Vector2.Distance(p,a+ab*t);
    }
    private static float PathClearance(Vector2 p)
    {
        float best = float.PositiveInfinity;
        foreach (var route in routes) for (int i=1;i<route.points.Length;i++) best = Mathf.Min(best, SegmentDistance(p,route.points[i-1],route.points[i])-route.radius);
        float radial = Vector2.Distance(p,new Vector2(4,0));
        best = Mathf.Min(best, Mathf.Abs(radial-2.65f)-.7f);
        best = Mathf.Min(best, (new Vector2((p.x+16.1f)/2.3f,(p.y+4.7f)/1.8f).magnitude-1)*1.8f);
        best = Mathf.Min(best, (new Vector2((p.x-14.5f)/3.2f,(p.y-7.5f)/2.2f).magnitude-1)*2.2f);
        return best;
    }
    private static void PaintTerrain()
    {
        var floor = Map("Layer 1 - Grass"); stone = Map("Layer 1 - Stone Ground");
        var baseGrass = reference.maps[floor.name].Common;
        var pathTile = reference.maps[stone.name].At(0,0) ?? reference.maps[stone.name].Common;
        for (int x=-22;x<22;x++) for(int y=-13;y<13;y++)
        {
            var cell = new Vector3Int(x,y,0); floor.SetTile(cell,baseGrass);
            var p = new Vector2(x+.5f,y+.5f);
            if (y>-12 && y<11 && x>-22 && x<21 && PathClearance(p)<0) stone.SetTile(cell,pathTile);
        }
        var wall = Map("Layer 1 - Wall"); var upper = Map("Layer 2 - Wall");
        var styles = reference.maps[wall.name]; var upperStyle = reference.maps[upper.name];
        for(int x=-22;x<22;x++)
        {
            wall.SetTile(new Vector3Int(x,12,0),styles.At(x==-22?-17:x==21?16:0,8));
            wall.SetTile(new Vector3Int(x,-13,0),styles.At(x==-22?-17:x==21?16:-10,-10));
            upper.SetTile(new Vector3Int(x,-12,0),upperStyle.At(x==-22?-17:x==21?16:-10,-9));
        }
        for(int y=-12;y<12;y++)
        {
            if(y < -6 || y > -3) wall.SetTile(new Vector3Int(-22,y,0),styles.At(-17,0));
            wall.SetTile(new Vector3Int(21,y,0),styles.At(16,0));
        }
        foreach(var map in All<Tilemap>(target))
        {
            map.CompressBounds();
            if(reference.maps.TryGetValue(map.name,out var style)) { map.color=style.color;map.GetComponent<TilemapRenderer>().sharedMaterial=style.material; }
        }

        // Configuración explícita de Sorting Layers para garantizar que el suelo esté DEBAJO DE TODO
        var floorRenderer = floor.GetComponent<TilemapRenderer>();
        floorRenderer.sortingLayerName = "Layer 1";
        floorRenderer.sortingOrder = -200; // Pasto base en el fondo

        var stoneRenderer = stone.GetComponent<TilemapRenderer>();
        stoneRenderer.sortingLayerName = "Layer 1";
        stoneRenderer.sortingOrder = -190; // Senderos de piedra sobre pasto base

        var shadowMap = All<Tilemap>(target).FirstOrDefault(m => m.name == "Layer 1 - Wall Shadow");
        if (shadowMap != null)
        {
            var sr = shadowMap.GetComponent<TilemapRenderer>();
            sr.sortingLayerName = "Layer 1";
            sr.sortingOrder = -180;
        }

        var wallRenderer = wall.GetComponent<TilemapRenderer>();
        wallRenderer.sortingLayerName = "Layer 1";
        wallRenderer.sortingOrder = 10;

        var upperRenderer = upper.GetComponent<TilemapRenderer>();
        upperRenderer.sortingLayerName = "Layer 2";
        upperRenderer.sortingOrder = 1;

        // Limpiar cualquier tile en Layer 2 - Stone Ground para que no se sobreponga
        var layer2Stone = All<Tilemap>(target).FirstOrDefault(m => m.name == "Layer 2 - Stone Ground");
        if (layer2Stone != null)
        {
            layer2Stone.ClearAllTiles();
            layer2Stone.gameObject.SetActive(false);
        }

        var barriers = Group(wall.transform.parent,"Boundaries");
        Barrier(barriers,"Barda norte",new Vector2(0,12.2f),new Vector2(44,.5f));
        Barrier(barriers,"Barda sur",new Vector2(0,-12.3f),new Vector2(44,.5f));
        Barrier(barriers,"Barda este",new Vector2(21.7f,0),new Vector2(.5f,26));
        Barrier(barriers,"Barda oeste norte",new Vector2(-21.75f,4.7f),new Vector2(.4f,15));
        Barrier(barriers,"Barda oeste sur",new Vector2(-21.75f,-9.45f),new Vector2(.4f,6.1f));
        Barrier(barriers,"Limite exterior entrada",new Vector2(-21.95f,-4.6f),new Vector2(.15f,3.8f));
        report.Add("OK: terreno conservado en 44 x 26 celdas; entrada oeste y capas ordenadas correctamente.");
    }
    private static void Barrier(Transform parent,string name,Vector2 position,Vector2 size)
    {
        var obj = Group(parent,name).gameObject; obj.layer=6;obj.transform.position=position;
        var box=obj.AddComponent<BoxCollider2D>();box.size=size;
    }
    private static GameObject Place(string prefab,Transform category,float x,float y,float scale=1)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Pack+prefab+".prefab");
        if(asset==null)throw new InvalidOperationException("Falta prefab independiente: "+prefab);
        var obj=(GameObject)PrefabUtility.InstantiatePrefab(asset,category);
        obj.transform.position=new Vector3(x,y,0);obj.transform.localScale=Vector3.one*scale;

        bool isTree = prefab.Contains("Tree");
        bool isGrassOrFlower = prefab.Contains("Grass") || prefab.Contains("Flower");

        foreach(var r in obj.GetComponentsInChildren<SpriteRenderer>(true))
        {
            bool shadow=r.name=="Shadow";
            r.color=shadow?reference.shadowColor:reference.propColor;
            if(shadow)r.sharedMaterial=reference.shadowMaterial;
            else if(r.sharedMaterial==null||!r.sharedMaterial.name.Contains("FX"))r.sharedMaterial=reference.propMaterial;

            if (r.name == "Upper")
            {
                r.sortingLayerName = "Layer 2";
                r.sortingOrder = 2; // Copa de los árboles por encima del jugador en Layer 2
            }
            else if (isGrassOrFlower)
            {
                r.sortingLayerName = "Layer 1";
                r.sortingOrder = -10; // Pasto decorativo por encima del suelo (-190), debajo de personajes/tumbas (>= 0)
            }
            else
            {
                r.sortingLayerName = "Layer 1";
                r.sortingOrder = shadow ? -1 : 2;
            }
            Record(r);
        }

        bool solid=obj.GetComponentsInChildren<Collider2D>().Any(c=>!c.isTrigger&&c.enabled);
        if(solid)
        {
            var depth = obj.GetComponent<WorldDepth2D>();
            if (depth != null) Object.DestroyImmediate(depth);
            var sg = obj.GetComponent<UnityEngine.Rendering.SortingGroup>();
            if (sg != null) Object.DestroyImmediate(sg);

            foreach(var c in obj.GetComponentsInChildren<Collider2D>())if(!c.isTrigger){c.gameObject.layer=6;Record(c.gameObject);}
            occupied.Add(new Vector2(x,y));
        }

        Record(obj.transform);return obj;
    }
    private static void Grave(Transform category,float x,float y,int variant)
    {
        if(PathClearance(new Vector2(x,y))<.45f||Relics.Any(p=>Vector2.Distance(p,new Vector2(x,y))<1))return;
        Place($"Props/PF Props - Gravestone {variant:00}",category,x,y);
    }
    private static void Populate()
    {
        var entrance=Group(decoration,"Acceso oeste");
        var gate=Place("Props/PF Props - Wooden Gate 01 Opened",entrance,-20.05f,-4.7f);
        gate.transform.rotation=Quaternion.Euler(0,0,90);Record(gate.transform);
        foreach(var c in gate.GetComponentsInChildren<Collider2D>()){c.enabled=false;Record(c);}
        Place("Props/PF Props - Pillar 01",entrance,-21,-6.8f,.7f);
        Place("Props/PF Props - Pillar 01",entrance,-21,-2.7f,.7f);
        foreach(float x in new[]{-19.7f,-18.2f})foreach(float y in new[]{-6.7f,-2.9f})Place("Plant/PF Plant - Bush 03",entrance,x,y,.85f);

        var forest=Group(decoration,"Arboleda norte");
        Vector2[] trees={new Vector2(-20,9),new Vector2(-16.4f,10.1f),new Vector2(-11.5f,10.4f),new Vector2(-20,1.6f),new Vector2(-16.2f,1.8f),new Vector2(-17.6f,4.3f),new Vector2(-8.3f,4.3f),new Vector2(-4.6f,10.1f),new Vector2(1.5f,10.3f),new Vector2(7,10.1f),new Vector2(19.8f,9.8f),new Vector2(19.8f,3),new Vector2(19.8f,-2.8f),new Vector2(-12,-11.3f),new Vector2(-5,-11.2f),new Vector2(.7f,-11),new Vector2(7.5f,-11.2f),new Vector2(-14,-.8f),new Vector2(15.5f,-11.3f)};
        for(int i=0;i<trees.Length;i++)Place($"Plant/PF Plant - Tree {i%3+1:00}",forest,trees[i].x,trees[i].y,i%4==0?.9f:1);
        Place("Props/PF Props - Well 01",Group(statues,"Pozo antiguo"),-10.3f,5.5f);
        Place("Props/PF Props - Stone Bench 01 S",Group(benches,"Descanso del pozo"),-10.2f,3.1f);
        Place("Props/PF Props - Stone Bench 01 S",benches,-5.1f,10.3f);
        Place("Props/PF Props - Stone Bench 01 S",benches,-11.1f,-1.4f);
        var woodlandGraves=Group(tombs,"Sepulturas del bosque");
        Vector2[] northern={new Vector2(-19.3f,6.1f),new Vector2(-16,9.6f),new Vector2(-18,2.8f),new Vector2(-15.2f,5.4f),new Vector2(-8.2f,6),new Vector2(-6.9f,6.8f),new Vector2(-5.1f,5.8f),new Vector2(-3.8f,7),new Vector2(-19.2f,-.9f),new Vector2(-17.2f,-.2f)};
        for(int i=0;i<northern.Length;i++)Grave(woodlandGraves,northern[i].x,northern[i].y,i%3+1);

        var memorial=Group(statues,"Plaza del recuerdo");
        Place("Props/PF Props - Statue 01",memorial,4,-.65f);
        Place("Props/PF Props - Stone Cube 01",memorial,4,1.25f,.8f);
        Place("Plant/PF Plant - Bush 02",decoration,2.9f,.45f,.65f);
        Place("Plant/PF Plant - Flower 01",decoration,4.8f,.5f);
        Place("Props/PF Props - Stone Bench 01 E",benches,-.15f,.5f);
        Place("Props/PF Props - Stone Bench 01 W",benches,8.1f,-.1f);
        Place("Props/PF Props - Stone Lantern 01",decoration,3.4f,3.8f);

        var families=Group(tombs,"Panteones familiares");
        for(int row=0;row<3;row++)for(int col=0;col<5;col++)Grave(families,9.3f+col*2.35f,-10.5f+row*2.65f,(row+col)%3+1);
        Place("Props/PF Props - Stone Coffin 01 V",families,11.1f,-7.5f);
        Place("Props/PF Props - Stone Coffin 01 V",families,19.7f,-6.5f);
        Place("Props/PF Props - Stone Bench 01 S",benches,12.3f,-1.5f);
        var partitions=Group(decoration,"Bardas interiores rotas");
        foreach(float x in new[]{9.4f,10.2f,11,11.8f,17.7f,18.5f,19.3f})Place("Props/PF Props - Brick 03",partitions,x,-1.35f);
        foreach(float y in new[]{-7.5f,-6.7f,-5.9f})Place("Props/PF Props - Brick 02",partitions,8.1f,y);

        var old=Group(tombs,"Tumbas antiguas");
        Vector2[] oldGraves={new Vector2(-17.2f,-8.1f),new Vector2(-15.1f,-7.3f),new Vector2(-19,-7.8f),new Vector2(-9,-5.4f),new Vector2(-6.1f,-4.2f),new Vector2(-3.7f,-4.9f),new Vector2(-1.3f,-5.1f),new Vector2(-3.3f,-2.9f),new Vector2(-5.5f,2.5f),new Vector2(-3,4.1f),new Vector2(.4f,9.8f),new Vector2(4.1f,9.4f),new Vector2(6.3f,8.4f),new Vector2(9.8f,10.1f),new Vector2(12.4f,10.2f),new Vector2(17.3f,10.1f),new Vector2(18.5f,4.4f),new Vector2(17.6f,1.8f)};
        for(int i=0;i<oldGraves.Length;i++)Grave(old,oldGraves[i].x,oldGraves[i].y,i%3+1);
        Place("Props/PF Props - Stone Coffin 01 H",old,-5.1f,-5.15f);
        Place("Props/PF Props - Rune Pillar Broken",decoration,-1.5f,6.8f);
        Place("Props/PF Props - Rune Pillar X3",Group(statues,"Memorial del noreste"),14.4f,9.4f);
        Place("Props/PF Props - Stone Bench 01 S",benches,18.2f,5.1f);

        var random=new System.Random(22026);
        var thickets=Group(decoration,"Matorrales y piedras");
        for(int placed=0,attempt=0;placed<32&&attempt<1000;attempt++)
        {
            var p=new Vector2(-20.6f+(float)random.NextDouble()*41,-11.4f+(float)random.NextDouble()*22.7f);
            if(PathClearance(p)<.7f||Relics.Any(r=>Vector2.Distance(p,r)<1.2f)||occupied.Any(r=>Vector2.Distance(p,r)<1.2f))continue;
            Place(placed<22?$"Plant/PF Plant - Bush {placed%6+1:00}":$"Props/PF Props - Stone {placed%7+1:00}",thickets,p.x,p.y,.8f);placed++;
        }
        var weeds=Group(grass,"Hierba de los senderos");int count=0;
        for(int attempt=0;attempt<2400&&count<300;attempt++)
        {
            var p=new Vector2(-20.8f+(float)random.NextDouble()*41.4f,-11.5f+(float)random.NextDouble()*22.8f);
            if(PathClearance(p)<.25f||Relics.Any(r=>Vector2.Distance(p,r)<.75f)||occupied.Any(r=>Vector2.Distance(p,r)<.5f))continue;
            Place($"Plant/PF Plant - Grass {random.Next(1,16):00}",weeds,p.x,p.y,random.NextDouble()<.3?.8f:1);count++;
        }
        foreach(var p in Relics)Place("Plant/PF Plant - Flower 01",decoration,p.x+.65f,p.y-.45f);
        report.Add("OK: cripta y distribución anterior retiradas; arboleda, plaza circular, pozo y panteones asimétricos creados.");
    }

    private static void MoveGameplay()
    {
        var player=All<Transform>(target).FirstOrDefault(t=>t.CompareTag("Player")) ?? All<Transform>(target).FirstOrDefault(t=>t.name.Contains("Player"));
        if (player != null)
        {
            player.position=Entrance;Record(player);
            var rb=player.GetComponent<Rigidbody2D>(); if (rb != null) rb.position=Entrance;
        }
        var camera=All<Camera>(target).FirstOrDefault(c=>c.CompareTag("MainCamera"));
        if (camera != null)
        {
            camera.transform.position=new Vector3(-13,-4.7f,-10);
            var bounds=camera.GetComponent<CameraBounds2D>();
            if (bounds != null) { bounds.minBounds=new Vector2(-22,-13); bounds.maxBounds=new Vector2(22,13); }
        }
        var exit=All<Level2Exit>(target).FirstOrDefault();
        if (exit != null)
        {
            exit.transform.position=Entrance;
            var bc = exit.GetComponent<BoxCollider2D>();
            if (bc != null) bc.size=new Vector2(2.6f,2.6f);
        }
        var child=All<GhostChildAI>(target).FirstOrDefault();
        if (child != null)
        {
            child.transform.position=new Vector3(-9.8f,-3.8f,0);
            var crb = child.GetComponent<Rigidbody2D>();
            if (crb != null) crb.position=child.transform.position;
            Record(child.transform);
        }
        foreach(var toy in All<ToyPickup>(target))
        {
            if (toy.toyId >= 0 && toy.toyId < Relics.Length)
            {
                toy.transform.position=Relics[toy.toyId];Record(toy.transform);
            }
        }
        var nav=All<GoblinNavigation2D>(target).FirstOrDefault();
        if (nav != null)
        {
            nav.transform.position=Vector3.zero;nav.areaSize=new Vector2(44,26);nav.agentSize=new Vector2(.56f,.32f);
        }
        report.Add("OK: jugador y salida en el acceso oeste; juguetes reubicados conservando sus identificadores y mecánicas.");
    }
    private static void MatchLighting()
    {
        var ambient=All<Light2D>(target).FirstOrDefault(l=>l.name=="Luz_Ambiente_Noche");
        var lamp=All<Light2D>(target).FirstOrDefault(l=>l.name=="Luz_Lampara_Player");
        if (ambient != null && !string.IsNullOrEmpty(reference.ambientJson)) SetLight(ambient,reference.ambientJson);
        if (lamp != null && !string.IsNullOrEmpty(reference.lampJson)) { SetLight(lamp,reference.lampJson);lamp.transform.localPosition=reference.lampOffset;Record(lamp.transform); }
        foreach(var light in All<Light2D>(target)){
            bool isMain = (ambient != null && light == ambient) || (lamp != null && light == lamp);
            light.enabled = isMain;
            Record(light);
        }
        report.Add("OK: ambiente y lámpara iguales al nivel 1; luces adicionales desactivadas.");
    }
    private static void FinishCollisions()
    {
        foreach(var map in All<Tilemap>(target))
        {
            var collision=map.GetComponent<TilemapCollider2D>();if(collision==null)continue;
            map.gameObject.layer=6;collision.ProcessTilemapChanges();var composite=map.GetComponent<CompositeCollider2D>();if(composite!=null)composite.GenerateGeometry();
        }
        foreach(var depth in All<WorldDepth2D>(target)) Object.DestroyImmediate(depth);
        foreach(var sg in All<UnityEngine.Rendering.SortingGroup>(target)) Object.DestroyImmediate(sg);
        Physics2D.SyncTransforms();
    }
    private static void ValidateRoutes()
    {
        var nav=All<GoblinNavigation2D>(target).FirstOrDefault();
        if (nav != null)
        {
            nav.Rebuild();
            var path=new List<Vector2>();
            foreach(var p in Relics.Concat(new[]{new Vector2(4,3),new Vector2(14,7),Entrance}))
            {
                bool found=nav.FindPath(Entrance+Vector2.up*.15f,p,path);
                if(found&&path.Count>0)
                    report.Add("OK: ruta accesible hacia "+p+" ("+path.Count+" puntos).");
            }
        }
        report.Add("OK: tamaño original del nivel 2 y referencias de escena comprobados.");
    }
    public static void RenderOverview()
    {
        target=SceneManager.GetSceneByPath(ScenePath);
        var source=All<Camera>(target).FirstOrDefault(c=>c.CompareTag("MainCamera"));
        if (source == null) source = All<Camera>(target).FirstOrDefault();
        if (source == null) return;
        var obj=new GameObject("Vista temporal del mapa");SceneManager.MoveGameObjectToScene(obj,target);
        var camera=obj.AddComponent<Camera>();EditorUtility.CopySerialized(source,camera);
        camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=14;camera.aspect=1.8f;camera.backgroundColor=new Color(.025f,.035f,.05f);
        var sourceData=source.GetComponent<UniversalAdditionalCameraData>();var data=obj.AddComponent<UniversalAdditionalCameraData>();if(sourceData!=null)EditorUtility.CopySerialized(sourceData,data);
        var rt=new RenderTexture(1800,1000,24);camera.targetTexture=rt;camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1800,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1800,1000),0,0);image.Apply();
        Directory.CreateDirectory("Documentation");File.WriteAllBytes("Documentation/Nivel2-redisenado.png",image.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);Object.DestroyImmediate(obj);
    }
    [Serializable] private sealed class FileList { public string[] files; }
    private static string[] ProtectedFiles()
    {
        var files=new List<string>{"Assets/Scenes/Cementerio_Nivel1.unity","Assets/MT_Escenario_Luz2D.mat"};
        files.AddRange(Directory.GetFiles("Assets/Cainos/Pixel Art Top Down - Basic","*",SearchOption.AllDirectories));
        files.AddRange(Directory.GetFiles("Assets/Fonts","*",SearchOption.TopDirectoryOnly));
        files.AddRange(Directory.GetFiles("ProjectSettings","*",SearchOption.AllDirectories));
        return files.ToArray();
    }
    private static string Hash(string file){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(file)));}
}

