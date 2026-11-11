using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Level4BossNavigation : MonoBehaviour
{
    private readonly List<Vector2> path = new List<Vector2>();
    private readonly GoblinNavigation2D.PathRequest pathRequest = new GoblinNavigation2D.PathRequest();
    private bool shortcutRequested;
    private Rigidbody2D body;
    private Collider2D feet;
    private Collider2D playerFeet;
    private GoblinNavigation2D navigation;
    private Vector2 preferredDirection;
    private float speed;
    private float chaseDistance;
    private float nextPath;
    private int waypoint;
    private Vector2 progressPosition;
    private float lastProgress;
    private bool allowTravelShortcut;

    public Transform MinionParent { get; private set; }
    public Sprite ProjectileSprite { get; private set; }

    public void Begin(PlayerHealth player, GoblinNavigation2D navigator, Transform minionParent,
        Sprite projectileSprite, bool allowTravelShortcut)
    {
        body = GetComponent<Rigidbody2D>();
        feet = GetComponent<Collider2D>();
        playerFeet = player.GetComponent<Collider2D>();
        navigation = navigator;
        MinionParent = minionParent;
        ProjectileSprite = projectileSprite;
        this.allowTravelShortcut = allowTravelShortcut;
        Physics2D.SyncTransforms();
        if (allowTravelShortcut && Vector2.Distance(feet.bounds.center, playerFeet.bounds.center) > 8f)
            TryApproachPlayer();

        progressPosition = feet.bounds.center;
        lastProgress = Time.time;
    }

    public bool IsInAttackRange(float range)
    {
        Vector2 center = feet.bounds.center;
        Vector2 target = playerFeet.bounds.center;
        return (target - center).sqrMagnitude <= range * range && navigation.HasLineOfSight(center, target);
    }

    public void SetMovement(Vector2 direction, float moveSpeed, float chaseFrom)
    {
        preferredDirection = direction;
        speed = moveSpeed;
        chaseDistance = chaseFrom;
    }

    public void Stop()
    {
        speed = 0f;
        CancelPathRequest();
        if (body != null && body.simulated) body.velocity = Vector2.zero;
    }

    private void FixedUpdate()
    {
        if (speed <= 0f || playerFeet == null || !feet.enabled || !body.simulated)
            return;

        Vector2 center = feet.bounds.center;
        Vector2 target = playerFeet.bounds.center;
        float distance = Vector2.Distance(center, target);
        bool pursue = distance > chaseDistance || !navigation.HasLineOfSight(center, target);
        if (Vector2.Distance(center, progressPosition) >= 0.18f || !pursue)
        {
            progressPosition = center;
            lastProgress = Time.time;
            if (shortcutRequested) CancelPathRequest();
        }
        else if (allowTravelShortcut && !shortcutRequested && distance > 5.5f && Time.time - lastProgress >= 2f)
        {
            lastProgress = Time.time;
            CancelPathRequest();
            shortcutRequested = navigation.RequestPath(pathRequest, center, target);
        }

        if (pathRequest.Take(path))
        {
            waypoint = 0;
            if (shortcutRequested)
            {
                shortcutRequested = false;
                if (TryApproachPlayerUsingPath(center, target)) return;
            }
        }

        float step = speed * Time.fixedDeltaTime;
        Vector2 movement = pursue
            ? Vector2.ClampMagnitude(GetPursuitPoint(center, target) - center, step)
            : preferredDirection * step;

        if (navigation.Clear(center, center + movement))
            body.MovePosition(body.position + movement);
    }

    private Vector2 GetPursuitPoint(Vector2 center, Vector2 target)
    {
        if (navigation.Clear(center, target))
        {
            if (!shortcutRequested)
            {
                CancelPathRequest();
                nextPath = 0f;
            }
            return target;
        }

        if (!shortcutRequested && Time.time >= nextPath && navigation.RequestPath(pathRequest, center, target))
            nextPath = Time.time + 0.5f;

        while (waypoint + 1 < path.Count && navigation.Clear(center, path[waypoint + 1]))
            waypoint++;

        return waypoint < path.Count && navigation.Clear(center, path[waypoint])
            ? path[waypoint]
            : center;
    }

    private bool TryApproachPlayer()
    {
        Vector2 target = playerFeet.bounds.center;
        Vector2 center = feet.bounds.center;
        navigation.FindPath(center, target, path);
        return TryApproachPlayerUsingPath(center, target);
    }

    private bool TryApproachPlayerUsingPath(Vector2 center, Vector2 target)
    {
        Vector2 offset = center - body.position;
        for (int index = path.Count - 1; index >= 0; index--)
        {
            Vector2 point = path[index];
            float distance = Vector2.Distance(point, target);
            if (distance < 5f || distance > 7f || !navigation.Clear(point, point)) continue;
            PlaceAt(point - offset);
            return true;
        }

        for (int index = 0; index < 16; index++)
        {
            float angle = index * Mathf.PI / 8f;
            Vector2 point = target + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 5.6f;
            if (!navigation.Clear(point, target)) continue;
            PlaceAt(point - offset);
            return true;
        }

        return false;
    }

    private void CancelPathRequest()
    {
        if (navigation != null) navigation.CancelPath(pathRequest);
        shortcutRequested = false;
    }

    private void OnDisable() => CancelPathRequest();

    private void PlaceAt(Vector2 position)
    {
        CancelPathRequest();
        body.position = position;
        transform.position = position;
        body.velocity = Vector2.zero;
        nextPath = 0f;
        path.Clear();
        Physics2D.SyncTransforms();
    }

    public void ReturnNearPlayer()
    {
        if (playerFeet == null) return;
        Vector2 target = playerFeet.bounds.center;
        var box = feet as BoxCollider2D;
        Vector2 offset = box != null ? (Vector2)box.transform.TransformVector(box.offset) : Vector2.zero;
        for (int index = 0; index < 16; index++)
        {
            float angle = (index * 5 % 16) * Mathf.PI / 8f;
            Vector2 point = target + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 4.15f;
            if (!navigation.Clear(point, point) || !navigation.Clear(point, target)) continue;
            PlaceAt(point - offset);
            progressPosition = point;
            lastProgress = Time.time;
            return;
        }
    }

    public IEnumerator BackstepFromPlayer()
    {
        const float maxDistance = 1.3f;
        const float backstepSpeed = 3.2f;
        var fixedFrame = new WaitForFixedUpdate();

        for (float remaining = maxDistance; remaining > 0f;)
        {
            yield return fixedFrame;
            if (playerFeet == null || !feet.enabled || !body.simulated) yield break;

            Vector2 center = feet.bounds.center;
            Vector2 away = (center - (Vector2)playerFeet.bounds.center).normalized;
            if (away == Vector2.zero) yield break;

            float step = Mathf.Min(remaining, backstepSpeed * Time.fixedDeltaTime);
            Vector2 move = away * step;
            if (!navigation.Clear(center, center + move))
            {
                Vector2 side = new Vector2(-away.y, away.x);
                move = (away + side).normalized * step;
                if (!navigation.Clear(center, center + move))
                {
                    move = (away - side).normalized * step;
                    if (!navigation.Clear(center, center + move)) yield break;
                }
            }

            body.MovePosition(body.position + move);
            remaining -= step;
        }

        yield return fixedFrame;
    }

    public bool CanStandAt(Vector2 position)
    {
        Vector2 center = position + (Vector2)feet.bounds.center - body.position;
        return navigation.Clear(center, center) && navigation.Clear(feet.bounds.center, center);
    }

    public bool TryFindMinionSpawn(Vector2 center, out Vector2 spawn)
    {
        for (int index = 0; index < 16; index++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector2 candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(2.8f, 4.5f);
            Vector2 feetCenter = candidate + Vector2.up * 0.18f;
            if (!navigation.Clear(feetCenter, feetCenter) || !navigation.Clear(feetCenter, center)) continue;
            spawn = candidate;
            return true;
        }

        spawn = center;
        return false;
    }
}
