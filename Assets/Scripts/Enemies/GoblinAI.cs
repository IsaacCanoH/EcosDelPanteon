using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
[RequireComponent(typeof(BoxCollider2D))]
public class GoblinAI : MonoBehaviour
{
    [Header("Movimiento")]
    public float moveSpeed = 1.3f;

    [Header("Maldicion del oro")]
    public float secondCoinSpeed = 2f;
    public float detectionRadius = 4f;
    public float stopDistance = 0.8f;

    [Header("Activacion")]
    public bool waitForFirstCoin;
    public bool increaseSpeedAfterSecondCoin = true;

    [Header("Rondines y deteccion")]
    public bool enablePatrol;
    [Min(0.2f)] public float patrolRadius = 2f;
    [Min(0.1f)] public float patrolSpeed = 0.65f;
    [Min(0.1f)] public float patrolPauseMin = 0.8f;
    [Min(0.1f)] public float patrolPauseMax = 1.8f;
    [Min(0.1f)] public float noticeRadius = 3f;
    [Min(0.1f)] public float trackingRadius = 6f;
    [Min(0.1f)] public float noticeSeconds = 0.7f;
    [Min(0.1f)] public float loseSightSeconds = 3f;
    [Min(0.1f)] public float investigationSeconds = 1.5f;

    public enum AwarenessState { Patrol, Suspicious, Chasing, Searching, Returning }

    [Header("Obstaculos")]
    public LayerMask obstacleMask;

    [Header("Ataque")]
    public float attackRange = 1f;
    public float attackCooldown = 1.5f;
    public int attackDamage = 1;

    [Header("Acometida opcional")]
    public bool enableLungeAttack;
    public float windupSeconds = 0.4f;
    public float lungeDistance = 0.85f;
    public float lungeSeconds = 0.18f;
    public float recoverySeconds = 0.65f;
    private bool windingUp;
    private bool lunging;
    private bool lungeHit;
    private Vector2 lungeDirection;
    private float lungeTravelled;
    public bool IsWindingUp { get { return windingUp; } }

    public void BeginWaveCombat(int wave)
    {
        enableLungeAttack = true;
        attackRange = 1.6f;
        attackCooldown = Mathf.Max(1.15f, 1.65f - (wave - 1) * 0.2f);
        nextAttackTime = Mathf.Max(nextAttackTime, Time.time + Random.Range(0.15f, 0.55f));
        Alert();
    }

    [Header("Navegacion")]
    public float repathInterval = 0.6f;
    public float separationDistance = 0.5f;
    public float separationStrength = 0.35f;
    public float stuckTime = 0.8f;

    private static readonly List<GoblinAI> agents =
        new List<GoblinAI>();

    private readonly List<Vector2> path = new List<Vector2>();

    private Rigidbody2D rb;
    private Animator animator;
    private BoxCollider2D body;
    private GoblinNavigation2D navigation;

    private Transform player;
    private Collider2D playerBody;
    private PlayerHealth playerHealth;
    private GoldCoinCollector coinCollector;

    private bool isAttacking;
    private bool nextAttackIsTwo;
    private bool chasing;
    private bool individuallyAlerted;

    private float nextAttackTime;
    private float nextRepath;
    private float lastCheck;
    private float recoveryUntil;

    private Vector2 lastPosition;
    private Vector2 checkPosition;
    private Vector2 recoveryDirection;

    private int waypoint;
    private bool navigationRequested;
    private Vector2 homePosition;
    private Vector2 movementGoal;
    private Vector2 lastSeenPosition;
    private AwarenessState awareness;
    private float suspicion;
    private float lastSeenAt;
    private float nextPatrolDecision;
    private float patrolDeadline;
    private float investigationDeadline;
    private float investigationUntil = -1f;
    private bool hasPatrolGoal;

    private float stunnedUntil;
    private bool stunActive;
    private RigidbodyConstraints2D constraintsBeforeStun;

