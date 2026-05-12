using UnityEngine;
using Spine;
using Spine.Unity;

/// SpineBoy cursor-following controller with deterministic walk animation and IK-driven aiming.
///
/// Setup:
///   1. Add this component to a GameObject that has SkeletonAnimation.
///   2. Assign skeletonAnimation in the Inspector (or it's auto-found on the same GameObject).
///   3. The character will auto-position itself in the bottom third of the screen on Start.
///
/// Deterministic walk:
///   The walk/run animation time is driven by the character's world-space X position,
///   not by elapsed time. This guarantees the same animation frame at the same screen position.
///   walkCyclesAcrossScreen and runCyclesAcrossScreen control how many full animation
///   loops span the full screen width.
///
/// Aiming (right mouse button):
///   The aim animation plays on track 1 (layered over walk/idle on track 0).
///   When the cursor is >= armExtendHeightMultiplier character-heights above the character,
///   the IK crosshair bone tracks the cursor and the arm extends toward it.
///   When the cursor is lower, the crosshair stays at its setup-pose position, giving a
///   bent/ready arm stance.
[RequireComponent(typeof(SkeletonAnimation))]
public class SpineboyController : MonoBehaviour
{
    [Header("Spine Reference")]
    [Tooltip("Auto-resolved from this GameObject if left empty.")]
    public SkeletonAnimation skeletonAnimation;

    [Header("Animation Names")]
    [SpineAnimation] public string animIdle = "idle";
    [SpineAnimation] public string animWalk = "walk";
    [SpineAnimation] public string animRun  = "run";
    [SpineAnimation] public string animAim  = "aim";

    [Header("Movement")]
    [Tooltip("Maximum horizontal speed in world units per second.")]
    public float maxSpeed = 5f;
    [Tooltip("Rate at which velocity ramps up toward desired speed.")]
    public float acceleration = 10f;
    [Tooltip("Rate at which velocity ramps down toward desired speed.")]
    public float deceleration = 14f;
    [Tooltip("Speeds below this threshold trigger the idle animation.")]
    public float idleSpeedThreshold = 0.12f;
    [Tooltip("Speeds at or above this threshold switch to the run animation (extra credit).")]
    public float runSpeedThreshold  = 3.5f;

    [Header("Deterministic Animation")]
    [Tooltip("How many full walk animation loops span the full screen width.")]
    public int walkCyclesAcrossScreen = 4;
    [Tooltip("How many full run animation loops span the full screen width.")]
    public int runCyclesAcrossScreen  = 6;
    [Tooltip("Cross-fade duration when switching between idle / walk / run.")]
    public float animCrossfadeTime = 0.15f;

    [Header("Aiming")]
    [Tooltip("Cursor must be at least this many character-heights above the character for the arm to extend toward it. Below this, the arm stays in a bent ready pose.")]
    public float armExtendHeightMultiplier = 2f;
    [Tooltip("Extra credit: flip the character to face the cursor while aiming.")]
    public bool flipTowardAimCursor = true;
    [Tooltip("Extra credit: also flip while walking to face movement direction.")]
    public bool flipTowardMovement = false;

    // ── Animation track indices ────────────────────────────────────────────────
    private const int TRACK_BASE = 0;
    private const int TRACK_AIM  = 1;

    // ── Runtime state ──────────────────────────────────────────────────────────
    private Camera           mainCamera;
    private Skeleton         skeleton;
    private Spine.AnimationState animState;
    private Bone             crosshairBone;
    private float            skeletonScale;

    private float            velocity;
    private bool             isAiming;
    private string           activeBaseAnim = "";
    private TrackEntry       baseEntry;

    private float            walkDuration;
    private float            runDuration;
    private float            characterWorldHeight; // in Unity world units

