using Gameplay.Movement;

namespace Gameplay.Animation
{
    public interface ILocomotionAnimator
    {
        void Tick(float currentX, float currentSpeed, HorizontalRange range);
    }
}
