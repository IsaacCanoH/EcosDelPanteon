using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class MenuPrincipalDesigner
{
    private const string ScenePath = "Assets/Scenes/MenuPrincipal.unity";

    static MenuPrincipalDesigner()
    {
        EditorApplication.delayCall += AutoApplyIfInScene;
    }

    private static void AutoApplyIfInScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path == ScenePath)
        {
            ApplyDesign();
        }
    }

    [MenuItem("Herramientas/Menu Principal/Actualizar Diseño y Opciones")]
    public static void ApplyDesign()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        bool wasDifferentScene = activeScene.path != ScenePath;

        if (wasDifferentScene)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        GameObject canvasGo = GameObject.Find("Canvas_Menu");
        if (canvasGo == null)
        {
            Debug.LogWarning("[MenuPrincipalDesigner] No se encontró Canvas_Menu en la escena.");
            return;
        }

        MenuPrincipal[] allMenus = UnityEngine.Object.FindObjectsOfType<MenuPrincipal>();
        MenuPrincipal menu = null;
        foreach (var m in allMenus)
        {
            if (m.gameObject.name == "ControlMenu") { menu = m; break; }
        }
        if (menu == null && allMenus.Length > 0) menu = allMenus[0];
        if (menu == null) menu = canvasGo.AddComponent<MenuPrincipal>();

        // Eliminar duplicados si existieran en Canvas_Menu
        foreach (var m in allMenus)
        {
            if (m != menu && m.gameObject == canvasGo)
            {
                UnityEngine.Object.DestroyImmediate(m);
            }
        }

        // Ejecutar construcción procedural de elementos
        menu.ConstruirOpcionesSiFaltan();

        // Asegurar que el título tenga el texto exacto pedido
        Transform areaMenu = canvasGo.transform.Find("AreaMenu");
        if (areaMenu != null)
        {
            Transform titTr = areaMenu.Find("Titulo");
            if (titTr != null)
            {
                TMP_Text tmp = titTr.GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    tmp.text = "ECOS DEL PANTEÓN";
                    tmp.fontSize = 44;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.color = new Color(0.98f, 0.90f, 0.62f, 1f);
                    EditorUtility.SetDirty(tmp);
                }
            }
        }

        // Marcar la escena como modificada y guardarla
        EditorUtility.SetDirty(menu);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[MenuPrincipalDesigner] Menú Principal y Apartado de Opciones actualizados exitosamente.");

        if (wasDifferentScene && !string.IsNullOrEmpty(activeScene.path))
        {
            EditorSceneManager.OpenScene(activeScene.path);
        }
    }
}
