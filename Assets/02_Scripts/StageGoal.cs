using UnityEngine;

public class StageGoal : MonoBehaviour
{
    private StageProgression _stageProgression;
    private bool _reached;

    public void Initialize(StageProgression stageProgression)
    {
        _stageProgression = stageProgression;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_reached) return;
        if (!other.CompareTag("Player")) return;

        _reached = true;
        _stageProgression.GenerateNext();
    }
}