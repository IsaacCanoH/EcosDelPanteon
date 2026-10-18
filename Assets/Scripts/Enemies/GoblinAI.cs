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

    [Header("Obstaculos")]
    public LayerMask obstacleMask;

    [Header("Ataque")]
    public float attackRange = 1f;
    public float attackCooldown = 1.5f;
    public int attackDamage = 1;

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

    private float nextAttackTime;
    private float nextRepath;
    private float lastCheck;
    private float recoveryUntil;

    private Vector2 lastPosition;
    private Vector2 checkPosition;
    private Vector2 recoveryDirection;

    private int waypoint;

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
        chasing = false;

        if (player == null || isAttacking ||
            (playerHealth != null && playerHealth.currentHealth <= 0))
            return;

        Vector2 delta =
            (Vector2)player.position - rb.position;

        float distance = delta.magnitude;

        bool detected =
            distance <= detectionRadius ||
            (coinCollector != null &&
             coinCollector.collectedCoins >= 1);

        if (!detected)
            return;

        bool visible = CanSeePlayer();

        if (distance <= attackRange &&
            visible &&
            Time.time >= nextAttackTime &&
            playerHealth != null)
        {
            Face(delta);

            StartCoroutine(
                AttackRoutine(nextAttackIsTwo ? 2 : 1));

            nextAttackIsTwo = !nextAttackIsTwo;
            return;
        }

        chasing = distance > stopDistance || !visible;
    }

    private bool CanSeePlayer()
    {
        Vector2 target = playerBody != null
            ? (Vector2)playerBody.bounds.center
            : (Vector2)player.position;

        return Physics2D.Linecast(
            body.bounds.center,
            target,
            obstacleMask).collider == null;
    }

    private void FixedUpdate()
    {
        Vector2 travelled = rb.position - lastPosition;
        lastPosition = rb.position;

        bool moving =
            !isAttacking &&
            travelled.sqrMagnitude > 0.000001f;

        animator.SetBool("IsMoving", moving);

        if (moving)
            Face(travelled);

        if (!chasing || isAttacking ||
            player == null || navigation == null)
        {
            rb.velocity = Vector2.zero;
            checkPosition = rb.position;
            lastCheck = Time.time;
            return;
        }

        Vector2 center = body.bounds.center;

        float speed =
            coinCollector != null &&
            coinCollector.collectedCoins >= 2
                ? secondCoinSpeed
                : moveSpeed;

        // Comprobar si esta intentando moverse sin avanzar.
        if (Time.time - lastCheck >= stuckTime)
        {
            if ((rb.position - checkPosition).sqrMagnitude < 0.0016f)
            {
                Vector2 wanted =
                    (Vector2)player.position - rb.position;

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
                    (Vector2)player.position + offset,
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