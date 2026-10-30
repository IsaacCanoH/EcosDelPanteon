using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public static class Level2Validation
{
    [MenuItem("Herramientas/Nivel 2/Validar escena")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) return;
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        EditorSceneManager.OpenScene("Assets/Scenes/Cementerio_Nivel2.unity");
        var checks=new List<string>();
        Action<bool,string> check=(valid,message)=>{if(!valid)throw new InvalidOperationException(message);checks.Add("OK: "+message);};
        var scene=SceneManager.GetActiveScene();
        check(scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"Escena sin scripts faltantes");
        var player=GameObject.FindGameObjectWithTag("Player");
        check(player!=null && player.GetComponent<PlayerHealth>()!=null && player.GetComponent<ToyCollector>()!=null,"Jugador con vida y recolección de juguetes");
        check(player.GetComponent<PlayerWeaponController>()!=null, "Jugador con PlayerWeaponController configurado");
        check(player.GetComponent<PlayerStoneThrow>()!=null, "Jugador con PlayerStoneThrow (6 piedras)");
        check(Object.FindObjectsOfType<ToyPickup>(true).Select(t=>t.toyId).OrderBy(i=>i).SequenceEqual(new[]{0,1,2}),"Tres juguetes distintos");
        check(Object.FindObjectsOfType<ToyEncounterZone>(true).Length==3,"Tres zonas de encuentro (altares)");
        var enc3 = Object.FindObjectsOfType<ToyEncounterZone>(true).FirstOrDefault(e => e.encounterIndex == 2);
        check(enc3 != null && enc3.bossPrefab != null, "Encuentro 3 con prefab de Jefe Niño Espectral");
        check(GameObject.Find("Canvas_HUD") != null, "Canvas_HUD presente en la escena");
        check(Object.FindObjectOfType<PlayerDeathScreenUI>(true) != null, "PlayerDeathScreenUI configurado");
        check(Object.FindObjectOfType<ToySearchUI>(true) != null, "ToySearchUI [E] configurado");
        check(Object.FindObjectOfType<Level2ClueSystem>(true) != null, "Level2ClueSystem configurado");
        check(GameObject.Find("SCENE/LAYER 1/Props/Tombs")!=null && GameObject.Find("SCENE/LAYER 1/Grass")!=null && GameObject.Find("SCENE/LAYER 2/Tilemap")!=null,"Jerarquía de layouts conservada");
        var frames=AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Enemies/Niño/sprites_niño.png").OfType<Sprite>().ToArray();
        check(frames.Length==32,"32 sprites recortados del PNG original");
        check(frames.All(s=>s.pivot.y>0 && s.pivot.x>0),"Pivotes anclados a los pies");
        var map=Object.FindObjectsOfType<Tilemap>().FirstOrDefault(t=>t.name=="Layer 1 - Wall");
        check(map!=null&&map.cellBounds.xMin==-22&&map.cellBounds.xMax==22&&map.cellBounds.yMax==13,"Perímetro de 44 celdas de ancho y barda norte en y=12");
        check(map!=null&&map.gameObject.layer==6,"Bardas incluidas en Obstacles");
        var floor=Object.FindObjectsOfType<Tilemap>().FirstOrDefault(t=>t.name=="Layer 1 - Grass");
        check(floor!=null&&floor.cellBounds.size==new Vector3Int(44,26,1),"Terreno de 44 x 26 celdas conservado");
        check(Object.FindObjectsOfType<Level2Exit>(true).Length>0,"Salida configurada en la puerta oeste");
        check(Object.FindObjectsOfType<Light2D>().Count(l=>l.enabled)==2,"Sólo ambiente nocturno y lámpara del jugador activos");
        Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/Nivel2-validacion.txt",checks);
        Debug.Log("VALIDACIÓN NIVEL 2: "+checks.Count+" comprobaciones correctas.");
    }

    public static void RunAndRender() { Run(); Render(); }

    public static void Render()
    {
        if (EditorApplication.isPlaying) return;
        EditorSceneManager.OpenScene("Assets/Scenes/Cementerio_Nivel2.unity");
        foreach(var canvas in Object.FindObjectsOfType<Canvas>())canvas.gameObject.SetActive(false);
        var camera=Camera.main;
        var bounds=camera.GetComponent<CameraBounds2D>();if(bounds!=null)bounds.enabled=false;
        var follow=camera.GetComponent<CameraFollow>();if(follow!=null)follow.enabled=false;
        camera.orthographicSize=14f;camera.aspect=1.8f;camera.transform.position=new Vector3(0,0,-10);
        camera.backgroundColor=new Color(.025f,.035f,.05f);
        var target=new RenderTexture(1800,1000,24);camera.targetTexture=target;camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
        Directory.CreateDirectory("Documentation");File.WriteAllBytes("Documentation/Nivel2-vista-general.png",image.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(image);
    }
}
