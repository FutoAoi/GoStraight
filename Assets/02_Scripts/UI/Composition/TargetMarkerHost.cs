using UnityEngine;

[RequireComponent(typeof(TargetMarkerView))]
public sealed class TargetMarkerHost : MonoBehaviour
{
    [SerializeField] private PlayerLockOn _source;
    [SerializeField] private Camera _camera;
    private TargetMarkerView _view;
    private void Awake() => _view = GetComponent<TargetMarkerView>();
    private void LateUpdate()
    {
        if (_source == null) _source = FindAnyObjectByType<PlayerLockOn>();
        if (_camera == null) _camera = Camera.main;
        bool active = _source != null && _source.isActiveAndEnabled &&
            !GameMenuController.IsGamePaused && !PlayerDeathSequence.IsGameOver;
        if (active) _view.RenderTargets(_camera, _source.Candidates, _source.LockedTarget);
        else _view.HideAll();
    }
    private void OnDisable()
    {
        if (_view != null) _view.HideAll();
    }
}