using System;
using UnityEngine;

namespace RetroInvaders {
    public sealed class GameSession : MonoBehaviour {
        private const string HighScoreKey = "RetroInvaders.HighScore";

        public enum GameState {
            Title,
            Playing,
            Paused,
            WaveClear,
            PlayerDead,
            GameOver
        }

        [SerializeField] private GameConfig config;
        [SerializeField] private PlayArea playArea;
        [SerializeField] private PlayerShip playerPrefab;
        [SerializeField] private Transform playerRoot;
        [SerializeField] private ProjectilePool projectilePool;
        [SerializeField] private InvaderFleetController invaderFleet;
        [SerializeField] private ShieldController shieldController;
        [SerializeField] private UfoController ufoController;
        [SerializeField] private ScorePopupController scorePopupController;
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private VfxSpawner vfxSpawner;
        [SerializeField] private ScreenShake screenShake;
        [SerializeField] private GameState currentState = GameState.Title;
        [SerializeField] private bool logStateChanges = true;

        private int score;
        private int highScore;
        private int lives;
        private int wave = 1;
        private float waveClearTimer;
        private float playerDeathTimer;
        private PlayerShip currentPlayer;

        public event Action<GameSession> OnSessionChanged;
        public event Action<GameState> OnStateChanged;
        public event Action<int, int> OnScoreChanged;

        public GameConfig Config => config;
        public PlayArea PlayArea => playArea;
        public PlayerShip CurrentPlayer => currentPlayer;
        public ProjectilePool ProjectilePool => projectilePool;
        public InvaderFleetController InvaderFleet => invaderFleet;
        public ShieldController ShieldController => shieldController;
        public UfoController UfoController => ufoController;
        public AudioManager AudioManager => audioManager;
        public VfxSpawner VfxSpawner => vfxSpawner;
        public GameState CurrentState => currentState;
        public int Score => score;
        public int HighScore => highScore;
        public int Lives => lives;
        public int Wave => wave;

        public void Configure(GameConfig gameConfig, PlayArea area) {
            config = gameConfig;
            playArea = area;
        }

        public void Configure(GameConfig gameConfig, PlayArea area, PlayerShip player, Transform playerParent) {
            Configure(gameConfig, area, player, playerParent, projectilePool, invaderFleet);
        }

        public void Configure(GameConfig gameConfig, PlayArea area, PlayerShip player, Transform playerParent, ProjectilePool pool) {
            Configure(gameConfig, area, player, playerParent, pool, invaderFleet);
        }

        public void Configure(
            GameConfig gameConfig,
            PlayArea area,
            PlayerShip player,
            Transform playerParent,
            ProjectilePool pool,
            InvaderFleetController fleet
        ) {
            Configure(gameConfig, area, player, playerParent, pool, fleet, shieldController, ufoController);
        }

        public void Configure(
            GameConfig gameConfig,
            PlayArea area,
            PlayerShip player,
            Transform playerParent,
            ProjectilePool pool,
            InvaderFleetController fleet,
            ShieldController shields
        ) {
            Configure(gameConfig, area, player, playerParent, pool, fleet, shields, ufoController);
        }

        public void Configure(
            GameConfig gameConfig,
            PlayArea area,
            PlayerShip player,
            Transform playerParent,
            ProjectilePool pool,
            InvaderFleetController fleet,
            ShieldController shields,
            UfoController ufo
        ) {
            config = gameConfig;
            playArea = area;
            playerPrefab = player;
            playerRoot = playerParent;
            projectilePool = pool;
            invaderFleet = fleet;
            shieldController = shields;
            ufoController = ufo;
        }

        public void SetScorePopupController(ScorePopupController popupController) {
            scorePopupController = popupController;
        }

        public void SetUfoController(UfoController controller) {
            ufoController = controller;
        }

        public void SetFeedbackSystems(AudioManager audio, VfxSpawner vfx, ScreenShake shake) {
            audioManager = audio;
            vfxSpawner = vfx;
            screenShake = shake;
        }

