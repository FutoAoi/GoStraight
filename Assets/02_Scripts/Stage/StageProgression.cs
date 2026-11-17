using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using VContainer;
using VContainer.Unity;

public class StageProgression : MonoBehaviour
{

    [SerializeField] private List<StageDefinition> StageDatas = new();
    [SerializeField] private Rigidbody _player;
    [Header("Stage transition")]
    [SerializeField, Min(0f)] private float _fadeOutDuration = 0.6f;
    [SerializeField, Min(0f)] private float _fadeInDuration = 0.6f;
    [Header("Entrance run (Start transform forward)")]
    [SerializeField, Min(0.1f)] private float _entranceRunSpeed = 5f;
    [SerializeField, Min(0.1f)] private float _entranceRunDistance = 6f;
    public bool IsTransitioning { get; private set; }
    private int _currentStageNumber = 0;
    private StageDefinition _currentStage;
    private PlayerPlacement _playerPlacement;
    private IObjectResolver _resolver;

    [Inject]
    public void Construct(IObjectResolver resolver)
    {
        _resolver = resolver;
    }

    public void GenerateNext()
    {
        if (IsTransitioning) return;
        // A title->game scene load already owns the fade. Build beneath that cover
        // instead of trying to start a second fade (which would reject the request).
        if (_currentStage == null && FadeManager.IsBusy && CanGenerate())
        {
            IsTransitioning = true;
            GenerateNextImmediate();
            var controller = _player.GetComponent<PlayerTransitionController>();
            if (controller == null) controller = _player.gameObject.AddComponent<PlayerTransitionController>();
            Vector3 forward = _currentStage.StartLocation.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            controller.Begin(_player.position + forward.normalized * _entranceRunDistance, _entranceRunSpeed);
            StartCoroutine(FinishSceneEntrance(controller));
            return;
        }
        TransitionToNext();
    }

    private IEnumerator FinishSceneEntrance(PlayerTransitionController controller)
    {
        try { while (FadeManager.IsBusy) yield return null; }
        finally
        {
            if (controller != null) controller.End();
            IsTransitioning = false;
        }
    }

    public bool TransitionToNext(Vector3? walkDestination = null, float walkSpeed = 5f,
        float walkBeforeFade = 0.6f)
    {
        if (IsTransitioning || !CanGenerate()) return false;
        var fade = FadeManager.Instance;
        if (fade.IsTransitioning) return false;
        bool firstStage = _currentStage == null;
        var controller = _player.GetComponent<PlayerTransitionController>();
        if (controller == null) controller = _player.gameObject.AddComponent<PlayerTransitionController>();
        IsTransitioning = true;
        controller.Begin(walkDestination ?? _player.position, walkDestination.HasValue ? walkSpeed : 0f);
        bool started = fade.Transition(() =>
        {
            if (controller != null) controller.StopWalking();
            if (this != null)
            {
                GenerateNextImmediate();
                if (controller != null)
                {
                    Vector3 forward = _currentStage.StartLocation.forward;
                    forward.y = 0f;
                    if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                    controller.MoveTo(_player.position + forward.normalized * _entranceRunDistance,
                        _entranceRunSpeed);
                }
            }
        }, firstStage ? 0f : _fadeOutDuration, _fadeInDuration,
            !firstStage && walkDestination.HasValue ? walkBeforeFade : 0f, () =>
        {
            if (controller != null) controller.End();
            if (this != null) IsTransitioning = false;
        });
        if (!started)
        {
            controller.End();
            IsTransitioning = false;
        }
        return started;
    }

    private bool CanGenerate()
    {
        bool valid = _player != null && _resolver != null && StageDatas.Count > 0 &&
            _currentStageNumber >= 0 && _currentStageNumber < StageDatas.Count &&
            StageDatas[_currentStageNumber] != null;
        if (!valid) Debug.LogError("Stage transition requires a player, a stage prefab, and an initialized container.", this);
        return valid;
    }

    private void GenerateNextImmediate()
    {
        _currentStage?.Discard();
        // 子の敵やPresenterにも、同じコンテナから依存先を注入する。
        _currentStage = _resolver.Instantiate(StageDatas[_currentStageNumber]);
        _currentStage.Initialize(this);
        // 初回のみ組み立てる。Inspectorの既存の_player参照を利用する。
        if (_playerPlacement == null)
            _playerPlacement = new PlayerPlacement(_player);

        _playerPlacement.PlaceAt(_currentStage.StartLocation);
        foreach (var levelView in FindObjectsByType<RoomLevelView>())
            if (levelView.gameObject.scene == gameObject.scene)
                levelView.Show(_currentStage.GetRoomLevel(_currentStageNumber + 1), _fadeInDuration);
        _currentStageNumber++;
        //クリア判定作る予定
        if(_currentStageNumber >= StageDatas.Count)
        {
            _currentStageNumber = StageDatas.Count - 1;
        }
    }
}
