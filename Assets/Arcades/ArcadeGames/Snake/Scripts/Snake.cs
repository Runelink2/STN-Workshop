using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System;

public enum SnakeGameState {
    NotStarted,
    Running,
    Ended
}

public class Snake : MonoBehaviour {
    
    public static Snake instance;

    [NonSerialized] public SnakeGameState gameState = SnakeGameState.NotStarted;
    [NonSerialized] public List <Food> food = new List<Food>();
    
    [SerializeField] Transform segmentPrefab;
    [SerializeField] Vector2Int direction = Vector2Int.right;
    [SerializeField] float speed = 20f;
    [SerializeField] float speedIncrease = 1f;
    [SerializeField] int initialSegments = 4;
    [SerializeField] int segmentsPerFood = 1;
    [SerializeField] bool moveThroughWalls = false;
    [SerializeField] int pointsPerFood = 10;
    
    [SerializeField] GameObject highScorePanel;
    [SerializeField] Text scoreText;
    [SerializeField] Text highScoreText;
    [SerializeField] Text highScoreTitleText;
    [SerializeField] Text endScreenScoreText;
    
    SpriteRenderer snakeSprite;
    List<Transform> segments = new List<Transform>();
    Queue<Vector2Int> inputQueue = new Queue<Vector2Int>();
    
    float startSpeed;
    float nextUpdate;
    int currentScore = 0;
    int highScore = 0;
    
    bool newHighScore = false;
    float gameEndTime = -99f;

    void Awake() {
        if (instance != null && instance != this) { Destroy(gameObject); } else { instance = this; }
    }

    void Start() {
        highScore = PlayerPrefs.GetInt("snakeHighScore", 0);
        
        snakeSprite = GetComponent<SpriteRenderer>();
        startSpeed = speed;
        
        SetGameState(SnakeGameState.NotStarted);
        Initialize();
    }

    private void Update() {
        if (newHighScore) {
            float sin = Mathf.Sin(Time.time * 4f);
            highScoreText.transform.localScale = Vector3.one * 0.5f * (1f + sin * 0.15f);
        }
        
        ProcessInput();
    }

    private void ProcessInput() {
        bool up = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
        bool down = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
        bool left = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
        bool right = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);

        if (gameState != SnakeGameState.Running) {
            if (Time.time - gameEndTime < 0.3f) {
                return;
            }

            if (up || down || left || right) {
                if (gameState == SnakeGameState.Ended) {
                    Initialize();
                }

                highScorePanel.SetActive(false);
                SetGameState(SnakeGameState.Running);
            }
        }

        if (inputQueue.Count < 3) {
            if (up) inputQueue.Enqueue(Vector2Int.up);
            if (down) inputQueue.Enqueue(Vector2Int.down);
            if (left) inputQueue.Enqueue(Vector2Int.left);
            if (right) inputQueue.Enqueue(Vector2Int.right);
        }
    }

    private void FixedUpdate() {
        if (gameState != SnakeGameState.Running || Time.fixedTime < nextUpdate) {
            return;
        }

        while (inputQueue.Count > 0) {
            Vector2Int nextInput = inputQueue.Dequeue();
            if (nextInput != -direction) { // Prevent reversing direction
                direction = nextInput;
                break;  // Only apply one direction change per movement
            }
        }

        // Move the segments
        for (int i = segments.Count - 1; i > 0; i--) {
            segments[i].localPosition = segments[i - 1].localPosition;
        }

        int x = Mathf.RoundToInt(transform.localPosition.x) + direction.x;
        int y = Mathf.RoundToInt(transform.localPosition.y) + direction.y;
        transform.localPosition = new Vector2(x, y);

        nextUpdate = Time.fixedTime + (1f / speed);
    }

    public void Grow(int amnt = 1) {
        for (int i = 0; i < amnt; i++) {
            Transform segment = Instantiate(segmentPrefab, transform.parent);
            segment.localPosition = segments[segments.Count - 1].localPosition;
            segments.Add(segment);
        }
        speed += speedIncrease;
    }
    
    public bool Occupies(int x, int y) {
        foreach (Transform segment in segments) {
            int roundedX = Mathf.RoundToInt(segment.localPosition.x);
            int roundedY = Mathf.RoundToInt(segment.localPosition.y);
            if (roundedX == x && roundedY == y) {
                return true;
            }
        }
        return false;
    }

    public void Initialize() {
        highScorePanel.SetActive(false);
        inputQueue.Clear();
        direction = Vector2Int.zero;
        transform.localPosition = Vector3.zero;
        speed = startSpeed;
        currentScore = 0;
        UpdateScoreText();

        for (int i = 1; i < segments.Count; i++) {
            Destroy(segments[i].gameObject);
        }

        segments.Clear();
        segments.Add(transform);

        Grow(initialSegments);
        scoreText.transform.parent.gameObject.SetActive(true);
        scoreText.text = currentScore.ToString();
    }

    private void UpdateScoreText() {
        if (scoreText != null) scoreText.text = currentScore.ToString ();
    }

    private void ShowHighScoreScreen() {
        SetGameState(SnakeGameState.Ended);
        newHighScore = currentScore > highScore;
        gameEndTime = Time.time;
        
        if (newHighScore) {
            highScore = currentScore;

            PlayerPrefs.SetInt("snakeHighScore", highScore);
            PlayerPrefs.Save();
        }
        
        highScorePanel.SetActive(true);
        Color orange = new Color(1f, 0.5f, 0f);
        highScoreTitleText.text = newHighScore ? "New High Score" : "High Score";
        highScoreTitleText.color = newHighScore ? orange : Color.red;
        highScoreTitleText.fontStyle = newHighScore ? FontStyle.Italic : FontStyle.Normal;
        highScoreText.text = highScore.ToString();
        endScreenScoreText.text = currentScore.ToString();
        scoreText.transform.parent.gameObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other) {
        if (gameState == SnakeGameState.Ended || gameState == SnakeGameState.NotStarted) return;

        if (other.gameObject.CompareTag("Food")) {
            Grow(segmentsPerFood);
            currentScore += pointsPerFood;
            UpdateScoreText();
        } else if ((other.gameObject.CompareTag("Obstacle") || other.gameObject.CompareTag("Wall")) && !moveThroughWalls) {
            ShowHighScoreScreen();
        }
    }

    private void SetGameState(SnakeGameState newState) {
        gameState = newState;
        SpriteVisibility();
    }
    
    void SpriteVisibility() {
        bool spritesVisible = gameState == SnakeGameState.Running || gameState == SnakeGameState.NotStarted;
        
        snakeSprite.enabled = spritesVisible;
        
        foreach (Transform segment in segments)
            segment.GetComponent<SpriteRenderer>().enabled = spritesVisible;
        
        foreach (Food food in food)
            food.foodSprite.enabled = spritesVisible;
    }
}