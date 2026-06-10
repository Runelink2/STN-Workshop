using UnityEngine;

namespace RetroInvaders {
    public sealed class VfxFlash : MonoBehaviour {
        [SerializeField] private Renderer visualRenderer;

        private Vector3 startScale = Vector3.one;
        private float duration = 0.1f;
        private float elapsed;

        public bool IsActive => gameObject.activeSelf;

        public void Play(Vector3 position, Vector3 scale, float lifeTime, Material material) {
            CacheRenderer();
            transform.localPosition = new Vector3(position.x, position.y, -0.08f);
            transform.localRotation = Quaternion.identity;
            transform.localScale = scale;
            startScale = scale;
            duration = Mathf.Max(0.01f, lifeTime);
            elapsed = 0.0f;

            if (visualRenderer != null && material != null) {
                visualRenderer.sharedMaterial = material;
            }

            gameObject.SetActive(true);
        }

        public void Hide() {
            gameObject.SetActive(false);
        }

        private void Awake() {
            CacheRenderer();
        }

        private void Update() {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);

            if (elapsed >= duration) {
                Hide();
            }
        }

        private void OnValidate() {
            CacheRenderer();
        }

        private void CacheRenderer() {
            if (visualRenderer == null) {
                visualRenderer = GetComponentInChildren<Renderer>();
            }
        }
    }
}
