using UnityEngine;

namespace RetroInvaders {
    [RequireComponent(typeof(Damageable))]
    public sealed class UfoShip : MonoBehaviour {
        [SerializeField] private Damageable damageable;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Vector2 boundsHalfExtents = new Vector2(0.58f, 0.24f);
        [SerializeField] private float bobAmount = 0.035f;
        [SerializeField] private float bobSpeed = 8.0f;

        private UfoController controller;
        private GameSession session;
        private PlayArea playArea;
        private Vector3 direction = Vector3.right;
        private Vector3 baseVisualLocalPosition;
        private float speed;
        private int scoreValue;
        private bool killNotified;

        public bool IsActive => gameObject.activeInHierarchy && damageable != null && damageable.IsAlive;

        public void ConfigurePrefab(Damageable configuredDamageable, Transform configuredVisualRoot, Vector2 configuredHalfExtents) {
            damageable = configuredDamageable;
            visualRoot = configuredVisualRoot;
            boundsHalfExtents = configuredHalfExtents;
            CacheReferences();
        }

        public void Launch(
            UfoController owner,
            GameSession gameSession,
            PlayArea area,
            Vector3 startPosition,
            int travelDirection,
            float travelSpeed,
            int bonusScore
        ) {
            controller = owner;
            session = gameSession;
            playArea = area;
            direction = travelDirection >= 0 ? Vector3.right : Vector3.left;
            speed = Mathf.Max(0.0f, travelSpeed);
            scoreValue = Mathf.Max(0, bonusScore);
            killNotified = false;
            Vector3 gamePosition = new Vector3(startPosition.x, startPosition.y, 0.0f);
            transform.position = playArea != null ? playArea.GameToWorld(gamePosition) : gamePosition;
            transform.rotation = Quaternion.identity;
            gameObject.SetActive(true);

            if (damageable != null) {
                damageable.Configure(Team.Invader, 1, false);
            }

            if (visualRoot != null) {
                visualRoot.localPosition = baseVisualLocalPosition;
            }
        }

        public void Despawn() {
            gameObject.SetActive(false);
        }

        private void Awake() {
            CacheReferences();
            PixelMeshUtility.OptimizePixelChildren(visualRoot, "UfoPixel");

            if (damageable != null) {
                damageable.OnKilled += HandleKilled;
            }
        }

        private void OnDestroy() {
            if (damageable != null) {
                damageable.OnKilled -= HandleKilled;
            }
        }

        private void Update() {
            if (!IsActive || session == null || session.CurrentState != GameSession.GameState.Playing) {
                return;
            }

            Vector3 gamePosition = playArea != null ? playArea.WorldToGame(transform.position) : transform.position;
            gamePosition += direction * speed * Time.deltaTime;
            transform.position = playArea != null ? playArea.GameToWorld(gamePosition) : gamePosition;
            AnimateVisual();

            if (HasExitedPlayArea()) {
                controller?.NotifyUfoExited(this);
            }
        }

        private void OnValidate() {
            boundsHalfExtents.x = Mathf.Max(0.05f, boundsHalfExtents.x);
            boundsHalfExtents.y = Mathf.Max(0.05f, boundsHalfExtents.y);
            bobAmount = Mathf.Max(0.0f, bobAmount);
            bobSpeed = Mathf.Max(0.0f, bobSpeed);
            CacheReferences();
        }

        private void HandleKilled(Damageable killedDamageable, HitInfo hit) {
            if (killNotified) {
                return;
            }

            killNotified = true;
            Vector3 gamePosition = playArea != null ? playArea.WorldToGame(transform.position) : transform.position;
            controller?.NotifyUfoKilled(this, scoreValue, gamePosition);
            gameObject.SetActive(false);
        }

        private bool HasExitedPlayArea() {
            if (playArea == null) {
                return false;
            }

            float margin = boundsHalfExtents.x + 0.25f;
            Vector3 gamePosition = playArea.WorldToGame(transform.position);
            return direction.x > 0.0f
                ? gamePosition.x - boundsHalfExtents.x > playArea.Right + margin
                : gamePosition.x + boundsHalfExtents.x < playArea.Left - margin;
        }

        private void AnimateVisual() {
            if (visualRoot == null) {
                return;
            }

            float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
            visualRoot.localPosition = baseVisualLocalPosition + new Vector3(0.0f, bob, 0.0f);
        }

        private void CacheReferences() {
            if (damageable == null) {
                damageable = GetComponent<Damageable>();
            }

            if (visualRoot == null) {
                visualRoot = transform.childCount > 0 ? transform.GetChild(0) : transform;
            }

            if (visualRoot != null) {
                baseVisualLocalPosition = visualRoot.localPosition;
            }
        }
    }
}
