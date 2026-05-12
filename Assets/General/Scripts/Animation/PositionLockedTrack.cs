using Spine;
using Spine.Unity;
using UnityEngine;
using AnimationState = Spine.AnimationState;

namespace Gameplay.Animation
{
    public sealed class PositionLockedTrack
    {
        private readonly AnimationState _state;
        private readonly int _trackIndex;
        private readonly string _animationName;
        private readonly float _stepsAcrossRange;
        private readonly float _stepsPerLoop;

        private TrackEntry _entry;

        public PositionLockedTrack(AnimationState state, int trackIndex, string animationName, float stepsAcrossRange, float stepsPerLoop)
        {
            _state = state;
            _trackIndex = trackIndex;
            _animationName = animationName;
            _stepsAcrossRange = Mathf.Max(0.0001f, stepsAcrossRange);
            _stepsPerLoop = Mathf.Max(0.0001f, stepsPerLoop);
        }

        public TrackEntry Entry => _entry;

        public void Start(MixBlend blend)
        {
            _entry = _state.SetAnimation(_trackIndex, _animationName, loop: true);
            _entry.MixBlend = blend;
            _entry.TimeScale = 0f;
            _entry.Alpha = 0f;
        }

        public void SetAlpha(float alpha)
        {
            if (_entry == null)
            {
                return;
            }

            _entry.Alpha = Mathf.Clamp01(alpha);
        }

        public void Apply(float normalizedX)
        {
            if (_entry == null)
            {
                return;
            }

            var loopDuration = _entry.AnimationEnd - _entry.AnimationStart;
            if (loopDuration <= 0f)
            {
                return;
            }

            var loops = normalizedX * (_stepsAcrossRange / _stepsPerLoop);
            var phase = loops - Mathf.Floor(loops); // [0..1)
            _entry.TrackTime = _entry.AnimationStart + phase * loopDuration;
        }
    }
}
