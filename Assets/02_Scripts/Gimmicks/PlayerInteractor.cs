using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class PlayerInteractor : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float _range = 2f;
    [SerializeField, Range(0f, 180f)] private float _viewHalfAngle = 80f;
    [SerializeField] private LayerMask _targetLayers = ~0;
    [SerializeField] private bool _showPrompt = true;
    private bool _interactionRequested;
    private PlayerCombat _combat;
    private PlayerMovement _movement;
    private IInteractable _target;
    public IInteractable CurrentTarget => _target;

    private void Awake()
    {
        _combat = GetComponent<PlayerCombat>();
        _movement = GetComponent<PlayerMovement>();
    }

    public void OnInteract(InputValue value)
    {
        if (isActiveAndEnabled && value.isPressed) _interactionRequested = true;
    }

    private void OnDisable() { _interactionRequested = false; _target = null; }

    private void Update()
    {
        bool requested = _interactionRequested;
        _interactionRequested = false;
        _target = null;
        if (Time.timeScale <= 0f || (_combat != null && (_combat.IsAttacking || _combat.IsHitStopped)) ||
            (_movement != null && _movement.IsKnockedBack)) return;
        Vector3 origin = transform.position + Vector3.up * 0.75f;
        float nearest = float.PositiveInfinity;
        foreach (var collider in Physics.OverlapSphere(origin, _range, _targetLayers, QueryTriggerInteraction.Collide))
        {
            var candidate = collider.GetComponentInParent<IInteractable>();
            if (candidate == null || !candidate.CanInteract(transform) || !(candidate is Component component)) continue;
            Vector3 point = collider.ClosestPoint(origin);
            Vector3 direction = point - origin;
            Vector3 horizontal = new Vector3(direction.x, 0f, direction.z);
            if (horizontal.sqrMagnitude > 0.001f && Vector3.Angle(transform.forward, horizontal) > _viewHalfAngle) continue;
            if (direction.sqrMagnitude >= nearest || IsBlocked(origin, point, component.transform)) continue;
            nearest = direction.sqrMagnitude;
            _target = candidate;
        }
        if (_target != null && requested) _target.Interact(transform);
    }

    private bool IsBlocked(Vector3 origin, Vector3 point, Transform target)
    {
        Vector3 direction = point - origin;
        foreach (var hit in Physics.RaycastAll(origin, direction.normalized, direction.magnitude,
            ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(target)) continue;
            return true;
        }
        return false;
    }

    private void OnGUI()
    {
        if (!_showPrompt || _target == null) return;
        GUI.Box(new Rect(Screen.width * 0.5f - 140f, Screen.height - 80f, 280f, 34f),
            "F / Y : " + _target.InteractionLabel);
    }
}
