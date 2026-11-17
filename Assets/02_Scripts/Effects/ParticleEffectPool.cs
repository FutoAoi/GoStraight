using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

// 単発のParticleSystem用。Prefabごとに別のプールを作る。
[DisallowMultipleComponent]
public class ParticleEffectPool : MonoBehaviour
{
    [SerializeField, Min(1)] private int _maxInactivePerPrefab = 16;
    private readonly Dictionary<ParticleSystem, ObjectPool<ParticleSystem>> _pools = new();
    private readonly List<PlayingEffect> _playing = new();

    private readonly struct PlayingEffect
    {
        public readonly ParticleSystem Effect;
        public readonly ObjectPool<ParticleSystem> Pool;
        public PlayingEffect(ParticleSystem effect, ObjectPool<ParticleSystem> pool)
        {
            Effect = effect;
            Pool = pool;
        }
    }

    public void Play(ParticleSystem prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null || !isActiveAndEnabled) return;
        if (!_pools.TryGetValue(prefab, out var pool))
        {
            pool = new ObjectPool<ParticleSystem>(
                () => CreateEffect(prefab), null, ReturnEffect, DestroyEffect,
                collectionCheck: true, defaultCapacity: 4,
                maxSize: Mathf.Max(1, _maxInactivePerPrefab));
            _pools.Add(prefab, pool);
        }

        ParticleSystem effect = pool.Get();
        effect.transform.SetPositionAndRotation(position, rotation);
        effect.gameObject.SetActive(true);
        effect.Play(true);
        _playing.Add(new PlayingEffect(effect, pool));
    }

    private ParticleSystem CreateEffect(ParticleSystem prefab)
    {
        // 呼び出し元の移動・回転に追従させない。
        ParticleSystem effect = Instantiate(prefab);
        effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        foreach (var particles in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None;
        }
        effect.gameObject.SetActive(false);
        return effect;
    }

    private void Update()
    {
        for (int i = _playing.Count - 1; i >= 0; i--)
        {
            PlayingEffect playing = _playing[i];
            if (playing.Effect != null && playing.Effect.IsAlive(true)) continue;
            _playing.RemoveAt(i);
            if (playing.Effect != null) playing.Pool.Release(playing.Effect);
        }
    }

    private static void ReturnEffect(ParticleSystem effect)
    {
        if (effect == null) return;
        effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        effect.gameObject.SetActive(false);
    }

    private static void DestroyEffect(ParticleSystem effect)
    {
        if (effect != null) Destroy(effect.gameObject);
    }

    public void StopAll()
    {
        foreach (var playing in _playing)
            if (playing.Effect != null) playing.Pool.Release(playing.Effect);
        _playing.Clear();
    }

    private void OnDisable() => StopAll();

    private void OnDestroy()
    {
        StopAll();
        foreach (var pool in _pools.Values) pool.Clear();
        _pools.Clear();
    }
}
