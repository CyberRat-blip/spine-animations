using UnityEngine;

namespace Gameplay.Config
{
    [CreateAssetMenu(fileName = "PlayerSettings", menuName = "Gameplay/Player Settings", order = 0)]
    public sealed class PlayerSettings : ScriptableObject
    {
        [Header("Границы")]
        [SerializeField] private float minX = -8f;
        [SerializeField] private float maxX = 8f;
        [SerializeField] private float yLine = -3f;

        [Header("Горизонтальное движение")]
        [SerializeField] private float smoothTime = 0.35f;
        [SerializeField] private float maxSpeed = 6f;
        [SerializeField] private float arriveEpsilon = 0.02f;
        [SerializeField] private float speedSmoothTime = 0.18f;

        [Header("Локомоция")]
        [SerializeField] private float idleSpeedThreshold = 0.15f;
        [SerializeField] private float walkStepsAcrossRange = 12f;
        [SerializeField] private float walkStepsPerLoop = 2f;

        [SerializeField] private float runStepsAcrossRange = 8f;
        [SerializeField] private float runStepsPerLoop = 2f;

        [SerializeField] private AnimationCurve walkWeightCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.3f, 1f),
            new Keyframe(3f, 1f),
            new Keyframe(6f, 0f));

        [SerializeField] private AnimationCurve runWeightCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(3f, 0f),
            new Keyframe(6f, 1f));

        [Header("Прицеливание")]
        [SerializeField] private float aimFadeTime = 0.15f;

        public float MinX => minX;
        public float MaxX => maxX;
        public float YLine => yLine;
        public float SmoothTime => Mathf.Max(0.0001f, smoothTime);
        public float MaxSpeed => Mathf.Max(0f, maxSpeed);
        public float ArriveEpsilon => Mathf.Max(0f, arriveEpsilon);
        public float SpeedSmoothTime => Mathf.Max(0.0001f, speedSmoothTime);

        public float IdleSpeedThreshold => Mathf.Max(0f, idleSpeedThreshold);
        public float WalkStepsAcrossRange => Mathf.Max(0.0001f, walkStepsAcrossRange);
        public float WalkStepsPerLoop => Mathf.Max(0.0001f, walkStepsPerLoop);
        public float RunStepsAcrossRange => Mathf.Max(0.0001f, runStepsAcrossRange);
        public float RunStepsPerLoop => Mathf.Max(0.0001f, runStepsPerLoop);

        public AnimationCurve WalkWeightCurve => walkWeightCurve;
        public AnimationCurve RunWeightCurve => runWeightCurve;

        public float AimFadeTime => Mathf.Max(0.0001f, aimFadeTime);
    }
}
