using UnityEngine;

namespace RetroInvaders {
    public sealed class UfoController : MonoBehaviour {
        [SerializeField] private GameSession session;
        [SerializeField] private GameConfig config;
        [SerializeField] private PlayArea playArea;
        [SerializeField] private UfoShip ufoPrefab;

        private UfoShip activeUfo;
        private float spawnTimer;

        public bool HasActiveUfo => activeUfo != null && activeUfo.IsActive;

        public void Configure(GameSession gameSession, GameConfig gameConfig, PlayArea area, UfoShip prefab) {
            session = gameSession;
            config = gameConfig;
            playArea = area;
            ufoPrefab = prefab;
            ResetController();
        }

        public void ResetController() {
            ClearUfo();
            spawnTimer = GetNextSpawnInterval();
        }

        public void ClearUfo() {
            if (activeUfo != null) {
                activeUfo.Despawn();
            }
        }

        public void NotifyUfoExited(UfoShip ufo) {
            if (ufo != activeUfo) {
                return;
            }

            ufo.Despawn();
            spawnTimer = GetNextSpawnInterval();
        }

        public void NotifyUfoKilled(UfoShip ufo, int scoreValue, Vector3 deathPosition) {
            if (ufo != activeUfo) {
                return;
            }

            session?.AddScore(scoreValue, deathPosition);
            session?.NotifyUfoDestroyed(deathPosition);
            spawnTimer = GetNextSpawnInterval();
        }

        private void Update() {
            if (session == null
                || config == null
                || playArea == null
                || ufoPrefab == null
                || session.CurrentState != GameSession.GameState.Playing) {
                return;
            }

            if (HasActiveUfo) {
                return;
            }

            spawnTimer -= Time.deltaTime;

            if (spawnTimer <= 0.0f) {
                SpawnUfo();
            }
        }

        private void SpawnUfo() {
            if (HasActiveUfo) {
                return;
            }

            if (activeUfo == null) {
                activeUfo = Instantiate(ufoPrefab, transform);
                activeUfo.name = "UFO Ship";
                activeUfo.gameObject.SetActive(false);
            }

            int direction = Random.value < 0.5f ? 1 : -1;
            float x = direction > 0
                ? playArea.Left - 1.0f
                : playArea.Right + 1.0f;
            Vector3 startPosition = new Vector3(x, config.UfoY, 0.0f);
            activeUfo.Launch(this, session, playArea, startPosition, direction, config.UfoSpeed, GetScoreValue());
        }

        private int GetScoreValue() {
            int[] values = config.UfoScoreValues;
            if (values == null || values.Length == 0) {
                return config.UfoScore;
            }

            return Mathf.Max(0, values[Random.Range(0, values.Length)]);
        }

        private float GetNextSpawnInterval() {
            if (config == null) {
                return 12.0f;
            }

            return Random.Range(config.UfoSpawnIntervalMin, config.UfoSpawnIntervalMax);
        }
    }
}
