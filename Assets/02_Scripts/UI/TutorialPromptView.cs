using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public sealed class TutorialPromptView : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TMP_Text _instruction;
    [SerializeField] private TMP_Text _button;
    [SerializeField, Min(.01f)] private float _fadeDuration = .15f;
    private bool _gamepad;
    private bool _visible;
    private Tween _fade;

    public void Configure(CanvasGroup group, TMP_Text instruction, TMP_Text button)
    {
        _group = group; _instruction = instruction; _button = button;
    }
    private void Awake()
    {
        if (_group == null) return;
        _group.alpha = 0;
        _group.interactable = false;
        _group.blocksRaycasts = false;
    }
    private void OnEnable() => InputSystem.onActionChange += OnAction;
    private void OnAction(object source, InputActionChange change)
    {
        if (change != InputActionChange.ActionPerformed || source is not InputAction action) return;
        var device = action.activeControl?.device;
        if (device is Gamepad) _gamepad = true;
        else if (device is Keyboard || device is Mouse) _gamepad = false;
    }
    private void Update()
    {
        var next = TutorialArea.Current;
        bool show = next != null && !GameMenuController.IsGamePaused && !PlayerDeathSequence.IsGameOver;
        if (_instruction != null && next != null) _instruction.text = next.Instruction;
        if (_button != null && next != null) _button.text = next.ButtonLabel(_gamepad);
        if (show == _visible || _group == null) return;
        _visible = show;
        _fade?.Kill();
        _fade = DOTween.To(() => _group.alpha, value => _group.alpha = value,
            show ? 1f : 0f, _fadeDuration).SetUpdate(true);
    }
    private void OnDisable()
    {
        InputSystem.onActionChange -= OnAction;
        _fade?.Kill();
        _visible = false;
        if (_group != null) _group.alpha = 0;
    }
}
