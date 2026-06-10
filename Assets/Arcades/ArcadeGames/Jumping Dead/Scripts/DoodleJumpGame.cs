using System.Collections.Generic;
using UnityEngine;

public sealed class DoodleJumpGame : MonoBehaviour {
    [SerializeField] private float gravity = -24.0f;
    [SerializeField] private float jumpVelocity = 12.0f;
    [SerializeField] private float springVelocity = 17.0f;
    [SerializeField] private float maxFallSpeed = -20.0f;
    [SerializeField] private float moveAcceleration = 42.0f;
    [SerializeField] private float maxMoveSpeed = 6.0f;
    [SerializeField] private float moveFriction = 28.0f;
    [SerializeField] private float platformWidth = 1.2f;
    [SerializeField] private float platformHeight = 0.3f;
    [SerializeField] private float platformDensityMultiplier = 2.0f;
    [SerializeField] private float scrollThresholdY = 0.5f;
    [SerializeField] private float difficultyHeight = 150.0f;
    [SerializeField] private float springChance = 0.12f;
    [SerializeField] private float enemyStartHeight = 18.0f;
    [SerializeField] private float maxEnemyChance = 0.12f;
    [SerializeField] private float enemyMoveSpeed = 1.35f;
    [SerializeField] private float enemyMinVerticalSpacing = 3.2f;
    [SerializeField] private int randomSeed = 0;
    [SerializeField] private bool showHud = true;
    [SerializeField] private Camera outputCamera;
    [SerializeField] private bool showStandaloneGui = true;
    [SerializeField] private Color paperColor = new Color(0.965f, 0.96f, 0.92f, 1.0f);
    [SerializeField] private Color gridColor = new Color(0.72f, 0.83f, 0.88f, 1.0f);
    [SerializeField] private Color playerColor = new Color(0.69f, 0.85f, 0.27f, 1.0f);
    [SerializeField] private Color normalPlatformColor = new Color(0.45f, 0.78f, 0.3f, 1.0f);
    [SerializeField] private Color movingPlatformColor = new Color(0.42f, 0.66f, 0.94f, 1.0f);
    [SerializeField] private Color breakablePlatformColor = new Color(0.71f, 0.51f, 0.32f, 1.0f);
    [SerializeField] private Color springColor = new Color(0.85f, 0.3f, 0.3f, 1.0f);
    [SerializeField] private Color enemyColor = new Color(0.72f, 0.24f, 0.68f, 1.0f);

    private enum GameState {
        Ready,
        Playing,
        GameOver
    }

    private enum PlatformType {
        Normal,
        Moving,
        Breakable
    }

    private sealed class Platform {
        public Transform root;
        public SpriteRenderer renderer;
        public Transform spring;
        public PlatformType type;
        public float moveSpeed;
        public bool broken;
        public bool hasSpring;
    }

    private sealed class Enemy {
        public Transform root;
        public SpriteRenderer renderer;
        public float moveSpeed;
        public bool defeated;
    }

    private const string GameTitle = "JUMPING DEAD";
    private const string BestScoreKey = "JumpingDead.BestScore";
    private const float PlayerHalfWidth = 0.3f;
    private const float PlayerHalfHeight = 0.36f;
    private const float EnemyHalfWidth = 0.36f;
    private const float EnemyHalfHeight = 0.38f;
    private const int EnemyStompScore = 250;

