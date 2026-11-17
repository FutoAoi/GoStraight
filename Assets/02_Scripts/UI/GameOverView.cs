using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Display and input only. Navigation and loading state belong to GameOverPresenter.
[DisallowMultipleComponent]
[RequireComponent(typeof(UiPresenterHost))]
public sealed class GameOverView : MonoBehaviour, IGameOverView
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private Button _retryButton;
    [SerializeField] private Button _titleButton;
    [SerializeField] private string _retryScene = "IngameScenes";
    [SerializeField] private string _titleScene = "TitleScene";
    [SerializeField, Min(0f)] private float _fadeDuration = 0.6f;
    public string RetryScene => _retryScene;
    public string TitleScene => _titleScene;
    public event Action ShowRequested, RetryRequested, TitleRequested, RevealCompleted;
    private bool _initialized;
    private Tween _reveal;
    private void Awake() => InitializeView();
    public void InitializeView()
    {
        if (_initialized) return;
        _initialized = true;
        if (_retryButton != null) _retryButton.onClick.AddListener(Retry);
        if (_titleButton != null) _titleButton.onClick.AddListener(ReturnToTitle);
        Hide();
    }
    public void Show() => ShowRequested?.Invoke();
    public void Retry() => RetryRequested?.Invoke();
    public void ReturnToTitle() => TitleRequested?.Invoke();
    public void Hide()
    {
        _reveal?.Kill();
        _reveal = null;
        if (_group != null) { _group.alpha = 0f; _group.blocksRaycasts = false; _group.interactable = false; }
        if (_panel != null) _panel.SetActive(false);
    }
    public void BeginReveal()
    {
        if (_panel == null || _group == null) return;
        _reveal?.Kill();
        _panel.SetActive(true);
        _group.blocksRaycasts = true;
        _group.interactable = false;
        _group.alpha = 0f;
        _reveal = DOTween.To(() => _group.alpha, a => _group.alpha = a, 1f,
            Mathf.Max(0f, _fadeDuration)).SetEase(Ease.OutSine).SetUpdate(true)
            .OnComplete(() => RevealCompleted?.Invoke());
    }
    public void SetInteractable(bool interactable)
    {
        if (_group != null) _group.interactable = interactable;
    }
    public void FocusRetry()
    {
        MenuInputFocus.Request(gameObject, _retryButton);
    }
    private void OnDisable() => Hide();
    private void OnDestroy()
    {
        if (_retryButton != null) _retryButton.onClick.RemoveListener(Retry);
        if (_titleButton != null) _titleButton.onClick.RemoveListener(ReturnToTitle);
    }
}
