using UnityEngine;

public class PlayerRotation : MonoBehaviour
{
    public Transform LookTarget {  get; set; }
    public Vector3? FacingDirection { get; set; }
    [SerializeField] private PlayerMovement _moveController;
    [SerializeField] private float _rotationSpeed = 720f;
    Rigidbody _rigidbody;
    private PlayerCombat _combat;

    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _combat = GetComponent<PlayerCombat>();
    }

    private void FixedUpdate()
    {
        if (_combat != null && _combat.BlocksMovement) return;
        if (!TryGetDirection(out var direction)) return;

        var targetRotation = Quaternion.LookRotation(direction);
        _rigidbody.MoveRotation(
            Quaternion.RotateTowards(_rigidbody.rotation, targetRotation, _rotationSpeed * Time.fixedDeltaTime));
    }

    private bool TryGetDirection(out Vector3 direction)
    {
        if (LookTarget != null)
        {
            direction = LookTarget.position - transform.position;
        }
        else if (FacingDirection.HasValue)
        {
            direction = FacingDirection.Value;
        }
        else
        {
            direction = _moveController.MoveDirection;
        }

        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f;
    }
}
