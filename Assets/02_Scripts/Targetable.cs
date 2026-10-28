using UnityEngine;

public class Targetable : MonoBehaviour, ITargetable
{
    [SerializeField] private Transform _aimPoint;
    [SerializeField] private bool _canTarget = true;

    public Transform AimPoint =>
        _aimPoint != null ? _aimPoint : transform;

    public bool CanTarget =>
        _canTarget && isActiveAndEnabled;

    public void SetTargetable(bool canTarget)
    {
        _canTarget = canTarget;
    }
}