    private GameState state = GameState.Ready;
    private System.Random rng;
    private Sprite platformSprite;
    private Sprite playerSprite;
    private Sprite springSprite;
    private Sprite enemySprite;
    private Transform playerTransform;
    private float playerX;
    private float playerY;
    private float playerVelocityX;
    private float playerVelocityY;
    private float playerFacing = 1.0f;
    private float squashTimer;
    private float worldHalfWidth;
    private float highestPlatformY;
    private float totalHeight;
    private readonly List<Platform> activePlatforms = new List<Platform>(32);
    private readonly Stack<Platform> platformPool = new Stack<Platform>(32);
    private readonly List<Enemy> activeEnemies = new List<Enemy>(12);
    private readonly Stack<Enemy> enemyPool = new Stack<Enemy>(12);
    private int score;
    private int bestScore;
    private GUIStyle scoreStyle;
    private GUIStyle messageStyle;
    private GUIStyle smallStyle;
    private readonly GUIContent guiContent = new GUIContent();
    private bool arcadeOutputMode;
    private int renderLayer;
    private TextMesh arcadeScoreText;
    private TextMesh arcadeMessageText;
    private TextMesh arcadeSmallText;
    private TextMesh arcadeHintText;

    private void Start() {
        rng = randomSeed == 0 ? new System.Random() : new System.Random(randomSeed);
        bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);

        ConfigureCamera();
        CreateSprites();
        CreateBackground();
        CreatePlayer();
        CreateArcadeHud();
        ResetRun();
        UpdateHud();