    public bool IsStunned { get { return isActiveAndEnabled && stunActive && Time.time < stunnedUntil; } }
    public float StunRemaining { get { return IsStunned ? Mathf.Max(0f, stunnedUntil - Time.time) : 0f; } }

    // Cada impacto reinicia el plazo, sin acumularlo.
    public bool Stun(float seconds)
    {
        if (!isActiveAndEnabled || seconds <= 0f || player == null ||
            (playerHealth != null && playerHealth.currentHealth <= 0) ||
            (coinCollector != null && coinCollector.IsLevelFinished))
            return false;

        Alert();
        StopAllCoroutines();
        windingUp = lunging = false;
        isAttacking = chasing = navigationRequested = false;
        if (!stunActive) constraintsBeforeStun = rb.constraints;
        stunActive = true;
        stunnedUntil = Time.time + seconds;
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        path.Clear();
        recoveryUntil = nextRepath = 0f;
        animator.SetBool("IsMoving", false);
        animator.SetInteger("ActionState", 0);
        string[] idle = { "Goblin_Idle_Down", "Goblin_Idle_Up", "Goblin_Idle_Right", "Goblin_Idle_Left" };
        animator.Play(idle[Mathf.Clamp(animator.GetInteger("Direction"), 0, 3)], 0, 0f);
        GoblinAudio sound = GetComponent<GoblinAudio>();
        if (sound != null) sound.StopForStun();
        return true;
    }

    private void EndStun()
    {
        if (!stunActive) return;
        stunActive = false;
        stunnedUntil = 0f;
        if (rb != null)
        {
            rb.constraints = constraintsBeforeStun;
            lastPosition = checkPosition = rb.position;
        }
        lastCheck = Time.time;
        nextRepath = recoveryUntil = 0f;
        nextAttackTime = Mathf.Max(nextAttackTime, Time.time + 0.2f);
    }

    public AwarenessState CurrentAwareness { get { return awareness; } }
    public Vector2 PatrolHome { get { return homePosition; } }
    public float SuspicionProgress
    {
        get { return Mathf.Clamp01(suspicion / Mathf.Max(0.1f, noticeSeconds)); }
    }

    public bool HasPermanentAlert
    {
        get
        {
            return individuallyAlerted ||
                (coinCollector != null && (coinCollector.waveDirector != null
                    ? coinCollector.waveDirector.IsWaveActive : coinCollector.collectedCoins >= 1));
        }
    }

    public bool IsAlerted
    {
        get
        {
            if (HasPermanentAlert) return true;
            if (enablePatrol) return awareness == AwarenessState.Chasing;
            return !waitForFirstCoin && player != null && rb != null &&
                Vector2.Distance(rb.position, player.position) <= detectionRadius;
        }
    }

    public float CurrentMoveSpeed
    {
        get
        {
            if (enablePatrol && !IsAlerted) return patrolSpeed;
            return increaseSpeedAfterSecondCoin && coinCollector != null &&
                coinCollector.collectedCoins >= 2 ? secondCoinSpeed : moveSpeed;
        }
    }

