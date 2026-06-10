using System.Collections.Generic;
using UnityEngine;

public sealed class FlappyBirdGame : MonoBehaviour {
    [SerializeField] private float gravity = -22.0f;
    [SerializeField] private float flapVelocity = 7.6f;
    [SerializeField] private float maxFallSpeed = -12.0f;
    [SerializeField] private float pipeSpeed = 3.1f;
    [SerializeField] private float pipeSpawnInterval = 1.55f;
    [SerializeField] private float pipeGapHeight = 3.2f;
    [SerializeField] private float pipeGapCenterMin = -1.3f;
    [SerializeField] private float pipeGapCenterMax = 2.3f;
    [SerializeField] private float pipeWidth = 1.3f;
    [SerializeField] private float birdX = -3.2f;
    [SerializeField] private float birdRadius = 0.32f;
    [SerializeField] private float groundTopY = -4.0f;
    [SerializeField] private float ceilingY = 5.0f;
    [SerializeField] private int randomSeed = 0;
    [SerializeField] private bool showHud = true;
    [SerializeField] private Camera outputCamera;
    [SerializeField] private bool showStandaloneGui = true;
    [SerializeField] private Color skyColor = new Color(0.443f, 0.78f, 0.875f, 1.0f);
    [SerializeField] private Color birdColor = new Color(1.0f, 0.84f, 0.25f, 1.0f);
    [SerializeField] private Color pipeColor = new Color(0.345f, 0.745f, 0.302f, 1.0f);
    [SerializeField] private Color pipeLipColor = new Color(0.255f, 0.58f, 0.224f, 1.0f);
    [SerializeField] private Color groundColor = new Color(0.87f, 0.78f, 0.5f, 1.0f);
    [SerializeField] private Color grassColor = new Color(0.45f, 0.78f, 0.35f, 1.0f);

    private enum GameState {
        Ready,
        Playing,
        GameOver
    }

    private sealed class PipePair {
        public Transform root;
        public bool scored;
        public float gapCenterY;
    }

    private const string BestScoreKey = "FlappyBird.BestScore";
    private const float ArcadeHudTitleSize = 0.10725f;
    private const float ArcadeHudBodySize = 0.0552f;
    private const float ArcadeHudScoreSize = 0.1035f;
    private const float ArcadeHudShadowOffset = 0.015f;

    private sealed class HudText {
        public TextMesh foreground;
        public TextMesh shadow;
    }

    private GameState state = GameState.Ready;
    private System.Random rng;
    private Sprite solidSprite;
    private Sprite birdSprite;
    private Sprite wingSprite;
    private Sprite cloudSprite;
    private Transform birdTransform;
    private Transform wingTransform;
    private float birdY;
    private float birdVelocityY;
    private float birdTilt;
    private float wingPhase;
    private float spawnTimer;
    private float worldHalfWidth;
    private readonly List<PipePair> activePipes = new List<PipePair>(8);
    private readonly Stack<PipePair> pipePool = new Stack<PipePair>(8);
    private readonly List<Transform> groundStripes = new List<Transform>(16);
    private readonly List<Transform> clouds = new List<Transform>(6);
    private readonly List<float> cloudSpeeds = new List<float>(6);
    private int score;
    private int bestScore;
    private GUIStyle scoreStyle;
    private GUIStyle messageStyle;
    private GUIStyle smallStyle;
    private readonly GUIContent guiContent = new GUIContent();
    private bool arcadeOutputMode;
    private int renderLayer;
    private HudText arcadeScoreText;
    private HudText arcadeMessageText;
    private HudText arcadeSmallText;
    private HudText arcadePromptText;

    private void Start() {
        rng = randomSeed == 0 ? new System.Random() : new System.Random(randomSeed);
        bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);

        ConfigureCamera();
        CreateSprites();
        CreateBird();
        CreateGround();
        CreateClouds();
        CreateArcadeHud();
        ResetRun();
        UpdateHud();

