using UnityEngine;
using UnityEngine.UI;
using Spine;
using Spine.Unity;

/// SpineBoy cursor-following controller for SkeletonGraphic (Canvas / UI).
///
/// Quick setup in Unity:
///   1. Add this component to the "SkeletonGraphic (spineboy-pro)" GameObject.
///   2. Ensure the parent Canvas is Screen Space Overlay with Scale Factor = 1.
///   3. Ensure the SkeletonGraphic has a SkeletonData Asset assigned.
///   4. Press Play — the character auto-positions to the bottom third of the screen.
///
/// Coordinate system:
///   SkeletonGraphic scales its mesh by canvas.referencePixelsPerUnit (= 100 in this project).
///   With skeletonDataAsset.scale = 0.01 that cancels out, so:
///     1 raw Spine data unit == 1 canvas pixel (at Canvas scaleFactor = 1).
///   RectTransformUtility.ScreenPointToLocalPointInRectangle therefore returns values in
///   canvas pixels.  The skeleton's runtime bone coordinates are in SPINE WORLD UNITS
///   (= raw data units × 0.01), so we divide cursor canvas-pixels by meshScale (100) to
///   get bone-space values.
///   The RectTransform pivot sits at the skeleton's root bone (feet level), making the
///   conversion straightforward.
///
/// Deterministic walk:
///   Walk/run TrackTime is driven by the character's X screen position, not by elapsed time.
///   Same X position → same animation frame, always.
///
/// Aiming (right mouse button):
///   The aim animation layers over walk/idle on AnimationState track 1.
///   When the cursor is above armExtendHeightMultiplier × body height (in spine world units)
///   relative to the character's root, the IK crosshair bone tracks the cursor and the arm
///   extends.  Below that threshold the crosshair stays at its setup-pose position (bent arm).
///
/// Tuning note:
///   With the default scene scale the character is roughly 6.86 spine-world-units tall.
///   Two full character heights above the feet (≈ 13.7 units) may be off-screen depending on
///   camera/resolution.  Adjust armExtendHeightMultiplier (default 1.0 ≈ one body height) as
///   needed.
[RequireComponent(typeof(SkeletonGraphic))]
public class SpineboyGraphicController : MonoBehaviour
{
    [Header("References — auto-resolved if left empty")]
    public SkeletonGraphic skeletonGraphic;

    [Header("Animation Names")]
    [SpineAnimation] public string animIdle = "idle";
    [SpineAnimation] public string animWalk = "walk";
    [SpineAnimation] public string animRun  = "run";
    [SpineAnimation] public string animAim  = "aim";

    [Header("Movement")]
    [Tooltip("Maximum speed in canvas pixels per second (≈ spine units per second).")]
    public float maxSpeed = 800f;
    [Tooltip("Acceleration rate.")]
    public float acceleration = 1400f;
    [Tooltip("Deceleration rate.")]
    public float deceleration = 2000f;
    [Tooltip("Speed below this → idle animation.")]
    public float idleSpeedThreshold = 15f;
    [Tooltip("Speed at or above this → run animation (extra credit).")]
    public float runSpeedThreshold  = 450f;

    [Header("Deterministic Animation")]
    [Tooltip("Full walk cycles from left screen edge to right.")]
    public int walkCyclesAcrossScreen = 4;
    [Tooltip("Full run cycles from left screen edge to right.")]
    public int runCyclesAcrossScreen  = 6;
    [Tooltip("Cross-fade time between idle / walk / run (seconds).")]
    public float animCrossfadeTime = 0.15f;

    [Header("Aiming")]
    [Tooltip("Multiplier on the character's spine-world body height for the arm-extend threshold.\n"
           + "Body height ≈ 6.86 spine-world units.  Default 1.0 → cursor must be ~6.86 units (686 raw px)\n"
           + "above the root before the arm extends.  Increase for a higher threshold.")]
    public float armExtendHeightMultiplier = 1.0f;
    [Tooltip("Extra credit: flip skeleton to face cursor while aiming.")]
    public bool flipTowardAimCursor = true;
    [Tooltip("Extra credit: flip skeleton to face movement direction.")]
    public bool flipTowardMovement  = false;

