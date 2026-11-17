using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Scene-owned UI. Only appearance changes at runtime; layout and sprites stay editable.
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class PlayerHealthView : MonoBehaviour
{
    [Header("Scene UI references (left to right)")]
    [SerializeField] private RectTransform _row;
    [SerializeField] private List<Image> _squares = new();
    [SerializeField] private Health _health;
    [Header("Appearance")]
    [SerializeField] private Color _filledColor = new(0.95f, 0.15f, 0.18f, 1f);
    [SerializeField] private Color _emptyColor = new(0.22f, 0.22f, 0.25f, 1f);
    [SerializeField, Min(1f)] private float _nextDamageScale = 1.5f;
    [SerializeField, Min(0)] private int _previewHealth = 5;
    [Header("Damage feedback")]
    [SerializeField, Min(0.01f)] private float _feedbackDuration = 0.4f;
    [SerializeField, Min(0f)] private float _shakePixels = 10f;
    [SerializeField, Min(0.01f)] private float _blinkInterval = 0.07f;
    private float _feedbackStarted;
    private bool _feedbackActive;
    private int _currentHealth;
    private Vector2 _restPosition;
    private bool _hasRestPosition;
    public Health HealthSource => _health;
    public bool HasUI => _row != null && _squares.Count > 0;

    private void Awake() => CapturePosition();
    private void CapturePosition()
    {
        if (_row == null) return;
        _restPosition = _row.anchoredPosition;
        _hasRestPosition = true;
    }
    public void Bind(Health health)
    {
        Unsubscribe();
        ResetFeedback();
        _health = health;
        if (_health == null) return;
        if (!_hasRestPosition) CapturePosition();
        if (isActiveAndEnabled && Application.isPlaying) Subscribe();
        Refresh(_health.CurrentHealth, _health.MaxHealth);
    }
    private void Subscribe()
    {
        Unsubscribe();
        if (_health == null) return;
        _health.HealthChanged += Refresh;
        _health.Damaged += OnDamaged;
    }
    private void Unsubscribe()
    {
        if (_health == null) return;
        _health.HealthChanged -= Refresh;
        _health.Damaged -= OnDamaged;
    }
    private void Refresh(int current, int maximum)
    {
        _currentHealth = current;
        for (int i = 0; i < _squares.Count; i++)
        {
            if (_squares[i] == null) continue;
            _squares[i].rectTransform.localScale = Vector3.one *
                (current > 0 && i == current - 1 ? Mathf.Max(1f, _nextDamageScale) : 1f);
            _squares[i].color = i < current ? _filledColor : _emptyColor;
        }
    }
    private void OnDamaged(int damage)
    {
        if (!_feedbackActive) CapturePosition();
        _feedbackStarted = Time.unscaledTime;
        _feedbackActive = true;
    }
    private void Update()
    {
        if (!Application.isPlaying || !_feedbackActive || _row == null) return;
        float elapsed = Time.unscaledTime - _feedbackStarted;
        if (elapsed >= Mathf.Max(0.01f, _feedbackDuration)) { ResetFeedback(); return; }
        float strength = _shakePixels * (1f - elapsed / Mathf.Max(0.01f, _feedbackDuration));
        _row.anchoredPosition = _restPosition +
            new Vector2(Mathf.Sin(elapsed * 110f), Mathf.Sin(elapsed * 87f) * 0.5f) * strength;
        bool red = Mathf.FloorToInt(elapsed / Mathf.Max(0.01f, _blinkInterval)) % 2 == 0;
        for (int i = 0; i < _squares.Count; i++)
            if (_squares[i] != null)
                _squares[i].color = red ? Color.red :
                    (i < _currentHealth ? new Color(1f, 0.8f, 0.8f) : _emptyColor);
    }
    private void ResetFeedback()
    {
        _feedbackActive = false;
        if (_row != null && _hasRestPosition && Application.isPlaying)
            _row.anchoredPosition = _restPosition;
        Refresh(_currentHealth, _squares.Count);
    }
    private void OnEnable()
    {
        CapturePosition();
        if (!Application.isPlaying) { Refresh(_previewHealth, _squares.Count); return; }
        if (_health == null) return;
        Subscribe();
        Refresh(_health.CurrentHealth, _health.MaxHealth);
    }
    private void OnValidate()
    {
#if UNITY_EDITOR
        // Inspector validation can occur while Unity is loading objects. Update UI afterwards.
        UnityEditor.EditorApplication.delayCall -= PreviewInEditor;
        UnityEditor.EditorApplication.delayCall += PreviewInEditor;
#endif
    }
#if UNITY_EDITOR
    private void PreviewInEditor()
    {
        if (this != null && !Application.isPlaying) Refresh(_previewHealth, _squares.Count);
    }
#endif
    private void OnDisable()
    {
        Unsubscribe();
        if (Application.isPlaying) ResetFeedback();
    }
}
