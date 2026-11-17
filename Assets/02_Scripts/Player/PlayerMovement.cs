using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour, IKnockbackReceiver
{
    public Vector2 MoveInput;
    public Vector3 MoveDirection { get; private set; }

    [Header("移動のステータス設定")]
    [SerializeField] private float _maxSpeed = 5f;
    [SerializeField] private float _accelerationTime = 0.5f;
    [Tooltip("最大速度から停止するまでの時間（秒）。")]
    [SerializeField, Min(0.01f)] private float _decelerationTime = 0.25f;
    [SerializeField] private Transform _camera;
    [SerializeField] private PlayerAnimation _playerAnimation;
    [SerializeField] private AnimationCurve _accelerationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);


    private float _currentSpeed;
    private float _accelerationProgress;
    private Rigidbody _rigidbody;
    private PlayerCombat _combat;
    private Vector3 _knockbackDirection;
    private float _knockbackDistance, _knockbackDuration, _knockbackElapsed;
    public bool IsKnockedBack { get; private set; }
    private PhysicsMaterial _movementMaterial;
    private readonly Dictionary<Collider, List<Vector3>> _wallNormals = new();

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _combat = GetComponent<PlayerCombat>();
        if (_playerAnimation == null) _playerAnimation = GetComponent<PlayerAnimation>();
        // 壁へ押し付けた際の摩擦で、横移動や落下が止まらないようにする。
        _movementMaterial = new PhysicsMaterial("PlayerFrictionless")
        {
            staticFriction = 0f,
            dynamicFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        foreach (var shape in GetComponents<Collider>())
            if (!shape.isTrigger) shape.sharedMaterial = _movementMaterial;
    }

    private void Start()
    {
        if (_camera == null && Camera.main != null) _camera = Camera.main.transform;
        if (_playerAnimation != null) _playerAnimation.SetMaxMovementSpeed(_maxSpeed);
    }

    /// <summary>
    /// 動きの初期化
    /// </summary>
    public void ResetMovement()
    {
        IsKnockedBack = false;
        // 入力は保持し、押し続けている場合も停止状態から再加速する。
        _currentSpeed = 0f;
        _accelerationProgress = 0f;
        MoveDirection = Vector3.zero;
        _wallNormals.Clear();
    }

    public void OnMove(InputValue value)
    {
       MoveInput = value.Get<Vector2>().normalized;
    }

    private void FixedUpdate()
    {
        if (_combat != null && _combat.IsHitStopped) return;
        if (IsKnockedBack)
        {
            float before = _knockbackElapsed / _knockbackDuration;
            _knockbackElapsed = Mathf.Min(_knockbackElapsed + Time.fixedDeltaTime, _knockbackDuration);
            float after = _knockbackElapsed / _knockbackDuration;
            float distance = _knockbackDistance * ((2f * after - after * after) - (2f * before - before * before));
            ApplyHorizontalVelocity(_knockbackDirection * (distance / Time.fixedDeltaTime));
            if (_knockbackElapsed >= _knockbackDuration) IsKnockedBack = false;
            return;
        }
        if (_playerAnimation != null && _playerAnimation.IsTakingDamage)
        {
            ApplyHorizontalVelocity(Vector3.zero);
            return;
        }
        if (_combat != null && _combat.BlocksMovement) return;
        bool hasInput = MoveInput.sqrMagnitude > 0f;

        if (hasInput)
        {
            var forward = _camera != null ? _camera.forward : Vector3.forward;
            forward.y = 0f;
            forward.Normalize();
            var right = _camera != null ? _camera.right : Vector3.right;
            right.y = 0f;
            right.Normalize();
            MoveDirection = forward * MoveInput.y + right * MoveInput.x;

            _accelerationProgress = Mathf.Clamp01(_accelerationProgress + Time.fixedDeltaTime / Mathf.Max(_accelerationTime, 0.01f));
        }
        else
        {
            // カメラが回っても、入力を離す直前のワールド方向へ減速する。
            _accelerationProgress = Mathf.MoveTowards(
                _accelerationProgress, 0f,
                Time.fixedDeltaTime / Mathf.Max(_decelerationTime, 0.01f));
        }

        // 同じカーブを逆にたどるので、減速中に再入力しても速度が連続する。
        float speedRatio = Mathf.Clamp01(_accelerationCurve.Evaluate(_accelerationProgress));
        _currentSpeed = _accelerationProgress > 0f ? _maxSpeed * speedRatio : 0f;
        if (_currentSpeed <= 0f) MoveDirection = Vector3.zero;

        var velocity = MoveDirection * _currentSpeed;
        ApplyHorizontalVelocity(velocity);
    }

    // 通常移動と攻撃の踏み込みが、同じ壁滑り処理を使う。
    public void ApplyHorizontalVelocity(Vector3 velocity)
    {
        if (_combat != null && _combat.IsHitStopped) return;
        velocity.y = 0f;
        // 角で複数の壁に触れている場合も、各壁への押し込みを抑える。
        for (int pass = 0; pass < 3; pass++)
        {
            foreach (var pair in _wallNormals)
            {
                if (pair.Key == null || !pair.Key.enabled || !pair.Key.gameObject.activeInHierarchy) continue;
                foreach (Vector3 normal in pair.Value)
                {
                    float intoWall = Vector3.Dot(velocity, normal);
                    if (intoWall < 0f) velocity -= normal * intoWall;
                }
            }
        }
        // 重力・ジャンプの速度には干渉しない。
        velocity.y = _rigidbody.linearVelocity.y;
        _rigidbody.linearVelocity = velocity;
    }

    private void OnCollisionEnter(Collision collision) => UpdateWallNormals(collision);
    private void OnCollisionStay(Collision collision) => UpdateWallNormals(collision);

    private void OnCollisionExit(Collision collision)
    {
        _wallNormals.Remove(collision.collider);
    }

    private void UpdateWallNormals(Collision collision)
    {
        // A pushable body must not be treated as an immovable wall.
        if (collision.rigidbody != null &&
            collision.rigidbody.TryGetComponent<PushableBox>(out var box) && box.CanPush)
        {
            _wallNormals.Remove(collision.collider);
            return;
        }
        if (!_wallNormals.TryGetValue(collision.collider, out var normals))
        {
            normals = new List<Vector3>();
            _wallNormals.Add(collision.collider, normals);
        }
        normals.Clear();
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            // 床や坂・天井を壁として扱わない。
            if (Mathf.Abs(normal.y) > 0.5f) continue;
            normal.y = 0f;
            normals.Add(normal.normalized);
        }
    }

    public void ApplyKnockback(Vector3 direction, float distance, float duration)
    {
        if (TryGetComponent<PlayerTransitionController>(out var transition) && transition.IsActive) return;
        if (!isActiveAndEnabled || distance <= 0f) return;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        if (_combat != null) _combat.ResetCombat();
        var jump = GetComponent<PlayerJamp>();
        if (jump != null) jump.ResetJump();
        ResetMovement();
        _knockbackDirection = direction.normalized;
        _knockbackDistance = distance;
        _knockbackDuration = Mathf.Max(0.01f, duration);
        _knockbackElapsed = 0f;
        IsKnockedBack = true;
    }

    private void OnDisable()
    {
        IsKnockedBack = false;
        _wallNormals.Clear();
    }

    private void OnDestroy()
    {
        if (_movementMaterial != null) Destroy(_movementMaterial);
    }

}
