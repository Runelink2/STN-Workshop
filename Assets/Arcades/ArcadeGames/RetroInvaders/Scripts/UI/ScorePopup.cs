using UnityEngine;
using UnityEngine.UI;

namespace RetroInvaders {
    [RequireComponent(typeof(Text))]
    public sealed class ScorePopup : MonoBehaviour {
        [SerializeField] private Text text;
        [SerializeField] private RectTransform rectTransform;

        private Vector2 startPosition;
        private float duration = 0.85f;
        private float riseDistance = 54.0f;
        private float elapsed;
        private Color baseColor = Color.white;
        private Vector3 baseScale = Vector3.one;

        public bool IsActive => gameObject.activeSelf;

        public void Show(int score, Vector2 anchoredPosition, float popupDuration, float popupRiseDistance, Color color) {
            CacheReferences();
            startPosition = anchoredPosition;
            duration = Mathf.Max(0.05f, popupDuration);
            riseDistance = Mathf.Max(0.0f, popupRiseDistance);
            elapsed = 0.0f;
            baseColor = color;

            rectTransform.anchoredPosition = startPosition;
            rectTransform.localScale = baseScale * 0.85f;
            text.text = "+" + Mathf.Max(0, score).ToString();
            text.color = baseColor;
            gameObject.SetActive(true);
        }

        public void Hide() {
            gameObject.SetActive(false);
        }

        private void Awake() {
            CacheReferences();
        }

        private void Update() {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1.0f - ((1.0f - t) * (1.0f - t));
            rectTransform.anchoredPosition = startPosition + Vector2.up * (riseDistance * eased);
            rectTransform.localScale = baseScale * Mathf.Lerp(0.85f, 1.08f, Mathf.Sin(t * Mathf.PI));

            Color color = baseColor;
            color.a = 1.0f - t;
            text.color = color;

            if (elapsed >= duration) {
                Hide();
            }
        }

        private void OnValidate() {
            CacheReferences();
        }

        private void CacheReferences() {
            if (text == null) {
                text = GetComponent<Text>();
            }

            if (rectTransform == null) {
                rectTransform = GetComponent<RectTransform>();
            }

            if (rectTransform != null) {
                baseScale = Vector3.one;
            }
        }
    }
}
