using UnityEngine;

public sealed class CpuPainterInput : IPainterInputSource {
    private const float SoftTurnMultiplier = 0.5f;
    private const float HardTurnMultiplier = 1.35f;
    private const float LongBeatMultiplier = 1.42f;
    private const float ShortBeatMultiplier = 0.72f;
    private const float EscapeTargetInsetMultiplier = 1.0f;
    private const float OpeningTurnMultiplier = 0.65f;
    private const float WallTouchEpsilon = 0.05f;
    private const float WallOutwardDotThreshold = 0.05f;
    private const float ItemSeekTurnSharpness = 3.0f;
    private const int MinVariationBeats = 3;
    private const int MaxVariationBeats = 7;

    private readonly System.Random random;
    private readonly float normalBeatDuration;
    private readonly float normalTurnStrength;

    private Mode mode;
    private int escapeTurnDirection;
    private int steerDirection;
    private int beatsUntilVariation;
    private float beatTimer;
    private float currentBeatDuration;
    private float currentTurnStrength;

    public CpuPainterInput(
        int seed,
        int startingSteerDirection,
        float normalBeatDuration,
        float normalTurnStrength,
        float initialBeatProgress
    ) {
        random = new System.Random(seed);
        mode = Mode.OpeningWiggle;
        escapeTurnDirection = 1;
        steerDirection = startingSteerDirection < 0 ? -1 : 1;

        this.normalBeatDuration = Mathf.Max(0.05f, normalBeatDuration);
        this.normalTurnStrength = Mathf.Clamp01(normalTurnStrength);

        currentBeatDuration = this.normalBeatDuration;
        currentTurnStrength = this.normalTurnStrength;
        beatTimer = Mathf.Clamp01(initialBeatProgress) * currentBeatDuration;
        beatsUntilVariation = GetRandomBeatCount();
    }

    public static float GetOpeningCenteredHeading(
        float centerHeadingDegrees,
        int startingSteerDirection,
        float normalBeatDuration,
        float normalTurnStrength,
        float turnSpeedDegreesPerSecond
    ) {
        float turnDirection = startingSteerDirection < 0 ? -1.0f : 1.0f;
        float openingArcDegrees = turnDirection
            * Mathf.Clamp01(normalTurnStrength)
            * OpeningTurnMultiplier
            * Mathf.Max(0.0f, turnSpeedDegreesPerSecond)
            * Mathf.Max(0.05f, normalBeatDuration);

        return Mathf.Repeat(centerHeadingDegrees - openingArcDegrees * 0.5f, 360.0f);
    }

    public float GetTurnInput(float deltaTime, PainterInputContext context) {
        deltaTime = Mathf.Max(0.0f, deltaTime);
        AdvanceRhythm(deltaTime);

        if (mode == Mode.OpeningWiggle) {
            return steerDirection * currentTurnStrength * OpeningTurnMultiplier;
        }

        bool shouldEscape = ShouldEnterEscape(context);

        if (mode == Mode.WallEscape) {
            if (IsSafelyInside(context) && !shouldEscape) {
                mode = Mode.NormalRhythm;
            }
        } else if (shouldEscape) {
            mode = Mode.WallEscape;
            escapeTurnDirection = ChooseEscapeTurnDirection(context);
        }

        if (mode == Mode.WallEscape) {
            return escapeTurnDirection;
        }

        if (context.HasItemTarget) {
            return GetItemSeekTurnInput(context);
        }

        return GetRhythmTurnInput();
    }

    private float GetItemSeekTurnInput(PainterInputContext context) {
        Vector2 toItem = context.ItemTargetPosition - context.Position;

        if (toItem.sqrMagnitude < 0.001f) {
            return 0.0f;
        }

        toItem.Normalize();

        Vector2 forward = GetForward(context.HeadingDegrees);
        float cross = forward.x * toItem.y - forward.y * toItem.x;
        float dot = Vector2.Dot(forward, toItem);

        if (dot < 0.0f) {
            return cross >= 0.0f ? 1.0f : -1.0f;
        }

        return Mathf.Clamp(cross * ItemSeekTurnSharpness, -1.0f, 1.0f);
    }

    private float GetRhythmTurnInput() {
        return steerDirection * currentTurnStrength;
    }

    private void AdvanceRhythm(float deltaTime) {
        beatTimer += Mathf.Max(0.0f, deltaTime);

        while (beatTimer >= currentBeatDuration) {
            beatTimer -= currentBeatDuration;
            AdvanceBeat();
        }
    }

    private bool ShouldEnterEscape(PainterInputContext context) {
        Vector2 forward = GetForward(context.HeadingDegrees);
        return IsDrivingIntoTouchedWall(context, forward);
    }

    private bool IsSafelyInside(PainterInputContext context) {
        return !IsTouchingWall(context);
    }

