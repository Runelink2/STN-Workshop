using UnityEngine;

namespace RetroInvaders {
    [RequireComponent(typeof(PlayerShip))]
    public sealed class PlayerWeapon : MonoBehaviour {
        [SerializeField] private GameSession session;
        [SerializeField] private GameConfig config;
        [SerializeField] private PlayerShip ship;
        [SerializeField] private ProjectilePool projectilePool;

        private float cooldownRemaining;

        public void Configure(GameSession gameSession, GameConfig gameConfig, PlayerShip playerShip, ProjectilePool pool) {
            session = gameSession;
            config = gameConfig;
            ship = playerShip;
            projectilePool = pool;
            cooldownRemaining = 0.0f;
        }

        private void Awake() {
            if (ship == null) {
                ship = GetComponent<PlayerShip>();
            }
        }

        private void Update() {
            if (cooldownRemaining > 0.0f) {
                cooldownRemaining -= Time.deltaTime;
            }

            if (session == null
                || config == null
                || projectilePool == null
                || session.CurrentState != GameSession.GameState.Playing) {
                return;
            }

            if (cooldownRemaining > 0.0f || !IsFirePressed()) {
                return;
            }

            Fire();
        }

        private void Fire() {
            Transform muzzle = ship != null ? ship.Muzzle : transform;
            Vector3 muzzlePosition = session.PlayArea != null ? session.PlayArea.WorldToGame(muzzle.position) : muzzle.position;
            Projectile projectile = projectilePool.Spawn(
                muzzlePosition,
                Vector3.up,
                config.PlayerProjectileSpeed,
                config.PlayerProjectileDamage,
                Team.Player
            );

            if (projectile != null) {
                cooldownRemaining = config.PlayerFireCooldown;
                session?.NotifyPlayerFired(muzzlePosition);
            }
        }

        private static bool IsFirePressed() {
            return Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftControl);
        }
    }
}
