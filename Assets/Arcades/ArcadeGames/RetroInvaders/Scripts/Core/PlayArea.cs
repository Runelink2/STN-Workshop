using UnityEngine;

namespace RetroInvaders {
    public sealed class PlayArea : MonoBehaviour {
        [SerializeField] private Vector2 center = Vector2.zero;
        [SerializeField] private Vector2 size = new Vector2(16.0f, 10.0f);

        public float Left => center.x - size.x * 0.5f;
        public float Right => center.x + size.x * 0.5f;
        public float Bottom => center.y - size.y * 0.5f;
        public float Top => center.y + size.y * 0.5f;
        public float Width => size.x;
        public float Height => size.y;
        public Vector2 Center => center;
        public Vector2 Size => size;
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

            Configure(config.PlayfieldCenter, config.PlayfieldSize);
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

        private void OnValidate() {
            size.x = Mathf.Max(0.1f, size.x);
            size.y = Mathf.Max(0.1f, size.y);
        }

        private void OnDrawGizmos() {
            Gizmos.color = new Color(0.0f, 0.9f, 1.0f, 0.85f);
            Gizmos.DrawWireCube(GameToWorld(new Vector3(center.x, center.y, 0.0f)), new Vector3(size.x, size.y, 0.05f));
        }
    }
}
