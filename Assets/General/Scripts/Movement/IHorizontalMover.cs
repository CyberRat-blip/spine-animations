namespace Gameplay.Movement
{
    public interface IHorizontalMover
    {
        float CurrentX { get; }
        float CurrentSpeed { get; }

        HorizontalRange Range { get; }

        void SetTargetX(float targetX);
        void Tick(float deltaTime);
    }
}
