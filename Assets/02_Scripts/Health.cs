using System;
using UnityEngine;

[DisallowMultipleComponent]
public class Health : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1)] private int _maxHealth = 100;

    public int MaxHealth => _maxHealth;
    public int CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0;

    // UIなどはこの通知を受け取り、初期表示にはCurrentHealthを使う。
    public event Action<int, int> HealthChanged;
    public event Action Died;

    private void Awake()
    {
        _maxHealth = Mathf.Max(1, _maxHealth);
        CurrentHealth = _maxHealth;
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || IsDead) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        bool died = IsDead;
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);

        // 死亡後のTakeDamageは上で無視するので、死亡通知は1回だけ。
        if (died) Died?.Invoke();
    }

    private void OnValidate()
    {
        _maxHealth = Mathf.Max(1, _maxHealth);
    }

    [ContextMenu("Debug/Take 10 Damage (Play Mode)")]
    private void DebugTakeDamage()
    {
        if (!Application.isPlaying) return;
        TakeDamage(10);
        Debug.Log($"{name}: HP {CurrentHealth}/{MaxHealth}", this);
    }
}
