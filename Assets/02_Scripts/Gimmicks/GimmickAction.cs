using UnityEngine;

// Receivers can be reused with sword switches, plates, levers, timers, etc.
public abstract class GimmickAction : MonoBehaviour
{
    public abstract void SetActivated(bool active);
}
