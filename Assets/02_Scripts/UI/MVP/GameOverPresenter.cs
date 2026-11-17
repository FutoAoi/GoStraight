using System;

public sealed class GameOverPresenter : IDisposable
{
    private readonly IGameOverView _view;
    private readonly GameOverModel _model;
    private readonly IUiSceneLoader _scenes;
    private readonly IUiAudioService _audio;
    private readonly IUiSession _session;
    private bool _disposed;
    public GameOverPresenter(IGameOverView view, GameOverModel model, IUiSceneLoader scenes,
        IUiAudioService audio, IUiSession session)
    {
        _view = view; _model = model; _scenes = scenes; _audio = audio; _session = session;
        _view.ShowRequested += Show;
        _view.RevealCompleted += Revealed;
        _view.RetryRequested += Retry;
        _view.TitleRequested += ReturnToTitle;
    }
    public void Initialize() { _model.SetPhase(GameOverPhase.Hidden); _view.Hide(); }
    private void Show()
    {
        if (_model.Phase != GameOverPhase.Hidden) return;
        _model.SetPhase(GameOverPhase.Revealing);
        _view.BeginReveal();
    }
    private void Revealed()
    {
        if (_model.Phase != GameOverPhase.Revealing) return;
        _model.SetPhase(GameOverPhase.Ready);
        _view.SetInteractable(true);
        _view.FocusRetry();
    }
    private void Retry() => Load(_model.RetryScene);
    private void ReturnToTitle() => Load(_model.TitleScene);
    private void Load(string scene)
    {
        if (_model.Phase != GameOverPhase.Ready || _scenes.IsBusy || !_scenes.CanLoad(scene)) return;
        _model.SetPhase(GameOverPhase.Loading);
        _view.SetInteractable(false);
        _audio.SaveSettings();
        _audio.PlayClick();
        if (_scenes.TryLoad(scene)) _session.PrepareGameOverSceneLoad();
        else
        {
            _model.SetPhase(GameOverPhase.Ready);
            _view.SetInteractable(true);
        }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _view.ShowRequested -= Show;
        _view.RevealCompleted -= Revealed;
        _view.RetryRequested -= Retry;
        _view.TitleRequested -= ReturnToTitle;
    }
}
