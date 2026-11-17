using DG.Tweening;
using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public sealed class RoomLevelView : MonoBehaviour
{
    [SerializeField] private TMP_Text _label;
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private string _labelFormat = "Level {0}";
    [SerializeField, Min(0f)] private float _holdDuration = 1.5f;
    [SerializeField, Min(0f)] private float _fadeDuration = 0.4f;
    [Header("DOTween motion")]
    [SerializeField] private Vector2 _enterOffset = new(0f, -24f);
    [SerializeField] private Vector2 _exitOffset = new(0f, 18f);
    private Sequence _sequence;
    private Vector2 _restPosition;
    private bool _hasPosition;

    private void Awake()
    {
        CapturePosition();
        if (_group != null) _group.alpha = 0f;
    }

    private void CapturePosition()
    {
        if (_hasPosition || _label == null) return;
        _restPosition = _label.rectTransform.anchoredPosition;
        _hasPosition = true;
    }

    public void Show(int level, float entranceFadeDuration)
    {
        if (_label == null || _group == null) return;
        CapturePosition();
        _sequence?.Kill();
        // Replace a literal token so arbitrary Inspector text cannot cause format exceptions.
        _label.text = _labelFormat.Replace("{0}", level.ToString());
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
        var rect = _label.rectTransform;
        rect.anchoredPosition = _restPosition + _enterOffset;
        float duration = Mathf.Max(0f, _fadeDuration);
        _sequence = DOTween.Sequence().SetUpdate(true);
        _sequence.AppendInterval(Mathf.Max(0f, entranceFadeDuration));
        _sequence.Append(DOTween.To(() => _group.alpha, a => _group.alpha = a, 1f, duration));
        _sequence.Join(DOTween.To(() => rect.anchoredPosition, p => rect.anchoredPosition = p,
            _restPosition, duration).SetEase(Ease.OutCubic));
        _sequence.AppendInterval(Mathf.Max(0f, _holdDuration));
        _sequence.Append(DOTween.To(() => _group.alpha, a => _group.alpha = a, 0f, duration));
        _sequence.Join(DOTween.To(() => rect.anchoredPosition, p => rect.anchoredPosition = p,
            _restPosition + _exitOffset, duration).SetEase(Ease.InCubic));
        _sequence.OnComplete(() => rect.anchoredPosition = _restPosition);
    }

    private void OnDisable()
    {
        _sequence?.Kill();
        _sequence = null;
        if (_hasPosition && _label != null) _label.rectTransform.anchoredPosition = _restPosition;
        if (_group != null) _group.alpha = 0f;
    }
}
