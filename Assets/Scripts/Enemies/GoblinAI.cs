using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class GoblinAI : MonoBehaviour
{
    [Header("Movimiento")]
    public float moveSpeed = 1.3f;

    [Header("Maldición del oro")]
    public float secondCoinSpeed = 1.3f;
    public float detectionRadius = 4f;
    public float stopDistance = 0.8f;

    [Header("Detección de obstáculos")]
    public LayerMask obstacleMask;

    [Header("Ataque")]
    public float attackRange = 1f;
    public float attackCooldown = 1.5f;
    public int attackDamage = 1;

    private bool isAttacking = false;
    private float nextAttackTime = 0f;
    private bool nextAttackIsTwo = false;

    private Transform player;
    private PlayerHealth playerHealth;
    private GoldCoinCollector coinCollector;
    private Rigidbody2D rb;
    private Animator animator;

    private Vector2 movement;
    private int direction = 0;
    private int avoidanceSide = 1;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
            playerHealth = playerObject.GetComponent<PlayerHealth>();

            // Consultar cuántas monedas ha recogido el Player.
            coinCollector = playerObject.GetComponent<GoldCoinCollector>();
        }
    }

    private void Update()
    {
        // No perseguir al jugador mientras ataca.
        if (isAttacking)
        {
            movement = Vector2.zero;
            return;
        }

        movement = Vector2.zero;

        if (player != null)
        {
            Vector2 toPlayer =
                (Vector2)player.position - rb.position;

            float distance = toPlayer.magnitude;

            if (distance <= detectionRadius ||
                (coinCollector != null && coinCollector.collectedCoins >= 1))
            {
                // Mirar hacia el jugador.
                if (Mathf.Abs(toPlayer.x) > Mathf.Abs(toPlayer.y))
                {
                    direction = toPlayer.x > 0 ? 2 : 3;
                }
                else if (toPlayer.sqrMagnitude > 0.0001f)
                {
                    direction = toPlayer.y > 0 ? 1 : 0;
                }

                // Atacar cuando el jugador esté cerca.
                if (distance <= attackRange &&
                    Time.time >= nextAttackTime &&
                    playerHealth != null)
                {
                    animator.SetInteger("Direction", direction);

                    // Alternar entre los dos ataques.
                    if (nextAttackIsTwo)
                    {
                        StartCoroutine(AttackTwoRoutine());
                    }
                    else
                    {
                        StartCoroutine(AttackOneRoutine());
                    }

                    // Preparar el siguiente ataque.
                    nextAttackIsTwo = !nextAttackIsTwo;
                    return;
                }

                // Perseguir al jugador.
                if (distance > stopDistance)
                {
                    movement = toPlayer.normalized;
                }
            }
        }

        animator.SetInteger("Direction", direction);
        animator.SetBool("IsMoving", movement.sqrMagnitude > 0);
    }

    private void FixedUpdate()
    {
        Vector2 finalMovement = movement;

        if (movement.sqrMagnitude > 0.001f)
        {
            // Detectar obstáculos delante de los pies del duende.
            Vector2 origin = rb.position + new Vector2(0f, 0.14f);

            RaycastHit2D obstacle = Physics2D.CircleCast(
                origin,
                0.26f,
                movement,
                0.65f,
                obstacleMask
            );

            if (obstacle.collider != null)
            {
                // Calcular una dirección lateral.
                Vector2 left = new Vector2(-movement.y, movement.x);
                Vector2 right = -left;

                Vector2 sideDirection =
                    avoidanceSide == 1 ? left : right;

                // Comprobar si puede pasar por ese lado.
                RaycastHit2D sideObstacle = Physics2D.CircleCast(
                    origin,
                    0.26f,
                    sideDirection,
                    0.45f,
                    obstacleMask
                );

                if (sideObstacle.collider != null)
                {
                    // Si ese lado está bloqueado, intentar el otro.
                    avoidanceSide *= -1;
                    sideDirection = -sideDirection;

                    sideObstacle = Physics2D.CircleCast(
                        origin,
                        0.26f,
                        sideDirection,
                        0.45f,
                        obstacleMask
                    );
                }

                finalMovement = sideObstacle.collider == null
                    ? sideDirection
                    : Vector2.zero;
            }
        }

        // Aumentar la velocidad al recoger la segunda moneda.
        float currentSpeed = moveSpeed;

        if (coinCollector != null && coinCollector.collectedCoins >= 2)
        {
            currentSpeed = secondCoinSpeed;
        }

        rb.MovePosition(
            rb.position + finalMovement * currentSpeed * Time.fixedDeltaTime
        );
    }

    private IEnumerator AttackOneRoutine()
    {
        isAttacking = true;
        movement = Vector2.zero;

        animator.SetBool("IsMoving", false);
        animator.SetInteger("ActionState", 1);

        // Esperar a que el Animator active el ataque.
        yield return new WaitForEndOfFrame();

        float duration = animator.GetCurrentAnimatorStateInfo(0).length;
        duration = Mathf.Max(duration, 0.2f);

        // Esperar hasta la mitad de la animación.
        yield return new WaitForSeconds(duration * 0.5f);

        // Aplicar daño solo si el jugador sigue cerca.
        if (player != null && playerHealth != null)
        {
            float distance = Vector2.Distance(rb.position, player.position);

            if (distance <= attackRange)
            {
                playerHealth.TakeDamage(attackDamage);
            }
        }

        // Esperar a que termine la animación.
        yield return new WaitForSeconds(duration * 0.5f);

        animator.SetInteger("ActionState", 0);

        nextAttackTime = Time.time + attackCooldown;
        isAttacking = false;

    }

    private IEnumerator AttackTwoRoutine()
    {
        isAttacking = true;
        movement = Vector2.zero;

        animator.SetBool("IsMoving", false);
        animator.SetInteger("ActionState", 2);

        // Esperar a que comience la animación del segundo ataque.
        yield return new WaitForEndOfFrame();

        float duration = animator.GetCurrentAnimatorStateInfo(0).length;
        duration = Mathf.Max(duration, 0.2f);

        // Esperar hasta la mitad del ataque.
        yield return new WaitForSeconds(duration * 0.5f);

        // Aplicar daño si el Player sigue dentro del alcance.
        if (player != null && playerHealth != null)
        {
            float distance = Vector2.Distance(rb.position, player.position);

            if (distance <= attackRange)
            {
                playerHealth.TakeDamage(attackDamage);
            }
        }

        // Esperar hasta terminar la animación.
        yield return new WaitForSeconds(duration * 0.5f);

        animator.SetInteger("ActionState", 0);

        nextAttackTime = Time.time + attackCooldown;
        isAttacking = false;
    }
}