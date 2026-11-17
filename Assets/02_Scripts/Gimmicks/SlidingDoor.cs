using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class SlidingDoor : GimmickAction
{
    [SerializeField] private Vector3 _openOffset = new(0f, 3.2f, 0f);
    [SerializeField, Min(0.01f)] private float _speed = 3f;
    [SerializeField] private bool _startOpen;
    private Vector3 _closedLocalPosition;
    private Rigidbody _body;
    private NavMeshObstacle _obstacle;
    public bool IsOpen { get; private set; }
    public bool IsFullyOpen => IsOpen && _body != null &&
        (_body.position - WorldTarget()).sqrMagnitude < 0.0001f;

    public void ConfigureOpening(Vector3 localOffset, float speed)
    {
        _openOffset = localOffset;
        _speed = Mathf.Max(0.01f, speed);
    }

    private void Awake()
    {
        _body = GetComponent<Rigidbody>();
        _body.isKinematic = true;
        _body.useGravity = false;
        _closedLocalPosition = transform.localPosition;
        _obstacle = GetComponent<NavMeshObstacle>();
        IsOpen = _startOpen;
        if (_startOpen) _body.position = WorldTarget();
        if (_obstacle != null) _obstacle.enabled = !_startOpen;
    }

    public override void SetActivated(bool active)
    {
        IsOpen = active;
        if (!active && _obstacle != null) _obstacle.enabled = true;
    }

    public void Open() => SetActivated(true);
    public void Close() => SetActivated(false);

    private Vector3 WorldTarget()
    {
        Vector3 local = _closedLocalPosition + (IsOpen ? _openOffset : Vector3.zero);
        return transform.parent != null ? transform.parent.TransformPoint(local) : local;
    }

    private void FixedUpdate()
    {
        Vector3 target = WorldTarget();
        Vector3 next = Vector3.MoveTowards(_body.position, target, _speed * Time.fixedDeltaTime);
        _body.MovePosition(next);
        if (_obstacle != null && IsOpen && (next - target).sqrMagnitude < 0.0001f)
            _obstacle.enabled = false;
    }
}
