using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using UnityEditor.SceneManagement;

[CustomEditor(typeof(ArcadeMachineSettings))]
public class ArcadeMachineSettingsEditor : Editor {

    public override void OnInspectorGUI() {
        ArcadeMachineSettings settings = (ArcadeMachineSettings)target;

        DrawDefaultInspector();
        
        GUILayout.Space(10);
        EditorGUILayout.HelpBox("Assigns assets by locating files that match the specified asset names.", MessageType.Info);
        if (GUILayout.Button("Populate Assets")) {
            settings.AssignAssets();
        }
        
        GUILayout.Space(15);
        
        if (GUILayout.Button("Export Arcade Unity Package")) {
            ExportParentFolder(settings);
        }
        if (GUILayout.Button("Build Game")) {
            BuildGame(settings);
        }
    }
    
    private void ExportParentFolder(ArcadeMachineSettings settings) {
        string assetPath = AssetDatabase.GetAssetPath(settings);
        if (string.IsNullOrEmpty(assetPath)) {
            Debug.LogError("Asset path could not be determined.");
            return;
        }

        // Go up two levels from the settings file
        string parentFolder = Path.GetDirectoryName(assetPath); // _GameSetup
        string grandParentFolder = Path.GetDirectoryName(parentFolder); // Arcade machine's root folder

        if (!string.IsNullOrEmpty(grandParentFolder) && Directory.Exists(grandParentFolder)) {
            // Open the export package window with recursive option
            string relativePath = grandParentFolder.Replace("\\", "/");
            AssetDatabase.ExportPackage(relativePath, $"{Path.GetFileName(grandParentFolder)}.unitypackage", ExportPackageOptions.Recurse | ExportPackageOptions.Interactive);
            Debug.Log($"Opened export package dialog for folder: {relativePath}");
        } else {
            Debug.LogError("Parent folder could not be found or does not exist.");
        }
    }
    
    private void BuildGame(ArcadeMachineSettings settings) {
        string assetPath = AssetDatabase.GetAssetPath(settings);
        if (string.IsNullOrEmpty(assetPath)) {
            Debug.LogError("Asset path could not be determined.");
            return;
        }

        string parentFolder = Path.GetDirectoryName(assetPath);
        string grandParentFolder = Path.GetDirectoryName(parentFolder);

        if (string.IsNullOrEmpty(grandParentFolder) || !Directory.Exists(grandParentFolder)) {
            Debug.LogError("Parent folder could not be found or does not exist.");
            return;
        }

        string buildFolder = Path.Combine(Application.dataPath, "..", "Builds", Path.GetFileName(grandParentFolder));
        string buildName = settings.gameName.Replace(" ", "_");
        string buildPath = Path.Combine(buildFolder, buildName + ".exe");

        if (!Directory.Exists(buildFolder)) {
            Directory.CreateDirectory(buildFolder);
        }

        List<string> scenes = new List<string>();
        foreach (var scene in EditorBuildSettings.scenes) {
            if (scene.enabled) {
                scenes.Add(scene.path);
            }
        }

        if (scenes.Count == 0) {
            Debug.LogError("No scenes found in Build Settings.");
            return;
        }

        var arcadeMachine = FindObjectOfType<ArcadeMachine>();
        if (arcadeMachine == null) {
            Debug.LogError("ArcadeMachine instance not found in the scene.");
            return;
        }

        var originalSettings = arcadeMachine.settings;
        arcadeMachine.settings = settings;
        arcadeMachine.ArcadeMachineVisualSetup();
        EditorUtility.SetDirty(arcadeMachine);
        
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        BuildPlayerOptions buildOptions = new BuildPlayerOptions {
            scenes = scenes.ToArray(),
            locationPathName = buildPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildPipeline.BuildPlayer(buildOptions);
        OpenFolder(buildFolder);
    }
    
    private void OpenFolder(string folderPath) {
        if (Directory.Exists(folderPath)) {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                FileName = folderPath,
                UseShellExecute = true
            });
        } else {
            Debug.LogError($"Folder does not exist: {folderPath}");
        }
    }
}