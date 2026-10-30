using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(Animator))]
public sealed class GhostChildAI : MonoBehaviour
{
    public enum Phase { Watching, Melee, MediumRange, LongRange }
    public const float StoneInterval = 4f;
    public Transform player;
    public ToyCollector toys;
    public GoblinNavigation2D navigation;
    public GhostStoneProjectile stonePrefab;
    public LayerMask obstacleMask = 1 << 6;
    public float watchingDistance = 5f;
    public float mediumDistance = 4.5f;
    public float longDistance = 8f;
    public float meleeRange = 0.9f;
    public float meleeCooldown = 1.5f;
    public float moveSpeed = 2.15f;
    public int damage = 1;
    public Phase CurrentPhase => (Phase)Mathf.Clamp(toys == null ? 0 : toys.Count, 0, 3);
    public float NextStoneTime => nextStone;

    private Rigidbody2D body;
    private BoxCollider2D feet;
    private Animator animator;
    private PlayerHealth health;
    private readonly List<Vector2> path = new List<Vector2>();
    private readonly RaycastHit2D[] sightHits = new RaycastHit2D[1];
    private ContactFilter2D sightFilter;
    private int waypoint;
    private float nextPath, nextMelee, nextStone;
    private bool attacking;
    private Phase previousPhase;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        feet = GetComponent<BoxCollider2D>();
        animator = GetComponent<Animator>();
        sightFilter = new ContactFilter2D();
        sightFilter.SetLayerMask(obstacleMask);
        sightFilter.useTriggers = false;
    }

    private void Start()
    {
        if (player == null)
        {
            var target = GameObject.FindGameObjectWithTag("Player");
            if (target != null) player = target.transform;
        }
        if (player == null) { enabled = false; return; }
        if (toys == null) toys = player.GetComponent<ToyCollector>();
        health = player.GetComponent<PlayerHealth>();
        if (navigation == null) navigation = FindObjectOfType<GoblinNavigation2D>();
        previousPhase = CurrentPhase;
        nextStone = Time.time + StoneInterval;
        // El espectro no empuja ni inmoviliza al jugador durante el acecho.
        foreach (var c in player.GetComponentsInChildren<Collider2D>()) Physics2D.IgnoreCollision(feet, c);
    }

    private bool Visible()
    {
        return Physics2D.Linecast(feet.bounds.center, player.position + Vector3.up * 0.15f, sightFilter, sightHits) == 0;
    }

    private void Update()
    {
        if (player == null || (health != null && health.currentHealth <= 0))
        {
            StopAllCoroutines();
            attacking = false;
            animator.SetInteger("ActionState", 0);
            animator.SetBool("IsMoving", false);
            body.velocity = Vector2.zero;
            return;
        }
        Phase phase = CurrentPhase;
        if (phase != previousPhase)
        {
            // No reiniciar el reloj al obtener el tercer juguete: nunca dos piedras en menos de 4 s.
            if (previousPhase < Phase.MediumRange && phase >= Phase.MediumRange) nextStone = Time.time + StoneInterval;
            previousPhase = phase;
            nextPath = 0f;
        }
        if (attacking) return;
        Vector2 delta = (Vector2)player.position - body.position;
        Face(delta);
        float distance = delta.magnitude;
        if (phase == Phase.Melee && distance <= meleeRange && Time.time >= nextMelee && Visible())
            StartCoroutine(Attack(false));
        else if (phase >= Phase.MediumRange && distance >= 2f && distance <= (phase == Phase.LongRange ? 12f : 7f) && Time.time >= nextStone && Visible() && stonePrefab != null)
            StartCoroutine(Attack(true));
    }

    private void FixedUpdate()
    {
        if (player == null || navigation == null || attacking || (health != null && health.currentHealth <= 0))
        { body.velocity = Vector2.zero; animator.SetBool("IsMoving", false); return; }
        Phase phase = CurrentPhase;
        Vector2 delta = (Vector2)player.position - body.position;
        float distance = delta.magnitude;
        float desired = phase == Phase.Watching ? watchingDistance : phase == Phase.Melee ? meleeRange * 0.8f : phase == Phase.MediumRange ? mediumDistance : longDistance;
        bool visible = Visible();
        bool move = Mathf.Abs(distance - desired) > 0.6f || !visible;
        if (!move) { body.velocity = Vector2.zero; animator.SetBool("IsMoving", false); return; }

        Vector2 center = feet.bounds.center;
        if (Time.time >= nextPath)
        {
            Vector2 away = distance > 0.01f ? -delta / distance : Vector2.right;
            // Si una tumba tapa la vista, acercarse siguiendo la ruta antes de atacar.
            Vector2 target = visible && phase != Phase.Melee ? (Vector2)player.position + away * desired : (Vector2)player.position;
            navigation.FindPath(center, target + (center - body.position), path);
            waypoint = 0;
            nextPath = Time.time + 0.45f;
        }
        while (waypoint < path.Count && Vector2.Distance(center, path[waypoint]) < 0.08f) waypoint++;
        if (waypoint >= path.Count) { body.velocity = Vector2.zero; animator.SetBool("IsMoving", false); return; }
        for (int i = Mathf.Min(waypoint + 6, path.Count - 1); i > waypoint; i--)
            if (navigation.Clear(center, path[i])) { waypoint = i; break; }
        Vector2 step = Vector2.ClampMagnitude(path[waypoint] - center, (phase == Phase.Watching ? moveSpeed * 0.72f : moveSpeed) * Time.fixedDeltaTime);
        bool clear = navigation.Clear(center, center + step);
        animator.SetBool("IsMoving", clear && step.sqrMagnitude > 0.000001f);
        if (clear) { Face(step); body.MovePosition(body.position + step); }
        else { body.velocity = Vector2.zero; nextPath = 0f; }
    }

    private IEnumerator Attack(bool throwStone)
    {
        attacking = true;
        body.velocity = Vector2.zero;
        animator.SetBool("IsMoving", false);
        animator.SetInteger("ActionState", throwStone ? 2 : 1);
        if (throwStone) nextStone = Time.time + StoneInterval;
        else nextMelee = Time.time + meleeCooldown;
        yield return new WaitForSeconds(0.3f);
        if (player != null && health != null && health.currentHealth > 0 && Visible())
        {
            Vector2 target = (Vector2)player.position + Vector2.up * 0.15f;
            if (throwStone)
            {
                var stone = Instantiate(stonePrefab, transform.position + Vector3.up * 0.45f, Quaternion.identity, transform.parent);
                stone.Launch(target, GetComponentsInChildren<Collider2D>(), damage);
            }
            else if (Vector2.Distance(body.position, player.position) <= meleeRange && CurrentPhase == Phase.Melee)
                health.TakeDamage(damage);
        }
        yield return new WaitForSeconds(0.3f);
        animator.SetInteger("ActionState", 0);
        attacking = false;
        nextPath = 0f;
    }

    private void Face(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.000001f) return;
        animator.SetInteger("Direction", Mathf.Abs(direction.x) > Mathf.Abs(direction.y) ? (direction.x > 0 ? 2 : 3) : (direction.y > 0 ? 1 : 0));
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        attacking = false;
        if (body != null) body.velocity = Vector2.zero;
        if (animator != null) { animator.SetBool("IsMoving", false); animator.SetInteger("ActionState", 0); }
    }
}
