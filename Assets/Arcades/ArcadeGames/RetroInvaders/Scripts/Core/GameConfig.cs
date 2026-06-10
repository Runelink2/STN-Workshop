using UnityEngine;
using UnityEngine.Serialization;

namespace RetroInvaders {
    public enum InvaderMovementMode {
        Smooth,
        Stepped
    }

    [CreateAssetMenu(menuName = "Retro Invaders/Game Config", fileName = "RetroInvadersConfig")]
    public sealed class GameConfig : ScriptableObject {
        [Header("Playfield")]
        [SerializeField] private Vector2 playfieldCenter = Vector2.zero;
        [SerializeField] private Vector2 playfieldSize = new Vector2(16.0f, 10.0f);

        [Header("Visual Grid")]
        [SerializeField] private Vector2 spritePixelSize = new Vector2(0.07f, 0.06f);

        [Header("Player")]
        [SerializeField] private Vector2 playerHalfExtents = new Vector2(0.45f, 0.25f);
        [FormerlySerializedAs("playerMoveSpeed")]
        [SerializeField] private float playerSpeed = 7.0f;
        [SerializeField] private float playerStartY = -4.25f;
        [SerializeField] private float playerHorizontalPadding = 0.2f;
        [SerializeField] private float playerDeathDelay = 1.15f;
        [SerializeField] private float playerRespawnInvulnerableSeconds = 1.5f;
        [SerializeField] private float playerRespawnBlinkRate = 7.0f;
        [FormerlySerializedAs("playerLives")]
        [SerializeField] private int startingLives = 3;

        [Header("Projectiles")]
        [SerializeField] private float playerProjectileSpeed = 12.0f;
        [FormerlySerializedAs("enemyProjectileSpeed")]
        [SerializeField] private float invaderProjectileSpeed = 5.5f;
        [SerializeField] private int invaderProjectileDamage = 1;
        [SerializeField] private float projectileLifetime = 4.0f;
        [SerializeField] private float playerFireCooldown = 0.28f;
        [FormerlySerializedAs("projectileDamage")]
        [SerializeField] private int playerProjectileDamage = 1;
        [SerializeField] private int projectilePoolInitialSize = 24;
        [SerializeField] private int projectilePoolMaxSize = 96;
        [SerializeField] private Vector3 projectileVisualScale = new Vector3(0.18f, 0.48f, 0.18f);

        [Header("Invaders")]
        [FormerlySerializedAs("enemyRows")]
        [SerializeField] private int invaderRows = 5;
        [FormerlySerializedAs("enemyColumns")]
        [SerializeField] private int invaderColumns = 11;
        [SerializeField] private float invaderSpacingX = 1.05f;
        [SerializeField] private float invaderSpacingY = 0.65f;
        [SerializeField] private float invaderStartY = 3.25f;
        [FormerlySerializedAs("enemyBaseSpeed")]
        [SerializeField] private float invaderBaseSpeed = 0.85f;
        [SerializeField] private float invaderMaxSpeedMultiplier = 3.5f;
        [SerializeField] private InvaderMovementMode invaderMovementMode = InvaderMovementMode.Stepped;
        [SerializeField] private int invaderStepPixels = 1;
        [SerializeField] private float invaderStepRateScale = 0.5f;
        [SerializeField, HideInInspector] private float invaderStepDistance = 0.24f;
        [SerializeField] private float invaderStepIntervalMin = 0.055f;
        [SerializeField] private float invaderStepIntervalMax = 0.48f;
        [SerializeField] private float invaderEdgePadding = 0.45f;
        [FormerlySerializedAs("enemyDescentStep")]
        [SerializeField] private float invaderDescentStep = 0.35f;
        [SerializeField] private int invaderHealth = 1;
        [SerializeField] private int invaderScoreBase = 10;
        [SerializeField] private float waveClearDelay = 1.25f;
        [FormerlySerializedAs("minimumEnemyFireInterval")]
        [SerializeField] private float invaderFireIntervalMin = 0.45f;
        [FormerlySerializedAs("enemyFireInterval")]
        [SerializeField] private float invaderFireIntervalMax = 1.4f;
        [SerializeField] private float invaderFireIntervalWaveScale = 0.92f;
        [SerializeField] private float invaderFireIntervalLowCountScale = 0.5f;

        [Header("Shields")]
        [SerializeField] private int shieldCount = 4;
        [SerializeField] private Vector2 shieldBlockSize = new Vector2(0.07f, 0.06f);
        [FormerlySerializedAs("shieldBlockHitPoints")]
        [SerializeField] private int shieldBlocksHealth = 1;
        [SerializeField] private float shieldY = -2.95f;
        [SerializeField] private int shieldWidthPattern = 24;
        [SerializeField] private float shieldCoverageFraction = 0.64f;
        [SerializeField] private bool shieldResetEveryWave = true;

