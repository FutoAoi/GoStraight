using UnityEngine;
using UnityEngine.InputSystem;

// Unity-dependent adapters. Models and presenters never call Unity APIs directly.
public sealed class UnityUiSceneLoader : IUiSceneLoader
{
    public bool IsBusy => FadeManager.IsBusy;
    public bool CanLoad(string scene)
    {
        if (Application.CanStreamedLevelBeLoaded(scene)) return true;
        Debug.LogError("Add scene to Build Profile: " + scene);
        return false;
    }
    public bool TryLoad(string scene) => FadeManager.Instance.LoadScene(scene);
}

public sealed class UnityUiAudioService : IUiAudioService
{
    public float MasterVolume => SoundManager.Instance.MasterVolume;
    public float BgmVolume => SoundManager.Instance.BgmVolume;
    public float SeVolume => SoundManager.Instance.SeVolume;
    public void SetVolumes(float master, float bgm, float se)
    {
        var sound = SoundManager.Instance;
        sound.SetMasterVolume(master); sound.SetBgmVolume(bgm); sound.SetSeVolume(se);
    }
    public void PlayClick() => SoundManager.Instance.PlayUiClick();
    public void PlayTitleMusic() => SoundManager.Instance.PlayTitleMusic();
    public void PlayGameMusic() => SoundManager.Instance.PlayGameMusic();
    public void SaveSettings() => SoundManager.Instance.SaveSettings();
}

public sealed class UnityUiSession : IUiSession
{
    private static UnityUiSession _pauseOwner;
    public static bool IsPausedGlobally => _pauseOwner != null;
    public bool IsPaused => IsPausedGlobally;
    public bool IsGameOver => PlayerDeathSequence.IsGameOver;
    private PlayerInput _playerInput;
    private bool _inputWasEnabled, _audioWasPaused, _cursorWasVisible;
    private float _timeScale;
    private CursorLockMode _cursorLock;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _pauseOwner = null;

    public bool Pause()
    {
        if (_pauseOwner != null || IsGameOver) return false;
        _timeScale = Time.timeScale;
        _audioWasPaused = AudioListener.pause;
        _cursorLock = Cursor.lockState;
        _cursorWasVisible = Cursor.visible;
        _playerInput = Object.FindAnyObjectByType<PlayerInput>();
        _inputWasEnabled = _playerInput != null && _playerInput.enabled;
        if (_playerInput != null) _playerInput.enabled = false;
        _pauseOwner = this;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        return true;
    }
    public void Resume()
    {
        // Only the owner can release a pause. Disposing a different screen is harmless.
        if (_pauseOwner != this) return;
        _pauseOwner = null;
        Time.timeScale = _timeScale;
        AudioListener.pause = _audioWasPaused;
        if (_playerInput != null) _playerInput.enabled = _inputWasEnabled;
        Cursor.lockState = _cursorLock;
        Cursor.visible = _cursorWasVisible;
    }
    public void EnterTitle()
    {
        _pauseOwner?.Resume();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    public void PrepareGameOverSceneLoad()
    {
        _pauseOwner?.Resume();
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }
    public void Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        Debug.Log("Quit requested (standalone builds will close).");
#endif
    }
}
