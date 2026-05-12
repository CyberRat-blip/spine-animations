using Gameplay.Config;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace Gameplay.Aim
{
    [RequireComponent(typeof(SkeletonAnimation))]
    public sealed class SpineAimController : MonoBehaviour, IAimController
    {
        [SerializeField] private SkeletonAnimation skeletonAnimation;
        [SerializeField] private PlayerSettings settings;

        [SpineAnimation][SerializeField] private string aimAnimation = "aim";
        [SpineBone][SerializeField] private string crosshairBoneName = "crosshair";

        [SpineSlot][SerializeField] private string crosshairSlotName = "crosshair";

        [SpineIkConstraint][SerializeField] private string aimIkConstraintName = "aim-ik";

        [SerializeField] private int aimTrackIndex = 3;

        private TrackEntry _aimEntry;
        private Bone _crosshairBone;
        private Slot _crosshairSlot;
        private IkConstraint _aimIk;
        private IAimPoseStrategy _bentPose;
        private IAimPoseStrategy _straightPose;

        private float _setupCrosshairX;
        private float _setupCrosshairY;

        private float _currentAlpha;

        public bool IsAiming { get; private set; }

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

            _bentPose = new BentArmPoseStrategy(settings.BentDistance);
            _straightPose = new StraightArmPoseStrategy(settings.StraightDistance);
        }

        private void Start()
        {
            var state = skeletonAnimation.AnimationState;

            _aimEntry = state.SetAnimation(aimTrackIndex, aimAnimation, loop: true);
            _aimEntry.MixBlend = MixBlend.Add;
            _aimEntry.Alpha = 0f;

            var skeleton = skeletonAnimation.Skeleton;
            _crosshairBone = skeleton.FindBone(crosshairBoneName);
            _crosshairSlot = skeleton.FindSlot(crosshairSlotName);
            _aimIk = skeleton.FindIkConstraint(aimIkConstraintName);

            if (_crosshairBone != null)
            {
                _setupCrosshairX = _crosshairBone.Data.X;
                _setupCrosshairY = _crosshairBone.Data.Y;
            }

            if (_aimIk != null)
            {
                _aimIk.Mix = 0f;
            }
            else
            {
                Debug.LogWarning($"[SpineAimController] Кость '{aimIkConstraintName}' не найдена в скелете. ");
            }
        }

        private void OnEnable()
        {
            if (skeletonAnimation == null)
            {
                skeletonAnimation = GetComponent<SkeletonAnimation>();
            }

            skeletonAnimation.UpdateLocal += AfterAnimationApply;
            skeletonAnimation.UpdateComplete += HideCrosshairAttachment;
        }

        private void OnDisable()
        {
            if (skeletonAnimation != null)
            {
                skeletonAnimation.UpdateLocal -= AfterAnimationApply;
                skeletonAnimation.UpdateComplete -= HideCrosshairAttachment;
            }
        }

        private void HideCrosshairAttachment(ISkeletonAnimation _)
        {
            if (_crosshairSlot != null)
            {
                _crosshairSlot.Attachment = null;
            }
        }

        private void AfterAnimationApply(ISkeletonAnimation _)
        {
            if (_aimIk != null)
            {
                _aimIk.Mix = _currentAlpha;
            }
        }

        public void Tick(bool isAimingHeld, Vector2 cursorWorldPosition, Vector3 playerWorldPosition)
        {
            IsAiming = isAimingHeld;

            var targetAlpha = isAimingHeld ? 1f : 0f;
            var fadeSpeed = 1f / settings.AimFadeTime;
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);
            if (_aimEntry != null)
            {
                _aimEntry.Alpha = _currentAlpha;
            }


            if (_crosshairBone == null)
            {
                return;
            }

            UpdateCrosshairBone(cursorWorldPosition, playerWorldPosition);
        }

        private void UpdateCrosshairBone(Vector2 cursorWorld, Vector3 playerWorld)
        {
            var aimed = ComputeAimedOffset(cursorWorld, playerWorld);

            var setup = new Vector2(_setupCrosshairX, _setupCrosshairY);
            var blended = Vector2.Lerp(setup, aimed, _currentAlpha);

            _crosshairBone.X = blended.x;
            _crosshairBone.Y = blended.y;
        }

        private Vector2 ComputeAimedOffset(Vector2 cursorWorld, Vector3 playerWorld)
        {
            var localDirection = cursorWorld - (Vector2)playerWorld;

            var verticalOffset = cursorWorld.y - playerWorld.y;
            var lower = settings.AimHeightThreshold - settings.AimSmoothBand;
            var upper = settings.AimHeightThreshold + settings.AimSmoothBand;
            var t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lower, upper, verticalOffset));

            var bent = _bentPose.ResolveCrosshairOffset(localDirection);
            var straight = _straightPose.ResolveCrosshairOffset(localDirection);
            return Vector2.Lerp(bent, straight, t);
        }
    }
}
