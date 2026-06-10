using UnityEngine;
using UnityEngine.UI;

namespace RetroInvaders {
    public sealed class HUDController : MonoBehaviour {
        [SerializeField] private GameSession session;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text highScoreText;
        [SerializeField] private Text livesText;
        [SerializeField] private Text waveText;
        [SerializeField] private GameObject messagePanel;
        [SerializeField] private Text messageTitleText;
        [SerializeField] private Text messageBodyText;
        [SerializeField] private Text messagePromptText;
        [SerializeField] private Text messageFooterText;

        private GameSession subscribedSession;
        private int displayedScore = int.MinValue;
        private int displayedHighScore = int.MinValue;
        private int displayedLives = int.MinValue;
        private int displayedWave = int.MinValue;
        private GameSession.GameState displayedState = (GameSession.GameState)(-1);

        public void Configure(
            GameSession gameSession,
            Text score,
            Text highScore,
            Text lives,
            Text wave,
            GameObject panel,
            Text title,
            Text body,
            Text prompt
        ) {
            Configure(gameSession, score, highScore, lives, wave, panel, title, body, prompt, null);
        }

        public void Configure(
            GameSession gameSession,
            Text score,
            Text highScore,
            Text lives,
            Text wave,
            GameObject panel,
            Text title,
            Text body,
            Text prompt,
            Text footer
        ) {
            session = gameSession;
            scoreText = score;
            highScoreText = highScore;
            livesText = lives;
            waveText = wave;
            messagePanel = panel;
            messageTitleText = title;
            messageBodyText = body;
            messagePromptText = prompt;
            messageFooterText = footer;

            if (isActiveAndEnabled) {
                SubscribeToSession();
                RefreshAll();
            }
        }

        private void OnEnable() {
            SubscribeToSession();
            RefreshAll();
        }

        private void OnDisable() {
            UnsubscribeFromSession();
        }

        private void SubscribeToSession() {
            if (subscribedSession == session) {
                return;
            }

            UnsubscribeFromSession();

            if (session == null) {
                return;
            }

            session.OnSessionChanged += HandleSessionChanged;
            session.OnStateChanged += HandleStateChanged;
            session.OnScoreChanged += HandleScoreChanged;
            subscribedSession = session;
        }

        private void UnsubscribeFromSession() {
            if (subscribedSession == null) {
                return;
            }

            subscribedSession.OnSessionChanged -= HandleSessionChanged;
            subscribedSession.OnStateChanged -= HandleStateChanged;
            subscribedSession.OnScoreChanged -= HandleScoreChanged;
            subscribedSession = null;
        }

        private void HandleSessionChanged(GameSession changedSession) {
            RefreshAll();
        }

        private void HandleStateChanged(GameSession.GameState state) {
            RefreshMessage(state);
        }

        private void HandleScoreChanged(int score, int highScore) {
            RefreshScore(score, highScore);
        }

        private void RefreshAll() {
            if (session == null) {
                return;
            }

            RefreshScore(session.Score, session.HighScore);
            RefreshStats();
            RefreshMessage(session.CurrentState);
        }

        private void RefreshScore(int score, int highScore) {
            if (displayedScore == score && displayedHighScore == highScore) {
                return;
            }

            displayedScore = score;
            displayedHighScore = highScore;
            SetText(scoreText, "SCORE " + score.ToString("00000"));
            SetText(highScoreText, "HIGH " + highScore.ToString("00000"));
        }

        private void RefreshStats() {
            if (session == null) {
                return;
            }

            if (displayedLives != session.Lives) {
                displayedLives = session.Lives;
                SetText(livesText, "LIVES " + session.Lives);
            }

            if (displayedWave != session.Wave) {
                displayedWave = session.Wave;
                SetText(waveText, "WAVE " + session.Wave);
            }
        }

        private void RefreshMessage(GameSession.GameState state) {
            if (displayedState == state
                && state != GameSession.GameState.WaveClear
                && state != GameSession.GameState.PlayerDead
                && state != GameSession.GameState.GameOver) {
                return;
            }

            displayedState = state;
            bool showPanel = true;
            string title = string.Empty;
            string body = string.Empty;
            string prompt = string.Empty;
            string footer = string.Empty;

            switch (state) {
                case GameSession.GameState.Title:
                    title = "RETRO INVADERS";
                    body = "Move: A/D or Arrow Keys\nFire: Space\nPause: P or Escape";
                    prompt = "Press Enter to Start";
                    footer = "Bonus: hit the UFO for extra points";
                    break;
                case GameSession.GameState.Paused:
                    title = "PAUSED";
                    body = string.Empty;
                    prompt = "Press P or Escape to Resume";
                    break;
                case GameSession.GameState.WaveClear:
                    title = "WAVE CLEAR";
                    body = "Next Wave " + (session != null ? (session.Wave + 1).ToString() : "1");
                    prompt = string.Empty;
                    break;
                case GameSession.GameState.PlayerDead:
                    title = "READY";
                    body = "Lives " + (session != null ? session.Lives.ToString() : "0");
                    prompt = string.Empty;
                    break;
                case GameSession.GameState.GameOver:
                    title = "GAME OVER";
                    body = "Final Score " + (session != null ? session.Score.ToString("00000") : "00000")
                        + "\nHigh Score " + (session != null ? session.HighScore.ToString("00000") : "00000");
                    prompt = "Press Enter to Restart";
                    footer = "Escape returns to Title";
                    break;
                default:
                    showPanel = false;
                    break;
            }

            if (messagePanel != null) {
                messagePanel.SetActive(showPanel);
            }

            if (!showPanel) {
                return;
            }

            SetText(messageTitleText, title);
            SetText(messageBodyText, body);
            SetText(messagePromptText, prompt);
            SetText(messageFooterText, footer);
        }

        private static void SetText(Text text, string value) {
            if (text != null && text.text != value) {
                text.text = value;
            }
        }
    }
}
