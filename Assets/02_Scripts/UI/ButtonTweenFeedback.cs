using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public sealed class ButtonTweenFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    [SerializeField, Min(1f)] private float _selectedScale = 1.06f;
    [SerializeField, Range(.5f, 1f)] private float _pressedScale = .95f;
    [SerializeField, Min(0f)] private float _floatPixels = 3f;
    [SerializeField, Min(0f)] private float _tiltDegrees = .6f;
    [SerializeField, Min(.1f)] private float _loopDuration = 1.3f;
    [SerializeField, Min(.01f)] private float _responseDuration = .14f;
    private RectTransform _rect;
    private Button _button;
    private Vector3 _scale;
    private Vector2 _position;
    private Quaternion _rotation;
    private Tween _response, _motion;
    private bool _hovered, _selected, _pressed, _ready, _interactable;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _button = GetComponent<Button>();
    }
    private void OnEnable()
    {
        _scale = _rect.localScale;
        _position = _rect.anchoredPosition;
        _rotation = _rect.localRotation;
        _ready = true;
        _selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        _interactable = _button.IsInteractable();
        Refresh();
    }
    private void Update()
    {
        bool interactable = _button.IsInteractable();
        if (_interactable == interactable) return;
        _interactable = interactable;
        _pressed = false;
        Refresh();
    }
    private void Refresh()
    {
        if (!_ready || !isActiveAndEnabled) return;
        _response?.Kill();
        _motion?.Kill();
        _rect.anchoredPosition = _position;
        _rect.localRotation = _rotation;
        bool focus = _interactable && (_hovered || _selected);
        float scale = _interactable && _pressed ? _pressedScale : focus ? _selectedScale : 1f;
        _response = DOTween.To(() => _rect.localScale, v => _rect.localScale = v,
            _scale * scale, _responseDuration).SetEase(Ease.OutBack).SetUpdate(true);
        if (!focus || _pressed) return;
        // Animate a normalized phase: avoids accumulating drift between selections.
        _motion = DOTween.To(() => 0f, phase =>
        {
            float wave = Mathf.Sin(phase * Mathf.PI * 2f);
            _rect.anchoredPosition = _position + Vector2.up * (wave * _floatPixels);
            _rect.localRotation = _rotation * Quaternion.Euler(0f, 0f, wave * _tiltDegrees);
        }, 1f, _loopDuration).SetEase(Ease.Linear).SetLoops(-1).SetUpdate(true);
    }
    public void OnPointerEnter(PointerEventData e) { _hovered = true; Refresh(); }
    public void OnPointerExit(PointerEventData e) { _hovered = false; _pressed = false; Refresh(); }
    public void OnSelect(BaseEventData e) { _selected = true; Refresh(); }
    public void OnDeselect(BaseEventData e) { _selected = false; _pressed = false; Refresh(); }
    public void OnPointerDown(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        _pressed = true; Refresh();
    }
    public void OnPointerUp(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        _pressed = false; Refresh();
    }
    public void OnSubmit(BaseEventData e)
    {
        if (!_button.IsInteractable()) return;
        _pressed = true; Refresh();
        _response.OnComplete(() => { _pressed = false; Refresh(); });
    }
    private void OnDisable()
    {
        _response?.Kill(); _motion?.Kill();
        if (_ready)
        {
            _rect.localScale = _scale;
            _rect.anchoredPosition = _position;
            _rect.localRotation = _rotation;
        }
        _hovered = _selected = _pressed = _ready = false;
    }
}
