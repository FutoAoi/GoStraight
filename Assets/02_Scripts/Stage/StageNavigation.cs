using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

// Wait until child modifiers have registered, then build before enemy updates.
[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(NavMeshSurface))]
public class StageNavigation : MonoBehaviour
{
    private bool _needsBuild;

    private void OnEnable() => _needsBuild = true;
    private void Start() => BuildIfNeeded();
    private void Update() => BuildIfNeeded();

    private void BuildIfNeeded()
    {
        if (!_needsBuild) return;
        _needsBuild = false;
        var surface = GetComponent<NavMeshSurface>();
        surface.BuildNavMesh();
    }
}