        [Header("UFO")]
        [SerializeField] private float ufoSpeed = 3.8f;
        [SerializeField] private float ufoSpawnIntervalMin = 12.0f;
        [FormerlySerializedAs("ufoSpawnInterval")]
        [SerializeField] private float ufoSpawnIntervalMax = 22.0f;
        [SerializeField] private int[] ufoScoreValues = { 100, 150, 300 };
        [SerializeField] private float ufoY = 4.15f;

        [Header("Score")]
        [SerializeField] private int bottomEnemyScore = 10;
        [SerializeField] private int middleEnemyScore = 20;
        [SerializeField] private int topEnemyScore = 30;
        [SerializeField] private float scorePopupDuration = 0.85f;
        [SerializeField] private float scorePopupRiseDistance = 54.0f;

        [Header("Feedback")]
        [SerializeField] private float sfxMasterVolume = 1.0f;
        [SerializeField] private float sfxVolume = 1.0f;
        [SerializeField] private float muzzleFlashDuration = 0.08f;
        [SerializeField] private float explosionDuration = 0.22f;
        [SerializeField] private float screenShakeDuration = 0.12f;
        [SerializeField] private float screenShakeMagnitude = 0.08f;

        [Header("Difficulty")]
        [SerializeField] private float waveSpeedMultiplier = 1.12f;
        [SerializeField] private float invaderDangerY = -4.05f;

        public Vector2 PlayfieldCenter => playfieldCenter;
        public Vector2 PlayfieldSize => playfieldSize;
        public Vector2 SpritePixelSize => spritePixelSize;
        public Vector2 PlayerSpawnPosition => new Vector2(playfieldCenter.x, playerStartY);
        public Vector2 PlayerHalfExtents => playerHalfExtents;
        public float PlayerSpeed => playerSpeed;
        public float PlayerStartY => playerStartY;
        public float PlayerHorizontalPadding => playerHorizontalPadding;
        public float PlayerDeathDelay => playerDeathDelay;
        public float PlayerRespawnInvulnerableSeconds => playerRespawnInvulnerableSeconds;
        public float PlayerRespawnBlinkRate => playerRespawnBlinkRate;
        public int StartingLives => startingLives;
        public int PlayerLives => startingLives;
        public float PlayerProjectileSpeed => playerProjectileSpeed;
        public float InvaderProjectileSpeed => invaderProjectileSpeed;
        public float EnemyProjectileSpeed => invaderProjectileSpeed;
        public int InvaderProjectileDamage => invaderProjectileDamage;
        public float ProjectileLifetime => projectileLifetime;
        public float PlayerFireCooldown => playerFireCooldown;
        public int PlayerProjectileDamage => playerProjectileDamage;
        public int ProjectileDamage => playerProjectileDamage;
        public int ProjectilePoolInitialSize => projectilePoolInitialSize;
        public int ProjectilePoolMaxSize => projectilePoolMaxSize;
        public Vector3 ProjectileVisualScale => projectileVisualScale;
        public int InvaderRows => invaderRows;
        public int InvaderColumns => invaderColumns;
        public float InvaderSpacingX => invaderSpacingX;
        public float InvaderSpacingY => invaderSpacingY;
        public Vector2 InvaderSpacing => new Vector2(invaderSpacingX, invaderSpacingY);
        public float InvaderStartY => invaderStartY;
        public float InvaderBaseSpeed => invaderBaseSpeed;
        public float InvaderMaxSpeedMultiplier => invaderMaxSpeedMultiplier;
        public InvaderMovementMode InvaderMovementMode => invaderMovementMode;
        public int InvaderStepPixels => invaderStepPixels;
        public float InvaderStepRateScale => invaderStepRateScale;
        public float InvaderStepDistance => Mathf.Max(0.02f, spritePixelSize.x) * Mathf.Max(1, invaderStepPixels);
        public float InvaderStepIntervalMin => invaderStepIntervalMin;
        public float InvaderStepIntervalMax => invaderStepIntervalMax;
        public float InvaderEdgePadding => invaderEdgePadding;
        public float InvaderDescentStep => invaderDescentStep;
        public int InvaderHealth => invaderHealth;
        public int InvaderScoreBase => invaderScoreBase;
        public float WaveClearDelay => waveClearDelay;
        public float InvaderFireIntervalMin => invaderFireIntervalMin;
        public float InvaderFireIntervalMax => invaderFireIntervalMax;
        public float InvaderFireIntervalWaveScale => invaderFireIntervalWaveScale;
        public float InvaderFireIntervalLowCountScale => invaderFireIntervalLowCountScale;
        public int EnemyRows => invaderRows;
        public int EnemyColumns => invaderColumns;
        public Vector2 EnemySpacing => InvaderSpacing;
        public float EnemyBaseSpeed => invaderBaseSpeed;
        public float EnemyDescentStep => invaderDescentStep;
        public float EnemyFireInterval => invaderFireIntervalMax;
        public int ShieldCount => shieldCount;
        public Vector2 ShieldBlockSize => shieldBlockSize;
        public int ShieldBlocksHealth => shieldBlocksHealth;
        public int ShieldBlockHitPoints => shieldBlocksHealth;
        public float ShieldY => shieldY;
        public int ShieldWidthPattern => shieldWidthPattern;
        public float ShieldCoverageFraction => shieldCoverageFraction;
        public bool ShieldResetEveryWave => shieldResetEveryWave;
        public float UfoSpeed => ufoSpeed;
        public float UfoSpawnIntervalMin => ufoSpawnIntervalMin;
        public float UfoSpawnIntervalMax => ufoSpawnIntervalMax;
        public float UfoSpawnInterval => ufoSpawnIntervalMax;
        public int[] UfoScoreValues => ufoScoreValues;
        public int UfoScore => ufoScoreValues != null && ufoScoreValues.Length > 0 ? ufoScoreValues[0] : 150;
        public float UfoY => ufoY;
        public int BottomEnemyScore => bottomEnemyScore;
        public int MiddleEnemyScore => middleEnemyScore;
        public int TopEnemyScore => topEnemyScore;
        public float ScorePopupDuration => scorePopupDuration;
        public float ScorePopupRiseDistance => scorePopupRiseDistance;
        public float SfxMasterVolume => sfxMasterVolume;
        public float SfxVolume => sfxVolume;
        public float MuzzleFlashDuration => muzzleFlashDuration;
        public float ExplosionDuration => explosionDuration;
        public float ScreenShakeDuration => screenShakeDuration;
        public float ScreenShakeMagnitude => screenShakeMagnitude;
        public float WaveSpeedMultiplier => waveSpeedMultiplier;
        public float InvaderDangerY => invaderDangerY;

