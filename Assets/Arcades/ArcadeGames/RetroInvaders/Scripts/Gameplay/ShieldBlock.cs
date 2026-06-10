using UnityEngine;

namespace RetroInvaders {
    [RequireComponent(typeof(Damageable))]
    public sealed class ShieldBlock : MonoBehaviour {
        [SerializeField] private Damageable damageable;
        [SerializeField] private Renderer visualRenderer;
        [SerializeField] private Vector3 blockScale = new Vector3(0.22f, 0.18f, 0.18f);
        [SerializeField] private Color hitFlashColor = new Color(1.0f, 1.0f, 0.65f, 1.0f);
        [SerializeField] private float hitFlashDuration = 0.08f;

        private int health = 3;
        private ShieldController owner;
        private int shieldIndex = -1;
        private int row = -1;
        private int column = -1;
        private Color baseColor = new Color(0.1f, 0.92f, 0.55f, 1.0f);
        private MaterialPropertyBlock propertyBlock;
        private float flashTimer;

        public Damageable Damageable => damageable;
        public int ShieldIndex => shieldIndex;
        public int Row => row;
        public int Column => column;

        public void Configure(int hitPoints, Vector3 scale, Material material) {
            Configure(null, -1, -1, -1, hitPoints, scale, material);
        }

        public void Configure(
            ShieldController shieldOwner,
            int configuredShieldIndex,
            int configuredRow,
            int configuredColumn,
            int hitPoints,
            Vector3 scale,
            Material material
        ) {
            owner = shieldOwner;
            shieldIndex = configuredShieldIndex;
            row = configuredRow;
            column = configuredColumn;
            health = Mathf.Max(1, hitPoints);
            blockScale = scale;
            CacheReferences();
            ApplyRuntimePixelVisual();

            if (visualRenderer != null && material != null) {
                visualRenderer.sharedMaterial = material;
                baseColor = material.color;
            }

            if (damageable != null) {
                damageable.Configure(Team.Neutral, health, true);
            }

            ResetBlock();
        }

        public void ResetBlock() {
            transform.localScale = blockScale;
            gameObject.SetActive(true);

            if (damageable != null) {
                damageable.Configure(Team.Neutral, health, true);
            }

            flashTimer = 0.0f;
            ApplyHealthTint();
        }

        public void ClearBlock() {
            gameObject.SetActive(false);
        }

        private void Awake() {
            CacheReferences();
            ApplyRuntimePixelVisual();
            CaptureBaseColor();

            if (damageable != null) {
                damageable.OnDamaged += HandleDamaged;
            }
        }

        private void OnDestroy() {
            if (damageable != null) {
                damageable.OnDamaged -= HandleDamaged;
            }
        }

        private void OnValidate() {
            blockScale.x = Mathf.Max(0.02f, blockScale.x);
            blockScale.y = Mathf.Max(0.02f, blockScale.y);
            blockScale.z = Mathf.Max(0.02f, blockScale.z);
            hitFlashDuration = Mathf.Max(0.0f, hitFlashDuration);
            CacheReferences();
        }

        private void Update() {
            if (flashTimer <= 0.0f) {
                return;
            }

            flashTimer -= Time.deltaTime;

            if (flashTimer <= 0.0f) {
                flashTimer = 0.0f;
                ApplyHealthTint();
            }
        }

        private void HandleDamaged(Damageable target, HitInfo hit) {
            if (target == null || target.MaxHealth <= 0) {
                return;
            }

            float healthFraction = target.CurrentHealth / (float)target.MaxHealth;
            float shrink = Mathf.Lerp(0.7f, 1.0f, healthFraction);
            transform.localScale = new Vector3(blockScale.x * shrink, blockScale.y * shrink, blockScale.z);
            flashTimer = hitFlashDuration;
            ApplyColor(hitFlashColor);
            owner?.NotifyBlockHit(this, hit);
        }

        private void CacheReferences() {
            if (damageable == null) {
                damageable = GetComponent<Damageable>();
            }

            if (visualRenderer == null) {
                visualRenderer = GetComponentInChildren<Renderer>();
            }
        }

        private void ApplyRuntimePixelVisual() {
            if (Application.isPlaying) {
                PixelMeshUtility.ApplyFlatQuadVisual(gameObject);
            }
        }

        private void CaptureBaseColor() {
            if (visualRenderer != null && visualRenderer.sharedMaterial != null) {
                baseColor = visualRenderer.sharedMaterial.color;
            }
        }

        private void ApplyHealthTint() {
            if (damageable == null || damageable.MaxHealth <= 0) {
                ApplyColor(baseColor);
                return;
            }

            float healthFraction = damageable.CurrentHealth / (float)damageable.MaxHealth;
            Color damagedColor = Color.Lerp(new Color(0.18f, 0.55f, 0.32f, 1.0f), baseColor, healthFraction);
            ApplyColor(damagedColor);
        }

        private void ApplyColor(Color color) {
            if (visualRenderer == null) {
                return;
            }

            if (propertyBlock == null) {
                propertyBlock = new MaterialPropertyBlock();
            }

            visualRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_Color", color);
            visualRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
