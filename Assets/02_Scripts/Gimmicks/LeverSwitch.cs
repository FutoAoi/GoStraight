using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class LeverSwitch : GimmickSignal, IInteractable
{
    [SerializeField] private string _interactionLabel = "Operate lever";
    [SerializeField] private bool _oneShot;
    [SerializeField] private Transform _handlePivot;
    [SerializeField] private Vector3 _activeRotation = new(60f, 0f, 0f);
    [SerializeField, Min(1f)] private float _rotationSpeed = 180f;
    private Quaternion _restRotation;
    public string InteractionLabel => _interactionLabel;

    private void Awake()
    {
        if (_handlePivot != null) _restRotation = _handlePivot.localRotation;
    }

    public bool CanInteract(Transform actor) => isActiveAndEnabled && (!_oneShot || !IsActive);

    public void Interact(Transform actor)
    {
        if (!CanInteract(actor)) return;
        if (_oneShot) Activate();
        else Toggle();
    }

    private void Update()
    {
        if (_handlePivot == null) return;
        Quaternion target = _restRotation * (IsActive ? Quaternion.Euler(_activeRotation) : Quaternion.identity);
        _handlePivot.localRotation = Quaternion.RotateTowards(_handlePivot.localRotation, target, _rotationSpeed * Time.deltaTime);
    }
}
