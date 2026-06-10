using System.Collections.Generic;
using UnityEngine;

namespace RetroInvaders {
    public sealed class ProjectilePool : MonoBehaviour {
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private PlayArea playArea;
        [SerializeField] private Material playerProjectileMaterial;
        [SerializeField] private Material invaderProjectileMaterial;
        [SerializeField] private float projectileLifetime = 4.0f;
        [SerializeField] private int initialSize = 24;
        [SerializeField] private int maxSize = 96;

        private readonly List<Projectile> projectiles = new List<Projectile>(96);
        private bool isPrewarmed;

        public int Count => projectiles.Count;

        public void Configure(
            Projectile prefab,
            PlayArea area,
            Material playerMaterial,
            Material invaderMaterial,
            float lifetime,
            int prewarmCount,
            int maximumSize
        ) {
            projectilePrefab = prefab;
            playArea = area;
            playerProjectileMaterial = playerMaterial;
            invaderProjectileMaterial = invaderMaterial;
            projectileLifetime = Mathf.Max(0.1f, lifetime);
            initialSize = Mathf.Max(0, prewarmCount);
            maxSize = Mathf.Max(initialSize, maximumSize);
            isPrewarmed = false;
        }

        public Projectile Spawn(Vector3 position, Vector3 direction, float speed, int damage, Team team) {
            if (projectilePrefab == null) {
                return null;
            }

            Prewarm();

            Projectile projectile = GetInactiveProjectile();
            if (projectile == null && projectiles.Count < maxSize) {
                projectile = CreateProjectile();
            }

            if (projectile == null) {
                return null;
            }

            projectile.Initialize(this, playArea, position, direction, speed, damage, team, projectileLifetime, GetMaterial(team));
            return projectile;
        }

        public void Release(Projectile projectile) {
            if (projectile == null) {
                return;
            }

            projectile.transform.SetParent(transform, true);
            projectile.DeactivateForPool();
        }

        public void DespawnAll() {
            Prewarm();

            for (int i = 0; i < projectiles.Count; i++) {
                if (projectiles[i] != null) {
                    projectiles[i].DeactivateForPool();
                }
            }
        }

        public void DespawnTeam(Team team) {
            for (int i = 0; i < projectiles.Count; i++) {
                Projectile projectile = projectiles[i];
                if (projectile != null && projectile.gameObject.activeSelf && projectile.OwnerTeam == team) {
                    projectile.DeactivateForPool();
                }
            }
        }

        private void Awake() {
            ClampSettings();
        }

        private void Start() {
            Prewarm();
        }

        private void OnValidate() {
            ClampSettings();
        }

        private void Prewarm() {
            if (isPrewarmed) {
                return;
            }

            RegisterExistingProjectiles();

            while (projectiles.Count < initialSize && projectiles.Count < maxSize && projectilePrefab != null) {
                CreateProjectile();
            }

            isPrewarmed = true;
        }

        private void RegisterExistingProjectiles() {
            for (int i = 0; i < transform.childCount; i++) {
                Projectile projectile = transform.GetChild(i).GetComponent<Projectile>();
                if (projectile != null && !projectiles.Contains(projectile)) {
                    projectiles.Add(projectile);
                    projectile.DeactivateForPool();
                }
            }
        }

        private Projectile GetInactiveProjectile() {
            for (int i = 0; i < projectiles.Count; i++) {
                Projectile projectile = projectiles[i];
                if (projectile != null && !projectile.gameObject.activeSelf) {
                    return projectile;
                }
            }

            return null;
        }

        private Projectile CreateProjectile() {
            Projectile projectile = Instantiate(projectilePrefab, transform);
            projectile.name = projectilePrefab.name + "_" + projectiles.Count.ToString("00");
            projectile.DeactivateForPool();
            projectiles.Add(projectile);
            return projectile;
        }

        private Material GetMaterial(Team team) {
            if (team == Team.Invader && invaderProjectileMaterial != null) {
                return invaderProjectileMaterial;
            }

            return playerProjectileMaterial;
        }

        private void ClampSettings() {
            projectileLifetime = Mathf.Max(0.1f, projectileLifetime);
            initialSize = Mathf.Max(0, initialSize);
            maxSize = Mathf.Max(initialSize, maxSize);
        }
    }
}
