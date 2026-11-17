using UnityEngine;

public interface ITargetable
{
    Transform AimPoint {  get; }
    bool CanTarget { get; }
}
