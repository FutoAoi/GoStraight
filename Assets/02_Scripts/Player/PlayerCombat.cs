using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    [Header("攻撃の当たり判定")]
    [SerializeField] private AttackHitbox _attackHitbox;
    [SerializeField, Min(1)] private int _attackDamage = 10;
    [Header("Third hit knockback") ]
    [SerializeField, Min(0f)] private float _thirdHitKnockbackDistance = 3f;
    [SerializeField, Min(0.01f)] private float _thirdHitKnockbackDuration = 0.3f;
    private int _hitboxStep;
    [Header("Hit stop (seconds, without changing Time.timeScale)")]
    [SerializeField, Min(0f)] private float _firstHitStop = 0.06f;
    [SerializeField, Min(0f)] private float _secondHitStop = 0.07f;
    [SerializeField, Min(0f)] private float _thirdHitStop = 0.1f;
    private ActorHitStop _hitStop;
    public bool IsHitStopped => _hitStop != null && _hitStop.IsStopped;
    private float ActionTime => _hitStop != null ? _hitStop.LocalTime : Time.time;

    [Header("次の攻撃の入力受付（アニメーションの進行率）")]
    [SerializeField, Range(0f, 0.7f)] private float _comboInputStart = 0.1f;
    [SerializeField, Range(0f, 1f)] private float _comboInputEnd = 0.65f;
    [SerializeField, Min(0f)] private float _comboInputBufferTime = 0.2f;
    private int _bufferedComboStep;
    private float _bufferedComboUntil;
    private bool _comboQueued;
    public int CurrentComboStep { get; private set; }
    private Rigidbody _body;
    private PlayerAnimation _animation;
    private PlayerMovement _movement;
    private PlayerJamp _jump;
    private bool _attackRequested;
    private bool _enteredAttack;
    private float _requestTime;
    private bool _movementReleased;

    public bool IsAttacking => isActiveAndEnabled &&
        (_attackRequested || _animation.IsAttackPlaying);

    // 攻撃から移動へ戻るブレンド中は、見た目をつなぎながら操作を許可する。
    public bool BlocksMovement => IsAttacking && !_animation.IsReturningFromAttack;

    private void Awake()
    {
        _hitStop = ActorHitStop.GetOrAdd(gameObject);
        if (!TryGetComponent<PlayerHealth>(out _)) gameObject.AddComponent<PlayerHealth>();
        if (!TryGetComponent<PlayerInteractor>(out _)) gameObject.AddComponent<PlayerInteractor>();
        _body = GetComponent<Rigidbody>();
        _animation = GetComponent<PlayerAnimation>();
        _movement = GetComponent<PlayerMovement>();
        _jump = GetComponent<PlayerJamp>();
        if (_attackHitbox == null)
            _attackHitbox = GetComponentInChildren<AttackHitbox>(true);
    }

    // Animation EventのInt引数に、対応する攻撃の段数（1〜3）を指定する。
    public void BeginAttackHitbox(int step)
    {
        if (!IsAttacking || _animation.IsReturningFromAttack || _attackHitbox == null) return;
        if (!_animation.TryGetComboProgress(out int currentStep, out _)) return;
        if (step != currentStep || step == _hitboxStep) return;

        EndHitbox();
        _hitboxStep = step;
        _attackHitbox.BeginAttack(transform, _attackDamage,
            step == 3 ? _thirdHitKnockbackDistance : 0f, _thirdHitKnockbackDuration, step);
    }

    public void EndAttackHitbox(int step)
    {
        // 前のクリップの終了イベントで、次の段の判定を消さない。
        if (step == _hitboxStep) EndHitbox();
    }

    private void EndHitbox()
    {
        if (_attackHitbox != null) _attackHitbox.EndAttack();
        _hitboxStep = 0;
    }

    private void OnDisable()
    {
        if (_hitStop != null) _hitStop.Cancel();
        EndHitbox();
    }

    public void PlayHitStop(Component target, int step)
    {
        if (target == null) return;
        var enemy = target.GetComponentInParent<EnemyMovement>();
        if (enemy == null) return;
        float duration = step == 3 ? _thirdHitStop : step == 2 ? _secondHitStop : _firstHitStop;
        _hitStop.Stop(duration);
        ActorHitStop.GetOrAdd(enemy.gameObject).Stop(duration);
    }

    // PlayerInputのSend Messagesから呼ばれる。
    public void OnAttack(InputValue value)
    {
        if (value.isPressed) TryAttack();
    }

    public void TryAttack()
    {
        if (!isActiveAndEnabled) return;
        if (GameMenuController.IsGamePaused || Time.timeScale <= 0f) return;
        if (_animation.IsTakingDamage) return;
        if (_movement.IsKnockedBack) return;
        if (_jump != null && (!_jump.IsGrounded || _jump.IsPreparingJump)) return;
        if (IsAttacking)
        {
            TryQueueCombo();
            return;
        }

        _attackRequested = true;
        _movementReleased = false;
        _enteredAttack = false;
        _requestTime = ActionTime;
        _comboQueued = false;
        CurrentComboStep = 1;
        _movement.ResetMovement();
        StopHorizontalMovement();
        _animation.PlayerAttack();
        SoundManager.Instance.PlaySwordSwing();
    }

    private void TryQueueCombo()
    {
        if (!_animation.TryGetComboProgress(out int step, out float progress)) return;
        RefreshComboStep(step);
        if (_comboQueued || step >= 3) return;
        if (progress > _comboInputEnd) return;

        // 早めの入力を短時間保持し、受付開始またはブレンド終了時に予約する。
        _bufferedComboStep = step;
        _bufferedComboUntil = ActionTime + _comboInputBufferTime;
        ConsumeBufferedCombo();
    }

    private void ConsumeBufferedCombo()
    {
        if (_bufferedComboStep == 0) return;
        if (ActionTime > _bufferedComboUntil)
        {
            _bufferedComboStep = 0;
            return;
        }
        if (!_animation.TryGetComboProgress(out int step, out float progress)) return;
        if (step != _bufferedComboStep || progress > _comboInputEnd)
        {
            _bufferedComboStep = 0;
            return;
        }
        if (_animation.IsTransitioning || progress < _comboInputStart) return;
        if (_comboQueued || step >= 3) return;

        // 1段につき1回だけ予約。Animatorが70%地点で次の段へ進める。
        _bufferedComboStep = 0;
        _comboQueued = true;
        _animation.QueueNextAttack();
    }

    private void RefreshComboStep(int step)
    {
        if (CurrentComboStep == step) return;
        if (_hitboxStep != step) EndHitbox();
        CurrentComboStep = step;
        _comboQueued = false;
    }

    private void LateUpdate()
    {
        if (IsHitStopped) return;
        if (!_attackRequested) return;
        ReleaseMovementIfReturning();

        if (_animation.IsAttackPlaying)
        {
            _enteredAttack = true;
            if (_animation.TryGetComboProgress(out int step, out _))
                RefreshComboStep(step);
            ConsumeBufferedCombo();
            return;
        }

        // 遷移完了で解除。設定不備で開始できない場合も永久に操作を止めない。
        if (_enteredAttack || ActionTime - _requestTime > 0.5f)
            FinishAttack();
    }

    public void ResetCombat()
    {
        if (_hitStop != null) _hitStop.Cancel();
        ClearAttackState();
        _movement.ResetMovement();
        StopHorizontalMovement();
    }

    private void FinishAttack()
    {
        // すでに移動を再開しているなら、速度・加速をもう一度リセットしない。
        if (!_movementReleased)
        {
            _movement.ResetMovement();
            StopHorizontalMovement();
        }
        ClearAttackState();
    }

    private void ClearAttackState()
    {
        EndHitbox();
        _attackRequested = false;
        _enteredAttack = false;
        _comboQueued = false;
        _bufferedComboStep = 0;
        CurrentComboStep = 0;
        _animation.CancelAttackRequest();
        _movementReleased = false;
    }

    private void ReleaseMovementIfReturning()
    {
        if (_movementReleased || !_animation.IsReturningFromAttack) return;
        _movementReleased = true;
        EndHitbox();
        StopHorizontalMovement();
    }

    private void OnValidate()
    {
        _comboInputEnd = Mathf.Max(_comboInputStart, _comboInputEnd);
    }

    private void StopHorizontalMovement()
    {
        Vector3 velocity = _body.linearVelocity;
        velocity.x = 0f;
        velocity.z = 0f;
        _body.linearVelocity = velocity;
    }

    private void OnAnimatorMove()
    {
        if (IsHitStopped) return;
        if (_movement.IsKnockedBack) return;
        ReleaseMovementIfReturning();
        if (!BlocksMovement || !_animation.IsAttackPlaying) return;
        if (Time.deltaTime <= 0f) return;

        Vector3 velocity = _animation.RootDeltaPosition / Time.deltaTime;
        _movement.ApplyHorizontalVelocity(velocity);
        _body.MoveRotation(_body.rotation * _animation.RootDeltaRotation);
    }
}
