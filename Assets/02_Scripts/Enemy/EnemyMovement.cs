using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent), typeof(Health))]
public class EnemyMovement : MonoBehaviour, IKnockbackReceiver
{
    public enum State { Wander, Chase, Knockback, Attack, ChargeWindup, Charge }
    [Header("Wandering area (spawn point when unset)")]
    [SerializeField] private Transform _areaCenter;
    [SerializeField, Min(0.5f)] private float _wanderRadius = 8f;
    [SerializeField, Min(0f)] private float _wanderWait = 1.5f;
    [SerializeField, Min(0.1f)] private float _wanderSpeed = 1.5f;
    [Header("Detection and pursuit")]
    [SerializeField] private Transform _player;
    [SerializeField, Min(0.1f)] private float _detectionRadius = 6f;
    [SerializeField, Min(0.1f)] private float _loseRadius = 10f;
    [SerializeField, Range(0f, 360f)] private float _viewAngle = 180f;
    [SerializeField] private LayerMask _obstacleLayers;
    [SerializeField, Min(0.1f)] private float _chaseSpeed = 3.5f;
    [SerializeField, Min(0f)] private float _stopDistance = 1.5f;
    [Header("Close range attack")]
    [SerializeField, Min(0.1f)] private float _attackRange = 2f;
    [SerializeField, Min(0f)] private float _attackDelay = 1f;
    [SerializeField, Min(0.1f)] private float _attackCooldown = 1.5f;
    [SerializeField, Min(0f)] private float _attackRecovery = 0.4f;
    [Tooltip("When the attack hits, relative to the Attack animation (0 = start, 1 = end).")]
    [SerializeField, Range(0f, 1f)] private float _attackHitNormalizedTime = 0.5f;
    [SerializeField, Range(0f, 180f)] private float _attackHitHalfAngle = 70f;
    [SerializeField, Min(0)] private int _attackDamage = 1;
    [SerializeField, Min(0f)] private float _attackKnockbackDistance = 3f;
    [SerializeField, Min(0.01f)] private float _attackKnockbackDuration = 0.3f;
    [Header("Attack variations (enable independently)")]
    [SerializeField] private bool _enableChargePunch;
    [SerializeField] private bool _enableDoublePunch;
    [SerializeField, Min(0.1f)] private float _chargeMinRange = 3.5f;
    [SerializeField, Min(0.1f)] private float _chargeMaxRange = 7f;
    [SerializeField, Min(0f)] private float _chargeWindup = 0.65f;
    [SerializeField, Min(0.1f)] private float _chargeSpeed = 8f;
    [SerializeField, Min(0.1f)] private float _chargeDistance = 6f;
    [SerializeField, Min(0.1f)] private float _chargeCooldown = 4f;
    [SerializeField, Min(0f)] private float _doublePunchGap = 0.35f;
    [SerializeField, Min(0f)] private float _doublePunchAdvanceSpeed = 6f;
    [SerializeField, Min(0f)] private float _doublePunchAdvanceDistance = 2.5f;
    [Header("Knockback multiplier (0 = immune)")]
    [SerializeField, Min(0f)] private float _knockbackMultiplier = 1f;
    public State CurrentState { get; private set; }
    private NavMeshAgent _agent;
    private Health _health;
    private Vector3 _spawn;
    private Vector3 _knockbackDirection;
    private float _knockbackDistance, _knockbackDuration, _knockbackElapsed;
    private float _waitUntil, _nextPathUpdate, _nextPlayerSearch, _nextNavMeshRetry;
    private NavMeshPath _path;
    private float _closeTime, _nextAttackTime, _recoveryUntil;
    private bool _attackLanded;
    private int _punchesRemaining;
    private bool _waitingSecondPunch;
    private float _secondPunchAt, _comboAdvanceRemaining;
    private float _chargeUntil, _chargeRemaining, _nextChargeTime;
    private Vector3 _chargeDirection;
    private bool _attackAnimationStarted;
    private float _attackAnimationStartTime;
    private Animator _animator;
    private int _animationState;
    private float _hitAnimationUntil;
    private ActorHitStop _hitStop;
    private float ActionTime => _hitStop != null ? _hitStop.LocalTime : Time.time;
    private bool Ready => _agent.enabled && _agent.isOnNavMesh;

