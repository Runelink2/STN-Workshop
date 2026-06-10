using UnityEngine;

public readonly struct PainterInputContext {
    public readonly Vector2 Position;
    public readonly float HeadingDegrees;
    public readonly Rect ArenaBounds;
    public readonly float PainterRadius;
    public readonly bool HasItemTarget;
    public readonly Vector2 ItemTargetPosition;

    public PainterInputContext(
        Vector2 position,
        float headingDegrees,
        Rect arenaBounds,
        float painterRadius
    ) : this(position, headingDegrees, arenaBounds, painterRadius, false, Vector2.zero) {
    }

    public PainterInputContext(
        Vector2 position,
        float headingDegrees,
        Rect arenaBounds,
        float painterRadius,
        bool hasItemTarget,
        Vector2 itemTargetPosition
    ) {
        Position = position;
        HeadingDegrees = headingDegrees;
        ArenaBounds = arenaBounds;
        PainterRadius = painterRadius;
        HasItemTarget = hasItemTarget;
        ItemTargetPosition = itemTargetPosition;
    }
}
