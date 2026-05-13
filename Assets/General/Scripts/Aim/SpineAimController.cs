using System;
using Gameplay.Config;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace Gameplay.Aim
{
    [RequireComponent(typeof(SkeletonAnimation))]
    public sealed class SpineAimController : MonoBehaviour, IAimController
    {
        private const bool AutoFlipFacing = true;
        private const float FacingAimDeadZone = 0.1f;
        private const float FacingMovementThreshold = 0.1f;

        [System.Serializable]
        private struct AltRotateBone
        {
            [SpineBone] public string boneName;
            [Range(0f, 1f)] public float weight;
        }

        [System.Serializable]
        private struct AltLockedBone
        {
            [SpineBone] public string boneName;
        }

        [SerializeField] private SkeletonAnimation skeletonAnimation;
        [SerializeField] private PlayerSettings settings;

        [Header("Standert Aim")]
        [SpineAnimation][SerializeField] private string aimAnimation = "aim";
        [SpineIkConstraint][SerializeField] private string aimIk = "aim-ik";
        [SerializeField] private int aimTrackIndex = 3;

        [Header("Alt Aim")]
        [SerializeField] private AltRotateBone[] altRotateBones;

        [SerializeField] private AltLockedBone[] altLockedBones;

        [Range(-90f, 90f)]
        [SerializeField] private float altAimAngleOffset = 0f;

        [Header("Порог по высоте курсора")]
        [SerializeField] private float aimSpineWhenCursorAboveOffsetY = 0.35f;

        [Header("Crosshair")]
        [SpineBone][SerializeField] private string crosshairBoneName = "crosshair";
        [SpineSlot][SerializeField] private string crosshairSlotName = "crosshair";

        private TrackEntry _aimTrackEntry;
        private Bone _crosshairBone;
        private Slot _crosshairSlot;

        private Bone[] _cachedAltRotateBones;
        private float[] _cachedAltRotateWeights;
        private Bone[] _cachedAltLockedBones;

        private bool _initialized;
        private bool _useSpineAimAnimation;

        private float _crosshairSetupPoseLocalX;
        private float _crosshairSetupPoseLocalY;
        private Vector2 _cursorLocalInSkeletonSpace;
        private float _aimBlendAlpha;

        private float _lastAltAimSkeletonScaleXSign = 1f;

        private bool _shouldResetAltBonesBeforeNextAnimationApply;

        public bool IsAiming { get; private set; }

        public float AimSpineWhenCursorAboveOffsetY => aimSpineWhenCursorAboveOffsetY;

        public float AimSpineThresholdWorldY => skeletonAnimation != null
            ? skeletonAnimation.transform.position.y + aimSpineWhenCursorAboveOffsetY
            : transform.position.y + aimSpineWhenCursorAboveOffsetY;

        public bool UsesSpineAimAnimation => _useSpineAimAnimation;

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
            ApplyAimAnimation();

            var skeleton = skeletonAnimation.Skeleton;
            _crosshairBone = skeleton.FindBone(crosshairBoneName);
            if (!string.IsNullOrEmpty(crosshairSlotName))
            {
                _crosshairSlot = skeleton.FindSlot(crosshairSlotName);
            }

            if (_crosshairBone != null)
            {
                _crosshairSetupPoseLocalX = _crosshairBone.Data.X;
                _crosshairSetupPoseLocalY = _crosshairBone.Data.Y;
                _cursorLocalInSkeletonSpace = new Vector2(_crosshairSetupPoseLocalX, _crosshairSetupPoseLocalY);
            }

            CacheAltBones(skeleton);
            SortAltRotateBonesByDepth();
            _lastAltAimSkeletonScaleXSign = Mathf.Sign(skeleton.ScaleX);
            if (_lastAltAimSkeletonScaleXSign == 0f)
            {
                _lastAltAimSkeletonScaleXSign = 1f;
            }

            _initialized = true;
        }

        private void CacheAltBones(Skeleton skeleton)
        {
            if (altRotateBones != null && altRotateBones.Length > 0)
            {
                _cachedAltRotateBones = new Bone[altRotateBones.Length];
                _cachedAltRotateWeights = new float[altRotateBones.Length];
                for (var i = 0; i < altRotateBones.Length; i++)
                {
                    var configuredBoneName = altRotateBones[i].boneName;
                    _cachedAltRotateBones[i] = string.IsNullOrEmpty(configuredBoneName)
                        ? null
                        : skeleton.FindBone(configuredBoneName);
                    _cachedAltRotateWeights[i] = altRotateBones[i].weight;
                }
            }

            if (altLockedBones != null && altLockedBones.Length > 0)
            {
                _cachedAltLockedBones = new Bone[altLockedBones.Length];
                for (var i = 0; i < altLockedBones.Length; i++)
                {
                    var configuredBoneName = altLockedBones[i].boneName;
                    _cachedAltLockedBones[i] = string.IsNullOrEmpty(configuredBoneName)
                        ? null
                        : skeleton.FindBone(configuredBoneName);
                }
            }
        }

        private static int GetBoneDepth(Bone bone)
        {
            var depth = 0;
            while (bone != null)
            {
                depth++;
                bone = bone.Parent;
            }

            return depth;
        }

        private void SortAltRotateBonesByDepth()
        {
            if (_cachedAltRotateBones == null || _cachedAltRotateBones.Length < 2)
            {
                return;
            }

            var boneCount = _cachedAltRotateBones.Length;
            var sortOrderByDepth = new int[boneCount];
            for (var i = 0; i < boneCount; i++)
            {
                sortOrderByDepth[i] = i;
            }

            Array.Sort(sortOrderByDepth, (indexA, indexB) =>
                GetBoneDepth(_cachedAltRotateBones[indexA]).CompareTo(GetBoneDepth(_cachedAltRotateBones[indexB])));

            var reorderedBones = new Bone[boneCount];
            var reorderedWeights = new float[boneCount];
            for (var i = 0; i < boneCount; i++)
            {
                var sourceIndex = sortOrderByDepth[i];
                reorderedBones[i] = _cachedAltRotateBones[sourceIndex];
                reorderedWeights[i] = _cachedAltRotateWeights[sourceIndex];
            }

            _cachedAltRotateBones = reorderedBones;
            _cachedAltRotateWeights = reorderedWeights;
        }

        private void OnEnable()
        {
            if (skeletonAnimation == null)
            {
                skeletonAnimation = GetComponent<SkeletonAnimation>();
            }

            skeletonAnimation.BeforeApply += BeforeAnimationApply;
            skeletonAnimation.UpdateLocal += AfterAnimationApply;
        }

        private void OnDisable()
        {
            if (skeletonAnimation != null)
            {
                skeletonAnimation.BeforeApply -= BeforeAnimationApply;
                skeletonAnimation.UpdateLocal -= AfterAnimationApply;
            }
        }

        private void BeforeAnimationApply(ISkeletonAnimation _)
        {
            if (!_shouldResetAltBonesBeforeNextAnimationApply)
            {
                return;
            }

            _shouldResetAltBonesBeforeNextAnimationApply = false;
            ResetAltAffectedBonesToSetupPose();
        }

        private void ApplyAimAnimation()
        {
            var state = skeletonAnimation.AnimationState;
            if (_useSpineAimAnimation && !string.IsNullOrEmpty(aimAnimation))
            {
                _aimTrackEntry = state.SetAnimation(aimTrackIndex, aimAnimation, loop: true);
                _aimTrackEntry.MixBlend = MixBlend.Add;
                _aimTrackEntry.Alpha = _aimBlendAlpha;
            }
            else
            {
                _aimTrackEntry = null;
                state.SetEmptyAnimation(aimTrackIndex, 0f);
            }
        }

        private void ResetAimIk()
        {
            if (string.IsNullOrEmpty(aimIk))
            {
                return;
            }

            var ik = skeletonAnimation.Skeleton.FindIkConstraint(aimIk);
            if (ik != null)
            {
                ik.Mix = ik.Data.Mix;
            }
        }

        private void SetSpineVersusAltMode(bool wantSpineAim)
        {
            if (_useSpineAimAnimation == wantSpineAim)
            {
                return;
            }

            var wasSpineAimAnimationActive = _useSpineAimAnimation;
            _useSpineAimAnimation = wantSpineAim;

            if (!_initialized)
            {
                return;
            }

            ApplyAimAnimation();
            if (wasSpineAimAnimationActive)
            {
                ResetAimIk();
            }
        }

        private void AfterAnimationApply(ISkeletonAnimation _)
        {
            if (_useSpineAimAnimation && _aimBlendAlpha <= 0f)
            {
                ResetAimIk();
            }

            if (!_useSpineAimAnimation && _aimBlendAlpha > 0f)
            {
                ApplyAltAim(_aimBlendAlpha);
            }

            if (_crosshairBone != null)
            {
                var crosshairSetupLocal = new Vector2(_crosshairSetupPoseLocalX, _crosshairSetupPoseLocalY);
                var blendedCrosshairLocal = Vector2.Lerp(crosshairSetupLocal, _cursorLocalInSkeletonSpace, _aimBlendAlpha);
                _crosshairBone.X = blendedCrosshairLocal.x;
                _crosshairBone.Y = blendedCrosshairLocal.y;
            }

            if (_crosshairSlot != null && _useSpineAimAnimation)
            {
                _crosshairSlot.Attachment = null;
            }
        }

        private void ResetAltAffectedBonesToSetupPose()
        {
            if (_cachedAltLockedBones != null)
            {
                for (var i = 0; i < _cachedAltLockedBones.Length; i++)
                {
                    var bone = _cachedAltLockedBones[i];
                    if (bone != null)
                    {
                        LerpBoneToSetup(bone, 1f);
                    }
                }
            }

            if (_cachedAltRotateBones == null)
            {
                return;
            }

            for (var i = 0; i < _cachedAltRotateBones.Length; i++)
            {
                var bone = _cachedAltRotateBones[i];
                if (bone != null)
                {
                    LerpBoneToSetup(bone, 1f);
                }
            }
        }

        private void ApplyAltAim(float alpha)
        {
            if (_cachedAltLockedBones != null)
            {
                for (var i = 0; i < _cachedAltLockedBones.Length; i++)
                {
                    var bone = _cachedAltLockedBones[i];
                    if (bone == null)
                    {
                        continue;
                    }

                    LerpBoneToSetup(bone, alpha);
                }
            }

            if (_cachedAltRotateBones == null)
            {
                return;
            }

            var skeleton = skeletonAnimation.Skeleton;
            var skeletonScaleXSign = Mathf.Sign(skeleton.ScaleX);
            if (skeletonScaleXSign == 0f)
            {
                skeletonScaleXSign = 1f;
            }

            if (Mathf.Abs(skeletonScaleXSign - _lastAltAimSkeletonScaleXSign) > 0.01f)
            {
                for (var i = 0; i < _cachedAltRotateBones.Length; i++)
                {
                    var bone = _cachedAltRotateBones[i];
                    if (bone != null)
                    {
                        LerpBoneToSetup(bone, 1f);
                    }
                }
            }

            _lastAltAimSkeletonScaleXSign = skeletonScaleXSign;

            skeleton.UpdateWorldTransform();

            for (var i = 0; i < _cachedAltRotateBones.Length; i++)
            {
                var bone = _cachedAltRotateBones[i];
                if (bone == null)
                {
                    continue;
                }

                var rotationBlendWeight = _cachedAltRotateWeights[i] * alpha;
                if (rotationBlendWeight <= 0f)
                {
                    continue;
                }

                RotateBoneTowards(bone, _cursorLocalInSkeletonSpace, rotationBlendWeight, altAimAngleOffset);
            }
        }

        private static void LerpBoneToSetup(Bone bone, float weight)
        {
            var data = bone.Data;
            bone.Rotation = Mathf.LerpAngle(bone.Rotation, data.Rotation, weight);
            bone.X = Mathf.Lerp(bone.X, data.X, weight);
            bone.Y = Mathf.Lerp(bone.Y, data.Y, weight);
            bone.ScaleX = Mathf.Lerp(bone.ScaleX, data.ScaleX, weight);
            bone.ScaleY = Mathf.Lerp(bone.ScaleY, data.ScaleY, weight);
            bone.ShearX = Mathf.Lerp(bone.ShearX, data.ShearX, weight);
            bone.ShearY = Mathf.Lerp(bone.ShearY, data.ShearY, weight);
        }

        private static void RotateBoneTowards(Bone bone, Vector2 targetSkeletonLocal, float weight, float worldOffsetDegrees)
        {
            var deltaX = targetSkeletonLocal.x - bone.WorldX;
            var deltaY = targetSkeletonLocal.y - bone.WorldY;
            if (Mathf.Approximately(deltaX, 0f) && Mathf.Approximately(deltaY, 0f))
            {
                return;
            }

            var boneSkeleton = bone.Skeleton;
            var flipSign = Mathf.Sign(boneSkeleton.ScaleX * boneSkeleton.ScaleY);
            if (flipSign == 0f)
            {
                flipSign = 1f;
            }

            var directionYForAtan2 = deltaY * flipSign;
            var desiredWorldRotationDegrees = Mathf.Atan2(directionYForAtan2, deltaX) * Mathf.Rad2Deg + worldOffsetDegrees;

            var parent = bone.Parent;
            var parentWorldRotationDegrees = parent != null ? parent.WorldRotationX : 0f;
            var parentDeterminantSign = parent != null
                ? Mathf.Sign(parent.A * parent.D - parent.B * parent.C)
                : Mathf.Sign(bone.Skeleton.ScaleX * bone.Skeleton.ScaleY);

            if (parentDeterminantSign == 0f)
            {
                parentDeterminantSign = 1f;
            }

            var desiredLocalRotationDegrees =
                (desiredWorldRotationDegrees - parentWorldRotationDegrees) * parentDeterminantSign;
            bone.Rotation = Mathf.LerpAngle(bone.Rotation, desiredLocalRotationDegrees, weight);
        }

        public void UpdateFacing(bool isAimingHeld, float cursorWorldX, float horizontalVelocity)
        {
            if (!AutoFlipFacing)
            {
                return;
            }

            var desiredSign = 0;

            var preferCursorFacing = isAimingHeld || _aimBlendAlpha > 0f;

            if (preferCursorFacing)
            {
                var cursorOffsetX = cursorWorldX - skeletonAnimation.transform.position.x;
                if (Mathf.Abs(cursorOffsetX) > FacingAimDeadZone)
                {
                    desiredSign = cursorOffsetX > 0f ? 1 : -1;
                }
            }
            else if (Mathf.Abs(horizontalVelocity) > FacingMovementThreshold)
            {
                desiredSign = horizontalVelocity > 0f ? 1 : -1;
            }

            if (desiredSign == 0)
            {
                return;
            }

            var skeleton = skeletonAnimation.Skeleton;
            var scaleXMagnitude = Mathf.Abs(skeleton.ScaleX);
            if (scaleXMagnitude < 0.0001f)
            {
                scaleXMagnitude = 1f;
            }

            skeleton.ScaleX = scaleXMagnitude * desiredSign;
        }

        public void Tick(bool isAimingHeld, Vector2 cursorWorldPosition)
        {
            IsAiming = isAimingHeld;

            var characterWorldPosition = skeletonAnimation.transform.position;
            var cursorAbove = cursorWorldPosition.y - characterWorldPosition.y;
            var wantSpineAim = cursorAbove > aimSpineWhenCursorAboveOffsetY;
            SetSpineVersusAltMode(wantSpineAim);

            var targetAlpha = isAimingHeld ? 1f : 0f;
            var fadeSpeed = 1f / settings.AimFadeTime;
            var previousAimBlendAlpha = _aimBlendAlpha;
            _aimBlendAlpha = Mathf.MoveTowards(_aimBlendAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

            if (!_useSpineAimAnimation && previousAimBlendAlpha > 0f && _aimBlendAlpha <= 0f)
            {
                _shouldResetAltBonesBeforeNextAnimationApply = true;
            }

            if (_aimTrackEntry != null)
            {
                _aimTrackEntry.Alpha = _aimBlendAlpha;
            }

            var skeleton = skeletonAnimation.Skeleton;
            var cursorWorldPosition3 = new Vector3(cursorWorldPosition.x, cursorWorldPosition.y, 0f);
            var cursorInCharacterLocalSpace = skeletonAnimation.transform.InverseTransformPoint(cursorWorldPosition3);
            cursorInCharacterLocalSpace.x *= skeleton.ScaleX;
            cursorInCharacterLocalSpace.y *= skeleton.ScaleY;
            _cursorLocalInSkeletonSpace = new Vector2(cursorInCharacterLocalSpace.x, cursorInCharacterLocalSpace.y);
        }
    }
}
