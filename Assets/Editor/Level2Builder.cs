using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cainos.PixelArtTopDown_Basic;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Herramientas explícitas: nunca modifica una escena al abrir el proyecto.</summary>
public static class Level2Builder
{
    private const string Scene1 = "Assets/Scenes/Cementerio_Nivel1.unity";
    private const string Scene2 = "Assets/Scenes/Cementerio_Nivel2.unity";
    private const string Pack = "Assets/Prefabs/Level2/";
    private const string ChildFolder = "Assets/Sprites/Enemies/Niño/";
    private const string AnimFolder = ChildFolder + "Animations/";
    private const string ItemFolder = "Assets/Sprites/Items/Toys/";
    private static readonly Vector2[] ToyPositions = { new Vector2(-18, 7.8f), new Vector2(18, -5.6f), new Vector2(15.8f, 10f) };
    private static readonly Dictionary<Vector3Int, TileBase> wall1 = new Dictionary<Vector3Int, TileBase>();
    private static readonly Dictionary<Vector3Int, TileBase> wall2 = new Dictionary<Vector3Int, TileBase>();
    private static Sprite[] toySprites;
    private static Material lit;

    [MenuItem("Herramientas/Nivel 2/Preparar mapa y enemigo")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.ReadAllText(Scene2).Contains("Salida_Nivel2"))
            throw new InvalidOperationException("El nivel 2 ya está preparado. Para reconstruir su mapa usa Herramientas/Nivel 2/Rehacer mapa con entrada oeste.");
        Directory.CreateDirectory(AnimFolder);
        Directory.CreateDirectory(ItemFolder);
        Directory.CreateDirectory("Assets/Prefabs/Enemies");
        Directory.CreateDirectory("Assets/Prefabs/Items");
        AssetDatabase.Refresh();
        lit = AssetDatabase.LoadAssetAtPath<Material>("Assets/MT_Escenario_Luz2D.mat");
        if (lit == null) lit = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + "Props/PF Props - Gravestone 01.prefab").GetComponent<SpriteRenderer>().sharedMaterial;
        BuildChildAnimations();
        BuildItems();
        FixLevel2Prefabs();

        // El nivel 1 se consulta como referencia; nunca se guarda ni se reordena.
        EditorSceneManager.OpenScene(Scene1);
        Capture(FindMap("Layer 1 - Wall"), wall1);
        Capture(FindMap("Layer 2 - Wall"), wall2);
        var nightTemplate = PrefabUtility.SaveAsPrefabAsset(GameObject.Find("Luz_Ambiente_Noche"), "Assets/Prefabs/PF_NightLighting.prefab");
        var player1 = GameObject.FindGameObjectWithTag("Player");
        // Guardar el jugador del nivel 1 como prefab conserva luz, audio, vida y animaciones.
        var playerTemplate = Object.Instantiate(player1);
        if (PrefabUtility.IsPartOfPrefabInstance(playerTemplate))
            PrefabUtility.UnpackPrefabInstance(playerTemplate, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var originalController = playerTemplate.GetComponent<TopDownCharacterController>();
        if (originalController != null)
        {
            playerTemplate.AddComponent<Level2CharacterController>().speed = originalController.speed;
            Object.DestroyImmediate(originalController);
        }
        PrefabUtility.SaveAsPrefabAsset(playerTemplate, "Assets/Prefabs/PF_LevelPlayer.prefab");
        Object.DestroyImmediate(playerTemplate);
        var hud1 = Object.FindObjectOfType<PlayerHUD>();
        Sprite full = hud1.corazonLleno, empty = hud1.corazonVacio;
        var font = Object.FindObjectOfType<TMP_Text>().font;

        var second = EditorSceneManager.OpenScene(Scene2);
        ReducePerimeter();
        Populate();
        // Sustituir sólo el personaje: el mapa y sus categorías se conservan.
        var previousPlayer = GameObject.FindGameObjectWithTag("Player");
        Vector3 start = previousPlayer.transform.position;
        Object.DestroyImmediate(previousPlayer);
        var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PF_LevelPlayer.prefab"));
        player.transform.position = start;
        var coins = player.GetComponent<GoldCoinCollector>();
        var toys = player.AddComponent<ToyCollector>();
        toys.pickupAudio = coins.audioRecoleccion;
        toys.pickupSound = coins.sonidoRecoleccion;
        Object.DestroyImmediate(coins);
        // El nivel 2 tiene su propio contador y no activa la maldición del oro.
        var night = Object.Instantiate(nightTemplate);
        night.name = "Luz_Ambiente_Noche";
        SceneManager.MoveGameObjectToScene(night, second);
        var camera = Camera.main;
        var oldFollow = camera.GetComponent<Cainos.PixelArtTopDown_Basic.CameraFollow>();
        if (oldFollow != null) Object.DestroyImmediate(oldFollow);
        var follow = Ensure<global::CameraFollow>(camera.gameObject);
        follow.target = player.transform;
        var bounds = Ensure<CameraBounds2D>(camera.gameObject);
        bounds.minBounds = new Vector2(-22, -13);
        bounds.maxBounds = new Vector2(22, 13);
        camera.transform.position = new Vector3(start.x, start.y, -10);
        camera.orthographicSize = 5f;
        if (camera.GetComponent<AudioListener>() != null && player.GetComponent<AudioListener>() != null) Object.DestroyImmediate(camera.GetComponent<AudioListener>());
        var navObject = new GameObject("Navigation");
        navObject.transform.position = new Vector3(0,-0.5f,0);
        var nav = navObject.AddComponent<GoblinNavigation2D>();
        nav.areaSize = new Vector2(44,27);
        nav.agentSize = new Vector2(0.56f,0.32f);
        nav.obstacleMask = 1 << 6;

        var enemyParent = Child(GameObject.Find("SCENE/LAYER 1/Props").transform, "Enemies");
        var enemyPrefab = BuildChildPrefab();
        var enemy = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, enemyParent);
        enemy.transform.position = new Vector3(-5.4f,-0.1f,0);
        var ai = enemy.GetComponent<GhostChildAI>();
        ai.player = player.transform;
        ai.toys = toys;
        ai.navigation = nav;
        var itemParent = Child(GameObject.Find("SCENE/LAYER 1/Props").transform, "Relics");
        for (int i=0;i<3;i++)
        {
            var item = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Items/PF_Toy_{i}.prefab"), itemParent);
            item.transform.position = ToyPositions[i];
        }
        BuildHUD(player, toys, font, full, empty);
        FixSceneDepth();
        ApplyNightMaterials();
        Physics2D.SyncTransforms();
        nav.Rebuild();
        ValidateReachability(nav, player);
        EditorSceneManager.SaveScene(second);
        AssetDatabase.SaveAssets();
        Debug.Log("Nivel 2 preparado: mapa compacto, juguetes, niño y navegación verificada.");
    }

    public static void ApplyNightMaterials()
    {
        RequireLevel2Scene();
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/MT_Escenario_Luz2D.mat");
        foreach(var renderer in Object.FindObjectsOfType<Renderer>())
        {
            if(!(renderer is SpriteRenderer) && !(renderer is TilemapRenderer))continue;
            if(renderer.GetComponentInParent<GhostChildAI>()!=null)continue;
            // Conservar las sombras del paquete; los cuerpos y el suelo reciben luces 2D.
            if(renderer.name=="Shadow")continue;
            renderer.sharedMaterial=material;
            if(PrefabUtility.IsPartOfPrefabInstance(renderer))PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
    }

    private static void Capture(Tilemap map, Dictionary<Vector3Int,TileBase> output)
    {
        output.Clear();
        foreach (var p in map.cellBounds.allPositionsWithin) if (map.HasTile(p)) output[p] = map.GetTile(p);
    }
    private static Tilemap FindMap(string name) { return Object.FindObjectsOfType<Tilemap>().Single(t=>t.name==name); }
    private static T Ensure<T>(GameObject obj) where T:Component
    {
        T component=obj.GetComponent<T>();
        return component!=null?component:obj.AddComponent<T>();
    }
    private static TileBase W(Dictionary<Vector3Int,TileBase> source,int x,int y) { source.TryGetValue(new Vector3Int(x,y,0), out var tile); return tile; }
    private static Transform Child(Transform parent,string name)
    {
        var found = parent.Find(name);
        if (found != null) return found;
        var obj = new GameObject(name);
        obj.transform.SetParent(parent,false);
        return obj.transform;
    }

    private static void ReducePerimeter()
    {
        var floor = FindMap("Layer 1 - Grass");
        TileBase grass = floor.GetTile(new Vector3Int(-10,0,0));
        if (grass == null) grass = floor.GetTilesBlock(floor.cellBounds).First(t=>t!=null);
        for(int x=-22;x<22;x++) for(int y=-13;y<13;y++) if(!floor.HasTile(new Vector3Int(x,y,0))) floor.SetTile(new Vector3Int(x,y,0),grass);
        var wall = FindMap("Layer 1 - Wall");
        wall.ClearAllTiles();
        // Conservar intacta la cripta elevada del diseño original.
        foreach(var pair in wall1) if(pair.Key.x>=-5 && pair.Key.x<=4 && pair.Key.y>=3 && pair.Key.y<=6) wall.SetTile(pair.Key,pair.Value);
        for(int x=-22;x<22;x++)
        {
            wall.SetTile(new Vector3Int(x,12,0),W(wall1,x==-22?-17:x==21?16:0,8));
            int sourceX = x==-22?-17:x==21?16:Mathf.Clamp(x,-16,15);
            wall.SetTile(new Vector3Int(x,-10,0),W(wall1,sourceX,-10));
        }
        for(int y=-9;y<12;y++)
        {
            wall.SetTile(new Vector3Int(-22,y,0),W(wall1,-17,0));
            wall.SetTile(new Vector3Int(21,y,0),W(wall1,16,0));
        }
        var upper = FindMap("Layer 2 - Wall");
        upper.ClearAllTiles();
        foreach(var pair in wall2) if(pair.Key.y>=5) upper.SetTile(pair.Key,pair.Value);
        for(int x=-22;x<22;x++) upper.SetTile(new Vector3Int(x,-9,0),W(wall2,x==-22?-17:x==21?16:Mathf.Clamp(x,-16,15),-9));
        // Retirar las dos barreras antiguas, que quedaban fuera del nuevo perímetro.
        foreach(string name in new[]{"Pared Invisible Derecha","Pared Invisible Izquierda"})
        {
            var old=GameObject.Find(name);
            if(old!=null) Object.DestroyImmediate(old);
        }
        var barriers = Child(wall.transform.parent,"Boundaries");
        Barrier(barriers,"Barda oeste",new Vector2(-21.7f,1),new Vector2(0.5f,22));
        Barrier(barriers,"Barda este",new Vector2(21.7f,1),new Vector2(0.5f,22));
        Barrier(barriers,"Barda norte",new Vector2(0,12.15f),new Vector2(44,0.5f));
        Barrier(barriers,"Barda sur oeste",new Vector2(-11.6f,-9.3f),new Vector2(20,0.5f));
        Barrier(barriers,"Barda sur este",new Vector2(11.6f,-9.3f),new Vector2(20,0.5f));
        Barrier(barriers,"Entrada oeste",new Vector2(-1.6f,-11.2f),new Vector2(0.35f,3.8f));
        Barrier(barriers,"Entrada este",new Vector2(1.6f,-11.2f),new Vector2(0.35f,3.8f));
        Barrier(barriers,"Limite entrada",new Vector2(0,-13),new Vector2(3.5f,0.4f));
        var stone = FindMap("Layer 1 - Stone Ground");
        var pathTile = stone.GetTile(new Vector3Int(0,0,0)) ?? stone.GetTilesBlock(stone.cellBounds).First(t=>t!=null);
        // Sendero periférico conectado al circuito y al eje central existentes.
        for(int x=-19;x<=19;x++) for(int y=8;y<=9;y++) stone.SetTile(new Vector3Int(x,y,0),pathTile);
        for(int y=-7;y<=9;y++) foreach(int x in new[]{-19,-18,18,19}) stone.SetTile(new Vector3Int(x,y,0),pathTile);
        for(int x=-19;x<=19;x++) for(int y=-8;y<=-7;y++) if(Mathf.Abs(x)>15) stone.SetTile(new Vector3Int(x,y,0),pathTile);
        for(int y=-13;y<=-9;y++) for(int x=-1;x<=0;x++) stone.SetTile(new Vector3Int(x,y,0),pathTile);
        foreach(var map in Object.FindObjectsOfType<Tilemap>()) map.CompressBounds();
    }
    private static void Barrier(Transform parent,string name,Vector2 position,Vector2 size)
    {
        var obj = Child(parent,name).gameObject;
        obj.layer=6;obj.transform.position=position;
        var c=Ensure<BoxCollider2D>(obj);c.size=size;
    }

    private static GameObject Place(string relative,Transform parent,float x,float y)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Pack+relative+".prefab");
        if(prefab==null) throw new InvalidOperationException("Falta prefab "+relative);
        var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
        obj.transform.position=new Vector3(x,y,0);
        return obj;
    }
    private static void Populate()
    {
        var props=GameObject.Find("SCENE/LAYER 1/Props").transform;
        var tombs=props.Find("Tombs");
        var benches=props.Cast<Transform>().First(t=>t.name.Trim()=="Bench");
        var decoration=props.Find("Decoration");
        var grass=GameObject.Find("SCENE/LAYER 1/Grass").transform;
        var statues=props.Find("Statues");
        // Nuevos grupos de tumbas, con pasillos de más de una unidad entre filas.
        for(int side=-1;side<=1;side+=2)
        {
            for(int row=0;row<4;row++)
                Place($"Props/PF Props - Gravestone {row%3+1:00}",tombs,side*20.25f,-5.8f+row*3.35f);
            for(int column=0;column<5;column++)
            {
                float x=side*(6.5f+column*2.65f);
                Place($"Props/PF Props - Gravestone {column%3+1:00}",tombs,x,10.35f);
            }
            Place("Plant/PF Plant - Tree 02",decoration,side*20.2f,7f);
            Place("Plant/PF Plant - Tree 03",decoration,side*16.9f,0.2f);
            Place("Plant/PF Plant - Tree 01",decoration,side*20.1f,-8.3f);
            Place("Plant/PF Plant - Tree 02",decoration,side*10.6f,10.5f);
            Place("Props/PF Props - Stone Bench 01 S",benches,side*12.3f,8.35f);
            Place("Props/PF Props - Stone Bench 01 S",benches,side*17f,-3.3f);
            Place("Props/PF Props - Stone Lantern 01",statues,side*3f,-8.4f);
            Place("Props/PF Props - Stone Lantern 01",statues,side*18f,6.7f);
            Place("Props/PF Props - Stone Coffin 01 V",tombs,side*17f,4.7f);
        }
        var random=new System.Random(20261021);
        int planted=0;
        for(int attempt=0;attempt<1800 && planted<175;attempt++)
        {
            float x=-20.8f+(float)random.NextDouble()*41.6f;
            float y=-8.7f+(float)random.NextDouble()*20.3f;
            // No cubrir los caminos con hierba, ni los puntos de recolección.
            if(Mathf.Abs(x)<1.8f || (y>7.6f && y<9.8f) || (Mathf.Abs(x)>17.5f&&Mathf.Abs(x)<19.8f) || ToyPositions.Any(p=>Vector2.Distance(p,new Vector2(x,y))<0.9f)) continue;
            Place($"Plant/PF Plant - Grass {random.Next(1,16):00}",grass,x,y);
            planted++;
        }
        foreach(var p in ToyPositions)
        {
            Place("Plant/PF Plant - Flower 01",decoration,p.x-0.6f,p.y-0.4f);
            Place("Plant/PF Plant - Grass 09",grass,p.x+0.55f,p.y-0.35f);
        }
    }

    private static void FixLevel2Prefabs()
    {
        foreach(string path in Directory.GetFiles(Pack,"*.prefab",SearchOption.AllDirectories))
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab==null || prefab.GetComponent<Collider2D>()==null || prefab.name.Contains("Stairs")) continue;
            var root=PrefabUtility.LoadPrefabContents(path);
            NormalizeDepth(root);
            if(root.name.Contains("Tree"))
            {
                var trunk=root.GetComponent<BoxCollider2D>();
                if(trunk!=null) { trunk.offset=new Vector2(0,0.18f);trunk.size=new Vector2(root.name.Contains("03")?0.65f:0.55f,0.34f); }
            }
            PrefabUtility.SaveAsPrefabAsset(root,path);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void NormalizeDepth(GameObject obj)
    {
        var sorter=Ensure<WorldDepth2D>(obj);
        foreach(var sprite in obj.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sprite.sortingLayerName="Layer 1";
            sprite.sortingOrder=sprite.name=="Shadow"?-1:sprite.name=="Upper"?1:0;
            sprite.spriteSortPoint=SpriteSortPoint.Pivot;
        }
        foreach(var c in obj.GetComponentsInChildren<Collider2D>(true))
            if(!c.isTrigger && obj.GetComponent<Rigidbody2D>()==null) c.gameObject.layer=6;
        sorter.Refresh();
    }

    private static void FixSceneDepth()
    {
        RequireLevel2Scene();
        foreach(var legacy in Object.FindObjectsOfType<PlayerOcclusionZone>()) legacy.enabled=false;
        // Las categorías siguen en su jerarquía; no deben formar un único bloque visual.
        foreach(var group in Object.FindObjectsOfType<SortingGroup>())
            if(group.GetComponent<WorldDepth2D>()==null && group.GetComponent<SpriteRenderer>()==null && group.GetComponent<Collider2D>()==null)
                Object.DestroyImmediate(group);
        foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())
        foreach(var t in root.GetComponentsInChildren<Transform>(true))
        {
            if(PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && !t.name.Contains("Stairs"))
            {
                bool solid=t.GetComponentsInChildren<Collider2D>().Any(c=>!c.isTrigger);
                if(solid || t.CompareTag("Player") || t.GetComponent<GhostChildAI>()!=null || t.GetComponent<ToyPickup>()!=null)
                {
                    NormalizeDepth(t.gameObject);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(t.GetComponent<WorldDepth2D>());
                    foreach(var s in t.GetComponentsInChildren<SpriteRenderer>()) PrefabUtility.RecordPrefabInstancePropertyModifications(s);
                    foreach(var c in t.GetComponentsInChildren<Collider2D>()) PrefabUtility.RecordPrefabInstancePropertyModifications(c.gameObject);
                }
                else foreach(var s in t.GetComponentsInChildren<SpriteRenderer>()) { s.sortingLayerName="Layer 1";s.sortingOrder=-10000;PrefabUtility.RecordPrefabInstancePropertyModifications(s); }
            }
        }
        foreach(var map in Object.FindObjectsOfType<Tilemap>())
        {
            var collision=map.GetComponent<TilemapCollider2D>();
            if(collision!=null)
            {
                map.gameObject.layer=6;
                collision.ProcessTilemapChanges();
                var composite=map.GetComponent<CompositeCollider2D>();
                if(composite!=null) composite.GenerateGeometry();
            }
            var renderer=map.GetComponent<TilemapRenderer>();
            if(map.name.Contains("Grass")||map.name.Contains("Ground")||map.name.Contains("Shadow"))
            {
                renderer.sortingLayerName="Layer 1";
                renderer.sortingOrder=map.name.Contains("Grass")?-25000:map.name.Contains("Shadow")?-23000:-24000;
            }
        }
        // Las plataformas siguen usando sus capas; el orden dinámico se aplica a personajes y props.
        var player=GameObject.FindGameObjectWithTag("Player");
        if(player!=null) NormalizeDepth(player);
    }

    private static void RequireLevel2Scene()
    {
        if (SceneManager.GetActiveScene().path != Scene2)
            throw new InvalidOperationException("Esta herramienta sólo puede modificar Cementerio_Nivel2.");
    }

    private static void BuildChildAnimations()
    {
        string sheet=ChildFolder+"sprites_niño.png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(sheet);
        if(importer==null) throw new InvalidOperationException("Falta "+sheet);
        importer.textureType=TextureImporterType.Sprite;
        importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=175;
        importer.filterMode=FilterMode.Point;
        importer.mipmapEnabled=false;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency=true;
        importer.npotScale=TextureImporterNPOTScale.None;
        importer.maxTextureSize=2048;
        var slices=new List<SpriteMetaData>();
        int[] xEdges={0,169,316,467,619,772,924,1077,1254};
        int[] yEdges={145,422,687,946,1254};
        int[] centers={98,240,388,540,695,845,997,1153};
        int[] feetY={395,661,924,1180};
        string[] directions={"Down","Up","Left","Right"};
        for(int row=0;row<4;row++) for(int col=0;col<8;col++)
            slices.Add(new SpriteMetaData{name=$"Child_{directions[row]}_{col}",rect=new Rect(xEdges[col],1254-yEdges[row+1],xEdges[col+1]-xEdges[col],yEdges[row+1]-yEdges[row]),alignment=(int)SpriteAlignment.Custom,pivot=new Vector2((float)(centers[col]-xEdges[col])/(xEdges[col+1]-xEdges[col]),(float)(yEdges[row+1]-feetY[row])/(yEdges[row+1]-yEdges[row]))});
        importer.spritesheet=slices.ToArray();
        importer.SaveAndReimport();
        var frames=AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>().ToDictionary(s=>s.name);
        string controllerPath=AnimFolder+"GhostChild.controller";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if(controller!=null) AssetDatabase.DeleteAsset(controllerPath);
        controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("Direction",AnimatorControllerParameterType.Int);
        controller.AddParameter("IsMoving",AnimatorControllerParameterType.Bool);
        controller.AddParameter("ActionState",AnimatorControllerParameterType.Int);
        var machine=controller.layers[0].stateMachine;
        int[] directionValues={0,1,3,2};
        for(int row=0;row<4;row++)
        {
            for(int action=0;action<4;action++)
            {
                string label=action==0?"Idle":action==1?"Walk":action==2?"Melee":"Throw";
                string name=$"Child_{label}_{directions[row]}";
                var clip=new AnimationClip{name=name,frameRate=10};
                int[] sequence=action==0?new[]{0,1,0}:action==1?new[]{2,3,4,5,6,7,2}:new[]{1,3,4,5,1};
                var keys=new ObjectReferenceKeyframe[sequence.Length];
                for(int i=0;i<keys.Length;i++) keys[i]=new ObjectReferenceKeyframe{time=i*(action>=2?0.15f:0.12f),value=frames[$"Child_{directions[row]}_{sequence[i]}"]};
                AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("Visual",typeof(SpriteRenderer),"m_Sprite"),keys);
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=action<2;AnimationUtility.SetAnimationClipSettings(clip,settings);
                if(action>=2)
                {
                    AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("Visual",typeof(Transform),"localEulerAnglesRaw.z"),new AnimationCurve(new Keyframe(0,0),new Keyframe(.15f,-8),new Keyframe(.3f,8),new Keyframe(.6f,0)));
                }
                string clipPath=AnimFolder+name+".anim";
                var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if(existing!=null) { EditorUtility.CopySerialized(clip,existing);Object.DestroyImmediate(clip);clip=existing; }
                else AssetDatabase.CreateAsset(clip,clipPath);
                var state=machine.AddState(name);state.motion=clip;
                if(row==0&&action==0) machine.defaultState=state;
                var transition=machine.AddAnyStateTransition(state);
                transition.hasExitTime=false;transition.duration=0;transition.canTransitionToSelf=false;
                transition.AddCondition(AnimatorConditionMode.Equals,directionValues[row],"Direction");
                transition.AddCondition(AnimatorConditionMode.Equals,action<2?0:action-1,"ActionState");
                if(action<2) transition.AddCondition(action==1?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,"IsMoving");
            }
        }
    }

    private static Sprite PixelSprite(string name,int width,int height,Action<Color32[],int,int> paint)
    {
        var pixels=new Color32[width*height];paint(pixels,width,height);
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);texture.SetPixels32(pixels);texture.Apply();
        string path=ItemFolder+name+".png";File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static void BuildItems()
    {
        Color32 outline=new Color32(39,31,53,255),gold=new Color32(229,178,91,255),red=new Color32(194,83,87,255),blue=new Color32(97,175,200,255);
        toySprites=new Sprite[3];
        // Dibujos de píxeles pequeños que conservan la escala del paquete existente.
        toySprites[0]=PixelSprite("Toy_Bear",24,28,(p,w,h)=>{for(int y=2;y<25;y++)for(int x=2;x<22;x++){bool head=(x-12)*(x-12)+(y-19)*(y-19)<52;bool ears=((x-6)*(x-6)+(y-24)*(y-24)<10)||((x-18)*(x-18)+(y-24)*(y-24)<10);bool body=x>=6&&x<=18&&y>=4&&y<=15;bool feet=y<7&&((x>=3&&x<10)||(x>=15&&x<22));if(head||ears||body||feet)p[y*w+x]=x%6==0?outline:gold;}p[20*w+9]=outline;p[20*w+15]=outline;p[17*w+12]=outline;for(int x=7;x<=17;x++)p[13*w+x]=red;});
        toySprites[1]=PixelSprite("Toy_Ball",24,24,(p,w,h)=>{for(int y=1;y<23;y++)for(int x=1;x<23;x++){int r=(x-12)*(x-12)+(y-12)*(y-12);if(r<111)p[y*w+x]=r>87?outline:x+y<18?gold:x>12?red:blue;}});
        toySprites[2]=PixelSprite("Toy_Train",32,24,(p,w,h)=>{for(int y=5;y<18;y++)for(int x=2;x<30;x++){if((y<12)||(x>15&&x<27)){p[y*w+x]=y==5||x==2||x==29?outline:(x>15?blue:red);}}for(int y=17;y<22;y++)for(int x=5;x<10;x++)p[y*w+x]=gold;for(int y=1;y<9;y++)for(int x=3;x<29;x++)if((x-8)*(x-8)+(y-5)*(y-5)<15||(x-23)*(x-23)+(y-5)*(y-5)<15)p[y*w+x]=outline;});
        for(int i=0;i<3;i++)
        {
            var obj=new GameObject("Juguete_"+new[]{"Oso","Pelota","Tren"}[i]);
            var s=obj.AddComponent<SpriteRenderer>();s.sprite=toySprites[i];s.sharedMaterial=lit;
            var c=obj.AddComponent<CircleCollider2D>();c.isTrigger=true;c.radius=.55f;
            obj.AddComponent<ToyPickup>().toyId=i;obj.AddComponent<WorldDepth2D>();
            var light=obj.AddComponent<Light2D>();light.lightType=Light2D.LightType.Point;light.color=new Color(.5f,.85f,1);light.intensity=.85f;light.pointLightInnerRadius=.2f;light.pointLightOuterRadius=1.5f;
            PrefabUtility.SaveAsPrefabAsset(obj,$"Assets/Prefabs/Items/PF_Toy_{i}.prefab");Object.DestroyImmediate(obj);
        }
        Sprite stone=PixelSprite("Ghost_Stone",12,10,(p,w,h)=>{for(int y=1;y<9;y++)for(int x=1;x<11;x++)if((x-6)*(x-6)+(y-5)*(y-5)<20)p[y*w+x]=y>5?new Color32(163,177,193,255):outline;});
        var rock=new GameObject("Piedra_del_Niño");rock.AddComponent<SpriteRenderer>().sprite=stone;
        var rb=rock.AddComponent<Rigidbody2D>();rb.gravityScale=0;rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
        var collider=rock.AddComponent<CircleCollider2D>();collider.isTrigger=true;collider.radius=.12f;
        rock.AddComponent<GhostStoneProjectile>();rock.AddComponent<WorldDepth2D>();
        PrefabUtility.SaveAsPrefabAsset(rock,"Assets/Prefabs/Enemies/PF_GhostStone.prefab");Object.DestroyImmediate(rock);
    }

    private static GameObject BuildChildPrefab()
    {
        var obj=new GameObject("Niño_Espectral");obj.layer=20;
        var rb=obj.AddComponent<Rigidbody2D>();rb.gravityScale=0;rb.freezeRotation=true;rb.interpolation=RigidbodyInterpolation2D.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
        var feet=obj.AddComponent<BoxCollider2D>();feet.size=new Vector2(.42f,.24f);feet.offset=new Vector2(0,.14f);
        obj.AddComponent<Animator>().runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimFolder+"GhostChild.controller");
        var visual=Child(obj.transform,"Visual").gameObject;
        var renderer=visual.AddComponent<SpriteRenderer>();renderer.sprite=AssetDatabase.LoadAllAssetsAtPath(ChildFolder+"sprites_niño.png").OfType<Sprite>().First(s=>s.name=="Child_Down_0");
        var unlit=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        var material=new Material(unlit);string materialPath=ChildFolder+"GhostChild_Unlit.mat";
        var existing=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(existing==null) AssetDatabase.CreateAsset(material,materialPath);else {Object.DestroyImmediate(material);material=existing;}
        renderer.sharedMaterial=material;
        obj.AddComponent<WorldDepth2D>();
        var ai=obj.AddComponent<GhostChildAI>();ai.stonePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/PF_GhostStone.prefab").GetComponent<GhostStoneProjectile>();
        var light=obj.AddComponent<Light2D>();light.lightType=Light2D.LightType.Point;light.color=new Color(.2f,.8f,1);light.intensity=.65f;light.pointLightInnerRadius=.2f;light.pointLightOuterRadius=1.5f;
        var result=PrefabUtility.SaveAsPrefabAsset(obj,"Assets/Prefabs/Enemies/PF_GhostChild.prefab");Object.DestroyImmediate(obj);return result;
    }

    private static void BuildHUD(GameObject player,ToyCollector toys,TMP_FontAsset font,Sprite full,Sprite empty)
    {
        Level2HUDUpgrade.Create(player, toys);
    }
    private static void ValidateReachability(GoblinNavigation2D nav,GameObject player)
    {
        var path=new List<Vector2>();var report=new List<string>();
        Vector2 start=player.GetComponent<Collider2D>().bounds.center;
        foreach(var target in ToyPositions.Concat(new[]{new Vector2(0,-11.7f)}))
        {
            bool found=nav.FindPath(start,target,path);
            float distance=path.Count>0?Vector2.Distance(path[path.Count-1],target):float.PositiveInfinity;
            if(!found||distance>.65f) throw new InvalidOperationException("Destino inaccesible: "+target+" distancia="+distance);
            report.Add(target+": ruta accesible, "+path.Count+" puntos.");
        }
        Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/Nivel2-validacion.txt",report);
    }
}
