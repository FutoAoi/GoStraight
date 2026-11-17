public enum GameOverPhase { Hidden, Revealing, Ready, Loading }

public sealed class GameOverModel
{
    public string RetryScene { get; }
    public string TitleScene { get; }
    public GameOverPhase Phase { get; private set; }
    public GameOverModel(string retryScene, string titleScene)
    {
        RetryScene = retryScene;
        TitleScene = titleScene;
    }
    public void SetPhase(GameOverPhase phase) => Phase = phase;
}
