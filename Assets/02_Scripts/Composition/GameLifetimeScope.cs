using UnityEngine;
using VContainer;
using VContainer.Unity;

public class GameLifetimeScope : LifetimeScope
{
    [SerializeField] private StageProgression _stageProgression;
    protected override void Configure(IContainerBuilder builder)
    {
        // ステージ・プレイヤーの外に、ゲームシーン共通のプールを1つ生成する。
        builder.RegisterComponentOnNewGameObject<ParticleEffectPool>(
            Lifetime.Scoped, "ParticleEffectPool").UnderTransform(transform);

        if (_stageProgression != null)
        {
            builder.RegisterComponent(_stageProgression);
            builder.RegisterEntryPoint<GameStartup>();
        }

        // シーンに配置済みのPresenterへ注入。非アクティブな敵も含める。
        // 複数の同型Presenterを個別登録せず、完成したコンテナから注入する。
        builder.RegisterBuildCallback(resolver =>
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var presenter in root.GetComponentsInChildren<AttackEffectPresenter>(true))
                    resolver.Inject(presenter);
            }
        });

    }
}
