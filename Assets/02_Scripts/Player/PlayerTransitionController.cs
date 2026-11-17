using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Temporarily owns player movement during a doorway transition.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement), typeof(Rigidbody))]
public sealed class PlayerTransitionController : MonoBehaviour
{
    private readonly List<(Behaviour component, bool enabled)> _controls = new();
    private PlayerMovement _movement;
    private Rigidbody _body;
    private Health _health;
    private bool _wasInvulnerable;
    private Animator _animator;
    private bool _usedRootMotion;
    private Vector3 _target;
    private float _speed;
    public bool IsActive { get; private set; }

    public void Begin(Vector3 destination, float walkSpeed)
    {
        if (IsActive) return;
        _movement = GetComponent<PlayerMovement>();
        _body = GetComponent<Rigidbody>();
        _health = GetComponent<Health>();
        _target = destination;
        _speed = Mathf.Max(0f, walkSpeed);
        IsActive = true;
        if (_health != null) { _wasInvulnerable = _health.IsInvulnerable; _health.IsInvulnerable = true; }

        var combat = GetComponent<PlayerCombat>();
        if (combat != null) combat.ResetCombat();
        var jump = GetComponent<PlayerJamp>();
        if (jump != null) jump.ResetJump();
        var lockOn = GetComponent<PlayerLockOn>();
        if (lockOn != null) lockOn.ResetLockOn();
        _movement.ResetMovement();
        _movement.MoveInput = Vector2.zero;
        var animation = GetComponent<PlayerAnimation>();
        _animator = GetComponent<Animator>();
        if (_animator != null)
        {
            _usedRootMotion = _animator.applyRootMotion;
            _animator.applyRootMotion = false;
        }
        if (animation != null) animation.ResetAnimation();

        _controls.Clear();
        Suspend(GetComponent<PlayerInput>());
        Suspend(combat);
        Suspend(jump);
        Suspend(lockOn);
        Suspend(GetComponent<PlayerRotation>());
        Suspend(GetComponent<PlayerInteractor>());
        Suspend(_movement);
    }

    public void MoveTo(Vector3 destination, float speed)
    {
        if (!IsActive) return;
        _target = destination;
        _speed = Mathf.Max(0f, speed);
    }

    private void Suspend(Behaviour component)
    {
        if (component == null) return;
        _controls.Add((component, component.enabled));
        component.enabled = false;
    }

    private void FixedUpdate()
    {
        if (!IsActive) return;
        Vector3 direction = _target - _body.position;
        direction.y = 0f;
        float speed = Mathf.Min(_speed, direction.magnitude / Time.fixedDeltaTime);
        _movement.ApplyHorizontalVelocity(direction.normalized * speed);
        if (_speed > 0f && direction.sqrMagnitude > 0.001f)
            _body.MoveRotation(Quaternion.RotateTowards(_body.rotation,
                Quaternion.LookRotation(direction), 540f * Time.fixedDeltaTime));
    }

    public void StopWalking()
    {
        _speed = 0f;
        if (_body != null)
        {
            var velocity = _body.linearVelocity;
            velocity.x = velocity.z = 0f;
            _body.linearVelocity = velocity;
        }
    }

    public void End()
    {
        if (!IsActive) return;
        StopWalking();
        IsActive = false;
        if (_health != null) _health.IsInvulnerable = _wasInvulnerable;
        if (_animator != null) _animator.applyRootMotion = _usedRootMotion;
        var animation = GetComponent<PlayerAnimation>();
        if (animation != null) animation.ResetAnimation();
        _movement.ResetMovement();
        _movement.MoveInput = Vector2.zero;
        // Restore the input component last, after the receivers are ready.
        for (int i = _controls.Count - 1; i >= 0; i--)
            if (_controls[i].component != null) _controls[i].component.enabled = _controls[i].enabled;
        _controls.Clear();
    }

    private void OnDisable() => End();
}