        Debug.Log("Jumping Dead ready. Steer with A/D or the arrow keys.");
    }

    private void Update() {
        float deltaTime = Time.deltaTime;
        bool startPressed = Input.GetKeyDown(KeyCode.Space)
            || Input.GetMouseButtonDown(0)
            || GetSteerInput() != 0.0f;

        switch (state) {
            case GameState.Ready:
                UpdateVerticalPhysics(deltaTime);
                ApplyPlayerTransform(deltaTime);
                if (startPressed) {
                    state = GameState.Playing;
                }
                break;
            case GameState.Playing:
                UpdateSteering(deltaTime);
                UpdateVerticalPhysics(deltaTime);
                if (state != GameState.Playing) {
                    ApplyPlayerTransform(deltaTime);
                    break;
                }
                UpdatePlatforms(deltaTime);
                UpdateEnemies(deltaTime);
                ScrollWorld();
                ApplyPlayerTransform(deltaTime);
                CheckDeath();
                break;
            case GameState.GameOver:
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.R) || Input.GetMouseButtonDown(0)) {
                    ResetRun();
                }
                break;
        }

        UpdateHud();
    }

    private float GetSteerInput() {
        bool left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        if (left == right) {
            return 0.0f;
        }
        return left ? -1.0f : 1.0f;
    }

    private void UpdateSteering(float deltaTime) {
        float steer = GetSteerInput();
        if (steer != 0.0f) {
            playerVelocityX = Mathf.MoveTowards(playerVelocityX, steer * maxMoveSpeed, moveAcceleration * deltaTime);
            playerFacing = steer;
        } else {
            playerVelocityX = Mathf.MoveTowards(playerVelocityX, 0.0f, moveFriction * deltaTime);
        }

        playerX += playerVelocityX * deltaTime;
        float wrapLimit = worldHalfWidth + PlayerHalfWidth;
        if (playerX > wrapLimit) {
            playerX = -wrapLimit;
        } else if (playerX < -wrapLimit) {
            playerX = wrapLimit;
        }
    }

    private void UpdateVerticalPhysics(float deltaTime) {
        float previousFeetY = playerY - PlayerHalfHeight;
        playerVelocityY = Mathf.Max(playerVelocityY + gravity * deltaTime, maxFallSpeed);
        playerY += playerVelocityY * deltaTime;

        if (CheckEnemyCollisions(previousFeetY)) {
            return;
        }

        if (playerVelocityY >= 0.0f) {
            return;
        }

        float feetY = playerY - PlayerHalfHeight;
        for (int i = 0; i < activePlatforms.Count; i++) {
            Platform platform = activePlatforms[i];
            if (platform.broken) {
                continue;
            }

            Vector3 position = platform.root.localPosition;
            float platformTop = position.y + platformHeight * 0.5f;
            if (previousFeetY < platformTop || feetY > platformTop) {
                continue;
            }
            if (Mathf.Abs(playerX - position.x) > platformWidth * 0.5f + PlayerHalfWidth * 0.6f) {
                continue;
            }

            if (platform.type == PlatformType.Breakable) {
                BreakPlatform(platform);
                continue;
            }

            playerY = platformTop + PlayerHalfHeight;
            bool springLaunch = platform.hasSpring
                && Mathf.Abs(playerX - (position.x + platform.spring.localPosition.x)) < 0.3f;
            playerVelocityY = springLaunch ? springVelocity : jumpVelocity;
            squashTimer = 1.0f;
            return;
        }
    }

    private void BreakPlatform(Platform platform) {
        platform.broken = true;
        platform.renderer.color = Color.Lerp(breakablePlatformColor, Color.black, 0.35f);
    }

    private bool CheckEnemyCollisions(float previousFeetY) {
        float playerLeft = playerX - PlayerHalfWidth * 0.85f;
        float playerRight = playerX + PlayerHalfWidth * 0.85f;
        float playerBottom = playerY - PlayerHalfHeight;
        float playerTop = playerY + PlayerHalfHeight * 0.75f;

        for (int i = 0; i < activeEnemies.Count; i++) {
            Enemy enemy = activeEnemies[i];
            if (enemy.defeated) {
                continue;
            }

            Vector3 position = enemy.root.localPosition;
            float enemyLeft = position.x - EnemyHalfWidth;
            float enemyRight = position.x + EnemyHalfWidth;
            float enemyBottom = position.y - EnemyHalfHeight;
            float enemyTop = position.y + EnemyHalfHeight;

            bool overlaps = playerRight > enemyLeft
                && playerLeft < enemyRight
                && playerTop > enemyBottom
                && playerBottom < enemyTop;
            if (!overlaps) {
                continue;
            }

            bool stomped = playerVelocityY < 0.0f
                && previousFeetY >= enemyTop - 0.08f
                && playerBottom <= enemyTop + 0.12f
                && playerY > position.y;
            if (stomped) {
                DefeatEnemy(enemy);
                playerY = enemyTop + PlayerHalfHeight;
                playerVelocityY = jumpVelocity;
                squashTimer = 1.0f;
                score += EnemyStompScore;
            } else {
                EndRun();
            }

            return true;
        }

        return false;
    }

    private void DefeatEnemy(Enemy enemy) {
        enemy.defeated = true;
        enemy.moveSpeed = 0.0f;
        enemy.renderer.color = Color.Lerp(enemyColor, Color.black, 0.2f);
    }

    private void UpdatePlatforms(float deltaTime) {
        float killY = -6.0f;
        for (int i = activePlatforms.Count - 1; i >= 0; i--) {
            Platform platform = activePlatforms[i];
            Vector3 position = platform.root.localPosition;

            if (platform.broken) {
                position.y -= 5.0f * deltaTime;
                platform.root.localRotation = Quaternion.Euler(0.0f, 0.0f, platform.root.localEulerAngles.z + 90.0f * deltaTime);
            } else if (platform.type == PlatformType.Moving) {
                position.x += platform.moveSpeed * deltaTime;
                float limit = worldHalfWidth - platformWidth * 0.5f;
                if (position.x > limit) {
                    position.x = limit;
                    platform.moveSpeed = -Mathf.Abs(platform.moveSpeed);
                } else if (position.x < -limit) {
                    position.x = -limit;
                    platform.moveSpeed = Mathf.Abs(platform.moveSpeed);
                }
            }

            platform.root.localPosition = position;

            if (position.y < killY) {
                RecyclePlatform(i);
            }
        }
    }

    private void UpdateEnemies(float deltaTime) {
        float killY = -6.0f;
        for (int i = activeEnemies.Count - 1; i >= 0; i--) {
            Enemy enemy = activeEnemies[i];
            Vector3 position = enemy.root.localPosition;

            if (enemy.defeated) {
                position.y -= 5.8f * deltaTime;
                enemy.root.localRotation = Quaternion.Euler(0.0f, 0.0f, enemy.root.localEulerAngles.z + 180.0f * deltaTime);
            } else {
                position.x += enemy.moveSpeed * deltaTime;
                float limit = worldHalfWidth - EnemyHalfWidth;
                if (position.x > limit) {
                    position.x = limit;
                    enemy.moveSpeed = -Mathf.Abs(enemy.moveSpeed);
                } else if (position.x < -limit) {
                    position.x = -limit;
                    enemy.moveSpeed = Mathf.Abs(enemy.moveSpeed);
                }
            }

            enemy.root.localPosition = position;

            if (position.y < killY) {
                RecycleEnemy(i);
            }
        }
    }

    private void ScrollWorld() {
        if (playerY <= scrollThresholdY) {
            return;
        }

        float delta = playerY - scrollThresholdY;
        playerY = scrollThresholdY;
        totalHeight += delta;
        score = Mathf.Max(score, Mathf.RoundToInt(totalHeight * 10.0f));
        highestPlatformY -= delta;

        for (int i = 0; i < activePlatforms.Count; i++) {
            Vector3 position = activePlatforms[i].root.localPosition;
            position.y -= delta;
            activePlatforms[i].root.localPosition = position;
        }

        for (int i = 0; i < activeEnemies.Count; i++) {
            Vector3 position = activeEnemies[i].root.localPosition;
            position.y -= delta;
            activeEnemies[i].root.localPosition = position;
        }

        FillPlatforms();
    }

    private void FillPlatforms() {
        float spawnCeiling = 6.5f;
        float difficulty = Mathf.Clamp01(totalHeight / difficultyHeight);
        float platformGapScale = 1.0f / Mathf.Max(0.1f, platformDensityMultiplier);

        while (highestPlatformY < spawnCeiling) {
            float gapMin = Mathf.Lerp(0.9f, 1.5f, difficulty) * platformGapScale;
            float gapMax = Mathf.Lerp(1.7f, 2.7f, difficulty) * platformGapScale;
            highestPlatformY += Mathf.Lerp(gapMin, gapMax, (float)rng.NextDouble());

            float movingChance = Mathf.Lerp(0.05f, 0.35f, difficulty);
            PlatformType type = (float)rng.NextDouble() < movingChance ? PlatformType.Moving : PlatformType.Normal;
            Platform platform = SpawnPlatform(type, RandomPlatformX(), highestPlatformY);

            if (platform.type == PlatformType.Normal && (float)rng.NextDouble() < springChance) {
                AttachSpring(platform);
            }

            float breakableChance = Mathf.Lerp(0.1f, 0.35f, difficulty);
            if ((float)rng.NextDouble() < breakableChance) {
                float extraY = highestPlatformY - Mathf.Lerp(0.4f, 0.9f, (float)rng.NextDouble());
                SpawnPlatform(PlatformType.Breakable, RandomPlatformX(), extraY);
            }

            TrySpawnEnemy(highestPlatformY, difficulty);
        }
    }

    private float RandomPlatformX() {
        float limit = worldHalfWidth - platformWidth * 0.5f;
        return Mathf.Lerp(-limit, limit, (float)rng.NextDouble());
    }

    private Platform SpawnPlatform(PlatformType type, float x, float y) {
        Platform platform = platformPool.Count > 0 ? platformPool.Pop() : CreatePlatform();
        platform.type = type;
        platform.broken = false;
        platform.hasSpring = false;
        platform.spring.gameObject.SetActive(false);
        platform.moveSpeed = 0.0f;
        platform.root.gameObject.SetActive(true);
        platform.root.localPosition = new Vector3(x, y, 0.0f);
        platform.root.localRotation = Quaternion.identity;

        switch (type) {
            case PlatformType.Normal:
                platform.renderer.color = normalPlatformColor;
                break;
            case PlatformType.Moving:
                platform.renderer.color = movingPlatformColor;
                float speed = Mathf.Lerp(1.2f, 2.6f, (float)rng.NextDouble());
                platform.moveSpeed = rng.NextDouble() < 0.5 ? -speed : speed;
                break;
            case PlatformType.Breakable:
                platform.renderer.color = breakablePlatformColor;
                break;
        }

        activePlatforms.Add(platform);
        return platform;
    }

    private void AttachSpring(Platform platform) {
        platform.hasSpring = true;
        platform.spring.gameObject.SetActive(true);
        float offsetLimit = platformWidth * 0.5f - 0.2f;
        float offsetX = Mathf.Lerp(-offsetLimit, offsetLimit, (float)rng.NextDouble());
        platform.spring.localPosition = new Vector3(offsetX, platformHeight * 0.5f + 0.11f, 0.0f);
    }

    private void TrySpawnEnemy(float platformY, float difficulty) {
        if (totalHeight < enemyStartHeight) {
            return;
        }

        float enemyChance = Mathf.Lerp(maxEnemyChance * 0.35f, maxEnemyChance, difficulty);
        if ((float)rng.NextDouble() >= enemyChance) {
            return;
        }

        float enemyY = platformY + Mathf.Lerp(0.6f, 1.0f, (float)rng.NextDouble());
        if (!HasEnemySpace(enemyY)) {
            return;
        }

        SpawnEnemy(RandomEnemyX(), enemyY);
    }

    private bool HasEnemySpace(float y) {
        for (int i = 0; i < activeEnemies.Count; i++) {
            if (Mathf.Abs(activeEnemies[i].root.localPosition.y - y) < enemyMinVerticalSpacing) {
                return false;
            }
        }

        return true;
    }

    private float RandomEnemyX() {
        float limit = worldHalfWidth - EnemyHalfWidth;
        return Mathf.Lerp(-limit, limit, (float)rng.NextDouble());
    }

    private Enemy SpawnEnemy(float x, float y) {
        Enemy enemy = enemyPool.Count > 0 ? enemyPool.Pop() : CreateEnemy();
        enemy.defeated = false;
        enemy.moveSpeed = enemyMoveSpeed * (rng.NextDouble() < 0.5 ? -1.0f : 1.0f);
        enemy.renderer.color = Color.white;
        enemy.root.gameObject.SetActive(true);
        enemy.root.localPosition = new Vector3(x, y, 0.0f);
        enemy.root.localRotation = Quaternion.identity;
        enemy.root.localScale = Vector3.one;
        activeEnemies.Add(enemy);
        return enemy;
    }

    private Enemy CreateEnemy() {
        GameObject root = new GameObject("Monster");
        root.layer = renderLayer;
        root.transform.SetParent(transform, false);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = enemySprite;
        renderer.sortingOrder = 8;

        return new Enemy { root = root.transform, renderer = renderer };
    }

    private Platform CreatePlatform() {
        GameObject root = new GameObject("Platform");
        root.layer = renderLayer;
        root.transform.SetParent(transform, false);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = platformSprite;
        renderer.sortingOrder = 5;

        GameObject spring = new GameObject("Spring");
        spring.layer = renderLayer;
        spring.transform.SetParent(root.transform, false);
        SpriteRenderer springRenderer = spring.AddComponent<SpriteRenderer>();
        springRenderer.sprite = springSprite;
        springRenderer.color = springColor;
        springRenderer.sortingOrder = 6;
        spring.SetActive(false);

        return new Platform { root = root.transform, renderer = renderer, spring = spring.transform };
    }

    private void RecyclePlatform(int index) {
        Platform platform = activePlatforms[index];
        platform.root.gameObject.SetActive(false);
        activePlatforms.RemoveAt(index);
        platformPool.Push(platform);
    }

    private void RecycleEnemy(int index) {
        Enemy enemy = activeEnemies[index];
        enemy.root.gameObject.SetActive(false);
        activeEnemies.RemoveAt(index);
        enemyPool.Push(enemy);
    }

    private void ApplyPlayerTransform(float deltaTime) {
        squashTimer = Mathf.Max(0.0f, squashTimer - deltaTime * 5.0f);
        float stretch = Mathf.Lerp(1.0f, 1.18f, squashTimer);
        playerTransform.localPosition = new Vector3(playerX, playerY, 0.0f);
        playerTransform.localScale = new Vector3(playerFacing / stretch, stretch, 1.0f);
    }

    private void CheckDeath() {
        if (playerY < -5.6f) {
            EndRun();
        }
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
        totalHeight = 0.0f;
        playerX = 0.0f;
        playerY = -2.8f;
        playerVelocityX = 0.0f;
        playerVelocityY = 0.0f;
        playerFacing = 1.0f;
        squashTimer = 0.0f;

        for (int i = activePlatforms.Count - 1; i >= 0; i--) {
            RecyclePlatform(i);
        }

        for (int i = activeEnemies.Count - 1; i >= 0; i--) {
            RecycleEnemy(i);
        }

        SpawnPlatform(PlatformType.Normal, 0.0f, -3.4f);
        highestPlatformY = -3.4f;
        FillPlatforms();
        ApplyPlayerTransform(0.0f);
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
        camera.backgroundColor = paperColor;
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
        platformSprite = CreatePlatformSprite();
        playerSprite = CreatePlayerSprite();
        springSprite = CreateSpringSprite();
        enemySprite = CreateEnemySprite();
    }

    private Sprite CreatePlatformSprite() {
        const int width = 64;
        const int height = 16;
        const float cornerRadius = 6.0f;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color clear = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        Color edge = new Color(0.82f, 0.82f, 0.82f, 1.0f);

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                float cx = Mathf.Clamp(x, cornerRadius, width - 1 - cornerRadius);
                float cy = Mathf.Clamp(y, cornerRadius, height - 1 - cornerRadius);
                float dx = x - cx;
                float dy = y - cy;
                if (dx * dx + dy * dy > cornerRadius * cornerRadius) {
                    texture.SetPixel(x, y, clear);
                } else {
                    texture.SetPixel(x, y, y < 4 ? edge : Color.white);
                }
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), width / platformWidth);
    }

    private Sprite CreatePlayerSprite() {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        Color body = playerColor;
        Color bodyShade = Color.Lerp(playerColor, Color.black, 0.25f);

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                Color pixel = clear;
                float dx = (x - 32.0f) / 22.0f;
                float dy = (y - 34.0f) / 24.0f;
                if (dx * dx + dy * dy <= 1.0f) {
                    pixel = body;
                }

                bool leftFoot = x >= 18 && x <= 28 && y >= 4 && y <= 12;
                bool rightFoot = x >= 36 && x <= 46 && y >= 4 && y <= 12;
                if (leftFoot || rightFoot) {
                    pixel = bodyShade;
                }

                float snoutDx = (x - 52.0f) / 10.0f;
                float snoutDy = (y - 38.0f) / 6.0f;
                if (snoutDx * snoutDx + snoutDy * snoutDy <= 1.0f) {
                    pixel = body;
                }

                float eyeDx = x - 40.0f;
                float eyeDy = y - 44.0f;
                if (eyeDx * eyeDx + eyeDy * eyeDy <= 6.0f * 6.0f) {
                    pixel = Color.white;
                }
                if ((x - 42.0f) * (x - 42.0f) + (y - 44.0f) * (y - 44.0f) <= 2.5f * 2.5f) {
                    pixel = new Color(0.1f, 0.1f, 0.1f, 1.0f);
                }

                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.45f), 84.0f);
    }

    private Sprite CreateSpringSprite() {
        const int width = 14;
        const int height = 10;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color clear = new Color(0.0f, 0.0f, 0.0f, 0.0f);

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                bool coil = y < 6 && (x + y) % 3 != 0;
                bool cap = y >= 6;
                texture.SetPixel(x, y, coil || cap ? Color.white : clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 56.0f);
    }

    private Sprite CreateEnemySprite() {
        const int width = 56;
        const int height = 52;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color clear = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        Color body = enemyColor;
        Color bodyShade = Color.Lerp(enemyColor, Color.black, 0.25f);
        Color mouth = new Color(0.12f, 0.05f, 0.12f, 1.0f);

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                Color pixel = clear;
                float dx = (x - 28.0f) / 22.0f;
                float dy = (y - 24.0f) / 19.0f;
                bool bodyPixel = dx * dx + dy * dy <= 1.0f;
                bool leftHorn = y >= 34 && y <= 48 && x >= 8 + (48 - y) / 2 && x <= 18 - (48 - y) / 3;
                bool rightHorn = y >= 34 && y <= 48 && x >= 38 + (48 - y) / 3 && x <= 48 - (48 - y) / 2;
                bool leftFoot = y < 8 && x >= 14 && x <= 23;
                bool rightFoot = y < 8 && x >= 33 && x <= 42;

                if (bodyPixel || leftHorn || rightHorn || leftFoot || rightFoot) {
                    pixel = y < 15 ? bodyShade : body;
                }

                bool leftEye = (x - 21) * (x - 21) + (y - 30) * (y - 30) <= 6 * 6;
                bool rightEye = (x - 35) * (x - 35) + (y - 30) * (y - 30) <= 6 * 6;
                if (leftEye || rightEye) {
                    pixel = Color.white;
                }

                bool leftPupil = (x - 23) * (x - 23) + (y - 29) * (y - 29) <= 2 * 2;
                bool rightPupil = (x - 37) * (x - 37) + (y - 29) * (y - 29) <= 2 * 2;
                if (leftPupil || rightPupil) {
                    pixel = new Color(0.08f, 0.06f, 0.08f, 1.0f);
                }

                bool mouthPixel = y >= 16 && y <= 20 && x >= 18 && x <= 38 && bodyPixel;
                if (mouthPixel) {
                    pixel = mouth;
                }

                bool tooth = y >= 13 && y < 17 && (x == 23 || x == 28 || x == 33);
                if (tooth && bodyPixel) {
                    pixel = Color.white;
                }

                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Point;
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.45f), height / (EnemyHalfHeight * 2.0f));
    }

    private void CreateBackground() {
        int widthPixels = 1024;
        float pixelsPerUnit = widthPixels / (worldHalfWidth * 2.0f);
        int heightPixels = Mathf.RoundToInt(pixelsPerUnit * 10.0f);
        Texture2D texture = new Texture2D(widthPixels, heightPixels, TextureFormat.RGBA32, false);
        int gridStep = Mathf.RoundToInt(pixelsPerUnit * 0.5f);

        for (int y = 0; y < heightPixels; y++) {
            for (int x = 0; x < widthPixels; x++) {
                bool line = x % gridStep == 0 || y % gridStep == 0;
                texture.SetPixel(x, y, line ? gridColor : paperColor);
            }
        }

        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, widthPixels, heightPixels), new Vector2(0.5f, 0.5f), pixelsPerUnit);

        GameObject background = new GameObject("Background");
        background.layer = renderLayer;
        background.transform.SetParent(transform, false);
        SpriteRenderer renderer = background.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = -10;
    }

    private void CreatePlayer() {
        GameObject player = new GameObject("Doodler");
        player.layer = renderLayer;
        player.transform.SetParent(transform, false);
        SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
        renderer.sprite = playerSprite;
        renderer.sortingOrder = 10;
        playerTransform = player.transform;
    }

    private void CreateArcadeHud() {
        if (!showHud || !arcadeOutputMode) {
            return;
        }

        GameObject hudRoot = new GameObject("Arcade HUD");
        hudRoot.layer = renderLayer;
        hudRoot.transform.SetParent(transform, false);
        arcadeScoreText = CreateHudText(hudRoot.transform, "Score", new Vector3(-worldHalfWidth + 0.35f, 4.55f, -0.1f), 0.09f, FontStyle.Bold);
        arcadeMessageText = CreateHudText(hudRoot.transform, "Message", new Vector3(0.0f, 4.05f, -0.1f), 0.085f, FontStyle.Bold);
        arcadeSmallText = CreateHudText(hudRoot.transform, "Small Message", new Vector3(worldHalfWidth - 0.35f, 4.55f, -0.1f), 0.055f, FontStyle.Normal);
        arcadeHintText = CreateHudText(hudRoot.transform, "Hint", new Vector3(0.0f, -4.15f, -0.1f), 0.055f, FontStyle.Normal);
        arcadeScoreText.anchor = TextAnchor.UpperLeft;
        arcadeScoreText.alignment = TextAlignment.Left;
        arcadeSmallText.anchor = TextAnchor.UpperRight;
        arcadeSmallText.alignment = TextAlignment.Right;
    }

    private TextMesh CreateHudText(Transform parent, string name, Vector3 localPosition, float characterSize, FontStyle fontStyle) {
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
        textMesh.color = new Color(0.08f, 0.07f, 0.08f, 1.0f);

        MeshRenderer renderer = textObject.GetComponent<MeshRenderer>();
        renderer.sortingOrder = 100;
        return textMesh;
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

        arcadeScoreText.text = state == GameState.Playing ? score.ToString() : "";
        if (state == GameState.Ready) {
            arcadeMessageText.text = GameTitle;
            arcadeSmallText.text = "BEST " + bestScore;
            arcadeHintText.text = "A/D OR ARROWS TO STEER";
        } else if (state == GameState.GameOver) {
            arcadeMessageText.text = "GAME OVER";
            arcadeSmallText.text = "SCORE " + score + "\nBEST " + bestScore;
            arcadeHintText.text = "SPACE TO RETRY";
        } else {
            arcadeMessageText.text = "";
            arcadeSmallText.text = "";
            arcadeHintText.text = "";
        }
    }

    private void OnGUI() {
        if (!showHud || arcadeOutputMode || !showStandaloneGui) {
            return;
        }

        EnsureGuiStyles();

        if (state == GameState.Playing) {
            DrawOutlinedLabel(new Rect(0.0f, Screen.height * 0.04f, Screen.width, 70.0f), score.ToString(), scoreStyle);
        }

        if (state == GameState.Ready) {
            DrawOutlinedLabel(new Rect(0.0f, Screen.height * 0.12f, Screen.width, 60.0f), GameTitle, messageStyle);
            DrawOutlinedLabel(new Rect(0.0f, Screen.height * 0.21f, Screen.width, 40.0f), "Best: " + bestScore, smallStyle);
            DrawOutlinedLabel(new Rect(0.0f, Screen.height * 0.82f, Screen.width, 40.0f), "Steer with A/D or the arrow keys", smallStyle);
        } else if (state == GameState.GameOver) {
            DrawOutlinedLabel(new Rect(0.0f, Screen.height * 0.12f, Screen.width, 60.0f), "GAME OVER", messageStyle);
            DrawOutlinedLabel(new Rect(0.0f, Screen.height * 0.21f, Screen.width, 40.0f), "Score: " + score + "   Best: " + bestScore, smallStyle);
            DrawOutlinedLabel(new Rect(0.0f, Screen.height * 0.82f, Screen.width, 40.0f), "Press Space to try again", smallStyle);
        }
    }

    private void EnsureGuiStyles() {
        if (scoreStyle != null) {
            return;
        }

        scoreStyle = new GUIStyle(GUI.skin.label) {
            fontSize = Mathf.RoundToInt(Screen.height * 0.06f),
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
