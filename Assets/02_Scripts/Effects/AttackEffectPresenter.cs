using UnityEngine;
using VContainer;

[DisallowMultipleComponent]
public class AttackEffectPresenter : MonoBehaviour
{
    [Tooltip("ヒット時にすべて再生する単発エフェクト。火花と衝撃波などを組み合わせられる。")]
    [SerializeField] private ParticleSystem[] _hitEffectPrefabs = System.Array.Empty<ParticleSystem>();
    private AttackHitbox[] _hitboxes;
    private ParticleEffectPool _effectPool;
    private bool _reportedMissingPool;

    [Inject]
    public void Construct(ParticleEffectPool effectPool)
    {
        _effectPool = effectPool;
    }

    private void Awake()
    {
        _hitboxes = GetComponentsInChildren<AttackHitbox>(true);
    }

    private void OnEnable()
    {
        foreach (var hitbox in _hitboxes)
            hitbox.Hit += HandleHit;
    }

    private void OnDisable()
    {
        foreach (var hitbox in _hitboxes)
            if (hitbox != null) hitbox.Hit -= HandleHit;
    }

    private void HandleHit(AttackHitInfo hit)
    {
        if (_effectPool == null)
        {
            if (!_reportedMissingPool)
            {
                Debug.LogWarning("AttackEffectPresenter needs ParticleEffectPool injection from GameLifetimeScope.", this);
                _reportedMissingPool = true;
            }
            return;
        }
        Quaternion rotation = hit.Direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(hit.Direction)
            : Quaternion.identity;

        foreach (var prefab in _hitEffectPrefabs)
        {
            if (prefab == null) continue;
            _effectPool.Play(prefab, hit.Position, rotation);
        }
    }
}