    // ── Track indices ─────────────────────────────────────────────────────────
    private const int TRACK_BASE = 0;
    private const int TRACK_AIM  = 1;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private RectTransform  rectTransform;
    private Canvas         parentCanvas;
    private Skeleton       skeleton;
    private Spine.AnimationState animState;
    private Bone           crosshairBone;

    private float          velocity;
    private bool           isAiming;
    private string         activeBaseAnim = "";
    private TrackEntry     baseEntry;

    private float          walkDuration;
    private float          runDuration;

    /// Spine-world body height (spine units = raw_data_units × 0.01).
    /// Equals data.Height × skeletonDataAsset.scale.
    private float          characterBodyHeight;

    /// Canvas pixels per spine-world unit.  From SkeletonGraphic source:
    ///   meshScale = canvas.referencePixelsPerUnit × referenceScale
    /// For this project: 100 × 1 = 100.  Read at runtime from skeletonGraphic.MeshScale.
    private float          meshScale;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (skeletonGraphic == null)
            skeletonGraphic = GetComponent<SkeletonGraphic>();
        rectTransform = GetComponent<RectTransform>();
    }

    void Start()
    {
        parentCanvas   = GetComponentInParent<Canvas>();
        skeleton       = skeletonGraphic.Skeleton;
        animState      = skeletonGraphic.AnimationState;
        crosshairBone  = skeleton.FindBone("crosshair");

        var data = skeletonGraphic.skeletonDataAsset.GetSkeletonData(true);
        float dataScale          = skeletonGraphic.skeletonDataAsset.scale; // 0.01
        walkDuration             = data.FindAnimation(animWalk).Duration;
        runDuration              = data.FindAnimation(animRun).Duration;
        characterBodyHeight      = data.Height * dataScale; // in spine WORLD units

        // meshScale = canvas pixels per spine-world unit (reads the live value after Init).
        // Force a layout update so SkeletonGraphic has computed meshScale already.
        meshScale = skeletonGraphic.MeshScale;

        // Pre-configure cross-fade times between base animations.
        var mixData = animState.Data;
        foreach (var from in new[] { animIdle, animWalk, animRun })
        foreach (var to   in new[] { animIdle, animWalk, animRun })
            if (from != to)
                mixData.SetMix(from, to, animCrossfadeTime);

        // Subscribe for bone overrides: after Apply(), before UpdateWorldTransform().
        skeletonGraphic.UpdateLocal += OnUpdateLocal;

        // Position character in the bottom third of the screen.
        PositionInBottomThird();

        SwitchBaseAnim(animIdle);
    }

    void OnDestroy()
    {
        if (skeletonGraphic != null)
            skeletonGraphic.UpdateLocal -= OnUpdateLocal;
    }

    void Update()
    {
        UpdateMovement();
        UpdateAiming();
    }

    // ── Movement ──────────────────────────────────────────────────────────────

    void UpdateMovement()
    {
        float scaleFactor = parentCanvas != null ? parentCanvas.scaleFactor : 1f;
        // Screen bounds in anchoredPosition space (anchor = 0.5 = screen center).
        float halfW    = Screen.width  / (2f * scaleFactor);
        float leftBound  = -halfW;
        float rightBound =  halfW;

        // Cursor X in the same coordinate space as anchoredPosition.
        float mouseAnchoredX = Input.mousePosition.x / scaleFactor - Screen.width  / (2f * scaleFactor);
        float targetX  = Mathf.Clamp(mouseAnchoredX, leftBound, rightBound);
        float currentX = rectTransform.anchoredPosition.x;

        // Spring-damper velocity toward target.
        float gap     = targetX - currentX;
        float desired = Mathf.Sign(gap) * Mathf.Min(maxSpeed, Mathf.Abs(gap) * deceleration * 0.1f);
        float rate    = Mathf.Abs(desired) >= Mathf.Abs(velocity) ? acceleration : deceleration;
        velocity      = Mathf.MoveTowards(velocity, desired, rate * Time.deltaTime);

        float newX = Mathf.Clamp(currentX + velocity * Time.deltaTime, leftBound, rightBound);
        rectTransform.anchoredPosition = new Vector2(newX, rectTransform.anchoredPosition.y);

        // ── Choose and drive base animation ───────────────────────────────────

        float speed = Mathf.Abs(velocity);
        if (speed < idleSpeedThreshold)
        {
            if (activeBaseAnim != animIdle)
                SwitchBaseAnim(animIdle);
        }
        else
        {
            bool   doRun    = speed >= runSpeedThreshold;
            string wantAnim = doRun ? animRun : animWalk;
            if (activeBaseAnim != wantAnim)
                SwitchBaseAnim(wantAnim);

            // Deterministic: TrackTime driven by position, not elapsed time.
            float screenWidth = Screen.width / scaleFactor;
            float posRatio    = (newX - leftBound) / (rightBound - leftBound); // 0..1
            float duration    = doRun ? runDuration  : walkDuration;
            int   cycles      = doRun ? runCyclesAcrossScreen : walkCyclesAcrossScreen;

            if (baseEntry != null)
            {
                baseEntry.TimeScale = 0f;
                baseEntry.TrackTime = (posRatio * cycles * duration) % duration;
            }
        }

        // Extra credit: flip toward movement direction.
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
                skeleton.ScaleX = 1f;
        }

        // Extra credit: flip toward cursor while aiming.
        if (isAiming && flipTowardAimCursor)
        {
            // Cursor relative to character pivot in canvas space.
            Vector2 cursorRelChar;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, Input.mousePosition, null, out cursorRelChar);
            skeleton.ScaleX = cursorRelChar.x < 0f ? -1f : 1f;
        }
    }

    // ── Bone overrides ────────────────────────────────────────────────────────

    void OnUpdateLocal(ISkeletonAnimation _)
    {
        if (!isAiming || crosshairBone == null) return;

        // RectTransformUtility returns cursor in the RectTransform's LOCAL SPACE (canvas pixels).
        // The pivot is at the skeleton root (feet).
        Vector2 cursorCanvasPx;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, Input.mousePosition, null, out cursorCanvasPx);

        // Convert canvas pixels → spine world units.
        // Pipeline:  canvas_px = spine_world_units × meshScale
        //            spine_world_units = canvas_px / meshScale
        float mScale = (meshScale > 0f) ? meshScale : skeletonGraphic.MeshScale;
        if (mScale <= 0f) mScale = 100f; // safe fallback

        float cursorSpineX = cursorCanvasPx.x / mScale;
        float cursorSpineY = cursorCanvasPx.y / mScale;

        // Arm extends only when cursor is sufficiently high (in spine world units).
        float extendThreshold = armExtendHeightMultiplier * characterBodyHeight;
        if (cursorSpineY < extendThreshold)
            return; // Leave crosshairBone at setup-pose position → bent ready arm.

        // Place crosshairBone so its spine-world position equals the cursor position.
        // Crosshair is a direct child of root.  Root world transform (approx.):
        //   worldX=0, worldY=0, a = ScaleX (±1), d = ScaleY (1), b≈0, c≈0.
        // WorldToLocal ≈ localX = worldX / ScaleX,  localY = worldY / ScaleY.
        crosshairBone.X = cursorSpineX / skeleton.ScaleX;
        crosshairBone.Y = cursorSpineY;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void SwitchBaseAnim(string name)
    {
        baseEntry      = animState.SetAnimation(TRACK_BASE, name, true);
        activeBaseAnim = name;
    }

    void PositionInBottomThird()
    {
        float scaleFactor = parentCanvas != null ? parentCanvas.scaleFactor : 1f;
        float screenH     = Screen.height / scaleFactor;
        // Anchor at (0.5, 0.5) → anchoredPosition Y = 0 is screen center.
        // Bottom third center ≈ -screenH/2 + screenH/3 = -screenH/6.
        float y = -screenH / 6f;
        rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, y);
    }
}
