using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class TimedAttackSwitch : GimmickSignal, IDamageable
{
    [SerializeField, Min(0.1f)] private float _duration = 5f;
    private float _remaining;
    public float RemainingSeconds => _remaining;

    protected override void Start()
    {
        if (IsActive) _remaining = _duration;
        base.Start();
    }

    public void TakeDamage(int damage)
    {
        if (!isActiveAndEnabled || damage <= 0) return;
        _remaining = Mathf.Max(0.1f, _duration);
        Activate();
    }

    private void Update()
    {
        if (!IsActive) return;
        _remaining = Mathf.Max(0f, _remaining - Time.deltaTime);
        if (_remaining <= 0f) Deactivate();
    }

    private void OnDisable()
    {
        _remaining = 0f;
        Deactivate();
    }
}
