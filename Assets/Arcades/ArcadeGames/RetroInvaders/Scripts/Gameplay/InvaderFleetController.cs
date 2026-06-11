using System.Collections.Generic;
using UnityEngine;

namespace RetroInvaders {
    public sealed class InvaderFleetController : MonoBehaviour {
        private const int MaxStepCatchUpPerFrame = 8;

        [SerializeField] private GameSession session;
        [SerializeField] private GameConfig config;
        [SerializeField] private PlayArea playArea;
        [SerializeField] private Invader invaderPrefab;
        [SerializeField] private Invader[] invaderPrefabs;
        [SerializeField] private ProjectilePool projectilePool;

        private readonly List<Invader> invaders = new List<Invader>(128);
        private int activeCount;
        private int totalInWave;
        private int currentRows;
        private int currentColumns;
        private int direction = 1;
        private int currentWave = 1;
        private float fireTimer;
        private float stepTimer;
        private int stepIndex;

        public int ActiveCount => activeCount;
        public bool IsSteppedMovement => config != null && config.InvaderMovementMode == InvaderMovementMode.Stepped;

        public void Configure(GameSession gameSession, GameConfig gameConfig, PlayArea area, Invader prefab) {
            Configure(gameSession, gameConfig, area, prefab, projectilePool);
        }

        public void Configure(GameSession gameSession, GameConfig gameConfig, PlayArea area, Invader prefab, ProjectilePool pool) {
            Configure(gameSession, gameConfig, area, prefab, null, pool);
        }

        public void Configure(GameSession gameSession, GameConfig gameConfig, PlayArea area, Invader[] prefabs, ProjectilePool pool) {
            Invader fallbackPrefab = prefabs != null && prefabs.Length > 0 ? prefabs[0] : invaderPrefab;
            Configure(gameSession, gameConfig, area, fallbackPrefab, prefabs, pool);
        }

        private void Configure(GameSession gameSession, GameConfig gameConfig, PlayArea area, Invader prefab, Invader[] prefabs, ProjectilePool pool) {
            session = gameSession;
            config = gameConfig;
            playArea = area;
            invaderPrefab = prefab;
            invaderPrefabs = prefabs;
            projectilePool = pool;
        }

        public void SpawnWave(int wave) {
            if (config == null || playArea == null || invaderPrefab == null) {
                return;
            }

            currentWave = Mathf.Max(1, wave);
            direction = 1;
            stepIndex = 0;
            transform.position = playArea.GameToWorld(Vector3.zero);

            int rows = config.InvaderRows;
            int columns = config.InvaderColumns;
            currentRows = rows;
            currentColumns = columns;
            totalInWave = rows * columns;
            activeCount = totalInWave;
            fireTimer = GetNextFireInterval();
            stepTimer = GetCurrentStepInterval();
            EnsureInvaderCapacity(totalInWave, rows, columns);
            if (invaders.Count < totalInWave) {
                ClearWave();
                return;
            }

            float totalWidth = (columns - 1) * config.InvaderSpacingX;
            float startX = config.PlayfieldCenter.x - totalWidth * 0.5f;
            int index = 0;

            for (int row = 0; row < rows; row++) {
                for (int column = 0; column < columns; column++) {
                    Invader invader = invaders[index];
                    invader.gameObject.SetActive(false);
                    invader.transform.SetParent(transform, false);
                    invader.name = "Invader_R" + row.ToString("00") + "_C" + column.ToString("00");
                    invader.transform.localPosition = new Vector3(
                        startX + column * config.InvaderSpacingX,
                        config.InvaderStartY - row * config.InvaderSpacingY,
                        0.0f
                    );
                    invader.transform.localRotation = Quaternion.identity;
                    invader.transform.localScale = Vector3.one;
                    invader.Configure(this, GetScoreForRow(row, rows), config.InvaderHealth, row, column);
                    invader.SetSteppedAnimation(IsSteppedMovement);
                    invader.gameObject.SetActive(true);
                    index++;
                }
            }

            for (int i = totalInWave; i < invaders.Count; i++) {
                if (invaders[i] != null) {
                    invaders[i].gameObject.SetActive(false);
                }
            }
        }

        public void ClearWave() {
            activeCount = 0;
            totalInWave = 0;
            currentRows = 0;
            currentColumns = 0;
            fireTimer = 0.0f;
            stepTimer = 0.0f;
            stepIndex = 0;

            for (int i = 0; i < invaders.Count; i++) {
                if (invaders[i] != null) {
                    invaders[i].gameObject.SetActive(false);
                }
            }
        }

        public void NotifyInvaderKilled(Invader invader, HitInfo hit) {
            if (invader == null || activeCount <= 0) {
                return;
            }

            activeCount = Mathf.Max(0, activeCount - 1);

            if (session != null) {
                Vector3 gamePosition = playArea != null ? playArea.WorldToGame(invader.transform.position) : invader.transform.position;
                session.NotifyInvaderDestroyed(gamePosition);
                session.AddScore(invader.ScoreValue, gamePosition);

                if (activeCount == 0) {
                    session.NotifyWaveCleared();
                }
            }
        }

