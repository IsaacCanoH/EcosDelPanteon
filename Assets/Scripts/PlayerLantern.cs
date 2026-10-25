using System.Collections.Generic;
using Cainos.PixelArtTopDown_Basic;
using UnityEngine;

[DefaultExecutionOrder(60)]
[RequireComponent(typeof(PlayerHealth), typeof(GoldCoinCollector))]
public class PlayerLantern : MonoBehaviour
{
    public IrisInNivel opening;
    public SpriteRenderer lanternVisual;
    public SpriteRenderer flashVisual;
    public SpriteRenderer animaVisual;
    public Sprite[] animaFrames;
    public GoblinNavigation2D navigation;
    [Min(0.1f)] public float range = 1.35f;
    [Range(20f, 180f)] public float angle = 110f;
    [Min(0.1f)] public float cooldown = 1f;
    private UnityEngine.Rendering.SortingGroup visualGroup;
    private SpriteRenderer playerVisual;
    private float nextAttackAt;
    private float flashUntil;
    private float animaUntil;
    private PlayerHealth health;
    private GoldCoinCollector collector;
    private PlayerCoinSearch search;
    private TopDownCharacterController movement;
    private PlayerStoneThrow facing;
    private Collider2D body;
    private Vector2 flashDirection;
    private Vector2 flashOrigin;
    private readonly HashSet<GoblinCombat> hitTargets = new HashSet<GoblinCombat>();
    public float CooldownRemaining { get { return Mathf.Max(0f, nextAttackAt - Time.time); } }

    private void Awake()
    {
        visualGroup = GetComponent<UnityEngine.Rendering.SortingGroup>();
        playerVisual = GetComponent<SpriteRenderer>();
        health = GetComponent<PlayerHealth>();
        collector = GetComponent<GoldCoinCollector>();
        search = GetComponent<PlayerCoinSearch>();
        movement = GetComponent<TopDownCharacterController>();
        facing = GetComponent<PlayerStoneThrow>();
        body = GetComponent<Collider2D>();
        if (flashVisual != null) flashVisual.enabled = false;
        if (animaVisual != null) animaVisual.enabled = false;
    }

    private void Update()
    {
        if (GameControls.LanternPressed) TryAttack();
    }

    public bool TryAttack()
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f || Time.time < nextAttackAt ||
            health == null || health.currentHealth <= 0 || collector.IsLevelFinished ||
            (opening != null && opening.isActiveAndEnabled) ||
            (search != null && search.IsSearching) ||
            (movement != null && !movement.enabled)) return false;
        Vector2 origin = body != null ? (Vector2)body.bounds.center : (Vector2)transform.position;
        Vector2 direction = facing != null ? facing.FacingDirection : Vector2.down;
        nextAttackAt = Time.time + cooldown;
        flashUntil = Time.time + 0.2f;
        animaUntil = Time.time + 0.45f;
        flashOrigin = origin;
        flashDirection = direction;
        hitTargets.Clear();
        foreach (var collider in Physics2D.OverlapCircleAll(origin, range))
        {
            if (collider.isTrigger) continue;
            GoblinCombat target = collider.GetComponentInParent<GoblinCombat>();
            if (target == null || target.IsBanished || !hitTargets.Add(target)) continue;
            Vector2 delta = (Vector2)collider.bounds.center - origin;
            if (delta.sqrMagnitude > 0.01f &&
                Vector2.Dot(direction, delta.normalized) < Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad))
                continue;
            if (navigation != null && !navigation.HasLineOfSight(origin, collider.bounds.center))
                continue;
            target.TakeLanternHit();
        }
        return true;
    }

    private void LateUpdate()
    {
        if (visualGroup != null && playerVisual != null)
            visualGroup.sortingLayerID = playerVisual.sortingLayerID;
        Vector2 direction = facing != null ? facing.FacingDirection : Vector2.down;
        if (lanternVisual != null)
        {
            lanternVisual.transform.localPosition = new Vector3(direction.x == 0f ? 0.38f : direction.x * 0.32f,
                0.42f + direction.y * 0.08f, 0f);
            lanternVisual.sortingOrder = direction.y > 0f ? 1 : 4;
            lanternVisual.enabled = health != null && health.currentHealth > 0;
        }
        UpdateAnima();
        if (flashVisual == null) return;
        flashVisual.enabled = Time.time < flashUntil && health != null && health.currentHealth > 0;
        if (!flashVisual.enabled) return;
        flashVisual.transform.position = new Vector3(flashOrigin.x, flashOrigin.y + 0.3f, transform.position.z);
        flashVisual.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(flashDirection.y, flashDirection.x) * Mathf.Rad2Deg);
        flashVisual.color = new Color(1f, 1f, 1f, Mathf.Clamp01((flashUntil - Time.time) / 0.2f));
    }

    private void UpdateAnima()
    {
        if (animaVisual == null) return;
        float remaining = animaUntil - Time.time;
        animaVisual.enabled = remaining > 0f && health != null && health.currentHealth > 0;
        if (!animaVisual.enabled) return;
        float t = Mathf.Clamp01(1f - remaining / 0.45f);
        Vector2 position = flashOrigin + flashDirection * Mathf.Lerp(0.15f, 0.9f, t);
        position.y += 0.32f + Mathf.Sin(t * Mathf.PI) * 0.08f;
        animaVisual.transform.position = new Vector3(position.x, position.y, transform.position.z);
        animaVisual.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 2f) * 3f);
        animaVisual.transform.localScale = Vector3.one * (0.85f + Mathf.Sin(t * Mathf.PI) * 0.15f);
        animaVisual.color = new Color(1f, 1f, 1f, Mathf.Clamp01(remaining / 0.15f));
        if (animaFrames != null && animaFrames.Length > 0)
            animaVisual.sprite = animaFrames[Mathf.FloorToInt(t * 6f) % animaFrames.Length];
    }

    private void OnDisable()
    {
        if (flashVisual != null) flashVisual.enabled = false;
        if (animaVisual != null) animaVisual.enabled = false;
    }
}
