using System;

public interface IGameMenuView
{
    event Action StartRequested;
    event Action SettingsRequested;
    event Action SettingsBackRequested;
    event Action ResumeRequested;
    event Action PauseRequested;
    event Action TitleRequested;
    event Action QuitRequested;
    event Action BackRequested;
    event Action<float> MasterChanged;
    event Action<float> BgmChanged;
    event Action<float> SeChanged;
    void RenderScreen(MenuScreen screen);
    void SetBusy(bool busy);
    void SetVolumes(float master, float bgm, float se);
    void Focus(MenuFocus focus);
}

public interface IGameOverView
{
    event Action ShowRequested;
    event Action RetryRequested;
    event Action TitleRequested;
    event Action RevealCompleted;
    void Hide();
    void BeginReveal();
    void SetInteractable(bool interactable);
    void FocusRetry();
}
