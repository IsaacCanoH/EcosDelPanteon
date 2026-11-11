using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(Animator))]
public sealed class LloronaEnemyAI : MonoBehaviour, IDamageable
{
    public enum EnemyState { Idle, Moving, Attacking, Screaming, Hurt, Dying, Appearing, Disappearing }

    [Header("Objetivo y navegacion")]
    public Transform player;
    public GoblinNavigation2D navigation;
    [Min(0.1f)] public float moveSpeed = 1.9f;
    [Min(0.1f)] public float detectionRange = 7f;
    [Min(0.1f)] public float loseRange = 11f;
    [Min(0.1f)] public float returnDelay = 3f;

    [Header("Ataques")]
    [Min(0.1f)] public float attackRange = 1.15f;
    [Min(0.1f)] public float attackCooldown = 2f;
    [Min(0.1f)] public float screamRange = 2.5f;
    [Min(0.1f)] public float screamCooldown = 7f;
    [Min(1)] public int damageHearts = 1;

    [Header("Vida")]
    [Min(1)] public int maxHealth = 150;
    public int CurrentHealth { get; private set; }
    public bool IsDead => finalEncounter != null ? finalEncounter.IsDefeated : CurrentHealth <= 0;
    public EnemyState CurrentState { get; private set; }
    public event Action<int, int> OnHealthChanged;
    public event Action<LloronaEnemyAI> OnDied;

