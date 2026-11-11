using System.Collections;
using UnityEngine;

public sealed class Level4ChildEncounter : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private IrisInNivel openingIris;
    [SerializeField] private CemeteryGate entranceGate;
    [SerializeField] private Canvas hud;
    [SerializeField] private PlayerHealth player;
    [SerializeField] private LloronaEnemyAI llorona;
    [SerializeField] private Transform childStatue;
    [SerializeField] private SpectralChildBossAI child;
    [SerializeField] private EnemyHealth childHealth;
    [SerializeField] private SpriteRenderer childVisual;
    [SerializeField] private GoblinNavigation2D navigation;
    [SerializeField] private EncounterWaveManager waves;
    [SerializeField] private ToyPickup firstDoll;
    [SerializeField] private Sprite spellSprite;
    [SerializeField] private AudioClip spellSound;
    [SerializeField] private int prerequisiteDollId = -1;
    [SerializeField] private bool placeWavesNearPlayer;
    [SerializeField] private bool approachFromStatue;
    [SerializeField] private string bossDisplayName = "NIÑO ESPECTRAL";

    private CameraFollow follow;
    private CameraBounds2D bounds;
    private Animator lloronaAnimator;
    private AnimatorUpdateMode originalAnimatorMode;
    private Behaviour movement;
    private float originalZoom;
    private float originalTimeScale;
    private bool followWasEnabled;
    private bool boundsWereEnabled;
    private bool movementWasEnabled;
    private bool hudWasEnabled;
    private bool controlsLocked;
    private SpriteRenderer spell;
    private bool rewarded;
    private Level4EncounterSupport support;

    private IEnumerator Start()
    {
        if (mainCamera == null || hud == null || player == null || llorona == null ||
            childStatue == null || child == null || childHealth == null || childVisual == null ||
            navigation == null || waves == null || firstDoll == null || spellSprite == null)
        {
            Debug.LogError("Faltan referencias para el encuentro del nivel 4.", this);
            yield break;
        }

        if (prerequisiteDollId >= 0)
        {
            var collector = player.GetComponent<ToyCollector>();
            while (!player.IsDead && !collector.HasToy(prerequisiteDollId))
                yield return null;

            yield return new WaitForSeconds(0.55f);
            while (ParchmentMessageUI.Instance != null && ParchmentMessageUI.Instance.IsOpen)
                yield return null;
            if (player.IsDead) yield break;
        }

        LockForOpening();
        if (openingIris != null)
        {
            if (entranceGate != null)
            {
                Vector3 view = entranceGate.transform.position + Vector3.up * 2f;
                mainCamera.transform.position = CameraPosition(view);
                mainCamera.orthographicSize = 4.15f;
                yield return entranceGate.Animate(true);
                yield return CemeterySceneMotion.Walk(player, entranceGate.Inside, 2.45f);
                yield return entranceGate.Animate(false);
            }
            else
            {
                mainCamera.transform.position = CameraPosition(player.transform.position);
            }

            while (openingIris.isActiveAndEnabled && !openingIris.IsComplete)
                yield return null;
        }

        var casterVisual = llorona.GetComponent<SpriteRenderer>();
        var casterFocus = llorona.transform.position + Vector3.up * 0.72f;
        var statueFocus = childStatue.position + Vector3.up * 0.95f;
        const float cinematicZoom = 3.05f;
        PoseLlorona("Llorona_Idle", 0, statueFocus - casterFocus);
        Vector3 casterCameraPosition = CameraPosition(casterFocus);
        if (openingIris != null)
        {
            const float travelDuration = 2.05f;
            Vector3 playerCameraPosition = mainCamera.transform.position;
            float startingZoom = mainCamera.orthographicSize;
            for (float elapsed = 0f; elapsed < travelDuration; elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f))
            {
                float progress = Mathf.SmoothStep(0f, 1f, elapsed / travelDuration);
                mainCamera.transform.position = Vector3.Lerp(playerCameraPosition, casterCameraPosition, progress);
                mainCamera.orthographicSize = Mathf.Lerp(startingZoom, cinematicZoom, progress);
                yield return null;
            }
        }

        mainCamera.transform.position = casterCameraPosition;
        mainCamera.orthographicSize = cinematicZoom;
        if (!casterVisual.enabled)
        {
            PoseLlorona("Llorona_Appear", 6, statueFocus - casterFocus);
            casterVisual.enabled = true;
            yield return new WaitForSecondsRealtime(0.66f);
        }
        else
        {
            yield return new WaitForSecondsRealtime(0.45f);
        }

        hud.enabled = true;
        yield return Level4StoryDialogue.Say(prerequisiteDollId < 0
            ? "«Ya deshiciste el hechizo de la Bruja. Si también te llevas estos muñecos, ¿qué me quedará de mis hijos?»"
            : "«Devuélvemelo. Todavía recuerdo el día en que se lo di.»");
        hud.enabled = false;

        PoseLlorona("Llorona_Scream", 3, statueFocus - casterFocus);
        yield return new WaitForSecondsRealtime(0.26f);
        if (spellSound != null)
            AudioSource.PlayClipAtPoint(spellSound, casterFocus, 0.65f * AudioManager.EnemyVolume);

        spell = new GameObject("Hechizo_Estatua").AddComponent<SpriteRenderer>();
        spell.sprite = spellSprite;
        spell.sortingLayerName = "Layer 1";
        spell.sortingOrder = 12;
        spell.color = new Color(0.55f, 1f, 0.84f, 1f);
        Vector3 spellStart = casterFocus + Vector3.up * 0.15f;
        Vector3 cameraStart = mainCamera.transform.position;
        Vector3 cameraEnd = CameraPosition(statueFocus);
        bool disappearing = false;
        for (float elapsed = 0f; elapsed < 0.68f; elapsed += Time.unscaledDeltaTime)
        {
            if (!disappearing && elapsed >= 0.22f)
            {
                PoseLlorona("Llorona_Disappear", 7, statueFocus - casterFocus);
                disappearing = true;
            }

            float progress = Mathf.Clamp01(elapsed / 0.68f);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            spell.transform.position = Vector3.Lerp(spellStart, statueFocus, progress) +
                Vector3.up * (Mathf.Sin(progress * Mathf.PI) * 0.54f);
            spell.transform.localScale = Vector3.one * (0.95f + Mathf.Sin(progress * Mathf.PI) * 0.28f);
            mainCamera.transform.position = Vector3.Lerp(cameraStart, cameraEnd, eased);
            yield return null;
        }

        mainCamera.transform.position = cameraEnd;
        spell.transform.position = statueFocus;
        Vector3 childExit = child.transform.position;
        Vector3 childOrigin = childStatue.position + Vector3.up * 0.30f;
        child.transform.position = childOrigin;
        childVisual.color = new Color(1f, 1f, 1f, 0f);
        child.gameObject.SetActive(true);
        if (!disappearing) PoseLlorona("Llorona_Disappear", 7, statueFocus - casterFocus);
        for (float elapsed = 0f; elapsed < 0.66f; elapsed += Time.unscaledDeltaTime)
        {
            if (elapsed >= 0.22f) casterVisual.enabled = false;
            float progress = Mathf.Clamp01(elapsed / 0.66f);
            child.transform.position = Vector3.Lerp(childOrigin, childExit, Mathf.SmoothStep(0f, 1f, progress));
            childVisual.color = new Color(1f, 1f, 1f, progress);
            spell.transform.localScale = Vector3.one * Mathf.Lerp(1f, 2.45f, progress);
            spell.color = new Color(0.55f, 1f, 0.84f, 1f - progress);
            yield return null;
        }
        child.transform.position = childExit;
        child.GetComponent<Rigidbody2D>().position = childExit;
        childVisual.color = Color.white;
        casterVisual.enabled = false;
        Destroy(spell.gameObject);
        spell = null;

        yield return new WaitForSecondsRealtime(0.24f);
        Vector3 playerFocus = CameraPosition(player.transform.position);
        for (float elapsed = 0f; elapsed < 1.1f; elapsed += Time.unscaledDeltaTime)
        {
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / 1.1f);
            mainCamera.transform.position = Vector3.Lerp(cameraEnd, playerFocus, progress);
            mainCamera.orthographicSize = Mathf.Lerp(cinematicZoom, originalZoom, progress);
            yield return null;
        }
        mainCamera.transform.position = playerFocus;

        RestorePlayerView();
        childHealth.OnDied += CompleteEncounter;
        child.OnDeathCinematicFinished += RevealDoll;
        child.BeginEncounterCombat(player, navigation, waves.transform, spellSprite, approachFromStatue);
        if (BossHealthBarUI.Instance != null)
            BossHealthBarUI.Instance.BindBoss(childHealth, bossDisplayName);
        if (placeWavesNearPlayer)
            PlaceWaveSpawns();
        support = gameObject.AddComponent<Level4EncounterSupport>();
        support.Begin(player, navigation, waves, child);
        waves.StartEncounter();
    }

    private void PlaceWaveSpawns()
    {
        Vector2 origin = player.GetComponent<Collider2D>().bounds.center;
        var route = new System.Collections.Generic.List<Vector2>();
        int count = 0;
        for (int sector = 0; sector < 12 && count < waves.spawnPoints.Length; sector++)
        {
            float angle = (sector * 5 % 12) * Mathf.PI / 6f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            for (float radius = 5.5f; radius <= 7.5f; radius += 1f)
            {
                Vector2 point = origin + direction * radius;
                if (!navigation.Clear(point, point) ||
                    !navigation.FindPath(point, origin, route) ||
                    route.Count == 0 || Vector2.Distance(route[route.Count - 1], origin) > 1f)
                    continue;

                waves.spawnPoints[count++].position = point - Vector2.up * 0.16f;
                break;
            }
        }
    }

    private void LockForOpening()
    {
        follow = mainCamera.GetComponent<CameraFollow>();
        bounds = mainCamera.GetComponent<CameraBounds2D>();
        movement = player.GetComponent<Cainos.PixelArtTopDown_Basic.TopDownCharacterController>();
        lloronaAnimator = llorona.GetComponent<Animator>();
        originalAnimatorMode = lloronaAnimator.updateMode;
        originalZoom = mainCamera.orthographicSize;
        originalTimeScale = Time.timeScale;
        followWasEnabled = follow != null && follow.enabled;
        boundsWereEnabled = bounds != null && bounds.enabled;
        movementWasEnabled = movement != null && movement.enabled;
        hudWasEnabled = hud.enabled;
        controlsLocked = true;

        if (follow != null) follow.enabled = false;
        if (bounds != null) bounds.enabled = false;
        if (movement != null) movement.enabled = false;
        hud.enabled = false;
        llorona.enabled = false;
        llorona.GetComponent<Collider2D>().enabled = false;
        lloronaAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        Time.timeScale = 0f;
    }

    private void PoseLlorona(string animation, int action, Vector2 direction)
    {
        int facing = Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
            ? (direction.x > 0f ? 1 : 2)
            : (direction.y > 0f ? 3 : 0);
        lloronaAnimator.SetFloat("Direction", facing);
        lloronaAnimator.SetInteger("ActionState", action);
        lloronaAnimator.Play(animation, 0, 0f);
    }

    private Vector3 CameraPosition(Vector3 world)
    {
        world.z = mainCamera.transform.position.z;
        return world;
    }

    private void RestorePlayerView()
    {
        if (!controlsLocked) return;
        controlsLocked = false;
        mainCamera.orthographicSize = originalZoom;
        if (follow != null)
        {
            follow.target = player.transform;
            follow.enabled = followWasEnabled;
        }
        if (bounds != null) bounds.enabled = boundsWereEnabled;
        if (movement != null) movement.enabled = movementWasEnabled;
        hud.enabled = hudWasEnabled;
        lloronaAnimator.updateMode = originalAnimatorMode;
        Time.timeScale = originalTimeScale;
    }

    private void CompleteEncounter(EnemyHealth defeated)
    {
        if (rewarded) return;
        rewarded = true;
        if (support != null) support.EndEncounter();
        foreach (var arrow in waves.GetComponentsInChildren<GoblinArrowProjectile>())
            arrow.Dispel();
        waves.StopEncounter();
    }

    private void RevealDoll(Vector3 position)
    {
        firstDoll.transform.position = position + Vector3.up * 0.24f;
        firstDoll.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        RestorePlayerView();
        if (spell != null) Destroy(spell.gameObject);
        if (childHealth != null) childHealth.OnDied -= CompleteEncounter;
        if (child != null) child.OnDeathCinematicFinished -= RevealDoll;
    }
}
