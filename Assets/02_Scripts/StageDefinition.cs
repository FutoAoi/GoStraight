using UnityEngine;

public class StageDefinition : MonoBehaviour
{
    [SerializeField] private Transform _startLocation;
    [SerializeField] private StageGoal _goal;
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
