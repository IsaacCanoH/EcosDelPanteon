using UnityEngine;
using UnityEngine.InputSystem;

// Un mapa compartido para que movimiento, busqueda, piedras y pistas lean los mismos controles.
public static class GameControls
{
    private static InputActionAsset runtimeActions;
    private static InputAction move;
    private static InputAction search;
    private static InputAction throwStone;
    private static InputAction clue;
    private static InputAction lantern;
    private static bool reportedMissing;

    public static Vector2 Move
    {
        get { return Ready() ? move.ReadValue<Vector2>() : Vector2.zero; }
    }
    public static bool SearchHeld
    {
        get { return Ready() && search.IsPressed(); }
    }
    public static bool ThrowPressed
    {
        get { return Ready() && throwStone.WasPressedThisFrame(); }
    }
    public static bool CluePressed
    {
        get { return Ready() && clue.WasPressedThisFrame(); }
    }

    public static bool LanternPressed
    {
        get { return Ready() && lantern.WasPressedThisFrame(); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Shutdown();
        reportedMissing = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (runtimeActions != null) return;
        InputActionAsset source = Resources.Load<InputActionAsset>("Input/EcosControls");
        if (source == null)
        {
            if (!reportedMissing)
                Debug.LogError("Falta Resources/Input/EcosControls.inputactions.");
            reportedMissing = true;
            return;
        }
        // La copia evita modificar el asset compartido al entrar o salir de Play.
        runtimeActions = Object.Instantiate(source);
        runtimeActions.name = "EcosControls_Runtime";
        runtimeActions.hideFlags = HideFlags.DontSave;
        move = runtimeActions.FindAction("Player/Move", true);
        search = runtimeActions.FindAction("Player/Search", true);
        throwStone = runtimeActions.FindAction("Player/Throw", true);
        clue = runtimeActions.FindAction("Player/Clue", true);
        lantern = runtimeActions.FindAction("Player/Lantern", true);
        runtimeActions.Enable();
        Application.quitting += Shutdown;
    }

    private static bool Ready()
    {
        if (!Application.isPlaying) return false;
        if (runtimeActions == null) Initialize();
        return runtimeActions != null;
    }

    private static void Shutdown()
    {
        Application.quitting -= Shutdown;
        if (runtimeActions != null)
        {
            runtimeActions.Disable();
            Object.Destroy(runtimeActions);
        }
        runtimeActions = null;
        move = search = throwStone = clue = lantern = null;
    }
}
