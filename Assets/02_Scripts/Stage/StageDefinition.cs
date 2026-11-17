using UnityEngine;

public class StageDefinition : MonoBehaviour
{
    [SerializeField] private Transform _startLocation;
    [SerializeField] private StageGoal _goal;
    [Tooltip("0 uses the stage list number. Set a positive value to specify this room's level.")]
    [SerializeField, Min(0)] private int _roomLevel;
    public int GetRoomLevel(int stageNumber) => _roomLevel > 0 ? _roomLevel : stageNumber;
    public Transform StartLocation => _startLocation;

    public void Initialize(StageProgression stageProgression)
    {
        _goal.Initialize(stageProgression);
    }
    public void Discard()
    {
        gameObject.SetActive(false);
        Destroy(this.gameObject);
    }
}
