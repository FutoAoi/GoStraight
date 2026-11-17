using UnityEngine;

public class Targetable : MonoBehaviour, ITargetable
{
    [SerializeField] private Transform _aimPoint;
    [SerializeField] private bool _canTarget = true;
    private Renderer[] _renderers;

    public Vector3 MarkerPosition(bool locked)
    {
        if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>(true);
        Bounds bounds = default;
        bool found = false;
        foreach (var renderer in _renderers)
        {
            if (renderer == null || !renderer.enabled || renderer is ParticleSystemRenderer ||
                renderer is TrailRenderer || renderer is LineRenderer || !renderer.gameObject.activeInHierarchy) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (!found) return AimPoint.position;
        return locked ? bounds.center : new Vector3(bounds.center.x, bounds.max.y + .12f, bounds.center.z);
    }

    public Transform AimPoint =>
        _aimPoint != null ? _aimPoint : transform;

    public bool CanTarget =>
        _canTarget && isActiveAndEnabled;

    public void SetTargetable(bool canTarget)
    {
        _canTarget = canTarget;
    }
}
