using System.Collections.Generic;
using UnityEngine;

// Barrido fisico: evita atravesar colliders aun en un frame largo.
public class StoneProjectile : MonoBehaviour
{
    [Min(0.1f)] public float speed = 8f;
    [Min(0.1f)] public float range = 6f;
    [Min(0.01f)] public float radius = 0.1f;
    [Min(0.01f)] public float enemyHitRadius = 0.25f;
    [Min(0.1f)] public float stunSeconds = 2.5f;
    public Transform visual;

    private PlayerStoneThrow owner;
    private Vector2 direction;
    private float travelled;
    private bool launched;
    private bool finished;
    private ContactFilter2D filter;
    private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(16);

    private readonly List<RaycastHit2D> visibilityHits = new List<RaycastHit2D>(16);

    public void Launch(PlayerStoneThrow source, Vector2 heading)
    {
        owner = source;
        direction = heading.sqrMagnitude > 0f ? heading.normalized : Vector2.down;
        filter = new ContactFilter2D();
        filter.SetLayerMask(~0);
        filter.useTriggers = false;
        launched = true;
    }

    private void FixedUpdate()
    {
        if (!launched || finished) return;
        if (owner == null || !owner.CanProjectilesContinue)
        {
            Finish();
            return;
        }
        Step(Mathf.Min(speed * Time.fixedDeltaTime, Mathf.Max(0f, range - travelled)));
    }

    private void Step(float distance)
    {
        if (finished) return;
        Vector2 origin = transform.position;
        int count = Physics2D.CircleCast(origin, radius, direction, filter, hits, distance);
        Collider2D nearest = null;
        GoblinAI target = null;
        float nearestDistance = float.PositiveInfinity;
        bool nearestStatic = false;

        for (int i = 0; i < count; i++)
        {
            Collider2D candidate = hits[i].collider;
            if (candidate == null || candidate.isTrigger ||
                (owner != null && candidate.transform.IsChildOf(owner.transform)))
                continue;

            GoblinAI goblin = candidate.GetComponentInParent<GoblinAI>();
            EnemyHealth eh = candidate.GetComponentInParent<EnemyHealth>();
            Rigidbody2D attached = candidate.attachedRigidbody;
            bool solid = attached == null || attached.bodyType == RigidbodyType2D.Static;

            bool isEnemy = (goblin != null && goblin.isActiveAndEnabled) ||
                           (eh != null && !eh.IsDead && eh.isActiveAndEnabled);

            if (!solid && !isEnemy) continue;

            float hitDistance = hits[i].distance;
            if (hitDistance < nearestDistance - 0.0001f ||
                (Mathf.Abs(hitDistance - nearestDistance) <= 0.0001f && solid && !nearestStatic))
            {
                nearest = candidate;
                target = goblin;
                nearestDistance = hitDistance;
                nearestStatic = solid;
            }
        }

        // El margen extra admite enemigos; no ensancha los choques con paredes.
        count = Physics2D.CircleCast(origin, Mathf.Max(radius, enemyHitRadius),
            direction, filter, hits, distance);
        for (int i = 0; i < count; i++)
        {
            Collider2D candidate = hits[i].collider;
            if (candidate == null || candidate.isTrigger) continue;

            GoblinAI goblin = candidate.GetComponentInParent<GoblinAI>();
            EnemyHealth eh = candidate.GetComponentInParent<EnemyHealth>();

            bool isEnemy = (goblin != null && goblin.isActiveAndEnabled) ||
                           (eh != null && !eh.IsDead && eh.isActiveAndEnabled);

            if (!isEnemy || hits[i].distance >= nearestDistance - 0.0001f ||
                !HasClearApproach(origin, candidate, goblin))
                continue;

            nearest = candidate;
            target = goblin;
            nearestDistance = hits[i].distance;
            nearestStatic = false;
        }

        float advance = nearest != null ? Mathf.Min(distance, nearestDistance) : distance;
        transform.position += (Vector3)(direction * advance);
        travelled += advance;
        if (visual != null) visual.Rotate(0f, 0f, -600f * advance / Mathf.Max(0.1f, speed));

        if (nearest != null)
        {
            if (target != null) target.Stun(stunSeconds);

            var enemyHealth = nearest.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(10, direction);
            }

            if (owner != null) owner.PlayImpact(transform.position);
            Finish();
        }
        else if (travelled >= range - 0.0001f)
            Finish();
    }

    private bool HasClearApproach(Vector2 origin, Collider2D targetBody, GoblinAI target)
    {
        int count = Physics2D.Linecast(origin, targetBody.bounds.center, filter, visibilityHits);
        for (int i = 0; i < count; i++)
        {
            Collider2D candidate = visibilityHits[i].collider;
            if (candidate == null || candidate.isTrigger ||
                (target != null && candidate.GetComponentInParent<GoblinAI>() == target) ||
                (owner != null && candidate.transform.IsChildOf(owner.transform)))
                continue;
            Rigidbody2D attached = candidate.attachedRigidbody;
            if (attached == null || attached.bodyType == RigidbodyType2D.Static)
                return false;
        }
        return true;
    }

    private void Finish()
    {
        finished = true;
        Destroy(gameObject);
    }
}
