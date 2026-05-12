using UnityEngine;

namespace Gameplay.Config
{
    [CreateAssetMenu(fileName = "PlayerSettings", menuName = "Platformer1/Player Settings", order = 0)]
    public sealed class PlayerSettings : ScriptableObject
    {
        [Header("Horizontal range (world units)")]
        [SerializeField] private float minX = -8f;

        [SerializeField] private float maxX = 8f;

        [SerializeField] private float yLine = -3f;

        [Header("Movement")]
        [SerializeField] private float smoothTime = 0.35f;

        [SerializeField] private float maxSpeed = 6f;

        [SerializeField] private float idleSpeedThreshold = 0.15f;

        [SerializeField] private float arriveEpsilon = 0.02f;

        [SerializeField] private float speedSmoothTime = 0.18f;

        [Header("Walk animation")]
        [SerializeField] private float walkStepsAcrossRange = 12f;

        [SerializeField] private float walkStepsPerLoop = 2f;

        [Header("Run animation")]
        [SerializeField] private float runStepsAcrossRange = 8f;

        [SerializeField] private float runStepsPerLoop = 2f;

        [Header("Locomotion blend (weights by |speed|)")]
        [SerializeField] private AnimationCurve walkWeightCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.3f, 1f),
            new Keyframe(3f, 1f),
            new Keyframe(6f, 0f));

        [SerializeField] private AnimationCurve runWeightCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(3f, 0f),
            new Keyframe(6f, 1f));

        [Header("Aim")]
        [SerializeField] private float aimFadeTime = 0.15f;

        [SerializeField] private float characterHeight = 2f;

        [SerializeField] private float aimHeightThresholdMultiplier = 2f;

        [SerializeField] private float aimSmoothBand = 0.6f;

        [SerializeField] private float bentDistance = 0.6f;

        [SerializeField] private float straightDistance = 4f;

        public float MinX => minX;
        public float MaxX => maxX;
        public float YLine => yLine;
        public float SmoothTime => Mathf.Max(0.0001f, smoothTime);
        public float MaxSpeed => Mathf.Max(0f, maxSpeed);
        public float IdleSpeedThreshold => Mathf.Max(0f, idleSpeedThreshold);
        public float ArriveEpsilon => Mathf.Max(0f, arriveEpsilon);
        public float SpeedSmoothTime => Mathf.Max(0.0001f, speedSmoothTime);

        public float WalkStepsAcrossRange => Mathf.Max(0.0001f, walkStepsAcrossRange);
        public float WalkStepsPerLoop => Mathf.Max(0.0001f, walkStepsPerLoop);
        public float RunStepsAcrossRange => Mathf.Max(0.0001f, runStepsAcrossRange);
        public float RunStepsPerLoop => Mathf.Max(0.0001f, runStepsPerLoop);

        public AnimationCurve WalkWeightCurve => walkWeightCurve;
        public AnimationCurve RunWeightCurve => runWeightCurve;

        public float AimFadeTime => Mathf.Max(0.0001f, aimFadeTime);
        public float CharacterHeight => Mathf.Max(0.01f, characterHeight);
        public float AimHeightThresholdMultiplier => Mathf.Max(0f, aimHeightThresholdMultiplier);
        public float AimHeightThreshold => CharacterHeight * AimHeightThresholdMultiplier;
        public float AimSmoothBand => Mathf.Max(0.0001f, aimSmoothBand);
        public float BentDistance => Mathf.Max(0.01f, bentDistance);
        public float StraightDistance => Mathf.Max(BentDistance + 0.01f, straightDistance);
    }
}
