using UnityEngine;

public sealed class PlayerPlacement
{
    private readonly Rigidbody _body;
    private readonly PlayerMovement _movement;
    private readonly PlayerAnimation _animation;
    private readonly PlayerLockOn _lockOn;
    private readonly PlayerRotation _rotation;
    private readonly PlayerCombat _combat;

    public PlayerPlacement(Rigidbody body)
    {
        _body = body;
        _movement = body.GetComponent<PlayerMovement>();
        _animation = body.GetComponent<PlayerAnimation>();
        _lockOn = body.GetComponent<PlayerLockOn>();
        _rotation = body.GetComponent<PlayerRotation>();
        _combat = body.GetComponent<PlayerCombat>();
    }

    public void PlaceAt(Transform startLocation)
    {
        // 同期処理なので、この処理の途中に通常のUpdateは挟まらない。
        _movement.ResetMovement();
        if (_combat != null) _combat.ResetCombat();
        if (_lockOn != null) _lockOn.ResetLockOn();
        if (_rotation != null) _rotation.LookTarget = null;
        _animation.ResetAnimation();

        // Animatorの初期化がTransformに影響しても、最後に配置を確定する。
        _body.linearVelocity = Vector3.zero;
        _body.angularVelocity = Vector3.zero;
        _body.position = startLocation.position;
        _body.rotation = startLocation.rotation;
    }
}
