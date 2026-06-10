using UnityEngine;

namespace RetroInvaders {
    [RequireComponent(typeof(Damageable))]
    public sealed class Invader : MonoBehaviour {
        [SerializeField] private Damageable damageable;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Vector2 boundsHalfExtents = new Vector2(0.38f, 0.28f);
        [SerializeField] private float animationSpeed = 7.0f;
        [SerializeField] private float animationAmount = 0.045f;
        [SerializeField] private int scoreValue = 10;

        private InvaderFleetController fleet;
        private Vector3 baseVisualScale = Vector3.one;
        private Vector3 baseVisualPosition;
        private Transform cachedBaseVisualRoot;
        private float animationPhase;
        private bool useSteppedAnimation;
        private bool killNotified;

        public Damageable Damageable => damageable;
        public Vector2 BoundsHalfExtents => boundsHalfExtents;
        public int ScoreValue => scoreValue;
        public bool IsAlive => gameObject.activeInHierarchy && damageable != null && damageable.IsAlive;

        public void Configure(InvaderFleetController owner, int score, int health, int row, int column) {
            fleet = owner;
            scoreValue = Mathf.Max(0, score);
            killNotified = false;
            animationPhase = (row * 0.47f) + (column * 0.31f);
            CacheReferences();

            if (damageable != null) {
                damageable.Configure(Team.Invader, health, false);
            }

            if (visualRoot != null) {
                visualRoot.localPosition = baseVisualPosition;
                visualRoot.localScale = baseVisualScale;
            }
        }

        public void ConfigurePrefab(Damageable configuredDamageable, Transform configuredVisualRoot, Vector2 configuredHalfExtents) {
            damageable = configuredDamageable;
            visualRoot = configuredVisualRoot;
            boundsHalfExtents = configuredHalfExtents;
            cachedBaseVisualRoot = null;
            CacheReferences();
        }

        public void SetSteppedAnimation(bool enabled) {
            useSteppedAnimation = enabled;

            if (visualRoot == null) {
                return;
            }

            visualRoot.localPosition = baseVisualPosition;
            visualRoot.localScale = baseVisualScale;
        }

        public void SetStepPose(int stepIndex) {
            if (visualRoot == null) {
                return;
            }

            useSteppedAnimation = true;
            float sign = stepIndex % 2 == 0 ? 1.0f : -1.0f;
            visualRoot.localPosition = baseVisualPosition + new Vector3(0.035f * sign, 0.0f, 0.0f);
            visualRoot.localScale = baseVisualScale;
        }

        private void Awake() {
            CacheReferences();
            PixelMeshUtility.OptimizePixelChildren(visualRoot, "InvaderPixel");

            if (damageable != null) {
                damageable.OnKilled += HandleKilled;
            }
        }

        private void OnDestroy() {
            if (damageable != null) {
                damageable.OnKilled -= HandleKilled;
            }
        }

        private void OnEnable() {
            killNotified = false;

            if (visualRoot != null) {
                visualRoot.localPosition = baseVisualPosition;
                visualRoot.localScale = baseVisualScale;
            }
        }

        private void Update() {
            if (!IsAlive || visualRoot == null || useSteppedAnimation) {
                return;
            }

            float pulse = 1.0f + Mathf.Sin((Time.time * animationSpeed) + animationPhase) * animationAmount;
            visualRoot.localScale = new Vector3(baseVisualScale.x * pulse, baseVisualScale.y, baseVisualScale.z);
        }

        private void OnValidate() {
            boundsHalfExtents.x = Mathf.Max(0.05f, boundsHalfExtents.x);
            boundsHalfExtents.y = Mathf.Max(0.05f, boundsHalfExtents.y);
            animationSpeed = Mathf.Max(0.0f, animationSpeed);
            animationAmount = Mathf.Max(0.0f, animationAmount);
            scoreValue = Mathf.Max(0, scoreValue);
            CacheReferences();
        }

        private void HandleKilled(Damageable killedDamageable, HitInfo hit) {
            if (killNotified) {
                return;
            }

            killNotified = true;
            fleet?.NotifyInvaderKilled(this, hit);
            gameObject.SetActive(false);
        }

        private void CacheReferences() {
            if (damageable == null) {
                damageable = GetComponent<Damageable>();
            }

            if (visualRoot == null) {
                visualRoot = transform.childCount > 0 ? transform.GetChild(0) : transform;
            }

            if (visualRoot != null && cachedBaseVisualRoot != visualRoot) {
                baseVisualScale = visualRoot.localScale;
                baseVisualPosition = visualRoot.localPosition;
                cachedBaseVisualRoot = visualRoot;
            }
        }
    }
}
