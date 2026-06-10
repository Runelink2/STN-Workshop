using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEngine;

public sealed class BattlePaintersGame : MonoBehaviour {
    [Header("Canvas")]
    [SerializeField] private int textureWidth = 640;
    [SerializeField] private int textureHeight = 480;
    [SerializeField] private float pixelsPerUnit = 100.0f;
    [SerializeField] private int brushRadius = 16;
    [SerializeField] private Color backgroundColor = new Color(0.94f, 0.94f, 0.9f, 1.0f);

    [Header("Background Icon")]
    // Optional sprite baked into the unpainted canvas at startup, stretched to
    // cover the whole arena. Strokes paint over it; it never affects coverage
    // scoring. Transparent areas show backgroundColor.
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private float backgroundSpriteOpacity = 1.0f;
    // Unpaintable rim around the arena: players can't reach it, paint never
    // covers it, and the background stays visible there. Excluded from
    // coverage scoring.
    [SerializeField] private int arenaBorderPixels = 3;

    [Header("General")]
    [SerializeField] private bool animate = true;
    [SerializeField] private int cpuSeed = 12877;
    [SerializeField] private bool showHud = true;
    [SerializeField] private Camera outputCamera;
    [SerializeField] private bool showStandaloneGui = true;
    [SerializeField] private Vector3 arcadeCabinetOffset = new Vector3(0.0f, -300.0f, 0.0f);
    [SerializeField] private bool showPlayerMarkers = true;

    [Header("Match Timer")]
    [SerializeField] private int matchBlockCount = 20;
    [SerializeField] private float matchBlockDuration = 5.0f;

    [Header("Item Spawning")]
    // Timer lights burn out right to left; an item drops when the light at one
    // of these indices (counted left to right) goes out: gaps of 4-4-4-5 lights.
    [SerializeField] private int[] itemLightIndices = { 15, 10, 5 };
    [SerializeField] private int itemIconRadius = 10;

    [Header("Item: Bomb Rain")]
    [SerializeField] private int bombRainSplatCount = 24;
    [SerializeField] private float bombRainSplatInterval = 0.08f;

    [Header("Item: Freeze")]
    [SerializeField] private float freezeDuration = 15.0f;
    // Replaces the brush icon (same transform, offset and scale) while a player is frozen.
    [SerializeField] private Sprite frozenIconSprite;

    [Header("Item: No Paint")]
    [SerializeField] private float noPaintDuration = 15.0f;
    // Shared paintless brush animation, shown for any player hit by NoPaint.
    [SerializeField] private Sprite[] blankBrushFrames;

    [Header("Item: Speed Boost")]
    [SerializeField] private float speedBoostDuration = 10.0f;
    [SerializeField] private float speedBoostMultiplier = 1.5f;

    [Header("Item: Big Brush")]
    [SerializeField] private float bigBrushDuration = 10.0f;
    [SerializeField] private float bigBrushRadiusMultiplier = 1.5f;

    [Header("Item: Big Explosion")]
    [SerializeField] private float bigExplosionRadiusMultiplier = 8.0f;
    [SerializeField] private float bigExplosionParticleDuration = 3.0f;

    [Header("Collision Bounce")]
    // Player-vs-player collision: a purely visual hop (logical position,
    // painting and the shadow stay grounded) followed by a movement slow.
    [SerializeField] private float collisionBounceDuration = 1.2f;
    [SerializeField] private float collisionBounceHeightPixels = 36.0f;
    [SerializeField] private float collisionSlowDuration = 10.0f;
    [SerializeField] private float collisionSlowMultiplier = 0.5f;
    // Overlaid on the brush while a player is collision-slowed, once the bounce has landed.
    [SerializeField] private Sprite bandageIconSprite;

    [Header("Brush Icon")]
    // Per-player brush icon sets. Each entry ping-pongs through its frames
    // (0 1 2 3 2 1 0 ...); a single assigned frame stays static.
    [SerializeField] private PlayerBrushSet[] playerBrushSets;
    [SerializeField] private float brushFrameDuration = 0.12f;
    [SerializeField] private float brushHeightPixels = 22.0f;
    [SerializeField] private float brushFrameScale = 0.5f;
    // Drawn at the painter's base while the brush hovers above it.
    [SerializeField] private Sprite brushShadowSprite;
    [SerializeField] private float brushShadowHeightPixels = 0.0f;

    [Header("Direction Arrow")]
    [SerializeField] private Sprite directionArrowSprite;
    [SerializeField] private float arrowOrbitRadiusPixels = 26.0f;
    [SerializeField] private float arrowScale = 0.5f;

    [Header("Players")]
    [SerializeField] private PlayerSlot[] playerSlots = {
        new PlayerSlot(
            "P1",
            1,
            PainterInputType.Human,
            new Vector2(0.45f, 0.55f),
            135.0f,
            new Color(0.988f, 0.38f, 0.675f, 1.0f),
            100.0f,
            135.0f,
            1,
            0.42f,
            0.78f,
            KeyCode.LeftArrow,
            KeyCode.RightArrow
        ),
        new PlayerSlot(
            "P2",
            2,
            PainterInputType.Cpu,
            new Vector2(0.55f, 0.55f),
            45.0f,
            new Color(0.161f, 0.627f, 0.996f, 1.0f),
            100.0f,
            135.0f,
            -1,
            0.47f,
            0.78f,
            KeyCode.LeftArrow,
            KeyCode.RightArrow
        ),
        new PlayerSlot(
            "P3",
            3,
            PainterInputType.Cpu,
            new Vector2(0.55f, 0.45f),
            315.0f,
            new Color(0.341f, 0.886f, 0.333f, 1.0f),
            100.0f,
            135.0f,
            1,
            0.52f,
            0.78f,
            KeyCode.LeftArrow,
            KeyCode.RightArrow
        ),
        new PlayerSlot(
            "P4",
            4,
            PainterInputType.Cpu,
            new Vector2(0.45f, 0.45f),
            225.0f,
            new Color(1.0f, 0.773f, 0.353f, 1.0f),
            100.0f,
            135.0f,
            -1,
            0.45f,
            0.78f,
            KeyCode.LeftArrow,
            KeyCode.RightArrow
        )
    };

    private const float ArcadeHudShadowOffset = 0.018f;
    private const int ArcadeHudTextSortingOrder = 120;
    private const int ArcadeHudPlateSortingOrder = 110;

    private sealed class HudText {
        public TextMesh foreground;
        public TextMesh shadow;
    }

    private readonly StringBuilder hudBuilder = new StringBuilder(192);

    private BattlePaintMap paintMap;
    private PaintEffectManager paintEffects;
    private Texture2D paintTexture;
    private Sprite displaySprite;
    private PainterMovement[] painters;
    private IPainterInputSource[] inputSources;
    private Transform[] markerTransforms;
    private SpriteRenderer[] markerRenderers;
    private Sprite[] markerSprites;
    private Texture2D[] markerTextures;
    private Transform[] brushTransforms;
    private SpriteRenderer[] brushRenderers;
    private Transform[] brushShadowTransforms;
    private SpriteRenderer[] bandageRenderers;
    private Transform[] arrowTransforms;
    private float brushAnimationTimer;
    private int[] brushAnimationSteps;
    private int[] currentBrushFrameIndices;
    private PlayerSlot[] runtimeSlots;
    private Color32[] runtimePlayerColors;
    private string coverageText;
    private GUIStyle hudStyle;
    private float hudRefreshTimer;
    private MatchState matchState = MatchState.Ready;
    private float matchTimeRemaining;
    private string matchSummaryText;
    private GUIStyle centerMessageStyle;
    private readonly GUIContent centerMessageContent = new GUIContent();
    private Texture2D lightOnTexture;
    private Texture2D lightOffTexture;
    private Texture2D itemLightOnTexture;
    private Sprite lightOnSprite;
    private Sprite lightOffSprite;
    private Sprite itemLightOnSprite;
    private Texture2D hudPlateTexture;
    private Sprite hudPlateSprite;
    private Texture2D[] itemTextures;
    private Sprite[] itemSprites;
    private System.Random itemRandom;
    private bool[] itemSpawnedFlags;
    private PainterItemType[] queuedItemTypes;
    private int nextQueuedItemIndex;
    private float[] frozenTimers;
    private float[] noPaintTimers;
    private float[] speedBoostTimers;
    private float[] bigBrushTimers;
    private float[] bounceTimers;
    private float[] collisionSlowTimers;
    // Visual-only heading used by the arrow while frozen: input still turns it
    // so the player looks like they're trying to steer, but the painter's real
    // heading is untouched.
    private float[] frozenVisualHeadings;
    private bool[] painterPairOverlap;
    private bool[] brushShowingBlank;
    private bool[] brushShowingFrozen;
    private ParticleSystem[] boostTrails;
    private ParticleSystem[] bigBrushTrails;
    private ParticleSystem explosionParticles;
    private Texture2D boostTrailTexture;
    private Material boostTrailMaterial;
    private bool arcadeOutputMode;
    private int renderLayer;
    private float cameraWorldHalfWidth = 3.58f;
    private float cameraWorldHalfHeight = 2.68f;
    private Transform runtimeRoot;
    private Transform arcadeHudRoot;
    private GameObject arcadeMessagePlate;
    private HudText arcadeCoverageText;
    private HudText arcadeTimeText;
    private HudText arcadeMessageText;
    private HudText arcadePromptText;
    private SpriteRenderer[] arcadeTimerLights;
    private readonly List<ItemPickup> activeItems = new List<ItemPickup>(4);

    private void Start() {
        InitializeRuntime();
    }

    private void InitializeRuntime() {
        matchState = MatchState.Ready;
        matchSummaryText = string.Empty;
        matchBlockCount = Mathf.Max(1, matchBlockCount);
        matchBlockDuration = Mathf.Max(0.1f, matchBlockDuration);

        itemRandom = new System.Random(cpuSeed + 777);
        itemSpawnedFlags = new bool[itemLightIndices == null ? 0 : itemLightIndices.Length];

        CreatePaintMap();
        CreatePaintEffects();
        CreateDisplay();
        CreatePainters();
        CreatePlayerMarkers();
        CreatePlayerIcons();
        CreateStatusVisuals();
        CreateTimerLightTextures();
        CreateItemAssets();
        ConfigureCamera();
        CreateArcadeHud();
        PaintStartingPositions();
        UpdateCoverageText();
        UpdateArcadeHud();

        Debug.Log("Paint Battle ready. " + coverageText.Replace("\n", " "));
    }

