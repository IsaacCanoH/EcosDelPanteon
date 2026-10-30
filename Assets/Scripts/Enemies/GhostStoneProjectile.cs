using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public sealed class GhostStoneProjectile : MonoBehaviour
{
    public float speed = 10f;
    public float lifetime = 5f;
    private int damage;
    private Rigidbody2D body;
    private Collider2D projectileCollider;
    private bool hit;

    public void Launch(Vector2 target, Collider2D[] ownerColliders, int attackDamage)
    {
        body = GetComponent<Rigidbody2D>();
        projectileCollider = GetComponent<Collider2D>();
        damage = attackDamage;
        foreach (var c in ownerColliders) if (c != null) Physics2D.IgnoreCollision(projectileCollider, c);
        body.velocity = (target - body.position).normalized * speed;
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hit || other.isTrigger || other.GetComponentInParent<GhostChildAI>() != null || other.GetComponent<GhostStoneProjectile>() != null) return;
        var health = other.GetComponentInParent<PlayerHealth>();
        if (health == null && other.gameObject.layer != LayerMask.NameToLayer("Obstacles")) return;
        hit = true;
        if (health != null) health.TakeDamage(damage);
        if (body != null) body.velocity = Vector2.zero;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
