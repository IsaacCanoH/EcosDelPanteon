using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LloronaEnemyAI))]
public sealed class Level4LloronaBoss : MonoBehaviour
{
    private const int FirstHealth = 1000;
    private const int SecondHealth = 1200;
    private const int RefillThreshold = 3;
    private const float ClawRange = 1.9f;
    private const float ClawSoundPeak = 0.205f;
    private const float ScreamRadius = 3.25f;
    private const float AuraCenterHeight = 0.77f;
    private static readonly Color BlinkColor = new Color(0.48f, 0.93f, 1f, 0.95f);
    private static readonly Color ScreamColor = new Color(1f, 0.42f, 0.62f, 0.93f);
    private static readonly Color ChargeColor = new Color(1f, 0.17f, 0.20f, 0.82f);

    [SerializeField] private PlayerHealth player;
    [SerializeField] private GoblinNavigation2D navigation;
    [SerializeField] private EncounterWaveManager hordes;
    [SerializeField] private ToyPickup lastDoll;
    [SerializeField] private AudioClip clawSound;
    [SerializeField] private AudioClip screamSound;
    [SerializeField] private AudioClip summonSound;
    [SerializeField] private AudioClip graveSound;
    [SerializeField] private Renderer[] echoStatues = new Renderer[0];

    private LloronaEnemyAI actor;
    private Rigidbody2D body;
    private BoxCollider2D feet;
    private Collider2D playerFeet;
    private SpriteRenderer visual;
    private SpriteRenderer cue;
    private SpriteRenderer heldDoll;
    private Color normalTint;
    private BossHealthBarUI healthBar;
    private Level4LloronaHazards hazards;
    private Level4LloronaFinalEffects finalEffects;
    private AudioSource attackAudio;
    private Coroutine combat;
    private bool vulnerable;
    private bool secondPhase;
    private bool vanishedInSecondPhase;
    private bool nextAttackIsScream;
    private bool evadeRequested;
    private float nextEvade;
    private float nextSummon;
    private int hitsSinceEvade;

    public int CurrentHealth { get; private set; }
    public int MaxHealth => secondPhase ? SecondHealth : FirstHealth;
    public bool IsDefeated { get; private set; }

    private void Awake()
    {
        actor = GetComponent<LloronaEnemyAI>();
        body = GetComponent<Rigidbody2D>();
        feet = GetComponent<BoxCollider2D>();
        visual = GetComponent<SpriteRenderer>();
        normalTint = visual.color;
        feet.enabled = false;
    }

    private IEnumerator Start()
    {
        if (player == null || navigation == null || hordes == null || lastDoll == null ||
            !player.TryGetComponent(out ToyCollector collector))
        {
            Debug.LogError("Faltan referencias para el combate de La Llorona.", this);
            yield break;
        }

        while (!player.IsDead && !collector.HasToy(1))
            yield return null;

        yield return new WaitForSeconds(0.55f);
        while (!player.IsDead && ParchmentMessageUI.Instance != null && ParchmentMessageUI.Instance.IsOpen)
            yield return null;
        if (player.IsDead) yield break;

        playerFeet = player.GetComponent<Collider2D>();
        hordes.ReinforcementSpawnPoint = FindHordeSpawn;
        hazards = gameObject.AddComponent<Level4LloronaHazards>();
        hazards.Begin(player, navigation, graveSound);
        attackAudio = gameObject.AddComponent<AudioSource>();
        attackAudio.playOnAwake = false;
        attackAudio.spatialBlend = 0f;
        finalEffects = gameObject.AddComponent<Level4LloronaFinalEffects>();
        actor.SetTarget(player.transform);
        healthBar = BossHealthBarUI.Instance;
        if (FindBlinkPoint(3.6f, false, out Vector2 entrance) ||
            FindBlinkPoint(2.8f, false, out entrance))
        {
            ShowCue("Entrada_Llorona", entrance, 1.05f, ChargeColor);
            yield return new WaitForSeconds(0.36f);
            PlaceAt(entrance);
            ClearCue();
        }

        actor.PlayFinalPose(LloronaEnemyAI.EnemyState.Appearing, PlayerPoint - FeetPoint);
        visual.enabled = true;
        yield return new WaitForSeconds(0.60f);
        HoldDoll();
        yield return Level4StoryDialogue.Say("«Este se queda conmigo.»");
        yield return FillHealthBar();
        ReleaseHeldDoll();
        if (player.IsDead) yield break;

        nextEvade = Time.time + 5f;
        combat = StartCoroutine(Fight());
    }

