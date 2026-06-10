using UnityEngine;

public sealed class PainterMovement {
    public byte OwnerId { get; }
    public Vector2 Position { get; private set; }
    public Vector2 PreviousPosition { get; private set; }
    public float HeadingDegrees { get; private set; }
    public float SpeedMultiplier { get; set; }

    private readonly float moveSpeedPixelsPerSecond;
    private readonly float turnSpeedDegreesPerSecond;

    public PainterMovement(
        byte ownerId,
        Vector2 startPosition,
        float startHeadingDegrees,
        float moveSpeedPixelsPerSecond,
        float turnSpeedDegreesPerSecond,
        Rect arenaBounds,
        float painterRadius
    ) {
        OwnerId = ownerId;
        Position = ClampToArena(startPosition, arenaBounds, painterRadius);
        PreviousPosition = Position;
        HeadingDegrees = Mathf.Repeat(startHeadingDegrees, 360.0f);

        this.moveSpeedPixelsPerSecond = Mathf.Max(0.0f, moveSpeedPixelsPerSecond);
        this.turnSpeedDegreesPerSecond = Mathf.Max(0.0f, turnSpeedDegreesPerSecond);
        SpeedMultiplier = 1.0f;
    }

    public void Tick(float deltaTime, float turnInput, Rect arenaBounds, float painterRadius) {
        PreviousPosition = Position;

        turnInput = Mathf.Clamp(turnInput, -1.0f, 1.0f);

        HeadingDegrees = Mathf.Repeat(
            HeadingDegrees + turnInput * turnSpeedDegreesPerSecond * deltaTime,
            360.0f
        );

        float headingRadians = HeadingDegrees * Mathf.Deg2Rad;
        Vector2 forward = new Vector2(Mathf.Cos(headingRadians), Mathf.Sin(headingRadians));
        Vector2 attemptedPosition = Position + forward * moveSpeedPixelsPerSecond * SpeedMultiplier * deltaTime;

        Position = ClampToArena(attemptedPosition, arenaBounds, painterRadius);
    }

    private static Vector2 ClampToArena(Vector2 position, Rect arenaBounds, float painterRadius) {
        float xMin = arenaBounds.xMin + painterRadius;
        float xMax = arenaBounds.xMax - painterRadius;
        float yMin = arenaBounds.yMin + painterRadius;
        float yMax = arenaBounds.yMax - painterRadius;

        if (xMin > xMax) {
            float centerX = (arenaBounds.xMin + arenaBounds.xMax) * 0.5f;
            xMin = centerX;
            xMax = centerX;
        }

        if (yMin > yMax) {
            float centerY = (arenaBounds.yMin + arenaBounds.yMax) * 0.5f;
            yMin = centerY;
            yMax = centerY;
        }

        return new Vector2(
            Mathf.Clamp(position.x, xMin, xMax),
            Mathf.Clamp(position.y, yMin, yMax)
        );
    }
}
