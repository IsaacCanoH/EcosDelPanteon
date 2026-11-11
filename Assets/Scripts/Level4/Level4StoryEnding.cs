using System.Collections;
using UnityEngine;

public sealed class Level4StoryEnding : MonoBehaviour
{
    [SerializeField] private PlayerHealth player;
    [SerializeField] private Level4LloronaBoss boss;
    [SerializeField] private CemeteryGate exitGate;
    [SerializeField] private IrisInNivel iris;
    [SerializeField] private Camera sceneCamera;
    [SerializeField] private GameObject[] gameplayPanels;

    private ToyCollector collector;
    internal bool IsRunning { get; private set; }

    private void OnEnable()
    {
        collector = player.GetComponent<ToyCollector>();
        collector.Changed += CheckEnding;
    }

    private void OnDisable()
    {
        if (collector != null) collector.Changed -= CheckEnding;
    }

    private void CheckEnding(int count)
    {
        if (IsRunning || count < ToyCollector.RequiredToys || !boss.IsDefeated || player.IsDead) return;
        IsRunning = true;
        StartCoroutine(CrossExit());
    }

    private IEnumerator CrossExit()
    {
        var goblins = FindObjectsOfType<GoblinAI>();
        foreach (var goblin in goblins)
        {
            goblin.enabled = false;
            if (goblin.TryGetComponent(out GoblinArcherController archer)) archer.enabled = false;
        }
        foreach (var arrow in FindObjectsOfType<GoblinArrowProjectile>()) arrow.Dispel();
        foreach (var projectile in FindObjectsOfType<SpectralProjectile>()) projectile.Dispel();

        player.GetComponent<Cainos.PixelArtTopDown_Basic.TopDownCharacterController>().enabled = false;
        CemeterySceneMotion.Place(player, player.transform.position);
        player.GetComponent<Animator>().SetBool("IsMoving", false);
        player.GetComponent<PlayerWeaponController>().enabled = false;
        player.GetComponent<PlayerLantern>().enabled = false;
        player.GetComponent<PlayerStoneThrow>().enabled = false;
        var parchment = ParchmentMessageUI.Instance;
        if (parchment != null && parchment.TryGetComponent(out DollObjectiveUI objective)) objective.enabled = false;

        yield return new WaitForSeconds(0.52f);
        yield return CemeterySceneMotion.Iris(iris, player.transform, true, 0.60f);
        foreach (var panel in gameplayPanels) if (panel != null) panel.SetActive(false);
        if (parchment != null && parchment.topRightClueAccess != null)
            parchment.topRightClueAccess.SetActive(false);
        foreach (var goblin in goblins) if (goblin != null) goblin.gameObject.SetActive(false);

        if (sceneCamera.TryGetComponent(out Cainos.PixelArtTopDown_Basic.CameraFollow follow)) follow.enabled = false;
        if (sceneCamera.TryGetComponent(out CameraFollow simpleFollow)) simpleFollow.enabled = false;
        if (sceneCamera.TryGetComponent(out CameraBounds2D bounds)) bounds.enabled = false;
        player.GetComponent<Collider2D>().enabled = false;
        CemeterySceneMotion.Place(player, exitGate.Inside);
        Vector3 center = exitGate.transform.position + Vector3.down * 1.45f;
        center.z = sceneCamera.transform.position.z;
        sceneCamera.transform.position = center;
        sceneCamera.orthographicSize = 3.55f;

        yield return CemeterySceneMotion.Iris(iris, player.transform, false, 0.72f);
        yield return exitGate.Animate(true);
        yield return CemeterySceneMotion.Walk(player, exitGate.Outside, 2.55f);
        yield return CemeterySceneMotion.Iris(iris, player.transform, true, 0.82f);
        CemeteryEpilogueRoute.ReturnToFirstCemetery();
    }
}

internal static class Level4StoryDialogue
{
    internal static IEnumerator Say(string text)
    {
        var parchment = ParchmentMessageUI.Instance;
        if (parchment == null) yield break;
        while (parchment != null && parchment.IsOpen) yield return null;
        if (parchment == null) yield break;

        float originalTime = Time.timeScale;
        bool originalAudio = AudioListener.pause;
        bool closed = false;
        parchment.Show("LA LLORONA", text, onClosed: () =>
        {
            Time.timeScale = originalTime;
            AudioListener.pause = originalAudio;
            closed = true;
        });
        while (!closed && parchment != null) yield return null;
    }
}
