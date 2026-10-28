using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class AttackHitbox : MonoBehaviour
{
    [SerializeField] private LayerMask _targetLayers = ~0;

    private readonly HashSet<IDamageable> _hitTargets = new();
    private BoxCollider _shape;
    private Transform _owner;
    private int _damage;
    private bool _active;

    private void Awake()
    {
        _shape = GetComponent<BoxCollider>();
        // Colliderは範囲の編集用。物理的な衝突やTrigger通知は使用しない。
        _shape.isTrigger = true;
        _shape.enabled = false;
    }

    public void BeginAttack(Transform owner, int damage)
    {
        if (!isActiveAndEnabled || _active || owner == null || damage <= 0) return;

        _owner = owner;
        _damage = damage;
        _hitTargets.Clear();
        _active = true;
        // 短い判定時間でも、開始時に最低1回は調べる。
        CheckHits();
    }

    public void EndAttack()
    {
        _active = false;
        _owner = null;
        _hitTargets.Clear();
    }

    private void FixedUpdate()
    {
        if (_active) CheckHits();
    }

    private void CheckHits()
    {
        if (_owner == null)
        {
            EndAttack();
            return;
        }

        Vector3 scale = transform.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        Vector3 halfExtents = Vector3.Scale(_shape.size * 0.5f, scale);
        Vector3 center = transform.TransformPoint(_shape.center);

        Collider[] hits = Physics.OverlapBox(center, halfExtents, transform.rotation,
            _targetLayers, QueryTriggerInteraction.Collide);

        foreach (var hit in hits)
        {
            // 武器や自分の体に付いたColliderは対象外。
            if (hit.transform.IsChildOf(_owner)) continue;

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;
            if (damageable is Component receiver &&
                receiver.transform.IsChildOf(_owner)) continue;

            // Collider単位ではなく、ダメージを受け取る相手単位で重複を防ぐ。
            if (_hitTargets.Add(damageable))
                damageable.TakeDamage(_damage);
        }
    }

    private void OnDisable()
    {
        EndAttack();
    }
}