    // ── Unity lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        if (skeletonAnimation == null)
            skeletonAnimation = GetComponent<SkeletonAnimation>();
    }

    void Start()
    {
        mainCamera    = Camera.main;
        skeleton      = skeletonAnimation.Skeleton;
        animState     = skeletonAnimation.AnimationState;
        crosshairBone = skeleton.FindBone("crosshair");
        skeletonScale = skeletonAnimation.skeletonDataAsset.scale;

        // Cache animation durations from skeleton data.
        var data = skeletonAnimation.skeletonDataAsset.GetSkeletonData(true);
        walkDuration        = data.FindAnimation(animWalk).Duration;
        runDuration         = data.FindAnimation(animRun).Duration;
        characterWorldHeight = data.Height * skeletonScale
                               * skeletonAnimation.transform.lossyScale.y;

        // Pre-configure cross-fade times between base animations.
        var mixData = animState.Data;
        foreach (var from in new[] { animIdle, animWalk, animRun })
        foreach (var to   in new[] { animIdle, animWalk, animRun })
            if (from != to)
                mixData.SetMix(from, to, animCrossfadeTime);

        // Subscribe to UpdateLocal: fires after Apply(), before UpdateWorldTransform().
        // This is where we safely modify bone positions so constraints (IK) see them.
        skeletonAnimation.UpdateLocal += OnUpdateLocal;

        // Snap character to bottom third of the screen.
        PositionInBottomThird();

        // Start with idle.
        SwitchBaseAnim(animIdle);
    }

    void OnDestroy()
    {
        if (skeletonAnimation != null)
            skeletonAnimation.UpdateLocal -= OnUpdateLocal;
    }

    void Update()
    {
        UpdateMovement();
        UpdateAiming();
    }

    // ── Movement & deterministic animation ────────────────────────────────────

    void UpdateMovement()
    {
        GetScreenBounds(out float leftBound, out float rightBound);

        // Map cursor to world X, clamped to screen.
        Vector3 cursorWorld = ScreenToWorld(Input.mousePosition);
        float targetX  = Mathf.Clamp(cursorWorld.x, leftBound, rightBound);
        float currentX = transform.position.x;

        // Desired velocity: proportional to remaining gap, capped at maxSpeed.
        float gap      = targetX - currentX;
        float desired  = Mathf.Sign(gap) * Mathf.Min(maxSpeed, Mathf.Abs(gap) * deceleration);
        float rate     = Mathf.Abs(desired) >= Mathf.Abs(velocity) ? acceleration : deceleration;
        velocity       = Mathf.MoveTowards(velocity, desired, rate * Time.deltaTime);

        // Clamp to screen edges.
        float newX = Mathf.Clamp(currentX + velocity * Time.deltaTime, leftBound, rightBound);
        transform.position = new Vector3(newX, transform.position.y, transform.position.z);

        // ── Choose and drive base animation ──────────────────────────────────

        float speed = Mathf.Abs(velocity);

        if (speed < idleSpeedThreshold)
        {
            if (activeBaseAnim != animIdle)
                SwitchBaseAnim(animIdle);
            // Idle plays normally (no manual time control needed).
        }
        else
        {
            bool  doRun     = speed >= runSpeedThreshold;
            string wantAnim = doRun ? animRun : animWalk;

            if (activeBaseAnim != wantAnim)
                SwitchBaseAnim(wantAnim);

            // Deterministic time: same X position → same animation frame, always.
            float posRatio  = (newX - leftBound) / (rightBound - leftBound);
            float duration  = doRun ? runDuration  : walkDuration;
            int   cycles    = doRun ? runCyclesAcrossScreen : walkCyclesAcrossScreen;
            float trackTime = (posRatio * cycles * duration) % duration;

            // Freeze auto-advance; we drive TrackTime directly from position.
            if (baseEntry != null)
            {
                baseEntry.TimeScale = 0f;
                baseEntry.TrackTime = trackTime;
            }
        }

        // ── Flip toward movement direction (extra credit, off by default) ────
        if (!isAiming && flipTowardMovement && speed >= idleSpeedThreshold)
            skeleton.ScaleX = velocity < 0f ? -1f : 1f;
    }

    // ── Aiming ───────────────────────────────────────────────────────────────

    void UpdateAiming()
    {
        bool wantAim = Input.GetMouseButton(1);

        if (wantAim && !isAiming)
        {
            animState.SetAnimation(TRACK_AIM, animAim, true);
            isAiming = true;
        }
        else if (!wantAim && isAiming)
        {
            animState.SetEmptyAnimation(TRACK_AIM, 0.15f);
            isAiming = false;
            if (!flipTowardMovement)
                skeleton.ScaleX = 1f; // restore default facing
        }

        // Extra credit: flip skeleton to face cursor while aiming.
        if (isAiming && flipTowardAimCursor)
        {
            Vector3 cursorWorld = ScreenToWorld(Input.mousePosition);
            skeleton.ScaleX = cursorWorld.x < transform.position.x ? -1f : 1f;
        }
    }

    // ── Bone overrides (runs after Apply, before UpdateWorldTransform) ────────

    void OnUpdateLocal(ISkeletonAnimation _)
    {
        if (!isAiming || crosshairBone == null) return;

        Vector3 cursorWorld = ScreenToWorld(Input.mousePosition);

        // Only extend arm (track cursor) when cursor is 2+ character heights above.
        float extendThresholdY = transform.position.y
                                 + armExtendHeightMultiplier * characterWorldHeight;

        if (cursorWorld.y < extendThresholdY)
            return; // Leave crosshairBone at its setup-pose position → bent arm.

        // Convert cursor Unity world position → crosshair bone local coordinates.
        //
        // Pipeline:
        //   Unity world pos
        //   → SkeletonAnimation transform's local space (Unity units)
        //   → Spine world coordinates (divide by skeletonDataAsset.scale)
        //   → crosshairBone parent-local coordinates
        //
        // The crosshair bone's parent is root. Root's world transform (approximate):
        //   worldX = 0, worldY = 0, a = ScaleX (±1), d = ScaleY (1), b = c ≈ 0
        // So WorldToLocal simplifies to: localX = spineX / ScaleX, localY = spineY.
        // We read skeleton.ScaleX directly to handle same-frame flips correctly.

        var   tf      = skeletonAnimation.transform;
        Vector3 local = tf.InverseTransformPoint(cursorWorld);
        float spineX  = local.x / skeletonScale;
        float spineY  = local.y / skeletonScale;

        // Divide by ScaleX (which equals ±1) to invert root's horizontal flip.
        crosshairBone.X = spineX / skeleton.ScaleX;
        crosshairBone.Y = spineY;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void SwitchBaseAnim(string name)
    {
        baseEntry      = animState.SetAnimation(TRACK_BASE, name, true);
        activeBaseAnim = name;
        // TimeScale is 1 by default; walk/run will override it each frame.
    }

    void PositionInBottomThird()
    {
        float camY  = mainCamera.transform.position.y;
        float halfH = mainCamera.orthographicSize;
        // Place at 1/3 from bottom: bottom + height/3.
        float y = camY - halfH + (halfH * 2f / 3f);
        transform.position = new Vector3(transform.position.x, y, transform.position.z);
    }

    void GetScreenBounds(out float leftBound, out float rightBound)
    {
        float halfW = mainCamera.orthographicSize * mainCamera.aspect;
        leftBound   = mainCamera.transform.position.x - halfW;
        rightBound  = mainCamera.transform.position.x + halfW;
    }

    /// Converts a screen-space position to Unity world space.
    /// Works for orthographic cameras (standard 2D setup).
    Vector3 ScreenToWorld(Vector3 screenPos)
    {
        screenPos.z = Mathf.Abs(mainCamera.transform.position.z);
        return mainCamera.ScreenToWorldPoint(screenPos);
    }
}
