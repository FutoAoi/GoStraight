using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
public sealed class PlayerHealth : MonoBehaviour
{
    [SerializeField, Min(1)] private int _maxHealth = 5;
    [SerializeField] private PlayerHealthView _view;
    [Header("Damage camera shake")]
    [SerializeField, Min(0f)] private float _shakeDuration = 0.18f;
    [SerializeField, Min(0f)] private float _shakeStrength = 0.045f;
    private Health _health;
    public Health Health => _health;

    private void Awake() => _health = GetComponent<Health>();
    private void OnEnable()
    {
        _health.Died += OnDied;
        _health.Damaged += OnDamaged;
    }
    private void OnDisable()
    {
        _health.Died -= OnDied;
        _health.Damaged -= OnDamaged;
    }

    private void OnDamaged(int damage)
    {
        SoundManager.Instance.PlayPlayerHit();
        var combat = GetComponent<PlayerCombat>();
        if (combat != null) combat.ResetCombat();
        var jump = GetComponent<PlayerJamp>();
        if (jump != null) jump.ResetJump();
        var animation = GetComponent<PlayerAnimation>();
        if (animation != null) animation.PlayDamage();
        var camera = Camera.main;
        if (camera != null && camera.TryGetComponent<CameraOrbit>(out var orbit))
            orbit.Shake(_shakeDuration, _shakeStrength);
    }

    private void Start()
    {
        // Configure after all Health.Awake calls, including existing scene components.
        _health.Initialize(_maxHealth);
        if (_view == null || !_view.HasUI)
        {
            foreach (var view in FindObjectsByType<PlayerHealthView>(FindObjectsInactive.Include))
            {
                if (!view.HasUI) continue;
                if (view.gameObject.scene != gameObject.scene) continue;
                if (view.HealthSource != null && view.HealthSource != _health) continue;
                _view = view;
                break;
            }
        }
        if (_view != null && _view.HasUI) _view.Bind(_health);
        else Debug.LogWarning("Player HUD is missing. Place PlayerHUD.prefab in the scene.", this);
        // Reuse the same material flash as enemies; it only responds to actual damage.
        if (!TryGetComponent<EnemyDamageFlash>(out _)) gameObject.AddComponent<EnemyDamageFlash>();
    }

    private void OnDied()
    {
        if (!TryGetComponent<PlayerDeathSequence>(out var sequence)) sequence = gameObject.AddComponent<PlayerDeathSequence>();
        sequence.Begin();
    }

    [ContextMenu("Debug/Take 1 Damage (Play Mode)")]
    private void DebugTakeDamage()
    {
        if (Application.isPlaying) _health.TakeDamage(1);
    }
}
