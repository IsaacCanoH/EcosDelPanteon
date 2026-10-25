using TMPro;
using UnityEngine;

public class CombatHUD : MonoBehaviour
{
    public PlayerLantern lantern;
    public GoblinWaveDirector director;
    public TMP_Text lanternLabel;
    public TMP_Text lanternHelp;
    public TMP_Text waveTitle;
    public TMP_Text waveCount;
    public CanvasGroup wavePanel;

    private void LateUpdate()
    {
        if (lantern != null && lanternHelp != null)
            lanternHelp.text = lantern.CooldownRemaining > 0f ? "RECUPERANDO LUZ..." : "DESTELLO";
        if (director == null || wavePanel == null) return;
        wavePanel.alpha = director.IsWaveActive || director.IsComplete ? 1f : 0f;
        if (waveTitle != null) waveTitle.text = "OLEADA " + director.CurrentWave + " DE 3";
        if (waveCount != null) waveCount.text = "DUENDES RESTANTES: " + director.RemainingEnemies;
    }
}
