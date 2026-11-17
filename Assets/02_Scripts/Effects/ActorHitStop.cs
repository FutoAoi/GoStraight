using UnityEngine;
using UnityEngine.AI;

// Pauses one actor only. Camera, UI, effects and the global physics clock keep running.
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class ActorHitStop : MonoBehaviour
{
    private Animator[] _animators;
    private float[] _animationSpeeds;
    private Rigidbody _body;
    private Vector3 _velocity, _angularVelocity;
    private RigidbodyConstraints _constraints;
    private bool _frozeBody;
    private NavMeshAgent _agent;
    private bool _stopped, _updatePosition, _updateRotation, _frozeAgent;
    private float _endsAt, _startedAt, _pausedTime;

    public bool IsStopped { get; private set; }
    // Use this clock for actor timers so combo buffers and attack delays also pause.
    public float LocalTime => Time.time - _pausedTime - (IsStopped ? Time.time - _startedAt : 0f);

    public static ActorHitStop GetOrAdd(GameObject actor)
    {
        return actor.TryGetComponent<ActorHitStop>(out var stop) ? stop : actor.AddComponent<ActorHitStop>();
    }

    public void Stop(float duration)
    {
        if (!isActiveAndEnabled || duration <= 0f) return;
        float end = Time.unscaledTime + duration;
        if (IsStopped)
        {
            _endsAt = Mathf.Max(_endsAt, end);
            return;
        }
        _startedAt = Time.time;
        _endsAt = end;
        IsStopped = true;
        _animators = GetComponentsInChildren<Animator>(true);
        _animationSpeeds = new float[_animators.Length];
        for (int i = 0; i < _animators.Length; i++)
        {
            _animationSpeeds[i] = _animators[i].speed;
            _animators[i].speed = 0f;
        }
        _body = GetComponent<Rigidbody>();
        _frozeBody = _body != null && !_body.isKinematic;
        if (_frozeBody)
        {
            _velocity = _body.linearVelocity;
            _angularVelocity = _body.angularVelocity;
            _constraints = _body.constraints;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.constraints = RigidbodyConstraints.FreezeAll;
        }
        _agent = GetComponent<NavMeshAgent>();
        _frozeAgent = _agent != null && _agent.enabled && _agent.isOnNavMesh;
        if (_frozeAgent)
        {
            _stopped = _agent.isStopped;
            _updatePosition = _agent.updatePosition;
            _updateRotation = _agent.updateRotation;
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
            _agent.updatePosition = false;
            _agent.updateRotation = false;
        }
    }

    private void Update()
    {
        if (IsStopped && Time.unscaledTime >= _endsAt) Cancel();
    }

    public void Cancel()
    {
        if (!IsStopped) return;
        _pausedTime += Time.time - _startedAt;
        IsStopped = false;
        for (int i = 0; i < _animators.Length; i++)
            if (_animators[i] != null) _animators[i].speed = _animationSpeeds[i];
        if (_frozeBody && _body != null)
        {
            _body.constraints = _constraints;
            if (!_body.isKinematic)
            {
                _body.linearVelocity = _velocity;
                _body.angularVelocity = _angularVelocity;
            }
        }
        if (_frozeAgent && _agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.nextPosition = transform.position;
            _agent.updatePosition = _updatePosition;
            _agent.updateRotation = _updateRotation;
            _agent.isStopped = _stopped;
        }
        _frozeBody = _frozeAgent = false;
    }

    private void OnDisable() => Cancel();
}
