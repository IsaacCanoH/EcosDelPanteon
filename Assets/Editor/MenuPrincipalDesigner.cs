using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Explicit, read-only authoring helpers. Opening the project must never rebuild
// or automatically save a legacy menu over the approved scene.
public static class MenuPrincipalDesigner
{
    private const string ScenePath = "Assets/Scenes/MenuPrincipal.unity";

    [MenuItem("Herramientas/Menu Principal/Abrir nuevo menú")]
    public static void OpenMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath);
    }

    [MenuItem("Herramientas/Menu Principal/Comprobar recursos")]
    public static void CheckResources()
    {
        string[] files = { "MainMenu.png", "LevelSelect.png", "Backdrop.png", "Lantern.png", "MenuHighlight.shader" };
        bool complete = true;
        foreach (string file in files)
        {
            string path = "Assets/Resources/UI/Menu/"+file;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) continue;
            Debug.LogWarning("Recurso de menú ausente: "+path);
            complete = false;
        }
        if (complete) Debug.Log("Menú pixel art: las cinco capas y recursos están disponibles.");
    }
}
