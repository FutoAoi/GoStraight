using UnityEngine;

[DisallowMultipleComponent]
public sealed class SoundManager : MonoBehaviour
{
    private static SoundManager _instance;
    public static SoundManager Instance
    {
        get
        {
            if (_instance == null) _instance = FindAnyObjectByType<SoundManager>();
            if (_instance == null) _instance = new GameObject("SoundManager").AddComponent<SoundManager>();
            return _instance;
        }
    }
    [Header("Assign audio clips in the Inspector")]
    [SerializeField] private AudioClip _titleBgm;
    [SerializeField] private AudioClip _gameBgm;
    [SerializeField] private AudioClip _uiClick;
    [SerializeField] private AudioClip _swordSwing;
    [SerializeField] private AudioClip _playerHit;
    [Header("Defaults (saved settings take priority)")]
    [SerializeField, Range(0f, 1f)] private float _masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float _bgmVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float _seVolume = 0.8f;
    private AudioSource _bgm, _se;
    public float MasterVolume => _masterVolume;
    public float BgmVolume => _bgmVolume;
    public float SeVolume => _seVolume;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _instance = null;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        _bgm = gameObject.AddComponent<AudioSource>();
        _bgm.playOnAwake = false;
        _bgm.loop = true;
        _bgm.spatialBlend = 0f;
        _se = gameObject.AddComponent<AudioSource>();
        _se.playOnAwake = false;
        _se.spatialBlend = 0f;
        _se.ignoreListenerPause = true;
        SetMasterVolume(PlayerPrefs.GetFloat("Audio.Master", _masterVolume));
        SetBgmVolume(PlayerPrefs.GetFloat("Audio.BGM", _bgmVolume));
        SetSeVolume(PlayerPrefs.GetFloat("Audio.SE", _seVolume));
    }
    public void SetMasterVolume(float value) { _masterVolume = Mathf.Clamp01(value); ApplyVolumes(); PlayerPrefs.SetFloat("Audio.Master", _masterVolume); }
    public void SetBgmVolume(float value) { _bgmVolume = Mathf.Clamp01(value); ApplyVolumes(); PlayerPrefs.SetFloat("Audio.BGM", _bgmVolume); }
    public void SetSeVolume(float value) { _seVolume = Mathf.Clamp01(value); ApplyVolumes(); PlayerPrefs.SetFloat("Audio.SE", _seVolume); }
    private void ApplyVolumes()
    {
        if (_bgm != null) _bgm.volume = _masterVolume * _bgmVolume;
        if (_se != null) _se.volume = _masterVolume * _seVolume;
    }
    public void PlayBgm(AudioClip clip)
    {
        if (clip == null) { _bgm.Stop(); _bgm.clip = null; return; }
        if (_bgm.clip == clip && _bgm.isPlaying) return;
        _bgm.clip = clip;
        _bgm.Play();
    }
    public void PlaySe(AudioClip clip, float volume = 1f)
    {
        if (clip != null) _se.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
    public void PlayTitleMusic() => PlayBgm(_titleBgm);
    public void PlayGameMusic() => PlayBgm(_gameBgm);
    public void StopBgm() => _bgm.Stop();
    public void PauseBgm() => _bgm.Pause();
    public void ResumeBgm() => _bgm.UnPause();
    public void PlayUiClick() => PlaySe(_uiClick);
    public void PlaySwordSwing() => PlaySe(_swordSwing);
    public void PlayPlayerHit() => PlaySe(_playerHit);
    public void SaveSettings() => PlayerPrefs.Save();
    private void OnApplicationPause(bool paused) { if (paused) SaveSettings(); }
    private void OnApplicationQuit() => SaveSettings();
    private void OnDestroy() { if (_instance == this) _instance = null; }
}