    internal void TakeDamage(int amount, Vector2 hitDirection)
    {
        if (!isActiveAndEnabled || !vulnerable || IsDefeated || player.IsDead || amount <= 0) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        if (healthBar != null) healthBar.UpdateDisplay(CurrentHealth, MaxHealth);
        if (CurrentHealth > 0)
        {
            if (CanVanishWithHorde)
            {
                InterruptAttack();
                combat = StartCoroutine(VanishAndReturn());
                return;
            }

            hitsSinceEvade++;
            if (hitsSinceEvade >= 2 && Time.time >= nextEvade)
                evadeRequested = true;
            return;
        }

        InterruptAttack();
        if (secondPhase)
        {
            IsDefeated = true;
            hazards.End();
            hordes.StopEncounter();
            if (healthBar != null) healthBar.FadeOut();
            combat = StartCoroutine(Die(-hitDirection));
        }
        else
        {
            combat = StartCoroutine(Recharge());
        }
    }

    private void InterruptAttack()
    {
        SetVulnerable(false);
        if (combat != null) StopCoroutine(combat);
        if (attackAudio != null) attackAudio.Stop();
        ClearCue();
        hazards.CancelRainWarning();
    }

    private bool CanVanishWithHorde => secondPhase && !vanishedInSecondPhase &&
        CurrentHealth <= SecondHealth / 2 && hordes.RemainingInCurrentWave > 0;

    private IEnumerator VanishAndReturn()
    {
        vanishedInSecondPhase = true;
        SetVulnerable(false);
        hazards.SuspendGraves(true);
        evadeRequested = false;
        hitsSinceEvade = 0;
        actor.PlayFinalPose(LloronaEnemyAI.EnemyState.Disappearing, PlayerPoint - FeetPoint);
        yield return finalEffects.Dissolve(visual, normalTint);
        actor.PlayFinalPose(LloronaEnemyAI.EnemyState.Idle, PlayerPoint - FeetPoint);
        yield return new WaitForSeconds(4f);
        if (player.IsDead) yield break;

        if (!FindBlinkPoint(3.4f, true, out Vector2 point))
            FindBlinkPoint(2.8f, true, out point);
        yield return finalEffects.WarnReturn(point - FeetOffset, visual);
        PlaceAt(point);
        actor.PlayFinalPose(LloronaEnemyAI.EnemyState.Appearing, PlayerPoint - FeetPoint);
        visual.enabled = true;
        yield return new WaitForSeconds(0.55f);
        if (player.IsDead) yield break;

        actor.PlayFinalPose(LloronaEnemyAI.EnemyState.Idle, PlayerPoint - FeetPoint);
        hazards.SuspendGraves(false);
        nextEvade = Time.time + 4f;
        yield return Fight();
    }

    private IEnumerator Fight()
    {
        SetVulnerable(true);
        float attackAt = Time.time + 0.65f;
        while (!player.IsDead)
        {
            if (CanVanishWithHorde)
            {
                yield return VanishAndReturn();
                yield break;
            }

            if (Time.time >= nextSummon && hordes.RemainingInCurrentWave <= RefillThreshold)
            {
                yield return SummonHorde();
                attackAt = Time.time + 1.05f;
            }
            else if (Time.time >= attackAt || (evadeRequested && Time.time >= attackAt - 0.3f))
            {
                if (evadeRequested)
                {
                    evadeRequested = false;
                    hitsSinceEvade = 0;
                    nextEvade = Time.time + 8f;
                }

                bool useScream = secondPhase && nextAttackIsScream;
                nextAttackIsScream = !nextAttackIsScream;
                yield return BlinkAttack(useScream);
                float recovery = secondPhase ? 0.50f : 0.85f;
                if (hordes.ActiveEnemyCount > RefillThreshold) recovery += secondPhase ? 0.25f : 0.55f;
                attackAt = Time.time + recovery;
            }
            yield return null;
        }
        ClearCue();
    }

