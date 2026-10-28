using UnityEngine;
using System.Collections.Generic;

public class StageProgression : MonoBehaviour
{

    [SerializeField] private List<StageDefinition> StageDatas = new();
    [SerializeField] private Rigidbody _player;
    private int _currentStageNumber = 0;
    private StageDefinition _currentStage;
    private PlayerPlacement _playerPlacement;

    public void GenerateNext()
    {
        _currentStage?.Discard();
        _currentStage = Instantiate(StageDatas[_currentStageNumber]);
        _currentStage.Initialize(this);
        // 初回のみ組み立てる。Inspectorの既存の_player参照を利用する。
        if (_playerPlacement == null)
            _playerPlacement = new PlayerPlacement(_player);

        _playerPlacement.PlaceAt(_currentStage.StartLocation);
        _currentStageNumber++;
        //クリア判定作る予定
        if(_currentStageNumber >= StageDatas.Count)
        {
            _currentStageNumber = StageDatas.Count - 1;
        }
    }
}
