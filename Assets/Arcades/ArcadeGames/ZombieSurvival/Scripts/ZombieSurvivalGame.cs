using System.Collections.Generic;
using UnityEngine;

public sealed class ZombieSurvivalGame : MonoBehaviour {
    [SerializeField] private float playerMoveSpeed = 4.2f;
    [SerializeField] private float playerRadius = 0.34f;
    [SerializeField] private int playerMaxHealth = 5;
    [SerializeField] private float invulnerabilityDuration = 1.3f;
    [SerializeField] private float fireInterval = 0.18f;
    [SerializeField] private float rapidFireInterval = 0.07f;
    [SerializeField] private float rapidFireDuration = 6.0f;
    [SerializeField] private float bulletSpeed = 14.0f;
    [SerializeField] private float healthDropChance = 0.1f;
    [SerializeField] private float rapidDropChance = 0.08f;
    [SerializeField] private float waveBreakDuration = 2.5f;
    [SerializeField] private int randomSeed = 0;
    [SerializeField] private bool showHud = true;
    [SerializeField] private Color groundColor = new Color(0.16f, 0.19f, 0.15f, 1.0f);
    [SerializeField] private Color groundCheckerColor = new Color(0.14f, 0.17f, 0.13f, 1.0f);
    [SerializeField] private Texture2D backgroundTexture;
    [SerializeField] private Texture2D readyLogoTexture;
    [SerializeField] private float readyLogoWidth = 10.0f;
    [SerializeField] private float arcadeMouseAimSensitivity = 0.08f;
    [SerializeField] private float visualIntensity = 1.0f;
    [SerializeField] private float vignetteStrength = 0.62f;
    [SerializeField] private float cameraShakeDuration = 0.12f;
    [SerializeField] private float cameraShakeMagnitude = 0.08f;
    [SerializeField] private float impactEffectLifetime = 0.28f;
    [SerializeField] private Camera outputCamera;
    [SerializeField] private bool showStandaloneGui = true;

    private enum GameState {
        Ready,
        Playing,
        GameOver
    }

    private enum ZombieType {
        Walker,
        Runner,
        Brute
    }

    private enum PickupType {
        Health,
        RapidFire
    }

    private sealed class Zombie {
        public Transform root;
        public SpriteRenderer renderer;
        public SpriteRenderer shadowRenderer;
        public ZombieType type;
        public float health;
        public float speed;
        public float radius;
        public float flashTimer;
        public float wobblePhase;
        public Vector2 position;
    }

    private sealed class Bullet {
        public Transform root;
        public SpriteRenderer renderer;
        public SpriteRenderer trailRenderer;
        public Vector2 position;
        public Vector2 velocity;
    }

    private sealed class Pickup {
        public Transform root;
        public SpriteRenderer renderer;
        public SpriteRenderer glowRenderer;
        public PickupType type;
        public Vector2 position;
        public float lifeTimer;
        public float pulsePhase;
    }

    private sealed class Splat {
        public Transform root;
        public SpriteRenderer renderer;
        public float fadeTimer;
    }

    private sealed class VisualEffect {
        public Transform root;
        public SpriteRenderer renderer;
        public Vector2 velocity;
        public float lifetime;
        public float timer;
        public float spinSpeed;
        public Vector3 startScale;
        public Vector3 endScale;
        public Color startColor;
        public Color endColor;
    }

    private sealed class HudText {
        public TextMesh foreground;
        public TextMesh shadow;
    }

    private const string BestScoreKey = "ZombieSurvival.BestScore";
    private const float SplatFadeDuration = 6.0f;
    private const float PickupLifetime = 8.0f;
    private const float PlayerDamageFlashDuration = 0.18f;
    private const float GroundPixelsPerUnit = 24.0f;
    private const float ArcadeHudTitleSize = 0.105f;
    private const float ArcadeHudBodySize = 0.052f;
    private const float ArcadeHudScoreSize = 0.062f;
    private const float ArcadeHudShadowOffset = 0.014f;

    private GameState state = GameState.Ready;
    private System.Random rng;
    private System.Random visualRng;
    private Camera mainCamera;
    private Vector3 cameraHomePosition;
    private float worldHalfWidth;
    private float worldHalfHeight;
    private Sprite circleSprite;
    private Sprite solidSprite;
    private Sprite[] zombieSprites;
    private Sprite playerSprite;
    private Sprite[] splatSprites;
    private Sprite bulletSprite;
    private Sprite shadowSprite;
    private Sprite pickupGlowSprite;
    private Sprite impactPuffSprite;
    private Sprite muzzleFlashSprite;
    private Sprite healthPickupSprite;
    private Sprite rapidPickupSprite;
    private Texture2D heartFullTexture;
    private Texture2D heartEmptyTexture;
    private Texture2D hudPanelTexture;
    private Texture2D overlayTexture;
    private Texture2D rapidBarTexture;
    private Texture2D rapidBarBackTexture;
    private Transform playerTransform;
    private SpriteRenderer playerRenderer;
    private SpriteRenderer playerShadowRenderer;
    private Transform gunPivot;
    private SpriteRenderer muzzleFlashRenderer;
    private Vector2 playerPosition;
    private Vector2 arcadeAimWorldPosition;
    private bool hasArcadeAimWorldPosition;
    private float aimAngle;
    private int playerHealth;
    private float invulnerabilityTimer;
    private float fireCooldown;
    private float rapidFireTimer;
    private float muzzleFlashTimer;
    private float playerDamageFlashTimer;
    private float cameraShakeTimer;
    private float cameraShakeStrength;
    private int wave;
    private int spawnRemaining;
    private float spawnTimer;
    private float waveBreakTimer;
    private float waveBannerTimer;
    private int score;
    private int bestScore;
    private readonly List<Zombie> activeZombies = new List<Zombie>(48);
    private readonly Stack<Zombie> zombiePool = new Stack<Zombie>(48);
    private readonly List<Bullet> activeBullets = new List<Bullet>(32);
    private readonly Stack<Bullet> bulletPool = new Stack<Bullet>(32);
    private readonly List<Pickup> activePickups = new List<Pickup>(8);
    private readonly Stack<Pickup> pickupPool = new Stack<Pickup>(8);
    private readonly List<Splat> activeSplats = new List<Splat>(40);
    private readonly Stack<Splat> splatPool = new Stack<Splat>(40);
    private readonly List<VisualEffect> activeEffects = new List<VisualEffect>(48);
    private readonly Stack<VisualEffect> effectPool = new Stack<VisualEffect>(48);
    private GUIStyle messageStyle;
    private GUIStyle smallStyle;
    private readonly GUIContent guiContent = new GUIContent();
    private bool arcadeOutputMode;
    private int renderLayer;
    private HudText arcadeScoreText;
    private HudText arcadeStatusText;
    private HudText arcadeMessageText;
    private HudText arcadeSmallText;
    private HudText arcadePromptText;
    private SpriteRenderer readyLogoRenderer;

    private static readonly Color WalkerColor = new Color(0.42f, 0.65f, 0.3f, 1.0f);
    private static readonly Color RunnerColor = new Color(0.78f, 0.72f, 0.3f, 1.0f);
    private static readonly Color BruteColor = new Color(0.6f, 0.28f, 0.24f, 1.0f);

    private void Start() {
        rng = randomSeed == 0 ? new System.Random() : new System.Random(randomSeed);
        visualRng = randomSeed == 0 ? new System.Random(73129) : new System.Random(randomSeed ^ 0x45d9f3b);
        bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);

        ConfigureCamera();
        CreateSprites();
        CreateGround();
        CreatePlayer();
        CreateArcadeHud();
        ResetRun();
        UpdateHud();

