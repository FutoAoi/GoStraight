using System;

public sealed class AudioSettingsModel
{
    public float Master { get; private set; }
    public float Bgm { get; private set; }
    public float Se { get; private set; }
    public event Action Changed;
    public void SetVolumes(float master, float bgm, float se)
    {
        master = Clamp(master); bgm = Clamp(bgm); se = Clamp(se);
        if (Master == master && Bgm == bgm && Se == se) return;
        Master = master; Bgm = bgm; Se = se;
        Changed?.Invoke();
    }
    public void SetMaster(float value) => SetVolumes(value, Bgm, Se);
    public void SetBgm(float value) => SetVolumes(Master, value, Se);
    public void SetSe(float value) => SetVolumes(Master, Bgm, value);
    private static float Clamp(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, Math.Min(1f, value));
}
