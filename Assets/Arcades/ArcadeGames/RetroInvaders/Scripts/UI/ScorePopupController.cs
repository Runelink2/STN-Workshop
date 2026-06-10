using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RetroInvaders {
    public sealed class ScorePopupController : MonoBehaviour {
        [SerializeField] private GameConfig config;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private RectTransform popupRoot;
        [SerializeField] private ScorePopup popupPrefab;
        [SerializeField] private Color popupColor = new Color(1.0f, 0.95f, 0.35f, 1.0f);
        [SerializeField] private int prewarmCount = 8;
        [SerializeField] private int maxPopups = 24;

        private readonly List<ScorePopup> popups = new List<ScorePopup>(24);
        private bool prewarmed;

        public void Configure(GameConfig gameConfig, Camera camera, RectTransform root, ScorePopup prefab) {
            config = gameConfig;
            worldCamera = camera;
            popupRoot = root;
            popupPrefab = prefab;
            ClampSettings();
            prewarmed = false;
        }

        public void ShowScore(int score, Vector3 worldPosition) {
            if (score <= 0 || popupRoot == null || popupPrefab == null || worldCamera == null) {
                return;
            }

            Prewarm();

            ScorePopup popup = GetInactivePopup();
            if (popup == null && popups.Count < maxPopups) {
                popup = CreatePopup();
            }

            if (popup == null) {
                return;
            }

            Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldPosition);
            Vector2 anchoredPosition;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(popupRoot, screenPoint, worldCamera, out anchoredPosition)) {
                return;
            }

            popup.Show(score, anchoredPosition, GetDuration(), GetRiseDistance(), popupColor);
        }

        public void Clear() {
            Prewarm();

            for (int i = 0; i < popups.Count; i++) {
                if (popups[i] != null) {
                    popups[i].Hide();
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
            if (prewarmed) {
                return;
            }

            RegisterExistingPopups();

            while (popups.Count < prewarmCount && popups.Count < maxPopups && popupPrefab != null) {
                CreatePopup();
            }

            prewarmed = true;
        }

        private void RegisterExistingPopups() {
            if (popupRoot == null) {
                return;
            }

            for (int i = 0; i < popupRoot.childCount; i++) {
                ScorePopup popup = popupRoot.GetChild(i).GetComponent<ScorePopup>();
                if (popup != null && !popups.Contains(popup)) {
                    popup.Hide();
                    popups.Add(popup);
                }
            }
        }

        private ScorePopup GetInactivePopup() {
            for (int i = 0; i < popups.Count; i++) {
                ScorePopup popup = popups[i];
                if (popup != null && !popup.IsActive) {
                    return popup;
                }
            }

            return null;
        }

        private ScorePopup CreatePopup() {
            if (popupPrefab == null || popupRoot == null) {
                return null;
            }

            ScorePopup popup = Instantiate(popupPrefab, popupRoot);
            popup.name = "ScorePopup_" + popups.Count.ToString("00");
            popup.Hide();
            popups.Add(popup);
            return popup;
        }

        private float GetDuration() {
            return config != null ? config.ScorePopupDuration : 0.85f;
        }

        private float GetRiseDistance() {
            return config != null ? config.ScorePopupRiseDistance : 54.0f;
        }

        private void ClampSettings() {
            prewarmCount = Mathf.Max(0, prewarmCount);
            maxPopups = Mathf.Max(prewarmCount, maxPopups);
        }
    }
}
