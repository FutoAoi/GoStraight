using UnityEngine;
using Unity.AI.Navigation;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public sealed class DeployableBridge : GimmickAction
{
    [SerializeField] private float _foldedAngle = -90f;
    [SerializeField, Min(1f)] private float _rotationSpeed = 90f;
    [SerializeField] private bool _startDeployed;
    [SerializeField, Min(0.1f)] private float _length = 6f;
    [SerializeField, Min(0.1f)] private float _width = 2.5f;
    private Rigidbody _body;
    private NavMeshLink _link;
    private Quaternion _deployedLocalRotation;
    public bool IsDeployed { get; private set; }

    private void Awake()
    {
        _body = GetComponent<Rigidbody>();
        _body.isKinematic = true;
        _body.useGravity = false;
        _deployedLocalRotation = transform.localRotation;
        IsDeployed = _startDeployed;
        if (!TryGetComponent<NavMeshLink>(out _link)) _link = gameObject.AddComponent<NavMeshLink>();
        _link.enabled = false;
        // Put endpoints safely inside each bank, beyond NavMesh edge erosion.
        _link.startPoint = new Vector3(0f, 0f, -1f);
        _link.endPoint = new Vector3(0f, 0f, _length + 1f);
        _link.width = _width;
        _body.rotation = TargetRotation();
        _link.enabled = _startDeployed;
    }

    public override void SetActivated(bool active)
    {
        IsDeployed = active;
        if (_link != null) _link.enabled = false;
    }

    private Quaternion TargetRotation()
    {
        Quaternion local = _deployedLocalRotation * Quaternion.Euler(IsDeployed ? 0f : _foldedAngle, 0f, 0f);
        return transform.parent != null ? transform.parent.rotation * local : local;
    }

    private void FixedUpdate()
    {
        Quaternion target = TargetRotation();
        Quaternion next = Quaternion.RotateTowards(_body.rotation, target, _rotationSpeed * Time.fixedDeltaTime);
        _body.MoveRotation(next);
        if (_link != null) _link.enabled = IsDeployed && Quaternion.Angle(_body.rotation, target) < 0.1f;
    }
}
