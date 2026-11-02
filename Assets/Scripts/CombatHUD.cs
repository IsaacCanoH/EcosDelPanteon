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

    private EncounterWaveManager[] level2Managers;

    private void Start()
    {
        level2Managers = FindObjectsOfType<EncounterWaveManager>(true);
    }

    private void LateUpdate()
    {
        if (lantern != null && lanternHelp != null)
            lanternHelp.text = lantern.CooldownRemaining > 0f ? "RECUPERANDO LUZ..." : "DESTELLO";

        if (wavePanel == null) return;

        // Soporte para Encuentros del Nivel 2 (EncounterWaveManager)
        EncounterWaveManager activeEncounter = null;
        if (level2Managers != null)
        {
            for (int i = 0; i < level2Managers.Length; i++)
            {
                if (level2Managers[i] != null && level2Managers[i].IsEncounterActive)
                {
                    activeEncounter = level2Managers[i];
                    break;
                }
            }
        }

        if (activeEncounter != null)
        {
            wavePanel.alpha = 1f;
            int totalWaves = activeEncounter.waves != null ? Mathf.Max(1, activeEncounter.waves.Length) : 2;
            int currentWave = Mathf.Clamp(activeEncounter.CurrentWaveIndex + 1, 1, totalWaves);
            if (waveTitle != null) waveTitle.text = "OLEADA " + currentWave + " DE " + totalWaves;
            if (waveCount != null) waveCount.text = "DUENDES RESTANTES: " + activeEncounter.RemainingInCurrentWave;
            return;
        }

        if (director == null)
        {
            wavePanel.alpha = 0f;
            return;
        }

        wavePanel.alpha = director.IsWaveActive || director.IsComplete ? 1f : 0f;
        if (waveTitle != null) waveTitle.text = "OLEADA " + director.CurrentWave + " DE 3";
        if (waveCount != null) waveCount.text = "DUENDES RESTANTES: " + director.RemainingEnemies;
    }
}