    private void Awake()
    {
        _hitStop = ActorHitStop.GetOrAdd(gameObject);
        _path = new NavMeshPath();
        _agent = GetComponent<NavMeshAgent>();
        _health = GetComponent<Health>();
        if (!TryGetComponent<EnemyDamageFlash>(out _)) gameObject.AddComponent<EnemyDamageFlash>();
        _spawn = transform.position;
    }

    private void Start()
    {
        _animator = GetComponentInChildren<Animator>(true);
        if (_animator == null)
        {
            var model = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (model != null)
            {
                Transform root = model.transform;
                while (root.parent != null && root.parent != transform) root = root.parent;
                _animator = root.gameObject.AddComponent<Animator>();
            }
        }
        if (_animator != null)
        {
            if (_animator.runtimeAnimatorController == null)
                _animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("EnemyAnimation");
            _animator.applyRootMotion = false;
            _animator.enabled = true;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (_animator.runtimeAnimatorController == null)
                Debug.LogError("EnemyAnimation controller missing. Assign an Animator Controller to the enemy model or restore Resources/EnemyAnimation.controller.", this);
        }
        else Debug.LogError("Enemy model has no Animator or skinned mesh. Check the enemy's Model child.", this);
        if (!Ready && NavMesh.SamplePosition(transform.position, out var hit, 3f, _agent.areaMask))
            _agent.Warp(hit.position);
        ChangeState(State.Wander);
    }

    private void LateUpdate()
    {
        if (GameMenuController.IsGamePaused) return;
        if (_hitStop.IsStopped) return;
        if (_health.IsDead || _animator == null || _animator.runtimeAnimatorController == null) return;
        string state = ActionTime < _hitAnimationUntil || CurrentState == State.Knockback ? "Hit" :
            CurrentState == State.Charge ? "Run" :
            CurrentState == State.Attack && _attackAnimationStarted ? "Attack" :
            Ready && _agent.velocity.sqrMagnitude > 0.01f ? "Walk" : "Idle";
        int hash = Animator.StringToHash("Base Layer." + state);
        if (state == "Run" && !_animator.HasState(0, hash)) hash = Animator.StringToHash("Base Layer.Walk");
        if (_animationState == hash || !_animator.HasState(0, hash)) return;
        _animationState = hash;
        _animator.CrossFadeInFixedTime(hash, 0.1f, 0, 0f);
    }

    private void Update()
    {
        if (GameMenuController.IsGamePaused) return;
        if (_hitStop.IsStopped) return;
        if (_health.IsDead || !_agent.enabled) return;
        if (!Ready)
        {
            if (ActionTime < _nextNavMeshRetry) return;
            _nextNavMeshRetry = ActionTime + 0.5f;
            if (!NavMesh.SamplePosition(transform.position, out var ground, 3f, _agent.areaMask) ||
                !_agent.Warp(ground.position)) return;
            ChangeState(State.Wander);
        }
        if (CurrentState == State.Knockback) { TickKnockback(); return; }
        if (CurrentState == State.ChargeWindup || CurrentState == State.Charge) { TickCharge(); return; }
        if (CurrentState == State.Attack) { TickAttack(); return; }
        if (_player == null && ActionTime >= _nextPlayerSearch)
        {
            _nextPlayerSearch = ActionTime + 1f;
            var player = FindAnyObjectByType<PlayerCombat>();
            if (player != null) _player = player.transform;
        }
        bool visible = CanSeePlayer(CurrentState == State.Chase ? _loseRadius : _detectionRadius);
        if (CurrentState == State.Wander && visible) ChangeState(State.Chase);
        else if (CurrentState == State.Chase && !visible) ChangeState(State.Wander);
        if (CurrentState == State.Chase)
        {
            if (_enableChargePunch && ActionTime >= _nextAttackTime && ActionTime >= _nextChargeTime &&
                ActionTime >= _hitAnimationUntil && CanSeePlayer(_chargeMaxRange) &&
                Vector3.Distance(transform.position, _player.position) >= Mathf.Max(_chargeMinRange, _attackRange + 0.1f))
            {
                ChangeState(State.ChargeWindup);
                _chargeUntil = ActionTime + _chargeWindup;
                _nextChargeTime = ActionTime + _chargeCooldown;
                return;
            }
            if (IsPlayerInAttackRange() && ActionTime >= _nextAttackTime)
            {
                ChangeState(State.Attack);
                return;
            }
            if (ActionTime >= _nextPathUpdate)
            {
                _nextPathUpdate = ActionTime + 0.2f;
                _agent.SetDestination(_player.position);
            }
        }
        else TickWander();
    }

