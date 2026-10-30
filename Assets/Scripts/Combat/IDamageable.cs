using UnityEngine;

public interface IDamageable
{
    bool IsDead { get; }
    void TakeDamage(int amount, Vector2 hitDirection);
}