        Debug.Log("Survive the Nights ready. WASD/Arrows to move, mouse to aim, click/Space to fire.");
    }

    private void Update() {
        float deltaTime = Time.deltaTime;
        Vector2 moveInput = ReadMoveInput();

        switch (state) {
            case GameState.Ready:
                UpdateAim(moveInput);
                if (StartPressed()) {
                    state = GameState.Playing;
                    StartWave(1);
                }
                break;
            case GameState.Playing:
                UpdatePlayer(deltaTime, moveInput);
                UpdateAim(moveInput);
                UpdateShooting(deltaTime);
                UpdateBullets(deltaTime);
                UpdateZombies(deltaTime);
                UpdateSpawning(deltaTime);
                UpdatePickups(deltaTime);
                UpdateSplats(deltaTime);
                UpdateTimers(deltaTime);
                break;
            case GameState.GameOver:
                UpdateSplats(deltaTime);
                if (RetryPressed()) {
                    ResetRun();
                }
                break;
        }

        UpdateVisualEffects(deltaTime);
        UpdateCameraShake(deltaTime);
        UpdateHud();
    }

    private static Vector2 ReadMoveInput() {
        float moveX = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1.0f : 0.0f)
            - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1.0f : 0.0f);
        float moveY = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1.0f : 0.0f)
            - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1.0f : 0.0f);

        Vector2 move = new Vector2(moveX, moveY);
        if (move.sqrMagnitude > 1.0f) {
            move.Normalize();
        }

        return move;
    }

    private bool StartPressed() {
        return Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.E)
            || Input.GetMouseButtonDown(0);
    }

    private bool RetryPressed() {
        return StartPressed()
            || Input.GetKeyDown(KeyCode.R);
    }

    private bool FireHeld() {
        return Input.GetKey(KeyCode.Space)
            || Input.GetKey(KeyCode.Return)
            || Input.GetKey(KeyCode.E)
            || Input.GetMouseButton(0);
    }

    private void UpdatePlayer(float deltaTime, Vector2 move) {
        playerPosition += move * playerMoveSpeed * deltaTime;
        playerPosition.x = Mathf.Clamp(playerPosition.x, -worldHalfWidth + playerRadius, worldHalfWidth - playerRadius);
        playerPosition.y = Mathf.Clamp(playerPosition.y, -worldHalfHeight + playerRadius, worldHalfHeight - playerRadius);
        playerTransform.localPosition = new Vector3(playerPosition.x, playerPosition.y, 0.0f);

        if (invulnerabilityTimer > 0.0f) {
            invulnerabilityTimer -= deltaTime;
            playerRenderer.enabled = Mathf.Repeat(invulnerabilityTimer, 0.2f) < 0.12f;
            if (invulnerabilityTimer <= 0.0f) {
                playerRenderer.enabled = true;
            }
        }

        if (playerDamageFlashTimer > 0.0f) {
            playerDamageFlashTimer -= deltaTime;
            float flash = Mathf.Clamp01(playerDamageFlashTimer / PlayerDamageFlashDuration);
            playerRenderer.color = Color.Lerp(Color.white, new Color(1.0f, 0.42f, 0.35f, 1.0f), flash);
        } else {
            playerRenderer.color = Color.white;
        }
    }

    private void UpdateAim(Vector2 moveInput) {
        Vector2 aimDirection = arcadeOutputMode
            ? GetArcadeMouseAimDirection(moveInput)
            : GetStandaloneMouseAimDirection(moveInput);

        if (aimDirection.sqrMagnitude > 0.001f) {
            aimAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        }

        gunPivot.localRotation = Quaternion.Euler(0.0f, 0.0f, aimAngle);
    }

    private Vector2 GetStandaloneMouseAimDirection(Vector2 fallbackDirection) {
        Vector2 aimDirection = Vector2.zero;
        if (mainCamera != null) {
            Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            aimDirection = new Vector2(mouseWorld.x - playerPosition.x, mouseWorld.y - playerPosition.y);
        }

        if (aimDirection.sqrMagnitude <= 0.001f) {
            aimDirection = fallbackDirection;
        }

        return aimDirection;
    }

    private Vector2 GetArcadeMouseAimDirection(Vector2 fallbackDirection) {
        if (!hasArcadeAimWorldPosition) {
            float radians = aimAngle * Mathf.Deg2Rad;
            Vector2 initialDirection = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            arcadeAimWorldPosition = playerPosition + initialDirection * Mathf.Min(worldHalfWidth, worldHalfHeight);
            hasArcadeAimWorldPosition = true;
        }

        if (Cursor.lockState == CursorLockMode.Locked) {
            Vector2 mouseDelta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * arcadeMouseAimSensitivity;
            if (mouseDelta.sqrMagnitude > 0.0f) {
                arcadeAimWorldPosition += mouseDelta;
            }
        } else {
            float screenWidth = Mathf.Max(1.0f, Screen.width);
            float screenHeight = Mathf.Max(1.0f, Screen.height);
            float viewportX = Mathf.Clamp01(Input.mousePosition.x / screenWidth);
            float viewportY = Mathf.Clamp01(Input.mousePosition.y / screenHeight);
            arcadeAimWorldPosition = new Vector2(
                Mathf.Lerp(-worldHalfWidth, worldHalfWidth, viewportX),
                Mathf.Lerp(-worldHalfHeight, worldHalfHeight, viewportY)
            );
        }

        arcadeAimWorldPosition = new Vector2(
            Mathf.Clamp(arcadeAimWorldPosition.x, -worldHalfWidth, worldHalfWidth),
            Mathf.Clamp(arcadeAimWorldPosition.y, -worldHalfHeight, worldHalfHeight)
        );

        Vector2 aimDirection = arcadeAimWorldPosition - playerPosition;
        return aimDirection.sqrMagnitude > 0.001f ? aimDirection : fallbackDirection;
    }

    private void UpdateShooting(float deltaTime) {
        fireCooldown -= deltaTime;
        muzzleFlashTimer -= deltaTime;
        muzzleFlashRenderer.enabled = muzzleFlashTimer > 0.0f;

        if (rapidFireTimer > 0.0f) {
            rapidFireTimer -= deltaTime;
        }

        bool firing = FireHeld();
        if (!firing || fireCooldown > 0.0f) {
            return;
        }

        fireCooldown = rapidFireTimer > 0.0f ? rapidFireInterval : fireInterval;
        muzzleFlashTimer = 0.05f;
        muzzleFlashRenderer.transform.localRotation = Quaternion.Euler(0.0f, 0.0f, (float)visualRng.NextDouble() * 360.0f);

        float radians = aimAngle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        Bullet bullet = bulletPool.Count > 0 ? bulletPool.Pop() : CreateBullet();
        bullet.position = playerPosition + direction * 0.55f;
        bullet.velocity = direction * bulletSpeed;
        bullet.root.gameObject.SetActive(true);
        bullet.root.localPosition = new Vector3(bullet.position.x, bullet.position.y, 0.0f);
        bullet.root.localRotation = Quaternion.Euler(0.0f, 0.0f, aimAngle);
        activeBullets.Add(bullet);

        SpawnVisualEffect(
            playerPosition + direction * 0.78f,
            impactPuffSprite,
            new Color(1.0f, 0.72f, 0.34f, 0.48f),
            0.14f,
            0.28f,
            7,
            direction * 0.7f,
            80.0f
        );
    }

    private void UpdateBullets(float deltaTime) {
        for (int i = activeBullets.Count - 1; i >= 0; i--) {
            Bullet bullet = activeBullets[i];
            bullet.position += bullet.velocity * deltaTime;
            bullet.root.localPosition = new Vector3(bullet.position.x, bullet.position.y, 0.0f);
            bullet.root.localRotation = Quaternion.Euler(0.0f, 0.0f, Mathf.Atan2(bullet.velocity.y, bullet.velocity.x) * Mathf.Rad2Deg);

            if (Mathf.Abs(bullet.position.x) > worldHalfWidth + 0.5f || Mathf.Abs(bullet.position.y) > worldHalfHeight + 0.5f) {
                RecycleBullet(i);
                continue;
            }

            for (int z = activeZombies.Count - 1; z >= 0; z--) {
                Zombie zombie = activeZombies[z];
                float hitRange = zombie.radius + 0.1f;
                if ((zombie.position - bullet.position).sqrMagnitude > hitRange * hitRange) {
                    continue;
                }

                zombie.health -= 1.0f;
                zombie.flashTimer = 0.1f;
                SpawnImpactEffect(bullet.position, zombie.type);
                AddCameraShake(zombie.type == ZombieType.Brute ? 0.35f : 0.2f);
                if (zombie.health <= 0.0f) {
                    KillZombie(z);
                }
                RecycleBullet(i);
                break;
            }
        }
    }

    private void UpdateZombies(float deltaTime) {
        for (int i = 0; i < activeZombies.Count; i++) {
            Zombie zombie = activeZombies[i];
            Vector2 toPlayer = playerPosition - zombie.position;
            float distance = toPlayer.magnitude;
            if (distance > 0.001f) {
                zombie.position += toPlayer / distance * zombie.speed * deltaTime;
            }

            if (zombie.flashTimer > 0.0f) {
                zombie.flashTimer -= deltaTime;
                zombie.renderer.color = zombie.flashTimer > 0.0f ? new Color(1.0f, 0.45f, 0.45f, 1.0f) : Color.white;
            }

            if (distance < zombie.radius + playerRadius && invulnerabilityTimer <= 0.0f) {
                playerHealth--;
                invulnerabilityTimer = invulnerabilityDuration;
                playerDamageFlashTimer = PlayerDamageFlashDuration;
                SpawnVisualEffect(
                    playerPosition,
                    impactPuffSprite,
                    new Color(0.9f, 0.12f, 0.08f, 0.52f),
                    0.22f,
                    0.5f,
                    9,
                    -toPlayer.normalized * 0.6f,
                    120.0f
                );
                AddCameraShake(1.0f);
                zombie.position -= toPlayer.normalized * 0.6f;
                if (playerHealth <= 0) {
                    EndRun();
                    return;
                }
            }
        }

        for (int i = 0; i < activeZombies.Count; i++) {
            for (int j = i + 1; j < activeZombies.Count; j++) {
                Zombie a = activeZombies[i];
                Zombie b = activeZombies[j];
                Vector2 delta = b.position - a.position;
                float minDistance = a.radius + b.radius;
                float sqr = delta.sqrMagnitude;
                if (sqr < minDistance * minDistance && sqr > 0.0001f) {
                    float dist = Mathf.Sqrt(sqr);
                    Vector2 push = delta / dist * (minDistance - dist) * 0.5f;
                    a.position -= push;
                    b.position += push;
                }
            }
        }

        for (int i = 0; i < activeZombies.Count; i++) {
            Zombie zombie = activeZombies[i];
            Vector2 toPlayer = playerPosition - zombie.position;
            float facing = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
            float wobble = Mathf.Sin(Time.time * 9.0f + zombie.wobblePhase) * 9.0f;
            zombie.root.localPosition = new Vector3(zombie.position.x, zombie.position.y, 0.0f);
            zombie.root.localRotation = Quaternion.Euler(0.0f, 0.0f, facing + wobble);
        }
    }

    private void UpdateSpawning(float deltaTime) {
        if (spawnRemaining > 0) {
            spawnTimer -= deltaTime;
            if (spawnTimer <= 0.0f) {
                spawnTimer = Mathf.Max(0.3f, 1.2f - wave * 0.05f);
                SpawnZombie();
                spawnRemaining--;
            }
        } else if (activeZombies.Count == 0) {
            waveBreakTimer -= deltaTime;
            if (waveBreakTimer <= 0.0f) {
                score += 100 + 25 * wave;
                StartWave(wave + 1);
            }
        }
    }

    private void UpdatePickups(float deltaTime) {
        for (int i = activePickups.Count - 1; i >= 0; i--) {
            Pickup pickup = activePickups[i];
            pickup.lifeTimer -= deltaTime;
            if (pickup.lifeTimer <= 0.0f) {
                RecyclePickup(i);
                continue;
            }

            bool blink = pickup.lifeTimer < 2.0f && Mathf.Repeat(pickup.lifeTimer, 0.3f) < 0.15f;
            pickup.renderer.enabled = !blink;
            pickup.glowRenderer.enabled = !blink;

            float pulse = 1.0f + Mathf.Sin(Time.time * 5.0f + pickup.pulsePhase) * 0.08f * visualIntensity;
            pickup.root.localScale = new Vector3(pulse, pulse, 1.0f);
            Color glowColor = pickup.type == PickupType.Health
                ? new Color(0.85f, 0.18f, 0.24f, 0.35f)
                : new Color(1.0f, 0.78f, 0.22f, 0.38f);
            glowColor.a *= 0.75f + Mathf.Sin(Time.time * 6.0f + pickup.pulsePhase) * 0.2f;
            pickup.glowRenderer.color = glowColor;

            if ((pickup.position - playerPosition).sqrMagnitude < 0.55f * 0.55f) {
                if (pickup.type == PickupType.Health) {
                    playerHealth = Mathf.Min(playerMaxHealth, playerHealth + 1);
                } else {
                    rapidFireTimer = rapidFireDuration;
                }
                score += 25;
                RecyclePickup(i);
            }
        }
    }

    private void UpdateSplats(float deltaTime) {
        for (int i = activeSplats.Count - 1; i >= 0; i--) {
            Splat splat = activeSplats[i];
            splat.fadeTimer -= deltaTime;
            if (splat.fadeTimer <= 0.0f) {
                splat.root.gameObject.SetActive(false);
                activeSplats.RemoveAt(i);
                splatPool.Push(splat);
                continue;
            }
            float alpha = Mathf.Clamp01(splat.fadeTimer / SplatFadeDuration) * 0.8f;
            splat.renderer.color = new Color(1.0f, 1.0f, 1.0f, alpha);
        }
    }

    private void UpdateVisualEffects(float deltaTime) {
        for (int i = activeEffects.Count - 1; i >= 0; i--) {
            VisualEffect effect = activeEffects[i];
            effect.timer -= deltaTime;
            if (effect.timer <= 0.0f) {
                effect.root.gameObject.SetActive(false);
                activeEffects.RemoveAt(i);
                effectPool.Push(effect);
                continue;
            }

            float t = 1.0f - Mathf.Clamp01(effect.timer / effect.lifetime);
            effect.root.localPosition += new Vector3(effect.velocity.x * deltaTime, effect.velocity.y * deltaTime, 0.0f);
            effect.root.Rotate(0.0f, 0.0f, effect.spinSpeed * deltaTime);
            effect.root.localScale = Vector3.Lerp(effect.startScale, effect.endScale, t);
            effect.renderer.color = Color.Lerp(effect.startColor, effect.endColor, t);
        }
    }

    private void UpdateCameraShake(float deltaTime) {
        if (cameraShakeTimer <= 0.0f) {
            if (mainCamera != null) {
                mainCamera.transform.position = cameraHomePosition;
            }
            return;
        }

        cameraShakeTimer -= deltaTime;
        float duration = Mathf.Max(0.01f, cameraShakeDuration);
        float t = Mathf.Clamp01(cameraShakeTimer / duration);
        float strength = cameraShakeMagnitude * cameraShakeStrength * t * Mathf.Max(0.0f, visualIntensity);
        float noise = Time.time * 58.0f;
        Vector3 offset = new Vector3(Mathf.Sin(noise * 1.73f), Mathf.Cos(noise * 2.11f), 0.0f) * strength;
        mainCamera.transform.position = cameraHomePosition + offset;

        if (cameraShakeTimer <= 0.0f) {
            cameraShakeStrength = 0.0f;
            mainCamera.transform.position = cameraHomePosition;
        }
    }

    private void SpawnImpactEffect(Vector2 position, ZombieType type) {
        Color color = new Color(0.48f, 0.08f, 0.06f, 0.72f);
        if (type == ZombieType.Runner) {
            color = new Color(0.62f, 0.16f, 0.06f, 0.7f);
        } else if (type == ZombieType.Brute) {
            color = new Color(0.36f, 0.04f, 0.04f, 0.78f);
        }

        Vector2 drift = RandomUnitVector() * 0.35f;
        SpawnVisualEffect(position, impactPuffSprite, color, impactEffectLifetime, 0.34f, 9, drift, 160.0f);
        SpawnVisualEffect(position, impactPuffSprite, new Color(0.08f, 0.07f, 0.06f, 0.32f), impactEffectLifetime * 0.8f, 0.24f, 8, -drift * 0.4f, -110.0f);
    }

    private void SpawnDeathBurst(Vector2 position, float scale, ZombieType type) {
        int count = type == ZombieType.Brute ? 7 : 4;
        Color blood = type == ZombieType.Runner
            ? new Color(0.55f, 0.13f, 0.05f, 0.68f)
            : new Color(0.45f, 0.05f, 0.045f, 0.72f);

        for (int i = 0; i < count; i++) {
            Vector2 direction = RandomUnitVector();
            float speed = Mathf.Lerp(0.35f, 1.05f, (float)visualRng.NextDouble()) * Mathf.Max(0.5f, scale);
            float size = Mathf.Lerp(0.2f, 0.42f, (float)visualRng.NextDouble()) * Mathf.Max(0.75f, scale);
            SpawnVisualEffect(position + direction * 0.12f, impactPuffSprite, blood, Mathf.Lerp(0.25f, 0.45f, (float)visualRng.NextDouble()), size, 9, direction * speed, Mathf.Lerp(-180.0f, 180.0f, (float)visualRng.NextDouble()));
        }
    }

    private void SpawnVisualEffect(Vector2 position, Sprite sprite, Color color, float lifetime, float scale, int sortingOrder, Vector2 velocity, float spinSpeed) {
        if (visualIntensity <= 0.0f) {
            return;
        }

        VisualEffect effect = effectPool.Count > 0 ? effectPool.Pop() : CreateVisualEffect();
        float adjustedLifetime = Mathf.Max(0.03f, lifetime);
        float adjustedScale = Mathf.Max(0.01f, scale * Mathf.Lerp(0.75f, 1.15f, Mathf.Clamp01(visualIntensity)));
        Color startColor = color;
        startColor.a *= Mathf.Clamp01(visualIntensity);

        effect.renderer.sprite = sprite;
        effect.renderer.sortingOrder = sortingOrder;
        effect.velocity = velocity;
        effect.lifetime = adjustedLifetime;
        effect.timer = adjustedLifetime;
        effect.spinSpeed = spinSpeed;
        effect.startScale = new Vector3(adjustedScale, adjustedScale, 1.0f);
        effect.endScale = new Vector3(adjustedScale * 1.9f, adjustedScale * 1.9f, 1.0f);
        effect.startColor = startColor;
        effect.endColor = new Color(startColor.r, startColor.g, startColor.b, 0.0f);
        effect.renderer.color = effect.startColor;
        effect.root.localPosition = new Vector3(position.x, position.y, 0.0f);
        effect.root.localRotation = Quaternion.Euler(0.0f, 0.0f, (float)visualRng.NextDouble() * 360.0f);
        effect.root.localScale = effect.startScale;
        effect.root.gameObject.SetActive(true);
        activeEffects.Add(effect);
    }

    private void AddCameraShake(float amount) {
        if (cameraShakeMagnitude <= 0.0f || visualIntensity <= 0.0f) {
            return;
        }

        float strength = Mathf.Max(0.0f, amount);
        cameraShakeTimer = Mathf.Max(cameraShakeTimer, cameraShakeDuration * Mathf.Lerp(0.75f, 1.4f, Mathf.Clamp01(strength)));
        cameraShakeStrength = Mathf.Max(cameraShakeStrength, strength);
    }

    private void UpdateTimers(float deltaTime) {
        waveBannerTimer = Mathf.Max(0.0f, waveBannerTimer - deltaTime);
    }

    private void StartWave(int newWave) {
        wave = newWave;
        spawnRemaining = 6 + wave * 3;
        spawnTimer = 0.6f;
        waveBreakTimer = waveBreakDuration;
        waveBannerTimer = 2.0f;
    }

    private void SpawnZombie() {
        ZombieType type = ZombieType.Walker;
        double roll = rng.NextDouble();
        if (wave >= 3 && roll < 0.15) {
            type = ZombieType.Brute;
        } else if (wave >= 2 && roll < 0.45) {
            type = ZombieType.Runner;
        }

        Zombie zombie = zombiePool.Count > 0 ? zombiePool.Pop() : CreateZombie();
        zombie.type = type;
        zombie.renderer.sprite = zombieSprites[(int)type];
        zombie.renderer.color = Color.white;
        zombie.shadowRenderer.color = new Color(0.0f, 0.0f, 0.0f, type == ZombieType.Brute ? 0.42f : 0.34f);
        zombie.flashTimer = 0.0f;
        zombie.wobblePhase = (float)rng.NextDouble() * 6.28f;

        float speedScale = 1.0f + wave * 0.04f;
        switch (type) {
            case ZombieType.Walker:
                zombie.health = 2.0f;
                zombie.speed = 1.3f * speedScale;
                zombie.radius = 0.34f;
                zombie.root.localScale = Vector3.one;
                break;
            case ZombieType.Runner:
                zombie.health = 1.0f;
                zombie.speed = 2.7f * speedScale;
                zombie.radius = 0.27f;
                zombie.root.localScale = new Vector3(0.8f, 0.8f, 1.0f);
                break;
            case ZombieType.Brute:
                zombie.health = 6.0f;
                zombie.speed = 0.85f * speedScale;
                zombie.radius = 0.55f;
                zombie.root.localScale = new Vector3(1.7f, 1.7f, 1.0f);
                break;
        }

        int side = rng.Next(4);
        float along = (float)rng.NextDouble();
        switch (side) {
            case 0:
                zombie.position = new Vector2(Mathf.Lerp(-worldHalfWidth, worldHalfWidth, along), worldHalfHeight + 0.8f);
                break;
            case 1:
                zombie.position = new Vector2(Mathf.Lerp(-worldHalfWidth, worldHalfWidth, along), -worldHalfHeight - 0.8f);
                break;
            case 2:
                zombie.position = new Vector2(-worldHalfWidth - 0.8f, Mathf.Lerp(-worldHalfHeight, worldHalfHeight, along));
                break;
            default:
                zombie.position = new Vector2(worldHalfWidth + 0.8f, Mathf.Lerp(-worldHalfHeight, worldHalfHeight, along));
                break;
        }

        zombie.root.localPosition = new Vector3(zombie.position.x, zombie.position.y, 0.0f);
        zombie.root.gameObject.SetActive(true);
        activeZombies.Add(zombie);
    }

    private void KillZombie(int index) {
        Zombie zombie = activeZombies[index];

        switch (zombie.type) {
            case ZombieType.Walker:
                score += 10;
                break;
            case ZombieType.Runner:
                score += 15;
                break;
            case ZombieType.Brute:
                score += 50;
                break;
        }

        SpawnSplat(zombie.position, zombie.root.localScale.x);
        SpawnDeathBurst(zombie.position, zombie.root.localScale.x, zombie.type);
        AddCameraShake(zombie.type == ZombieType.Brute ? 0.75f : 0.35f);

        double roll = rng.NextDouble();
        if (roll < healthDropChance) {
            SpawnPickup(PickupType.Health, zombie.position);
        } else if (roll < healthDropChance + rapidDropChance) {
            SpawnPickup(PickupType.RapidFire, zombie.position);
        }

        zombie.root.gameObject.SetActive(false);
        activeZombies.RemoveAt(index);
        zombiePool.Push(zombie);
    }

    private void SpawnSplat(Vector2 position, float scale) {
        Splat splat = splatPool.Count > 0 ? splatPool.Pop() : CreateSplat();
        splat.fadeTimer = SplatFadeDuration;
        splat.renderer.sprite = splatSprites[visualRng.Next(splatSprites.Length)];
        splat.renderer.color = new Color(1.0f, 1.0f, 1.0f, 0.8f);
        splat.root.gameObject.SetActive(true);
        splat.root.localPosition = new Vector3(position.x, position.y, 0.0f);
        splat.root.localRotation = Quaternion.Euler(0.0f, 0.0f, (float)visualRng.NextDouble() * 360.0f);
        float size = scale * Mathf.Lerp(0.8f, 1.25f, (float)visualRng.NextDouble());
        splat.root.localScale = new Vector3(size, size, 1.0f);
        activeSplats.Add(splat);
    }

    private void SpawnPickup(PickupType type, Vector2 position) {
        Pickup pickup = pickupPool.Count > 0 ? pickupPool.Pop() : CreatePickup();
        pickup.type = type;
        pickup.renderer.sprite = type == PickupType.Health ? healthPickupSprite : rapidPickupSprite;
        pickup.renderer.enabled = true;
        pickup.glowRenderer.enabled = true;
        pickup.lifeTimer = PickupLifetime;
        pickup.pulsePhase = (float)visualRng.NextDouble() * Mathf.PI * 2.0f;
        pickup.position = new Vector2(
            Mathf.Clamp(position.x, -worldHalfWidth + 0.5f, worldHalfWidth - 0.5f),
            Mathf.Clamp(position.y, -worldHalfHeight + 0.5f, worldHalfHeight - 0.5f)
        );
        pickup.root.localPosition = new Vector3(pickup.position.x, pickup.position.y, 0.0f);
        pickup.root.localScale = Vector3.one;
        pickup.root.gameObject.SetActive(true);
        activePickups.Add(pickup);
    }

    private void RecycleBullet(int index) {
        Bullet bullet = activeBullets[index];
        bullet.root.gameObject.SetActive(false);
        activeBullets.RemoveAt(index);
        bulletPool.Push(bullet);
    }

    private void RecyclePickup(int index) {
        Pickup pickup = activePickups[index];
        pickup.root.gameObject.SetActive(false);
        pickup.root.localScale = Vector3.one;
        activePickups.RemoveAt(index);
        pickupPool.Push(pickup);
    }

    private void EndRun() {
        state = GameState.GameOver;
        playerRenderer.enabled = true;
        playerRenderer.color = Color.white;
        AddCameraShake(1.2f);
        if (score > bestScore) {
            bestScore = score;
            PlayerPrefs.SetInt(BestScoreKey, bestScore);
            PlayerPrefs.Save();
        }
    }

    private void ResetRun() {
        state = GameState.Ready;
        score = 0;
        wave = 0;
        playerHealth = playerMaxHealth;
        playerPosition = Vector2.zero;
        aimAngle = 0.0f;
        hasArcadeAimWorldPosition = false;
        invulnerabilityTimer = 0.0f;
        fireCooldown = 0.0f;
        rapidFireTimer = 0.0f;
        muzzleFlashTimer = 0.0f;
        playerDamageFlashTimer = 0.0f;
        cameraShakeTimer = 0.0f;
        cameraShakeStrength = 0.0f;
        spawnRemaining = 0;
        waveBannerTimer = 0.0f;
        playerRenderer.enabled = true;
        playerRenderer.color = Color.white;
        playerTransform.localPosition = Vector3.zero;
        mainCamera.transform.position = cameraHomePosition;

        for (int i = activeZombies.Count - 1; i >= 0; i--) {
            activeZombies[i].root.gameObject.SetActive(false);
            zombiePool.Push(activeZombies[i]);
        }
        activeZombies.Clear();

        for (int i = activeBullets.Count - 1; i >= 0; i--) {
            RecycleBullet(i);
        }
        for (int i = activePickups.Count - 1; i >= 0; i--) {
            RecyclePickup(i);
        }
        for (int i = activeSplats.Count - 1; i >= 0; i--) {
            activeSplats[i].root.gameObject.SetActive(false);
            splatPool.Push(activeSplats[i]);
            activeSplats.RemoveAt(i);
        }
        for (int i = activeEffects.Count - 1; i >= 0; i--) {
            activeEffects[i].root.gameObject.SetActive(false);
            effectPool.Push(activeEffects[i]);
            activeEffects.RemoveAt(i);
        }
    }

    private Zombie CreateZombie() {
        GameObject root = CreateLayeredGameObject("Zombie");
        root.transform.SetParent(transform, false);

        GameObject shadow = CreateLayeredGameObject("Shadow");
        shadow.transform.SetParent(root.transform, false);
        shadow.transform.localPosition = new Vector3(0.0f, -0.08f, 0.0f);
        shadow.transform.localScale = new Vector3(0.95f, 0.55f, 1.0f);
        SpriteRenderer shadowRenderer = shadow.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = shadowSprite;
        shadowRenderer.color = new Color(0.0f, 0.0f, 0.0f, 0.34f);
        shadowRenderer.sortingOrder = 2;

        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 6;
        return new Zombie { root = root.transform, renderer = renderer, shadowRenderer = shadowRenderer };
    }

    private Bullet CreateBullet() {
        GameObject root = CreateLayeredGameObject("Bullet");
        root.transform.SetParent(transform, false);

        GameObject trail = CreateLayeredGameObject("Trail");
        trail.transform.SetParent(root.transform, false);
        trail.transform.localPosition = new Vector3(-0.18f, 0.0f, 0.0f);
        trail.transform.localScale = new Vector3(0.45f, 0.08f, 1.0f);
        SpriteRenderer trailRenderer = trail.AddComponent<SpriteRenderer>();
        trailRenderer.sprite = solidSprite;
        trailRenderer.color = new Color(1.0f, 0.55f, 0.14f, 0.55f);
        trailRenderer.sortingOrder = 7;

        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = bulletSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = 8;
        root.transform.localScale = Vector3.one;
        return new Bullet { root = root.transform, renderer = renderer, trailRenderer = trailRenderer };
    }

    private Pickup CreatePickup() {
        GameObject root = CreateLayeredGameObject("Pickup");
        root.transform.SetParent(transform, false);

        GameObject glow = CreateLayeredGameObject("Glow");
        glow.transform.SetParent(root.transform, false);
        glow.transform.localScale = new Vector3(0.85f, 0.85f, 1.0f);
        SpriteRenderer glowRenderer = glow.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = pickupGlowSprite;
        glowRenderer.color = new Color(1.0f, 0.8f, 0.3f, 0.3f);
        glowRenderer.sortingOrder = 3;

        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 4;
        return new Pickup { root = root.transform, renderer = renderer, glowRenderer = glowRenderer };
    }

    private Splat CreateSplat() {
        GameObject root = CreateLayeredGameObject("Splat");
        root.transform.SetParent(transform, false);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = splatSprites[0];
        renderer.sortingOrder = 1;
        return new Splat { root = root.transform, renderer = renderer };
    }

    private VisualEffect CreateVisualEffect() {
        GameObject root = CreateLayeredGameObject("VisualEffect");
        root.transform.SetParent(transform, false);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = impactPuffSprite;
        renderer.sortingOrder = 9;
        root.SetActive(false);
        return new VisualEffect { root = root.transform, renderer = renderer };
    }

    private void ConfigureCamera() {
        mainCamera = ResolveOutputCamera();
        renderLayer = gameObject.layer;
        if (mainCamera == null) {
            cameraHomePosition = new Vector3(0.0f, 0.0f, -10.0f);
            worldHalfWidth = 6.67f;
            worldHalfHeight = 5.0f;
            return;
        }

        arcadeOutputMode = mainCamera.targetTexture != null;
        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 5.0f;
        if (mainCamera.transform.IsChildOf(transform)) {
            mainCamera.transform.localPosition = new Vector3(0.0f, 0.0f, -10.0f);
            mainCamera.transform.localRotation = Quaternion.identity;
        } else {
            mainCamera.transform.position = new Vector3(0.0f, 0.0f, -10.0f);
            mainCamera.transform.rotation = Quaternion.identity;
        }
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = groundColor;
        if (arcadeOutputMode) {
            int uiLayer = LayerMask.NameToLayer("UI");
            renderLayer = uiLayer >= 0 ? uiLayer : gameObject.layer;
            SetLayerRecursively(gameObject, renderLayer);
            mainCamera.cullingMask = 1 << renderLayer;
        } else if (mainCamera.cullingMask == 0) {
            mainCamera.cullingMask = -1;
        }
        cameraHomePosition = mainCamera.transform.position;
        worldHalfWidth = mainCamera.orthographicSize * mainCamera.aspect;
        worldHalfHeight = mainCamera.orthographicSize;
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

    private GameObject CreateLayeredGameObject(string name) {
        GameObject created = new GameObject(name);
        created.layer = renderLayer;
        return created;
    }

    private static void SetLayerRecursively(GameObject root, int layer) {
        root.layer = layer;
        Transform rootTransform = root.transform;
        for (int i = 0; i < rootTransform.childCount; i++) {
            SetLayerRecursively(rootTransform.GetChild(i).gameObject, layer);
        }
    }

    private void CreateSprites() {
        solidSprite = CreateSolidSprite();
        circleSprite = CreateCircleSprite(32, Color.white);
        shadowSprite = CreateShadowSprite();
        pickupGlowSprite = CreateGlowSprite();
        impactPuffSprite = CreatePuffSprite();
        bulletSprite = CreateBulletSprite();
        muzzleFlashSprite = CreateMuzzleFlashSprite();
        zombieSprites = new Sprite[] {
            CreateZombieSprite(ZombieType.Walker, WalkerColor),
            CreateZombieSprite(ZombieType.Runner, RunnerColor),
            CreateZombieSprite(ZombieType.Brute, BruteColor)
        };
        playerSprite = CreatePlayerSprite();
        splatSprites = new Sprite[] {
            CreateSplatSprite(),
            CreateSplatSprite(),
            CreateSplatSprite()
        };
        healthPickupSprite = CreateHealthPickupSprite();
        rapidPickupSprite = CreateRapidPickupSprite();
        heartFullTexture = CreateHeartTexture(true);
        heartEmptyTexture = CreateHeartTexture(false);
        hudPanelTexture = CreateSolidTexture(new Color(0.02f, 0.025f, 0.02f, 0.62f));
        overlayTexture = CreateSolidTexture(new Color(0.0f, 0.0f, 0.0f, 0.72f));
        rapidBarTexture = CreateSolidTexture(new Color(1.0f, 0.76f, 0.22f, 0.88f));
        rapidBarBackTexture = CreateSolidTexture(new Color(0.08f, 0.075f, 0.055f, 0.72f));
    }

    private static bool IsEllipse(float x, float y, float centerX, float centerY, float radiusX, float radiusY) {
        if (radiusX <= 0.0f || radiusY <= 0.0f) {
            return false;
        }

        float dx = (x - centerX) / radiusX;
        float dy = (y - centerY) / radiusY;
        return dx * dx + dy * dy <= 1.0f;
    }

    private static bool IsZombiePixel(int x, int y, ZombieType type, float inset) {
        float lean = type == ZombieType.Runner ? 0.82f : 1.0f;
        float bulk = type == ZombieType.Brute ? 1.18f : 1.0f;
        float bodyRadiusX = 21.0f * lean * bulk - inset;
        float bodyRadiusY = 23.0f * bulk - inset;
        float headRadiusX = 14.0f * bulk - inset;
        float headRadiusY = 16.0f * bulk - inset;
        float armRadiusX = 17.0f * lean * bulk - inset;
        float armRadiusY = (type == ZombieType.Brute ? 6.5f : 5.0f) * bulk - inset;

        return IsEllipse(x, y, 31.0f, 32.0f, bodyRadiusX, bodyRadiusY)
            || IsEllipse(x, y, 48.0f, 32.0f, headRadiusX, headRadiusY)
            || IsEllipse(x, y, 53.0f, 48.0f, armRadiusX, armRadiusY)
            || IsEllipse(x, y, 53.0f, 16.0f, armRadiusX, armRadiusY);
    }

    private static float Hash01(int x, int y, int seed) {
        unchecked {
            int hash = seed;
            hash ^= x * 374761393;
            hash = (hash << 13) | (hash >> 19);
            hash ^= y * 668265263;
            hash *= 1274126177;
            return (hash & 0x7fffffff) / 2147483647.0f;
        }
    }

    private static float DistanceToSegment(float px, float py, Vector4 segment) {
        Vector2 point = new Vector2(px, py);
        Vector2 a = new Vector2(segment.x, segment.y);
        Vector2 b = new Vector2(segment.z, segment.w);
        Vector2 ab = b - a;
        float t = Vector2.Dot(point - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude);
        t = Mathf.Clamp01(t);
        return Vector2.Distance(point, a + ab * t);
    }

    private static Color ScaleRgb(Color color, float scale) {
        return new Color(
            Mathf.Clamp01(color.r * scale),
            Mathf.Clamp01(color.g * scale),
            Mathf.Clamp01(color.b * scale),
            color.a
        );
    }

    private Vector2 RandomUnitVector() {
        float angle = (float)visualRng.NextDouble() * Mathf.PI * 2.0f;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    private static Texture2D CreateSolidTexture(Color color) {
        Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color32 c = color;
        Color32[] pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++) {
            pixels[i] = c;
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    private static Sprite CreateSolidSprite() {
        Texture2D texture = CreateSolidTexture(Color.white);
        return Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4.0f);
    }

    private static Sprite CreateCircleSprite(int size, Color color) {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        float half = size * 0.5f;
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dx = x - half + 0.5f;
                float dy = y - half + 0.5f;
                texture.SetPixel(x, y, dx * dx + dy * dy <= (half - 1.0f) * (half - 1.0f) ? color : clear);
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite CreateShadowSprite() {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dx = (x - 31.5f) / 25.0f;
                float dy = (y - 31.5f) / 13.0f;
                float d = dx * dx + dy * dy;
                float alpha = Mathf.Clamp01(1.0f - d) * 0.55f;
                texture.SetPixel(x, y, alpha > 0.0f ? new Color(0.0f, 0.0f, 0.0f, alpha) : clear);
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 80.0f);
    }

    private static Sprite CreateGlowSprite() {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        float half = size * 0.5f;
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dx = x - half + 0.5f;
                float dy = y - half + 0.5f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy) / half;
                float alpha = Mathf.Pow(Mathf.Clamp01(1.0f - distance), 1.8f);
                texture.SetPixel(x, y, alpha > 0.0f ? new Color(1.0f, 1.0f, 1.0f, alpha) : clear);
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 70.0f);
    }

    private static Sprite CreatePuffSprite() {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        Vector3[] blobs = {
            new Vector3(30.0f, 32.0f, 16.0f),
            new Vector3(42.0f, 35.0f, 10.0f),
            new Vector3(22.0f, 40.0f, 8.0f),
            new Vector3(35.0f, 21.0f, 9.0f)
        };

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float alpha = 0.0f;
                for (int b = 0; b < blobs.Length; b++) {
                    float dx = x - blobs[b].x;
                    float dy = y - blobs[b].y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / blobs[b].z;
                    alpha = Mathf.Max(alpha, Mathf.Clamp01(1.0f - d));
                }
                alpha *= 0.78f + Hash01(x, y, 41) * 0.22f;
                texture.SetPixel(x, y, alpha > 0.03f ? new Color(1.0f, 1.0f, 1.0f, alpha) : clear);
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 88.0f);
    }

    private static Sprite CreateBulletSprite() {
        const int size = 48;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        Color core = new Color(1.0f, 0.92f, 0.42f, 1.0f);
        Color hot = new Color(1.0f, 0.52f, 0.12f, 0.9f);

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                bool halo = IsEllipse(x, y, 24.0f, 24.0f, 14.0f, 8.0f);
                bool body = IsEllipse(x, y, 26.0f, 24.0f, 9.0f, 4.5f);
                bool nose = x >= 28 && x <= 38 && Mathf.Abs(y - 24.0f) <= 5.0f - (x - 28.0f) * 0.32f;
                Color pixel = clear;
                if (halo) {
                    pixel = new Color(hot.r, hot.g, hot.b, 0.35f);
                }
                if (body || nose) {
                    pixel = core;
                }
                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 130.0f);
    }

    private static Sprite CreateMuzzleFlashSprite() {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dx = x - 31.5f;
                float dy = y - 31.5f;
                float angle = Mathf.Atan2(dy, dx);
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float starRadius = 12.0f + Mathf.Abs(Mathf.Cos(angle * 4.0f)) * 15.0f;
                float alpha = Mathf.Clamp01(1.0f - distance / starRadius);
                Color pixel = clear;
                if (alpha > 0.0f) {
                    pixel = Color.Lerp(new Color(1.0f, 0.48f, 0.08f, 0.78f), new Color(1.0f, 0.96f, 0.62f, 1.0f), alpha);
                    pixel.a *= alpha;
                }
                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 88.0f);
    }

    private static Sprite CreateZombieSprite(ZombieType type, Color bodyColor) {
        const int size = 80;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        Color outline = Color.Lerp(bodyColor, Color.black, type == ZombieType.Brute ? 0.68f : 0.55f);
        Color shade = Color.Lerp(bodyColor, Color.black, 0.25f);
        Color highlight = Color.Lerp(bodyColor, Color.white, 0.24f);
        Color wound = new Color(0.25f, 0.035f, 0.035f, 1.0f);
        Color eye = type == ZombieType.Brute ? new Color(1.0f, 0.18f, 0.12f, 1.0f) : new Color(0.12f, 0.08f, 0.055f, 1.0f);

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                Color pixel = clear;
                bool shape = IsZombiePixel(x, y, type, 0.0f);
                if (shape) {
                    bool edge = !IsZombiePixel(x + 2, y, type, 0.0f)
                        || !IsZombiePixel(x - 2, y, type, 0.0f)
                        || !IsZombiePixel(x, y + 2, type, 0.0f)
                        || !IsZombiePixel(x, y - 2, type, 0.0f);
                    float light = Mathf.Clamp01(0.35f + (size - y) * 0.006f - x * 0.002f);
                    pixel = edge ? outline : Color.Lerp(shade, highlight, light);

                    if (!edge && Hash01(x, y, 17 + (int)type * 19) > 0.975f) {
                        pixel = Color.Lerp(pixel, wound, 0.62f);
                    }
                }

                if (IsEllipse(x, y, 57.0f, 40.0f, 3.2f, 3.8f)) {
                    pixel = eye;
                }
                if (IsEllipse(x, y, 57.0f, 24.0f, 3.2f, 3.8f)) {
                    pixel = eye;
                }
                if (x >= 58 && x <= 66 && y >= 30 && y <= 34 && shape) {
                    pixel = new Color(0.08f, 0.045f, 0.04f, 1.0f);
                }

                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 104.0f);
    }

    private static Sprite CreatePlayerSprite() {
        const int size = 80;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        Color jacket = new Color(0.22f, 0.42f, 0.78f, 1.0f);
        Color vest = new Color(0.13f, 0.18f, 0.26f, 1.0f);
        Color outline = Color.Lerp(jacket, Color.black, 0.62f);
        Color head = new Color(0.95f, 0.8f, 0.62f, 1.0f);
        Color hair = new Color(0.16f, 0.1f, 0.07f, 1.0f);

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                Color pixel = clear;
                bool body = IsEllipse(x, y, 39.0f, 39.0f, 27.0f, 26.0f);
                if (body) {
                    bool edge = !IsEllipse(x + 2, y, 39.0f, 39.0f, 27.0f, 26.0f)
                        || !IsEllipse(x - 2, y, 39.0f, 39.0f, 27.0f, 26.0f)
                        || !IsEllipse(x, y + 2, 39.0f, 39.0f, 27.0f, 26.0f)
                        || !IsEllipse(x, y - 2, 39.0f, 39.0f, 27.0f, 26.0f);
                    float light = Mathf.Clamp01(0.3f + (size - y) * 0.006f - x * 0.0015f);
                    pixel = edge ? outline : Color.Lerp(Color.Lerp(jacket, Color.black, 0.16f), Color.Lerp(jacket, Color.white, 0.22f), light);
                }
                if (IsEllipse(x, y, 27.0f, 39.0f, 10.0f, 18.0f)) {
                    pixel = Color.Lerp(vest, Color.black, 0.16f);
                }
                if (IsEllipse(x, y, 49.0f, 39.0f, 12.0f, 18.0f)) {
                    pixel = Color.Lerp(jacket, Color.white, 0.08f);
                }
                if (IsEllipse(x, y, 42.0f, 39.0f, 12.5f, 12.5f)) {
                    float faceLight = Mathf.Clamp01(0.55f + (size - y) * 0.004f);
                    pixel = Color.Lerp(Color.Lerp(head, Color.black, 0.15f), Color.Lerp(head, Color.white, 0.16f), faceLight);
                }
                if (IsEllipse(x, y, 38.0f, 39.0f, 6.5f, 12.0f) && x < 40) {
                    pixel = hair;
                }
                if (IsEllipse(x, y, 24.0f, 39.0f, 7.0f, 17.0f)) {
                    pixel = Color.Lerp(vest, Color.black, 0.28f);
                }
                if (x >= 48 && x <= 57 && y >= 36 && y <= 42) {
                    pixel = new Color(0.78f, 0.86f, 1.0f, 1.0f);
                }
                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 104.0f);
    }

    private Sprite CreateSplatSprite() {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        Color blood = new Color(0.34f, 0.035f, 0.035f, 1.0f);
        Color darkBlood = new Color(0.18f, 0.015f, 0.015f, 1.0f);
        Vector3[] blobs = new Vector3[7];
        blobs[0] = new Vector3(32.0f, 32.0f, 13.5f);
        for (int i = 1; i < blobs.Length; i++) {
            blobs[i] = new Vector3(
                12.0f + (float)visualRng.NextDouble() * 40.0f,
                12.0f + (float)visualRng.NextDouble() * 40.0f,
                3.5f + (float)visualRng.NextDouble() * 8.0f
            );
        }

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                Color pixel = clear;
                float strongest = 0.0f;
                for (int b = 0; b < blobs.Length; b++) {
                    float dx = x - blobs[b].x;
                    float dy = y - blobs[b].y;
                    float t = Mathf.Clamp01(1.0f - Mathf.Sqrt(dx * dx + dy * dy) / blobs[b].z);
                    strongest = Mathf.Max(strongest, t);
                }
                if (strongest > 0.0f) {
                    pixel = Color.Lerp(blood, darkBlood, strongest * 0.45f + Hash01(x, y, 83) * 0.22f);
                    pixel.a = Mathf.Clamp01(0.45f + strongest * 0.55f);
                }
                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 72.0f);
    }

    private static Sprite CreateHealthPickupSprite() {
        const int size = 48;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        Color box = new Color(0.88f, 0.92f, 0.9f, 1.0f);
        Color shadow = new Color(0.45f, 0.52f, 0.5f, 1.0f);
        Color outline = new Color(0.12f, 0.16f, 0.15f, 1.0f);
        Color cross = new Color(0.85f, 0.2f, 0.25f, 1.0f);

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                Color pixel = clear;
                bool outer = x >= 5 && x < size - 5 && y >= 7 && y < size - 7;
                bool inner = x >= 8 && x < size - 8 && y >= 10 && y < size - 10;
                if (outer) {
                    pixel = outline;
                }
                if (inner) {
                    float light = Mathf.Clamp01(0.5f + (size - y) * 0.008f);
                    pixel = Color.Lerp(shadow, box, light);
                    bool vertical = x >= 21 && x <= 26 && y >= 14 && y <= 34;
                    bool horizontal = y >= 21 && y <= 26 && x >= 14 && x <= 34;
                    if (vertical || horizontal) {
                        pixel = cross;
                    }
                }
                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 96.0f);
    }

    private static Sprite CreateRapidPickupSprite() {
        const int size = 48;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        Color bolt = new Color(1.0f, 0.85f, 0.2f, 1.0f);
        Color outline = new Color(0.18f, 0.12f, 0.02f, 1.0f);

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                Color pixel = clear;
                float dx = Mathf.Abs(x - 23.5f);
                float dy = Mathf.Abs(y - 23.5f);
                if (dx + dy <= 19.0f) {
                    pixel = new Color(0.24f, 0.2f, 0.1f, 0.72f);
                }

                bool boltBody = (x >= 24 && x <= 34 && y >= 7 && y <= 24)
                    || (x >= 13 && x <= 26 && y >= 21 && y <= 28)
                    || (x >= 15 && x <= 24 && y >= 25 && y <= 41);
                bool cutA = x + y < 32;
                bool cutB = x + y > 64;
                bool boltOuter = boltBody && !cutA && !cutB;
                bool boltInner = boltOuter && x > 15 && x < 32 && y > 9 && y < 39;
                if (boltOuter) {
                    pixel = outline;
                }
                if (boltInner) {
                    pixel = Color.Lerp(bolt, Color.white, Mathf.Clamp01((float)(40 - y) / 48.0f) * 0.22f);
                }

                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 96.0f);
    }

    private static Texture2D CreateHeartTexture(bool full) {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        Color fill = full ? new Color(0.88f, 0.13f, 0.2f, 0.98f) : new Color(0.16f, 0.16f, 0.16f, 0.82f);
        Color outline = full ? new Color(0.22f, 0.03f, 0.04f, 1.0f) : new Color(0.06f, 0.06f, 0.06f, 0.92f);
        Color shine = full ? new Color(1.0f, 0.52f, 0.58f, 0.98f) : new Color(0.26f, 0.26f, 0.26f, 0.82f);

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                bool left = IsEllipse(x, y, 10.0f, 21.0f, 7.5f, 7.0f);
                bool right = IsEllipse(x, y, 21.0f, 21.0f, 7.5f, 7.0f);
                bool point = y <= 21 && y >= 4 && Mathf.Abs(x - 15.5f) <= (y - 3.0f) * 0.72f;
                bool heart = left || right || point;
                bool inner = IsEllipse(x, y, 10.0f, 21.0f, 5.5f, 5.0f)
                    || IsEllipse(x, y, 21.0f, 21.0f, 5.5f, 5.0f)
                    || (y <= 20 && y >= 6 && Mathf.Abs(x - 15.5f) <= (y - 5.0f) * 0.6f);

                Color pixel = clear;
                if (heart) {
                    pixel = inner ? fill : outline;
                }
                if (full && IsEllipse(x, y, 10.0f, 22.0f, 2.0f, 2.0f)) {
                    pixel = shine;
                }

                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        return texture;
    }

    private void CreateGround() {
        if (backgroundTexture != null) {
            CreateGroundObject(backgroundTexture);
            return;
        }

        const int width = 320;
        const int height = 192;
        const int tileSize = 16;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color dirtColor = new Color(0.18f, 0.15f, 0.11f, 1.0f);
        Color stainColor = new Color(0.08f, 0.09f, 0.07f, 1.0f);
        Color crackColor = new Color(0.045f, 0.052f, 0.043f, 1.0f);
        Vector3[] stains = new Vector3[18];
        Vector4[] cracks = new Vector4[9];

        for (int i = 0; i < stains.Length; i++) {
            stains[i] = new Vector3(
                (float)visualRng.NextDouble() * width,
                (float)visualRng.NextDouble() * height,
                10.0f + (float)visualRng.NextDouble() * 28.0f
            );
        }

        for (int i = 0; i < cracks.Length; i++) {
            float startX = (float)visualRng.NextDouble() * width;
            float startY = (float)visualRng.NextDouble() * height;
            float length = 35.0f + (float)visualRng.NextDouble() * 70.0f;
            float angle = (float)visualRng.NextDouble() * Mathf.PI * 2.0f;
            cracks[i] = new Vector4(startX, startY, startX + Mathf.Cos(angle) * length, startY + Mathf.Sin(angle) * length);
        }

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                int tileX = x / tileSize;
                int tileY = y / tileSize;
                bool dark = ((tileX + tileY) & 1) == 0;
                float tileNoise = Hash01(tileX, tileY, 211);
                Color color = dark ? groundCheckerColor : groundColor;
                color = Color.Lerp(color, dirtColor, tileNoise * 0.24f);

                float grain = Hash01(x, y, 313);
                color = ScaleRgb(color, 0.88f + grain * 0.22f);

                for (int i = 0; i < stains.Length; i++) {
                    float dx = x - stains[i].x;
                    float dy = y - stains[i].y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / stains[i].z;
                    if (d < 1.0f) {
                        color = Color.Lerp(color, stainColor, (1.0f - d) * 0.42f);
                    }
                }

                for (int i = 0; i < cracks.Length; i++) {
                    float distance = DistanceToSegment(x, y, cracks[i]);
                    if (distance < 1.35f) {
                        color = Color.Lerp(color, crackColor, 0.78f - distance * 0.35f);
                    }
                }

                float debris = Hash01(x, y, 719);
                if (debris > 0.993f) {
                    color = Color.Lerp(color, debris > 0.997f ? new Color(0.26f, 0.24f, 0.19f, 1.0f) : crackColor, 0.65f);
                }

                float nx = x / (float)(width - 1);
                float ny = y / (float)(height - 1);
                float edge = Mathf.Max(Mathf.Abs(nx - 0.5f) * 2.0f, Mathf.Abs(ny - 0.5f) * 2.0f);
                float vignette = Mathf.Pow(Mathf.Clamp01(edge), 1.8f) * Mathf.Clamp01(vignetteStrength);
                color = Color.Lerp(color, Color.black, vignette * 0.46f);
                texture.SetPixel(x, y, color);
            }
        }
        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;

        CreateGroundObject(texture);
    }

    private void CreateGroundObject(Texture2D texture) {
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), GroundPixelsPerUnit);
        GameObject ground = CreateLayeredGameObject("Ground");
        ground.transform.SetParent(transform, false);
        ground.transform.localScale = new Vector3(
            worldHalfWidth * 2.0f / Mathf.Max(0.01f, texture.width / GroundPixelsPerUnit),
            worldHalfHeight * 2.0f / Mathf.Max(0.01f, texture.height / GroundPixelsPerUnit),
            1.0f
        );
        SpriteRenderer renderer = ground.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = -10;
    }

    private void CreatePlayer() {
        GameObject player = CreateLayeredGameObject("Survivor");
        player.transform.SetParent(transform, false);

        GameObject shadow = CreateLayeredGameObject("Shadow");
        shadow.transform.SetParent(player.transform, false);
        shadow.transform.localPosition = new Vector3(0.0f, -0.08f, 0.0f);
        shadow.transform.localScale = new Vector3(1.0f, 0.58f, 1.0f);
        playerShadowRenderer = shadow.AddComponent<SpriteRenderer>();
        playerShadowRenderer.sprite = shadowSprite;
        playerShadowRenderer.color = new Color(0.0f, 0.0f, 0.0f, 0.36f);
        playerShadowRenderer.sortingOrder = 2;

        playerRenderer = player.AddComponent<SpriteRenderer>();
        playerRenderer.sprite = playerSprite;
        playerRenderer.sortingOrder = 10;
        playerTransform = player.transform;

        GameObject pivot = CreateLayeredGameObject("GunPivot");
        pivot.transform.SetParent(player.transform, false);
        gunPivot = pivot.transform;

        GameObject barrel = CreateLayeredGameObject("Barrel");
        barrel.transform.SetParent(pivot.transform, false);
        barrel.transform.localPosition = new Vector3(0.46f, 0.0f, 0.0f);
        barrel.transform.localScale = new Vector3(0.52f, 0.11f, 1.0f);
        SpriteRenderer barrelRenderer = barrel.AddComponent<SpriteRenderer>();
        barrelRenderer.sprite = solidSprite;
        barrelRenderer.color = new Color(0.08f, 0.085f, 0.095f, 1.0f);
        barrelRenderer.sortingOrder = 11;

        GameObject barrelHighlight = CreateLayeredGameObject("BarrelHighlight");
        barrelHighlight.transform.SetParent(pivot.transform, false);
        barrelHighlight.transform.localPosition = new Vector3(0.48f, 0.035f, 0.0f);
        barrelHighlight.transform.localScale = new Vector3(0.42f, 0.025f, 1.0f);
        SpriteRenderer barrelHighlightRenderer = barrelHighlight.AddComponent<SpriteRenderer>();
        barrelHighlightRenderer.sprite = solidSprite;
        barrelHighlightRenderer.color = new Color(0.33f, 0.34f, 0.38f, 1.0f);
        barrelHighlightRenderer.sortingOrder = 12;

        GameObject flash = CreateLayeredGameObject("MuzzleFlash");
        flash.transform.SetParent(pivot.transform, false);
        flash.transform.localPosition = new Vector3(0.78f, 0.0f, 0.0f);
        flash.transform.localScale = new Vector3(0.48f, 0.48f, 1.0f);
        muzzleFlashRenderer = flash.AddComponent<SpriteRenderer>();
        muzzleFlashRenderer.sprite = muzzleFlashSprite;
        muzzleFlashRenderer.color = new Color(1.0f, 0.95f, 0.6f, 0.95f);
        muzzleFlashRenderer.sortingOrder = 13;
        muzzleFlashRenderer.enabled = false;
    }

    private void CreateArcadeHud() {
        if (!showHud || !arcadeOutputMode) {
            return;
        }

        GameObject hudRoot = CreateLayeredGameObject("Arcade HUD");
        hudRoot.transform.SetParent(transform, false);
        arcadeScoreText = CreateHudText(hudRoot.transform, "Score", new Vector3(0.0f, 4.55f, -0.1f), ArcadeHudScoreSize, FontStyle.Bold);
        arcadeStatusText = CreateHudText(hudRoot.transform, "Status", new Vector3(0.0f, 4.0f, -0.1f), ArcadeHudBodySize, FontStyle.Bold);
        arcadeMessageText = CreateHudText(hudRoot.transform, "Message", new Vector3(0.0f, 1.0f, -0.1f), ArcadeHudTitleSize, FontStyle.Bold);
        arcadeSmallText = CreateHudText(hudRoot.transform, "Small Message", new Vector3(0.0f, -3.05f, -0.1f), ArcadeHudBodySize, FontStyle.Bold);
        arcadePromptText = CreateHudText(hudRoot.transform, "Prompt", new Vector3(0.0f, -3.55f, -0.1f), ArcadeHudBodySize, FontStyle.Normal);
        CreateReadyLogo(hudRoot.transform);
    }

    private void CreateReadyLogo(Transform parent) {
        if (readyLogoTexture == null) {
            return;
        }

        Sprite logoSprite = Sprite.Create(readyLogoTexture, new Rect(0, 0, readyLogoTexture.width, readyLogoTexture.height), new Vector2(0.5f, 0.5f), 100.0f);
        GameObject logoObject = CreateLayeredGameObject("Ready Logo");
        logoObject.transform.SetParent(parent, false);
        logoObject.transform.localPosition = new Vector3(0.0f, 1.0f, -0.1f);

        float logoWidth = Mathf.Max(0.1f, readyLogoWidth);
        float spriteWorldWidth = Mathf.Max(0.01f, logoSprite.bounds.size.x);
        float logoScale = logoWidth / spriteWorldWidth;
        logoObject.transform.localScale = new Vector3(logoScale, logoScale, 1.0f);

        readyLogoRenderer = logoObject.AddComponent<SpriteRenderer>();
        readyLogoRenderer.sprite = logoSprite;
        readyLogoRenderer.sortingOrder = 100;
        readyLogoRenderer.enabled = false;
    }

    private void SetHudTextPosition(HudText hudText, Vector3 localPosition) {
        if (hudText == null) {
            return;
        }

        hudText.foreground.transform.localPosition = localPosition;
        hudText.shadow.transform.localPosition = localPosition + new Vector3(ArcadeHudShadowOffset, -ArcadeHudShadowOffset, 0.0f);
    }

    private HudText CreateHudText(Transform parent, string name, Vector3 localPosition, float characterSize, FontStyle fontStyle) {
        Vector3 shadowPosition = localPosition + new Vector3(ArcadeHudShadowOffset, -ArcadeHudShadowOffset, 0.0f);
        TextMesh shadow = CreateHudTextMesh(parent, name + " Shadow", shadowPosition, characterSize, fontStyle, new Color(0.0f, 0.0f, 0.0f, 0.7f), 99);
        TextMesh foreground = CreateHudTextMesh(parent, name, localPosition, characterSize, fontStyle, Color.white, 100);
        return new HudText { foreground = foreground, shadow = shadow };
    }

    private TextMesh CreateHudTextMesh(Transform parent, string name, Vector3 localPosition, float characterSize, FontStyle fontStyle, Color color, int sortingOrder) {
        GameObject textObject = CreateLayeredGameObject(name);
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

    private void UpdateHud() {
        if (!arcadeOutputMode || arcadeScoreText == null) {
            return;
        }

        if (state == GameState.Ready) {
            SetHudText(arcadeScoreText, "");
            SetHudText(arcadeStatusText, "");
            SetReadyLogoVisible(readyLogoRenderer != null);
            SetHudTextPosition(arcadeSmallText, new Vector3(0.0f, -3.05f, -0.1f));
            SetHudTextPosition(arcadePromptText, new Vector3(0.0f, -3.55f, -0.1f));
            SetHudText(arcadeMessageText, readyLogoRenderer == null ? "SURVIVE THE NIGHTS" : "");
            SetHudText(arcadeSmallText, "BEST: " + bestScore);
            SetHudText(arcadePromptText, "SPACE / ENTER TO START");
            return;
        }

        SetReadyLogoVisible(false);
        SetHudText(arcadeScoreText, "SCORE " + score + "   WAVE " + wave);
        string status = "HP " + playerHealth + "/" + playerMaxHealth;
        if (rapidFireTimer > 0.0f) {
            status += "   RAPID " + rapidFireTimer.ToString("0.0");
        }
        SetHudText(arcadeStatusText, status);

        if (state == GameState.GameOver) {
            SetHudTextPosition(arcadeSmallText, new Vector3(0.0f, 0.25f, -0.1f));
            SetHudTextPosition(arcadePromptText, new Vector3(0.0f, -0.32f, -0.1f));
            SetHudText(arcadeMessageText, "YOU DIED");
            SetHudText(arcadeSmallText, "SCORE: " + score + "   BEST: " + bestScore);
            SetHudText(arcadePromptText, "SPACE TO RETRY");
        } else if (waveBannerTimer > 0.0f && Mathf.Repeat(waveBannerTimer, 0.5f) < 0.32f) {
            SetHudText(arcadeMessageText, "WAVE " + wave);
            SetHudText(arcadeSmallText, "");
            SetHudText(arcadePromptText, "");
        } else {
            SetHudText(arcadeMessageText, "");
            SetHudText(arcadeSmallText, "");
            SetHudText(arcadePromptText, "");
        }
    }

    private void SetReadyLogoVisible(bool visible) {
        if (readyLogoRenderer != null) {
            readyLogoRenderer.enabled = visible;
        }
    }

    private void OnGUI() {
        if (!showHud || arcadeOutputMode || !showStandaloneGui) {
            return;
        }

        EnsureGuiStyles();

        if (state != GameState.Ready) {
            float hudHeight = Mathf.Max(56.0f, Screen.height * 0.075f);
            DrawGuiTexture(new Rect(0.0f, 0.0f, Screen.width, hudHeight), hudPanelTexture, 1.0f);

            DrawOutlinedLabel(new Rect(Screen.width * 0.03f, Screen.height * 0.018f, Screen.width * 0.4f, 36.0f),
                "SCORE " + score, smallStyle, TextAnchor.MiddleLeft);
            DrawOutlinedLabel(new Rect(0.0f, Screen.height * 0.018f, Screen.width, 36.0f),
                "WAVE " + wave, smallStyle, TextAnchor.MiddleCenter);

            float heartSize = Mathf.Clamp(Screen.height * 0.034f, 24.0f, 38.0f);
            float heartSpacing = heartSize * 1.25f;
            float heartsX = Screen.width * 0.97f - playerMaxHealth * heartSpacing;
            for (int i = 0; i < playerMaxHealth; i++) {
                Rect heartRect = new Rect(heartsX + i * heartSpacing, Screen.height * 0.022f, heartSize, heartSize);
                GUI.DrawTexture(heartRect, i < playerHealth ? heartFullTexture : heartEmptyTexture);
            }

            if (rapidFireTimer > 0.0f) {
                float barWidth = Mathf.Clamp(Screen.width * 0.22f, 170.0f, 260.0f);
                Rect barBack = new Rect(Screen.width * 0.5f - barWidth * 0.5f, hudHeight + 8.0f, barWidth, 18.0f);
                DrawGuiTexture(barBack, rapidBarBackTexture, 1.0f);
                DrawGuiTexture(new Rect(barBack.x, barBack.y, barBack.width * Mathf.Clamp01(rapidFireTimer / rapidFireDuration), barBack.height), rapidBarTexture, 1.0f);
                DrawOutlinedLabel(new Rect(0.0f, hudHeight + 3.0f, Screen.width, 28.0f),
                    "RAPID FIRE " + rapidFireTimer.ToString("0.0"), smallStyle, TextAnchor.MiddleCenter);
            }
        }

        if (state == GameState.Ready) {
            DrawGuiTexture(new Rect(0.0f, 0.0f, Screen.width, Screen.height), overlayTexture, 0.65f);
            DrawOutlinedLabel(new Rect(0.0f, Screen.height * 0.28f, Screen.width, 60.0f), "SURVIVE THE NIGHTS", messageStyle);
            DrawOutlinedLabel(new Rect(0.0f, Screen.height * 0.38f, Screen.width, 40.0f), "WASD to move, mouse to aim, hold to fire", smallStyle);
            DrawOutlinedLabel(new Rect(0.0f, Screen.height * 0.44f, Screen.width, 40.0f), "Survive the waves. Grab medkits and ammo bolts.", smallStyle);
            DrawOutlinedLabel(new Rect(0.0f, Screen.height * 0.5f, Screen.width, 40.0f), "Best: " + bestScore + " - click to start", smallStyle);
        } else if (state == GameState.GameOver) {
            DrawGuiTexture(new Rect(0.0f, 0.0f, Screen.width, Screen.height), overlayTexture, 0.72f);
            DrawOutlinedLabel(new Rect(0.0f, Screen.height * 0.32f, Screen.width, 60.0f), "YOU DIED", messageStyle);
            DrawOutlinedLabel(new Rect(0.0f, Screen.height * 0.42f, Screen.width, 40.0f),
                "Score: " + score + "   Wave: " + wave + "   Best: " + bestScore, smallStyle);
            DrawOutlinedLabel(new Rect(0.0f, Screen.height * 0.48f, Screen.width, 40.0f), "Press Space to try again", smallStyle);
        } else if (waveBannerTimer > 0.0f && Mathf.Repeat(waveBannerTimer, 0.5f) < 0.32f) {
            DrawOutlinedLabel(new Rect(0.0f, Screen.height * 0.3f, Screen.width, 50.0f), "WAVE " + wave, messageStyle);
        }
    }

    private void EnsureGuiStyles() {
        if (messageStyle != null) {
            return;
        }

        messageStyle = new GUIStyle(GUI.skin.label) {
            fontSize = Mathf.RoundToInt(Screen.height * 0.05f),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        smallStyle = new GUIStyle(messageStyle) {
            fontSize = Mathf.RoundToInt(Screen.height * 0.026f)
        };
    }

    private void DrawOutlinedLabel(Rect rect, string text, GUIStyle style) {
        DrawOutlinedLabel(rect, text, style, TextAnchor.MiddleCenter);
    }

    private static void DrawGuiTexture(Rect rect, Texture texture, float alpha) {
        Color previous = GUI.color;
        GUI.color = new Color(1.0f, 1.0f, 1.0f, alpha);
        GUI.DrawTexture(rect, texture);
        GUI.color = previous;
    }

    private void DrawOutlinedLabel(Rect rect, string text, GUIStyle style, TextAnchor anchor) {
        TextAnchor previousAnchor = style.alignment;
        style.alignment = anchor;
        guiContent.text = text;
        Color previous = GUI.color;
        GUI.color = new Color(0.0f, 0.0f, 0.0f, 0.8f);
        GUI.Label(new Rect(rect.x + 2.0f, rect.y + 2.0f, rect.width, rect.height), guiContent, style);
        GUI.color = Color.white;
        GUI.Label(rect, guiContent, style);
        GUI.color = previous;
        style.alignment = previousAnchor;
    }
}
