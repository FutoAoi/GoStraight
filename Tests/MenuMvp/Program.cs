using System;

// Executes the actual production Models/Presenters. No Unity DLLs or Unity API stubs.
internal static class Program
{
    private static int _checks;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        _checks++;
    }
    private static void Main()
    {
        TestTitle();
        TestPauseAndSettings();
        TestGuardsAndLifetime();
        TestGameOver();
        TestTargetMarkers();
        Console.WriteLine($"PASS: {_checks} menu MVP checks using production code (no Unity runtime).");
    }
    private sealed class MarkerView : ITargetMarkerView
    {
        public TargetMarkerState State;
        public void Render(TargetMarkerState state) => State = state;
    }
    private static void TestTargetMarkers()
    {
        var view = new MarkerView();
        var model = new TargetMarkerModel();
        var presenter = new TargetMarkerPresenter(model, view);
        presenter.Update(false, false, true);
        Check(view.State == TargetMarkerState.Hidden, "no target hides marker");
        presenter.Update(true, false, true);
        Check(view.State == TargetMarkerState.Candidate, "available candidate shows arrow");
        presenter.Update(true, true, true);
        Check(view.State == TargetMarkerState.Locked, "locked marker replaces candidate arrow");
        presenter.Update(true, true, false);
        Check(view.State == TargetMarkerState.Hidden, "offscreen or paused hides locked marker");
        presenter.Update(true, false, true);
        Check(view.State == TargetMarkerState.Candidate, "unlock restores candidate arrow");
        presenter.Update(false, false, false);
        Check(model.State == TargetMarkerState.Hidden && view.State == model.State, "lost target clears model and view");
    }
    private static void TestTitle()
    {
        var h = new Harness(true);
        Check(h.View.Screen == MenuScreen.Title && h.View.FocusValue == MenuFocus.Start, "title initially selected");
        Check(h.Audio.TitleMusic == 1 && h.View.Master == .8f, "saved audio rendered");
        h.Presenter.Initialize();
        Check(h.Audio.TitleMusic == 1, "initialize is idempotent");
        h.View.Pause();
        Check(!h.Session.IsPaused, "title cannot pause");
        h.View.Settings();
        Check(h.Model.Screen == MenuScreen.Settings && !h.Session.IsPaused, "title settings stay unpaused");
        int applications = h.Audio.Applied;
        h.View.MasterInput(2f);
        Check(h.Settings.Master == 1f && h.Audio.MasterVolume == 1f && h.View.Master == 1f, "volume model clamps and renders");
        Check(h.Audio.Applied == applications + 1, "rendering sliders does not feed back into input");
        h.View.BgmInput(-1f); h.View.SeInput(float.NaN);
        Check(h.Audio.BgmVolume == 0f && h.Audio.SeVolume == 0f, "negative and NaN volume rejected");
        h.View.SettingsBack();
        Check(h.Model.Screen == MenuScreen.Title && h.Audio.Saves == 1, "title settings close and save");
        h.View.Start(); h.View.Start();
        Check(h.Scenes.Loads == 1 && h.Scenes.Last == "Game", "start requests exactly one load");
        Check(h.Model.IsLoading && h.View.Busy, "loading blocks UI");
        h.Presenter.Dispose();
    }
    private static void TestPauseAndSettings()
    {
        var h = new Harness(false);
        Check(h.Model.Screen == MenuScreen.Playing && h.Audio.GameMusic == 1, "game initially unpaused");
        h.View.Back();
        Check(h.Session.IsPaused && h.Model.Screen == MenuScreen.Pause, "escape pauses");
        h.View.Settings();
        Check(h.Session.IsPaused && h.Model.Screen == MenuScreen.Settings, "settings preserve pause");
        h.View.Back();
        Check(h.Session.IsPaused && h.Model.Screen == MenuScreen.Pause, "escape from settings returns to pause");
        h.View.Back();
        Check(!h.Session.IsPaused && h.Model.Screen == MenuScreen.Playing, "second escape resumes");
        int clicks = h.Audio.Clicks;
        h.View.Settings();
        Check(h.Session.IsPaused && h.Model.Screen == MenuScreen.Settings, "opening settings directly pauses");
        Check(h.Audio.Clicks == clicks + 1, "one click sound per settings operation");
        h.View.SettingsBack();
        h.Scenes.Allowed = false;
        h.View.Title();
        Check(h.Session.IsPaused && !h.View.Busy && h.Scenes.Loads == 0, "invalid scene preserves pause");
        h.Scenes.Allowed = true; h.Scenes.Accept = false;
        h.View.Title();
        Check(h.Session.IsPaused && !h.Model.IsLoading && !h.View.Busy, "rejected load restores UI without releasing pause");
        h.Scenes.Accept = true;
        h.View.Title();
        Check(!h.Session.IsPaused && h.Model.IsLoading && h.Scenes.Last == "Title", "accepted load releases pause");
        h.Presenter.Dispose();
    }
    private static void TestGuardsAndLifetime()
    {
        var h = new Harness(false);
        h.Scenes.IsBusy = true; h.View.Back(); h.View.Settings(); h.View.Start();
        Check(h.Model.Screen == MenuScreen.Playing && h.Scenes.Loads == 0, "fade blocks menu requests");
        h.Scenes.IsBusy = false; h.Session.IsGameOver = true; h.View.Back(); h.View.Settings();
        Check(h.Model.Screen == MenuScreen.Playing && !h.Session.IsPaused, "death blocks pause and settings");
        h.Session.IsGameOver = false; h.View.Pause();
        h.Presenter.Dispose();
        Check(!h.Session.IsPaused, "disposing paused presenter restores session");
        h.View.Pause(); h.View.Quit(); h.View.Start();
        Check(!h.Session.IsPaused && h.Session.Quits == 0 && h.Scenes.Loads == 0, "disposed presenter no longer receives events");
        int pauses = h.Session.Pauses;
        var replacement = new MenuPresenter(h.View, new MenuModel(false, "Game", "Title"),
            new AudioSettingsModel(), h.Scenes, h.Audio, h.Session);
        replacement.Initialize(); h.View.Back();
        Check(h.Session.Pauses == pauses + 1, "rebinding does not duplicate subscriptions");
        replacement.Dispose();
        var title = new Harness(true); title.View.Quit();
        Check(title.Session.Quits == 1 && title.Audio.Saves == 1, "quit routed through services");
        title.Presenter.Dispose();
    }
    private static void TestGameOver()
    {
        var view = new FakeGameOverView(); var model = new GameOverModel("Game", "Title");
        var scenes = new FakeScenes(); var audio = new FakeAudio(); var session = new FakeSession();
        var p = new GameOverPresenter(view, model, scenes, audio, session);
        p.Initialize(); view.Retry();
        Check(model.Phase == GameOverPhase.Hidden && scenes.Loads == 0, "hidden game-over cannot navigate");
        view.Show(); view.Show(); view.Retry();
        Check(model.Phase == GameOverPhase.Revealing && view.Reveals == 1 && scenes.Loads == 0, "reveal starts once and blocks input");
        view.CompleteReveal();
        Check(model.Phase == GameOverPhase.Ready && view.Interactable && view.FocusCount == 1, "completed reveal enables retry");
        scenes.IsBusy = true; view.Retry();
        Check(scenes.Loads == 0, "game-over respects global fade");
        scenes.IsBusy = false; scenes.Allowed = false; view.Retry();
        Check(view.Interactable && scenes.Loads == 0, "missing retry scene preserves ready UI");
        scenes.Allowed = true; scenes.Accept = false; view.Retry();
        Check(model.Phase == GameOverPhase.Ready && view.Interactable && session.Prepared == 0, "failed retry does not release frozen game-over");
        scenes.Accept = true; view.Retry(); int loads = scenes.Loads; view.Title();
        Check(model.Phase == GameOverPhase.Loading && !view.Interactable && session.Prepared == 1, "accepted retry resumes scene clock");
        Check(scenes.Loads == loads && scenes.Last == "Game", "game-over load cannot repeat");
        p.Dispose();
        view = new FakeGameOverView(); model = new GameOverModel("Game", "Title"); scenes = new FakeScenes();
        p = new GameOverPresenter(view, model, scenes, audio, session);
        p.Initialize(); view.Show(); view.CompleteReveal(); view.Title();
        Check(scenes.Last == "Title", "game-over title navigation");
        p.Dispose();
        view = new FakeGameOverView(); model = new GameOverModel("Game", "Title");
        p = new GameOverPresenter(view, model, scenes, audio, session);
        p.Initialize(); p.Dispose(); view.Show();
        Check(model.Phase == GameOverPhase.Hidden, "game-over subscriptions disposed");
    }
    private sealed class Harness
    {
        public readonly FakeMenuView View = new();
        public readonly FakeScenes Scenes = new();
        public readonly FakeAudio Audio = new();
        public readonly FakeSession Session = new();
        public readonly AudioSettingsModel Settings = new();
        public readonly MenuModel Model;
        public readonly MenuPresenter Presenter;
        public Harness(bool title)
        {
            Model = new MenuModel(title, "Game", "Title");
            Presenter = new MenuPresenter(View, Model, Settings, Scenes, Audio, Session);
            Presenter.Initialize();
        }
    }
}