        private void Update() {
            if (session == null
                || config == null
                || playArea == null
                || activeCount <= 0
                || session.CurrentState != GameSession.GameState.Playing) {
                return;
            }

            MoveFleet();

            if (session.CurrentState != GameSession.GameState.Playing || activeCount <= 0) {
                return;
            }

            UpdateShooting();
        }

        private void MoveFleet() {
            if (IsSteppedMovement) {
                MoveFleetStepped();
                return;
            }

            MoveFleetSmooth();
        }

        private void MoveFleetSmooth() {
            float speed = GetCurrentSpeed();
            Vector3 position = playArea.WorldToGame(transform.position);
            position.x += direction * speed * Time.deltaTime;
            transform.position = playArea.GameToWorld(position);

            float minX;
            float maxX;
            if (!TryGetActiveHorizontalBounds(out minX, out maxX)) {
                return;
            }

            float leftLimit = playArea.Left + config.InvaderEdgePadding;
            float rightLimit = playArea.Right - config.InvaderEdgePadding;

            if (direction > 0 && maxX >= rightLimit) {
                CorrectAndDescend(rightLimit - maxX);
            } else if (direction < 0 && minX <= leftLimit) {
                CorrectAndDescend(leftLimit - minX);
            }

            CheckDangerLine();
        }

        private void MoveFleetStepped() {
            stepTimer -= Time.deltaTime;

            int stepsThisFrame = 0;
            while (stepTimer <= 0.0f && stepsThisFrame < MaxStepCatchUpPerFrame) {
                if (!StepFleet()) {
                    stepTimer = GetCurrentStepInterval();
                    return;
                }

                stepsThisFrame++;
                stepTimer += GetCurrentStepInterval();
                CheckDangerLine();

                if (session == null
                    || session.CurrentState != GameSession.GameState.Playing
                    || activeCount <= 0) {
                    return;
                }
            }

            if (stepsThisFrame >= MaxStepCatchUpPerFrame && stepTimer <= 0.0f) {
                stepTimer = 0.0f;
            }
        }

        private bool StepFleet() {
            float minX;
            float maxX;
            if (!TryGetActiveHorizontalBounds(out minX, out maxX)) {
                return false;
            }

            float leftLimit = playArea.Left + config.InvaderEdgePadding;
            float rightLimit = playArea.Right - config.InvaderEdgePadding;
            float stepDistance = GetCurrentStepDistance();
            float xStep = stepDistance * direction;
            bool hitRight = direction > 0 && maxX + xStep >= rightLimit;
            bool hitLeft = direction < 0 && minX + xStep <= leftLimit;

            if (hitRight || hitLeft) {
                DescendAtEdge();
            } else {
                Vector3 position = playArea.WorldToGame(transform.position);
                position.x += xStep;
                transform.position = playArea.GameToWorld(position);
                NotifyStepAdvanced();
            }

            return true;
        }

        private void CorrectAndDescend(float xCorrection) {
            Vector3 position = playArea.WorldToGame(transform.position);
            position.x += xCorrection;
            position.y -= config.InvaderDescentStep;
            transform.position = playArea.GameToWorld(position);
            direction *= -1;
            NotifyStepAdvanced();
        }

        private void DescendAtEdge() {
            Vector3 position = playArea.WorldToGame(transform.position);
            position.y -= config.InvaderDescentStep;
            transform.position = playArea.GameToWorld(position);
            direction *= -1;
            NotifyStepAdvanced();
        }

        private void NotifyStepAdvanced() {
            stepIndex++;
            ApplyStepPoseToInvaders();
            session?.NotifyFleetStepped();
        }

        private void ApplyStepPoseToInvaders() {
            if (!IsSteppedMovement) {
                return;
            }

            for (int i = 0; i < invaders.Count; i++) {
                Invader invader = invaders[i];
                if (invader != null && invader.IsAlive) {
                    invader.SetStepPose(stepIndex);
                }
            }
        }

        private bool TryGetActiveHorizontalBounds(out float minX, out float maxX) {
            minX = float.MaxValue;
            maxX = float.MinValue;
            bool found = false;

            for (int i = 0; i < invaders.Count; i++) {
                Invader invader = invaders[i];
                if (invader == null || !invader.IsAlive) {
                    continue;
                }

                Vector3 position = playArea.WorldToGame(invader.transform.position);
                Vector2 halfExtents = invader.BoundsHalfExtents;
                minX = Mathf.Min(minX, position.x - halfExtents.x);
                maxX = Mathf.Max(maxX, position.x + halfExtents.x);
                found = true;
            }

            return found;
        }

