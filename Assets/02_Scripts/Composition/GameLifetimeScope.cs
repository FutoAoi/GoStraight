using UnityEngine;
using VContainer;
using VContainer.Unity;

public class GameLifetimeScope : LifetimeScope
{
    [SerializeField] private StageProgression _stageProgression;
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponent(_stageProgression);
        builder.RegisterEntryPoint<GameStartup>();
    }
}
