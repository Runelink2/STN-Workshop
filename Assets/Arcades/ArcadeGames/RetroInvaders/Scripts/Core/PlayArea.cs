using UnityEngine;

namespace RetroInvaders {
    public sealed class PlayArea : MonoBehaviour {
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color FallbackBackgroundColor = new Color(0.01f, 0.012f, 0.018f, 1.0f);

        [SerializeField] private GameConfig config;
        [SerializeField] private Vector2 center = Vector2.zero;
        [SerializeField] private Vector2 size = new Vector2(11.2f, 9.6f);
        [SerializeField] private Renderer backgroundRenderer;
        [SerializeField] private Texture backgroundTexture;

        private MaterialPropertyBlock backgroundProperties;

        public float Left => center.x - size.x * 0.5f;
        public float Right => center.x + size.x * 0.5f;
        public float Bottom => center.y - size.y * 0.5f;
        public float Top => center.y + size.y * 0.5f;
        public float Width => size.x;
        public float Height => size.y;
        public Vector2 Center => center;
        public Vector2 Size => size;
        public Texture BackgroundTexture => backgroundTexture != null ? backgroundTexture : config != null ? config.BackgroundTexture : null;
        private Transform SpaceRoot => transform.parent != null ? transform.parent : transform;

        public void Configure(Vector2 newCenter, Vector2 newSize) {
            center = newCenter;
            size = new Vector2(Mathf.Max(0.1f, newSize.x), Mathf.Max(0.1f, newSize.y));
            transform.localPosition = new Vector3(center.x, center.y, 0.0f);
        }

        public void Configure(GameConfig config) {
            if (config == null) {
                return;
            }

            this.config = config;
            Configure(config.PlayfieldCenter, config.PlayfieldSize);
            ApplyBackgroundTexture();
        }

        public void SetBackgroundRenderer(Renderer renderer) {
            backgroundRenderer = renderer;
            ApplyBackgroundTexture();
        }

        public void SetBackgroundTexture(Texture texture) {
            backgroundTexture = texture;
            ApplyBackgroundTexture();
        }

        public Vector3 ClampPosition(Vector3 position, float margin = 0.0f) {
            position.x = Mathf.Clamp(position.x, Left + margin, Right - margin);
            position.y = Mathf.Clamp(position.y, Bottom + margin, Top - margin);
            return position;
        }

        public Vector3 GameToWorld(Vector3 position) {
            return SpaceRoot.TransformPoint(position);
        }

        public Vector3 WorldToGame(Vector3 position) {
            return SpaceRoot.InverseTransformPoint(position);
        }

        public bool Contains(Vector3 position, float margin = 0.0f) {
            return position.x >= Left + margin
                && position.x <= Right - margin
                && position.y >= Bottom + margin
                && position.y <= Top - margin;
        }

        public bool IsAboveTop(Vector3 position, float margin = 0.0f) {
            return position.y > Top + margin;
        }

        public bool IsBelowBottom(Vector3 position, float margin = 0.0f) {
            return position.y < Bottom - margin;
        }

        public bool IsPastLeft(Vector3 position, float margin = 0.0f) {
            return position.x < Left - margin;
        }

        public bool IsPastRight(Vector3 position, float margin = 0.0f) {
            return position.x > Right + margin;
        }

        private void OnDrawGizmos() {
            Gizmos.color = new Color(1.0f, 0.08f, 0.08f, 0.85f);
            Gizmos.DrawWireCube(GameToWorld(new Vector3(center.x, center.y, 0.0f)), new Vector3(size.x, size.y, 0.05f));
        }

        private void Awake() {
            ApplyBackgroundTexture();
        }

        private void OnValidate() {
            size.x = Mathf.Max(0.1f, size.x);
            size.y = Mathf.Max(0.1f, size.y);
            ApplyBackgroundTexture();
        }

        private void ApplyBackgroundTexture() {
            Renderer renderer = ResolveBackgroundRenderer();
            if (renderer == null) {
                return;
            }

            if (backgroundProperties == null) {
                backgroundProperties = new MaterialPropertyBlock();
            }

            backgroundProperties.Clear();

            Texture texture = BackgroundTexture;
            Material material = renderer.sharedMaterial;
            if (material == null) {
                renderer.SetPropertyBlock(backgroundProperties);
                return;
            }

            if (material.HasProperty(ColorId)) {
                backgroundProperties.SetColor(ColorId, texture != null ? Color.white : FallbackBackgroundColor);
            }

            if (material.HasProperty(BaseMapId)) {
                backgroundProperties.SetTexture(BaseMapId, texture != null ? texture : Texture2D.blackTexture);
            }

            if (material.HasProperty(MainTexId)) {
                backgroundProperties.SetTexture(MainTexId, texture != null ? texture : Texture2D.blackTexture);
            }

            renderer.SetPropertyBlock(backgroundProperties);
        }

        private Renderer ResolveBackgroundRenderer() {
            if (backgroundRenderer != null) {
                return backgroundRenderer;
            }

            Transform screenPlane = transform.Find("Screen Plane");
            if (screenPlane == null) {
                return null;
            }

            backgroundRenderer = screenPlane.GetComponent<Renderer>();
            return backgroundRenderer;
        }
    }
}