    // Permite alertar a un solo duende al incorporar las piedras.
    public void Alert()
    {
        if (!isActiveAndEnabled ||
            (playerHealth != null && playerHealth.currentHealth <= 0) ||
            (coinCollector != null &&
             coinCollector.IsLevelFinished))
            return;

        individuallyAlerted = true;
        nextRepath = 0f;
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetAgents()
    {
        agents.Clear();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        body = GetComponent<BoxCollider2D>();

        lastPosition = checkPosition = rb.position;
        homePosition = movementGoal = rb.position;
        nextPatrolDecision = Time.time + Random.Range(0.2f, 0.8f);

        GameObject target =
            GameObject.FindGameObjectWithTag("Player");

        if (target == null)
            return;

        player = target.transform;
        playerBody = target.GetComponent<Collider2D>();
        playerHealth = target.GetComponent<PlayerHealth>();
        coinCollector = target.GetComponent<GoldCoinCollector>();
    }

    private void OnEnable()
    {
        if (body == null)
            body = GetComponent<BoxCollider2D>();

        // Evitar que los duendes se bloqueen fisicamente entre si.
        foreach (GoblinAI other in agents)
        {
            if (other != null && other.body != null)
                Physics2D.IgnoreCollision(body, other.body, true);
        }

        agents.Add(this);
    }

    private void Start()
    {
        navigation = FindObjectOfType<GoblinNavigation2D>();

        if (navigation != null)
            return;

        Debug.LogError(
            "Falta el objeto Navigation con GoblinNavigation2D.",
            this);

        enabled = false;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        chasing = false;
        navigationRequested = false;
        if (stunActive && !IsStunned) EndStun();
        if (IsStunned)
        {
            rb.velocity = Vector2.zero;
            animator.SetBool("IsMoving", false);
            return;
        }
        if (player == null ||
            (playerHealth != null && playerHealth.currentHealth <= 0) ||
            (coinCollector != null &&
             coinCollector.IsLevelFinished))
            return;

        if (enablePatrol)
            UpdateAwareness(Mathf.Min(Time.deltaTime, 0.1f));
        else
            movementGoal = player.position;

        if (isAttacking) return;

        if (!IsAlerted)
        {
            navigationRequested = enablePatrol && HasMovementGoal() &&
                Vector2.Distance(rb.position, movementGoal) > 0.16f;
            return;
        }

        Vector2 delta = (Vector2)player.position - rb.position;
        float distance = delta.magnitude;
        bool visible = CanSeePlayer();

        if (distance <= attackRange && visible &&
            Time.time >= nextAttackTime && playerHealth != null)
        {
            Face(delta);
            StartCoroutine(enableLungeAttack
                ? LungeRoutine(nextAttackIsTwo ? 2 : 1)
                : AttackRoutine(nextAttackIsTwo ? 2 : 1));
            nextAttackIsTwo = !nextAttackIsTwo;
            return;
        }

        chasing = distance > stopDistance || !visible;
        navigationRequested = chasing &&
            Vector2.Distance(rb.position, movementGoal) > 0.12f;
    }

    private bool HasMovementGoal()
    {
        return awareness == AwarenessState.Returning ||
            awareness == AwarenessState.Searching ||
            (awareness == AwarenessState.Patrol && hasPatrolGoal);
    }

    private void UpdateAwareness(float deltaTime)
    {
        if (HasPermanentAlert)
        {
            ChangeAwareness(AwarenessState.Chasing);
            RememberPlayer();
            return;
        }

        float radius = awareness == AwarenessState.Chasing ||
            awareness == AwarenessState.Searching
                ? Mathf.Max(noticeRadius, trackingRadius) : noticeRadius;
        bool visible = Vector2.Distance(rb.position, player.position) <= radius &&
            CanSeePlayer();

        switch (awareness)
        {
            case AwarenessState.Patrol:
                if (visible)
                {
                    ChangeAwareness(AwarenessState.Suspicious);
                    suspicion = Mathf.Max(0f, deltaTime);
                    Face((Vector2)player.position - rb.position);
                }
                else UpdatePatrol();
                break;

            case AwarenessState.Suspicious:
                if (!visible)
                {
                    suspicion = 0f;
                    ChangeAwareness(AwarenessState.Patrol);
                    break;
                }
                Face((Vector2)player.position - rb.position);
                suspicion += Mathf.Max(0f, deltaTime);
                if (suspicion >= Mathf.Max(0.1f, noticeSeconds))
                {
                    ChangeAwareness(AwarenessState.Chasing);
                    RememberPlayer();
                }
                break;

            case AwarenessState.Chasing:
                if (visible) RememberPlayer();
                else
                {
                    movementGoal = lastSeenPosition;
                    if (Time.time - lastSeenAt >= loseSightSeconds)
                        ChangeAwareness(AwarenessState.Searching);
                }
                break;

            case AwarenessState.Searching:
                if (visible)
                {
                    ChangeAwareness(AwarenessState.Chasing);
                    RememberPlayer();
                    break;
                }
                movementGoal = lastSeenPosition;
                if (Vector2.Distance(rb.position, lastSeenPosition) <= 0.25f)
                {
                    if (investigationUntil < 0f)
                        investigationUntil = Time.time + investigationSeconds;
                    if (Time.time >= investigationUntil)
                        ChangeAwareness(AwarenessState.Returning);
                }
                if (Time.time >= investigationDeadline)
                    ChangeAwareness(AwarenessState.Returning);
                break;

            case AwarenessState.Returning:
                movementGoal = homePosition;
                if (visible)
                {
                    ChangeAwareness(AwarenessState.Suspicious);
                    Face((Vector2)player.position - rb.position);
                }
                else if (Vector2.Distance(rb.position, homePosition) <= 0.2f)
                    ChangeAwareness(AwarenessState.Patrol);
                break;
        }
    }

    private void RememberPlayer()
    {
        lastSeenPosition = player.position;
        movementGoal = lastSeenPosition;
        lastSeenAt = Time.time;
    }

    private void ChangeAwareness(AwarenessState next)
    {
        if (awareness == next) return;
        awareness = next;
        suspicion = 0f;
        nextRepath = 0f;
        recoveryUntil = 0f;
        lastCheck = Time.time;
        checkPosition = rb.position;
        path.Clear();
        waypoint = 0;
        if (next == AwarenessState.Patrol)
        {
            hasPatrolGoal = false;
            nextPatrolDecision = Time.time +
                Random.Range(patrolPauseMin, Mathf.Max(patrolPauseMin, patrolPauseMax));
        }
        else if (next == AwarenessState.Searching)
        {
            investigationUntil = -1f;
            investigationDeadline = Time.time + 6f;
            movementGoal = lastSeenPosition;
        }
        else if (next == AwarenessState.Returning)
            movementGoal = homePosition;
    }

    private void UpdatePatrol()
    {
        if (hasPatrolGoal)
        {
            if (Vector2.Distance(rb.position, movementGoal) <= 0.2f ||
                Time.time >= patrolDeadline)
            {
                hasPatrolGoal = false;
                nextPatrolDecision = Time.time +
                    Random.Range(patrolPauseMin, Mathf.Max(patrolPauseMin, patrolPauseMax));
            }
            return;
        }
        if (Time.time < nextPatrolDecision || navigation == null) return;

        Vector2 offset = (Vector2)body.bounds.center - rb.position;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 candidate = homePosition + Random.insideUnitCircle * patrolRadius;
            float distance = Vector2.Distance(rb.position, candidate);
            if (distance < 0.55f ||
                !navigation.Clear(body.bounds.center, candidate + offset))
                continue;

            movementGoal = candidate;
            hasPatrolGoal = true;
            nextRepath = 0f;
            patrolDeadline = Time.time + distance / Mathf.Max(0.1f, patrolSpeed) + 3f;
            return;
        }
        nextPatrolDecision = Time.time + 1f;
    }

