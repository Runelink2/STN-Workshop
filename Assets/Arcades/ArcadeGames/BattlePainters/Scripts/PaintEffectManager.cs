using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public sealed class PaintEffectManager {
    private const int MaxPositionAttemptsPerSplat = 20;

    private readonly BattlePaintMap paintMap;
    private readonly System.Random random;
    private readonly List<BombRainEffect> activeEffects;
    private readonly int bombRainSplatCount;
    private readonly float bombRainSplatInterval;

    public PaintEffectManager(
        BattlePaintMap paintMap,
        int randomSeed,
        int bombRainSplatCount,
        float bombRainSplatInterval
    ) {
        this.paintMap = paintMap;
        this.bombRainSplatCount = Mathf.Max(1, bombRainSplatCount);
        this.bombRainSplatInterval = Mathf.Max(0.0f, bombRainSplatInterval);
        random = new System.Random(randomSeed);
        activeEffects = new List<BombRainEffect>(8);
    }

    public void Activate(PainterItemType itemType, byte ownerId) {
        if (itemType == PainterItemType.BombRain) {
            ActivateBombRain(ownerId);
        }
    }

    public void ActivateBombRain(byte ownerId) {
        activeEffects.Add(new BombRainEffect(ownerId, bombRainSplatCount, bombRainSplatInterval));
    }

    public void Tick(float deltaTime, NativeArray<Color32> texturePixels) {
        for (int i = activeEffects.Count - 1; i >= 0; i--) {
            BombRainEffect effect = activeEffects[i];
            effect.Tick(deltaTime, paintMap, random, texturePixels);

            if (effect.IsFinished) {
                int lastIndex = activeEffects.Count - 1;
                activeEffects[i] = activeEffects[lastIndex];
                activeEffects.RemoveAt(lastIndex);
            }
        }
    }

    private sealed class BombRainEffect {
        public bool IsFinished {
            get { return remainingSplats <= 0; }
        }

        private readonly byte ownerId;
        private readonly float splatInterval;
        private int remainingSplats;
        private float splatTimer;

        public BombRainEffect(byte ownerId, int splatCount, float splatInterval) {
            this.ownerId = ownerId;
            this.splatInterval = splatInterval;
            remainingSplats = splatCount;
            splatTimer = 0.0f;
        }

        public void Tick(
            float deltaTime,
            BattlePaintMap paintMap,
            System.Random random,
            NativeArray<Color32> texturePixels
        ) {
            splatTimer -= deltaTime;

            while (splatTimer <= 0.0f && remainingSplats > 0) {
                DropSplat(paintMap, random, texturePixels);
                remainingSplats--;
                splatTimer += splatInterval;
            }
        }

        private void DropSplat(BattlePaintMap paintMap, System.Random random, NativeArray<Color32> texturePixels) {
            for (int attempt = 0; attempt < MaxPositionAttemptsPerSplat; attempt++) {
                int x = random.Next(0, paintMap.Width);
                int y = random.Next(0, paintMap.Height);

                if (paintMap.IsPaintable(x, y)) {
                    paintMap.PaintStamp(new Vector2Int(x, y), ownerId, texturePixels);
                    return;
                }
            }
        }
    }
}
