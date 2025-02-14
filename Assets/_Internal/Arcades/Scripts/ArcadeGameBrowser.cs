using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine.EventSystems;

public class ArcadeGameBrowser : MonoBehaviour {
    public Text gameNameText;
    public Button leftArrowButton;
    public Button rightArrowButton;
    public Button exitGameButton;
    public Button insertCoinButton;
    public GameObject browser;

    private List<ArcadeMachineSettings> gameSettings = new List<ArcadeMachineSettings>();
    private int currentIndex = 0;

    void Start() {
        if (Application.isEditor == false) {
            Destroy(gameObject);
            return;
        }
        LoadAllGameSettings();
        UpdateArcade();

        leftArrowButton.onClick.AddListener(() => ChangeGame(-1));
        rightArrowButton.onClick.AddListener(() => ChangeGame(1));
        exitGameButton.onClick.AddListener(() => ArcadeMachine.instance.CloseArcade());
        insertCoinButton.onClick.AddListener(() => ArcadeMachine.instance.OpenArcade());
        exitGameButton.gameObject.SetActive(false);
    }
    
    public void OpenCloseArcade (bool open) {
        browser.SetActive(!open);
        exitGameButton.gameObject.SetActive(open);
    }

    void LoadAllGameSettings() {
        gameSettings.Clear();

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:ArcadeMachineSettings");
        foreach (var guid in guids) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var settings = AssetDatabase.LoadAssetAtPath<ArcadeMachineSettings>(path);
            if (settings != null) {
                gameSettings.Add(settings);
            }
        }
#endif

        gameSettings = gameSettings.OrderBy(x => x.gameName).ToList();

        if (gameSettings.Count == 0) {
            Debug.LogWarning("No ArcadeMachineSettings assets found!");
        }
    }

    void UpdateArcade() {
        if (gameSettings.Count == 0) {
            gameNameText.text = "No games available.";
            return;
        }

        var currentSettings = gameSettings[currentIndex];
        gameNameText.text = currentSettings.gameName;
        
        ArcadeMachine arcadeMachine = FindObjectOfType<ArcadeMachine>();
        if (arcadeMachine != null) {
            arcadeMachine.settings = currentSettings;
            arcadeMachine.ArcadeMachineVisualSetup();
        }
    }

    void ChangeGame(int direction) {
        if (gameSettings.Count == 0) {
            Debug.LogWarning("No games available.");
            return;
        } 
        EventSystem.current.SetSelectedGameObject(null); // Deselect button

        currentIndex += direction;

        // Wrap around the index if it goes out of range
        if (currentIndex < 0) currentIndex = gameSettings.Count - 1;
        if (currentIndex >= gameSettings.Count) currentIndex = 0;

        UpdateArcade();
    }
}