    private bool CanSeePlayer()
    {
        Vector2 target = playerBody != null
            ? (Vector2)playerBody.bounds.center
            : (Vector2)player.position;

        if (navigation != null && navigation.includeStaticSceneColliders)
            return navigation.HasLineOfSight(body.bounds.center, target);

        return Physics2D.Linecast(
            body.bounds.center,
            target,
            obstacleMask).collider == null;
    }

    private void FixedUpdate()
    {
        if (lunging)
        {
            if (IsStunned || (playerHealth != null && playerHealth.currentHealth <= 0))
            {
                rb.velocity = Vector2.zero;
                return;
            }
            float dashStep = Mathf.Min(lungeDistance - lungeTravelled,
                lungeDistance / Mathf.Max(0.05f, lungeSeconds) * Time.fixedDeltaTime);
            Vector2 dashCenter = body.bounds.center;
            if (dashStep > 0f && navigation != null &&
                navigation.Clear(dashCenter, dashCenter + lungeDirection * dashStep))
            {
                rb.MovePosition(rb.position + lungeDirection * dashStep);
                lungeTravelled += dashStep;
            }
            TryLungeHit();
            return;
        }
        if (IsStunned)
        {
            rb.velocity = Vector2.zero;
            lastPosition = checkPosition = rb.position;
            lastCheck = Time.time;
            animator.SetBool("IsMoving", false);
            return;
        }
        Vector2 travelled = rb.position - lastPosition;
        lastPosition = rb.position;

        bool moving =
            !isAttacking &&
            travelled.sqrMagnitude > 0.000001f;

        animator.SetBool("IsMoving", moving);

        if (moving)
            Face(travelled);

        if (!navigationRequested || isAttacking ||
            player == null || navigation == null)
        {
            rb.velocity = Vector2.zero;
            checkPosition = rb.position;
            lastCheck = Time.time;
            return;
        }

        Vector2 center = body.bounds.center;

        float speed = CurrentMoveSpeed;

        // Comprobar si esta intentando moverse sin avanzar.
        if (Time.time - lastCheck >= stuckTime)
        {
            if ((rb.position - checkPosition).sqrMagnitude < 0.0016f)
            {
                Vector2 wanted =
                    movementGoal - rb.position;

                float angle =
                    Random.value < 0.5f ? 90f : -90f;

                for (int i = 0; i < 3; i++)
                {
                    float turn = i == 0
                        ? angle
                        : (i == 1 ? -angle : 180f);

                    Vector2 candidate =
                        Quaternion.Euler(0f, 0f, turn) *
                        wanted.normalized;

                    if (!navigation.Clear(
                        center,
                        center + candidate * 0.25f))
                        continue;

                    recoveryDirection = candidate;
                    recoveryUntil = Time.time + 0.25f;
                    break;
                }

                nextRepath = 0f;
            }

            checkPosition = rb.position;
            lastCheck = Time.time;
        }

        Vector2 direction;
        float remaining;

        if (Time.time < recoveryUntil)
        {
            direction = recoveryDirection;
            remaining = 0.25f;
        }
        else
        {
            Vector2 offset = center - rb.position;

            if (Time.time >= nextRepath)
            {
                navigation.FindPath(
                    center,
                    movementGoal + offset,
                    path);

                waypoint = 0;

                nextRepath =
                    Time.time +
                    Mathf.Max(0.2f, repathInterval) +
                    Random.Range(0f, 0.15f);
            }

            while (waypoint < path.Count &&
                   Vector2.Distance(
                       center, path[waypoint]) < 0.04f)
            {
                waypoint++;
            }

            if (waypoint >= path.Count)
            {
                rb.velocity = Vector2.zero;
                return;
            }

            // Suavizar la ruta saltando puntos visibles.
            for (int i = Mathf.Min(
                     waypoint + 8, path.Count - 1);
                 i > waypoint;
                 i--)
            {
                if (navigation.Clear(center, path[i]))
                {
                    waypoint = i;
                    break;
                }
            }

            Vector2 delta = path[waypoint] - center;
            remaining = delta.magnitude;
            direction = delta.normalized;

            // Mantener cierta separacion cuando haya espacio.
            Vector2 separation = Vector2.zero;

            foreach (GoblinAI other in agents)
            {
                if (other == null || other == this)
                    continue;

                Vector2 away =
                    rb.position - other.rb.position;

                float distance = away.magnitude;

                if (distance > 0.001f &&
                    distance < separationDistance)
                {
                    separation += away / distance *
                        (1f - distance / separationDistance);
                }
            }

            Vector2 steered =
                (direction +
                 separation * separationStrength).normalized;

            if (Vector2.Dot(steered, direction) > 0.2f &&
                navigation.Clear(
                    center, center + steered * 0.15f))
            {
                direction = steered;
            }
        }

        float step = Mathf.Min(
            speed * Time.fixedDeltaTime,
            remaining);

        if (enablePatrol && awareness == AwarenessState.Patrol &&
            Vector2.Distance(rb.position + direction * step, homePosition) > patrolRadius + 0.03f)
        {
            hasPatrolGoal = false;
            nextPatrolDecision = Time.time + patrolPauseMin;
            rb.velocity = Vector2.zero;
            return;
        }

        Vector2 destination = center + direction * step;

        if (navigation.Clear(center, destination))
            rb.MovePosition(rb.position + direction * step);
        else
            rb.velocity = Vector2.zero;
    }