        public void StartNewGame() {
            Time.timeScale = 1.0f;
            waveClearTimer = 0.0f;
            playerDeathTimer = 0.0f;
            DespawnProjectiles();
            ClearInvaders();
            ResetShields();
            ResetUfo();
            ClearScorePopups();
            ClearVfx();
            score = 0;
            lives = GetStartingLives();
            wave = 1;
            SpawnOrResetPlayer(0.0f);
            SpawnCurrentWave();
            OnScoreChanged?.Invoke(score, highScore);
            SetStateInternal(GameState.Playing, false);
            NotifySessionChanged();
        }

        public void AddScore(int amount) {
            AddScoreInternal(amount, false, Vector3.zero);
        }

        public void AddScore(int amount, Vector3 gamePosition) {
            AddScoreInternal(amount, true, gamePosition);
        }

        private void AddScoreInternal(int amount, bool showPopup, Vector3 gamePosition) {
            if (amount <= 0) {
                return;
            }

            score += amount;

            if (score > highScore) {
                highScore = score;
                PlayerPrefs.SetInt(HighScoreKey, highScore);
                PlayerPrefs.Save();
            }

            OnScoreChanged?.Invoke(score, highScore);

            if (showPopup && scorePopupController != null) {
                scorePopupController.ShowScore(amount, ToWorldPosition(gamePosition));
            }

            NotifySessionChanged();
        }

        public void LoseLife() {
            HandlePlayerLifeLost();
        }

        public void SetPaused(bool paused) {
            if (paused && currentState == GameState.Playing) {
                SetStateInternal(GameState.Paused);
            } else if (!paused && currentState == GameState.Paused) {
                SetStateInternal(GameState.Playing);
            }
        }

        public void GameOver() {
            Time.timeScale = 1.0f;
            waveClearTimer = 0.0f;
            playerDeathTimer = 0.0f;
            SaveHighScoreIfNeeded();
            HidePlayer();
            DespawnProjectiles();
            ClearInvaders();
            ClearShields();
            ClearUfo();
            ClearScorePopups();
            audioManager?.PlayGameOver();
            SetStateInternal(GameState.GameOver);
        }

        public void ReturnToTitle() {
            Time.timeScale = 1.0f;
            waveClearTimer = 0.0f;
            playerDeathTimer = 0.0f;
            score = 0;
            lives = GetStartingLives();
            wave = 1;
            HidePlayer();
            DespawnProjectiles();
            ClearInvaders();
            ClearShields();
            ClearUfo();
            ClearScorePopups();
            ClearVfx();
            OnScoreChanged?.Invoke(score, highScore);
            SetStateInternal(GameState.Title, false);
            NotifySessionChanged();
        }

        public void Restart() {
            StartNewGame();
        }

        public void SetState(GameState nextState) {
            SetStateInternal(nextState);
        }

        public void NotifyWaveCleared() {
            if (currentState != GameState.Playing) {
                return;
            }

            DespawnProjectiles();
            ResetUfo();
            waveClearTimer = GetWaveClearDelay();
            audioManager?.PlayWaveClear();
            SetStateInternal(GameState.WaveClear);
        }

        public void NotifyPlayerKilled(PlayerShip player, HitInfo hit) {
            if (currentState != GameState.Playing || player != currentPlayer) {
                return;
            }

            HandlePlayerLifeLost();
        }

        public void NotifyBaseInvaded() {
            if (currentState != GameState.Playing) {
                return;
            }

            GameOver();
        }

        public void NotifyPlayerFired(Vector3 position) {
            audioManager?.PlayPlayerShoot();
            vfxSpawner?.SpawnPlayerMuzzleFlash(position);
        }

        public void NotifyInvaderFired(Vector3 position) {
            audioManager?.PlayInvaderShoot();
            vfxSpawner?.SpawnInvaderMuzzleFlash(position);
        }

        public void NotifyFleetStepped() {
            audioManager?.PlayFleetStep();
        }

