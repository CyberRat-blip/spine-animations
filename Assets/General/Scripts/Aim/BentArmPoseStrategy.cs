using UnityEngine;

namespace Gameplay.Aim
{
    public sealed class BentArmPoseStrategy : IAimPoseStrategy
    {
        private readonly float _distance;

        public BentArmPoseStrategy(float distance)
        {
            _distance = distance;
        }

        public Vector2 ResolveCrosshairOffset(Vector2 localDirection)
        {
            if (localDirection.sqrMagnitude < Mathf.Epsilon)
            {
                return new Vector2(_distance, 0f);
            }

            return localDirection.normalized * _distance;
        }
    }
}
