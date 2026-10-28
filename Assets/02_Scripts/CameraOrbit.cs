using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class CameraOrbit : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private InputActionReference _lookAction;
    [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 1.5f, 0f); // 注視点(頭の高さ)
    [SerializeField] private float _distance = 6f;
    [Header("カメラ感度")]
    [FormerlySerializedAs("_sensitivity")]
    [Tooltip("マウス移動量に対する回転量。以前の感度設定を引き継ぐ。")]
    [SerializeField, Min(0f)] private float _mouseSensitivity = 0.1f;
    [Tooltip("右スティックを最大まで倒したときの、1秒あたりの回転角度。")]
    [SerializeField, Min(0f)] private float _gamepadSensitivity = 180f;
    [SerializeField] private float _minPitch = -20f;
    [SerializeField] private float _maxPitch = 60f;
    [SerializeField] private PlayerLockOn _lockOn;
    [SerializeField] private float _lockOnTurnSpeed = 360f;

    float _yaw;
    float _pitch = 20f;

    private void OnEnable() => _lookAction.action.Enable();
    private void OnDisable() => _lookAction.action.Disable();

    private void Start()
    {
        _yaw = transform.eulerAngles.y;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void LateUpdate()
    {
        if (_target == null) return;

        Transform lockTarget = _lockOn != null ? _lockOn.CurrentTarget : null;

        if (_lockOn != null && _lockOn.IsFocusing)
        {
            Vector3 direction = lockTarget != null
                ? lockTarget.position - _target.position
                : _lockOn.FocusDirection;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                _yaw = Mathf.MoveTowardsAngle( _yaw, targetYaw, _lockOnTurnSpeed * Time.deltaTime);
            }
        }
        else
        {
            InputAction lookAction = _lookAction.action;
            Vector2 look = lookAction.ReadValue<Vector2>();

            // スティックは倒している量なので、時間を掛けて回転量にする。
            // マウスは既に移動量なので、時間は掛けない。
            float sensitivity = lookAction.activeControl?.device is Gamepad
                ? _gamepadSensitivity * Time.deltaTime
                : _mouseSensitivity;

            _yaw += look.x * sensitivity;
            _pitch = Mathf.Clamp(_pitch - look.y * sensitivity, _minPitch, _maxPitch);
        }

        var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        var pivot = _target.position + _targetOffset;

        transform.rotation = rotation;
        transform.position = pivot - rotation * Vector3.forward * _distance;
    }
}
