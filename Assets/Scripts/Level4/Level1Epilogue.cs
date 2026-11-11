using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Cainos.PixelArtTopDown_Basic;

[DefaultExecutionOrder(-10000)]
public sealed class Level1Epilogue : MonoBehaviour
{
    [SerializeField] private PlayerHealth player;
    [SerializeField] private LevelOneEnding result;
    [SerializeField] private CemeteryGate sideGate;
    [SerializeField] private Transform altar;
    [SerializeField] private IrisInNivel iris;
    [SerializeField] private Camera sceneCamera;
    [SerializeField] private Light2D ambientLight;
    [SerializeField] private ParchmentMessageUI parchment;
    [SerializeField] private LloronaEnemyAI llorona;
    [SerializeField] private Level4LloronaFinalEffects ghostEffects;
    [SerializeField] private Sprite dollSprite;
    [SerializeField] private GameObject[] gameplayObjects;

    private bool active;
    private Behaviour follow;
    private CameraBounds2D cameraBounds;
    private float originalZoom;

    private void Awake()
    {
        active = CemeteryEpilogueRoute.Consume();
        if (!active) return;

        result.enabled = false;
        result.clues.enabled = false;
        foreach (var item in gameplayObjects) if (item != null) item.SetActive(false);
        player.GetComponent<TopDownCharacterController>().enabled = false;
        player.GetComponent<Collider2D>().enabled = false;
        player.GetComponent<PlayerStoneThrow>().enabled = false;
        player.GetComponent<PlayerLantern>().enabled = false;
        player.GetComponent<PlayerCoinSearch>().enabled = false;
        player.GetComponent<GoldCoinCollector>().enabled = false;

        follow = result.cameraFollow;
        if (follow == null) follow = sceneCamera.GetComponent<CameraFollow>();
        cameraBounds = sceneCamera.GetComponent<CameraBounds2D>();
        SetFollowing(false);
        originalZoom = sceneCamera.orthographicSize;
        sceneCamera.orthographicSize = 3.55f;
        Frame(new Vector2(sideGate.transform.position.x - 2.65f, sideGate.transform.position.y));
        CemeterySceneMotion.Place(player, sideGate.Outside);
        parchment.gameObject.SetActive(true);
        if (parchment.topRightClueAccess != null) parchment.topRightClueAccess.SetActive(false);
    }

    private IEnumerator Start()
    {
        if (!active) yield break;
        yield return sideGate.Animate(true);
        yield return CemeterySceneMotion.Walk(player, sideGate.Inside, 2.45f);
        yield return sideGate.Animate(false);
        while (iris.isActiveAndEnabled && !iris.IsComplete) yield return null;

        yield return ResumeWalkingCamera();
        result.exitNavigation.Rebuild();
        Vector2 atAltar = (Vector2)altar.position + Vector2.down * 1.12f;
        yield return CemeterySceneMotion.WalkPath(player, result.exitNavigation, atAltar);

        SetFollowing(false);
        yield return CemeterySceneMotion.Pan(sceneCamera, (Vector2)altar.position + Vector2.up * 0.5f, 3.3f, 0.60f);
        var altarVisual = altar.GetComponent<SpriteRenderer>();
        var playerSorting = player.GetComponent<SortingGroup>();
        int playerOrder = playerSorting.sortingOrder;
        int dollOrder = altarVisual.sortingOrder + 1;
        playerSorting.sortingOrder = Mathf.Max(playerOrder, dollOrder + 1);
        yield return LayDolls(altarVisual.sortingLayerID, dollOrder);

        var sprite = llorona.GetComponent<SpriteRenderer>();
        llorona.gameObject.SetActive(true);
        sprite.color = new Color(1f, 1f, 1f, 0.78f);
        llorona.PlayFinalPose(LloronaEnemyAI.EnemyState.Appearing, altar.position - llorona.transform.position);
        yield return new WaitForSeconds(0.66f);
        yield return Level4StoryDialogue.Say("«Perdónenme por retenerlos tanto tiempo. Ya pueden descansar.»");
        if (parchment.topRightClueAccess != null) parchment.topRightClueAccess.SetActive(false);
        ghostEffects.PlayLament();
        llorona.PlayFinalPose(LloronaEnemyAI.EnemyState.Disappearing, Vector2.left);
        yield return ghostEffects.Dissolve(sprite, sprite.color);
        yield return new WaitForSeconds(0.48f);
        llorona.gameObject.SetActive(false);
        yield return Dawn();

        yield return ResumeWalkingCamera();
        yield return CemeterySceneMotion.WalkPath(player, result.exitNavigation, result.gatePoint.position);
        playerSorting.sortingOrder = playerOrder;
        SetFollowing(false);
        yield return CemeterySceneMotion.Walk(player, result.offscreenPoint.position, 2.65f);
        yield return CemeterySceneMotion.Iris(iris, player.transform, true, 0.82f);
        result.ShowEpilogueVictory();
    }

    private IEnumerator LayDolls(int sortingLayer, int sortingOrder)
    {
        Vector2 start = (Vector2)player.transform.position + Vector2.up * 0.55f;
        for (int i = 0; i < ToyCollector.RequiredToys; i++)
        {
            var doll = Level4SpiritEffects.CreateVisual("Muneco_En_Altar_" + (i + 1), dollSprite, sortingOrder, transform);
            doll.sortingLayerID = sortingLayer;
            doll.transform.localScale = Vector3.one * 0.52f;
            Vector2 target = (Vector2)altar.position + new Vector2((i - 1) * 0.36f, 0.38f);
            const float duration = 0.32f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float progress = Mathf.Clamp01(t / duration);
                doll.transform.position = Vector2.Lerp(start, target, progress) +
                    Vector2.up * (0.25f * Mathf.Sin(progress * Mathf.PI));
                yield return null;
            }
            doll.transform.position = target;
        }
    }

    private IEnumerator Dawn()
    {
        Color before = ambientLight.color;
        float beforeIntensity = ambientLight.intensity;
        var sunrise = new Color(1f, 0.83f, 0.62f);
        const float duration = 1.45f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float progress = Mathf.SmoothStep(0f, 1f, t / duration);
            ambientLight.color = Color.Lerp(before, sunrise, progress);
            ambientLight.intensity = Mathf.Lerp(beforeIntensity, 1.18f, progress);
            yield return null;
        }
        ambientLight.color = sunrise;
        ambientLight.intensity = 1.18f;
    }

    private IEnumerator ResumeWalkingCamera()
    {
        Vector2 center = player.transform.position;
        if (cameraBounds != null)
        {
            Vector2 half = new Vector2(originalZoom * sceneCamera.aspect, originalZoom);
            Vector2 min = cameraBounds.minBounds + Vector2.one * cameraBounds.edgeMargin + half;
            Vector2 max = cameraBounds.maxBounds - Vector2.one * cameraBounds.edgeMargin - half;
            center.x = min.x < max.x ? Mathf.Clamp(center.x, min.x, max.x) : (min.x + max.x) * 0.5f;
            center.y = min.y < max.y ? Mathf.Clamp(center.y, min.y, max.y) : (min.y + max.y) * 0.5f;
        }
        yield return CemeterySceneMotion.Pan(sceneCamera, center, originalZoom, 0.52f);
        SetFollowing(true);
    }

    private void SetFollowing(bool enabled)
    {
        if (follow != null) follow.enabled = enabled;
        if (cameraBounds != null) cameraBounds.enabled = enabled;
    }

    private void Frame(Vector2 center)
    {
        sceneCamera.transform.position = new Vector3(center.x, center.y, sceneCamera.transform.position.z);
    }
}
