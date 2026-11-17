using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
public sealed class TutorialArea : MonoBehaviour
{
    [SerializeField] private string _instruction = "ジャンプ";
    [SerializeField] private string _keyboardButton = "SPACE";
    [SerializeField] private string _gamepadButton = "A";
    [Tooltip("エリアが重なったとき、大きい値を優先。同じ値なら後から入ったエリアを表示。")]
    [SerializeField] private int _priority;
    private readonly HashSet<Collider> _contacts = new();
    private static readonly List<TutorialArea> Active = new();
    private static long _sequence;
    private long _entered;
    public string Instruction => _instruction;
    public string ButtonLabel(bool gamepad) => gamepad ? _gamepadButton : _keyboardButton;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() { Active.Clear(); _sequence = 0; }
    private void Reset()
    {
        var box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(4, 3, 4);
        box.center = new Vector3(0, 1.5f, 0);
    }
    private void OnTriggerEnter(Collider other) => Register(other);
    private void OnTriggerStay(Collider other) => Register(other);
    private void Register(Collider other)
    {
        if (other.GetComponentInParent<PlayerMovement>() == null) return;
        if (!_contacts.Add(other) || _contacts.Count != 1) return;
        _entered = ++_sequence;
        if (!Active.Contains(this)) Active.Add(this);
    }
    private void OnTriggerExit(Collider other)
    {
        _contacts.Remove(other);
        if (_contacts.Count == 0) Active.Remove(this);
    }
    private void Update()
    {
        _contacts.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        if (!GetComponent<BoxCollider>().enabled) _contacts.Clear();
        if (_contacts.Count == 0) Active.Remove(this);
    }
    private void OnDisable() { _contacts.Clear(); Active.Remove(this); }

    public static TutorialArea Current
    {
        get
        {
            TutorialArea best = null;
            foreach (var area in Active)
            {
                if (area == null || !area.isActiveAndEnabled) continue;
                if (best == null || area._priority > best._priority ||
                    (area._priority == best._priority && area._entered > best._entered)) best = area;
            }
            return best;
        }
    }
    private void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(.2f, .8f, 1f, .12f);
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
