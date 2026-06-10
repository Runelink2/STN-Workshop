using UnityEngine;

namespace RetroInvaders {
    public sealed class PlayerShip : MonoBehaviour {
        [SerializeField] private Transform muzzle;
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerWeapon weapon;
        [SerializeField] private Damageable damageable;

        private GameSession session;
        private GameConfig config;
        private Renderer[] renderers;
        private float invulnerabilityTimer;
        private bool deathReported;

        public Transform Muzzle => muzzle != null ? muzzle : transform;
        public PlayerController Controller => controller;
        public PlayerWeapon Weapon => weapon;
        public Damageable Damageable => damageable;

        public void Configure(GameSession session, GameConfig config, PlayArea playArea) {
            Configure(session, config, playArea, null);
        }

        public void Configure(GameSession session, GameConfig config, PlayArea playArea, ProjectilePool projectilePool) {
            this.session = session;
            this.config = config;

            if (controller == null) {
                controller = GetComponent<PlayerController>();
            }

            if (weapon == null) {
                weapon = GetComponent<PlayerWeapon>();
            }

            if (damageable == null) {
                damageable = GetComponent<Damageable>();
            }

            SubscribeDamageable();

            if (controller != null) {
                controller.Configure(session, config, playArea, this);
            }

            if (weapon != null) {
                weapon.Configure(session, config, this, projectilePool);
            }

            if (damageable != null) {
                damageable.Configure(Team.Player, 1, false);
            }
        }

        public void PrepareForSpawn(float invulnerableSeconds) {
            deathReported = false;
            CacheRenderers();

            if (damageable != null) {
                damageable.ResetHealth();
                damageable.SetInvulnerable(invulnerableSeconds > 0.0f);
            }

            invulnerabilityTimer = Mathf.Max(0.0f, invulnerableSeconds);
            SetRenderersVisible(true);
        }

        public void SetMuzzle(Transform muzzleTransform) {
            muzzle = muzzleTransform;
        }

        public void SetDamageable(Damageable configuredDamageable) {
            damageable = configuredDamageable;
        }

        private void Awake() {
            PixelMeshUtility.OptimizePixelChildren(GetVisualRoot(), "PlayerPixel");
            CacheReferences();
            SubscribeDamageable();
            CacheRenderers();
        }

        private void OnEnable() {
            deathReported = false;
            SetRenderersVisible(true);
        }

        private void OnDestroy() {
            UnsubscribeDamageable();
        }

        private void Update() {
            UpdateInvulnerability();
        }

        private void CacheReferences() {
            if (controller == null) {
                controller = GetComponent<PlayerController>();
            }

            if (weapon == null) {
                weapon = GetComponent<PlayerWeapon>();
            }

            if (damageable == null) {
                damageable = GetComponent<Damageable>();
            }
        }

        private Transform GetVisualRoot() {
            Transform visualRoot = transform.Find("Visual");
            return visualRoot != null ? visualRoot : transform;
        }

        private void SubscribeDamageable() {
            if (damageable != null) {
                damageable.OnKilled -= HandleKilled;
                damageable.OnKilled += HandleKilled;
            }
        }

        private void UnsubscribeDamageable() {
            if (damageable != null) {
                damageable.OnKilled -= HandleKilled;
            }
        }

        private void HandleKilled(Damageable killedDamageable, HitInfo hit) {
            if (deathReported) {
                return;
            }

            deathReported = true;
            session?.NotifyPlayerKilled(this, hit);
        }

        private void UpdateInvulnerability() {
            if (invulnerabilityTimer <= 0.0f) {
                return;
            }

            invulnerabilityTimer -= Time.deltaTime;

            if (invulnerabilityTimer <= 0.0f) {
                invulnerabilityTimer = 0.0f;

                if (damageable != null) {
                    damageable.SetInvulnerable(false);
                }

                SetRenderersVisible(true);
                return;
            }

            float blinkRate = config != null ? config.PlayerRespawnBlinkRate : 7.0f;
            bool visible = Mathf.FloorToInt(Time.time * blinkRate) % 2 == 0;
            SetRenderersVisible(visible);
        }

        private void CacheRenderers() {
            if (renderers == null || renderers.Length == 0) {
                renderers = GetComponentsInChildren<Renderer>(true);
            }
        }

        private void SetRenderersVisible(bool visible) {
            if (renderers == null) {
                return;
            }

            for (int i = 0; i < renderers.Length; i++) {
                if (renderers[i] != null) {
                    renderers[i].enabled = visible;
                }
            }
        }
    }
}
