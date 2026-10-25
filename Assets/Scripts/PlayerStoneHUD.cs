using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerStoneHUD : MonoBehaviour
{
    public PlayerStoneThrow thrower;
    public TMP_Text countLabel;
    public TMP_Text helpLabel;
    public Image icon;
    public CanvasGroup group;

    private void LateUpdate()
    {
        if (thrower == null) return;
        if (countLabel != null)
            countLabel.text = "PIEDRAS " + thrower.RemainingStones + "/" + thrower.startingStones;
        bool available = thrower.RemainingStones > 0;
        if (helpLabel != null) helpLabel.text = available ? "LANZAR" : "SIN PIEDRAS";
        Color tint = available ? new Color(0.9f, 0.86f, 0.72f, 1f) : new Color(0.55f, 0.55f, 0.55f, 1f);
        if (countLabel != null) countLabel.color = tint;
        if (icon != null)
            icon.color = thrower.CooldownRemaining > 0f ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
        if (group != null)
            group.alpha = thrower.opening != null && thrower.opening.isActiveAndEnabled
                ? 0f : (thrower.CanProjectilesContinue ? 1f : 0.45f);
    }
}
