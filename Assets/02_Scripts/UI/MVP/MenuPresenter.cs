using System;

// Coordinates the UI. All external effects go through injected service interfaces.
public sealed class MenuPresenter : IDisposable
{
    private readonly IGameMenuView _view;
    private readonly MenuModel _model;
    private readonly AudioSettingsModel _settings;
    private readonly IUiSceneLoader _scenes;
    private readonly IUiAudioService _audio;
    private readonly IUiSession _session;
    private bool _initialized, _disposed;
    private bool Blocked => _model.IsLoading || _scenes.IsBusy || _session.IsGameOver;

    public MenuPresenter(IGameMenuView view, MenuModel model, AudioSettingsModel settings,
        IUiSceneLoader scenes, IUiAudioService audio, IUiSession session)
    {
        _view = view; _model = model; _settings = settings;
        _scenes = scenes; _audio = audio; _session = session;
        _view.StartRequested += StartGame;
        _view.SettingsRequested += OpenSettings;
        _view.SettingsBackRequested += CloseSettings;
        _view.ResumeRequested += Resume;
        _view.PauseRequested += Pause;
        _view.TitleRequested += ReturnToTitle;
        _view.QuitRequested += Quit;
        _view.BackRequested += Back;
        _view.MasterChanged += _settings.SetMaster;
        _view.BgmChanged += _settings.SetBgm;
        _view.SeChanged += _settings.SetSe;
        _model.Changed += Render;
    }
    public void Initialize()
    {
        if (_initialized || _disposed) return;
        _initialized = true;
        _settings.SetVolumes(_audio.MasterVolume, _audio.BgmVolume, _audio.SeVolume);
        _settings.Changed += ApplyAudioSettings;
        RenderVolumes();
        Render();
        if (_model.IsTitle)
        {
            _session.EnterTitle();
            _audio.PlayTitleMusic();
            _view.Focus(MenuFocus.Start);
        }
        else _audio.PlayGameMusic();
    }
    private void Render() { _view.RenderScreen(_model.Screen); _view.SetBusy(_model.IsLoading); }
    private void RenderVolumes() => _view.SetVolumes(_settings.Master, _settings.Bgm, _settings.Se);
    private void ApplyAudioSettings()
    {
        _audio.SetVolumes(_settings.Master, _settings.Bgm, _settings.Se);
        RenderVolumes();
    }
    private void Show(MenuScreen screen, MenuFocus focus)
    {
        _model.SetScreen(screen);
        _view.Focus(focus);
    }
    private void Pause()
    {
        if (Blocked || _model.IsTitle || _session.IsPaused || !_session.Pause()) return;
        Show(MenuScreen.Pause, MenuFocus.Resume);
        _audio.PlayClick();
    }
    private void Resume()
    {
        if (_model.IsLoading || !_session.IsPaused) return;
        _session.Resume();
        Show(MenuScreen.Playing, MenuFocus.None);
        _audio.PlayClick();
    }
    private void Back()
    {
        if (Blocked) return;
        if (_model.Screen == MenuScreen.Settings) { CloseSettings(); return; }
        if (_model.IsTitle) return;
        if (_session.IsPaused) Resume(); else Pause();
    }
    private void OpenSettings()
    {
        if (Blocked) return;
        if (!_model.IsTitle && !_session.IsPaused && !_session.Pause()) return;
        _settings.SetVolumes(_audio.MasterVolume, _audio.BgmVolume, _audio.SeVolume);
        Show(MenuScreen.Settings, MenuFocus.SettingsBack);
        _audio.PlayClick();
    }
    private void CloseSettings()
    {
        if (Blocked || _model.Screen != MenuScreen.Settings) return;
        _audio.SaveSettings();
        Show(_model.IsTitle ? MenuScreen.Title : MenuScreen.Pause,
            _model.IsTitle ? MenuFocus.TitleSettings : MenuFocus.PauseSettings);
        _audio.PlayClick();
    }
    private void StartGame() => Load(_model.GameScene);
    private void ReturnToTitle() => Load(_model.TitleScene);
    private void Load(string scene)
    {
        if (Blocked || !_scenes.CanLoad(scene)) return;
        _model.SetLoading(true);
        _audio.SaveSettings();
        _audio.PlayClick();
        if (_scenes.TryLoad(scene)) _session.Resume();
        else _model.SetLoading(false); // A failed request leaves pause/time/input untouched.
    }
    private void Quit()
    {
        if (Blocked) return;
        _audio.SaveSettings();
        _session.Quit();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _view.StartRequested -= StartGame;
        _view.SettingsRequested -= OpenSettings;
        _view.SettingsBackRequested -= CloseSettings;
        _view.ResumeRequested -= Resume;
        _view.PauseRequested -= Pause;
        _view.TitleRequested -= ReturnToTitle;
        _view.QuitRequested -= Quit;
        _view.BackRequested -= Back;
        _view.MasterChanged -= _settings.SetMaster;
        _view.BgmChanged -= _settings.SetBgm;
        _view.SeChanged -= _settings.SetSe;
        _model.Changed -= Render;
        _settings.Changed -= ApplyAudioSettings;
        _session.Resume();
    }
}
