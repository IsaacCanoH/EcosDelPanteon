using UnityEngine;

public sealed class Level4EncounterSupport : MonoBehaviour
{
    private Collider2D playerFeet;
    private GoblinNavigation2D navigation;
    private EncounterWaveManager waves;
    private SpectralChildBossAI boss;

    internal void Begin(PlayerHealth target, GoblinNavigation2D navigator,
                        EncounterWaveManager manager, SpectralChildBossAI encounterBoss)
    {
        playerFeet = target.GetComponent<Collider2D>();
        navigation = navigator;
        waves = manager;
        boss = encounterBoss;
        boss.OnRetreatChanged += HandleRetreat;
    }

    internal void EndEncounter()
    {
        if (waves != null) waves.SetRetreatPressure(false);
        if (boss != null) boss.OnRetreatChanged -= HandleRetreat;
    }

    private void HandleRetreat(bool retreating)
    {
        waves.SetRetreatPressure(retreating);
        if (!retreating) return;

        Vector2 origin = playerFeet.bounds.center;
        foreach (var goblin in GoblinAI.ActiveAgents)
        {
            if (goblin == null || !goblin.isActiveAndEnabled) continue;
            if (goblin.transform.IsChildOf(waves.transform) ||
                (Vector2.SqrMagnitude((Vector2)goblin.transform.position - origin) < 36f &&
                 navigation.HasLineOfSight(origin, goblin.GetComponent<Collider2D>().bounds.center)))
                goblin.Alert();
        }
    }

    private void OnDisable()
    {
        EndEncounter();
    }
}
