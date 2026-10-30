using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Reutiliza los elementos originales del nivel 1 sin alterar esa escena.</summary>
public static class Level2HUDUpgrade
{
    private const string Template = "Assets/Prefabs/UI/PF_Level2HUD.prefab";
    private const string Level2Font = "Assets/Fonts/Level2/PressStart2P-Level2 SDF.asset";

    [MenuItem("Herramientas/Nivel 2/Actualizar HUD desde nivel 1")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/Scenes/Cementerio_Nivel1.unity");
        Directory.CreateDirectory("Assets/Prefabs/UI");
        AssetDatabase.Refresh();
        var canvas = Object.Instantiate(GameObject.Find("Canvas_HUD"));
        canvas.name = "Canvas_HUD";
        var source = canvas.GetComponentInChildren<PlayerHUD>(true);
        var hud = canvas.AddComponent<Level2HUD>();
        hud.healthText = source.textoVida;
        hud.objective = source.textoMonedas;
        hud.hearts = source.corazones;
        hud.fullHeart = source.corazonLleno;
        hud.emptyHeart = source.corazonVacio;
        hud.toyIcons = source.monedas;
        for (int i = 0; i < hud.toyIcons.Length; i++)
        {
            var image = hud.toyIcons[i];
            image.name = "Juguete_" + i;
            image.sprite = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Items/PF_Toy_{i}.prefab").GetComponent<SpriteRenderer>().sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
        var note = canvas.transform.Find("Nota_Mensaje");
        hud.noticeCard = note.GetComponent<CanvasGroup>();
        hud.notice = note.Find("TextoNota").GetComponent<TMP_Text>();
        hud.noticeTitle = note.Find("TituloNota").GetComponent<TMP_Text>();
        hud.noticeTitle.text = "OBJETIVO";
        hud.notice.text = "Recupera los 3 juguetes\ndel niño del panteón.";
        hud.noticeCard.alpha = 0f;
        hud.objective.text = "0/3";
        hud.healthText.text = "5/5";
        Object.DestroyImmediate(source);
        var hintObject = Object.Instantiate(hud.objective.gameObject, canvas.transform, false);
        hintObject.name = "Aviso_Salida";
        hud.exitHint = hintObject.GetComponent<TMP_Text>();
        hud.exitHint.text = "";
        hud.exitHint.fontSize = 14;
        hud.exitHint.enableAutoSizing = true;
        hud.exitHint.fontSizeMin = 10;
        hud.exitHint.fontSizeMax = 14;
        hud.exitHint.alignment = TextAlignmentOptions.Center;
        var hintRect = (RectTransform)hintObject.transform;
        hintRect.anchorMin = hintRect.anchorMax = hintRect.pivot = new Vector2(.5f, 0);
        hintRect.anchoredPosition = new Vector2(0, 24);
        hintRect.sizeDelta = new Vector2(1000, 42);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Level2Font);
        if (font == null) throw new InvalidOperationException("Falta la fuente independiente del nivel 2.");
        foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            text.font = font;
            text.fontSharedMaterial = font.material;
        }
        PrefabUtility.SaveAsPrefabAsset(canvas, Template);
        Object.DestroyImmediate(canvas);

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Cementerio_Nivel2.unity");
        var previous = Object.FindObjectOfType<Level2HUD>();
        if (previous != null) Object.DestroyImmediate(previous.gameObject);
        var player = GameObject.FindGameObjectWithTag("Player");
        Create(player, player.GetComponent<ToyCollector>());
        EditorSceneManager.SaveScene(scene);
        var stone = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Enemies/PF_GhostStone.prefab");
        stone.GetComponent<GhostStoneProjectile>().speed = 10f;
        PrefabUtility.SaveAsPrefabAsset(stone, "Assets/Prefabs/Enemies/PF_GhostStone.prefab");
        PrefabUtility.UnloadPrefabContents(stone);
        AssetDatabase.SaveAssets();
        Debug.Log("HUD del nivel 1 reutilizado: corazones, fuente pixelada y pergamino; piedras a 10 unidades/s.");
    }

    public static Level2HUD Create(GameObject player, ToyCollector toys)
    {
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(Template);
        if (template == null) throw new InvalidOperationException("Primero ejecuta Actualizar HUD desde nivel 1 para guardar la plantilla de interfaz.");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(template);
        instance.name = "Canvas_HUD";
        var hud = instance.GetComponent<Level2HUD>();
        hud.health = player.GetComponent<PlayerHealth>();
        hud.toys = toys;
        PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
        var exit = Object.FindObjectOfType<Level2Exit>();
        if (exit == null)
        {
            var obj = new GameObject("Salida_Nivel2");
            obj.transform.SetParent(GameObject.Find("SCENE").transform);
            obj.transform.position = Level2MapDesigner.Entrance;
            var collider = obj.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(2.6f, 2.6f);
            exit = obj.AddComponent<Level2Exit>();
        }
        exit.hint = hud.exitHint;
        return hud;
    }
}
