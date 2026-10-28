using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
public class EnemyDeath : MonoBehaviour
{
    private Health _health;

    private void Awake()
    {
        _health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        _health.Died += HandleDeath;
    }

    private void OnDisable()
    {
        _health.Died -= HandleDeath;
    }

    private void HandleDeath()
    {
        // Destroyはフレーム末尾なので、それまでのロックオンと接触も止める。
        foreach (var target in GetComponentsInChildren<Targetable>(true))
            target.SetTargetable(false);

        foreach (var hitCollider in GetComponentsInChildren<Collider>(true))
            hitCollider.enabled = false;

        // 死亡演出や撃破通知は、この敵専用の処理へ追加できる。
        Destroy(gameObject);
    }
}
