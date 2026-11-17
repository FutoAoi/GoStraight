public interface IUiSceneLoader
{
    bool IsBusy { get; }
    bool CanLoad(string scene);
    bool TryLoad(string scene);
}

public interface IUiAudioService
{
    float MasterVolume { get; }
    float BgmVolume { get; }
    float SeVolume { get; }
    void SetVolumes(float master, float bgm, float se);
    void PlayClick();
    void PlayTitleMusic();
    void PlayGameMusic();
    void SaveSettings();
}

public interface IUiSession
{
    bool IsPaused { get; }
    bool IsGameOver { get; }
    bool Pause();
    void Resume();
    void EnterTitle();
    void PrepareGameOverSceneLoad();
    void Quit();
}
