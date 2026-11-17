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
    [Header("Wall avoidance")]
    [SerializeField] private LayerMask _obstacleLayers = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0.01f)] private float _collisionRadius = 0.25f;
    [SerializeField, Min(0f)] private float _wallPadding = 0.05f;
    [SerializeField, Min(0.01f)] private float _returnSmoothTime = 0.25f;
    [SerializeField] private bool _protectNearClipPlane = true;

    private readonly RaycastHit[] _wallHits = new RaycastHit[32];
    private readonly Collider[] _overlaps = new Collider[32];
    private Camera _camera;
    private float _currentDistance;
    private float _distanceVelocity;
    private bool _distanceInitialized;
    private float _shakeStarted;
    private float _shakeDuration;
    private float _shakeStrength;
    private bool _deathShot;
    private float _deathBlend, _deathDistance, _deathHeight, _deathPitch;

    public void BeginDeathShot(float distance, float targetHeight, float pitch)
    {
        _deathShot = true;
        _deathBlend = 0f;
        _deathDistance = Mathf.Max(0.5f, distance);
        _deathHeight = targetHeight;
        _deathPitch = pitch;
    }

    public void Shake(float duration, float strength)
    {
        if (duration <= 0f || strength <= 0f) return;
        _shakeStarted = Time.unscaledTime;
        _shakeDuration = duration;
        _shakeStrength = strength;
    }

    float _yaw;
    float _pitch = 20f;

    private void OnEnable()
    {
        _lookAction.action.Enable();
        _distanceInitialized = false;
        _distanceVelocity = 0f;
    }
    private void OnDisable() => _lookAction.action.Disable();

    private void Start()
    {
        _camera = GetComponent<Camera>();
        _yaw = transform.eulerAngles.y;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void LateUpdate()
    {
        if (_target == null || GameMenuController.IsGamePaused) return;

        Transform lockTarget = _lockOn != null ? _lockOn.CurrentTarget : null;

        if (_deathShot)
        {
            _deathBlend = Mathf.MoveTowards(_deathBlend, 1f, Time.unscaledDeltaTime * 0.8f);
            _yaw = Mathf.MoveTowardsAngle(_yaw, _target.eulerAngles.y + 155f, 75f * Time.unscaledDeltaTime);
            _pitch = Mathf.MoveTowards(_pitch, _deathPitch, 35f * Time.unscaledDeltaTime);
        }
        else if (_lockOn != null && _lockOn.IsFocusing)
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
        else if (!FadeManager.IsBusy)
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
        var pivotOffset = _deathShot ? Vector3.Lerp(_targetOffset, Vector3.up * _deathHeight, _deathBlend) : _targetOffset;
        var pivot = _target.position + pivotOffset;

        transform.rotation = rotation;
        Vector3 backward = -(rotation * Vector3.forward);
        float radius = GetCollisionRadius();
        float desiredDistance = _deathShot ? Mathf.Lerp(_distance, _deathDistance, _deathBlend) : _distance;
        float safeDistance = FindSafeDistance(pivot, backward, Mathf.Max(0f, desiredDistance), radius);

        // Move inward immediately so smoothing can never leave the camera inside a wall.
        if (!_distanceInitialized || safeDistance < _currentDistance)
        {
            _currentDistance = safeDistance;
            _distanceVelocity = 0f;
            _distanceInitialized = true;
        }
        else if (Time.deltaTime > 0f)
        {
            _currentDistance = Mathf.Min(safeDistance, Mathf.SmoothDamp(_currentDistance,
                safeDistance, ref _distanceVelocity, _returnSmoothTime));
        }
        transform.position = pivot + backward * _currentDistance;
        float shakeElapsed = Time.unscaledTime - _shakeStarted;
        if (_shakeDuration > 0f && shakeElapsed < _shakeDuration)
        {
            float amount = _shakeStrength * (1f - shakeElapsed / _shakeDuration);
            float phase = shakeElapsed * 30f;
            Vector3 offset = rotation * new Vector3(
                (Mathf.PerlinNoise(phase, 7.3f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(13.7f, phase) - 0.5f) * 2f, 0f) * amount;
            Vector3 ray = transform.position + offset - pivot;
            // Apply shaking before the final collision check so it cannot push into a wall.
            if (ray.sqrMagnitude > 0.0001f)
                transform.position = pivot + ray.normalized *
                    FindSafeDistance(pivot, ray.normalized, ray.magnitude, radius);
        }
    }

    private float GetCollisionRadius()
    {
        float radius = Mathf.Max(0.01f, _collisionRadius);
        if (!_protectNearClipPlane || _camera == null) return radius;
        float halfHeight = _camera.orthographic ? _camera.orthographicSize :
            _camera.nearClipPlane * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float halfWidth = halfHeight * _camera.aspect;
        // Include the near-plane corners, not only the camera's center point.
        return Mathf.Max(radius, Mathf.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight +
            _camera.nearClipPlane * _camera.nearClipPlane));
    }

    private bool IsObstacle(Collider collider)
    {
        if (collider == null || collider.isTrigger || collider.transform.IsChildOf(transform)) return false;
        if (collider.transform.IsChildOf(_target)) return false;
        // All gameplay objects currently share Default, so exclude characters explicitly.
        if (collider.GetComponentInParent<PlayerMovement>() != null ||
            collider.GetComponentInParent<EnemyMovement>() != null) return false;
        return true;
    }

    private float FindSafeDistance(Vector3 pivot, Vector3 direction, float distance, float radius)
    {
        if (distance <= 0f) return 0f;
        float safe = distance;
        int count = Physics.SphereCastNonAlloc(pivot, radius, direction, _wallHits,
            distance, _obstacleLayers, QueryTriggerInteraction.Ignore);
        RaycastHit[] hits = _wallHits;
        if (count == _wallHits.Length)
        {
            hits = Physics.SphereCastAll(pivot, radius, direction, distance,
                _obstacleLayers, QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }
        for (int i = 0; i < count; i++)
            if (IsObstacle(hits[i].collider)) safe = Mathf.Min(safe, Mathf.Max(0f, hits[i].distance - _wallPadding));

        // A sphere cast can miss an initial overlap. Also check the center ray and final position.
        count = Physics.RaycastNonAlloc(pivot, direction, _wallHits, distance,
            _obstacleLayers, QueryTriggerInteraction.Ignore);
        hits = _wallHits;
        if (count == _wallHits.Length)
        {
            hits = Physics.RaycastAll(pivot, direction, distance, _obstacleLayers, QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }
        for (int i = 0; i < count; i++)
            if (IsObstacle(hits[i].collider)) safe = Mathf.Min(safe, Mathf.Max(0f, hits[i].distance - radius - _wallPadding));

        if (OverlapsWall(pivot + direction * safe, radius))
        {
            float lower = 0f, upper = safe;
            for (int i = 0; i < 8; i++)
            {
                float middle = (lower + upper) * 0.5f;
                if (OverlapsWall(pivot + direction * middle, radius)) upper = middle;
                else lower = middle;
            }
            safe = Mathf.Max(0f, lower - _wallPadding);
        }
        return safe;
    }

    private bool OverlapsWall(Vector3 position, float radius)
    {
        int count = Physics.OverlapSphereNonAlloc(position, radius, _overlaps,
            _obstacleLayers, QueryTriggerInteraction.Ignore);
        Collider[] colliders = _overlaps;
        if (count == _overlaps.Length)
        {
            colliders = Physics.OverlapSphere(position, radius, _obstacleLayers, QueryTriggerInteraction.Ignore);
            count = colliders.Length;
        }
        for (int i = 0; i < count; i++) if (IsObstacle(colliders[i])) return true;
        return false;
    }
}
