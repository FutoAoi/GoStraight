using System;
using UnityEngine;
using UnityEngine.Events;

// Sources expose state without knowing what they operate.
public abstract class GimmickSignal : MonoBehaviour
{
    [SerializeField] private bool _isActive;
    [SerializeField] private UnityEvent<bool> _onStateChanged = new();
    public bool IsActive => _isActive;
    public UnityEvent<bool> OnStateChanged => _onStateChanged;
    public event Action<bool> StateChanged;

    protected virtual void Start() => Publish();

    public void SetState(bool active)
    {
        if (_isActive == active) return;
        _isActive = active;
        Publish();
    }

    public void Activate() => SetState(true);
    public void Deactivate() => SetState(false);
    public void Toggle() => SetState(!_isActive);

    private void Publish()
    {
        StateChanged?.Invoke(_isActive);
        _onStateChanged.Invoke(_isActive);
    }
}