    private static readonly string[] StateNames =
        { "Idle", "Move", "Attack", "Scream", "Hit", "Death", "Appear", "Disappear" };
    private static readonly int ActionParameter = Animator.StringToHash("ActionState");
    private static readonly int DirectionParameter = Animator.StringToHash("Direction");
    private readonly List<Vector2> path = new List<Vector2>();
    private Rigidbody2D body;
    private BoxCollider2D feet;
    private Animator animator;
    private PlayerHealth targetHealth;
    private Level4LloronaBoss finalEncounter;
    private bool cinematicPose;
    private Collider2D targetCollider;
    private Vector2 home;
    private Vector2 facing = Vector2.down;
    private Vector2 desiredTarget;
    private int waypoint;
    private float nextPath, nextAttack, nextScream, lastSeen;
    private bool alerted, impactUsed;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        feet = GetComponent<BoxCollider2D>();
        animator = GetComponent<Animator>();
        finalEncounter = GetComponent<Level4LloronaBoss>();
        home = body.position;
        desiredTarget = feet.bounds.center;
        CurrentHealth = maxHealth;
    }

    private void OnEnable()
    {
        if (body == null || finalEncounter != null) return;
        body.velocity = Vector2.zero;
        feet.enabled = false;
        SetState(EnemyState.Appearing, true);
    }

    private void Start()
    {
        if (finalEncounter != null) return;
        if (navigation == null) navigation = FindObjectOfType<GoblinNavigation2D>();
        if (player == null)
        {
            GameObject target = GameObject.FindGameObjectWithTag("Player");
            if (target != null) player = target.transform;
        }
        SetTarget(player);
        nextScream = Time.time + 3f;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void SetTarget(Transform target)
    {
        if (player != null && feet != null && feet.enabled)
            foreach (Collider2D c in player.GetComponentsInChildren<Collider2D>())
                if (c != null && c.enabled) Physics2D.IgnoreCollision(feet, c, false);
        player = target;
        targetHealth = player == null ? null : player.GetComponent<PlayerHealth>();
        targetCollider = player == null ? null : player.GetComponent<Collider2D>();
        IgnoreTargetCollision();
        alerted = false;
        nextPath = 0f;
    }

    internal void IgnoreTargetCollision()
    {
        if (player == null || feet == null || !feet.enabled) return;
        foreach (Collider2D c in player.GetComponentsInChildren<Collider2D>())
            if (c != null && c.enabled) Physics2D.IgnoreCollision(feet, c);
    }

    private Vector2 TargetPoint
    {
        get { return targetCollider != null ? (Vector2)targetCollider.bounds.center : (Vector2)player.position; }
    }

    private bool CanSeeTarget()
    {
        return player != null && navigation != null &&
               navigation.HasLineOfSight(feet.bounds.center, TargetPoint);
    }

    private bool Busy
    {
        get { return CurrentState != EnemyState.Idle && CurrentState != EnemyState.Moving; }
    }

    private void Update()
    {
        if (finalEncounter != null || Time.timeScale <= 0f || IsDead || Busy) return;
        if (player == null || (targetHealth != null && targetHealth.IsDead))
        {
            alerted = false;
            desiredTarget = home + (Vector2)(feet.bounds.center - transform.position);
            return;
        }

        Vector2 delta = TargetPoint - (Vector2)feet.bounds.center;
        float distance = delta.magnitude;
        bool visible = CanSeeTarget();
        if (visible && distance <= detectionRange)
        {
            alerted = true;
            lastSeen = Time.time;
        }
        if (alerted && distance > loseRange && Time.time - lastSeen >= returnDelay)
            alerted = false;

        desiredTarget = alerted ? TargetPoint : home + (Vector2)(feet.bounds.center - transform.position);
        if (!alerted) return;
        Face(delta);
        if (visible && distance <= attackRange && Time.time >= nextAttack)
        {
            nextAttack = Time.time + attackCooldown;
            SetState(EnemyState.Attacking, true);
        }
        else if (visible && distance > attackRange + 0.15f &&
                 distance <= screamRange && Time.time >= nextScream)
        {
            nextScream = Time.time + screamCooldown;
            nextAttack = Mathf.Max(nextAttack, Time.time + 0.7f);
            SetState(EnemyState.Screaming, true);
        }
    }

    private void FixedUpdate()
    {
        if (finalEncounter != null) return;
        if (Busy || IsDead || navigation == null || Time.timeScale <= 0f ||
            (targetHealth != null && targetHealth.IsDead))
        {
            body.velocity = Vector2.zero;
            return;
        }

        Vector2 center = feet.bounds.center;
        float distance = Vector2.Distance(center, desiredTarget);
        if ((!alerted && distance <= 0.16f) ||
            (alerted && distance <= attackRange * 0.8f && CanSeeTarget()))
        {
            StopMoving();
            return;
        }

        Vector2 direction;
        if (navigation.Clear(center, desiredTarget))
        {
            direction = desiredTarget - center;
        }
        else
        {
            if (Time.time >= nextPath)
            {
                navigation.FindPath(center, desiredTarget, path);
                waypoint = 0;
                nextPath = Time.time + 0.55f;
            }
            while (waypoint < path.Count && Vector2.Distance(center, path[waypoint]) < 0.13f)
                waypoint++;
            if (waypoint >= path.Count) { StopMoving(); return; }
            direction = path[waypoint] - center;
        }
        if (direction.sqrMagnitude < 0.0025f) { StopMoving(); return; }
        Face(direction);
        SetState(EnemyState.Moving);
        body.velocity = direction.normalized * moveSpeed;
    }

    private void StopMoving()
    {
        body.velocity = Vector2.zero;
        SetState(EnemyState.Idle);
    }

    private void Face(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.001f) return;
        int index;
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            index = direction.x > 0f ? 1 : 2;
            facing = direction.x > 0f ? Vector2.right : Vector2.left;
        }
        else
        {
            index = direction.y > 0f ? 3 : 0;
            facing = direction.y > 0f ? Vector2.up : Vector2.down;
        }
        animator.SetFloat(DirectionParameter, index);
    }

    private void SetState(EnemyState state, bool restart = false)
    {
        if (CurrentState == state && !restart) return;
        CurrentState = state;
        animator.SetInteger(ActionParameter, (int)state);
        if (state != EnemyState.Moving) body.velocity = Vector2.zero;
        if (state == EnemyState.Attacking || state == EnemyState.Screaming) impactUsed = false;
        if (restart) animator.Play("Llorona_" + StateNames[(int)state], 0, 0f);
    }

    internal Vector2 PlayFinalPose(EnemyState state, Vector2 direction, float speed = 1f)
    {
        cinematicPose = true;
        Face(direction);
        animator.speed = speed;
        SetState(state, true);
        return facing;
    }

    public void OnAttackImpact()
    {
        if (finalEncounter != null || CurrentState != EnemyState.Attacking || impactUsed) return;
        impactUsed = true;
        DamageTarget(attackRange + 0.15f, 0f);
    }

    public void OnScreamImpact()
    {
        if (finalEncounter != null || CurrentState != EnemyState.Screaming || impactUsed) return;
        impactUsed = true;
        DamageTarget(screamRange, 0.15f);
    }

    private void DamageTarget(float range, float minimumDot)
    {
        if (player == null || targetHealth == null || targetHealth.IsDead || !CanSeeTarget()) return;
        Vector2 delta = TargetPoint - (Vector2)feet.bounds.center;
        if (delta.magnitude > range || (delta.sqrMagnitude > 0.001f &&
            Vector2.Dot(facing, delta.normalized) < minimumDot)) return;
        targetHealth.TakeDamage(Mathf.Max(1, targetHealth.HealthPerHeart) * damageHearts);
    }

    public void TakeDamage(int amount, Vector2 hitDirection)
    {
        if (finalEncounter != null)
        {
            finalEncounter.TakeDamage(amount, hitDirection);
            return;
        }

        if (IsDead || amount <= 0 || !isActiveAndEnabled ||
            CurrentState == EnemyState.Appearing || CurrentState == EnemyState.Disappearing) return;
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        alerted = player != null;
        lastSeen = Time.time;
        nextPath = 0f;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        if (CurrentHealth == 0)
        {
            feet.enabled = false;
            SetState(EnemyState.Dying, true);
        }
        else
        {
            Face(-hitDirection);
            SetState(EnemyState.Hurt, true);
        }
    }

    public void Despawn()
    {
        if (IsDead || CurrentState == EnemyState.Disappearing) return;
        feet.enabled = false;
        SetState(EnemyState.Disappearing, true);
    }

    public void OnActionFinished(int action)
    {
        if (finalEncounter != null || cinematicPose || (int)CurrentState != action) return;
        if (CurrentState == EnemyState.Dying)
        {
            OnDied?.Invoke(this);
            Destroy(gameObject);
        }
        else if (CurrentState == EnemyState.Disappearing)
        {
            Destroy(gameObject);
        }
        else
        {
            feet.enabled = true;
            IgnoreTargetCollision();
            SetState(EnemyState.Idle);
        }
    }

    private void OnDisable()
    {
        if (body != null) body.velocity = Vector2.zero;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.35f, 0.9f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