    private void Update() {
        if (paintMap == null || paintTexture == null || painters == null) {
            return;
        }

        bool startPressed = IsStartPressed();

        if (matchState == MatchState.Ready) {
            if (startPressed) {
                StartMatch();
            }

            // Keep icon placement live so inspector tweaks show before the match starts.
            for (int i = 0; i < painters.Length; i++) {
                UpdatePlayerIcons(i);
            }

            UpdateArcadeHud();
            return;
        }

        if (matchState == MatchState.Finished) {
            if (startPressed) {
                RestartMatch();
            }

            UpdateArcadeHud();
            return;
        }

        if (!animate) {
            UpdateArcadeHud();
            return;
        }

        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0.0f) {
            return;
        }

        NativeArray<Color32> pixels = paintTexture.GetPixelData<Color32>(0);
        Rect arenaBounds = GetArenaBounds();
        float painterRadius = GetPainterRadius();

        UpdateBrushAnimation(deltaTime);

        for (int i = 0; i < painters.Length; i++) {
            if (frozenTimers[i] > 0.0f) {
                frozenTimers[i] = Mathf.Max(0.0f, frozenTimers[i] - deltaTime);
                TickFrozenSteering(i, deltaTime, arenaBounds, painterRadius);
                UpdatePlayerIcons(i);
                continue;
            }

            bool hasPaint = noPaintTimers[i] <= 0.0f;

            if (!hasPaint) {
                noPaintTimers[i] = Mathf.Max(0.0f, noPaintTimers[i] - deltaTime);
            }

            bool boosted = speedBoostTimers[i] > 0.0f;

            if (boosted) {
                speedBoostTimers[i] = Mathf.Max(0.0f, speedBoostTimers[i] - deltaTime);
            }

            bool bigBrush = bigBrushTimers[i] > 0.0f;

            if (bigBrush) {
                bigBrushTimers[i] = Mathf.Max(0.0f, bigBrushTimers[i] - deltaTime);
            }

            if (bounceTimers[i] > 0.0f) {
                bounceTimers[i] = Mathf.Max(0.0f, bounceTimers[i] - deltaTime);
            }

            bool slowed = collisionSlowTimers[i] > 0.0f;

            if (slowed) {
                collisionSlowTimers[i] = Mathf.Max(0.0f, collisionSlowTimers[i] - deltaTime);
            }

            PainterMovement painter = painters[i];
            float speedMultiplier = boosted ? Mathf.Max(0.1f, speedBoostMultiplier) : 1.0f;

            if (slowed) {
                speedMultiplier *= Mathf.Clamp(collisionSlowMultiplier, 0.05f, 1.0f);
            }

            painter.SpeedMultiplier = speedMultiplier;
            Vector2 itemTargetPosition;
            bool hasItemTarget = TryGetNearestItemPosition(painter.Position, out itemTargetPosition);
            PainterInputContext inputContext = new PainterInputContext(
                painter.Position,
                painter.HeadingDegrees,
                arenaBounds,
                painterRadius,
                hasItemTarget,
                itemTargetPosition
            );
            float turnInput = inputSources[i] == null
                ? 0.0f
                : inputSources[i].GetTurnInput(deltaTime, inputContext);

            painter.Tick(deltaTime, turnInput, arenaBounds, painterRadius);

            if (hasPaint) {
                float brushScale = bigBrush ? Mathf.Max(0.1f, bigBrushRadiusMultiplier) : 1.0f;
                paintMap.PaintSegment(painter.PreviousPosition, painter.Position, painter.OwnerId, pixels, brushScale);
            }

            UpdatePlayerMarker(i);
            UpdatePlayerIcons(i);
        }

        DetectPainterCollisions();
        UpdateItemPickups();
        UpdateMarkerVisibility();
        UpdateStatusVisuals();
        paintEffects.Tick(deltaTime, pixels);

        paintTexture.Apply(false, false);

        matchTimeRemaining -= deltaTime;

        if (matchTimeRemaining <= 0.0f) {
            matchTimeRemaining = 0.0f;
            FinishMatch();
            return;
        }

        UpdateItemSpawning();
        HandleDebugItemSpawning();

        hudRefreshTimer -= deltaTime;

        if (hudRefreshTimer <= 0.0f) {
            hudRefreshTimer = 0.12f;
            UpdateCoverageText();
        }

