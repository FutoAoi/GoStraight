using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLockOn : MonoBehaviour
{
    [SerializeField] private PlayerRotation _playerRotation;
    [SerializeField] private InputActionReference _lockOnAction;
    [SerializeField] float _maxDistance = 15f;

    public Transform CurrentTarget { get; private set; }
    public bool IsFocusing { get; private set; }
    public Vector3 FocusDirection { get; private set; }
    private bool _waitForRelease;
    private bool _wasPressed;
    private Targetable _currentTargetable;

    public void ResetLockOn()
    {
        ClearTarget();
        // 押し続けたまま次のステージで即座に再ロックしない。
        _waitForRelease = _lockOnAction.action.IsPressed();
    }

    private void OnEnable()
    {
        _lockOnAction.action.Enable();
    }

    private void OnDisable()
    {
        _lockOnAction.action.Disable();
        ClearTarget();
        _wasPressed = false;
        
    }

    private void Update()
    {
        bool isPressed = _lockOnAction.action.IsPressed();

        if (_waitForRelease)
        {
            _waitForRelease = isPressed;
            _wasPressed = isPressed;
            return;
        }

        if (!isPressed)
        {
            ClearTarget();
        }
        else
        {
            // 押し始めたときだけ検索し、押している間は同じ相手を追う。
            if (!_wasPressed)
            {
                _currentTargetable = FindNearestTarget();
                CaptureFacingDirection();
            }

            if (TryGetAimPoint(_currentTargetable, out var aimPoint))
            {
                SetTarget(aimPoint);
                IsFocusing = true;
                _playerRotation.FacingDirection = null;
            }
            else
            {
                // 敵を見失った場合も、その時点の向きを保って注目を続ける。
                if (IsFocusing && !_playerRotation.FacingDirection.HasValue)
                    CaptureFacingDirection();

                _currentTargetable = null;
                SetTarget(null);
                IsFocusing = true;
                _playerRotation.FacingDirection = FocusDirection;
            }
        }

        _wasPressed = isPressed;
    }

    private Targetable FindNearestTarget()
    {
        Targetable nearest = null;
        float nearestDistanceSquared = float.PositiveInfinity;

        foreach (var candidate in FindObjectsByType<Targetable>())
        {
            // プレイヤー自身にTargetableが付いていても対象にしない。
            if (candidate.transform.IsChildOf(transform)) continue;
            if (!TryGetAimPoint(candidate, out var aimPoint)) continue;

            float distanceSquared = (aimPoint.position - transform.position).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearest = candidate;
                nearestDistanceSquared = distanceSquared;
            }
        }

        return nearest;
    }

    private bool TryGetAimPoint(Targetable candidate, out Transform aimPoint)
    {
        aimPoint = null;
        // Unityの破棄済み判定は、インターフェースに変換する前に行う。
        if (candidate == null) return false;

        ITargetable target = candidate;
        if (!target.CanTarget) return false;

        aimPoint = target.AimPoint;
        if (aimPoint == null) return false;

        float maxDistance = Mathf.Max(0f, _maxDistance);
        return (aimPoint.position - transform.position).sqrMagnitude
            <= maxDistance * maxDistance;
    }

    private void ClearTarget()
    {
        _currentTargetable = null;
        SetTarget(null);
        IsFocusing = false;
        FocusDirection = Vector3.zero;
        _playerRotation.FacingDirection = null;
    }

    private void CaptureFacingDirection()
    {
        Vector3 forward = _playerRotation.transform.forward;
        forward.y = 0f;
        FocusDirection = forward.sqrMagnitude > 0.0001f
            ? forward.normalized : Vector3.forward;
    }

    private void SetTarget(Transform target)
    {
        CurrentTarget = target;
        _playerRotation.LookTarget = target;
    }
}
