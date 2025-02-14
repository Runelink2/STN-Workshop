using UnityEngine;
using UnityEditor;
using System.IO;

[CreateAssetMenu(fileName = "ArcadeMachineSettings", menuName = "ScriptableObjects/ArcadeMachineSettings")]
public class ArcadeMachineSettings : ScriptableObject {
    [SerializeField, HideInInspector] private string machineFolderPath;
    
    [Header ("Adjustable Settings")]
    public string gameName = "Arcade Game";
    public Color arcadeMachineColor;
    
    [Header ("Asset Names")]
    public string arcadeGamePrefabName = "ArcadeGame";
    public string arcadeTextureName = "txt_Arcade";
    public string arcadeEmissionName = "txt_Arcade_em";
    public string insertCoinTextureName = "txt_InsertCoin";
    [Space]
    [ReadOnly] public GameObject arcadeGamePrefab;
    [ReadOnly] public Texture2D arcadeTexture;
    [ReadOnly] public Texture2D arcadeEmission;
    [ReadOnly] public Texture2D insertCoinTexture;


#if UNITY_EDITOR
    private void OnValidate() {
        string currentPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(this));
        if (currentPath != machineFolderPath) {
            machineFolderPath = currentPath;
            AssignAssets();
            Debug.Log($"Folder change detected for '{name}'. Assets have been reassigned.");
        }
    }

    public void AssignAssets() {
        insertCoinTexture = LoadAsset<Texture2D>(insertCoinTextureName);
        arcadeTexture = LoadAsset<Texture2D>(arcadeTextureName);
        arcadeEmission = LoadAsset<Texture2D>(arcadeEmissionName);
        arcadeGamePrefab = LoadAsset<GameObject>(arcadeGamePrefabName);

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        Debug.Log($"Assets assigned for {machineFolderPath}");
    }
    
    private T LoadAsset<T>(string fileNameWithoutExtension) where T : Object {
        if (string.IsNullOrEmpty(machineFolderPath)) return null;

        if (typeof(T) == typeof(GameObject)) {
            string prefabPath = Path.Combine(machineFolderPath, fileNameWithoutExtension + ".prefab");
            return AssetDatabase.LoadAssetAtPath<T>(prefabPath);
        }

        string[] textureExtensions = { ".png", ".tga", ".jpg", ".jpeg", ".psd" };

        foreach (string ext in textureExtensions) {
            string fullPath = Path.Combine(machineFolderPath, fileNameWithoutExtension + ext);
            T asset = AssetDatabase.LoadAssetAtPath<T>(fullPath);
            if (asset != null) return asset;
        }

        return null;
    }
#endif
}