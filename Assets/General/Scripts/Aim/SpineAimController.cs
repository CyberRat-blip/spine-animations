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

        [SerializeField] private int aimTrackIndex = 3;

        private TrackEntry _aimEntry;
        private Bone _crosshairBone;
        private Slot _crosshairSlot;

        private float _setupCrosshairX;
        private float _setupCrosshairY;
        private Vector2 _cursorSkelLocal;

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

            if (_crosshairBone != null)
            {
                _setupCrosshairX = _crosshairBone.Data.X;
                _setupCrosshairY = _crosshairBone.Data.Y;
                _cursorSkelLocal = new Vector2(_setupCrosshairX, _setupCrosshairY);
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
            if (_crosshairBone == null)
            {
                return;
            }

            var setup = new Vector2(_setupCrosshairX, _setupCrosshairY);
            var blended = Vector2.Lerp(setup, _cursorSkelLocal, _currentAlpha);
            _crosshairBone.X = blended.x;
            _crosshairBone.Y = blended.y;
        }

        public void Tick(bool isAimingHeld, Vector2 cursorWorldPosition)
        {
            IsAiming = isAimingHeld;

            var targetAlpha = isAimingHeld ? 1f : 0f;
            var fadeSpeed = 1f / settings.AimFadeTime;
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);
            if (_aimEntry != null)
            {
                _aimEntry.Alpha = _currentAlpha;
            }

            var skeleton = skeletonAnimation.Skeleton;
            var cursorWorld3 = new Vector3(cursorWorldPosition.x, cursorWorldPosition.y, 0f);
            var local = skeletonAnimation.transform.InverseTransformPoint(cursorWorld3);
            local.x *= skeleton.ScaleX;
            local.y *= skeleton.ScaleY;
            _cursorSkelLocal = new Vector2(local.x, local.y);
        }
    }
}
