using UnityEngine;

// Composition root for both title and game scenes (including scenes without VContainer).
// Presenter dependencies are provided through ordinary C# constructor injection.
[DisallowMultipleComponent]
public sealed class UiPresenterHost : MonoBehaviour
{
    private MenuPresenter _menu;
    private GameOverPresenter _gameOver;
    private bool _started;
    private void Awake()
    {
        // Game-over Show can be requested before Start by other components.
        CreateGameOverPresenter();
    }
    private void Start()
    {
        _started = true;
        CreateMenuPresenter();
    }
    private void OnEnable()
    {
        if (!_started) return;
        CreateMenuPresenter();
        CreateGameOverPresenter();
    }
    private void CreateMenuPresenter()
    {
        if (_menu != null || !TryGetComponent<GameMenuController>(out var view)) return;
        view.InitializeView();
        _menu = new MenuPresenter(view, new MenuModel(view.IsTitleScene, view.GameScene, view.TitleScene),
            new AudioSettingsModel(), new UnityUiSceneLoader(), new UnityUiAudioService(), new UnityUiSession());
        _menu.Initialize();
    }
    private void CreateGameOverPresenter()
    {
        if (_gameOver != null || !TryGetComponent<GameOverView>(out var view)) return;
        view.InitializeView();
        _gameOver = new GameOverPresenter(view, new GameOverModel(view.RetryScene, view.TitleScene),
            new UnityUiSceneLoader(), new UnityUiAudioService(), new UnityUiSession());
        _gameOver.Initialize();
    }
    private void OnDisable()
    {
        _menu?.Dispose(); _menu = null;
        _gameOver?.Dispose(); _gameOver = null;
    }
}
