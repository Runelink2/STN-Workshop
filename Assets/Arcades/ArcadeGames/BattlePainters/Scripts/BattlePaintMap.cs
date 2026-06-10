using System;
using Unity.Collections;
using UnityEngine;

public sealed class BattlePaintMap {
    public int Width { get; }
    public int Height { get; }
    public int PlayerCount { get; }
    public int PaintablePixelCount { get; private set; }
    public int[] OwnedPixels { get; }

    private readonly byte[] owners;
    private readonly bool[] paintable;
    private readonly Color32[] colorsByOwner;
    private readonly Color32[] backgroundPixels;
    private readonly int baseBrushRadius;

    public BattlePaintMap(
        int width,
        int height,
        int playerCount,
        int brushRadius,
        Color32[] playerColors,
        Color32[] backgroundPixels,
        bool[] paintableMask
    ) {
        if (width <= 0) {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0) {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (playerCount <= 0 || playerCount > 254) {
            throw new ArgumentOutOfRangeException(nameof(playerCount));
        }

        if (brushRadius <= 0) {
            throw new ArgumentOutOfRangeException(nameof(brushRadius));
        }

        if (playerColors == null || playerColors.Length < playerCount) {
            throw new ArgumentException("Not enough player colors.", nameof(playerColors));
        }

        int pixelCount = width * height;

        if (backgroundPixels == null || backgroundPixels.Length != pixelCount) {
            throw new ArgumentException("Background pixel buffer has wrong size.", nameof(backgroundPixels));
        }

        Width = width;
        Height = height;
        PlayerCount = playerCount;

        owners = new byte[pixelCount];
        paintable = new bool[pixelCount];
        OwnedPixels = new int[playerCount + 1];

        colorsByOwner = new Color32[playerCount + 1];

        for (int i = 0; i < playerCount; i++) {
            colorsByOwner[i + 1] = playerColors[i];
        }

        this.backgroundPixels = (Color32[])backgroundPixels.Clone();

        if (paintableMask == null) {
            for (int i = 0; i < pixelCount; i++) {
                paintable[i] = true;
            }

            PaintablePixelCount = pixelCount;
        } else {
            if (paintableMask.Length != pixelCount) {
                throw new ArgumentException("Paintable mask has wrong size.", nameof(paintableMask));
            }

            for (int i = 0; i < pixelCount; i++) {
                paintable[i] = paintableMask[i];

                if (paintable[i]) {
                    PaintablePixelCount++;
                }
            }
        }

        baseBrushRadius = brushRadius;
    }

    public void PaintSegment(Vector2 previous, Vector2 current, byte ownerId, NativeArray<Color32> texturePixels) {
        PaintSegment(previous, current, ownerId, texturePixels, 1.0f);
    }

    public void PaintSegment(
        Vector2 previous,
        Vector2 current,
        byte ownerId,
        NativeArray<Color32> texturePixels,
        float brushScale
    ) {
        if (ownerId == 0 || ownerId > PlayerCount) {
            throw new ArgumentOutOfRangeException(nameof(ownerId));
        }

        if (texturePixels.Length < owners.Length) {
            throw new ArgumentException("Texture pixel buffer is too small.", nameof(texturePixels));
        }

        float radius = Mathf.Max(1.0f, baseBrushRadius * brushScale);
        PaintCapsule(previous, current, radius, ownerId, texturePixels);
    }

    public void PaintStamp(Vector2Int center, byte ownerId, NativeArray<Color32> texturePixels) {
        if (ownerId == 0 || ownerId > PlayerCount) {
            throw new ArgumentOutOfRangeException(nameof(ownerId));
        }

        if (texturePixels.Length < owners.Length) {
            throw new ArgumentException("Texture pixel buffer is too small.", nameof(texturePixels));
        }

        Vector2 position = new Vector2(center.x, center.y);
        PaintCapsule(position, position, baseBrushRadius, ownerId, texturePixels);
    }

    public bool IsPaintable(int x, int y) {
        if (x < 0 || x >= Width || y < 0 || y >= Height) {
            return false;
        }

        return paintable[y * Width + x];
    }

    public float GetCoverage01(int ownerId) {
        if (ownerId <= 0 || ownerId > PlayerCount || PaintablePixelCount == 0) {
            return 0.0f;
        }

        return OwnedPixels[ownerId] / (float)PaintablePixelCount;
    }

    // Rasterizes the segment as a capsule. Pixels within the brush radius are
    // claimed and painted solid; a one-pixel fringe outside the radius is only
    // tinted toward the paint color so stroke edges stay anti-aliased. The
    // fringe blends from the pixel's base color (its owner's paint color, or
    // the background pixel when unowned) rather than the current texture
    // value, which keeps repeated overlapping strokes from accumulating.
    private void PaintCapsule(Vector2 from, Vector2 to, float radius, byte ownerId, NativeArray<Color32> texturePixels) {
        float outerRadius = radius + 1.0f;
        float radiusSquared = radius * radius;
        float outerRadiusSquared = outerRadius * outerRadius;

        int xMin = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(from.x, to.x) - outerRadius));
        int xMax = Mathf.Min(Width - 1, Mathf.CeilToInt(Mathf.Max(from.x, to.x) + outerRadius));
        int yMin = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(from.y, to.y) - outerRadius));
        int yMax = Mathf.Min(Height - 1, Mathf.CeilToInt(Mathf.Max(from.y, to.y) + outerRadius));

        if (xMin > xMax || yMin > yMax) {
            return;
        }

        Vector2 segment = to - from;
        float segmentLengthSquared = segment.sqrMagnitude;
        Color32 paintColor = colorsByOwner[ownerId];

        for (int y = yMin; y <= yMax; y++) {
            int baseIndex = y * Width;

            for (int x = xMin; x <= xMax; x++) {
                int index = baseIndex + x;

                if (!paintable[index]) {
                    continue;
                }

                float dx = x - from.x;
                float dy = y - from.y;

                if (segmentLengthSquared > 0.0f) {
                    float t = Mathf.Clamp01((dx * segment.x + dy * segment.y) / segmentLengthSquared);
                    dx -= segment.x * t;
                    dy -= segment.y * t;
                }

                float distanceSquared = dx * dx + dy * dy;

                if (distanceSquared >= outerRadiusSquared) {
                    continue;
                }

                if (distanceSquared <= radiusSquared) {
                    byte oldOwner = owners[index];

                    if (oldOwner != ownerId) {
                        if (oldOwner != 0) {
                            OwnedPixels[oldOwner]--;
                        }

                        owners[index] = ownerId;
                        OwnedPixels[ownerId]++;
                    }

                    texturePixels[index] = paintColor;
                } else {
                    float coverage = outerRadius - Mathf.Sqrt(distanceSquared);
                    byte fringeOwner = owners[index];
                    Color32 baseColor = fringeOwner == 0
                        ? backgroundPixels[index]
                        : colorsByOwner[fringeOwner];
                    texturePixels[index] = Color32.Lerp(baseColor, paintColor, coverage);
                }
            }
        }
    }
}
