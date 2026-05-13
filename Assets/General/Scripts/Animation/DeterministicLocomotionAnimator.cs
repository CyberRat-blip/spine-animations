using Gameplay.Config;
using Gameplay.Movement;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace Gameplay.Animation
{
    [RequireComponent(typeof(SkeletonAnimation))]
    public sealed class DeterministicLocomotionAnimator : MonoBehaviour, ILocomotionAnimator
    {
        [SerializeField] private SkeletonAnimation skeletonAnimation;
        [SerializeField] private PlayerSettings settings;

        [SpineAnimation][SerializeField] private string idleAnimation = "idle";
        [SpineAnimation][SerializeField] private string walkAnimation = "walk";
        [SpineAnimation][SerializeField] private string runAnimation = "run";

        private const int IdleTrack = 0;
        private const int WalkTrack = 1;
        private const int RunTrack = 2;

        private TrackEntry _idleEntry;
        private PositionLockedTrack _walkTrack;
        private PositionLockedTrack _runTrack;

        private float _smoothedSpeed;
        private float _smoothedSpeedVelocity;

        private void Reset()
        {
            skeletonAnimation = GetComponent<SkeletonAnimation>();
        }

        private void Awake()
        {
            if (skeletonAnimation == null)
            {
                skeletonAnimation = GetComponent<SkeletonAnimation>();
            }
        }

        private void Start()
        {
            var state = skeletonAnimation.AnimationState;

            _idleEntry = state.SetAnimation(IdleTrack, idleAnimation, loop: true);
            _idleEntry.MixBlend = MixBlend.Replace;
            _idleEntry.Alpha = 1f;

            _walkTrack = new PositionLockedTrack(state, WalkTrack, walkAnimation,
                settings.WalkStepsAcrossRange, settings.WalkStepsPerLoop);

            _walkTrack.Start(MixBlend.Replace);

            _runTrack = new PositionLockedTrack(state, RunTrack, runAnimation,
                settings.RunStepsAcrossRange, settings.RunStepsPerLoop);

            _runTrack.Start(MixBlend.Replace);
        }

        public void Tick(float currentX, float currentSpeed, HorizontalRange range)
        {
            _smoothedSpeed = Mathf.SmoothDamp(_smoothedSpeed, Mathf.Abs(currentSpeed), ref _smoothedSpeedVelocity, settings.SpeedSmoothTime);

            var walkWeight = Mathf.Clamp01(settings.WalkWeightCurve.Evaluate(_smoothedSpeed));
            var runWeight = Mathf.Clamp01(settings.RunWeightCurve.Evaluate(_smoothedSpeed));

            if (_smoothedSpeed < settings.IdleSpeedThreshold)
            {
                walkWeight = 0f;
                runWeight = 0f;
            }

            _idleEntry.Alpha = 1f;
            _walkTrack.SetAlpha(walkWeight);
            _runTrack.SetAlpha(runWeight);

            var normalizedX = range.Normalize(currentX);
            var invertPhase = skeletonAnimation.Skeleton.ScaleX < 0f;
            _walkTrack.Apply(normalizedX, invertPhase);
            _runTrack.Apply(normalizedX, invertPhase);
        }
    }
}
