using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class AttackSwitch : GimmickSignal, IDamageable
{
    [SerializeField, Min(1)] private int _requiredHits = 1;
    [Tooltip("Off: stay activated once hit enough times. On: toggle on/off each time.")]
    [SerializeField] private bool _toggle;
    private int _hitCount;

    public void TakeDamage(int damage)
    {
        if (!isActiveAndEnabled || damage <= 0 || (!_toggle && IsActive)) return;
        if (++_hitCount < Mathf.Max(1, _requiredHits)) return;
        _hitCount = 0;
        if (_toggle) Toggle();
        else Activate();
    }

    public void ResetSwitch()
    {
        _hitCount = 0;
        Deactivate();
    }
}
