using UnityEngine;

// No virtual mouse or keyboard: each finger owns only the control it touched.
public enum AndroidTouchAction { Attack, Search, Stone, Weapon, Clue, Pause }

public static class AndroidTouchInput
{
    public static bool IsAndroid
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return Application.platform == RuntimePlatform.Android;
#else
            return false;
#endif
        }
    }

    public static Vector2 Move { get; internal set; }
    public static bool SearchHeld { get; internal set; }
    public static bool DialogueOwnsClue { get; internal set; }
    private static readonly int[] pulseFrames = new int[6];

    // Animator directions: 0 down, 1 up, 2 right, 3 left. The four sectors
    // meet along an X; movement itself remains analog and can be diagonal.
    public static int ResolveFacingDirection(Vector2 movement, int previousDirection)
    {
        // Match the joystick's existing central dead zone and keep the last pose.
        if (movement.sqrMagnitude < 0.0225f) return previousDirection;

        float horizontal = Mathf.Abs(movement.x);
        float vertical = Mathf.Abs(movement.y);

        // About five degrees of hysteresis on either side of each diagonal.
        // Only retain a direction that still points toward the current input;
        // reversing the stick must turn the player immediately.
        const float switchRatio = 1.2f;
        bool wasHorizontal = (previousDirection == 2 && movement.x > 0f) ||
                             (previousDirection == 3 && movement.x < 0f);
        bool wasVertical = (previousDirection == 1 && movement.y > 0f) ||
                           (previousDirection == 0 && movement.y < 0f);

        if (wasHorizontal && vertical <= horizontal * switchRatio)
            return previousDirection;
        if (wasVertical && horizontal <= vertical * switchRatio)
            return previousDirection;

        if (horizontal >= vertical) return movement.x < 0f ? 3 : 2;
        return movement.y > 0f ? 1 : 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetAll()
    {
        Move = Vector2.zero;
        SearchHeld = false;
        DialogueOwnsClue = false;
        for (int i = 0; i < pulseFrames.Length; i++) pulseFrames[i] = -1;
    }

    public static void Press(AndroidTouchAction action)
    {
        // EventSystem runs after several gameplay Updates (-120/-100). Deliver on
        // the next frame so every reader sees one and the same press, even paused.
        pulseFrames[(int)action] = Time.frameCount + 1;
    }

    public static bool WasPressed(AndroidTouchAction action)
    {
        return pulseFrames[(int)action] == Time.frameCount;
    }

    public static void ReleaseGameplay()
    {
        Move = Vector2.zero;
        SearchHeld = false;
        for (int i = 0; i < (int)AndroidTouchAction.Clue; i++) pulseFrames[i] = -1;
    }
}
