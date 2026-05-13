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

        [Header("Aim - Spine анимация + IK")]
        [SpineAnimation][SerializeField] private string aimAnimation = "aim";
        [SpineIkConstraint][SerializeField] private string aimIk = "aim-ik";
        [SerializeField] private int aimTrackIndex = 3;

        [Header("Alt - код крутит кости")]
        [SerializeField] private AltRotateBone[] altRotateBones;

        [SerializeField] private AltLockedBone[] altLockedBones;

        [Range(-90f, 90f)]
        [SerializeField] private float altAimAngleOffset = 0f;

        [Header("Порог по высоте курсора")]
        [SerializeField] private float aimSpineWhenCursorAboveOffsetY = 0.35f;

        [Header("Crosshair")]
        [SpineBone][SerializeField] private string crosshairBoneName = "crosshair";

        private TrackEntry _aimEntry;
        private Bone _crosshairBone;

        private Bone[] _altRotateRefs;
        private float[] _altRotateWeights;
        private Bone[] _altLockedRefs;

        private bool _initialized;
        private bool _spineAimActive;

        private float _setupCrosshairX;
        private float _setupCrosshairY;
        private Vector2 _cursorSkelLocal;
        private float _currentAlpha;

        private float _lastApplyAltScaleSign = 1f;

        public bool IsAiming { get; private set; }

        public float AimSpineWhenCursorAboveOffsetY => aimSpineWhenCursorAboveOffsetY;

        public float AimSpineThresholdWorldY => skeletonAnimation != null
            ? skeletonAnimation.transform.position.y + aimSpineWhenCursorAboveOffsetY
            : transform.position.y + aimSpineWhenCursorAboveOffsetY;

        public bool UsesSpineAimAnimation => _spineAimActive;

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
            if (_crosshairBone != null)
            {
                _setupCrosshairX = _crosshairBone.Data.X;
                _setupCrosshairY = _crosshairBone.Data.Y;
                _cursorSkelLocal = new Vector2(_setupCrosshairX, _setupCrosshairY);
            }

            CacheAltBones(skeleton);
            SortAltRotateBonesByDepth();
            _lastApplyAltScaleSign = Mathf.Sign(skeleton.ScaleX);
            if (_lastApplyAltScaleSign == 0f)
            {
                _lastApplyAltScaleSign = 1f;
            }

            _initialized = true;
        }

        private void CacheAltBones(Skeleton skeleton)
        {
            if (altRotateBones != null && altRotateBones.Length > 0)
            {
                _altRotateRefs = new Bone[altRotateBones.Length];
                _altRotateWeights = new float[altRotateBones.Length];
                for (var i = 0; i < altRotateBones.Length; i++)
                {
                    var name = altRotateBones[i].boneName;
                    _altRotateRefs[i] = string.IsNullOrEmpty(name) ? null : skeleton.FindBone(name);
                    _altRotateWeights[i] = altRotateBones[i].weight;
                }
            }

            if (altLockedBones != null && altLockedBones.Length > 0)
            {
                _altLockedRefs = new Bone[altLockedBones.Length];
                for (var i = 0; i < altLockedBones.Length; i++)
                {
                    var name = altLockedBones[i].boneName;
                    _altLockedRefs[i] = string.IsNullOrEmpty(name) ? null : skeleton.FindBone(name);
                }
            }
        }

        private static int GetBoneDepth(Bone bone)
        {
            var d = 0;
            while (bone != null)
            {
                d++;
                bone = bone.Parent;
            }

            return d;
        }

        private void SortAltRotateBonesByDepth()
        {
            if (_altRotateRefs == null || _altRotateRefs.Length < 2)
            {
                return;
            }

            var n = _altRotateRefs.Length;
            var order = new int[n];
            for (var i = 0; i < n; i++)
            {
                order[i] = i;
            }

            Array.Sort(order, (a, b) =>
                GetBoneDepth(_altRotateRefs[a]).CompareTo(GetBoneDepth(_altRotateRefs[b])));

            var newRefs = new Bone[n];
            var newWeights = new float[n];
            for (var i = 0; i < n; i++)
            {
                var j = order[i];
                newRefs[i] = _altRotateRefs[j];
                newWeights[i] = _altRotateWeights[j];
            }

            _altRotateRefs = newRefs;
            _altRotateWeights = newWeights;
        }

        private void OnEnable()
        {
            if (skeletonAnimation == null)
            {
                skeletonAnimation = GetComponent<SkeletonAnimation>();
            }

            skeletonAnimation.UpdateLocal += AfterAnimationApply;
        }

        private void OnDisable()
        {
            if (skeletonAnimation != null)
            {
                skeletonAnimation.UpdateLocal -= AfterAnimationApply;
            }
        }

        private void ApplyAimAnimation()
        {
            var state = skeletonAnimation.AnimationState;
            if (_spineAimActive && !string.IsNullOrEmpty(aimAnimation))
            {
                _aimEntry = state.SetAnimation(aimTrackIndex, aimAnimation, loop: true);
                _aimEntry.MixBlend = MixBlend.Add;
                _aimEntry.Alpha = _currentAlpha;
            }
            else
            {
                _aimEntry = null;
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
            if (_spineAimActive == wantSpineAim)
            {
                return;
            }

            var previous = _spineAimActive;
            _spineAimActive = wantSpineAim;

            if (!_initialized)
            {
                return;
            }

            ApplyAimAnimation();
            if (previous)
            {
                ResetAimIk();
            }
        }

        private void AfterAnimationApply(ISkeletonAnimation _)
        {
            if (_spineAimActive && _currentAlpha <= 0f)
            {
                ResetAimIk();
            }

            if (!_spineAimActive && _currentAlpha > 0f)
            {
                ApplyAltAim(_currentAlpha);
            }

            if (_crosshairBone == null)
            {
                return;
            }

            var setup = new Vector2(_setupCrosshairX, _setupCrosshairY);
            var blended = Vector2.Lerp(setup, _cursorSkelLocal, _currentAlpha);
            _crosshairBone.X = blended.x;
            _crosshairBone.Y = blended.y;
        }

        private void ApplyAltAim(float alpha)
        {
            if (_altLockedRefs != null)
            {
                for (var i = 0; i < _altLockedRefs.Length; i++)
                {
                    var bone = _altLockedRefs[i];
                    if (bone == null)
                    {
                        continue;
                    }

                    LerpBoneToSetup(bone, alpha);
                }
            }

            if (_altRotateRefs == null)
            {
                return;
            }

            var skeleton = skeletonAnimation.Skeleton;
            var sxSign = Mathf.Sign(skeleton.ScaleX);
            if (sxSign == 0f)
            {
                sxSign = 1f;
            }

            if (Mathf.Abs(sxSign - _lastApplyAltScaleSign) > 0.01f)
            {
                for (var i = 0; i < _altRotateRefs.Length; i++)
                {
                    var bone = _altRotateRefs[i];
                    if (bone != null)
                    {
                        LerpBoneToSetup(bone, 1f);
                    }
                }
            }

            _lastApplyAltScaleSign = sxSign;

            skeleton.UpdateWorldTransform();

            for (var i = 0; i < _altRotateRefs.Length; i++)
            {
                var bone = _altRotateRefs[i];
                if (bone == null)
                {
                    continue;
                }

                var w = _altRotateWeights[i] * alpha;
                if (w <= 0f)
                {
                    continue;
                }

                RotateBoneTowards(bone, _cursorSkelLocal, w, altAimAngleOffset);
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

        private static void RotateBoneTowards(Bone bone, Vector2 targetSkeletonLocal, float weight, float worldOffsetDeg)
        {
            var dx = targetSkeletonLocal.x - bone.WorldX;
            var dy = targetSkeletonLocal.y - bone.WorldY;
            if (Mathf.Approximately(dx, 0f) && Mathf.Approximately(dy, 0f))
            {
                return;
            }

            var sk = bone.Skeleton;
            var flip = Mathf.Sign(sk.ScaleX * sk.ScaleY);
            if (flip == 0f)
            {
                flip = 1f;
            }

            var dyAim = dy * flip;
            var desiredWorldDeg = Mathf.Atan2(dyAim, dx) * Mathf.Rad2Deg + worldOffsetDeg;

            var parent = bone.Parent;
            var parentWorldDeg = parent != null ? parent.WorldRotationX : 0f;
            var sign = parent != null
                ? Mathf.Sign(parent.A * parent.D - parent.B * parent.C)
                : Mathf.Sign(bone.Skeleton.ScaleX * bone.Skeleton.ScaleY);
            if (sign == 0f)
            {
                sign = 1f;
            }

            var desiredLocal = (desiredWorldDeg - parentWorldDeg) * sign;
            bone.Rotation = Mathf.LerpAngle(bone.Rotation, desiredLocal, weight);
        }

        public void UpdateFacing(bool isAimingHeld, float cursorWorldX, float horizontalVelocity)
        {
            if (!AutoFlipFacing)
            {
                return;
            }

            var desiredSign = 0;

            var preferCursorFacing = isAimingHeld || _currentAlpha > 0f;

            if (preferCursorFacing)
            {
                var dx = cursorWorldX - skeletonAnimation.transform.position.x;
                if (Mathf.Abs(dx) > FacingAimDeadZone)
                {
                    desiredSign = dx > 0f ? 1 : -1;
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
            var magnitude = Mathf.Abs(skeleton.ScaleX);
            if (magnitude < 0.0001f)
            {
                magnitude = 1f;
            }

            skeleton.ScaleX = magnitude * desiredSign;
        }

        public void Tick(bool isAimingHeld, Vector2 cursorWorldPosition)
        {
            IsAiming = isAimingHeld;

            var charPos = skeletonAnimation.transform.position;
            var cursorAbove = cursorWorldPosition.y - charPos.y;
            var wantSpineAim = cursorAbove > aimSpineWhenCursorAboveOffsetY;
            SetSpineVersusAltMode(wantSpineAim);

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
