using UnityEngine;

public class StageGoal : MonoBehaviour
{
    [Header("Automatic goal door")]
    [SerializeField] private Transform _leftPanel;
    [SerializeField] private Transform _rightPanel;
    [SerializeField, Min(0.1f)] private float _openDistance = 6f;
    [SerializeField, Min(0.1f)] private float _closeDistance = 7f;
    [SerializeField, Min(0.1f)] private float _slideDistance = 3.5f;
    [SerializeField, Min(0.1f)] private float _doorSpeed = 4f;
    [Header("Walk into doorway (local forward / blue Z axis)")]
    [SerializeField, Min(0.1f)] private float _walkIntoDistance = 6f;
    [SerializeField, Min(0.1f)] private float _walkSpeed = 5f;
    [SerializeField, Min(0f)] private float _walkBeforeFade = 0.6f;
    private SlidingDoor _leftDoor;
    private SlidingDoor _rightDoor;
    private PlayerMovement _player;
    private bool _opened;
    private StageProgression _stageProgression;
    private bool _reached;

    private void Awake()
    {
        // Keep the goal trigger fixed while only the two physical panels slide.
        var goalCollider = GetComponent<Collider>();
        if (goalCollider != null) goalCollider.isTrigger = true;
        _leftDoor = PreparePanel(_leftPanel, -transform.right * _slideDistance);
        _rightDoor = PreparePanel(_rightPanel, transform.right * _slideDistance);
    }

    private SlidingDoor PreparePanel(Transform panel, Vector3 worldOffset)
    {
        if (panel == null) return null;
        var door = panel.GetComponent<SlidingDoor>();
        if (door == null) door = panel.gameObject.AddComponent<SlidingDoor>();
        Vector3 localOffset = panel.parent != null
            ? panel.parent.InverseTransformVector(worldOffset) : worldOffset;
        door.ConfigureOpening(localOffset, _doorSpeed);
        return door;
    }

    private void Start()
    {
        _player = FindAnyObjectByType<PlayerMovement>();
    }

    public void Initialize(StageProgression stageProgression)
    {
        _stageProgression = stageProgression;
    }

    private void Update()
    {
        if (_reached || _player == null) return;
        float distance = Vector3.Distance(_player.transform.position, transform.position);
        float closeDistance = Mathf.Max(_closeDistance, _openDistance + 0.1f);
        bool shouldOpen = _opened ? distance < closeDistance : distance <= _openDistance;
        if (shouldOpen == _opened) return;
        _opened = shouldOpen;
        if (_leftDoor != null) _leftDoor.SetActivated(_opened);
        if (_rightDoor != null) _rightDoor.SetActivated(_opened);
    }

    private void OnTriggerEnter(Collider other) => TryReachGoal(other);
    private void OnTriggerStay(Collider other) => TryReachGoal(other);

    private void TryReachGoal(Collider other)
    {
        if (_reached || _stageProgression == null) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;
        if (_leftDoor != null && !_leftDoor.IsFullyOpen) return;
        if (_rightDoor != null && !_rightDoor.IsFullyOpen) return;

        Vector3 destination = transform.position + transform.forward * _walkIntoDistance;
        _reached = _stageProgression.TransitionToNext(destination, _walkSpeed, _walkBeforeFade);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _openDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(_closeDistance, _openDistance + 0.1f));
    }
}
