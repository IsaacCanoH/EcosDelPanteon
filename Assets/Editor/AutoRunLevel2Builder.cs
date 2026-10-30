using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AutoRunLevel2Builder
{
    private const string TriggerFile = "Temp/run_level2_builder.trigger";

    static AutoRunLevel2Builder()
    {
        EditorApplication.delayCall += OnEditorReady;
        EditorApplication.update += CheckTrigger;
    }

    private static void CheckTrigger()
    {
        if (EditorApplication.isPlaying || EditorApplication.isPaused) return;

        if (File.Exists(TriggerFile))
        {
            OnEditorReady();
        }
    }

    private static void OnEditorReady()
    {
        if (EditorApplication.isPlaying || EditorApplication.isPaused) return;

        if (File.Exists(TriggerFile))
        {
            try
            {
                File.Delete(TriggerFile);
                Debug.Log("[AutoRunLevel2Builder] Ejecutando Reconstruir Nivel 2...");
                Level2SceneBuilder.BuildLevel2(false);
                Debug.Log("[AutoRunLevel2Builder] ¡Nivel 2 Reconstruido y guardado con éxito!");
                Level2Validation.RunAndRender();
                Debug.Log("[AutoRunLevel2Builder] ¡Validación y render de Nivel 2 completados con éxito!");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[AutoRunLevel2Builder] Error al ejecutar: " + ex);
            }
        }
    }
}

