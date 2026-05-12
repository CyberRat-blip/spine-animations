using Gameplay.Config;
using UnityEngine;

namespace Gameplay.Movement
{
    public sealed class InertialHorizontalMover : MonoBehaviour, IHorizontalMover
    {
        [SerializeField] private PlayerSettings settings;

        private float _targetX;
        private float _velocity;
        private HorizontalRange _range;

        public float CurrentX => transform.position.x;
        public float CurrentSpeed => _velocity;
        public HorizontalRange Range => _range;

        private void Awake()
        {
            _range = new HorizontalRange(settings.MinX, settings.MaxX, settings.YLine);

            var position = transform.position;
            position.x = _range.Clamp(position.x);
            position.y = _range.YLine;
            transform.position = position;

            _targetX = position.x;
        }

        public void SetTargetX(float targetX)
        {
            _targetX = _range.Clamp(targetX);
        }

        public void Tick(float deltaTime)
        {
            var position = transform.position;

            position.x = Mathf.SmoothDamp(
                position.x,
                _targetX,
                ref _velocity,
                settings.SmoothTime,
                settings.MaxSpeed,
                deltaTime);

            if (Mathf.Abs(position.x - _targetX) <= settings.ArriveEpsilon &&
                Mathf.Abs(_velocity) <= settings.ArriveEpsilon)
            {
                position.x = _targetX;
                _velocity = 0f;
            }

            position.x = _range.Clamp(position.x);
            position.y = _range.YLine;
            transform.position = position;
        }
    }
}