    private IEnumerator BlinkAttack(bool scream)
    {
        if (!FindBlinkPoint(1.48f, true, out Vector2 point) &&
            !FindBlinkPoint(1.18f, true, out point)) yield break;

        if (secondPhase)
        {
            yield return Blink(point, 0.16f, BlinkColor, 0.16f);
            yield return new WaitForSeconds(0.08f);
            if (FindBlinkPoint(scream ? 2.65f : 1.48f, true, out Vector2 flank))
                point = flank;
            else
                scream = false;
        }

        yield return Blink(point, secondPhase ? 0.21f : 0.35f,
            scream ? ScreamColor : BlinkColor, secondPhase ? 0.18f : 0.24f);
        SetVulnerable(true);

        if (scream)
        {
            Vector2 origin = FeetPoint;
            ShowCue("Aviso_Grito_Llorona", origin, ScreamRadius, ScreamColor);
            actor.PlayFinalPose(LloronaEnemyAI.EnemyState.Screaming, PlayerPoint - origin);
            PlayAttackSound(screamSound, 0.34f);
            yield return new WaitForSeconds(0.444f);
            if (!player.IsDead && (PlayerPoint - origin).sqrMagnitude <= ScreamRadius * ScreamRadius)
                player.TakeDamage(player.HealthPerHeart);
            ClearCue();
            yield return new WaitForSeconds(0.36f);
        }
        else
        {
            Vector2 attackDirection = actor.PlayFinalPose(LloronaEnemyAI.EnemyState.Attacking,
                PlayerPoint - FeetPoint, secondPhase ? 1.20f : 1f);
            float windup = secondPhase ? 0.278f : 0.333f;
            PlayAttackSound(clawSound, 0.60f, windup - ClawSoundPeak);
            yield return new WaitForSeconds(windup);

            Vector2 delta = PlayerPoint - FeetPoint;
            if (!player.IsDead && delta.sqrMagnitude <= ClawRange * ClawRange &&
                (delta.sqrMagnitude < 0.001f || Vector2.Dot(attackDirection, delta.normalized) >= 0.35f) &&
                navigation.HasLineOfSight(FeetPoint, PlayerPoint))
                player.TakeDamage(Mathf.RoundToInt(player.HealthPerHeart * 1.5f));

            yield return new WaitForSeconds(secondPhase ? 0.36f : 0.43f);
        }

        actor.PlayFinalPose(LloronaEnemyAI.EnemyState.Idle, PlayerPoint - FeetPoint);
    }

    private void PlayAttackSound(AudioClip clip, float gain, float delay = 0f)
    {
        if (clip == null) return;

        attackAudio.clip = clip;
        attackAudio.volume = gain * AudioManager.EnemyVolume;
        if (delay > 0f)
            attackAudio.PlayScheduled(AudioSettings.dspTime + delay);
        else
            attackAudio.Play();
    }

    private IEnumerator Blink(Vector2 point, float warningTime, Color? tint = null, float arrival = 0.24f)
    {
        SetVulnerable(false);
        actor.PlayFinalPose(LloronaEnemyAI.EnemyState.Disappearing, PlayerPoint - FeetPoint, 2.8f);
        ShowCue("Aviso_Teletransporte_Llorona", point, 0.42f, tint ?? BlinkColor);
        yield return new WaitForSeconds(warningTime);

        PlaceAt(point);
        ClearCue();
        actor.PlayFinalPose(LloronaEnemyAI.EnemyState.Appearing, PlayerPoint - FeetPoint, 2.8f);
        yield return new WaitForSeconds(arrival);
    }

    private IEnumerator SummonHorde()
    {
        Vector2 origin = FeetPoint;
        hazards.WarnRain();
        ShowAura("Aviso_Lluvia_Llorona", 1.35f, new Color(0.75f, 0.43f, 1f, 0.95f));
        PlayAttackSound(summonSound, 0.24f);
        actor.PlayFinalPose(LloronaEnemyAI.EnemyState.Idle, PlayerPoint - origin);
        yield return new WaitForSeconds(0.48f);

        actor.PlayFinalPose(LloronaEnemyAI.EnemyState.Screaming, PlayerPoint - origin);
        yield return new WaitForSeconds(0.444f);
        if (player.IsDead)
        {
            ClearCue();
            yield break;
        }
        hazards.ReleaseRain();
        if (player.IsDead)
        {
            ClearCue();
            yield break;
        }

        cue.color = new Color(1f, 0.77f, 0.60f, 1f);
        hordes.maxSimultaneousEnemies = secondPhase ? 7 : 6;
        hordes.AddReinforcements(new EncounterWaveManager.WaveDefinition
        {
            waveName = "Horda de La Llorona",
            normalGoblins = secondPhase ? 12 : 10,
            chubbyGoblins = secondPhase ? 4 : 2,
            archerGoblins = secondPhase ? 4 : 3
        });

        yield return new WaitForSeconds(0.40f);
        ClearCue();
        actor.PlayFinalPose(LloronaEnemyAI.EnemyState.Idle, PlayerPoint - FeetPoint);
        nextSummon = Time.time + 5f;
    }

