using UnityEngine;

namespace Gameplay.Aim
{
    public interface IAimPoseStrategy
    {
        Vector2 ResolveCrosshairOffset(Vector2 localDirection);
    }
}
