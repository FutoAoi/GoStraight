using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimation : MonoBehaviour
{
    private Animator _animator;
    private Rigidbody _body;
    private PlayerLockOn _lockOn;
    private float _maxMovementSpeed = 5f;
    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int AttackId = Animator.StringToHash("Attack");
    private static readonly int NextAttackId = Animator.StringToHash("NextAttack");
    private static readonly int[] ComboStateIds =
    {
        Animator.StringToHash("Attack01"),
        Animator.StringToHash("Attack02"),
        Animator.StringToHash("Attack03")
    };
    private static readonly int MoveXId = Animator.StringToHash("MoveX");
    private static readonly int MoveZId = Animator.StringToHash("MoveZ");
    private static readonly int IsFocusingId = Animator.StringToHash("IsFocusing");

    public bool IsAttackPlaying =>
        _animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack") ||
        (_animator.IsInTransition(0) &&
         _animator.GetNextAnimatorStateInfo(0).IsTag("Attack"));

    public Vector3 RootDeltaPosition => _animator.deltaPosition;
    public bool IsReturningFromAttack =>
        _animator.IsInTransition(0) &&
        _animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack") &&
        !_animator.GetNextAnimatorStateInfo(0).IsTag("Attack");
    public Quaternion RootDeltaRotation => _animator.deltaRotation;
    public bool IsTransitioning => _animator.IsInTransition(0);

    public void CancelAttackRequest()
    {
        _animator.ResetTrigger(AttackId);
        _animator.ResetTrigger(NextAttackId);
    }

    public void QueueNextAttack()
    {
        _animator.SetTrigger(NextAttackId);
    }

    public bool TryGetComboProgress(out int step, out float progress)
    {
        step = 0;
        progress = 0f;
        // ブレンド中の入力は、遷移先の段に対する先行入力として扱う。
        AnimatorStateInfo state = _animator.IsInTransition(0)
            ? _animator.GetNextAnimatorStateInfo(0)
            : _animator.GetCurrentAnimatorStateInfo(0);
        for (int i = 0; i < ComboStateIds.Length; i++)
        {
            if (state.shortNameHash != ComboStateIds[i]) continue;
            step = i + 1;
            progress = state.normalizedTime;
            return true;
        }
        return false;
    }

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _body = GetComponent<Rigidbody>();
        _lockOn = GetComponent<PlayerLockOn>();
    }

    public void SetMaxMovementSpeed(float speed)
    {
        _maxMovementSpeed = Mathf.Max(speed, 0.01f);
    }

    private void LateUpdate()
    {
        // ワールドの移動速度を、プレイヤーから見た前後・左右へ変換する。
        Vector3 velocity = _body.linearVelocity;
        velocity.y = 0f;
        Vector3 localVelocity = transform.InverseTransformDirection(velocity);
        Vector2 movement = Vector2.ClampMagnitude(
            new Vector2(localVelocity.x, localVelocity.z) / _maxMovementSpeed, 1f);

        _animator.SetFloat(SpeedId, velocity.magnitude);
        _animator.SetFloat(MoveXId, movement.x);
        _animator.SetFloat(MoveZId, movement.y);
        _animator.SetBool(IsFocusingId, _lockOn != null && _lockOn.IsFocusing);
    }

    public void PlayerAttack()
    {
        _animator.SetTrigger(AttackId);
    }

    /// <summary>
    /// アニメーションの初期化
    /// </summary>
    public void ResetAnimation()
    {
        _animator.Rebind();
        _animator.SetFloat(SpeedId, 0f);
        _animator.SetFloat(MoveXId, 0f);
        _animator.SetFloat(MoveZId, 0f);
        _animator.SetBool(IsFocusingId, false);
        CancelAttackRequest();
        _animator.Update(0f);
    }
}