    private bool CanSeePlayer(float radius)
    {
        if (_player == null || !_player.gameObject.activeInHierarchy) return false;
        Vector3 offset = _player.position - transform.position;
        if (offset.sqrMagnitude > radius * radius) return false;
        if (CurrentState == State.Wander && Vector3.Angle(transform.forward, offset) > _viewAngle * 0.5f) return false;
        Vector3 eye = transform.position + Vector3.up * 0.5f;
        return !Physics.Linecast(eye, _player.position + Vector3.up * 0.5f,
            _obstacleLayers, QueryTriggerInteraction.Ignore);
    }

    private void ChangeState(State next)
    {
        if (Ready) _agent.ResetPath();
        CurrentState = next;
        _closeTime = 0f;
        _attackLanded = false;
        _attackAnimationStarted = false;
        _waitingSecondPunch = false;
        _punchesRemaining = next == State.Attack && _enableDoublePunch ? 2 : 1;
        if (!Ready) return;
        bool controlled = next == State.Knockback || next == State.Attack || next == State.ChargeWindup || next == State.Charge;
        _agent.isStopped = controlled;
        _agent.updateRotation = !controlled;
        _agent.speed = next == State.Chase ? _chaseSpeed : _wanderSpeed;
        _agent.stoppingDistance = next == State.Chase ? Mathf.Min(_stopDistance, _attackRange * 0.8f) : 0.1f;
        _waitUntil = ActionTime + _wanderWait;
        _nextPathUpdate = 0f;
    }

    private bool IsPlayerInAttackRange()
    {
        return CanSeePlayer(_attackRange);
    }

