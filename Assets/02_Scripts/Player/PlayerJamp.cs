using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerJamp : MonoBehaviour
{
    [Header("ジャンプ設定")]
    [SerializeField, Min(0f)] private float _jumpSpeed = 7f;
    [SerializeField] private LayerMask _groundLayers = ~0;
    [SerializeField, Range(0f, 89f)] private float _maxGroundAngle = 50f;
    [Tooltip("接触が一瞬途切れた際に、落下アニメーションへ切り替えるまでの猶予。")]
    [SerializeField, Min(0f)] private float _animationGroundGraceTime = 0.08f;
    [Tooltip("Jumpクリップで足が地面を離れる位置。0.25は再生全体の25%。")]
    [SerializeField, Range(0f, 0.9f)] private float _takeoffNormalizedTime = 0.25f;
    [Tooltip("踏み込み部分だけのアニメーション再生倍率。離陸後は1倍に戻す。")]
    [SerializeField, Min(1f)] private float _preparationAnimationSpeed = 3f;
    public bool IsPreparingJump { get; private set; }
    private float _prepareStartedTime;

    private readonly HashSet<Collider> _groundContacts = new HashSet<Collider>();
    private Rigidbody _body;
    private bool _jumpRequested;
    private PlayerAnimation _animation;
    private PlayerCombat _combat;
    private PlayerMovement _movement;
    private float _lastGroundedTime;
    private bool _jumping;

    public bool IsGrounded => _groundContacts.Count > 0 && _body.linearVelocity.y <= 0.1f;

    private void Awake()
    {
        _body = GetComponent<Rigidbody>();
        _animation = GetComponent<PlayerAnimation>();
        _combat = GetComponent<PlayerCombat>();
        _movement = GetComponent<PlayerMovement>();
        _lastGroundedTime = Time.time;
    }

    private void OnEnable()
    {
        // Allow physics to report floor contact before changing the visible air state.
        // This does not grant a jump: IsGrounded still requires an actual contact.
        _lastGroundedTime = Time.time;
    }

    // PlayerInput の Send Messages から、Jump アクションで呼ばれる。
    public void OnJump(InputValue value)
    {
        if (_animation != null && _animation.IsTakingDamage) return;
        if (_movement != null && _movement.IsKnockedBack) return;
        if (isActiveAndEnabled && value.isPressed && Time.timeScale > 0f)
            _jumpRequested = true;
    }

    private void FixedUpdate()
    {
        if (_combat != null && _combat.IsHitStopped) return;
        if (IsGrounded)
        {
            _lastGroundedTime = Time.time;
            _jumping = false;
        }
        if (!IsGrounded && _combat != null && _combat.IsAttacking)
            _combat.ResetCombat();

        if (_jumpRequested && !IsPreparingJump && IsGrounded && (_combat == null || !_combat.BlocksMovement))
        {
            IsPreparingJump = true;
            _prepareStartedTime = Time.time;
            _animation.SetJumpPreparing(true, _preparationAnimationSpeed);
            _animation.PlayJump();
        }
        _jumpRequested = false;

        if (IsPreparingJump)
        {
            bool hasJumpState = _animation.TryGetJumpProgress(out float progress);
            if (!IsGrounded || (!hasJumpState && Time.time - _prepareStartedTime > 1f))
            {
                // 踏み外した場合やAnimatorの設定不備では、ジャンプ予約を破棄する。
                IsPreparingJump = false;
                _animation.CancelJumpRequest();
                _animation.SetJumpPreparing(false);
            }
            else if (hasJumpState && progress >= _takeoffNormalizedTime)
            {
                // 踏み込みを地上で再生してから、実際の上向き速度を与える。
                Vector3 velocity = _body.linearVelocity;
                velocity.y = _jumpSpeed;
                _body.linearVelocity = velocity;
                _groundContacts.Clear();
                _jumping = true;
                IsPreparingJump = false;
                _animation.SetJumpPreparing(false);
            }
        }
        // 判定の猶予は見た目だけに使用。空中で再ジャンプは許可しない。
        bool animationGrounded = IsGrounded ||
            (!_jumping && Time.time - _lastGroundedTime <= _animationGroundGraceTime);
        _animation.SetAirState(animationGrounded, _body.linearVelocity.y);
    }

    public void ResetJump()
    {
        IsPreparingJump = false;
        _animation.CancelJumpRequest();
        _animation.SetJumpPreparing(false);
        _jumpRequested = false;
        _groundContacts.Clear();
        _jumping = false;
        _lastGroundedTime = Time.time;
    }

    private void OnCollisionEnter(Collision collision)
    {
        UpdateGroundContact(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        UpdateGroundContact(collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        _groundContacts.Remove(collision.collider);
    }

    private void UpdateGroundContact(Collision collision)
    {
        _groundContacts.Remove(collision.collider);
        // Unity sends collision callbacks to disabled behaviours as well.
        // Keep floor contacts while doorway transitions temporarily suspend jumping.
        if (_body == null || _body.linearVelocity.y > 0.1f) return;
        if ((_groundLayers.value & (1 << collision.gameObject.layer)) == 0) return;

        float minGroundNormalY = Mathf.Cos(_maxGroundAngle * Mathf.Deg2Rad);
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y < minGroundNormalY) continue;
            _groundContacts.Add(collision.collider);
            return;
        }
    }

    private void OnDisable()
    {
        ResetJump();
    }
}
