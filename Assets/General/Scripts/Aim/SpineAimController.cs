using Gameplay.Config;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace Gameplay.Aim
{
    public enum AimMode
    {
        Aim,
        Alt,
    }

    [RequireComponent(typeof(SkeletonAnimation))]
    public sealed class SpineAimController : MonoBehaviour, IAimController
    {
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

        [Header("Aim Mode")]
        [SerializeField] private AimMode initialAimMode = AimMode.Aim;

        [Header("Aim - анимация в Spine + IK на руку")]
        [SpineAnimation][SerializeField] private string aimAnimation = "aim";
        [SpineIkConstraint][SerializeField] private string aimIk = "aim-ik";
        [SerializeField] private int aimTrackIndex = 3;

        [Header("Alt - без Spine: код фиксирует и крутит кости")]
        [Tooltip("Кости, которые разворачиваются к прицелу (например torso/chest/head). Их сумма weight задаёт долю общего поворота, обычно 1 на главной кости.")]
        [SerializeField] private AltRotateBone[] altRotateBones;

        [Tooltip("Кости, которые лочатся в setup-позу при прицеливании (обычно обе руки, чтобы они не дёргались под беговую анимацию).")]
        [SerializeField] private AltLockedBone[] altLockedBones;

        [Tooltip("Угловое смещение прицеливания в градусах. Положительное значение поднимает аим выше курсора, отрицательное опускает. Работает одинаково в обе стороны флипа, потому что skeleton.ScaleX = -1 зеркалит только X, Y не трогает.")]
        [Range(-90f, 90f)]
        [SerializeField] private float altAimAngleOffset = 0f;

        [Header("Facing - автоповорот персонажа")]
        [Tooltip("При прицеливании смотрит в сторону курсора, при беге - в сторону движения. На детерминированную локомоцию не влияет.")]
        [SerializeField] private bool autoFlip = true;

        [Tooltip("Мёртвая зона в мировых юнитах: насколько курсор должен уйти от центра персонажа, чтобы инициировать разворот при прицеливании.")]
        [SerializeField] private float facingAimDeadZone = 0.1f;

        [Tooltip("Минимальная горизонтальная скорость, при которой персонаж разворачивается по направлению движения.")]
        [SerializeField] private float facingMovementThreshold = 0.1f;

        [Header("Crosshair")]
        [SpineBone][SerializeField] private string crosshairBoneName = "crosshair";

        private TrackEntry _aimEntry;
        private Bone _crosshairBone;

        private Bone[] _altRotateRefs;
        private float[] _altRotateWeights;
        private Bone[] _altLockedRefs;

        private AimMode _aimMode;
        private bool _initialized;

        private float _setupCrosshairX;
        private float _setupCrosshairY;
        private Vector2 _cursorSkelLocal;
        private float _currentAlpha;

        public bool IsAiming { get; private set; }

        public AimMode Mode
        {
            get => _aimMode;
            set
            {
                if (_aimMode == value)
                {
                    return;
                }

                var previous = _aimMode;
                _aimMode = value;

                if (!_initialized)
                {
                    return;
                }

                ApplyAimAnimation();
                if (previous == AimMode.Aim)
                {
                    ResetAimIk();
                }
            }
        }

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
            _aimMode = initialAimMode;
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
            if (_aimMode == AimMode.Aim && !string.IsNullOrEmpty(aimAnimation))
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

        private void AfterAnimationApply(ISkeletonAnimation _)
        {
            if (_aimMode == AimMode.Aim && _currentAlpha <= 0f)
            {
                ResetAimIk();
            }

            if (_aimMode == AimMode.Alt && _currentAlpha > 0f)
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
            // 1) Фиксируем перечисленные кости в setup-позе. Это просто перезапись local-значений,
            //    мировые трансформы тут ещё не нужны.
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

            // 2) Принудительно пересчитываем мировые трансформы под СВЕЖИЕ local-значения.
            //    Без этого bone.WorldRotationX и bone.WorldX/Y будут от предыдущего кадра, что
            //    замыкается с беговой анимацией в положительную обратную связь и даёт тряску.
            //    Spine после нашего колбэка вызовет UpdateWorldTransform ещё раз - финальный рендер
            //    использует именно вторую итерацию, так что повторная работа здесь не теряется.
            var skeleton = skeletonAnimation.Skeleton;
            skeleton.UpdateWorldTransform();

            // Оффсет задан в "визуальной" системе Unity (положительный = выше). Поскольку
            // skeleton.ScaleX = -1 зеркалит только X (Y не трогает), направление "вверх"
            // совпадает с положительным приращением atan2 в обоих случаях - знак инвертировать
            // НЕ нужно. Раньше тут стоял множитель * sign(ScaleX), и при флипе оффсет
            // вкручивал корпус в обратную сторону.
            var effectiveOffset = altAimAngleOffset;

            // 3) Доворачиваем кости-таргеты к курсору. Здесь bone.WorldRotationX уже текущий,
            //    формула стабильна.
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
                RotateBoneTowards(bone, _cursorSkelLocal, w, effectiveOffset);
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

            var desiredWorldDeg = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + worldOffsetDeg;

            // Локальный поворот = (нужный мировой - мировой родителя), с учётом знака
            // родительской матрицы (определитель < 0 означает отражение - локальный поворот
            // распространяется в обратную сторону).
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
            if (!autoFlip)
            {
                return;
            }

            var desiredSign = 0;

            // Пока аим не успел угаснуть (alpha > 0), фейсинг приоритетно держится на курсоре -
            // иначе при отпускании кнопки фейс мгновенно прыгает на сторону движения, а корпус
            // ещё продолжает довод к курсору, что даёт визуально "согнут не в ту сторону".
            var preferCursorFacing = isAimingHeld || _currentAlpha > 0f;

            if (preferCursorFacing)
            {
                var dx = cursorWorldX - skeletonAnimation.transform.position.x;
                if (Mathf.Abs(dx) > facingAimDeadZone)
                {
                    desiredSign = dx > 0f ? 1 : -1;
                }
            }
            else if (Mathf.Abs(horizontalVelocity) > facingMovementThreshold)
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