        UpdateArcadeHud();
    }

    private void StartMatch() {
        matchState = MatchState.Playing;
        matchTimeRemaining = matchBlockCount * matchBlockDuration;
        matchSummaryText = string.Empty;

        if (itemSpawnedFlags != null) {
            for (int i = 0; i < itemSpawnedFlags.Length; i++) {
                itemSpawnedFlags[i] = false;
            }
        }

        for (int i = 0; i < frozenTimers.Length; i++) {
            frozenTimers[i] = 0.0f;
            noPaintTimers[i] = 0.0f;
            speedBoostTimers[i] = 0.0f;
            bigBrushTimers[i] = 0.0f;
            bounceTimers[i] = 0.0f;
            collisionSlowTimers[i] = 0.0f;
        }

        for (int i = 0; i < painterPairOverlap.Length; i++) {
            painterPairOverlap[i] = false;
        }

        BuildItemQueue();
        UpdateCoverageText();
        UpdateArcadeHud();
    }

    private bool IsStartPressed() {
        return Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetMouseButtonDown(0);
    }

    private void RestartMatch() {
        ResetRuntime();
        StartMatch();
    }

    private void ResetRuntime() {
        ClearItems();
        ClearStatusVisuals();
        DestroyRuntimeObjects();
        DestroyRuntimeAssets();
        activeItems.Clear();
        InitializeRuntime();
    }

    private void DestroyRuntimeObjects() {
        if (runtimeRoot == null) {
            return;
        }

        runtimeRoot.gameObject.SetActive(false);
        Destroy(runtimeRoot.gameObject);
        runtimeRoot = null;
        arcadeHudRoot = null;
        arcadeMessagePlate = null;
        arcadeCoverageText = null;
        arcadeTimeText = null;
        arcadeMessageText = null;
        arcadePromptText = null;
        arcadeTimerLights = null;
    }

    private void BuildItemQueue() {
        queuedItemTypes = new PainterItemType[] {
            PainterItemType.BombRain,
            PainterItemType.Freeze,
            PainterItemType.NoPaint,
            PainterItemType.SpeedBoost,
            PainterItemType.BigBrush,
            PainterItemType.BigExplosion
        };

        for (int i = queuedItemTypes.Length - 1; i > 0; i--) {
            int swapIndex = itemRandom.Next(0, i + 1);
            PainterItemType temp = queuedItemTypes[i];
            queuedItemTypes[i] = queuedItemTypes[swapIndex];
            queuedItemTypes[swapIndex] = temp;
        }

        nextQueuedItemIndex = 0;
    }

    private void FinishMatch() {
        matchState = MatchState.Finished;
        ClearItems();
        ClearStatusVisuals();
        UpdateCoverageText();
        matchSummaryText = BuildMatchSummary();
        UpdateArcadeHud();
    }

    private string BuildMatchSummary() {
        int bestIndex = -1;
        float bestCoverage = -1.0f;
        bool tie = false;

        for (int i = 0; i < runtimeSlots.Length; i++) {
            float coverage = paintMap.GetCoverage01(runtimeSlots[i].ownerId);

            if (coverage > bestCoverage + 0.0001f) {
                bestCoverage = coverage;
                bestIndex = i;
                tie = false;
            } else if (coverage > bestCoverage - 0.0001f) {
                tie = true;
            }
        }

        hudBuilder.Length = 0;
        hudBuilder.AppendLine("Time's up!");

        if (tie || bestIndex < 0) {
            hudBuilder.AppendLine("It's a draw!");
        } else {
            PlayerSlot winner = runtimeSlots[bestIndex];
            hudBuilder.Append(winner.label);
            hudBuilder.Append(" (");
            hudBuilder.Append(FormatInputType(winner.inputType));
            hudBuilder.Append(") wins!");
            hudBuilder.AppendLine();
        }

        hudBuilder.AppendLine();

        for (int i = 0; i < runtimeSlots.Length; i++) {
            PlayerSlot slot = runtimeSlots[i];
            float percentage = paintMap.GetCoverage01(slot.ownerId) * 100.0f;

            hudBuilder.Append(slot.label);
            hudBuilder.Append(" (");
            hudBuilder.Append(FormatInputType(slot.inputType));
            hudBuilder.Append("): ");
            hudBuilder.Append(percentage.ToString("0.0"));
            hudBuilder.Append('%');

            if (i < runtimeSlots.Length - 1) {
                hudBuilder.AppendLine();
            }
        }

        return hudBuilder.ToString();
    }

    private void OnGUI() {
        if (arcadeOutputMode || !showStandaloneGui) {
            return;
        }

        DrawHud();
        DrawTimerLights();
        DrawCenterMessage();
    }

    private void DrawHud() {
        if (!showHud || string.IsNullOrEmpty(coverageText)) {
            return;
        }

        if (hudStyle == null) {
            hudStyle = new GUIStyle(GUI.skin.box);
            hudStyle.alignment = TextAnchor.UpperLeft;
            hudStyle.fontSize = 14;
            hudStyle.padding = new RectOffset(12, 12, 10, 10);
            hudStyle.normal.textColor = Color.white;
        }

        float height = 42.0f + (paintMap.PlayerCount + 1) * 18.0f;
        GUI.Box(new Rect(12.0f, 12.0f, 290.0f, height), coverageText, hudStyle);
    }

    private void DrawTimerLights() {
        if (lightOnTexture == null || lightOffTexture == null) {
            return;
        }

        int litCount = GetLitLightCount();
        float size = lightOnTexture.width;
        float spacing = 8.0f;
        float totalWidth = matchBlockCount * size + (matchBlockCount - 1) * spacing;
        float x = (Screen.width - totalWidth) * 0.5f;
        float y = Screen.height - size - 14.0f;

        for (int i = 0; i < matchBlockCount; i++) {
            Texture2D lightTexture;

            if (i < litCount) {
                lightTexture = IsItemLightIndex(i) ? itemLightOnTexture : lightOnTexture;
            } else {
                lightTexture = lightOffTexture;
            }

            GUI.DrawTexture(new Rect(x + i * (size + spacing), y, size, size), lightTexture);
        }
    }

    private bool IsItemLightIndex(int lightIndex) {
        if (itemLightIndices == null) {
            return false;
        }

        for (int i = 0; i < itemLightIndices.Length; i++) {
            if (itemLightIndices[i] == lightIndex) {
                return true;
            }
        }

        return false;
    }

    private int GetLitLightCount() {
        if (matchState == MatchState.Ready) {
            return matchBlockCount;
        }

        if (matchState == MatchState.Finished) {
            return 0;
        }

        return Mathf.Clamp(Mathf.CeilToInt(matchTimeRemaining / matchBlockDuration), 0, matchBlockCount);
    }

    private void DrawCenterMessage() {
        string message;

        if (matchState == MatchState.Ready) {
            message = "Paint Battle\nPress Space / Enter to start";
        } else if (matchState == MatchState.Finished) {
            message = matchSummaryText + "\n\nPress Space / Enter to play again";
        } else {
            return;
        }

        if (string.IsNullOrEmpty(message)) {
            return;
        }

        if (centerMessageStyle == null) {
            centerMessageStyle = new GUIStyle(GUI.skin.box);
            centerMessageStyle.alignment = TextAnchor.MiddleCenter;
            centerMessageStyle.fontSize = 20;
            centerMessageStyle.padding = new RectOffset(24, 24, 18, 18);
            centerMessageStyle.normal.textColor = Color.white;
        }

        centerMessageContent.text = message;

        float width = 420.0f;
        float height = centerMessageStyle.CalcHeight(centerMessageContent, width);
        Rect rect = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height
        );

        GUI.Box(rect, centerMessageContent, centerMessageStyle);
    }

    private void CreateArcadeHud() {
        if (!showHud || !arcadeOutputMode) {
            return;
        }

        GameObject hudObject = new GameObject("Arcade HUD");
        hudObject.transform.SetParent(RuntimeParent, false);
        arcadeHudRoot = hudObject.transform;
        ApplyRenderLayer(hudObject);

        if (hudPlateSprite == null) {
            hudPlateTexture = CreateSolidTexture(new Color32(255, 255, 255, 255));
            hudPlateSprite = CreateRuntimeSprite(hudPlateTexture, 1.0f);
        }

        float leftX = -cameraWorldHalfWidth + 0.28f;
        float rightX = cameraWorldHalfWidth - 0.28f;
        float topY = cameraWorldHalfHeight - 0.22f;
        float scoreUiScale = 0.7f;
        float bottomY = -cameraWorldHalfHeight + 0.28f;

        CreateHudPlate(
            arcadeHudRoot,
            new Vector3(0.0f, topY - 0.22f * scoreUiScale, -0.18f),
            new Vector3(cameraWorldHalfWidth * 2.0f - 0.28f, 0.62f * scoreUiScale, 1.0f)
        );
        CreateHudPlate(
            arcadeHudRoot,
            new Vector3(0.0f, bottomY, -0.18f),
            new Vector3(cameraWorldHalfWidth * 2.0f - 0.42f, 0.36f, 1.0f)
        );
        arcadeMessagePlate = CreateHudPlate(arcadeHudRoot, new Vector3(0.0f, 0.0f, -0.16f), new Vector3(5.2f, 1.26f, 1.0f));
        arcadeCoverageText = CreateHudText(
            arcadeHudRoot,
            "Coverage",
            new Vector3(leftX, topY, -0.1f),
            0.035f * scoreUiScale,
            FontStyle.Bold,
            TextAnchor.UpperLeft,
            TextAlignment.Left
        );
        arcadeTimeText = CreateHudText(
            arcadeHudRoot,
            "Time",
            new Vector3(rightX, topY, -0.1f),
            0.045f * scoreUiScale,
            FontStyle.Bold,
            TextAnchor.UpperRight,
            TextAlignment.Right
        );
        arcadeMessageText = CreateHudText(
            arcadeHudRoot,
            "Message",
            new Vector3(0.0f, 0.28f, -0.1f),
            0.07f,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            TextAlignment.Center
        );
        arcadePromptText = CreateHudText(
            arcadeHudRoot,
            "Prompt",
            new Vector3(0.0f, -0.48f, -0.1f),
            0.04f,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            TextAlignment.Center
        );

        CreateArcadeTimerLights(bottomY);
    }

    private GameObject CreateHudPlate(Transform parent, Vector3 localPosition, Vector3 localScale) {
        GameObject plateObject = new GameObject("HUD Plate");
        plateObject.layer = renderLayer;
        plateObject.transform.SetParent(parent, false);
        plateObject.transform.localPosition = localPosition;
        plateObject.transform.localScale = localScale;

        SpriteRenderer renderer = plateObject.AddComponent<SpriteRenderer>();
        renderer.sprite = hudPlateSprite;
        renderer.color = new Color(0.02f, 0.022f, 0.03f, 0.74f);
        renderer.sortingOrder = ArcadeHudPlateSortingOrder;
        return plateObject;
    }

    private HudText CreateHudText(
        Transform parent,
        string name,
        Vector3 localPosition,
        float characterSize,
        FontStyle fontStyle,
        TextAnchor anchor,
        TextAlignment alignment
    ) {
        Vector3 shadowPosition = localPosition + new Vector3(ArcadeHudShadowOffset, -ArcadeHudShadowOffset, 0.0f);
        TextMesh shadow = CreateHudTextMesh(
            parent,
            name + " Shadow",
            shadowPosition,
            characterSize,
            fontStyle,
            anchor,
            alignment,
            new Color(0.0f, 0.0f, 0.0f, 0.72f),
            ArcadeHudTextSortingOrder - 1
        );
        TextMesh foreground = CreateHudTextMesh(
            parent,
            name,
            localPosition,
            characterSize,
            fontStyle,
            anchor,
            alignment,
            Color.white,
            ArcadeHudTextSortingOrder
        );
        return new HudText { foreground = foreground, shadow = shadow };
    }

    private TextMesh CreateHudTextMesh(
        Transform parent,
        string name,
        Vector3 localPosition,
        float characterSize,
        FontStyle fontStyle,
        TextAnchor anchor,
        TextAlignment alignment,
        Color color,
        int sortingOrder
    ) {
        GameObject textObject = new GameObject(name);
        textObject.layer = renderLayer;
        textObject.transform.SetParent(parent, false);
        textObject.transform.localPosition = localPosition;

        TextMesh textMesh = textObject.AddComponent<TextMesh>();
        textMesh.anchor = anchor;
        textMesh.alignment = alignment;
        textMesh.characterSize = characterSize;
        textMesh.fontSize = 96;
        textMesh.fontStyle = fontStyle;
        textMesh.color = color;

        MeshRenderer renderer = textObject.GetComponent<MeshRenderer>();
        renderer.sortingOrder = sortingOrder;
        return textMesh;
    }

    private void CreateArcadeTimerLights(float y) {
        if (lightOnSprite == null || lightOffSprite == null || itemLightOnSprite == null) {
            return;
        }

        arcadeTimerLights = new SpriteRenderer[matchBlockCount];
        float lightWidth = lightOnSprite.bounds.size.x * 0.62f;
        float spacing = 0.05f;
        float totalWidth = matchBlockCount * lightWidth + Mathf.Max(0, matchBlockCount - 1) * spacing;
        float startX = -totalWidth * 0.5f + lightWidth * 0.5f;

        for (int i = 0; i < matchBlockCount; i++) {
            GameObject lightObject = new GameObject("Timer Light " + (i + 1));
            lightObject.layer = renderLayer;
            lightObject.transform.SetParent(arcadeHudRoot, false);
            lightObject.transform.localPosition = new Vector3(startX + i * (lightWidth + spacing), y, -0.11f);
            lightObject.transform.localScale = Vector3.one * 0.62f;

            SpriteRenderer renderer = lightObject.AddComponent<SpriteRenderer>();
            renderer.sprite = lightOnSprite;
            renderer.sortingOrder = ArcadeHudPlateSortingOrder + 2;
            arcadeTimerLights[i] = renderer;
        }
    }

    private void UpdateArcadeHud() {
        if (!arcadeOutputMode || arcadeHudRoot == null) {
            return;
        }

        bool showCenterMessage = matchState != MatchState.Playing;
        if (arcadeMessagePlate != null && arcadeMessagePlate.activeSelf != showCenterMessage) {
            arcadeMessagePlate.SetActive(showCenterMessage);
        }

        SetHudText(arcadeCoverageText, coverageText);
        SetHudText(arcadeTimeText, GetArcadeTimeText());

        if (matchState == MatchState.Ready) {
            SetHudText(arcadeMessageText, "PAINT BATTLE");
            SetHudText(arcadePromptText, "SPACE / ENTER TO START");
        } else if (matchState == MatchState.Finished) {
            SetHudText(arcadeMessageText, matchSummaryText);
            SetHudText(arcadePromptText, "SPACE / ENTER TO PLAY AGAIN");
        } else {
            SetHudText(arcadeMessageText, string.Empty);
            SetHudText(arcadePromptText, string.Empty);
        }

        UpdateArcadeTimerLights();
    }

    private string GetArcadeTimeText() {
        if (matchState == MatchState.Ready) {
            return "TIME " + FormatSeconds(matchBlockCount * matchBlockDuration);
        }

        if (matchState == MatchState.Finished) {
            return "TIME UP";
        }

        return "TIME " + FormatSeconds(matchTimeRemaining);
    }

    private static string FormatSeconds(float secondsRemaining) {
        int seconds = Mathf.Max(0, Mathf.CeilToInt(secondsRemaining));
        int minutes = seconds / 60;
        seconds %= 60;
        return minutes.ToString("00") + ":" + seconds.ToString("00");
    }

    private void UpdateArcadeTimerLights() {
        if (arcadeTimerLights == null) {
            return;
        }

        int litCount = GetLitLightCount();
        for (int i = 0; i < arcadeTimerLights.Length; i++) {
            if (arcadeTimerLights[i] == null) {
                continue;
            }

            if (i < litCount) {
                arcadeTimerLights[i].sprite = IsItemLightIndex(i) ? itemLightOnSprite : lightOnSprite;
            } else {
                arcadeTimerLights[i].sprite = lightOffSprite;
            }
        }
    }

    private static void SetHudText(HudText hudText, string value) {
        if (hudText == null) {
            return;
        }

        hudText.foreground.text = value;
        hudText.shadow.text = value;
    }

    private Transform RuntimeParent {
        get {
            if (runtimeRoot == null) {
                GameObject root = new GameObject("Paint Battle Runtime");
                root.transform.SetParent(transform, false);
                runtimeRoot = root.transform;
                ApplyRenderLayer(root);
            }

            return runtimeRoot;
        }
    }

    private void OnDestroy() {
        DestroyRuntimeObjects();
        DestroyRuntimeAssets();
    }

    private void CreatePaintMap() {
        textureWidth = Mathf.Max(32, textureWidth);
        textureHeight = Mathf.Max(32, textureHeight);
        brushRadius = Mathf.Max(1, brushRadius);
        brushRadius = Mathf.Min(brushRadius, Mathf.Max(1, Mathf.Min(textureWidth, textureHeight) / 2 - 2));
        pixelsPerUnit = Mathf.Max(1.0f, pixelsPerUnit);

        runtimeSlots = GetRuntimeSlots();
        int playerCount = Mathf.Clamp(runtimeSlots.Length, 1, 254);
        runtimePlayerColors = CreatePlayerColors(playerCount);

        arenaBorderPixels = Mathf.Clamp(arenaBorderPixels, 0, Mathf.Min(textureWidth, textureHeight) / 4);

        Color32[] backgroundPixels = CreateBackgroundPixels();

        paintMap = new BattlePaintMap(
            textureWidth,
            textureHeight,
            playerCount,
            brushRadius,
            runtimePlayerColors,
            backgroundPixels,
            CreateArenaPaintableMask()
        );

        paintTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        paintTexture.filterMode = FilterMode.Bilinear;
        paintTexture.wrapMode = TextureWrapMode.Clamp;

        NativeArray<Color32> pixels = paintTexture.GetPixelData<Color32>(0);

        for (int i = 0; i < pixels.Length; i++) {
            pixels[i] = backgroundPixels[i];
        }

        paintTexture.Apply(false, false);
    }

    private Color32[] CreateBackgroundPixels() {
        Color32[] pixels = new Color32[textureWidth * textureHeight];
        Color32 background = backgroundColor;

        for (int i = 0; i < pixels.Length; i++) {
            pixels[i] = background;
        }

        BlitBackgroundSprite(pixels);
        return pixels;
    }

    private bool[] CreateArenaPaintableMask() {
        if (arenaBorderPixels <= 0) {
            return null;
        }

        bool[] mask = new bool[textureWidth * textureHeight];

        for (int y = 0; y < textureHeight; y++) {
            bool rowInside = y >= arenaBorderPixels && y < textureHeight - arenaBorderPixels;
            int baseIndex = y * textureWidth;

            for (int x = 0; x < textureWidth; x++) {
                mask[baseIndex + x] = rowInside
                    && x >= arenaBorderPixels
                    && x < textureWidth - arenaBorderPixels;
            }
        }

        return mask;
    }

    private void BlitBackgroundSprite(Color32[] pixels) {
        if (backgroundSprite == null || backgroundSprite.texture == null) {
            return;
        }

        Texture2D spriteTexture = backgroundSprite.texture;

        if (!spriteTexture.isReadable) {
            Debug.LogWarning(
                "Background sprite texture '" + spriteTexture.name +
                "' is not readable. Enable Read/Write in its import settings to bake it into the canvas."
            );
            return;
        }

        float opacity = Mathf.Clamp01(backgroundSpriteOpacity);

        if (opacity <= 0.0f) {
            return;
        }

        Rect spriteRect = backgroundSprite.textureRect;

        for (int y = 0; y < textureHeight; y++) {
            float v = (y + 0.5f) / textureHeight;
            float textureV = (spriteRect.y + v * spriteRect.height) / spriteTexture.height;
            int baseIndex = y * textureWidth;

            for (int x = 0; x < textureWidth; x++) {
                float u = (x + 0.5f) / textureWidth;
                float textureU = (spriteRect.x + u * spriteRect.width) / spriteTexture.width;
                Color sample = spriteTexture.GetPixelBilinear(textureU, textureV);
                float alpha = sample.a * opacity;

                if (alpha <= 0.0f) {
                    continue;
                }

                int index = baseIndex + x;
                Color32 sampleColor = new Color32(
                    (byte)Mathf.RoundToInt(sample.r * 255.0f),
                    (byte)Mathf.RoundToInt(sample.g * 255.0f),
                    (byte)Mathf.RoundToInt(sample.b * 255.0f),
                    255
                );
                pixels[index] = Color32.Lerp(pixels[index], sampleColor, alpha);
            }
        }
    }

    private void CreatePaintEffects() {
        paintEffects = new PaintEffectManager(
            paintMap,
            cpuSeed + 31337,
            bombRainSplatCount,
            bombRainSplatInterval
        );
    }

    private PlayerSlot[] GetRuntimeSlots() {
        PlayerSlot[] defaultSlots = CreateDefaultSlots();

        if (playerSlots == null || playerSlots.Length == 0) {
            return defaultSlots;
        }

        int playerCount = Mathf.Clamp(playerSlots.Length, 1, 254);
        PlayerSlot[] slots = new PlayerSlot[playerCount];

        for (int i = 0; i < playerCount; i++) {
            PlayerSlot source = playerSlots[i] == null ? defaultSlots[i % defaultSlots.Length] : playerSlots[i];
            slots[i] = CreateRuntimeSlot(source, defaultSlots[i % defaultSlots.Length], i, playerCount);
        }

        return slots;
    }

    private PlayerSlot CreateRuntimeSlot(PlayerSlot source, PlayerSlot fallback, int index, int playerCount) {
        PlayerSlot slot = new PlayerSlot();
        slot.label = string.IsNullOrEmpty(source.label) ? fallback.label : source.label;
        slot.ownerId = source.ownerId <= 0 ? index + 1 : Mathf.Clamp(source.ownerId, 1, playerCount);
        slot.inputType = source.inputType;
        slot.normalizedStart = source.normalizedStart;
        slot.headingDegrees = source.headingDegrees;
        slot.paintColor = source.paintColor.a <= 0.0f ? fallback.paintColor : source.paintColor;
        slot.moveSpeedPixelsPerSecond = source.moveSpeedPixelsPerSecond <= 0.0f
            ? fallback.moveSpeedPixelsPerSecond
            : source.moveSpeedPixelsPerSecond;
        slot.turnSpeedDegreesPerSecond = source.turnSpeedDegreesPerSecond <= 0.0f
            ? fallback.turnSpeedDegreesPerSecond
            : source.turnSpeedDegreesPerSecond;
        slot.startingSteerDirection = source.startingSteerDirection < 0 ? -1 : 1;
        slot.normalBeatDuration = source.normalBeatDuration <= 0.0f
            ? fallback.normalBeatDuration
            : source.normalBeatDuration;
        slot.normalTurnStrength = source.normalTurnStrength <= 0.0f
            ? fallback.normalTurnStrength
            : Mathf.Clamp01(source.normalTurnStrength);
        slot.leftKey = source.leftKey == KeyCode.None ? fallback.leftKey : source.leftKey;
        slot.rightKey = source.rightKey == KeyCode.None ? fallback.rightKey : source.rightKey;

        return slot;
    }

    private Color32[] CreatePlayerColors(int playerCount) {
        Color32[] colors = new Color32[playerCount];
        bool[] assigned = new bool[playerCount];

        for (int i = 0; i < runtimeSlots.Length; i++) {
            int ownerIndex = Mathf.Clamp(runtimeSlots[i].ownerId, 1, playerCount) - 1;
            colors[ownerIndex] = runtimeSlots[i].paintColor;
            assigned[ownerIndex] = true;
        }

        for (int i = 0; i < playerCount; i++) {
            if (!assigned[i]) {
                colors[i] = Color.HSVToRGB(i / (float)Mathf.Max(1, playerCount), 0.72f, 0.95f);
            }
        }

        return colors;
    }

    private void CreateDisplay() {
        GameObject displayObject = new GameObject("Paint Surface");
        displayObject.transform.SetParent(RuntimeParent, false);

        SpriteRenderer renderer = displayObject.AddComponent<SpriteRenderer>();
        displaySprite = Sprite.Create(
            paintTexture,
            new Rect(0.0f, 0.0f, textureWidth, textureHeight),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit
        );

        renderer.sprite = displaySprite;
        renderer.sortingOrder = 0;
    }

    private void CreatePainters() {
        painters = new PainterMovement[paintMap.PlayerCount];
        inputSources = new IPainterInputSource[paintMap.PlayerCount];
        frozenTimers = new float[paintMap.PlayerCount];
        noPaintTimers = new float[paintMap.PlayerCount];
        speedBoostTimers = new float[paintMap.PlayerCount];
        bigBrushTimers = new float[paintMap.PlayerCount];
        bounceTimers = new float[paintMap.PlayerCount];
        collisionSlowTimers = new float[paintMap.PlayerCount];
        frozenVisualHeadings = new float[paintMap.PlayerCount];
        painterPairOverlap = new bool[paintMap.PlayerCount * paintMap.PlayerCount];

        Rect arenaBounds = GetArenaBounds();
        Rect centerBounds = GetPainterCenterBounds(arenaBounds, GetPainterRadius());

        for (int i = 0; i < painters.Length; i++) {
            PlayerSlot slot = runtimeSlots[i];
            Vector2 startPosition = new Vector2(
                Mathf.Lerp(centerBounds.xMin, centerBounds.xMax, Mathf.Clamp01(slot.normalizedStart.x)),
                Mathf.Lerp(centerBounds.yMin, centerBounds.yMax, Mathf.Clamp01(slot.normalizedStart.y))
            );
            float startHeadingDegrees = GetStartHeading(slot);

            inputSources[i] = CreateInputSource(slot, i);
            painters[i] = new PainterMovement(
                (byte)slot.ownerId,
                startPosition,
                startHeadingDegrees,
                slot.moveSpeedPixelsPerSecond,
                slot.turnSpeedDegreesPerSecond,
                arenaBounds,
                GetPainterRadius()
            );
        }
    }

    private float GetStartHeading(PlayerSlot slot) {
        if (slot.inputType != PainterInputType.Cpu) {
            return slot.headingDegrees;
        }

        return CpuPainterInput.GetOpeningCenteredHeading(
            slot.headingDegrees,
            slot.startingSteerDirection,
            slot.normalBeatDuration,
            slot.normalTurnStrength,
            slot.turnSpeedDegreesPerSecond
        );
    }

    private IPainterInputSource CreateInputSource(PlayerSlot slot, int slotIndex) {
        if (slot.inputType == PainterInputType.Human) {
            return new HumanPainterInput(slot.leftKey, slot.rightKey);
        }

        return new CpuPainterInput(
            cpuSeed + slotIndex * 997,
            slot.startingSteerDirection,
            slot.normalBeatDuration,
            slot.normalTurnStrength,
            0.0f
        );
    }

    private void CreatePlayerMarkers() {
        if (!showPlayerMarkers || painters == null) {
            return;
        }

        markerTransforms = new Transform[painters.Length];
        markerRenderers = new SpriteRenderer[painters.Length];
        markerSprites = new Sprite[painters.Length];
        markerTextures = new Texture2D[painters.Length];

        for (int i = 0; i < painters.Length; i++) {
            GameObject markerObject = new GameObject(runtimeSlots[i].label + " Marker");
            markerObject.transform.SetParent(RuntimeParent, false);

            Texture2D markerTexture = CreateMarkerTexture(runtimeSlots[i].paintColor);
            Sprite markerSprite = Sprite.Create(
                markerTexture,
                new Rect(0.0f, 0.0f, markerTexture.width, markerTexture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit
            );

            SpriteRenderer renderer = markerObject.AddComponent<SpriteRenderer>();
            renderer.sprite = markerSprite;
            renderer.sortingOrder = 10;

            markerTransforms[i] = markerObject.transform;
            markerRenderers[i] = renderer;
            markerSprites[i] = markerSprite;
            markerTextures[i] = markerTexture;
            UpdatePlayerMarker(i);
        }
    }

    private Texture2D CreateMarkerTexture(Color32 fillColor) {
        return CreateCircleTexture(brushRadius, fillColor);
    }

    private void CreatePlayerIcons() {
        if (painters == null) {
            return;
        }

        bool anyHasBrush = false;
        for (int i = 0; i < painters.Length; i++) {
            if (HasBrushFrames(i)) {
                anyHasBrush = true;
                break;
            }
        }

        bool hasArrow = directionArrowSprite != null;

        if (!anyHasBrush && !hasArrow && bandageIconSprite == null) {
            return;
        }

        if (anyHasBrush) {
            brushTransforms = new Transform[painters.Length];
            brushRenderers = new SpriteRenderer[painters.Length];
            brushAnimationSteps = new int[painters.Length];
            currentBrushFrameIndices = new int[painters.Length];
            brushShowingBlank = new bool[painters.Length];
            brushShowingFrozen = new bool[painters.Length];

            if (brushShadowSprite != null) {
                brushShadowTransforms = new Transform[painters.Length];
            }
        }

        if (hasArrow) {
            arrowTransforms = new Transform[painters.Length];
        }

        for (int i = 0; i < painters.Length; i++) {
            if (anyHasBrush && HasBrushFrames(i)) {
                if (brushShadowTransforms != null) {
                    GameObject shadowObject = new GameObject(runtimeSlots[i].label + " Brush Shadow");
                    shadowObject.transform.SetParent(RuntimeParent, false);

                    SpriteRenderer shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
                    shadowRenderer.sprite = brushShadowSprite;
                    shadowRenderer.sortingOrder = 11;

                    brushShadowTransforms[i] = shadowObject.transform;
                }

                GameObject brushObject = new GameObject(runtimeSlots[i].label + " Brush");
                brushObject.transform.SetParent(RuntimeParent, false);

                SpriteRenderer brushRenderer = brushObject.AddComponent<SpriteRenderer>();
                brushRenderer.sprite = playerBrushSets[i].frames[0];
                brushRenderer.sortingOrder = 12;

                brushTransforms[i] = brushObject.transform;
                brushRenderers[i] = brushRenderer;
            }

            if (hasArrow) {
                GameObject arrowObject = new GameObject(runtimeSlots[i].label + " Arrow");
                arrowObject.transform.SetParent(RuntimeParent, false);

                SpriteRenderer arrowRenderer = arrowObject.AddComponent<SpriteRenderer>();
                arrowRenderer.sprite = directionArrowSprite;
                arrowRenderer.sortingOrder = 13;

                arrowTransforms[i] = arrowObject.transform;
            }

            if (bandageIconSprite != null) {
                if (bandageRenderers == null) {
                    bandageRenderers = new SpriteRenderer[painters.Length];
                }

                GameObject bandageObject = new GameObject(runtimeSlots[i].label + " Bandage");
                bandageObject.transform.SetParent(RuntimeParent, false);

                SpriteRenderer bandageRenderer = bandageObject.AddComponent<SpriteRenderer>();
                bandageRenderer.sprite = bandageIconSprite;
                bandageRenderer.sortingOrder = 14;
                bandageRenderer.enabled = false;

                bandageRenderers[i] = bandageRenderer;
            }

            UpdatePlayerIcons(i);
        }
    }

    private bool HasBrushFrames(int playerIndex) {
        return playerBrushSets != null
            && playerIndex < playerBrushSets.Length
            && playerBrushSets[playerIndex] != null
            && playerBrushSets[playerIndex].frames != null
            && playerBrushSets[playerIndex].frames.Length > 0
            && playerBrushSets[playerIndex].frames[0] != null;
    }

    private void UpdateBrushAnimation(float deltaTime) {
        if (brushRenderers == null || brushAnimationSteps == null) {
            return;
        }

        float frameDuration = Mathf.Max(0.02f, brushFrameDuration);
        brushAnimationTimer += deltaTime;

        int stepsThisTick = 0;

        while (brushAnimationTimer >= frameDuration) {
            brushAnimationTimer -= frameDuration;
            stepsThisTick++;
        }

        for (int i = 0; i < brushRenderers.Length; i++) {
            if (brushRenderers[i] == null || !HasBrushFrames(i)) {
                continue;
            }

            // Frozen replaces the brush sprite on the same renderer, so brush
            // positioning (offsets, future bounce) applies to it automatically
            // and the animated brush can never show behind it.
            bool frozen = frozenTimers != null && frozenTimers[i] > 0.0f && frozenIconSprite != null;

            if (frozen) {
                if (!brushShowingFrozen[i]) {
                    brushShowingFrozen[i] = true;
                    brushRenderers[i].sprite = frozenIconSprite;
                }

                continue;
            }

            bool wasFrozen = brushShowingFrozen[i];
            brushShowingFrozen[i] = false;

            bool useBlank = noPaintTimers != null && noPaintTimers[i] > 0.0f && HasBlankBrushFrames();
            bool setChanged = wasFrozen || useBlank != brushShowingBlank[i];
            brushShowingBlank[i] = useBlank;

            Sprite[] frames = useBlank ? blankBrushFrames : playerBrushSets[i].frames;

            brushAnimationSteps[i] += stepsThisTick;

            int frameIndex = 0;

            if (frames.Length >= 2) {
                int cycleLength = (frames.Length - 1) * 2;
                int step = brushAnimationSteps[i] % cycleLength;
                frameIndex = step < frames.Length ? step : cycleLength - step;
            }

            if (!setChanged && (stepsThisTick == 0 || frameIndex == currentBrushFrameIndices[i])) {
                continue;
            }

            currentBrushFrameIndices[i] = frameIndex;
            Sprite frame = frames[frameIndex];

            if (frame != null) {
                brushRenderers[i].sprite = frame;
            }
        }
    }

    private bool HasBlankBrushFrames() {
        return blankBrushFrames != null
            && blankBrushFrames.Length > 0
            && blankBrushFrames[0] != null;
    }

    private void UpdatePlayerIcons(int index) {
        if (painters == null || index < 0 || index >= painters.Length) {
            return;
        }

        PainterMovement painter = painters[index];
        float bounceOffset = GetBounceOffsetPixels(index);

        if (brushTransforms != null && brushTransforms[index] != null) {
            Vector2 brushPosition = painter.Position + new Vector2(0.0f, brushHeightPixels + bounceOffset);
            brushTransforms[index].localPosition = TextureToWorld(brushPosition);
            brushTransforms[index].localScale = Vector3.one * brushFrameScale;
        }

        if (brushShadowTransforms != null && brushShadowTransforms[index] != null) {
            Vector2 shadowPosition = painter.Position + new Vector2(0.0f, brushShadowHeightPixels);
            brushShadowTransforms[index].localPosition = TextureToWorld(shadowPosition);
            brushShadowTransforms[index].localScale = Vector3.one * brushFrameScale;
        }

        if (bandageRenderers != null && bandageRenderers[index] != null) {
            bool showBandage = collisionSlowTimers != null
                && collisionSlowTimers[index] > 0.0f
                && bounceTimers[index] <= 0.0f
                && frozenTimers[index] <= 0.0f;
            bandageRenderers[index].enabled = showBandage;

            if (showBandage) {
                Vector2 bandagePosition = painter.Position + new Vector2(0.0f, brushHeightPixels + bounceOffset);
                bandageRenderers[index].transform.localPosition = TextureToWorld(bandagePosition);
                bandageRenderers[index].transform.localScale = Vector3.one * brushFrameScale;
            }
        }

        if (arrowTransforms != null && arrowTransforms[index] != null) {
            float headingDegrees = frozenTimers != null && frozenTimers[index] > 0.0f
                ? frozenVisualHeadings[index]
                : painter.HeadingDegrees;
            float headingRadians = headingDegrees * Mathf.Deg2Rad;
            Vector2 forward = new Vector2(Mathf.Cos(headingRadians), Mathf.Sin(headingRadians));
            Vector2 orbitCenter = painter.Position + new Vector2(0.0f, brushHeightPixels + bounceOffset);
            Vector2 arrowPosition = orbitCenter + forward * arrowOrbitRadiusPixels;

            arrowTransforms[index].localPosition = TextureToWorld(arrowPosition);
            arrowTransforms[index].localRotation = Quaternion.Euler(0.0f, 0.0f, headingDegrees);
            arrowTransforms[index].localScale = Vector3.one * arrowScale;
        }
    }

    private void CreateTimerLightTextures() {
        lightOnTexture = CreateCircleTexture(9, new Color32(255, 209, 92, 255));
        lightOffTexture = CreateCircleTexture(9, new Color32(62, 65, 72, 255));
        itemLightOnTexture = CreateCircleTexture(9, new Color32(142, 220, 255, 255));
        lightOnSprite = CreateRuntimeSprite(lightOnTexture, pixelsPerUnit);
        lightOffSprite = CreateRuntimeSprite(lightOffTexture, pixelsPerUnit);
        itemLightOnSprite = CreateRuntimeSprite(itemLightOnTexture, pixelsPerUnit);
    }

    private void CreateItemAssets() {
        itemIconRadius = Mathf.Max(4, itemIconRadius);

        Color32[] coreColorsByType = {
            new Color32(255, 92, 92, 255),   // BombRain
            new Color32(110, 200, 255, 255), // Freeze
            new Color32(120, 122, 130, 255), // NoPaint
            new Color32(255, 205, 64, 255),  // SpeedBoost
            new Color32(196, 110, 255, 255), // BigBrush
            new Color32(255, 138, 48, 255)   // BigExplosion
        };

        itemTextures = new Texture2D[coreColorsByType.Length];
        itemSprites = new Sprite[coreColorsByType.Length];

        for (int i = 0; i < coreColorsByType.Length; i++) {
            itemTextures[i] = CreateItemTexture(itemIconRadius, coreColorsByType[i]);
            itemSprites[i] = Sprite.Create(
                itemTextures[i],
                new Rect(0.0f, 0.0f, itemTextures[i].width, itemTextures[i].height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit
            );
        }
    }

    private static Texture2D CreateItemTexture(int radius, Color32 coreColor) {
        int size = radius * 2 + 1;
        float center = radius;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        NativeArray<Color32> pixels = texture.GetPixelData<Color32>(0);
        Color32 outlineColor = new Color32(45, 48, 54, 255);
        Color32 shellColor = new Color32(248, 248, 244, 255);

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                int index = y * size + x;

                Color32 color;

                if (distance > radius - 1.6f) {
                    color = outlineColor;
                } else if (distance <= radius * 0.45f) {
                    color = coreColor;
                } else {
                    color = shellColor;
                }

                // Anti-alias the outer silhouette by fading alpha at the rim.
                float coverage = Mathf.Clamp01(radius - distance);
                pixels[index] = new Color32(
                    color.r,
                    color.g,
                    color.b,
                    (byte)Mathf.RoundToInt(color.a * coverage)
                );
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    private void UpdateItemSpawning() {
        if (itemLightIndices == null || itemSpawnedFlags == null) {
            return;
        }

        int litCount = GetLitLightCount();

        for (int i = 0; i < itemLightIndices.Length; i++) {
            if (itemSpawnedFlags[i] || itemLightIndices[i] >= matchBlockCount) {
                continue;
            }

            if (litCount <= itemLightIndices[i]) {
                itemSpawnedFlags[i] = true;
                SpawnItem();
            }
        }
    }

    private void SpawnItem() {
        SpawnItem(GetNextQueuedItemType(), FindItemSpawnPosition());
    }

    private void SpawnItem(PainterItemType itemType, Vector2 position) {
        GameObject itemObject = new GameObject(itemType + " Pickup");
        itemObject.transform.SetParent(RuntimeParent, false);
        itemObject.layer = renderLayer;
        itemObject.transform.localPosition = TextureToWorld(position);

        SpriteRenderer renderer = itemObject.AddComponent<SpriteRenderer>();
        renderer.sprite = itemSprites[(int)itemType % itemSprites.Length];
        renderer.sortingOrder = 5;

        activeItems.Add(new ItemPickup(itemType, position, itemObject));
    }

    // Editor-only debug helper: number keys force-spawn items next to player 1
    // (1 = BombRain, 2 = Freeze, 3 = NoPaint, 4 = SpeedBoost, 5 = BigBrush, 6 = BigExplosion).
    private void HandleDebugItemSpawning() {
        if (!Application.isEditor) {
            return;
        }

        int itemTypeCount = System.Enum.GetValues(typeof(PainterItemType)).Length;

        for (int i = 0; i < itemTypeCount; i++) {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) {
                SpawnDebugItem((PainterItemType)i);
            }
        }
    }

    private void SpawnDebugItem(PainterItemType itemType) {
        Vector2 basePosition = painters[0].Position;
        Rect bounds = GetPainterCenterBounds(GetArenaBounds(), GetPainterRadius());
        Vector2 position = new Vector2(
            Mathf.Clamp(basePosition.x + GetPainterRadius() * 3.0f, bounds.xMin, bounds.xMax),
            Mathf.Clamp(basePosition.y, bounds.yMin, bounds.yMax)
        );

        SpawnItem(itemType, position);
    }

    private PainterItemType GetNextQueuedItemType() {
        if (queuedItemTypes == null || queuedItemTypes.Length == 0) {
            return PainterItemType.BombRain;
        }

        PainterItemType itemType = queuedItemTypes[nextQueuedItemIndex % queuedItemTypes.Length];
        nextQueuedItemIndex++;
        return itemType;
    }

    private Vector2 FindItemSpawnPosition() {
        Rect spawnBounds = GetPainterCenterBounds(GetArenaBounds(), GetPainterRadius() * 2.0f);

        for (int attempt = 0; attempt < 20; attempt++) {
            float x = Mathf.Lerp(spawnBounds.xMin, spawnBounds.xMax, (float)itemRandom.NextDouble());
            float y = Mathf.Lerp(spawnBounds.yMin, spawnBounds.yMax, (float)itemRandom.NextDouble());

            if (paintMap.IsPaintable(Mathf.RoundToInt(x), Mathf.RoundToInt(y))) {
                return new Vector2(x, y);
            }
        }

        return spawnBounds.center;
    }

    private void UpdateItemPickups() {
        if (activeItems.Count == 0) {
            return;
        }

        float pickupRadius = GetPainterRadius() + itemIconRadius;
        float pickupRadiusSquared = pickupRadius * pickupRadius;

        for (int itemIndex = activeItems.Count - 1; itemIndex >= 0; itemIndex--) {
            ItemPickup item = activeItems[itemIndex];

            for (int i = 0; i < painters.Length; i++) {
                // Frozen painters can't collect an item that sits under them.
                if (frozenTimers[i] > 0.0f) {
                    continue;
                }

                if ((painters[i].Position - item.Position).sqrMagnitude > pickupRadiusSquared) {
                    continue;
                }

                ApplyItemEffect(item.ItemType, i);
                Destroy(item.GameObject);
                activeItems.RemoveAt(itemIndex);
                break;
            }
        }
    }

    // Feeds the frozen player's input into a visual-only heading so the arrow
    // keeps wiggling as if steering, without affecting the painter itself.
    private void TickFrozenSteering(int index, float deltaTime, Rect arenaBounds, float painterRadius) {
        if (inputSources[index] == null) {
            return;
        }

        PainterMovement painter = painters[index];
        Vector2 itemTargetPosition;
        bool hasItemTarget = TryGetNearestItemPosition(painter.Position, out itemTargetPosition);
        PainterInputContext inputContext = new PainterInputContext(
            painter.Position,
            frozenVisualHeadings[index],
            arenaBounds,
            painterRadius,
            hasItemTarget,
            itemTargetPosition
        );

        float turnInput = Mathf.Clamp(inputSources[index].GetTurnInput(deltaTime, inputContext), -1.0f, 1.0f);
        frozenVisualHeadings[index] = Mathf.Repeat(
            frozenVisualHeadings[index] + turnInput * runtimeSlots[index].turnSpeedDegreesPerSecond * deltaTime,
            360.0f
        );
    }

    private void DetectPainterCollisions() {
        float collisionDistance = GetPainterRadius() * 2.0f;
        float collisionDistanceSquared = collisionDistance * collisionDistance;

        for (int i = 0; i < painters.Length; i++) {
            for (int j = i + 1; j < painters.Length; j++) {
                bool overlapping = (painters[i].Position - painters[j].Position).sqrMagnitude <= collisionDistanceSquared;
                int pairIndex = i * painters.Length + j;

                // Only trigger on the frame the overlap starts, so a pair
                // passing through each other bounces once, not every frame.
                // Frozen painters are inert ice blocks: they neither hop nor
                // get slowed when someone runs into them.
                if (overlapping && !painterPairOverlap[pairIndex]) {
                    if (frozenTimers[i] <= 0.0f) {
                        StartCollisionBounce(i);
                    }

                    if (frozenTimers[j] <= 0.0f) {
                        StartCollisionBounce(j);
                    }
                }

                painterPairOverlap[pairIndex] = overlapping;
            }
        }
    }

    private void StartCollisionBounce(int index) {
        bounceTimers[index] = Mathf.Max(0.05f, collisionBounceDuration);
        collisionSlowTimers[index] = Mathf.Max(0.0f, collisionSlowDuration);
        // Crashing ends a speed boost rather than blending with the slow.
        speedBoostTimers[index] = 0.0f;
    }

    // Parabolic hop applied to the player's visuals only; the logical
    // position, painting and the brush shadow are unaffected.
    private float GetBounceOffsetPixels(int index) {
        if (bounceTimers == null || bounceTimers[index] <= 0.0f || collisionBounceDuration <= 0.0f) {
            return 0.0f;
        }

        float t = 1.0f - bounceTimers[index] / Mathf.Max(0.05f, collisionBounceDuration);
        return collisionBounceHeightPixels * 4.0f * t * (1.0f - t);
    }

    private void ApplyItemEffect(PainterItemType itemType, int collectorIndex) {
        if (itemType == PainterItemType.BombRain) {
            paintEffects.Activate(PainterItemType.BombRain, painters[collectorIndex].OwnerId);
            return;
        }

        if (itemType == PainterItemType.SpeedBoost) {
            speedBoostTimers[collectorIndex] = speedBoostDuration;
            // The boost cures a collision slow (and its bandage).
            collisionSlowTimers[collectorIndex] = 0.0f;
            return;
        }

        if (itemType == PainterItemType.BigBrush) {
            bigBrushTimers[collectorIndex] = bigBrushDuration;
            return;
        }

        if (itemType == PainterItemType.BigExplosion) {
            ActivateBigExplosion(collectorIndex);
            return;
        }

        for (int i = 0; i < painters.Length; i++) {
            if (i == collectorIndex) {
                continue;
            }

            if (itemType == PainterItemType.Freeze) {
                frozenTimers[i] = freezeDuration;
                frozenVisualHeadings[i] = painters[i].HeadingDegrees;
                // Freeze supersedes movement effects: no lingering slow,
                // mid-air hop or boost carrying over past the thaw.
                collisionSlowTimers[i] = 0.0f;
                bounceTimers[i] = 0.0f;
                speedBoostTimers[i] = 0.0f;
            } else if (itemType == PainterItemType.NoPaint) {
                noPaintTimers[i] = noPaintDuration;
            }
        }
    }

    private void ActivateBigExplosion(int collectorIndex) {
        PainterMovement painter = painters[collectorIndex];

        NativeArray<Color32> pixels = paintTexture.GetPixelData<Color32>(0);
        paintMap.PaintSegment(
            painter.Position,
            painter.Position,
            painter.OwnerId,
            pixels,
            Mathf.Max(1.0f, bigExplosionRadiusMultiplier)
        );

        if (explosionParticles != null) {
            explosionParticles.transform.localPosition = TextureToWorld(painter.Position);

            ParticleSystem.MainModule main = explosionParticles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(
                (Color)runtimeSlots[collectorIndex].paintColor,
                Color.white
            );

            explosionParticles.Play();
        }
    }

    private bool TryGetNearestItemPosition(Vector2 fromPosition, out Vector2 itemPosition) {
        itemPosition = Vector2.zero;

        if (activeItems.Count == 0) {
            return false;
        }

        float bestDistanceSquared = float.MaxValue;

        for (int i = 0; i < activeItems.Count; i++) {
            float distanceSquared = (activeItems[i].Position - fromPosition).sqrMagnitude;

            if (distanceSquared < bestDistanceSquared) {
                bestDistanceSquared = distanceSquared;
                itemPosition = activeItems[i].Position;
            }
        }

        return true;
    }

    private void ClearItems() {
        for (int i = 0; i < activeItems.Count; i++) {
            if (activeItems[i].GameObject != null) {
                Destroy(activeItems[i].GameObject);
            }
        }

        activeItems.Clear();
    }

    private static Texture2D CreateCircleTexture(int radius, Color32 fillColor) {
        int size = radius * 2 + 1;
        float center = radius;
        float outerRadius = radius;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        NativeArray<Color32> pixels = texture.GetPixelData<Color32>(0);

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                int index = y * size + x;

                // One-pixel anti-aliased rim: full alpha inside, fading to
                // transparent at the radius.
                float coverage = Mathf.Clamp01(outerRadius - distance);
                pixels[index] = new Color32(
                    fillColor.r,
                    fillColor.g,
                    fillColor.b,
                    (byte)Mathf.RoundToInt(fillColor.a * coverage)
                );
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    private static Texture2D CreateSolidTexture(Color32 fillColor) {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, fillColor);
        texture.Apply(false, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Point;
        return texture;
    }

    private static Sprite CreateRuntimeSprite(Texture2D texture, float spritePixelsPerUnit) {
        return Sprite.Create(
            texture,
            new Rect(0.0f, 0.0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            Mathf.Max(1.0f, spritePixelsPerUnit)
        );
    }

    private void ConfigureCamera() {
        Camera mainCamera = ResolveOutputCamera();

        if (mainCamera == null) {
            return;
        }

        float worldWidth = textureWidth / pixelsPerUnit;
        float worldHeight = textureHeight / pixelsPerUnit;
        float aspect = Mathf.Max(0.01f, mainCamera.aspect);
        arcadeOutputMode = mainCamera.targetTexture != null;

        if (arcadeOutputMode && transform.parent != null) {
            transform.localPosition = arcadeCabinetOffset;
        }

        mainCamera.orthographic = true;
        if (mainCamera.transform.IsChildOf(transform)) {
            mainCamera.transform.localPosition = new Vector3(0.0f, 0.0f, -10.0f);
            mainCamera.transform.localRotation = Quaternion.identity;
        } else {
            mainCamera.transform.position = new Vector3(0.0f, 0.0f, -10.0f);
            mainCamera.transform.rotation = Quaternion.identity;
        }
        mainCamera.orthographicSize = Mathf.Max(worldHeight * 0.56f, (worldWidth * 0.56f) / aspect);
        mainCamera.backgroundColor = Color.black;
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        cameraWorldHalfHeight = mainCamera.orthographicSize;
        cameraWorldHalfWidth = mainCamera.orthographicSize * aspect;

        renderLayer = gameObject.layer;
        if (arcadeOutputMode) {
            int uiLayer = LayerMask.NameToLayer("UI");
            renderLayer = uiLayer >= 0 ? uiLayer : gameObject.layer;
            SetLayerRecursively(gameObject, renderLayer);
            mainCamera.cullingMask = 1 << renderLayer;
        }
    }

    private Camera ResolveOutputCamera() {
        if (outputCamera != null) {
            return outputCamera;
        }

        outputCamera = GetComponent<Camera>();
        if (outputCamera == null) {
            outputCamera = GetComponentInChildren<Camera>();
        }
        if (outputCamera == null) {
            outputCamera = GetComponentInParent<Camera>();
        }
        if (outputCamera == null) {
            outputCamera = Camera.main;
        }
        return outputCamera;
    }

    private void ApplyRenderLayer(GameObject root) {
        SetLayerRecursively(root, renderLayer);
    }

    private static void SetLayerRecursively(GameObject root, int layer) {
        root.layer = layer;
        Transform rootTransform = root.transform;
        for (int i = 0; i < rootTransform.childCount; i++) {
            SetLayerRecursively(rootTransform.GetChild(i).gameObject, layer);
        }
    }

    private void PaintStartingPositions() {
        NativeArray<Color32> pixels = paintTexture.GetPixelData<Color32>(0);

        for (int i = 0; i < painters.Length; i++) {
            PainterMovement painter = painters[i];
            paintMap.PaintSegment(painter.Position, painter.Position, painter.OwnerId, pixels);
        }

        paintTexture.Apply(false, false);
    }

    private Rect GetArenaBounds() {
        return Rect.MinMaxRect(
            arenaBorderPixels,
            arenaBorderPixels,
            textureWidth - 1.0f - arenaBorderPixels,
            textureHeight - 1.0f - arenaBorderPixels
        );
    }

    private float GetPainterRadius() {
        return brushRadius;
    }

    private Rect GetPainterCenterBounds(Rect arenaBounds, float painterRadius) {
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

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private Vector3 TextureToWorld(Vector2 texturePosition) {
        return new Vector3(
            (texturePosition.x - textureWidth * 0.5f) / pixelsPerUnit,
            (texturePosition.y - textureHeight * 0.5f) / pixelsPerUnit,
            -0.1f
        );
    }

    private void UpdatePlayerMarker(int index) {
        if (markerTransforms == null || index < 0 || index >= markerTransforms.Length || markerTransforms[index] == null) {
            return;
        }

        Vector2 markerPosition = painters[index].Position + new Vector2(0.0f, GetBounceOffsetPixels(index));
        markerTransforms[index].localPosition = TextureToWorld(markerPosition);
    }

    private void CreateStatusVisuals() {
        if (painters == null) {
            return;
        }

        boostTrailTexture = CreateCircleTexture(8, new Color32(255, 255, 255, 255));
        boostTrailMaterial = new Material(Shader.Find("Sprites/Default"));
        boostTrailMaterial.mainTexture = boostTrailTexture;

        boostTrails = new ParticleSystem[painters.Length];

        for (int i = 0; i < painters.Length; i++) {
            GameObject trailObject = new GameObject(runtimeSlots[i].label + " Boost Trail");
            trailObject.transform.SetParent(RuntimeParent, false);
            trailObject.transform.localPosition = TextureToWorld(painters[i].Position);

            ParticleSystem trail = trailObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = trail.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.6f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.0f, brushRadius * 0.8f / pixelsPerUnit);
            main.startSize = new ParticleSystem.MinMaxCurve(
                brushRadius * 0.7f / pixelsPerUnit,
                brushRadius * 1.4f / pixelsPerUnit
            );
            // Whitened sparkles so the trail pops against the player's own paint.
            Color sparkleColor = Color.Lerp(runtimeSlots[i].paintColor, Color.white, 0.65f);
            main.startColor = new ParticleSystem.MinMaxGradient(sparkleColor, Color.white);
            main.maxParticles = 192;

            ParticleSystem.EmissionModule emission = trail.emission;
            emission.rateOverTime = 0.0f;
            emission.rateOverDistance = 18.0f;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = trail.shape;
            shape.enabled = false;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = trail.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0.0f, 1.0f, 1.0f, 0.0f));

            ParticleSystemRenderer trailRenderer = trailObject.GetComponent<ParticleSystemRenderer>();
            trailRenderer.material = boostTrailMaterial;
            trailRenderer.sortingOrder = 9;

            boostTrails[i] = trail;
        }

        bigBrushTrails = new ParticleSystem[painters.Length];

        for (int i = 0; i < painters.Length; i++) {
            GameObject splatterObject = new GameObject(runtimeSlots[i].label + " Big Brush Splatter");
            splatterObject.transform.SetParent(RuntimeParent, false);
            splatterObject.transform.localPosition = TextureToWorld(painters[i].Position);

            ParticleSystem splatter = splatterObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = splatter.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.7f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                brushRadius * 1.2f / pixelsPerUnit,
                brushRadius * 3.0f / pixelsPerUnit
            );
            main.startSize = new ParticleSystem.MinMaxCurve(
                brushRadius * 0.6f / pixelsPerUnit,
                brushRadius * 1.1f / pixelsPerUnit
            );
            // Droplets vary between darkened and whitened paint so they stay
            // visible on top of the player's own freshly painted stroke.
            Color darkDroplet = Color.Lerp(runtimeSlots[i].paintColor, Color.black, 0.35f);
            Color lightDroplet = Color.Lerp(runtimeSlots[i].paintColor, Color.white, 0.55f);
            main.startColor = new ParticleSystem.MinMaxGradient(darkDroplet, lightDroplet);
            main.maxParticles = 192;

            ParticleSystem.EmissionModule emission = splatter.emission;
            emission.rateOverTime = 0.0f;
            emission.rateOverDistance = 20.0f;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = splatter.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = brushRadius * 0.8f / pixelsPerUnit;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = splatter.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0.0f, 1.0f, 1.0f, 0.0f));

            ParticleSystemRenderer splatterRenderer = splatterObject.GetComponent<ParticleSystemRenderer>();
            splatterRenderer.material = boostTrailMaterial;
            splatterRenderer.sortingOrder = 8;

            bigBrushTrails[i] = splatter;
        }

        CreateExplosionParticles();
    }

    private void CreateExplosionParticles() {
        GameObject explosionObject = new GameObject("Big Explosion Particles");
        explosionObject.transform.SetParent(RuntimeParent, false);

        explosionParticles = explosionObject.AddComponent<ParticleSystem>();
        explosionParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = explosionParticles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = Mathf.Max(0.1f, bigExplosionParticleDuration);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 0.4f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.0f, brushRadius * 0.6f / pixelsPerUnit);
        main.startSize = new ParticleSystem.MinMaxCurve(
            brushRadius * 0.6f / pixelsPerUnit,
            brushRadius * 1.4f / pixelsPerUnit
        );
        main.maxParticles = 256;

        ParticleSystem.EmissionModule emission = explosionParticles.emission;
        emission.rateOverTime = 24.0f;
        emission.rateOverDistance = 0.0f;

        ParticleSystem.ShapeModule shape = explosionParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = brushRadius * Mathf.Max(1.0f, bigExplosionRadiusMultiplier) / pixelsPerUnit;

        // Quick grow-then-fade pop for each small explosion puff.
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = explosionParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve popCurve = new AnimationCurve(
            new Keyframe(0.0f, 0.2f),
            new Keyframe(0.3f, 1.0f),
            new Keyframe(1.0f, 0.0f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, popCurve);

        ParticleSystemRenderer explosionRenderer = explosionObject.GetComponent<ParticleSystemRenderer>();
        explosionRenderer.material = boostTrailMaterial;
        explosionRenderer.sortingOrder = 6;
    }

    private void UpdateStatusVisuals() {
        if (painters == null) {
            return;
        }

        for (int i = 0; i < painters.Length; i++) {
            if (boostTrails != null && boostTrails[i] != null) {
                boostTrails[i].transform.localPosition = TextureToWorld(painters[i].Position);

                ParticleSystem.EmissionModule emission = boostTrails[i].emission;
                emission.enabled = speedBoostTimers[i] > 0.0f && frozenTimers[i] <= 0.0f;
            }

            if (bigBrushTrails != null && bigBrushTrails[i] != null) {
                bigBrushTrails[i].transform.localPosition = TextureToWorld(painters[i].Position);

                ParticleSystem.EmissionModule emission = bigBrushTrails[i].emission;
                emission.enabled = bigBrushTimers[i] > 0.0f
                    && frozenTimers[i] <= 0.0f
                    && noPaintTimers[i] <= 0.0f;
            }
        }
    }

    private void ClearStatusVisuals() {
        if (boostTrails != null) {
            for (int i = 0; i < boostTrails.Length; i++) {
                if (boostTrails[i] != null) {
                    ParticleSystem.EmissionModule emission = boostTrails[i].emission;
                    emission.enabled = false;
                }
            }
        }

        if (bigBrushTrails != null) {
            for (int i = 0; i < bigBrushTrails.Length; i++) {
                if (bigBrushTrails[i] != null) {
                    ParticleSystem.EmissionModule emission = bigBrushTrails[i].emission;
                    emission.enabled = false;
                }
            }
        }

        if (explosionParticles != null) {
            explosionParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void UpdateMarkerVisibility() {
        if (markerRenderers == null || noPaintTimers == null) {
            return;
        }

        for (int i = 0; i < markerRenderers.Length; i++) {
            if (markerRenderers[i] != null) {
                markerRenderers[i].enabled = noPaintTimers[i] <= 0.0f;
            }
        }
    }

    private void UpdateCoverageText() {
        if (paintMap == null) {
            coverageText = string.Empty;
            return;
        }

        hudBuilder.Length = 0;
        hudBuilder.AppendLine("Paint Battle");
        hudBuilder.AppendLine(matchState == MatchState.Playing ? "Cover the canvas" : "One player vs CPU");

        for (int i = 0; i < runtimeSlots.Length; i++) {
            PlayerSlot slot = runtimeSlots[i];
            float percentage = paintMap.GetCoverage01(slot.ownerId) * 100.0f;

            hudBuilder.Append(slot.label);
            hudBuilder.Append(" ");
            hudBuilder.Append(FormatInputType(slot.inputType));
            hudBuilder.Append(": ");
            hudBuilder.Append(percentage.ToString("0.0"));
            hudBuilder.Append('%');

            if (i < runtimeSlots.Length - 1) {
                hudBuilder.AppendLine();
            }
        }

        coverageText = hudBuilder.ToString();
    }

    private string FormatInputType(PainterInputType inputType) {
        return inputType == PainterInputType.Human ? "Human" : "CPU";
    }

    private void DestroyRuntimeAssets() {
        if (displaySprite != null) {
            Destroy(displaySprite);
        }

        if (paintTexture != null) {
            Destroy(paintTexture);
        }

        if (markerSprites != null) {
            for (int i = 0; i < markerSprites.Length; i++) {
                if (markerSprites[i] != null) {
                    Destroy(markerSprites[i]);
                }
            }
        }

        if (markerTextures != null) {
            for (int i = 0; i < markerTextures.Length; i++) {
                if (markerTextures[i] != null) {
                    Destroy(markerTextures[i]);
                }
            }
        }

        if (lightOnSprite != null) {
            Destroy(lightOnSprite);
        }

        if (lightOffSprite != null) {
            Destroy(lightOffSprite);
        }

        if (itemLightOnSprite != null) {
            Destroy(itemLightOnSprite);
        }

        if (hudPlateSprite != null) {
            Destroy(hudPlateSprite);
        }

        if (lightOnTexture != null) {
            Destroy(lightOnTexture);
        }

        if (lightOffTexture != null) {
            Destroy(lightOffTexture);
        }

        if (itemLightOnTexture != null) {
            Destroy(itemLightOnTexture);
        }

        if (hudPlateTexture != null) {
            Destroy(hudPlateTexture);
        }

        if (boostTrailMaterial != null) {
            Destroy(boostTrailMaterial);
        }

        if (boostTrailTexture != null) {
            Destroy(boostTrailTexture);
        }

        if (itemSprites != null) {
            for (int i = 0; i < itemSprites.Length; i++) {
                if (itemSprites[i] != null) {
                    Destroy(itemSprites[i]);
                }
            }
        }

        if (itemTextures != null) {
            for (int i = 0; i < itemTextures.Length; i++) {
                if (itemTextures[i] != null) {
                    Destroy(itemTextures[i]);
                }
            }
        }
    }

    private static PlayerSlot[] CreateDefaultSlots() {
        return new PlayerSlot[] {
            new PlayerSlot(
                "P1",
                1,
                PainterInputType.Human,
                new Vector2(0.45f, 0.55f),
                135.0f,
                new Color(0.988f, 0.38f, 0.675f, 1.0f),
                100.0f,
                135.0f,
                1,
                0.42f,
                0.78f,
                KeyCode.LeftArrow,
                KeyCode.RightArrow
            ),
            new PlayerSlot(
                "P2",
                2,
                PainterInputType.Cpu,
                new Vector2(0.55f, 0.55f),
                45.0f,
                new Color(0.161f, 0.627f, 0.996f, 1.0f),
                100.0f,
                135.0f,
                -1,
                0.47f,
                0.78f,
                KeyCode.LeftArrow,
                KeyCode.RightArrow
            ),
            new PlayerSlot(
                "P3",
                3,
                PainterInputType.Cpu,
                new Vector2(0.55f, 0.45f),
                315.0f,
                new Color(0.341f, 0.886f, 0.333f, 1.0f),
                100.0f,
                135.0f,
                1,
                0.52f,
                0.78f,
                KeyCode.LeftArrow,
                KeyCode.RightArrow
            ),
            new PlayerSlot(
                "P4",
                4,
                PainterInputType.Cpu,
                new Vector2(0.45f, 0.45f),
                225.0f,
                new Color(1.0f, 0.773f, 0.353f, 1.0f),
                100.0f,
                135.0f,
                -1,
                0.45f,
                0.78f,
                KeyCode.LeftArrow,
                KeyCode.RightArrow
            )
        };
    }

    [System.Serializable]
    private sealed class PlayerBrushSet {
        public Sprite[] frames;
    }

    private sealed class ItemPickup {
        public PainterItemType ItemType { get; }
        public Vector2 Position { get; }
        public GameObject GameObject { get; }

        public ItemPickup(PainterItemType itemType, Vector2 position, GameObject gameObject) {
            ItemType = itemType;
            Position = position;
            GameObject = gameObject;
        }
    }

    private enum MatchState {
        Ready,
        Playing,
        Finished
    }

    private enum PainterInputType {
        Human,
        Cpu
    }

    [System.Serializable]
    private sealed class PlayerSlot {
        public string label;
        public int ownerId;
        public PainterInputType inputType;
        public Vector2 normalizedStart;
        public float headingDegrees;
        public Color paintColor;
        public float moveSpeedPixelsPerSecond;
        public float turnSpeedDegreesPerSecond;
        public int startingSteerDirection;
        public float normalBeatDuration;
        public float normalTurnStrength;
        public KeyCode leftKey;
        public KeyCode rightKey;

        public PlayerSlot() {
            label = "P1";
            ownerId = 1;
            inputType = PainterInputType.Human;
            normalizedStart = new Vector2(0.5f, 0.5f);
            headingDegrees = 45.0f;
            paintColor = new Color(0.988f, 0.38f, 0.675f, 1.0f);
            moveSpeedPixelsPerSecond = 100.0f;
            turnSpeedDegreesPerSecond = 135.0f;
            startingSteerDirection = 1;
            normalBeatDuration = 0.45f;
            normalTurnStrength = 0.78f;
            leftKey = KeyCode.LeftArrow;
            rightKey = KeyCode.RightArrow;
        }

        public PlayerSlot(
            string label,
            int ownerId,
            PainterInputType inputType,
            Vector2 normalizedStart,
            float headingDegrees,
            Color paintColor,
            float moveSpeedPixelsPerSecond,
            float turnSpeedDegreesPerSecond,
            int startingSteerDirection,
            float normalBeatDuration,
            float normalTurnStrength,
            KeyCode leftKey,
            KeyCode rightKey
        ) {
            this.label = label;
            this.ownerId = ownerId;
            this.inputType = inputType;
            this.normalizedStart = normalizedStart;
            this.headingDegrees = headingDegrees;
            this.paintColor = paintColor;
            this.moveSpeedPixelsPerSecond = moveSpeedPixelsPerSecond;
            this.turnSpeedDegreesPerSecond = turnSpeedDegreesPerSecond;
            this.startingSteerDirection = startingSteerDirection;
            this.normalBeatDuration = normalBeatDuration;
            this.normalTurnStrength = normalTurnStrength;
            this.leftKey = leftKey;
            this.rightKey = rightKey;
        }
    }
}
