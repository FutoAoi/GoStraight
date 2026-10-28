using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
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

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _combat = GetComponent<PlayerCombat>();
    }

    private void Start()
    {
        if (_camera == null) _camera = Camera.main.transform;
        _playerAnimation.SetMaxMovementSpeed(_maxSpeed);
    }

    /// <summary>
    /// 動きの初期化
    /// </summary>
    public void ResetMovement()
    {
        // 入力は保持し、押し続けている場合も停止状態から再加速する。
        _currentSpeed = 0f;
        _accelerationProgress = 0f;
        MoveDirection = Vector3.zero;
    }

    public void OnMove(InputValue value)
    {
       MoveInput = value.Get<Vector2>().normalized;
    }

    private void FixedUpdate()
    {
        if (_combat.BlocksMovement) return;
        bool hasInput = MoveInput.sqrMagnitude > 0f;

        if (hasInput)
        {
            var forward = _camera.forward;
            forward.y = 0f;
            forward.Normalize();
            var right = _camera.right;
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
        velocity.y = _rigidbody.linearVelocity.y;
        _rigidbody.linearVelocity = velocity;
    }


}