        Debug.Log("Floppy Bird ready. Press Space or click to flap.");
    }

    private void Update() {
        float deltaTime = Time.deltaTime;
        bool flapPressed = Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.W)
            || Input.GetKeyDown(KeyCode.UpArrow)
            || Input.GetMouseButtonDown(0);

        switch (state) {
            case GameState.Ready:
                UpdateClouds(deltaTime);
                AnimateIdleBird();
                if (flapPressed) {
                    state = GameState.Playing;
                    Flap();
                }
                break;
            case GameState.Playing:
                if (flapPressed) {
                    Flap();
                }
                UpdateBird(deltaTime);
                UpdatePipes(deltaTime);
                UpdateGround(deltaTime);
                UpdateClouds(deltaTime);
                CheckCollisions();
                break;
            case GameState.GameOver:
                UpdateDeadBird(deltaTime);
                if (flapPressed || Input.GetKeyDown(KeyCode.R)) {
                    ResetRun();
                }
                break;
        }

        UpdateHud();
    }

    private void Flap() {
        birdVelocityY = flapVelocity;
        wingPhase = 1.0f;
    }

    private void UpdateBird(float deltaTime) {
        birdVelocityY = Mathf.Max(birdVelocityY + gravity * deltaTime, maxFallSpeed);
        birdY += birdVelocityY * deltaTime;

        if (birdY > ceilingY - birdRadius) {
            birdY = ceilingY - birdRadius;
            birdVelocityY = 0.0f;
        }

        float targetTilt = Mathf.Clamp(birdVelocityY * 6.0f, -75.0f, 28.0f);
        birdTilt = Mathf.MoveTowards(birdTilt, targetTilt, 360.0f * deltaTime);
        ApplyBirdTransform();
        AnimateWing(deltaTime);
    }

    private void UpdateDeadBird(float deltaTime) {
        if (birdY <= groundTopY + birdRadius) {
            return;
        }

        birdVelocityY = Mathf.Max(birdVelocityY + gravity * deltaTime, maxFallSpeed);
        birdY = Mathf.Max(birdY + birdVelocityY * deltaTime, groundTopY + birdRadius);
        birdTilt = Mathf.MoveTowards(birdTilt, -90.0f, 420.0f * deltaTime);
        ApplyBirdTransform();
    }

    private void AnimateIdleBird() {
        birdY = Mathf.Sin(Time.time * 3.0f) * 0.25f;
        birdTilt = 0.0f;
        ApplyBirdTransform();
        AnimateWing(Time.deltaTime);
        wingPhase = Mathf.Repeat(Time.time, 0.8f) < 0.1f ? 1.0f : wingPhase;
    }

    private void ApplyBirdTransform() {
        birdTransform.localPosition = new Vector3(birdX, birdY, 0.0f);
        birdTransform.localRotation = Quaternion.Euler(0.0f, 0.0f, birdTilt);
    }

    private void AnimateWing(float deltaTime) {
        wingPhase = Mathf.Max(0.0f, wingPhase - deltaTime * 4.0f);
        float wingAngle = Mathf.Lerp(-20.0f, 45.0f, wingPhase);
        wingTransform.localRotation = Quaternion.Euler(0.0f, 0.0f, wingAngle);
    }

    private void UpdatePipes(float deltaTime) {
        spawnTimer -= deltaTime;
        if (spawnTimer <= 0.0f) {
            spawnTimer += pipeSpawnInterval;
            SpawnPipePair();
        }

        float step = pipeSpeed * deltaTime;
        for (int i = activePipes.Count - 1; i >= 0; i--) {
            PipePair pipe = activePipes[i];
            Vector3 position = pipe.root.localPosition;
            position.x -= step;
            pipe.root.localPosition = position;

            if (!pipe.scored && position.x + pipeWidth * 0.5f < birdX - birdRadius) {
                pipe.scored = true;
                score++;
            }

            if (position.x < -worldHalfWidth - pipeWidth) {
                pipe.root.gameObject.SetActive(false);
                activePipes.RemoveAt(i);
                pipePool.Push(pipe);
            }
        }
    }

    private void UpdateGround(float deltaTime) {
        float step = pipeSpeed * deltaTime;
        float wrapWidth = groundStripes.Count * 2.0f;
        for (int i = 0; i < groundStripes.Count; i++) {
            Vector3 position = groundStripes[i].localPosition;
            position.x -= step;
            if (position.x < -worldHalfWidth - 1.0f) {
                position.x += wrapWidth;
            }
            groundStripes[i].localPosition = position;
        }
    }

    private void UpdateClouds(float deltaTime) {
        for (int i = 0; i < clouds.Count; i++) {
            Vector3 position = clouds[i].localPosition;
            position.x -= cloudSpeeds[i] * deltaTime;
            if (position.x < -worldHalfWidth - 2.5f) {
                position.x = worldHalfWidth + 2.5f;
                position.y = Mathf.Lerp(1.0f, 4.2f, (float)rng.NextDouble());
            }
            clouds[i].localPosition = position;
        }
    }

    private void CheckCollisions() {
        if (birdY - birdRadius <= groundTopY) {
            birdY = groundTopY + birdRadius;
            EndRun();
            return;
        }

        for (int i = 0; i < activePipes.Count; i++) {
            PipePair pipe = activePipes[i];
            float pipeX = pipe.root.localPosition.x;
            if (Mathf.Abs(pipeX - birdX) > pipeWidth * 0.5f + birdRadius + 0.1f) {
                continue;
            }

            float gapHalf = pipeGapHeight * 0.5f;
            float bottomTop = pipe.gapCenterY - gapHalf;
            float topBottom = pipe.gapCenterY + gapHalf;
            if (CircleOverlapsRect(pipeX, -20.0f, bottomTop) || CircleOverlapsRect(pipeX, topBottom, 20.0f)) {
                EndRun();
                return;
            }
        }
    }

    private bool CircleOverlapsRect(float pipeX, float rectBottom, float rectTop) {
        float halfWidth = pipeWidth * 0.5f;
        float closestX = Mathf.Clamp(birdX, pipeX - halfWidth, pipeX + halfWidth);
        float closestY = Mathf.Clamp(birdY, rectBottom, rectTop);
        float dx = birdX - closestX;
        float dy = birdY - closestY;
        return dx * dx + dy * dy < birdRadius * birdRadius;
    }

    private void EndRun() {
        state = GameState.GameOver;
        if (score > bestScore) {
            bestScore = score;
            PlayerPrefs.SetInt(BestScoreKey, bestScore);
            PlayerPrefs.Save();
        }
    }

    private void ResetRun() {
        state = GameState.Ready;
        score = 0;
        birdY = 0.0f;
        birdVelocityY = 0.0f;
        birdTilt = 0.0f;
        wingPhase = 0.0f;
        spawnTimer = pipeSpawnInterval;

        for (int i = activePipes.Count - 1; i >= 0; i--) {
            activePipes[i].root.gameObject.SetActive(false);
            pipePool.Push(activePipes[i]);
        }
        activePipes.Clear();
        ApplyBirdTransform();
    }

    private void SpawnPipePair() {
        PipePair pipe = pipePool.Count > 0 ? pipePool.Pop() : CreatePipePair();
        pipe.scored = false;
        pipe.gapCenterY = Mathf.Lerp(pipeGapCenterMin, pipeGapCenterMax, (float)rng.NextDouble());
        pipe.root.gameObject.SetActive(true);
        pipe.root.localPosition = new Vector3(worldHalfWidth + pipeWidth, 0.0f, 0.0f);
        LayoutPipePair(pipe);
        activePipes.Add(pipe);
    }

    private PipePair CreatePipePair() {
        GameObject root = new GameObject("PipePair");
        root.transform.SetParent(transform, false);

        CreateQuad(root.transform, "TopBody", pipeColor, 1);
        CreateQuad(root.transform, "TopLip", pipeLipColor, 2);
        CreateQuad(root.transform, "BottomBody", pipeColor, 1);
        CreateQuad(root.transform, "BottomLip", pipeLipColor, 2);

        return new PipePair { root = root.transform };
    }

    private void LayoutPipePair(PipePair pipe) {
        float gapHalf = pipeGapHeight * 0.5f;
        float topBottom = pipe.gapCenterY + gapHalf;
        float bottomTop = pipe.gapCenterY - gapHalf;
        float topHeight = ceilingY + 2.0f - topBottom;
        float bottomHeight = bottomTop - (groundTopY - 1.0f);
        float lipHeight = 0.45f;
        float lipWidth = pipeWidth + 0.22f;

        Transform topBody = pipe.root.GetChild(0);
        topBody.localPosition = new Vector3(0.0f, topBottom + topHeight * 0.5f, 0.0f);
        topBody.localScale = new Vector3(pipeWidth, topHeight, 1.0f);

        Transform topLip = pipe.root.GetChild(1);
        topLip.localPosition = new Vector3(0.0f, topBottom + lipHeight * 0.5f, 0.0f);
        topLip.localScale = new Vector3(lipWidth, lipHeight, 1.0f);

        Transform bottomBody = pipe.root.GetChild(2);
        bottomBody.localPosition = new Vector3(0.0f, bottomTop - bottomHeight * 0.5f, 0.0f);
        bottomBody.localScale = new Vector3(pipeWidth, bottomHeight, 1.0f);

        Transform bottomLip = pipe.root.GetChild(3);
        bottomLip.localPosition = new Vector3(0.0f, bottomTop - lipHeight * 0.5f, 0.0f);
        bottomLip.localScale = new Vector3(lipWidth, lipHeight, 1.0f);
    }

    private void ConfigureCamera() {
        Camera camera = ResolveOutputCamera();
        if (camera == null) {
            worldHalfWidth = 6.67f;
            return;
        }

        arcadeOutputMode = camera.targetTexture != null;
        camera.orthographic = true;
        camera.orthographicSize = 5.0f;
        if (camera.transform.IsChildOf(transform)) {
            camera.transform.localPosition = new Vector3(0.0f, 0.0f, -10.0f);
            camera.transform.localRotation = Quaternion.identity;
        } else {
            camera.transform.position = new Vector3(0.0f, 0.0f, -10.0f);
            camera.transform.rotation = Quaternion.identity;
        }
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = skyColor;
        renderLayer = gameObject.layer;
        if (arcadeOutputMode) {
            int uiLayer = LayerMask.NameToLayer("UI");
            renderLayer = uiLayer >= 0 ? uiLayer : gameObject.layer;
            SetLayerRecursively(gameObject, renderLayer);
            camera.cullingMask = 1 << renderLayer;
        }
        worldHalfWidth = camera.orthographicSize * camera.aspect;
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

    private void CreateSprites() {
        Texture2D solidTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color32[] solidPixels = new Color32[16];
        for (int i = 0; i < solidPixels.Length; i++) {
            solidPixels[i] = new Color32(255, 255, 255, 255);
        }
        solidTexture.SetPixels32(solidPixels);
        solidTexture.Apply();
        solidSprite = Sprite.Create(solidTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4.0f);

        birdSprite = CreateBirdSprite();
        wingSprite = CreateEllipseSprite(36, 24, new Color(1.0f, 0.96f, 0.85f, 1.0f));
        cloudSprite = CreateCloudSprite();
    }

    private Sprite CreateBirdSprite() {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        Color belly = Color.Lerp(birdColor, Color.white, 0.45f);
        Color beak = new Color(0.95f, 0.5f, 0.15f, 1.0f);

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dx = x - 28.0f;
                float dy = y - 32.0f;
                Color pixel = clear;
                if (dx * dx + dy * dy <= 26.0f * 26.0f) {
                    pixel = dy < -8.0f ? belly : birdColor;
                }
                float beakDx = x - 52.0f;
                float beakDy = (y - 30.0f) * 1.8f;
                if (beakDx * beakDx + beakDy * beakDy <= 11.0f * 11.0f) {
                    pixel = beak;
                }
                float eyeDx = x - 40.0f;
                float eyeDy = y - 42.0f;
                if (eyeDx * eyeDx + eyeDy * eyeDy <= 8.0f * 8.0f) {
                    pixel = Color.white;
                }
                if ((x - 43.0f) * (x - 43.0f) + (y - 42.0f) * (y - 42.0f) <= 3.5f * 3.5f) {
                    pixel = new Color(0.1f, 0.1f, 0.1f, 1.0f);
                }
                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.45f, 0.5f), 80.0f);
    }

    private Sprite CreateEllipseSprite(int width, int height, Color color) {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color clear = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                float nx = (x - halfWidth + 0.5f) / halfWidth;
                float ny = (y - halfHeight + 0.5f) / halfHeight;
                texture.SetPixel(x, y, nx * nx + ny * ny <= 1.0f ? color : clear);
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.85f, 0.5f), 80.0f);
    }

    private Sprite CreateCloudSprite() {
        const int width = 96;
        const int height = 48;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color clear = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        Color white = new Color(1.0f, 1.0f, 1.0f, 0.9f);
        Vector3[] puffs = {
            new Vector3(28.0f, 20.0f, 17.0f),
            new Vector3(50.0f, 26.0f, 20.0f),
            new Vector3(72.0f, 19.0f, 15.0f)
        };

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                Color pixel = clear;
                for (int p = 0; p < puffs.Length; p++) {
                    float dx = x - puffs[p].x;
                    float dy = y - puffs[p].y;
                    if (dx * dx + dy * dy <= puffs[p].z * puffs[p].z) {
                        pixel = white;
                        break;
                    }
                }
                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 48.0f);
    }

    private void CreateBird() {
        GameObject bird = new GameObject("Bird");
        bird.layer = renderLayer;
        bird.transform.SetParent(transform, false);
        SpriteRenderer renderer = bird.AddComponent<SpriteRenderer>();
        renderer.sprite = birdSprite;
        renderer.sortingOrder = 10;
        birdTransform = bird.transform;

        GameObject wing = new GameObject("Wing");
        wing.layer = renderLayer;
        wing.transform.SetParent(bird.transform, false);
        wing.transform.localPosition = new Vector3(-0.05f, -0.02f, 0.0f);
        SpriteRenderer wingRenderer = wing.AddComponent<SpriteRenderer>();
        wingRenderer.sprite = wingSprite;
        wingRenderer.sortingOrder = 11;
        wingTransform = wing.transform;
    }

    private void CreateGround() {
        float groundWidth = worldHalfWidth * 2.0f + 4.0f;

        Transform dirt = CreateQuad(transform, "Dirt", groundColor, 5);
        dirt.localPosition = new Vector3(0.0f, groundTopY - 0.65f, 0.0f);
        dirt.localScale = new Vector3(groundWidth, 1.3f, 1.0f);

        Transform grass = CreateQuad(transform, "Grass", grassColor, 6);
        grass.localPosition = new Vector3(0.0f, groundTopY - 0.11f, 0.0f);
        grass.localScale = new Vector3(groundWidth, 0.22f, 1.0f);

        Color stripeColor = Color.Lerp(grassColor, Color.black, 0.25f);
        int stripeCount = Mathf.CeilToInt(groundWidth / 2.0f) + 1;
        for (int i = 0; i < stripeCount; i++) {
            Transform stripe = CreateQuad(transform, "GrassStripe", stripeColor, 7);
            stripe.localPosition = new Vector3(-worldHalfWidth + i * 2.0f, groundTopY - 0.11f, 0.0f);
            stripe.localScale = new Vector3(0.6f, 0.22f, 1.0f);
            groundStripes.Add(stripe);
        }
    }

    private void CreateClouds() {
        for (int i = 0; i < 4; i++) {
            GameObject cloud = new GameObject("Cloud");
            cloud.layer = renderLayer;
            cloud.transform.SetParent(transform, false);
            SpriteRenderer renderer = cloud.AddComponent<SpriteRenderer>();
            renderer.sprite = cloudSprite;
            renderer.sortingOrder = 0;
            float t = (float)rng.NextDouble();
            cloud.transform.localPosition = new Vector3(
                Mathf.Lerp(-worldHalfWidth, worldHalfWidth, (i + t * 0.5f) / 4.0f),
                Mathf.Lerp(1.0f, 4.2f, (float)rng.NextDouble()),
                0.0f
            );
            float scale = Mathf.Lerp(0.7f, 1.4f, (float)rng.NextDouble());
            cloud.transform.localScale = new Vector3(scale, scale, 1.0f);
            clouds.Add(cloud.transform);
            cloudSpeeds.Add(Mathf.Lerp(0.25f, 0.7f, (float)rng.NextDouble()));
        }
    }

    private Transform CreateQuad(Transform parent, string name, Color color, int sortingOrder) {
        GameObject quad = new GameObject(name);
        quad.layer = renderLayer;
        quad.transform.SetParent(parent, false);
        SpriteRenderer renderer = quad.AddComponent<SpriteRenderer>();
        renderer.sprite = solidSprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return quad.transform;
    }

    private void CreateArcadeHud() {
        if (!showHud || !arcadeOutputMode) {
            return;
        }

        GameObject hudRoot = new GameObject("Arcade HUD");
        hudRoot.layer = renderLayer;
        hudRoot.transform.SetParent(transform, false);
        arcadeScoreText = CreateHudText(hudRoot.transform, "Score", new Vector3(0.0f, 4.45f, -0.1f), ArcadeHudScoreSize, FontStyle.Bold);
        arcadeMessageText = CreateHudText(hudRoot.transform, "Message", new Vector3(0.0f, 3.95f, -0.1f), ArcadeHudTitleSize, FontStyle.Bold);
        arcadeSmallText = CreateHudText(hudRoot.transform, "Small Message", new Vector3(0.0f, 3.14f, -0.1f), ArcadeHudBodySize, FontStyle.Bold);
        arcadePromptText = CreateHudText(hudRoot.transform, "Prompt", new Vector3(0.0f, 2.52f, -0.1f), ArcadeHudBodySize, FontStyle.Normal);
    }

    private HudText CreateHudText(Transform parent, string name, Vector3 localPosition, float characterSize, FontStyle fontStyle) {
        Vector3 shadowPosition = localPosition + new Vector3(ArcadeHudShadowOffset, -ArcadeHudShadowOffset, 0.0f);
        TextMesh shadow = CreateHudTextMesh(parent, name + " Shadow", shadowPosition, characterSize, fontStyle, new Color(0.0f, 0.0f, 0.0f, 0.62f), 99);
        TextMesh foreground = CreateHudTextMesh(parent, name, localPosition, characterSize, fontStyle, Color.white, 100);
        return new HudText { foreground = foreground, shadow = shadow };
    }

    private TextMesh CreateHudTextMesh(Transform parent, string name, Vector3 localPosition, float characterSize, FontStyle fontStyle, Color color, int sortingOrder) {
        GameObject textObject = new GameObject(name);
        textObject.layer = renderLayer;
        textObject.transform.SetParent(parent, false);
        textObject.transform.localPosition = localPosition;

        TextMesh textMesh = textObject.AddComponent<TextMesh>();
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = characterSize;
        textMesh.fontSize = 96;
        textMesh.fontStyle = fontStyle;
        textMesh.color = color;

        MeshRenderer renderer = textObject.GetComponent<MeshRenderer>();
        renderer.sortingOrder = sortingOrder;
        return textMesh;
    }

    private static void SetHudText(HudText hudText, string value) {
        if (hudText == null) {
            return;
        }

        hudText.foreground.text = value;
        hudText.shadow.text = value;
    }

    private static void SetLayerRecursively(GameObject root, int layer) {
        root.layer = layer;
        Transform rootTransform = root.transform;
        for (int i = 0; i < rootTransform.childCount; i++) {
            SetLayerRecursively(rootTransform.GetChild(i).gameObject, layer);
        }
    }

    private void UpdateHud() {
        if (!arcadeOutputMode || arcadeScoreText == null) {
            return;
        }

        SetHudText(arcadeScoreText, state == GameState.Playing ? score.ToString() : "");
        if (state == GameState.Ready) {
            SetHudText(arcadeMessageText, "FLOPPY BIRD");
            SetHudText(arcadeSmallText, "BEST: " + bestScore);
            SetHudText(arcadePromptText, "SPACE / CLICK TO FLAP");
        } else if (state == GameState.GameOver) {
            SetHudText(arcadeMessageText, "GAME OVER");
            SetHudText(arcadeSmallText, "SCORE: " + score + "   BEST: " + bestScore);
            SetHudText(arcadePromptText, "SPACE TO RETRY");
        } else {
            SetHudText(arcadeMessageText, "");
            SetHudText(arcadeSmallText, "");
            SetHudText(arcadePromptText, "");
        }
    }

    private void OnGUI() {
        if (!showHud || arcadeOutputMode || !showStandaloneGui) {
            return;
        }

        EnsureGuiStyles();

        if (state == GameState.Playing || state == GameState.GameOver) {
            DrawOutlinedLabel(new Rect(0.0f, Screen.height * 0.06f, Screen.width, 80.0f), score.ToString(), scoreStyle);
        }

        if (state == GameState.Ready) {
            DrawOutlinedLabel(new Rect(0.0f, Screen.height * 0.32f, Screen.width, 60.0f), "FLOPPY BIRD", messageStyle);
            DrawOutlinedLabel(new Rect(0.0f, Screen.height * 0.42f, Screen.width, 40.0f), "Press Space or click to flap", smallStyle);
            DrawOutlinedLabel(new Rect(0.0f, Screen.height * 0.48f, Screen.width, 40.0f), "Best: " + bestScore, smallStyle);
        } else if (state == GameState.GameOver) {
            DrawOutlinedLabel(new Rect(0.0f, Screen.height * 0.32f, Screen.width, 60.0f), "GAME OVER", messageStyle);
            DrawOutlinedLabel(new Rect(0.0f, Screen.height * 0.42f, Screen.width, 40.0f), "Score: " + score + "   Best: " + bestScore, smallStyle);
            DrawOutlinedLabel(new Rect(0.0f, Screen.height * 0.48f, Screen.width, 40.0f), "Press Space to try again", smallStyle);
        }
    }

    private void EnsureGuiStyles() {
        if (scoreStyle != null) {
            return;
        }

        scoreStyle = new GUIStyle(GUI.skin.label) {
            fontSize = Mathf.RoundToInt(Screen.height * 0.07f),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        messageStyle = new GUIStyle(scoreStyle) {
            fontSize = Mathf.RoundToInt(Screen.height * 0.055f)
        };
        smallStyle = new GUIStyle(scoreStyle) {
            fontSize = Mathf.RoundToInt(Screen.height * 0.028f),
            fontStyle = FontStyle.Normal
        };
    }

    private void DrawOutlinedLabel(Rect rect, string text, GUIStyle style) {
        guiContent.text = text;
        Color previous = GUI.color;
        GUI.color = new Color(0.0f, 0.0f, 0.0f, 0.8f);
        GUI.Label(new Rect(rect.x + 2.0f, rect.y + 2.0f, rect.width, rect.height), guiContent, style);
        GUI.color = Color.white;
        GUI.Label(rect, guiContent, style);
        GUI.color = previous;
    }
}
