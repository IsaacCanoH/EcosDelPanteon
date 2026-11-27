using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

// Set up the new UI module before the first EventSystem.Update in every scene.
// Existing scenes and their visual layout remain unchanged.
public static class GameInputUI
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneLoaded += SceneLoaded;
    }

    private static void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureEventSystem();
    }

    public static EventSystem EnsureEventSystem()
    {
        var systems = Object.FindObjectsOfType<EventSystem>();
        if (systems.Length == 0)
            systems = new[] { new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>() };

        foreach (var system in systems)
        {
            foreach (var oldModule in system.GetComponents<BaseInputModule>())
                if (!(oldModule is InputSystemUIInputModule)) oldModule.enabled = false;

            var module = system.GetComponent<InputSystemUIInputModule>();
            if (module == null) module = system.gameObject.AddComponent<InputSystemUIInputModule>();
            if (module.point == null || module.leftClick == null || module.submit == null || module.cancel == null)
                module.AssignDefaultActions();
            if (AndroidTouchInput.IsAndroid) module.pointerBehavior = UIPointerBehavior.AllPointersAsIs;
            module.enabled = true;
        }

        return EventSystem.current != null ? EventSystem.current : systems[0];
    }
}