        private void OnValidate() {
            playfieldSize.x = Mathf.Max(4.0f, playfieldSize.x);
            playfieldSize.y = Mathf.Max(4.0f, playfieldSize.y);
            spritePixelSize.x = Mathf.Max(0.02f, spritePixelSize.x);
            spritePixelSize.y = Mathf.Max(0.02f, spritePixelSize.y);
            playerHalfExtents.x = Mathf.Max(0.05f, playerHalfExtents.x);
            playerHalfExtents.y = Mathf.Max(0.05f, playerHalfExtents.y);
            playerSpeed = Mathf.Max(0.1f, playerSpeed);
            playerStartY = Mathf.Clamp(playerStartY, playfieldCenter.y - playfieldSize.y * 0.5f, playfieldCenter.y + playfieldSize.y * 0.5f);
            playerHorizontalPadding = Mathf.Max(0.0f, playerHorizontalPadding);
            playerDeathDelay = Mathf.Max(0.1f, playerDeathDelay);
            playerRespawnInvulnerableSeconds = Mathf.Max(0.0f, playerRespawnInvulnerableSeconds);
            playerRespawnBlinkRate = Mathf.Clamp(playerRespawnBlinkRate, 2.0f, 12.0f);
            startingLives = Mathf.Max(1, startingLives);
            playerProjectileSpeed = Mathf.Max(0.1f, playerProjectileSpeed);
            invaderProjectileSpeed = Mathf.Max(0.1f, invaderProjectileSpeed);
            invaderProjectileDamage = Mathf.Max(1, invaderProjectileDamage);
            projectileLifetime = Mathf.Max(0.1f, projectileLifetime);
            playerFireCooldown = Mathf.Max(0.02f, playerFireCooldown);
            playerProjectileDamage = Mathf.Max(1, playerProjectileDamage);
            projectilePoolInitialSize = Mathf.Max(0, projectilePoolInitialSize);
            projectilePoolMaxSize = Mathf.Max(projectilePoolInitialSize, projectilePoolMaxSize);
            projectileVisualScale.x = Mathf.Max(0.02f, projectileVisualScale.x);
            projectileVisualScale.y = Mathf.Max(0.02f, projectileVisualScale.y);
            projectileVisualScale.z = Mathf.Max(0.02f, projectileVisualScale.z);
            invaderRows = Mathf.Clamp(invaderRows, 1, 8);
            invaderColumns = Mathf.Clamp(invaderColumns, 1, 16);
            invaderSpacingX = Mathf.Max(0.1f, invaderSpacingX);
            invaderSpacingY = Mathf.Max(0.1f, invaderSpacingY);
            invaderStartY = Mathf.Clamp(invaderStartY, playfieldCenter.y - playfieldSize.y * 0.5f, playfieldCenter.y + playfieldSize.y * 0.5f);
            invaderBaseSpeed = Mathf.Max(0.05f, invaderBaseSpeed);
            invaderMaxSpeedMultiplier = Mathf.Max(1.0f, invaderMaxSpeedMultiplier);
            invaderStepPixels = Mathf.Clamp(invaderStepPixels, 1, 8);
            invaderStepRateScale = Mathf.Clamp(invaderStepRateScale, 0.1f, 2.0f);
            invaderStepDistance = Mathf.Max(0.02f, invaderStepDistance);
            invaderStepIntervalMin = Mathf.Max(0.02f, invaderStepIntervalMin);
            invaderStepIntervalMax = Mathf.Max(invaderStepIntervalMin, invaderStepIntervalMax);
            invaderEdgePadding = Mathf.Max(0.0f, invaderEdgePadding);
            invaderDescentStep = Mathf.Max(0.01f, invaderDescentStep);
            invaderHealth = Mathf.Max(1, invaderHealth);
            invaderScoreBase = Mathf.Max(0, invaderScoreBase);
            waveClearDelay = Mathf.Max(0.1f, waveClearDelay);
            invaderFireIntervalMin = Mathf.Max(0.05f, invaderFireIntervalMin);
            invaderFireIntervalMax = Mathf.Max(invaderFireIntervalMin, invaderFireIntervalMax);
            invaderFireIntervalWaveScale = Mathf.Clamp(invaderFireIntervalWaveScale, 0.25f, 1.0f);
            invaderFireIntervalLowCountScale = Mathf.Clamp(invaderFireIntervalLowCountScale, 0.1f, 1.0f);
            shieldCount = Mathf.Clamp(shieldCount, 0, 6);
            shieldBlockSize.x = Mathf.Max(0.02f, shieldBlockSize.x);
            shieldBlockSize.y = Mathf.Max(0.02f, shieldBlockSize.y);
            shieldBlocksHealth = Mathf.Max(1, shieldBlocksHealth);
            shieldY = Mathf.Clamp(shieldY, playfieldCenter.y - playfieldSize.y * 0.5f, playfieldCenter.y + playfieldSize.y * 0.5f);
            shieldWidthPattern = Mathf.Clamp(shieldWidthPattern, 16, 24);
            shieldCoverageFraction = Mathf.Clamp(shieldCoverageFraction, 0.4f, 0.9f);
            ufoSpeed = Mathf.Max(0.1f, ufoSpeed);
            ufoSpawnIntervalMin = Mathf.Max(1.0f, ufoSpawnIntervalMin);
            ufoSpawnIntervalMax = Mathf.Max(ufoSpawnIntervalMin, ufoSpawnIntervalMax);
            ufoY = Mathf.Clamp(ufoY, playfieldCenter.y - playfieldSize.y * 0.5f, playfieldCenter.y + playfieldSize.y * 0.5f);
            if (ufoScoreValues == null || ufoScoreValues.Length == 0) {
                ufoScoreValues = new[] { 150 };
            }

            for (int i = 0; i < ufoScoreValues.Length; i++) {
                ufoScoreValues[i] = Mathf.Max(0, ufoScoreValues[i]);
            }

            bottomEnemyScore = Mathf.Max(0, bottomEnemyScore);
            middleEnemyScore = Mathf.Max(0, middleEnemyScore);
            topEnemyScore = Mathf.Max(0, topEnemyScore);
            scorePopupDuration = Mathf.Max(0.05f, scorePopupDuration);
            scorePopupRiseDistance = Mathf.Max(0.0f, scorePopupRiseDistance);
            sfxMasterVolume = Mathf.Clamp01(sfxMasterVolume);
            sfxVolume = Mathf.Clamp01(sfxVolume);
            muzzleFlashDuration = Mathf.Max(0.01f, muzzleFlashDuration);
            explosionDuration = Mathf.Max(0.01f, explosionDuration);
            screenShakeDuration = Mathf.Max(0.0f, screenShakeDuration);
            screenShakeMagnitude = Mathf.Max(0.0f, screenShakeMagnitude);
            waveSpeedMultiplier = Mathf.Max(1.0f, waveSpeedMultiplier);
            invaderDangerY = Mathf.Clamp(invaderDangerY, playfieldCenter.y - playfieldSize.y * 0.5f, playfieldCenter.y + playfieldSize.y * 0.5f);
        }
    }
}