    private void Face(Vector2 delta)
    {
        if (delta.sqrMagnitude < 0.000001f)
            return;

        int direction =
            Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                ? (delta.x > 0f ? 2 : 3)
                : (delta.y > 0f ? 1 : 0);

        animator.SetInteger("Direction", direction);
    }

    private IEnumerator LungeRoutine(int action)
    {
        isAttacking = true;
        windingUp = true;
        rb.velocity = Vector2.zero;
        lungeDirection = ((Vector2)player.position - rb.position).normalized;
        Face(lungeDirection);
        animator.SetBool("IsMoving", false);
        animator.SetInteger("ActionState", action);
        nextAttackTime = Time.time + attackCooldown;
        GoblinAudio sound = GetComponent<GoblinAudio>();
        if (sound != null) sound.AttackVoice();
        yield return new WaitForSeconds(windupSeconds);
        windingUp = false;
        lungeTravelled = 0f;
        lungeHit = false;
        lunging = true;
        yield return new WaitForSeconds(lungeSeconds);
        lunging = false;
        TryLungeHit();
        rb.velocity = Vector2.zero;
        animator.SetInteger("ActionState", 0);
        yield return new WaitForSeconds(recoverySeconds);
        isAttacking = false;
        nextRepath = recoveryUntil = 0f;
        lastPosition = checkPosition = rb.position;
        lastCheck = Time.time;
    }

