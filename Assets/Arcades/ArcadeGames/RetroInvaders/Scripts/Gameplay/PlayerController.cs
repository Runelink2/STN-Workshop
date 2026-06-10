using UnityEngine;

namespace RetroInvaders {
    [RequireComponent(typeof(PlayerShip))]
    public sealed class PlayerController : MonoBehaviour {
        private const string HorizontalAxis = "Horizontal";

        [SerializeField] private GameSession session;
        [SerializeField] private GameConfig config;
        [SerializeField] private PlayArea playArea;
        [SerializeField] private PlayerShip ship;

        public void Configure(GameSession gameSession, GameConfig gameConfig, PlayArea area, PlayerShip playerShip) {
            session = gameSession;
            config = gameConfig;
            playArea = area;
            ship = playerShip;
        }

        private void Awake() {
            if (ship == null) {
                ship = GetComponent<PlayerShip>();
            }
        }

        private void Update() {
            if (session == null || config == null || playArea == null || session.CurrentState != GameSession.GameState.Playing) {
                return;
            }

            float input = ReadHorizontalInput();
            if (Mathf.Approximately(input, 0.0f)) {
                return;
            }

            Vector3 position = playArea.WorldToGame(transform.position);
            position.x += input * config.PlayerSpeed * Time.deltaTime;

            float margin = config.PlayerHalfExtents.x + config.PlayerHorizontalPadding;
            position.x = Mathf.Clamp(position.x, playArea.Left + margin, playArea.Right - margin);
            position.y = config.PlayerStartY;
            position.z = 0.0f;
            transform.position = playArea.GameToWorld(position);
        }

        private static float ReadHorizontalInput() {
            float axis = Input.GetAxisRaw(HorizontalAxis);

            if (!Mathf.Approximately(axis, 0.0f)) {
                return Mathf.Clamp(axis, -1.0f, 1.0f);
            }

            float keyboard = 0.0f;

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) {
                keyboard -= 1.0f;
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) {
                keyboard += 1.0f;
            }

            return Mathf.Clamp(keyboard, -1.0f, 1.0f);
        }
    }
}
