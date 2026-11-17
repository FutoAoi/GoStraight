using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public sealed class PushableBox : MonoBehaviour
{
    [SerializeField] private bool _canPush = true;
    [SerializeField, Min(0f)] private float _pushAcceleration = 15f;
    [SerializeField, Min(0.1f)] private float _maxSpeed = 2f;
    private Rigidbody _body;
    public bool CanPush => _canPush && isActiveAndEnabled && _body != null && !_body.isKinematic;

    private void Reset()
    {
        var body = GetComponent<Rigidbody>();
        body.mass = 5f;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.linearDamping = 3f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void Awake() => _body = GetComponent<Rigidbody>();

    private void FixedUpdate()
    {
        if (_body.isKinematic) return;
        Vector3 velocity = _body.linearVelocity;
        Vector3 horizontal = Vector3.ClampMagnitude(new Vector3(velocity.x, 0f, velocity.z), _maxSpeed);
        _body.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!CanPush) return;
        var player = collision.gameObject.GetComponentInParent<PlayerMovement>();
        if (player == null || player.MoveInput.sqrMagnitude < 0.01f || player.IsKnockedBack) return;
        var combat = player.GetComponent<PlayerCombat>();
        if (combat != null && (combat.BlocksMovement || combat.IsHitStopped)) return;
        bool sideContact = false;
        for (int i = 0; i < collision.contactCount; i++)
            if (Mathf.Abs(collision.GetContact(i).normal.y) < 0.5f) sideContact = true;
        if (!sideContact) return;
        Vector3 direction = player.MoveDirection;
        direction.y = 0f;
        Vector3 towardBox = _body.worldCenterOfMass - player.transform.position;
        towardBox.y = 0f;
        if (Vector3.Dot(direction, towardBox) <= 0f) return;
        _body.AddForce(direction.normalized * _pushAcceleration, ForceMode.Acceleration);
    }
}
