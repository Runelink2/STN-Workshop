using UnityEngine;

namespace RetroInvaders {
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Projectile : MonoBehaviour {
        [SerializeField] private Renderer visualRenderer;
        [SerializeField] private float boundsMargin = 0.5f;

        private ProjectilePool pool;
        private PlayArea playArea;
        private Vector3 direction = Vector3.up;
        private float speed;
        private float lifetimeRemaining;
        private int damage = 1;
        private Team ownerTeam = Team.Neutral;
        private bool isLive;

        public Team OwnerTeam => ownerTeam;
        public bool IsLive => isLive;

        public void Initialize(
            ProjectilePool owningPool,
            PlayArea area,
            Vector3 position,
            Vector3 travelDirection,
            float projectileSpeed,
            int projectileDamage,
            Team team,
            float lifetime,
            Material material
        ) {
            pool = owningPool;
            playArea = area;
            direction = ResolveDirection(travelDirection);
            speed = Mathf.Max(0.0f, projectileSpeed);
            damage = Mathf.Max(1, projectileDamage);
            ownerTeam = team;
            lifetimeRemaining = Mathf.Max(0.01f, lifetime);
            Vector3 gamePosition = new Vector3(position.x, position.y, 0.0f);
            transform.position = playArea != null ? playArea.GameToWorld(gamePosition) : gamePosition;
            transform.rotation = Quaternion.identity;

            if (visualRenderer != null && material != null) {
                visualRenderer.sharedMaterial = material;
            }

            isLive = true;
            gameObject.SetActive(true);
        }

        public void DeactivateForPool() {
            isLive = false;
            gameObject.SetActive(false);
        }

        private void Awake() {
            CacheRenderer();
            ConfigurePhysicsComponents();
        }

        private void OnValidate() {
            boundsMargin = Mathf.Max(0.0f, boundsMargin);
            CacheRenderer();
            ConfigurePhysicsComponents();
        }

        private void Update() {
            if (!isLive) {
                return;
            }

            float deltaTime = Time.deltaTime;
            Vector3 gamePosition = playArea != null ? playArea.WorldToGame(transform.position) : transform.position;
            gamePosition += direction * speed * deltaTime;
            transform.position = playArea != null ? playArea.GameToWorld(gamePosition) : gamePosition;
            lifetimeRemaining -= deltaTime;

            if (lifetimeRemaining <= 0.0f || IsOutsidePlayArea(gamePosition)) {
                Despawn();
            }
        }

        private void OnTriggerEnter(Collider other) {
            if (!isLive) {
                return;
            }

            if (other.GetComponentInParent<Projectile>() != null) {
                return;
            }

            Damageable damageable = other.GetComponentInParent<Damageable>();
            if (damageable == null) {
                return;
            }

            HitInfo hit = new HitInfo(damage, ownerTeam, transform.position, direction, gameObject);
            if (!damageable.CanReceiveDamage(hit)) {
                return;
            }

            isLive = false;
            damageable.ApplyDamage(hit);
            Despawn();
        }

        private void Despawn() {
            isLive = false;

            if (pool != null) {
                pool.Release(this);
            } else {
                gameObject.SetActive(false);
            }
        }

        private bool IsOutsidePlayArea(Vector3 position) {
            if (playArea == null) {
                return false;
            }

            return playArea.IsAboveTop(position, boundsMargin)
                || playArea.IsBelowBottom(position, boundsMargin)
                || playArea.IsPastLeft(position, boundsMargin)
                || playArea.IsPastRight(position, boundsMargin);
        }

        private static Vector3 ResolveDirection(Vector3 travelDirection) {
            if (travelDirection.sqrMagnitude <= 0.0001f) {
                return Vector3.up;
            }

            Vector3 resolved = travelDirection.normalized;
            resolved.z = 0.0f;
            return resolved.sqrMagnitude <= 0.0001f ? Vector3.up : resolved.normalized;
        }

        private void CacheRenderer() {
            if (visualRenderer == null) {
                visualRenderer = GetComponentInChildren<Renderer>();
            }
        }

        private void ConfigurePhysicsComponents() {
            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null) {
                body.useGravity = false;
                body.isKinematic = true;
            }

            Collider trigger = GetComponent<Collider>();
            if (trigger != null) {
                trigger.isTrigger = true;
            }
        }
    }
}
