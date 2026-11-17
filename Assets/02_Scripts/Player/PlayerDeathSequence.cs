using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class PlayerDeathSequence : MonoBehaviour
{
    [Header("Death cinematic")]
    [SerializeField, Min(0.1f)] private float _minimumShotTime = 1.8f;
    [SerializeField, Min(0.1f)] private float _animationTimeout = 5f;
    [SerializeField, Min(0f)] private float _poseHoldTime = 0.4f;
    [SerializeField, Min(0.5f)] private float _cameraDistance = 3.5f;
    [SerializeField, Min(0.1f)] private float _cameraTargetHeight = 0.5f;
    [SerializeField, Range(0f, 70f)] private float _cameraPitch = 30f;
    private static PlayerDeathSequence _active;
    public static bool IsGameOver => _active != null;
    private bool _started;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _active = null;

    public void Begin()
    {
        if (_started) return;
        _started = true;
        if (GameMenuController.IsGamePaused)
            foreach (var menu in FindObjectsByType<GameMenuController>()) menu.Resume();
        _active = this;
        GetComponent<Health>().IsInvulnerable = true;
        var combat = GetComponent<PlayerCombat>();
        if (combat != null) combat.ResetCombat();
        var movement = GetComponent<PlayerMovement>();
        if (movement != null) { movement.ResetMovement(); movement.MoveInput = Vector2.zero; }
        var animation = GetComponent<PlayerAnimation>();
        if (animation != null) animation.ResetAnimation();
        Disable(GetComponent<PlayerInput>());
        Disable(combat);
        Disable(GetComponent<PlayerJamp>());
        Disable(GetComponent<PlayerLockOn>());
        Disable(GetComponent<PlayerRotation>());
        Disable(GetComponent<PlayerInteractor>());
        Disable(GetComponent<LandingPoseCorrection>());
        Disable(movement);
        Disable(animation);
        foreach (var enemy in FindObjectsByType<EnemyMovement>())
        {
            enemy.enabled = false;
            foreach (var enemyAnimator in enemy.GetComponentsInChildren<Animator>()) enemyAnimator.speed = 0f;
        }
        var body = GetComponent<Rigidbody>();
        if (body != null)
        {
            var velocity = body.linearVelocity;
            velocity.x = velocity.z = 0f;
            body.linearVelocity = velocity;
            body.angularVelocity = Vector3.zero;
        }
        var animator = GetComponent<Animator>();
        int death = Animator.StringToHash("Base Layer.Death");
        bool hasDeath = animator != null && animator.runtimeAnimatorController != null && animator.HasState(0, death);
        if (hasDeath)
        {
            animator.applyRootMotion = false;
            animator.speed = 1f;
            animator.CrossFadeInFixedTime(death, 0.08f, 0, 0f);
        }
        else Debug.LogWarning("Player Animator needs the Death state. Game over will still be shown.", this);
        if (Camera.main != null && Camera.main.TryGetComponent<CameraOrbit>(out var camera))
            camera.BeginDeathShot(_cameraDistance, _cameraTargetHeight, _cameraPitch);
        StartCoroutine(ShowAfterCollapse(animator, death, hasDeath));
    }
    private static void Disable(Behaviour component) { if (component != null) component.enabled = false; }
    private IEnumerator ShowAfterCollapse(Animator animator, int death, bool hasDeath)
    {
        float start = Time.unscaledTime;
        while (Time.unscaledTime - start < Mathf.Max(_minimumShotTime, _animationTimeout))
        {
            float elapsed = Time.unscaledTime - start;
            if (elapsed >= _minimumShotTime)
            {
                if (!hasDeath || animator == null) break;
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.fullPathHash == death && state.normalizedTime >= 0.98f) break;
            }
            yield return null;
        }
        if (_poseHoldTime > 0f) yield return new WaitForSecondsRealtime(_poseHoldTime);
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        bool found = false;
        foreach (var view in FindObjectsByType<GameOverView>(FindObjectsInactive.Include))
            if (view.gameObject.scene == gameObject.scene) { view.Show(); found = true; }
        if (!found) Debug.LogError("Place GameOverUI.prefab in the scene.", this);
    }
    private void OnDestroy()
    {
        if (_active != this) return;
        _active = null;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }
}