        public void NotifyInvaderDestroyed(Vector3 position) {
            audioManager?.PlayInvaderKilled();
            vfxSpawner?.SpawnInvaderExplosion(position);
        }

        public void NotifyUfoDestroyed(Vector3 position) {
            audioManager?.PlayUfoKilled();
            vfxSpawner?.SpawnUfoExplosion(position);
            screenShake?.Shake();
        }

        private void Awake() {
            highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
            lives = GetStartingLives();
            HidePlayer();
        }

        private void Start() {
            NotifySessionChanged();
            OnStateChanged?.Invoke(currentState);

            if (logStateChanges) {
                Debug.Log("RetroInvaders ready. State: " + currentState, this);
            }
        }

        private void Update() {
            if (Input.GetKeyDown(KeyCode.Return)) {
                HandleEnterPressed();
            }

            if (Input.GetKeyDown(KeyCode.P)) {
                TogglePause();
            }

            if (Input.GetKeyDown(KeyCode.Escape)) {
                HandleEscapePressed();
            }

            UpdatePlayerDeadTimer();
            UpdateWaveClearTimer();
        }

        private void HandleEnterPressed() {
            if (currentState == GameState.Title) {
                StartNewGame();
            } else if (currentState == GameState.GameOver) {
                Restart();
            }
        }

        private void HandleEscapePressed() {
            if (currentState == GameState.Playing || currentState == GameState.Paused) {
                TogglePause();
            } else if (currentState == GameState.GameOver || currentState == GameState.Title) {
                ReturnToTitle();
            }
        }

        private void TogglePause() {
            if (currentState == GameState.Playing) {
                SetPaused(true);
            } else if (currentState == GameState.Paused) {
                SetPaused(false);
            }
        }

        private void SetStateInternal(GameState nextState, bool notifySession = true) {
            if (currentState == nextState) {
                if (notifySession) {
                    NotifySessionChanged();
                }

                return;
            }

            currentState = nextState;
            ApplyStateSideEffects(currentState);
            OnStateChanged?.Invoke(currentState);

            if (logStateChanges) {
                Debug.Log("RetroInvaders state: " + currentState, this);
            }

            if (notifySession) {
                NotifySessionChanged();
            }
        }

        private void NotifySessionChanged() {
            OnSessionChanged?.Invoke(this);
        }

        private static void ApplyStateSideEffects(GameState state) {
            Time.timeScale = state == GameState.Paused ? 0.0f : 1.0f;
        }

        private void UpdatePlayerDeadTimer() {
            if (currentState != GameState.PlayerDead) {
                return;
            }

            playerDeathTimer -= Time.deltaTime;

            if (playerDeathTimer <= 0.0f) {
                RespawnPlayer();
            }
        }

        private void UpdateWaveClearTimer() {
            if (currentState != GameState.WaveClear) {
                return;
            }

            waveClearTimer -= Time.deltaTime;

            if (waveClearTimer <= 0.0f) {
                BeginNextWave();
            }
        }

        private void BeginNextWave() {
            wave++;
            waveClearTimer = 0.0f;
            DespawnProjectiles();

            if (config == null || config.ShieldResetEveryWave) {
                ResetShields();
            }

            SpawnOrResetPlayer(0.0f);
            SpawnCurrentWave();
            SetStateInternal(GameState.Playing, false);
            NotifySessionChanged();
        }

        private void RespawnPlayer() {
            if (lives <= 0) {
                GameOver();
                return;
            }

            playerDeathTimer = 0.0f;
            DespawnProjectiles();
            SpawnOrResetPlayer(GetRespawnInvulnerableSeconds());
            SetStateInternal(GameState.Playing, false);
            NotifySessionChanged();
        }

        private void SpawnCurrentWave() {
            if (invaderFleet != null) {
                invaderFleet.SpawnWave(wave);
            }
        }