internal sealed class FakeMenuView : IGameMenuView
{
    public event Action StartRequested, SettingsRequested, SettingsBackRequested, ResumeRequested,
        PauseRequested, TitleRequested, QuitRequested, BackRequested;
    public event Action<float> MasterChanged, BgmChanged, SeChanged;
    public MenuScreen Screen; public MenuFocus FocusValue; public bool Busy; public float Master;
    public void RenderScreen(MenuScreen screen) => Screen = screen;
    public void SetBusy(bool busy) => Busy = busy;
    public void SetVolumes(float master, float bgm, float se) => Master = master;
    public void Focus(MenuFocus focus) => FocusValue = focus;
    public void Start() => StartRequested?.Invoke();
    public void Settings() => SettingsRequested?.Invoke();
    public void SettingsBack() => SettingsBackRequested?.Invoke();
    public void Resume() => ResumeRequested?.Invoke();
    public void Pause() => PauseRequested?.Invoke();
    public void Title() => TitleRequested?.Invoke();
    public void Quit() => QuitRequested?.Invoke();
    public void Back() => BackRequested?.Invoke();
    public void MasterInput(float value) => MasterChanged?.Invoke(value);
    public void BgmInput(float value) => BgmChanged?.Invoke(value);
    public void SeInput(float value) => SeChanged?.Invoke(value);
}
internal sealed class FakeGameOverView : IGameOverView
{
    public event Action ShowRequested, RetryRequested, TitleRequested, RevealCompleted;
    public bool Interactable; public int Reveals, FocusCount;
    public void Hide() => Interactable = false;
    public void BeginReveal() { Reveals++; Interactable = false; }
    public void SetInteractable(bool value) => Interactable = value;
    public void FocusRetry() => FocusCount++;
    public void Show() => ShowRequested?.Invoke();
    public void Retry() => RetryRequested?.Invoke();
    public void Title() => TitleRequested?.Invoke();
    public void CompleteReveal() => RevealCompleted?.Invoke();
}
internal sealed class FakeScenes : IUiSceneLoader
{
    public bool IsBusy { get; set; }
    public bool Allowed = true, Accept = true;
    public int Loads; public string Last;
    public bool CanLoad(string scene) => Allowed;
    public bool TryLoad(string scene) { Loads++; Last = scene; return Accept; }
}
internal sealed class FakeAudio : IUiAudioService
{
    public float MasterVolume { get; private set; } = .8f;
    public float BgmVolume { get; private set; } = .6f;
    public float SeVolume { get; private set; } = .4f;
    public int Applied, Clicks, TitleMusic, GameMusic, Saves;
    public void SetVolumes(float master, float bgm, float se) { MasterVolume = master; BgmVolume = bgm; SeVolume = se; Applied++; }
    public void PlayClick() => Clicks++;
    public void PlayTitleMusic() => TitleMusic++;
    public void PlayGameMusic() => GameMusic++;
    public void SaveSettings() => Saves++;
}
internal sealed class FakeSession : IUiSession
{
    public bool IsPaused { get; private set; }
    public bool IsGameOver { get; set; }
    public int Pauses, Prepared, Quits;
    public bool Pause() { if (IsPaused) return false; IsPaused = true; Pauses++; return true; }
    public void Resume() => IsPaused = false;
    public void EnterTitle() => IsPaused = false;
    public void PrepareGameOverSceneLoad() => Prepared++;
    public void Quit() => Quits++;
}
