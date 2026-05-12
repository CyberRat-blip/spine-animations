using UnityEngine;

namespace Gameplay.Input
{
    public interface IPlayerInput
    {
        Vector2 CursorWorldPosition { get; }
        bool IsAiming { get; }
        void Tick();
    }
}
