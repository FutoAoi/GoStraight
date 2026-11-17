using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLockOn : MonoBehaviour
{
    [SerializeField] private PlayerRotation _playerRotation;
    [SerializeField] private InputActionReference _lockOnAction;
    [SerializeField] float _maxDistance = 15f;

    public Transform CurrentTarget { get; private set; }
    public Transform CandidateTarget { get; private set; }
    private float _nextCandidateSearch;
    private readonly List<Targetable> _candidates = new();
    public IReadOnlyList<Targetable> Candidates => _candidates;
    public Targetable LockedTarget => _currentTargetable;
    [Header("Target switching")]
    [SerializeField] private InputActionReference _switchLookAction;
    [SerializeField, Range(0.1f, 1f)] private float _stickThreshold = .6f;
    [SerializeField, Min(1f)] private float _mouseThreshold = 12f;
    [SerializeField, Min(.1f)] private float _switchCooldown = .3f;
    private bool _switchHeld;
    private float _nextSwitchTime;
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
        CandidateTarget = null;
        
    }

    private void Update()
    {
        if (GameMenuController.IsGamePaused) return;
        if (Time.time >= _nextCandidateSearch)
        {
            var candidate = FindNearestTarget();
            CandidateTarget = candidate != null ? candidate.AimPoint : null;
            _nextCandidateSearch = Time.time + 0.1f;
        }
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

            if (_wasPressed) UpdateTargetSwitch();
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
        _candidates.Clear();
        Targetable nearest = null;
        float nearestDistanceSquared = float.PositiveInfinity;

        foreach (var candidate in FindObjectsByType<Targetable>())
        {
            // プレイヤー自身にTargetableが付いていても対象にしない。
            if (candidate.transform.IsChildOf(transform)) continue;
            if (!TryGetAimPoint(candidate, out var aimPoint)) continue;

            _candidates.Add(candidate);
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

    private void UpdateTargetSwitch()
    {
        var look = _switchLookAction != null ? _switchLookAction.action
            : _lockOnAction.action.actionMap?.FindAction("Look");
        Vector2 value = look != null ? look.ReadValue<Vector2>() : Vector2.zero;
        bool mouse = look?.activeControl?.device is Mouse;
        float threshold = mouse ? _mouseThreshold : _stickThreshold;
        bool pressed = Mathf.Abs(value.x) >= threshold;
        if (pressed && (mouse || !_switchHeld) && Time.unscaledTime >= _nextSwitchTime)
        {
            SwitchTarget(value.x > 0 ? 1 : -1);
            _nextSwitchTime = Time.unscaledTime + _switchCooldown;
        }
        _switchHeld = pressed;
    }

    private void SwitchTarget(int direction)
    {
        FindNearestTarget();
        var camera = Camera.main;
        if (camera == null) return;
        var current = _currentTargetable != null ? _currentTargetable.AimPoint : null;
        float origin = current != null ? camera.WorldToViewportPoint(current.position).x : .5f;
        Targetable best = null;
        float bestDistance = float.PositiveInfinity;
        foreach (var candidate in _candidates)
        {
            if (candidate == _currentTargetable) continue;
            var point = camera.WorldToViewportPoint(candidate.AimPoint.position);
            if (point.z <= 0 || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) continue;
            float distance = (point.x - origin) * direction;
            if (distance <= .001f || distance >= bestDistance) continue;
            bestDistance = distance;
            best = candidate;
        }
        if (best != null) _currentTargetable = best;
    }

    private void ClearTarget()
    {
        _currentTargetable = null;
        SetTarget(null);
        IsFocusing = false;
        _switchHeld = false;
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
