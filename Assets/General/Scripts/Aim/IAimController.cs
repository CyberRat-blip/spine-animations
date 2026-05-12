using UnityEngine;

namespace Gameplay.Aim
{
    public interface IAimController
    {
        bool IsAiming { get; }
        void Tick(bool isAimingHeld, Vector2 cursorWorldPosition, Vector3 playerWorldPosition);
    }
}
