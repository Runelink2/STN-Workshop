using UnityEngine;

namespace RetroInvaders {
    public sealed class ScreenShake : MonoBehaviour {
        [SerializeField] private GameConfig config;

        private Vector3 baseLocalPosition;
        private float duration;
        private float magnitude;
        private float remaining;

        public void Configure(GameConfig gameConfig) {
            config = gameConfig;
            CacheBasePosition();
        }

        public void Shake() {
            if (config == null) {
                Shake(0.12f, 0.08f);
                return;
            }

            Shake(config.ScreenShakeDuration, config.ScreenShakeMagnitude);
        }

        public void Shake(float shakeDuration, float shakeMagnitude) {
            duration = Mathf.Max(0.0f, shakeDuration);
            magnitude = Mathf.Max(0.0f, shakeMagnitude);
            remaining = duration;

            if (remaining <= 0.0f || magnitude <= 0.0f) {
                ResetPosition();
            }
        }

        private void Awake() {
            CacheBasePosition();
        }

        private void OnEnable() {
            CacheBasePosition();
        }

        private void OnDisable() {
            ResetPosition();
        }

        private void Update() {
            if (remaining <= 0.0f) {
                return;
            }

            remaining -= Time.deltaTime;

            if (remaining <= 0.0f) {
                ResetPosition();
                return;
            }

            float strength = duration > 0.0f ? remaining / duration : 0.0f;
            Vector2 offset = Random.insideUnitCircle * magnitude * strength;
            transform.localPosition = baseLocalPosition + new Vector3(offset.x, offset.y, 0.0f);
        }

        private void CacheBasePosition() {
            baseLocalPosition = transform.localPosition;
        }

        private void ResetPosition() {
            transform.localPosition = baseLocalPosition;
        }
    }
}