    private void TryLungeHit()
    {
        if (lungeHit || player == null || playerHealth == null ||
            playerHealth.currentHealth <= 0 || IsStunned) return;
        Vector2 delta = (Vector2)player.position - rb.position;
        if (delta.magnitude <= 0.8f &&
            (delta.sqrMagnitude < 0.01f || Vector2.Dot(delta.normalized, lungeDirection) > 0.15f) &&
            CanSeePlayer())
        {
            lungeHit = true;
            playerHealth.TakeDamage(attackDamage);
        }
    }

    private IEnumerator AttackRoutine(int action)
    {
        isAttacking = true;

        animator.SetBool("IsMoving", false);
        animator.SetInteger("ActionState", action);

        rb.velocity = Vector2.zero;

        yield return new WaitForEndOfFrame();

        float duration = Mathf.Max(
            0.2f,
            animator.GetCurrentAnimatorStateInfo(0).length);

        yield return new WaitForSeconds(duration * 0.5f);

        if (player != null &&
            playerHealth != null &&
            Vector2.Distance(
                rb.position, player.position) <= attackRange &&
            CanSeePlayer())
        {
            playerHealth.TakeDamage(attackDamage);
        }

        yield return new WaitForSeconds(duration * 0.5f);

        animator.SetInteger("ActionState", 0);

        nextAttackTime = Time.time + attackCooldown;
        nextRepath = 0f;
        isAttacking = false;
    }

    private void OnDisable()
    {
        agents.Remove(this);
        windingUp = lunging = false;
        EndStun();
        StopAllCoroutines();

        isAttacking = false;

        if (rb != null)
            rb.velocity = Vector2.zero;

        if (animator != null)
            animator.SetBool("IsMoving", false);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        for (int i = waypoint; i < path.Count - 1; i++)
            Gizmos.DrawLine(path[i], path[i + 1]);
    }
}