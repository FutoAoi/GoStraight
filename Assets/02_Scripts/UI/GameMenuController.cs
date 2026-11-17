using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Legacy name retained to preserve prefab script GUIDs and Inspector references.
// This component is now a View: render UI and publish input events only.
[DisallowMultipleComponent]
[RequireComponent(typeof(UiPresenterHost))]
public sealed class GameMenuController : MonoBehaviour, IGameMenuView
{
    [SerializeField] private bool _isTitleScene;
    [SerializeField] private string _gameScene = "IngameScenes";
    [SerializeField] private string _titleScene = "TitleScene";
    [Header("Panels")]
    [SerializeField] private GameObject _titlePanel;
    [SerializeField] private GameObject _pausePanel;
    [SerializeField] private GameObject _settingsPanel;
    [Header("Buttons")]
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _titleSettingsButton;
    [SerializeField] private Button _quitButton;
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _pauseSettingsButton;
    [SerializeField] private Button _returnTitleButton;
    [SerializeField] private Button _settingsBackButton;
    [Header("Volume sliders (0-1)")]
    [SerializeField] private Slider _masterSlider;
    [SerializeField] private Slider _bgmSlider;
    [SerializeField] private Slider _seSlider;
    public bool IsTitleScene => _isTitleScene;
    public string GameScene => _gameScene;
    public string TitleScene => _titleScene;
    public static bool IsGamePaused => UnityUiSession.IsPausedGlobally;
    public event Action StartRequested, SettingsRequested, SettingsBackRequested, ResumeRequested,
        PauseRequested, TitleRequested, QuitRequested, BackRequested;
    public event Action<float> MasterChanged, BgmChanged, SeChanged;
    private bool _initialized, _busy;
    private Selectable[] _selectables;
    private bool[] _originalInteractable;

    private void Awake() => InitializeView();
    public void InitializeView()
    {
        if (_initialized) return;
        _initialized = true;
        EnsureEventSystem();
        Bind(_startButton, StartGame);
        Bind(_titleSettingsButton, OpenSettings);
        Bind(_quitButton, Quit);
        Bind(_resumeButton, Resume);
        Bind(_pauseSettingsButton, OpenSettings);
        Bind(_returnTitleButton, ReturnToTitle);
        Bind(_settingsBackButton, CloseSettings);
        BindSlider(_masterSlider, OnMasterChanged);
        BindSlider(_bgmSlider, OnBgmChanged);
        BindSlider(_seSlider, OnSeChanged);
    }
    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) button.onClick.AddListener(action);
    }
    private static void Unbind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) button.onClick.RemoveListener(action);
    }
    private static void BindSlider(Slider slider, UnityEngine.Events.UnityAction<float> action)
    {
        if (slider == null) return;
        slider.minValue = 0f; slider.maxValue = 1f;
        slider.onValueChanged.AddListener(action);
    }
    private void Update()
    {
        if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame))
            BackRequested?.Invoke();
    }
    // Compatibility entry points also serve as the button event adapters.
    public void StartGame() => StartRequested?.Invoke();
    public void Pause() => PauseRequested?.Invoke();
    public void Resume() => ResumeRequested?.Invoke();
    public void OpenSettings() => SettingsRequested?.Invoke();
    public void CloseSettings() => SettingsBackRequested?.Invoke();
    public void ReturnToTitle() => TitleRequested?.Invoke();
    private void Quit() => QuitRequested?.Invoke();
    private void OnMasterChanged(float value) => MasterChanged?.Invoke(value);
    private void OnBgmChanged(float value) => BgmChanged?.Invoke(value);
    private void OnSeChanged(float value) => SeChanged?.Invoke(value);

    public void RenderScreen(MenuScreen screen)
    {
        if (_titlePanel != null) _titlePanel.SetActive(screen == MenuScreen.Title);
        if (_pausePanel != null) _pausePanel.SetActive(screen == MenuScreen.Pause);
        if (_settingsPanel != null) _settingsPanel.SetActive(screen == MenuScreen.Settings);
    }
    public void SetVolumes(float master, float bgm, float se)
    {
        if (_masterSlider != null) _masterSlider.SetValueWithoutNotify(master);
        if (_bgmSlider != null) _bgmSlider.SetValueWithoutNotify(bgm);
        if (_seSlider != null) _seSlider.SetValueWithoutNotify(se);
    }
    public void SetBusy(bool busy)
    {
        if (_busy == busy) return;
        _busy = busy;
        if (busy)
        {
            _selectables = GetComponentsInChildren<Selectable>(true);
            _originalInteractable = new bool[_selectables.Length];
            for (int i = 0; i < _selectables.Length; i++)
            {
                _originalInteractable[i] = _selectables[i].interactable;
                _selectables[i].interactable = false;
            }
        }
        else if (_selectables != null)
            for (int i = 0; i < _selectables.Length; i++)
                if (_selectables[i] != null) _selectables[i].interactable = _originalInteractable[i];
    }
    public void Focus(MenuFocus focus)
    {
        Selectable item = focus switch
        {
            MenuFocus.Start => _startButton,
            MenuFocus.Resume => _resumeButton,
            MenuFocus.TitleSettings => _titleSettingsButton,
            MenuFocus.PauseSettings => _pauseSettingsButton,
            MenuFocus.SettingsBack => _settingsBackButton,
            _ => null
        };
        MenuInputFocus.Request(gameObject, item);
    }
    private void EnsureEventSystem()
    {
        var system = GetComponentInChildren<EventSystem>(true);
        if (system == null) system = EventSystem.current;
        if (system == null)
            system = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
        foreach (var other in FindObjectsByType<EventSystem>())
            if (other != system && other.gameObject.scene == gameObject.scene) other.enabled = false;
        system.enabled = true;
        var legacy = system.GetComponent<StandaloneInputModule>();
        if (legacy != null) legacy.enabled = false;
        var module = system.GetComponent<InputSystemUIInputModule>();
        if (module == null) module = system.gameObject.AddComponent<InputSystemUIInputModule>();
        module.enabled = true;
        if (module.actionsAsset == null) module.AssignDefaultActions();
    }
    private void OnDestroy()
    {
        Unbind(_startButton, StartGame); Unbind(_titleSettingsButton, OpenSettings);
        Unbind(_quitButton, Quit); Unbind(_resumeButton, Resume);
        Unbind(_pauseSettingsButton, OpenSettings); Unbind(_returnTitleButton, ReturnToTitle);
        Unbind(_settingsBackButton, CloseSettings);
        if (_masterSlider != null) _masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
        if (_bgmSlider != null) _bgmSlider.onValueChanged.RemoveListener(OnBgmChanged);
        if (_seSlider != null) _seSlider.onValueChanged.RemoveListener(OnSeChanged);
    }
}
