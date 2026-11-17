using System;

public enum MenuScreen { Playing, Title, Pause, Settings }
public enum MenuFocus { None, Start, Resume, TitleSettings, PauseSettings, SettingsBack }

// State only: no Unity objects, input polling, audio or scene loading.
public sealed class MenuModel
{
    public bool IsTitle { get; }
    public string GameScene { get; }
    public string TitleScene { get; }
    public MenuScreen Screen { get; private set; }
    public bool IsLoading { get; private set; }
    public event Action Changed;
    public MenuModel(bool isTitle, string gameScene, string titleScene)
    {
        IsTitle = isTitle;
        GameScene = gameScene;
        TitleScene = titleScene;
        Screen = isTitle ? MenuScreen.Title : MenuScreen.Playing;
    }
    public void SetScreen(MenuScreen screen)
    {
        if (Screen == screen) return;
        Screen = screen;
        Changed?.Invoke();
    }
    public void SetLoading(bool loading)
    {
        if (IsLoading == loading) return;
        IsLoading = loading;
        Changed?.Invoke();
    }
}
