using UnityEngine;
using VContainer.Unity;

public class GameStartup : IStartable
{
    private readonly StageProgression _stageProgression;

    public GameStartup(StageProgression stageProgression)
    {
        _stageProgression = stageProgression;
    }

    public void Start()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        _stageProgression.GenerateNext();
    }
}
