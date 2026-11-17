using UnityEngine;

public interface IInteractable
{
    string InteractionLabel { get; }
    bool CanInteract(Transform actor);
    void Interact(Transform actor);
}