    private IEnumerator Recharge()
    {
        if (FindBlinkPoint(3.6f, false, out Vector2 point))
            yield return Blink(point, 0.34f);

        secondPhase = true;
        nextEvade = Time.time + 4f;
        evadeRequested = false;
        hitsSinceEvade = 0;
        HoldDoll();
        yield return Level4StoryDialogue.Say("«Todavía no… No quiero despedirme otra vez.»");
        yield return FillHealthBar();
        ReleaseHeldDoll();
        finalEffects.PlayStatuePulse(transform.TransformPoint(Vector3.up * AuraCenterHeight), echoStatues);
        nextAttackIsScream = false;
        hazards.BeginGraves();
        yield return Fight();
    }

    private IEnumerator FillHealthBar()
    {
        ShowHealthBar();
        actor.PlayFinalPose(LloronaEnemyAI.EnemyState.Hurt, PlayerPoint - FeetPoint, 0.75f);
        const float duration = 1.35f;
        ShowAura("Recarga_Llorona", 1.35f, ChargeColor);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            visual.color = Color.Lerp(normalTint, new Color(1f, 0.23f, 0.24f, 1f),
                0.58f + 0.35f * Mathf.Sin(progress * Mathf.PI * 5f));
            if (healthBar != null)
                healthBar.UpdateDisplay(Mathf.RoundToInt(Mathf.SmoothStep(0f, MaxHealth, progress)), MaxHealth);
            yield return null;
        }

