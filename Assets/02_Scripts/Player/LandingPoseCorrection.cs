using UnityEngine;

// 物理的な位置は変えず、着地アニメーションの足裏の沈み込みを補正する。
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator), typeof(Rigidbody))]
public class LandingPoseCorrection : MonoBehaviour
{
    [SerializeField] private LayerMask _groundLayers = ~0;
    [Tooltip("Avatar上の足裏と、見た目の靴底の差を吸収する余裕。")]
    [SerializeField, Min(0f)] private float _soleClearance = 0.025f;
    [SerializeField, Min(0.01f)] private float _maxLift = 0.3f;
    [SerializeField, Min(0.01f)] private float _releaseSmoothTime = 0.12f;

    private static readonly int LandId = Animator.StringToHash("Land");
    private Animator _animator;
    private Rigidbody _body;
    private PlayerJamp _jump;
    private float _leftLift;
    private float _rightLift;
    private float _leftLiftVelocity;
    private float _rightLiftVelocity;
    private readonly RaycastHit[] _groundHits = new RaycastHit[16];

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _body = GetComponent<Rigidbody>();
        _jump = GetComponent<PlayerJamp>();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || !_animator.isHuman) return;

        bool landing = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash == LandId ||
            (_animator.IsInTransition(0) &&
             _animator.GetNextAnimatorStateInfo(0).shortNameHash == LandId);

        if ((_jump != null && (!_jump.IsGrounded || _jump.IsPreparingJump)) ||
            (!landing && _leftLift <= 0f && _rightLift <= 0f))
        {
            ClearCorrection();
            return;
        }

        Vector3 left = _animator.GetIKPosition(AvatarIKGoal.LeftFoot);
        Vector3 right = _animator.GetIKPosition(AvatarIKGoal.RightFoot);
        // 身体全体や反対の足を持ち上げず、埋まっている足だけを補正する。
        CorrectFoot(AvatarIKGoal.LeftFoot, left, _animator.leftFeetBottomHeight,
            ref _leftLift, ref _leftLiftVelocity);
        CorrectFoot(AvatarIKGoal.RightFoot, right, _animator.rightFeetBottomHeight,
            ref _rightLift, ref _rightLiftVelocity);
    }

    private void CorrectFoot(AvatarIKGoal foot, Vector3 position, float bottomHeight,
        ref float lift, ref float liftVelocity)
    {
        float requiredLift = Mathf.Clamp(RequiredLift(foot, position, bottomHeight), 0f, _maxLift);
        if (requiredLift >= lift)
        {
            lift = requiredLift;
            liftVelocity = 0f;
        }
        else
        {
            lift = Mathf.Max(requiredLift, Mathf.SmoothDamp(
                lift, requiredLift, ref liftVelocity, _releaseSmoothTime,
                Mathf.Infinity, Time.deltaTime));
            if (requiredLift <= 0f && lift < 0.0001f) lift = 0f;
        }
        _animator.SetIKPositionWeight(foot, lift > 0f ? 1f : 0f);
        _animator.SetIKPosition(foot, position + Vector3.up * lift);
    }

    private float RequiredLift(AvatarIKGoal foot, Vector3 position, float bottomHeight)
    {
        Vector3 sole = position + _animator.GetIKRotation(foot) * (Vector3.down * bottomHeight);
        // 足が少し埋まっていても、上から床を探せるようにする。
        int count = Physics.RaycastNonAlloc(sole + Vector3.up * 0.5f, Vector3.down,
            _groundHits, 0.8f, _groundLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        float floorY = 0f;
        for (int i = 0; i < count; i++)
        {
            var hit = _groundHits[i];
            if (hit.collider.attachedRigidbody == _body || hit.transform.IsChildOf(transform)) continue;
            if (hit.normal.y < 0.6f || hit.distance >= nearest) continue;
            nearest = hit.distance;
            floorY = hit.point.y;
        }
        return float.IsPositiveInfinity(nearest) ? 0f :
            Mathf.Max(0f, floorY + _soleClearance - sole.y);
    }

    private void ClearCorrection()
    {
        ResetOffsets();
        _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
        _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
    }

    private void OnDisable()
    {
        ResetOffsets();
    }

    private void ResetOffsets()
    {
        _leftLift = 0f;
        _rightLift = 0f;
        _leftLiftVelocity = 0f;
        _rightLiftVelocity = 0f;
    }
}
