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
    private static InputAction pause;
    private static bool reportedMissing;

    public static Vector2 Move
    {
        get { return AndroidTouchInput.IsAndroid ? AndroidTouchInput.Move : (Ready() ? move.ReadValue<Vector2>() : Vector2.zero); }
    }
    public static bool SearchHeld
    {
        get { return AndroidTouchInput.IsAndroid ? AndroidTouchInput.SearchHeld : Ready() && search.IsPressed(); }
    }

    public static bool SearchPressed
    {
        get { return !AndroidTouchInput.IsAndroid && Ready() && search.WasPressedThisFrame(); }
    }
    public static bool ThrowPressed
    {
        get { return AndroidTouchInput.IsAndroid ? AndroidTouchInput.WasPressed(AndroidTouchAction.Stone) : Ready() && throwStone.WasPressedThisFrame(); }
    }
    public static bool CluePressed
    {
        get { return AndroidTouchInput.IsAndroid ? AndroidTouchInput.WasPressed(AndroidTouchAction.Clue) : Ready() && clue.WasPressedThisFrame(); }
    }

    public static bool LanternPressed
    {
        get { return AndroidTouchInput.IsAndroid ? AndroidTouchInput.WasPressed(AndroidTouchAction.Attack) : Ready() && lantern.WasPressedThisFrame(); }
    }

    public static bool PausePressed
    {
        get { return AndroidTouchInput.IsAndroid ? AndroidTouchInput.WasPressed(AndroidTouchAction.Pause) : Ready() && pause.WasPressedThisFrame(); }
    }

    public static bool WeaponCyclePressed
    {
        get
        {
            if (AndroidTouchInput.IsAndroid) return AndroidTouchInput.WasPressed(AndroidTouchAction.Weapon);
            return KeyPressed(Key.Q) || (Mouse.current != null && Mathf.Abs(Mouse.current.scroll.ReadValue().y) > 0.1f);
        }
    }

    public static bool AttackPressed
    {
        get { return LanternPressed || (!AndroidTouchInput.IsAndroid && (KeyPressed(Key.J) || PointerPressed)); }
    }

    // Preserve the existing desktop shortcuts using Input System devices only.
    public static bool OptionsPressed { get { return KeyPressed(Key.O); } }
    public static bool CancelPressed { get { return KeyPressed(Key.Escape); } }
    public static bool RetryPressed { get { return KeyPressed(Key.R) || KeyPressed(Key.Space); } }
    public static bool MenuPressed { get { return KeyPressed(Key.M) || CancelPressed; } }
    public static bool QuitPressed { get { return KeyPressed(Key.Q); } }
    public static bool NextLevelPressed { get { return KeyPressed(Key.Enter) || KeyPressed(Key.NumpadEnter); } }
    public static bool RetryLevelPressed { get { return KeyPressed(Key.R); } }
    public static bool ReturnToMenuPressed { get { return KeyPressed(Key.M); } }
    public static bool HasPointer { get { return Mouse.current != null; } }
    public static Vector2 PointerPosition { get { return HasPointer ? Mouse.current.position.ReadValue() : Vector2.zero; } }
    public static bool PointerPressed { get { return HasPointer && Mouse.current.leftButton.wasPressedThisFrame; } }

    public static bool WeaponSelected(int slot)
    {
        if (AndroidTouchInput.IsAndroid) return false;
        switch (slot)
        {
            case 1: return KeyPressed(Key.Digit1) || KeyPressed(Key.Numpad1);
            case 2: return KeyPressed(Key.Digit2) || KeyPressed(Key.Numpad2);
            case 3: return KeyPressed(Key.Digit3) || KeyPressed(Key.Numpad3);
            default: return false;
        }
    }

    private static bool KeyPressed(Key key)
    {
        return Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
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
        pause = runtimeActions.FindAction("Player/Pause", true);
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
        move = search = throwStone = clue = lantern = pause = null;
    }
}
