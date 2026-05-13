using Spine;
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

        private TrackEntry _trackEntry;

        public PositionLockedTrack(AnimationState state, int trackIndex, string animationName, float stepsAcrossRange, float stepsPerLoop)
        {
            _state = state;
            _trackIndex = trackIndex;
            _animationName = animationName;
            _stepsAcrossRange = Mathf.Max(0.0001f, stepsAcrossRange);
            _stepsPerLoop = Mathf.Max(0.0001f, stepsPerLoop);
        }

        public TrackEntry Entry => _trackEntry;

        public void Start(MixBlend blend)
        {
            _trackEntry = _state.SetAnimation(_trackIndex, _animationName, loop: true);
            _trackEntry.MixBlend = blend;
            _trackEntry.TimeScale = 0f;
            _trackEntry.Alpha = 0f;
        }

        public void SetAlpha(float alpha)
        {
            if (_trackEntry == null)
            {
                return;
            }

            _trackEntry.Alpha = Mathf.Clamp01(alpha);
        }

        public void Apply(float normalizedX, bool invertPhaseWithinLoop)
        {
            if (_trackEntry == null)
            {
                return;
            }

            var loopDuration = _trackEntry.AnimationEnd - _trackEntry.AnimationStart;
            if (loopDuration <= 0f)
            {
                return;
            }

            var loops = normalizedX * (_stepsAcrossRange / _stepsPerLoop);
            var phase = loops - Mathf.Floor(loops);
            if (invertPhaseWithinLoop)
            {
                phase = 1f - phase;
            }

            _trackEntry.TrackTime = _trackEntry.AnimationStart + phase * loopDuration;
        }
    }
}
