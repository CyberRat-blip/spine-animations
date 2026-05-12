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

            var pos = transform.position;
            pos.x = _range.Clamp(pos.x);
            pos.y = _range.YLine;
            transform.position = pos;

            _targetX = pos.x;
        }

        public void SetTargetX(float targetX)
        {
            _targetX = _range.Clamp(targetX);
        }

        public void Tick(float deltaTime)
        {
            var pos = transform.position;

            pos.x = Mathf.SmoothDamp(
                pos.x,
                _targetX,
                ref _velocity,
                settings.SmoothTime,
                settings.MaxSpeed,
                deltaTime);

            if (Mathf.Abs(pos.x - _targetX) <= settings.ArriveEpsilon &&
                Mathf.Abs(_velocity) <= settings.ArriveEpsilon)
            {
                pos.x = _targetX;
                _velocity = 0f;
            }

            pos.x = _range.Clamp(pos.x);
            pos.y = _range.YLine;
            transform.position = pos;
        }
    }
}
