using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class ArcadeMachine : MonoBehaviour {
	public static ArcadeMachine instance;
	public ArcadeMachineSettings _settings;
	public ArcadeMachineSettings settings {
		get {
			return _settings;
		}
		set {
			//Debug.Log ($"Setting settings: {value.gameName}");
			_settings = value;
		}
	}
	[Space]
	[SerializeField] MeshRenderer arcadeMachineScreen;
	[SerializeField]  MeshRenderer arcadeMachineInsertCoin;
	[SerializeField] MeshRenderer arcadeMachineModel;
	[SerializeField] Light arcadeMachineLight;
	
	GameObject arcadePrefabInstance;
	
	ArcadeGameBrowser _gameBrowser;
	ArcadeGameBrowser gameBrowser {
		get { return _gameBrowser ?? (_gameBrowser = FindObjectOfType<ArcadeGameBrowser>()); }
	}
	
	void Awake () {
		instance = this;
	}

    void Update() {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.E)) {
			if (arcadePrefabInstance == null) {
				OpenArcade();
			}
		}
		if (Input.GetKeyDown(KeyCode.Escape)) {
			if (arcadePrefabInstance != null) {
				CloseArcade();
			}
		}
    }

    public void ArcadeMachineVisualSetup () {
		if (arcadeMachineScreen) {
			arcadeMachineScreen.gameObject.SetActive (false);
		}
		if (arcadeMachineInsertCoin) {
			arcadeMachineInsertCoin.material.mainTexture = settings.insertCoinTexture;
		}
		if (arcadeMachineModel) {
			arcadeMachineModel.material.mainTexture = settings.arcadeTexture;
			arcadeMachineModel.material.SetTexture("_EmissionMap", settings.arcadeEmission);
		}
		if (arcadeMachineLight) {
			arcadeMachineLight.color = settings.arcadeMachineColor;
		}
	}
	
	public void OpenArcade () {
		if (settings == null) {
			Debug.LogError("No arcade machine settings set in arcade machine");
			return;
		}
		if (settings.arcadeGamePrefab == null) {
			Debug.LogError("No arcade game prefab set in arcade machine settings");
			return;
		}
		arcadePrefabInstance = Instantiate (settings.arcadeGamePrefab, Vector3.down * 100, Quaternion.identity, transform);
		arcadeMachineScreen.gameObject.SetActive (true);
		
		if (Application.isEditor) {
			gameBrowser.OpenCloseArcade (true);
		}
	}

	public void CloseArcade () {
		arcadeMachineScreen.gameObject.SetActive (false);
		if (arcadePrefabInstance) {
			Destroy (arcadePrefabInstance);
		}
		
		if (Application.isEditor) {
			gameBrowser.OpenCloseArcade (false);
		}
	}
}