    private void TickAttack()
    {
        if (_waitingSecondPunch)
        {
            if (_player == null) { ChangeState(State.Wander); return; }
            FacePlayer();
            Vector3 offset = _player.position - transform.position;
            offset.y = 0f;
            float step = Mathf.Min(_doublePunchAdvanceSpeed * Time.deltaTime, _comboAdvanceRemaining,
                Mathf.Max(0f, offset.magnitude - _attackRange * 0.8f));
            if (step > 0f) { MoveOnNavMesh(offset.normalized * step); _comboAdvanceRemaining -= step; }
            if (ActionTime >= _secondPunchAt)
            {
                _waitingSecondPunch = false;
                BeginPunch();
            }
            return;
        }
        if (_attackAnimationStarted)
        {
            float progress = GetAttackProgress();
            if (!_attackLanded && progress >= _attackHitNormalizedTime) ResolveAttack();
            if (_attackLanded && progress >= 1f)
            {
                if (_punchesRemaining > 1)
                {
                    _punchesRemaining--;
                    _attackAnimationStarted = false;
                    _waitingSecondPunch = true;
                    _secondPunchAt = ActionTime + _doublePunchGap;
                    _comboAdvanceRemaining = _doublePunchAdvanceDistance;
                }
                else if (ActionTime >= _recoveryUntil)
                    ChangeState(CanSeePlayer(_loseRadius) ? State.Chase : State.Wander);
            }
            return;
        }
        if (ActionTime < _hitAnimationUntil) return;
        // Leaving range or breaking sight cancels the windup completely.
        if (!IsPlayerInAttackRange())
        {
            ChangeState(CanSeePlayer(_loseRadius) ? State.Chase : State.Wander);
            return;
        }
        Vector3 direction = _player.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction), _agent.angularSpeed * Time.deltaTime);
        _closeTime += Time.deltaTime;
        if (_closeTime < _attackDelay) return;
        BeginPunch();
    }

    private void BeginPunch()
    {
        _attackLanded = false;
        _attackAnimationStarted = true;
        _attackAnimationStartTime = ActionTime;
        int hash = Animator.StringToHash("Base Layer.Attack");
        if (_animator != null && _animator.runtimeAnimatorController != null && _animator.HasState(0, hash))
        {
            // Restart the clip explicitly for every attack, including consecutive attacks.
            _animationState = hash;
            _animator.CrossFadeInFixedTime(hash, 0.05f, 0, 0f);
        }
    }

    private void FacePlayer()
    {
        if (_player == null) return;
        Vector3 direction = _player.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction), _agent.angularSpeed * Time.deltaTime);
    }

    private void TickCharge()
    {
        if (_player == null || !_enableChargePunch)
        {
            ChangeState(State.Wander);
            return;
        }
        if (CurrentState == State.ChargeWindup)
        {
            if (!CanSeePlayer(_chargeMaxRange)) { ChangeState(State.Chase); return; }
            FacePlayer();
            if (ActionTime < _chargeUntil) return;
            _chargeDirection = _player.position - transform.position;
            _chargeDirection.y = 0f;
            _chargeDirection = _chargeDirection.sqrMagnitude > 0.001f ? _chargeDirection.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(_chargeDirection);
            ChangeState(State.Charge);
            _chargeRemaining = _chargeDistance;
        }
        // Direction is locked at takeoff, so the player can dodge sideways.
        if (!IsPlayerInAttackRange() && _chargeRemaining > 0f)
        {
            float step = Mathf.Min(_chargeSpeed * Time.deltaTime, _chargeRemaining);
            bool blocked = MoveOnNavMesh(_chargeDirection * step);
            _chargeRemaining -= step;
            if (!blocked && _chargeRemaining > 0f && !IsPlayerInAttackRange()) return;
        }
        ChangeState(State.Attack);
        _punchesRemaining = 1; // Charge and double punch are separate attack choices.
        BeginPunch();
    }

    // Returns true at a NavMesh boundary. Agent.Move keeps avoidance/collision geometry in use.
    private bool MoveOnNavMesh(Vector3 offset)
    {
        Vector3 start = _agent.nextPosition;
        Vector3 target = start + offset;
        bool blocked = _agent.Raycast(target, out var hit);
        if (blocked) target = hit.position;
        _agent.Move(target - start);
        return blocked;
    }

    private float GetAttackProgress()
    {
        int hash = Animator.StringToHash("Base Layer.Attack");
        if (_animator != null && _animator.runtimeAnimatorController != null && _animator.HasState(0, hash))
        {
            var state = _animator.IsInTransition(0)
                ? _animator.GetNextAnimatorStateInfo(0) : _animator.GetCurrentAnimatorStateInfo(0);
            return state.fullPathHash == hash ? state.normalizedTime : 0f;
        }
        // Keep enemies without a valid Animator functional.
        return (ActionTime - _attackAnimationStartTime) / 0.6f;
    }

    private void ResolveAttack()
    {
        _attackLanded = true;
        _nextAttackTime = ActionTime + _attackCooldown;
        _recoveryUntil = ActionTime + _attackRecovery;
        // Check again at impact. A player who dodged the punch takes no damage.
        if (!IsPlayerInAttackRange()) return;
        Vector3 direction = _player.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f &&
            Vector3.Angle(transform.forward, direction) > _attackHitHalfAngle) return;
        var health = _player.GetComponent<Health>();
        if (health != null && (health.IsDead || health.IsInvulnerable)) return;
        if (health != null) health.TakeDamage(_attackDamage);
        if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;
        _player.GetComponent<IKnockbackReceiver>()?.ApplyKnockback(
            direction, _attackKnockbackDistance, _attackKnockbackDuration);
    }

    private void TickWander()
    {
        if (_agent.pathPending) return;
        if (_agent.hasPath && _agent.remainingDistance > _agent.stoppingDistance + 0.1f) return;
        if (_agent.hasPath) { _agent.ResetPath(); _waitUntil = ActionTime + _wanderWait; }
        if (ActionTime < _waitUntil) return;
        Vector3 center = _areaCenter != null ? _areaCenter.position : _spawn;
        for (int i = 0; i < 12; i++)
        {
            Vector2 random = Random.insideUnitCircle * _wanderRadius;
            Vector3 candidate = center + new Vector3(random.x, 0f, random.y);
            if (!NavMesh.SamplePosition(candidate, out var hit, 1.5f, _agent.areaMask)) continue;
            if ((new Vector2(hit.position.x - center.x, hit.position.z - center.z)).sqrMagnitude > _wanderRadius * _wanderRadius) continue;
            if (!_agent.CalculatePath(hit.position, _path) || _path.status != NavMeshPathStatus.PathComplete) continue;
            _agent.SetPath(_path);
            return;
        }
        _waitUntil = ActionTime + 1f;
    }

    public void ApplyKnockback(Vector3 direction, float distance, float duration)
    {
        if (!isActiveAndEnabled || _health.IsDead || !Ready || distance <= 0f || _knockbackMultiplier <= 0f) return;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        _knockbackDirection = direction.normalized;
        _knockbackDistance = distance * _knockbackMultiplier;
        _knockbackDuration = Mathf.Max(0.01f, duration);
        _knockbackElapsed = 0f;
        ChangeState(State.Knockback);
    }

    private void TickKnockback()
    {
        float before = _knockbackElapsed / _knockbackDuration;
        _knockbackElapsed = Mathf.Min(_knockbackElapsed + Time.deltaTime, _knockbackDuration);
        float after = _knockbackElapsed / _knockbackDuration;
        // Ease-out displacement; NavMesh raycast keeps the enemy inside walkable ground.
        float distance = _knockbackDistance * ((2f * after - after * after) - (2f * before - before * before));
        Vector3 target = _agent.nextPosition + _knockbackDirection * distance;
        if (_agent.Raycast(target, out var hit)) target = hit.position;
        _agent.Move(target - _agent.nextPosition);
        if (_knockbackElapsed >= _knockbackDuration) ChangeState(CanSeePlayer(_detectionRadius) ? State.Chase : State.Wander);
    }

    private void OnDisable()
    {
        if (_hitStop != null) _hitStop.Cancel();
        if (_health != null) _health.Damaged -= HandleDamage;
        if (Ready) { _agent.ResetPath(); _agent.isStopped = true; }
    }

    private void OnEnable()
    {
        if (_health != null) _health.Damaged += HandleDamage;
        if (_agent != null) ChangeState(State.Wander);
    }

    private void HandleDamage(int damage)
    {
        // Initialization/health refresh is not damage and must not interrupt the AI.
        // Restart the attack windup without adding knockback to the first two hits.
        if (_health.IsDead) return;
        _closeTime = 0f;
        if (CurrentState == State.Attack || CurrentState == State.ChargeWindup || CurrentState == State.Charge)
            ChangeState(State.Chase);
        _hitAnimationUntil = ActionTime + 0.25f;
        _animationState = 0;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_areaCenter != null ? _areaCenter.position : Application.isPlaying ? _spawn : transform.position, _wanderRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);
    }
}
