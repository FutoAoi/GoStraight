using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct AttackHitInfo
{
    public IDamageable Target { get; }
    public Vector3 Position { get; }
    public Vector3 Direction { get; }

    public AttackHitInfo(IDamageable target, Vector3 position, Vector3 direction)
    {
        Target = target;
        Position = position;
        Direction = direction;
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class AttackHitbox : MonoBehaviour
{
    [Serializable]
    private class ComboHitboxSettings
    {
        [Tooltip("Multiplier relative to the BoxCollider size. X = width, Y = height, Z = reach.")]
        public Vector3 SizeMultiplier = Vector3.one;
        [Tooltip("Offset from the BoxCollider center in local space. Negative Z moves it toward the player.")]
        public Vector3 CenterOffset = Vector3.zero;
    }

    [SerializeField] private LayerMask _targetLayers = ~0;
    [Header("Hitbox settings per combo step")]
    [SerializeField] private ComboHitboxSettings _firstHit = new();
    [SerializeField] private ComboHitboxSettings _secondHit = new()
    {
        SizeMultiplier = new Vector3(1.3f, 1f, 1.2f)
    };
    [SerializeField] private ComboHitboxSettings _thirdHit = new();

    private readonly HashSet<IDamageable> _hitTargets = new();
    private BoxCollider _shape;
    private Transform _owner;
    private int _damage;
    private float _knockbackDistance;
    private float _knockbackDuration;
    private bool _active;
    private int _comboStep;
    private PlayerCombat _ownerCombat;
    public event Action<AttackHitInfo> Hit;

    private void Awake()
    {
        _shape = GetComponent<BoxCollider>();
        // Colliderは範囲の編集用。物理的な衝突やTrigger通知は使用しない。
        _shape.isTrigger = true;
        _shape.enabled = false;
    }

    public void BeginAttack(Transform owner, int damage, float knockbackDistance = 0f, float knockbackDuration = 0.25f, int comboStep = 0)
    {
        if (!isActiveAndEnabled || _active || owner == null || damage <= 0) return;

        _owner = owner;
        _ownerCombat = owner.GetComponent<PlayerCombat>();
        _comboStep = comboStep;
        _damage = damage;
        _knockbackDistance = knockbackDistance;
        _knockbackDuration = knockbackDuration;
        _hitTargets.Clear();
        _active = true;
        // 短い判定時間でも、開始時に最低1回は調べる。
        CheckHits();
    }

    public void EndAttack()
    {
        _active = false;
        _owner = null;
        _ownerCombat = null;
        _hitTargets.Clear();
    }

    private void FixedUpdate()
    {
        if (_active) CheckHits();
    }

    private void CheckHits()
    {
        if (_ownerCombat != null && _ownerCombat.IsHitStopped) return;
        if (_owner == null)
        {
            EndAttack();
            return;
        }

        Vector3 scale = transform.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        GetLocalShape(_shape, _comboStep, out Vector3 localSize, out Vector3 localCenter);
        Vector3 halfExtents = Vector3.Scale(localSize * 0.5f, scale);
        Vector3 center = transform.TransformPoint(localCenter);

        Collider[] hits = Physics.OverlapBox(center, halfExtents, transform.rotation,
            _targetLayers, QueryTriggerInteraction.Collide);

        foreach (var hit in hits)
        {
            // 武器や自分の体に付いたColliderは対象外。
            if (hit.transform.IsChildOf(_owner)) continue;

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;
            if (damageable is Health health && (health.IsDead || health.IsInvulnerable)) continue;
            if (damageable is Component receiver &&
                receiver.transform.IsChildOf(_owner)) continue;

            // Collider単位ではなく、ダメージを受け取る相手単位で重複を防ぐ。
            if (_hitTargets.Add(damageable))
            {
                // 死亡処理でColliderが無効になる前に、演出用の位置を保存する。
                Vector3 position = hit.ClosestPoint(center);
                Vector3 direction = position - _owner.position;
                if (direction.sqrMagnitude < 0.0001f) direction = _owner.forward;
                var info = new AttackHitInfo(damageable, position, direction.normalized);
                damageable.TakeDamage(_damage);
                if (_knockbackDistance > 0f && damageable is Component component)
                {
                    var knockback = component.GetComponentInParent<IKnockbackReceiver>();
                    Vector3 away = component.transform.position - _owner.position;
                    if (away.sqrMagnitude < 0.0001f) away = _owner.forward;
                    knockback?.ApplyKnockback(away, _knockbackDistance, _knockbackDuration);
                }
                Hit?.Invoke(info);
                if (_ownerCombat != null && damageable is Component target)
                    _ownerCombat.PlayHitStop(target, _comboStep);
            }
        }
    }

    private void OnDisable()
    {
        EndAttack();
    }

    private void GetLocalShape(BoxCollider shape, int step, out Vector3 size, out Vector3 center)
    {
        var settings = step == 1 ? _firstHit : step == 2 ? _secondHit : step == 3 ? _thirdHit : null;
        Vector3 multiplier = settings != null ? settings.SizeMultiplier : Vector3.one;
        multiplier = new Vector3(Mathf.Max(0.01f, multiplier.x),
            Mathf.Max(0.01f, multiplier.y), Mathf.Max(0.01f, multiplier.z));
        size = Vector3.Scale(shape.size, multiplier);
        center = shape.center + (settings != null ? settings.CenterOffset : Vector3.zero);
    }

    private void OnDrawGizmosSelected()
    {
        var shape = GetComponent<BoxCollider>();
        if (shape == null) return;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        for (int step = 1; step <= 3; step++)
        {
            GetLocalShape(shape, step, out Vector3 size, out Vector3 center);
            Gizmos.color = step == 1 ? Color.cyan : step == 2 ? Color.yellow : Color.magenta;
            Gizmos.DrawWireCube(center, size);
        }
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
