using UnityEngine;

// Shared contract for player and enemy knockback; independent of either implementation.
public interface IKnockbackReceiver
{
    void ApplyKnockback(Vector3 direction, float distance, float duration);
}
