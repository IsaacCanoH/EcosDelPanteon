using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class Level2PlayVerification
{
    static Level2PlayVerification()
    {
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("Level2Probe",false))
            {
                SessionState.SetBool("Level2Probe",false);
                new GameObject("Level2 runtime checks").AddComponent<Level2RuntimeProbe>();
            }
        };
    }
    public static void Begin()
    {
        Level2Validation.RunAndRender();
        EditorSceneManager.OpenScene("Assets/Scenes/Cementerio_Nivel2.unity");
        SessionState.SetBool("Level2Probe",true);
        EditorApplication.EnterPlaymode();
    }
}