    private bool IsDrivingIntoTouchedWall(PainterInputContext context, Vector2 forward) {
        Rect centerBounds = GetCenterBounds(context, 0.0f);

        if (context.Position.x <= centerBounds.xMin + WallTouchEpsilon && forward.x < -WallOutwardDotThreshold) {
            return true;
        }

        if (context.Position.x >= centerBounds.xMax - WallTouchEpsilon && forward.x > WallOutwardDotThreshold) {
            return true;
        }

        if (context.Position.y <= centerBounds.yMin + WallTouchEpsilon && forward.y < -WallOutwardDotThreshold) {
            return true;
        }

        if (context.Position.y >= centerBounds.yMax - WallTouchEpsilon && forward.y > WallOutwardDotThreshold) {
            return true;
        }

        return false;
    }

    private bool IsTouchingWall(PainterInputContext context) {
        Rect centerBounds = GetCenterBounds(context, 0.0f);

        return context.Position.x <= centerBounds.xMin + WallTouchEpsilon
            || context.Position.x >= centerBounds.xMax - WallTouchEpsilon
            || context.Position.y <= centerBounds.yMin + WallTouchEpsilon
            || context.Position.y >= centerBounds.yMax - WallTouchEpsilon;
    }

    private int ChooseEscapeTurnDirection(PainterInputContext context) {
        Vector2 forward = GetForward(context.HeadingDegrees);
        Vector2 targetDirection = GetInsideDirection(context);
        float cross = forward.x * targetDirection.y - forward.y * targetDirection.x;

        if (Mathf.Abs(cross) < 0.001f) {
            return escapeTurnDirection == 0 ? steerDirection : escapeTurnDirection;
        }

        return cross > 0.0f ? 1 : -1;
    }

    private Vector2 GetInsideDirection(PainterInputContext context) {
        Rect safeBounds = GetCenterBounds(context, context.PainterRadius * EscapeTargetInsetMultiplier);
        Vector2 forward = GetForward(context.HeadingDegrees);
        Vector2 safePoint = new Vector2(
            Mathf.Clamp(context.Position.x, safeBounds.xMin, safeBounds.xMax),
            Mathf.Clamp(context.Position.y, safeBounds.yMin, safeBounds.yMax)
        );

        Vector2 direction = safePoint - context.Position;

        if (direction.sqrMagnitude > 0.001f) {
            return direction.normalized;
        }

        Vector2 arenaCenter = context.ArenaBounds.center;
        direction = arenaCenter - context.Position;

        if (direction.sqrMagnitude > 0.001f) {
            return direction.normalized;
        }

        return -forward;
    }

    private Rect GetCenterBounds(PainterInputContext context, float extraInset) {
        float inset = context.PainterRadius + Mathf.Max(0.0f, extraInset);
        float xMin = context.ArenaBounds.xMin + inset;
        float xMax = context.ArenaBounds.xMax - inset;
        float yMin = context.ArenaBounds.yMin + inset;
        float yMax = context.ArenaBounds.yMax - inset;

        if (xMin > xMax) {
            float centerX = context.ArenaBounds.center.x;
            xMin = centerX;
            xMax = centerX;
        }

        if (yMin > yMax) {
            float centerY = context.ArenaBounds.center.y;
            yMin = centerY;
            yMax = centerY;
        }

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private static Vector2 GetForward(float headingDegrees) {
        float headingRadians = headingDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(headingRadians), Mathf.Sin(headingRadians));
    }

    private static bool Contains(Rect rect, Vector2 point) {
        return point.x >= rect.xMin
            && point.x <= rect.xMax
            && point.y >= rect.yMin
            && point.y <= rect.yMax;
    }

    private void AdvanceBeat() {
        bool wasOpening = mode == Mode.OpeningWiggle;

        if (mode == Mode.OpeningWiggle) {
            mode = Mode.NormalRhythm;
        }

        steerDirection = -steerDirection;
        currentBeatDuration = normalBeatDuration;
        currentTurnStrength = normalTurnStrength;

        if (wasOpening) {
            return;
        }

        beatsUntilVariation--;

        if (beatsUntilVariation > 0) {
            return;
        }

        ApplyVariation();
        beatsUntilVariation = GetRandomBeatCount();
    }

    private void ApplyVariation() {
        float roll = GetRandom01();

        if (roll < 0.3f) {
            currentTurnStrength = normalTurnStrength * SoftTurnMultiplier;
        } else if (roll < 0.62f) {
            currentTurnStrength = Mathf.Clamp01(normalTurnStrength * HardTurnMultiplier);
        } else if (roll < 0.84f) {
            currentBeatDuration = normalBeatDuration * LongBeatMultiplier;
            currentTurnStrength = Mathf.Clamp01(normalTurnStrength * 1.08f);
        } else {
            currentBeatDuration = normalBeatDuration * ShortBeatMultiplier;
        }
    }

    private int GetRandomBeatCount() {
        return random.Next(MinVariationBeats, MaxVariationBeats + 1);
    }

    private float GetRandom01() {
        return (float)random.NextDouble();
    }

    private enum Mode {
        OpeningWiggle,
        NormalRhythm,
        WallEscape
    }
}