        ClearCue();
        visual.color = normalTint;
        CurrentHealth = MaxHealth;
        if (healthBar != null) healthBar.UpdateDisplay(CurrentHealth, MaxHealth);
        actor.PlayFinalPose(LloronaEnemyAI.EnemyState.Idle, PlayerPoint - FeetPoint);
    }

    private IEnumerator Die(Vector2 direction)
    {
        visual.color = normalTint;
        actor.PlayFinalPose(LloronaEnemyAI.EnemyState.Hurt, direction, 0.82f);
        yield return new WaitForSeconds(0.35f);

        Vector2 facing = actor.PlayFinalPose(LloronaEnemyAI.EnemyState.Dying, direction);
        Vector2 feetPoint = FeetPoint;
        Vector2 landing = feetPoint + facing * 0.74f;
        if (!navigation.Clear(feetPoint, landing)) landing = feetPoint;
        landing += Vector2.up * 0.24f;
        Vector2 hands = (Vector2)transform.position + Vector2.up * 0.85f + facing * 0.28f;
        yield return new WaitForSeconds(0.24f);

        var doll = lastDoll.GetComponent<SpriteRenderer>();
        var falling = Level4SpiritEffects.CreateVisual("Caida_Muneco_Llorona", doll.sprite, 12, transform);
        falling.sortingLayerID = doll.sortingLayerID;
        falling.transform.localScale = lastDoll.transform.lossyScale;
        const float duration = 0.52f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            falling.transform.position = Vector2.Lerp(hands, landing, progress) +
                Vector2.up * (Mathf.Sin(progress * Mathf.PI) * 0.44f);
            falling.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(progress * Mathf.PI) * 34f);
            visual.color = new Color(normalTint.r, normalTint.g, normalTint.b, normalTint.a * (1f - progress));
            yield return null;
        }

        Destroy(falling.gameObject);
        lastDoll.transform.position = landing;
        lastDoll.gameObject.SetActive(true);
        gameObject.SetActive(false);
    }

    private void HoldDoll()
    {
        var source = lastDoll.GetComponent<SpriteRenderer>();
        heldDoll = Level4SpiritEffects.CreateVisual("Muneco_En_Manos", source.sprite, 12, transform);
        heldDoll.sortingLayerID = visual.sortingLayerID;
        heldDoll.transform.localPosition = new Vector3(0.24f, 0.78f, 0f);
        heldDoll.transform.localScale = lastDoll.transform.lossyScale * 0.82f;
    }

    private void ReleaseHeldDoll()
    {
        if (heldDoll != null) Destroy(heldDoll.gameObject);
        heldDoll = null;
    }

    internal void ShowFarewell(Vector2 position, Vector2 altarPosition)
    {
        gameObject.SetActive(true);
        PlaceAt(position);
        visual.color = new Color(normalTint.r, normalTint.g, normalTint.b, normalTint.a * 0.72f);
        visual.enabled = true;
        actor.PlayFinalPose(LloronaEnemyAI.EnemyState.Appearing, altarPosition - position);
    }

    internal IEnumerator FadeFarewell()
    {
        finalEffects.PlayLament();
        actor.PlayFinalPose(LloronaEnemyAI.EnemyState.Disappearing, Vector2.left);
        yield return finalEffects.Dissolve(visual, visual.color);
        yield return new WaitForSeconds(0.48f);
        gameObject.SetActive(false);
    }

    private Vector2 PlayerPoint => playerFeet.bounds.center;
    private Vector2 FeetOffset => feet.transform.TransformVector(feet.offset);
    private Vector2 FeetPoint => body.position + FeetOffset;

    private bool FindBlinkPoint(float distance, bool oppositeSide, out Vector2 point)
    {
        Vector2 target = PlayerPoint;
        Vector2 side = (FeetPoint - target).normalized;
        Vector2 preferred = oppositeSide ? -side : side;
        if (preferred.sqrMagnitude < 0.01f) preferred = Vector2.left;
        for (int i = 0; i < 12; i++)
        {
            float turn = (i + 1) / 2 * 30f * (i % 2 == 0 ? 1f : -1f);
            Vector2 candidate = target + (Vector2)(Quaternion.Euler(0f, 0f, turn) * preferred) * distance;
            if ((candidate - FeetPoint).sqrMagnitude < 0.64f) continue;
            if (navigation.Clear(candidate, candidate) && navigation.HasLineOfSight(candidate, target))
            {
                point = candidate;
                return true;
            }
        }
        point = FeetPoint;
        return false;
    }

    private Vector3? FindHordeSpawn(int flank)
    {
        Vector2 target = PlayerPoint;
        Vector2 front = (FeetPoint - target).normalized;
        if (front == Vector2.zero) front = Vector2.up;
        float centerAngle = Mathf.Atan2(front.y, front.x) + flank * 1.95f;
        for (int i = 0; i < 18; i++)
        {
            float sweep = ((i % 9 + 1) / 2) * (i % 2 == 0 ? 1f : -1f) * 0.24f;
            float angle = centerAngle + sweep;
            float distance = i < 9 ? Random.Range(4.65f, 5.45f) : 3.55f;
            Vector2 candidate = target + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            if (navigation.Clear(candidate, target)) return candidate - Vector2.up * 0.16f;
        }
        return null;
    }

    private void PlaceAt(Vector2 point)
    {
        Vector2 position = point - FeetOffset;
        body.position = position;
        body.velocity = Vector2.zero;
        transform.position = position;
        Physics2D.SyncTransforms();
    }

    private void SetVulnerable(bool value)
    {
        vulnerable = value;
        feet.enabled = value;
        if (value) actor.IgnoreTargetCollision();
    }

    private void ShowHealthBar()
    {
        if (healthBar != null)
            healthBar.ShowUnboundBoss(secondPhase ? "LA LLORONA · FASE 2" : "LA LLORONA · FASE 1",
                CurrentHealth, MaxHealth);
    }

    private void ShowAura(string cueName, float radius, Color tint)
    {
        Vector2 center = transform.TransformPoint(Vector3.up * AuraCenterHeight);
        ShowCue(cueName, center, radius, tint);
        cue.transform.SetParent(transform, true);
        cue.sortingLayerID = visual.sortingLayerID;
        cue.sortingOrder = visual.sortingOrder - 1;
    }

    private void ShowCue(string cueName, Vector2 point, float radius, Color tint)
    {
        ClearCue();
        cue = Level4SpiritEffects.CreateVisual(cueName, Level4SpiritEffects.Ring, 4, transform.parent);
        cue.transform.position = point;
        cue.transform.localScale = Vector3.one * (radius * 26f / 22.5f);
        cue.color = tint;
    }

    private void ClearCue()
    {
        if (cue != null) Destroy(cue.gameObject);
        cue = null;
    }

    private void OnDisable()
    {
        if (attackAudio != null) attackAudio.Stop();
        ClearCue();
        ReleaseHeldDoll();
        if (hazards != null) hazards.End();
        if (hordes != null) hordes.StopEncounter();
    }
}