        private void CheckDangerLine() {
            float lowestY;
            if (TryGetLowestActiveY(out lowestY) && lowestY <= config.InvaderDangerY) {
                session?.NotifyBaseInvaded();
            }
        }

        private bool TryGetLowestActiveY(out float lowestY) {
            lowestY = float.MaxValue;
            bool found = false;

            for (int i = 0; i < invaders.Count; i++) {
                Invader invader = invaders[i];
                if (invader == null || !invader.IsAlive) {
                    continue;
                }

                Vector3 position = playArea.WorldToGame(invader.transform.position);
                lowestY = Mathf.Min(lowestY, position.y - invader.BoundsHalfExtents.y);
                found = true;
            }

            return found;
        }

        private float GetCurrentSpeed() {
            if (totalInWave <= 0) {
                return config.InvaderBaseSpeed;
            }

            float killedFraction = 1.0f - (activeCount / (float)totalInWave);
            float waveMultiplier = Mathf.Pow(config.WaveSpeedMultiplier, currentWave - 1);
            float countMultiplier = Mathf.Lerp(1.0f, config.InvaderMaxSpeedMultiplier, killedFraction);
            return config.InvaderBaseSpeed * waveMultiplier * countMultiplier;
        }

        private float GetCurrentStepInterval() {
            if (config == null) {
                return 0.25f;
            }

            float speed = Mathf.Max(0.01f, GetCurrentSpeed() * config.InvaderStepRateScale);
            float interval = GetCurrentStepDistance() / speed;
            return Mathf.Clamp(interval, config.InvaderStepIntervalMin, config.InvaderStepIntervalMax);
        }

        private float GetCurrentStepDistance() {
            return Mathf.Max(0.02f, config.InvaderStepDistance);
        }

        private void UpdateShooting() {
            if (projectilePool == null) {
                return;
            }

            fireTimer -= Time.deltaTime;

            if (fireTimer > 0.0f) {
                return;
            }

            TryFire();
            fireTimer = GetNextFireInterval();
        }

        private void TryFire() {
            Invader shooter;
            if (!TrySelectShooter(out shooter)) {
                return;
            }

            Vector3 origin = playArea.WorldToGame(shooter.transform.position);
            origin.y -= shooter.BoundsHalfExtents.y + 0.12f;
            origin.z = 0.0f;
            Projectile projectile = projectilePool.Spawn(origin, Vector3.down, config.InvaderProjectileSpeed, config.InvaderProjectileDamage, Team.Invader);

            if (projectile != null) {
                session?.NotifyInvaderFired(origin);
            }
        }

        private bool TrySelectShooter(out Invader shooter) {
            shooter = null;

            if (currentRows <= 0 || currentColumns <= 0) {
                return false;
            }

            int startColumn = Random.Range(0, currentColumns);

            for (int offset = 0; offset < currentColumns; offset++) {
                int column = (startColumn + offset) % currentColumns;

                for (int row = currentRows - 1; row >= 0; row--) {
                    int index = row * currentColumns + column;
                    if (index < 0 || index >= invaders.Count) {
                        continue;
                    }

                    Invader candidate = invaders[index];
                    if (candidate != null && candidate.IsAlive) {
                        shooter = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        private float GetNextFireInterval() {
            if (config == null) {
                return 1.0f;
            }

            float killedFraction = totalInWave > 0 ? 1.0f - (activeCount / (float)totalInWave) : 0.0f;
            float waveScale = Mathf.Pow(config.InvaderFireIntervalWaveScale, currentWave - 1);
            float countScale = Mathf.Lerp(1.0f, config.InvaderFireIntervalLowCountScale, killedFraction);
            float interval = Random.Range(config.InvaderFireIntervalMin, config.InvaderFireIntervalMax);
            return Mathf.Max(0.08f, interval * waveScale * countScale);
        }

        private int GetScoreForRow(int row, int rowCount) {
            return Mathf.Max(0, config.InvaderScoreBase * (rowCount - row));
        }

        private Invader GetPrefabForRow(int row) {
            if (invaderPrefabs != null && invaderPrefabs.Length > 0) {
                int index = Mathf.Clamp(row, 0, invaderPrefabs.Length - 1);

                if (invaderPrefabs[index] != null) {
                    return invaderPrefabs[index];
                }
            }

            return invaderPrefab;
        }

        private void EnsureInvaderCapacity(int count, int rows, int columns) {
            while (invaders.Count < count) {
                int index = invaders.Count;
                int row = columns > 0 ? Mathf.Clamp(index / columns, 0, Mathf.Max(0, rows - 1)) : 0;
                Invader prefab = GetPrefabForRow(row);

                if (prefab == null) {
                    return;
                }

                Invader invader = Instantiate(prefab, transform);
                invader.name = "Invader_" + invaders.Count.ToString("00");
                invader.gameObject.SetActive(false);
                invaders.Add(invader);
            }
        }
    }
}
