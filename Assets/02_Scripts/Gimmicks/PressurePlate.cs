using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class PressurePlate : GimmickSignal
{
    [SerializeField] private bool _acceptPlayer = true;
    [SerializeField] private bool _acceptBoxes = true;
    private readonly HashSet<Collider> _occupants = new();

    private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

    private void OnTriggerEnter(Collider other) => Register(other);
    private void OnTriggerStay(Collider other) => Register(other);

    private void Register(Collider other)
    {
        if ((_acceptPlayer && other.GetComponentInParent<PlayerMovement>() != null) ||
            (_acceptBoxes && other.GetComponentInParent<PushableBox>() != null))
        {
            _occupants.Add(other);
            SetState(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        _occupants.Remove(other);
        SetState(_occupants.Count > 0);
    }

    private void FixedUpdate()
    {
        _occupants.RemoveWhere(other => other == null || !other.enabled || !other.gameObject.activeInHierarchy);
        SetState(_occupants.Count > 0);
    }

    private void OnDisable()
    {
        _occupants.Clear();
        SetState(false);
    }
}