        private void SpawnOrResetPlayer(float invulnerableSeconds) {
            if (config == null || playArea == null || playerPrefab == null) {
                return;
            }

            if (currentPlayer == null) {
                currentPlayer = Instantiate(playerPrefab, playerRoot);
                currentPlayer.name = "Player Ship";
            }

            currentPlayer.transform.position = ToWorldPosition(GetPlayerSpawnPosition());
            currentPlayer.transform.rotation = Quaternion.identity;
            currentPlayer.Configure(this, config, playArea, projectilePool);
            currentPlayer.gameObject.SetActive(true);
            currentPlayer.PrepareForSpawn(invulnerableSeconds);
        }

        private void HidePlayer() {
            if (currentPlayer != null) {
                currentPlayer.gameObject.SetActive(false);
            }
        }

        private void DespawnProjectiles() {
            if (projectilePool != null) {
                projectilePool.DespawnAll();
            }
        }

        private void DespawnInvaderProjectiles() {
            if (projectilePool != null) {
                projectilePool.DespawnTeam(Team.Invader);
            }
        }

        private void ClearInvaders() {
            if (invaderFleet != null) {
                invaderFleet.ClearWave();
            }
        }

        private void ResetShields() {
            if (shieldController != null) {
                shieldController.ResetShields();
            }
        }

        private void ClearShields() {
            if (shieldController != null) {
                shieldController.ClearShields();
            }
        }

        private void ResetUfo() {
            if (ufoController != null) {
                ufoController.ResetController();
            }
        }

        private void ClearUfo() {
            if (ufoController != null) {
                ufoController.ClearUfo();
            }
        }

        private void ClearScorePopups() {
            if (scorePopupController != null) {
                scorePopupController.Clear();
            }
        }

        private void ClearVfx() {
            if (vfxSpawner != null) {
                vfxSpawner.Clear();
            }
        }

        private Vector3 GetPlayerSpawnPosition() {
            Vector2 spawn = config != null ? config.PlayerSpawnPosition : Vector2.zero;
            return new Vector3(spawn.x, spawn.y, 0.0f);
        }

        private Vector3 ToGamePosition(Vector3 worldPosition) {
            return playArea != null ? playArea.WorldToGame(worldPosition) : worldPosition;
        }

        private Vector3 ToWorldPosition(Vector3 gamePosition) {
            return playArea != null ? playArea.GameToWorld(gamePosition) : gamePosition;
        }

        private int GetStartingLives() {
            return config != null ? config.StartingLives : 3;
        }

        private float GetWaveClearDelay() {
            return config != null ? config.WaveClearDelay : 1.25f;
        }

        private float GetPlayerDeathDelay() {
            return config != null ? config.PlayerDeathDelay : 1.15f;
        }

        private float GetRespawnInvulnerableSeconds() {
            return config != null ? config.PlayerRespawnInvulnerableSeconds : 1.5f;
        }

        private void HandlePlayerLifeLost() {
            if (currentState != GameState.Playing) {
                return;
            }

            lives = Mathf.Max(0, lives - 1);
            Vector3 hitPosition = currentPlayer != null ? ToGamePosition(currentPlayer.transform.position) : GetPlayerSpawnPosition();

            if (lives == 0) {
                audioManager?.PlayPlayerDeath();
            } else {
                audioManager?.PlayPlayerHit();
            }

            vfxSpawner?.SpawnPlayerHit(hitPosition);
            screenShake?.Shake();
            HidePlayer();
            DespawnInvaderProjectiles();

            if (lives == 0) {
                GameOver();
                return;
            }

            playerDeathTimer = GetPlayerDeathDelay();
            SetStateInternal(GameState.PlayerDead, false);
            NotifySessionChanged();
        }

        private void SaveHighScoreIfNeeded() {
            if (score <= highScore) {
                return;
            }

            highScore = score;
            PlayerPrefs.SetInt(HighScoreKey, highScore);
            PlayerPrefs.Save();
            OnScoreChanged?.Invoke(score, highScore);
        }
    }
}
