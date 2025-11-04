#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Compilation;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using System.Text.RegularExpressions;
using Object = UnityEngine.Object;
using STN.ModSDK;

public class ModSDKWindow : EditorWindow
{
    // ---------- UI state ----------
    [Serializable] public class PublicEntry { public string address; public string type; public string labels; public string role; public string meta; public string structure; public string notes; }
    [Serializable] public class PublicCatalog { public List<PublicEntry> entries = new List<PublicEntry>(); }
    [Serializable] public class PublicRowsWrapper { public PublicEntry[] rows; }

    [Serializable]
    private class PrefabNode
    {
        public string path;
        public Vector3 lp;
        public Vector3 lr;
        public Vector3 ls;
        public bool hasBounds;
        public Vector3 bCenter;
        public Vector3 bSize;
        public Vector3 pivotOffset;
        public Vector3 pivotNorm;
        public List<BoxCol> boxes;
    }

    [Serializable]
    private class PrefabStructure
    {
        public Vector3 rootScale;
        public List<PrefabNode> nodes;
    }

    [Serializable]
    private class BoxCol
    {
        public Vector3 center;
        public Vector3 size;
        public bool isTrigger;
    }

    private const string DefaultPublicJsonPath = "Assets/_Internal/Json/public_metadata.json";
    private PublicCatalog publicCatalog = new PublicCatalog();
    private string publicJsonPath = DefaultPublicJsonPath;

    private string modName = "MyMod";            // Active mod context
    private string newModName = "MyMod";         // Name used in Create New Mod UI only
    private string modVersion = "1.0.0";         // Version used when building (user-provided)
    private string targetGroupName = "Mod_PublicAssets";
    private string search = "";
    private string modOutputDir = ""; // absolute folder where per-mod catalog/bundles are written
    private string filterLabel = "";
    private string filterFolder = ""; // prefix match on address path like "Weapons/357Magnum"
    private string selectedAddress = null;
    private HashSet<string> treeExpanded = new HashSet<string>();

    private Vector2 scrollAssign;
    private Vector2 scrollValidate;
        private Vector2 scrollOverrides;
        private string searchOverrides = "";
		private HashSet<string> overridesExpanded = new HashSet<string>();
		private static readonly Color TintOverride = new Color(0.45f, 1.00f, 0.45f, 1f);
		private static readonly Color TintSelected = new Color(1.00f, 0.85f, 0.35f, 1f);

    // ---------- ModInfo flags (client/server) ----------
    private bool modIsClient = true;   // default to client-enabled for back-compat
    private bool modIsServer = false;  // default to server-disabled for back-compat
    private string flagsLoadedForMod = null; // track which mod flags were loaded for

	// ---------- Working root (configurable) ----------
	private const string DefaultWorkingRootName = "My_Mods";
	private const string LegacyWorkingRootName = "_ModSDK_Temp";

	// ---------- Caches / perf ----------
	// Public list versioning (bumped when JSON is reloaded)
	private int publicVersion = 0;
	// Cache for filtered list + built tree
	private string cacheSearch = "";
	private string cacheLabel = "";
	private string cacheFolder = "";
	private int cacheVersion = -1;
	private List<PublicEntry> cacheFiltered = null;
	private TreeNode cacheRoot = null;
	// Cache for override map (Addressables); throttled
	private Dictionary<string, string> cachedOverrideMap = null;
	private double lastOverrideRefresh = -1;
	private string cachedOverrideForMod = null;
	private const double OverrideRefreshInterval = 0.35; // seconds
	private const float BottomRowFixedHeight = 180f; // px height for Validate/Build row
	private const float ValidationPanelFixedWidth = 420f; // px width for Validate panel

	// ---------- Validation state ----------
	private List<string> lastValidationIssues = new List<string>();
	private bool lastValidationPassed = false;

    // ---------- Menu ----------
    [MenuItem("Modding/Open Mod SDK")]
    public static void Open() => GetWindow<ModSDKWindow>("Mod SDK");

    // Quick creator for weapon settings (GunConfig ScriptableObject)
    [MenuItem("Modding/Create Weapon Settings (GunConfig)")]
    public static void CreateWeaponSettings()
    {
        try
        {
            var targetDir = "Assets";
            if (Selection.activeObject != null)
            {
                var selPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(selPath))
                {
                    if (System.IO.Directory.Exists(selPath)) targetDir = selPath;
                    else
                    {
                        var dir = System.IO.Path.GetDirectoryName(selPath);
                        if (!string.IsNullOrEmpty(dir)) targetDir = dir;
                    }
                }
                }
            var defaultPath = System.IO.Path.Combine(targetDir, "ClientWeapon.asset");
            defaultPath = AssetDatabase.GenerateUniqueAssetPath(defaultPath);

            var asset = ScriptableObject.CreateInstance<GunConfig>();
            AssetDatabase.CreateAsset(asset, defaultPath);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
            Debug.Log($"[ModSDK] Created Weapon Settings asset at '{defaultPath}'.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ModSDK] Failed to create Weapon Settings: {ex.Message}");
        }
    }

    private void OnEnable()
    {
        // Force the path to the canonical location to avoid stale serialized values
        publicJsonPath = DefaultPublicJsonPath;
        LoadPublicAddresses();
        AutoSelectFirstModIfAvailable();
        // Load flags for initial mod selection
        flagsLoadedForMod = null; // force reload on first EnsureModFlagsLoaded
        // Ensure UI reflects Project selection changes immediately
        try { Selection.selectionChanged += Repaint; } catch { }
    }

    private void OnDisable()
    {
        try { Selection.selectionChanged -= Repaint; } catch { }
    }

    // ---------- GUI ----------
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Mod SDK", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

		// Config
		using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
		{
			// Two-column flow: Load Existing vs Create New
			using (new EditorGUILayout.HorizontalScope())
			{
				// Load Existing Mod
				using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
				{
					EditorGUILayout.LabelField("Load Existing Mod", EditorStyles.boldLabel);
					var mods = GetAvailableModNames();
					using (new EditorGUILayout.HorizontalScope())
					{
						EditorGUILayout.LabelField(new GUIContent("Available Mods", $"Detected mod working folders under {DefaultWorkingRootName} (and legacy)"), GUILayout.Width(120));
						if (mods.Count == 0)
						{
							EditorGUILayout.LabelField($"(none)", EditorStyles.miniLabel, GUILayout.Width(140));
							if (GUILayout.Button("Reveal", GUILayout.Width(64)))
							{
								var abs = GetAbsoluteFilePath(Path.Combine("Assets", DefaultWorkingRootName));
								if (!string.IsNullOrEmpty(abs))
								{
									if (!Directory.Exists(abs)) Directory.CreateDirectory(abs);
									EditorUtility.RevealInFinder(abs);
								}
							}
						}
						else
						{
							var current = San(modName);
							var idx = Mathf.Max(0, mods.IndexOf(current));
							var newIdx = EditorGUILayout.Popup(idx, mods.ToArray(), GUILayout.Width(220));
							var selectedMod = mods[Mathf.Clamp(newIdx, 0, mods.Count - 1)];
							if (newIdx != idx && newIdx >= 0 && newIdx < mods.Count)
							{
								modName = selectedMod;
								Repaint();
							}
							if (GUILayout.Button("Refresh", GUILayout.Width(56)))
							{
								modName = selectedMod;
								// Force override cache refresh so UI updates immediately
								cachedOverrideMap = null;
								lastOverrideRefresh = -1;
								Repaint();
							}
							// Reveal button removed; use "Reveal Mod Folder" in the status row below
						}
					}
					using (new EditorGUILayout.HorizontalScope())
					{
						if (GUILayout.Button(new GUIContent("Load From Folder…", $"Pick a working folder under Assets/{DefaultWorkingRootName} (or legacy)"), GUILayout.Width(220)))
						{
							var preferred = Path.Combine(Application.dataPath, San(DefaultWorkingRootName));
							var legacy = Path.Combine(Application.dataPath, LegacyWorkingRootName);
							var baseTemp = Directory.Exists(preferred) ? preferred : legacy;
							Directory.CreateDirectory(baseTemp);
							var picked = EditorUtility.OpenFolderPanel("Select Mod Working Folder", baseTemp, "");
							if (!string.IsNullOrEmpty(picked))
							{
								var hasSettings = File.Exists(Path.Combine(picked, "AddressableAssetSettings.asset"));
								if (hasSettings)
								{
									modName = San(new DirectoryInfo(picked).Name);
									Repaint();
								}
								else
								{
									EditorUtility.DisplayDialog("Not a Mod Settings Folder", "The selected folder does not contain AddressableAssetSettings.asset.", "OK");
								}
							}
					}
					}

					EditorGUILayout.Space(2);
					// Active mod status (moved here for clearer context)
					{
						var activeNew = Path.Combine(GetWorkingRootAssetPath(), San(modName), "AddressableAssetSettings.asset");
						var activeLegacy = Path.Combine(LegacyRootAssetPath(), San(modName), "AddressableAssetSettings.asset");
						bool activeInNew = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(activeNew) != null;
						bool activeInLegacy = !activeInNew && AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(activeLegacy) != null;
						bool hasActive = activeInNew || activeInLegacy;
						using (new EditorGUILayout.HorizontalScope())
						{
							if (hasActive)
							{
								var root = activeInNew ? GetWorkingRootAssetPath() : LegacyRootAssetPath();
								var folderAsset = Path.Combine(root, San(modName));
								EditorGUILayout.LabelField($"Active Mod: {folderAsset}", EditorStyles.miniBoldLabel);
								//GUILayout.FlexibleSpace();
								if (GUILayout.Button("Reveal Mod Folder", GUILayout.Width(140)))
								{
									var abs = GetAbsoluteFilePath(folderAsset);
									if (!string.IsNullOrEmpty(abs)) EditorUtility.RevealInFinder(abs);
								}
							}
							else
							{
								EditorGUILayout.LabelField("Active Mod: (none)", EditorStyles.miniBoldLabel);
							}
						}
					}
				}

				// Create New Mod (left) + Public addresses metadata (right)
				using (new EditorGUILayout.HorizontalScope())
				{
					// Left: Create New Mod
					using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
					{
						EditorGUILayout.LabelField("Create New Mod", EditorStyles.boldLabel);
						newModName = EditorGUILayout.TextField("Mod Name", newModName);
						modVersion = EditorGUILayout.TextField("Mod Version", modVersion);
						GUILayout.Space (6);
						using (new EditorGUILayout.HorizontalScope())
						{
							//GUILayout.FlexibleSpace();
							if (GUILayout.Button(new GUIContent("Create Mod", $"Create per‑mod working folder and Addressables settings in {DefaultWorkingRootName}/<ModName>"), GUILayout.Width(160)))
							{
								var newName = San(string.IsNullOrEmpty(newModName) ? "MyMod" : newModName);
								// If a mod with this name already exists, offer to switch or reveal instead of recreating
								if (TryFindPerModSettings(newName, out var existingSettings, out var existingFolder))
								{
									var choice = EditorUtility.DisplayDialogComplex("Mod already exists",
										$"A mod named '{newName}' already exists.",
										"Switch to it",
										"Cancel",
										"Reveal Folder");
									if (choice == 0)
									{
										modName = newName;
										Repaint();
									}
									else if (choice == 2)
									{
										var abs = GetAbsoluteFilePath(existingFolder);
										if (!string.IsNullOrEmpty(abs)) EditorUtility.RevealInFinder(abs);
									}
								}
								else
								{
									var settings = GetOrCreatePerModSettingsFor(newName);
									if (settings != null)
									{
									// Ensure our working group exists and has required schemas
									var group = EnsureGroup(settings, targetGroupName);
									var bundled = group.GetSchema<BundledAssetGroupSchema>() ?? group.AddSchema<BundledAssetGroupSchema>();
									bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
									bundled.BundleMode  = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
									var update = group.GetSchema<ContentUpdateGroupSchema>() ?? group.AddSchema<ContentUpdateGroupSchema>();
									update.StaticContent = false;

									// Create and set a local profile that points to a dummy path under the mod temp (not used for build output)
									var profiles = settings.profileSettings;
									var profileId = settings.activeProfileId;
									if (string.IsNullOrEmpty(profileId)) profileId = profiles.AddProfile("Mod", null);
									settings.activeProfileId = profileId;
									var modTempRoot = Path.Combine(GetWorkingRootAssetPath(), newName).Replace('\\','/');
									EnsureProfileVariable(settings, AddressableAssetSettings.kLocalBuildPath, modTempRoot);
									EnsureProfileVariable(settings, AddressableAssetSettings.kLocalLoadPath, modTempRoot);
									bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
									bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
									settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupSchemaModified, group, true, false);
									AssetDatabase.SaveAssets();
									Debug.Log($"[ModSDK] Mod context ready: '{newName}' → {GetWorkingRootAssetPath()}/{newName}");
									// Offer to switch to the newly created mod
									var after = EditorUtility.DisplayDialogComplex("Mod created",
										$"Mod '{newName}' was created. Switch to it now?",
										"Switch",
										"Stay",
										"Reveal Folder");
									if (after == 0)
									{
										modName = newName;
										Repaint();
									}
									else if (after == 2)
									{
										var folder = Path.Combine(GetWorkingRootAssetPath(), newName);
										var abs = GetAbsoluteFilePath(folder);
										if (!string.IsNullOrEmpty(abs)) EditorUtility.RevealInFinder(abs);
									}
								}
							}
							}
							GUILayout.FlexibleSpace();
						}
					}

					// Right: Public addresses metadata
					using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(420)))
					{
						EditorGUILayout.LabelField("Public addresses metadata", EditorStyles.boldLabel);
						using (new EditorGUILayout.HorizontalScope())
						{
							EditorGUILayout.LabelField(new GUIContent("JSON Path", "JSON file containing the game's public address list used for assignments"), GUILayout.Width(72));
							EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(publicJsonPath) ? "(not set)" : publicJsonPath, GUILayout.Height(16), GUILayout.MaxWidth(340));
						}
						EditorGUILayout.HelpBox("This JSON lists public asset addresses, types, roles, and metadata that mods can use or override. Reload if the file changes.", MessageType.None);
						using (new EditorGUILayout.HorizontalScope())
						{
							if (GUILayout.Button("Reload", GUILayout.Width(80))) LoadPublicAddresses();
							if (GUILayout.Button("Reveal File", GUILayout.Width(90)))
							{
								var abs = GetAbsoluteFilePath(publicJsonPath);
								if (!string.IsNullOrEmpty(abs))
								{
									if (File.Exists(abs)) EditorUtility.RevealInFinder(abs);
									else EditorUtility.RevealInFinder(Path.GetDirectoryName(abs));
								}
							}
							GUILayout.FlexibleSpace();
						}
					}
				}
		}
			// (moved) Public addresses metadata now appears alongside Create New Mod above
			// Active mod status moved above, alongside Load Existing
        }

		// Gate the rest of the UI until a mod context is selected/available
		var perModSettingsPathNew = Path.Combine(GetWorkingRootAssetPath(), San(modName), "AddressableAssetSettings.asset");
		var perModSettingsPathLegacy = Path.Combine(LegacyRootAssetPath(), San(modName), "AddressableAssetSettings.asset");
		bool hasModContext = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(perModSettingsPathNew) != null ||
			AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(perModSettingsPathLegacy) != null;
        if (!hasModContext)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox("Select or create a mod above to continue.", MessageType.Info);
            return;
        }
        else
        {
            // keep flags in sync with the active mod selection
            EnsureModFlagsLoaded();
        }

        // ASSIGN + OVERRIDES side-by-side
        EditorGUILayout.Space(4);
		using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true)))
		using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
        {
			// Fixed 50/50 split for left/right columns
			var colWidth = Mathf.Max(120f, (position.width - 8f) * 0.5f);
			// LEFT: Assign addresses
			using (new EditorGUILayout.VerticalScope(GUILayout.Width(colWidth), GUILayout.ExpandHeight(true)))
            {
				EditorGUILayout.LabelField("Addressables Browser", EditorStyles.boldLabel);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandHeight(true)))
                {
                    search = EditorGUILayout.TextField("Search addresses", search);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        filterLabel = EditorGUILayout.TextField(new GUIContent("Label contains", "Filter entries whose labels contains this text"), filterLabel);
                        filterFolder = EditorGUILayout.TextField(new GUIContent("Folder prefix", "Filter entries whose address starts with this folder path/prefix"), filterFolder);
                    }

                    EnsureFilteredTreeUpToDate();
                    var filtered = cacheFiltered ?? new List<PublicEntry>();

                    // Compute overrides info for controls and rendering
                    var overrideMap = GetOverrideMapCached();
                    var overrideFolders = GetOverrideFolderSet(overrideMap);

                    EditorGUILayout.LabelField($"Matching: {filtered.Count} entries", EditorStyles.miniLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Expand All", GUILayout.Width(100)))
                            treeExpanded = new HashSet<string>(GetAllFolderPaths(filtered));
                        if (GUILayout.Button("Expand Overrides", GUILayout.Width(140)))
                            treeExpanded = new HashSet<string>(overrideFolders);
                        if (GUILayout.Button("Collapse All", GUILayout.Width(100)))
                            treeExpanded.Clear();
                        GUILayout.FlexibleSpace();
                    }

                    using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true)))
                    {
                        scrollAssign = EditorGUILayout.BeginScrollView(scrollAssign, GUILayout.ExpandHeight(true));
                        var root = cacheRoot ?? BuildAddressTree(filtered);
                        DrawAddressTree(root, 0, overrideMap, overrideFolders);
                        EditorGUILayout.EndScrollView();
                    }

				// Enable only when an address is highlighted AND at least one asset is selected in Project view
                EditorGUILayout.HelpBox("Tip: You can drag and drop assets from the Project view directly onto an address in the list above to assign them. Or: click an address to highlight it, select an asset in the Project and press 'Assign Selected To Highlighted'.", MessageType.None);
                
				EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(selectedAddress) || GetSelectedGuids(true).Count == 0);
				if (GUILayout.Button("Assign Selected To Highlighted"))
                    {
					if (!string.IsNullOrEmpty(selectedAddress))
                        {
                            var entry = publicCatalog.entries.FirstOrDefault(e => string.Equals(e.address, selectedAddress, StringComparison.Ordinal));
                            if (entry != null) AssignSelectedToAddress(entry);
                            else Debug.LogWarning("Selected address not found.");
                        }
                        else Debug.LogWarning("Select an address from the list first.");
                    }
				EditorGUI.EndDisabledGroup();
				EditorGUILayout.HelpBox("Select a Model/Prefab addressabl and click 'Build Model/Prefab Preview' to spawn a temporary preview that reconstructs the object hierarchy with transforms, box colliders, model bounds, and correct mesh pivots (baked offsets).", MessageType.None);

				// Enable only when an address is highlighted AND it has meta or structure AND is a model/prefab-like type
				bool canPreview = false;
				if (!string.IsNullOrEmpty(selectedAddress))
				{
					var entryPeek = publicCatalog.entries.FirstOrDefault(e => string.Equals(e.address, selectedAddress, StringComparison.Ordinal));
					if (entryPeek != null)
					{
						bool hasData = !string.IsNullOrEmpty(entryPeek.structure) || !string.IsNullOrEmpty(entryPeek.meta);
						bool isModelOrPrefab =
							string.Equals(entryPeek.type, "Model", StringComparison.OrdinalIgnoreCase) ||
							string.Equals(entryPeek.type, "GameObject", StringComparison.OrdinalIgnoreCase) ||
							string.Equals(entryPeek.role, "Model", StringComparison.OrdinalIgnoreCase) ||
							string.Equals(entryPeek.role, "Prefab", StringComparison.OrdinalIgnoreCase);
						canPreview = hasData && isModelOrPrefab;
					}
				}
				EditorGUI.BeginDisabledGroup(!canPreview);
				if (GUILayout.Button("Build Model/Prefab Preview"))
				{
					if (!string.IsNullOrEmpty(selectedAddress))
					{
						var entry = publicCatalog.entries.FirstOrDefault(e => string.Equals(e.address, selectedAddress, StringComparison.Ordinal));
						if (entry != null) BuildPreviewFromMeta(entry);
						else Debug.LogWarning("Selected address not found.");
					}
					else Debug.LogWarning("Select an address from the list first.");
				}
				EditorGUI.EndDisabledGroup();

                }
            }


			// RIGHT: Current overrides
			using (new EditorGUILayout.VerticalScope(GUILayout.Width(colWidth), GUILayout.ExpandHeight(true)))
            {
				EditorGUILayout.LabelField("Mod Overrides", EditorStyles.boldLabel);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandHeight(true)))
                {
                    searchOverrides = EditorGUILayout.TextField("Search", searchOverrides);
                    var overrides = GetOverrideMapCached();
                    if (!string.IsNullOrEmpty(searchOverrides))
                    {
                        var q = searchOverrides.Trim().ToLowerInvariant();
                        overrides = overrides
                            .Where(kv => (kv.Key ?? "").ToLowerInvariant().Contains(q) || (kv.Value ?? "").ToLowerInvariant().Contains(q))
                            .ToDictionary(k => k.Key, v => v.Value);
                    }
                    EditorGUILayout.LabelField($"Matching: {overrides.Count} overrides", EditorStyles.miniLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Expand All", GUILayout.Width(100)))
                        {
                            overridesExpanded = new HashSet<string>(GetOverrideFolderSet(overrides));
                        }
                        if (GUILayout.Button("Collapse All", GUILayout.Width(100)))
                        {
                            overridesExpanded.Clear();
                        }
                        GUILayout.FlexibleSpace();
                    }

                    using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true)))
                    {
                        scrollOverrides = EditorGUILayout.BeginScrollView(scrollOverrides, GUILayout.ExpandHeight(true));

                        // Build a folder tree from override addresses
                        var overrideEntries = overrides
                            .Select(kv => new PublicEntry { address = kv.Key, type = "", role = "", labels = "", meta = kv.Value })
                            .ToList();
                        var oRoot = BuildAddressTree(overrideEntries);
                        DrawOverridesTree(oRoot, 0, overrides);

                        EditorGUILayout.EndScrollView();
                    }
                    if (overrides.Count == 0)
                        EditorGUILayout.HelpBox("No overrides found for this mod.", MessageType.Info);
                }
            }
        }

        EditorGUILayout.Space(4);
        using (new EditorGUILayout.VerticalScope(GUILayout.Height(BottomRowFixedHeight)))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
				using (new EditorGUILayout.VerticalScope(GUILayout.Width(ValidationPanelFixedWidth)))
                {
                    EditorGUILayout.LabelField("Mod Validation", EditorStyles.boldLabel);
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Run Validation", GUILayout.Width(120)))
                        {
                            lastValidationPassed = ValidateMod(out var issues);
                            lastValidationIssues = issues ?? new List<string>();
                            Repaint();
                        }
                        if (GUILayout.Button("Clear", GUILayout.Width(72)))
                        {
                            lastValidationIssues = new List<string>();
                            lastValidationPassed = false;
                            Repaint();
                        }
                        GUILayout.FlexibleSpace();
                    }
                    scrollValidate = EditorGUILayout.BeginScrollView(scrollValidate, GUILayout.MinHeight(120), GUILayout.MaxHeight(240));
                    if (lastValidationIssues != null && lastValidationIssues.Count > 0)
                    {
                        var redStyle = new GUIStyle(EditorStyles.miniBoldLabel);
                        redStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);
                        EditorGUILayout.LabelField($"Issues: {lastValidationIssues.Count}", redStyle);
                        foreach (var line in lastValidationIssues)
                        {
                            var redWordWrapStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
                            redWordWrapStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);
                            EditorGUILayout.LabelField("• " + line, redWordWrapStyle);
                        }
                    }
                    else
                    {
                        var status = lastValidationPassed ? "Validation passed." : "Press Run Validation to analyze the current mod.";
                        EditorGUILayout.LabelField(status, EditorStyles.miniLabel);
                    }
                    EditorGUILayout.EndScrollView();
                    EditorGUILayout.HelpBox("Checks: duplicates, spaces in addresses, type mismatches vs. public list, non-public overrides.", MessageType.None);
                    }
                }

                GUILayout.Space(8);

                // RIGHT: Build mod
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    EditorGUILayout.LabelField("Build Mod", EditorStyles.boldLabel);
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        // Ensure mod flags are loaded for the current selection
                        EnsureModFlagsLoaded();

                        // Code Targets (Client/Server) toggles
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField("Code Targets", GUILayout.Width(100));
                            var newClient = EditorGUILayout.ToggleLeft(new GUIContent("Client", "Apply Harmony code on clients"), modIsClient, GUILayout.Width(80));
                            var newServer = EditorGUILayout.ToggleLeft(new GUIContent("Server", "Apply Harmony code on dedicated servers"), modIsServer, GUILayout.Width(80));
                            if (newClient != modIsClient || newServer != modIsServer)
                            {
                                modIsClient = newClient;
                                modIsServer = newServer;
                                SaveModFlagsToWorkingModInfo();
                            }
                            GUILayout.FlexibleSpace();
                        }
                        EditorGUILayout.Space(4);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(new GUIContent("Mod Output Folder", "Absolute path where the per-mod catalog and bundles will be built"), GUILayout.Width(140));
                            EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(modOutputDir) ? "(not set)" : modOutputDir, GUILayout.Height(16));
                            if (GUILayout.Button("Choose…", GUILayout.Width(80)))
                            {
                                var start = Directory.Exists(modOutputDir) ? modOutputDir : Application.dataPath;
                                var picked = EditorUtility.OpenFolderPanel("Choose Mod Output Folder", start, "");
                                if (!string.IsNullOrEmpty(picked)) modOutputDir = picked;
                            }
                        }
                        // Code packaging uses 'code/' under the selected mod automatically; no extra UI needed
                        if (GUILayout.Button("Build Mod"))
                            BuildMod();
                        EditorGUILayout.HelpBox("If Mod Output Folder is set, the SDK builds a separate catalog + bundles into that folder (per‑mod). If not set, it falls back to copying bundles under StreamingAssets/aa/<Platform>/mods/<ModName>/ and uses the platform catalog.", MessageType.Info);
                    }
                }
            }
        }
    }

    // ---------- Public list ----------
    private void LoadPublicAddresses()
    {
        try
        {
            publicCatalog = new PublicCatalog();
            string json = null;
            // Load via AssetDatabase first (Unity asset path)
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(publicJsonPath);
            if (ta != null) json = ta.text;
            else
            {
                var abs = GetAbsoluteFilePath(publicJsonPath);
                if (!string.IsNullOrEmpty(abs) && File.Exists(abs)) json = File.ReadAllText(abs);
                else Debug.LogWarning($"[ModSDK] Public metadata not found. Path='{publicJsonPath}'");
            }
            if (!string.IsNullOrWhiteSpace(json))
            {
                // Prefer structured wrapper with rows: [ { address, type, labels, role, meta } ]
                if (json.IndexOf("\"rows\"", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var wrapper = JsonUtility.FromJson<PublicRowsWrapper>(json);
                    publicCatalog.entries = (wrapper != null && wrapper.rows != null) ? new List<PublicEntry>(wrapper.rows) : new List<PublicEntry>();
                    if (publicCatalog.entries.Count == 0)
                        publicCatalog.entries = MiniJsonList(json);
                }
                else
                {
                    // Fallback: tiny JSON array format
                    publicCatalog.entries = MiniJsonList(json);
                }
            }
			Debug.Log($"[ModSDK] Loaded {publicCatalog.entries.Count} public addresses.");
			// Invalidate caches and bump version so filtered/tree recompute lazily
			publicVersion++;
			cacheVersion = -1;
			cacheRoot = null;
			cacheFiltered = null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ModSDK] Failed to load public addresses: {ex.Message}");
        }
    }

	private List<PublicEntry> FilterPublic(string q)
    {
        var list = publicCatalog.entries;
        if (string.IsNullOrWhiteSpace(q)) return list;
        q = q.Trim().ToLowerInvariant();
		return list.Where(e =>
			(e.address ?? "").ToLowerInvariant().Contains(q) ||
			(e.type    ?? "").ToLowerInvariant().Contains(q) ||
			(e.role    ?? "").ToLowerInvariant().Contains(q) ||
			(e.labels  ?? "").ToLowerInvariant().Contains(q) ||
			(e.meta    ?? "").ToLowerInvariant().Contains(q)).ToList();
    }

    private List<PublicEntry> FilterPublic(string text, string labelContains, string folderPrefix)
    {
        var q = (text ?? "").Trim().ToLowerInvariant();
        var labelQ = (labelContains ?? "").Trim().ToLowerInvariant();
        var folder = (folderPrefix ?? "").Replace('\\','/').Trim();
        var list = publicCatalog.entries;
        IEnumerable<PublicEntry> res = list;
		if (!string.IsNullOrEmpty(q))
			res = res.Where(e => (e.address ?? "").ToLowerInvariant().Contains(q) || (e.type ?? "").ToLowerInvariant().Contains(q) || (e.role ?? "").ToLowerInvariant().Contains(q) || (e.labels ?? "").ToLowerInvariant().Contains(q) || (e.meta ?? "").ToLowerInvariant().Contains(q));
        if (!string.IsNullOrEmpty(labelQ))
            res = res.Where(e => (e.labels ?? "").ToLowerInvariant().Contains(labelQ));
        if (!string.IsNullOrEmpty(folder))
        {
            var norm = folder.EndsWith("/") ? folder : folder + "/";
            res = res.Where(e => (e.address ?? "").Replace('\\','/').StartsWith(norm, StringComparison.Ordinal));
        }
        return res.ToList();
    }

    // ---------- Tree model ----------
    private class TreeNode
    {
        public string name;
        public string fullPath; // for folders this is the folder path ending with '/'; for leaves this is address
        public bool isLeaf;
        public List<TreeNode> children = new List<TreeNode>();
        public PublicEntry entry; // only for leaves
    }

    private IEnumerable<string> GetAllFolderPaths(List<PublicEntry> entries)
    {
        var set = new HashSet<string>();
        foreach (var e in entries)
        {
            var addr = (e.address ?? "").Replace('\\','/');
            var parts = addr.Split(new[]{'/'}, StringSplitOptions.RemoveEmptyEntries);
            var path = "";
            for (int i = 0; i < parts.Length - 1; i++)
            {
                path += parts[i] + "/";
                set.Add(path);
            }
        }
        return set;
    }

    private TreeNode BuildAddressTree(List<PublicEntry> entries)
    {
        var root = new TreeNode { name = "<root>", fullPath = "", isLeaf = false };
        var folderToNode = new Dictionary<string, TreeNode>();
        folderToNode[""] = root;

        foreach (var e in entries)
        {
            var addr = (e.address ?? "").Replace('\\','/');
            var parts = addr.Split(new[]{'/'}, StringSplitOptions.RemoveEmptyEntries);
            var path = "";
            TreeNode parent = root;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                path += parts[i] + "/";
                if (!folderToNode.TryGetValue(path, out var node))
                {
                    node = new TreeNode { name = parts[i], fullPath = path, isLeaf = false };
                    parent.children.Add(node);
                    folderToNode[path] = node;
                }
                parent = node;
            }
            // leaf
            var leaf = new TreeNode { name = parts.Length > 0 ? parts[parts.Length-1] : addr, fullPath = addr, isLeaf = true, entry = e };
            parent.children.Add(leaf);
        }

        // sort: folders first then leaves, alphabetical
        void Sort(TreeNode n)
        {
            n.children = n.children
                .OrderBy(c => c.isLeaf ? 1 : 0)
                .ThenBy(c => c.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var c in n.children) if (!c.isLeaf) Sort(c);
        }
        Sort(root);
        return root;
    }

    private void DrawAddressTree(TreeNode node, int indent, Dictionary<string, string> overrideMap, HashSet<string> overrideFolders = null)
    {
        if (node == null) return;
        foreach (var child in node.children)
        {
            if (child.isLeaf)
            {
				using (new EditorGUILayout.HorizontalScope())
				{
					// Indent one more level than the parent folder so leaves align under the folder content
					GUILayout.Space(12 * (indent + 1));
					bool isSel = string.Equals(selectedAddress, child.fullPath, StringComparison.Ordinal);
                    var typeText = string.IsNullOrEmpty(child.entry?.type) ? "" : $"  [{child.entry.type}]";
                    var roleText = string.IsNullOrEmpty(child.entry?.role) ? "" : $" ({child.entry.role})";
                    var label = new GUIContent(child.name + typeText + roleText);
					// Tooltip shows full context, including meta
					{
						var tt = "";
						if (!string.IsNullOrEmpty(child.fullPath)) tt += $"Address: {child.fullPath}\n";
						if (!string.IsNullOrEmpty(child.entry?.type)) tt += $"Type: {child.entry.type}\n";
						if (!string.IsNullOrEmpty(child.entry?.role)) tt += $"Role: {child.entry.role}\n";
						if (!string.IsNullOrEmpty(child.entry?.meta)) tt += $"Meta: {child.entry.meta}";
						label.tooltip = tt;
					}
                    var style = isSel ? new GUIStyle(EditorStyles.boldLabel) : new GUIStyle(EditorStyles.label);
                    if (overrideMap != null && overrideMap.ContainsKey(child.fullPath))
                    {
                        style.fontStyle = isSel ? FontStyle.BoldAndItalic : FontStyle.Italic;
                        style.normal.textColor = TintOverride;
                        style.hover.textColor = TintOverride;
                        style.active.textColor = TintOverride;
                    }
                    if (isSel)
                    {
                        style.normal.textColor = TintSelected;
                        style.hover.textColor = TintSelected;
                        style.active.textColor = TintSelected;
                    }
					var clickedName = GUILayout.Button(label, style, GUILayout.ExpandWidth(false));
					var nameRect = GUILayoutUtility.GetLastRect();
					EditorGUIUtility.AddCursorRect(nameRect, MouseCursor.Link);
					if (clickedName)
						selectedAddress = child.fullPath;
					// Drag & Drop onto address name to assign assets to this address
					var evt = Event.current;
					if ((evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform) && nameRect.Contains(evt.mousePosition))
					{
						DragAndDrop.visualMode = DragAndDropVisualMode.Link;
						if (evt.type == EventType.DragPerform)
						{
							DragAndDrop.AcceptDrag();
							var guids = new HashSet<string>();
							foreach (var obj in DragAndDrop.objectReferences)
							{
								var p = AssetDatabase.GetAssetPath(obj);
								if (string.IsNullOrEmpty(p)) continue;
								if (Directory.Exists(p))
								{
									foreach (var g in AssetDatabase.FindAssets("", new[] { p })) guids.Add(g);
								}
								else
								{
									var g = AssetDatabase.AssetPathToGUID(p);
									if (!string.IsNullOrEmpty(g)) guids.Add(g);
								}
							}
							if (guids.Count > 0)
							{
								AssignGuidsToAddress(child.entry, guids.ToList());
								cachedOverrideMap = null;
								lastOverrideRefresh = -1;
								Repaint();
							}
						}
						evt.Use();
					}
					GUILayout.FlexibleSpace();
					// Inline chosen override path on the right
						if (overrideMap != null && overrideMap.TryGetValue(child.fullPath, out var overPath) && !string.IsNullOrEmpty(overPath))
						{
							var rawText = "→ " + CompactAssetPath(overPath);
							var linkStyle = new GUIStyle(EditorStyles.miniLabel);
							linkStyle.richText = true;
							var content = new GUIContent($"<color=#9ccbff>{rawText}</color>", overPath);
							var rect = GUILayoutUtility.GetRect(content, linkStyle, GUILayout.ExpandWidth(false));
							EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
							GUI.Label(rect, content, linkStyle);
							if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
							{
								var obj = AssetDatabase.LoadAssetAtPath<Object>(overPath);
								if (obj != null)
								{
									EditorGUIUtility.PingObject(obj);
									Selection.activeObject = obj;
								}
								Event.current.Use();
							}
						}
				}
            }
			else
			{
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(12 * indent);
                    var expanded = treeExpanded.Contains(child.fullPath);
                    var folderStyle = new GUIStyle(EditorStyles.foldout);
                    if (overrideFolders != null && overrideFolders.Contains(child.fullPath))
                    {
                        folderStyle.fontStyle = FontStyle.Italic;
                        folderStyle.normal.textColor = TintOverride;
                        folderStyle.onNormal.textColor = TintOverride;
                        folderStyle.focused.textColor = TintOverride;
                        folderStyle.onFocused.textColor = TintOverride;
                    }
                    var newExpanded = EditorGUILayout.Foldout(expanded, child.name, true, folderStyle);
                    if (newExpanded != expanded)
                    {
                        if (newExpanded) treeExpanded.Add(child.fullPath);
                        else treeExpanded.Remove(child.fullPath);
                    }
                }
				if (treeExpanded.Contains(child.fullPath))
                    DrawAddressTree(child, indent + 1, overrideMap, overrideFolders);
            }
        }
    }

    private void DrawOverridesTree(TreeNode node, int indent, Dictionary<string, string> overrides)
    {
        if (node == null) return;
        foreach (var child in node.children)
        {
            if (child.isLeaf)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(12 * (indent + 1));
                    bool isSel = string.Equals(selectedAddress, child.fullPath, StringComparison.Ordinal);
                    var style = isSel ? EditorStyles.boldLabel : EditorStyles.label;
					var clicked = GUILayout.Button(child.name, style, GUILayout.ExpandWidth(false));
					var rect = GUILayoutUtility.GetLastRect();
					EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
					if (clicked)
						selectedAddress = child.fullPath;

                    GUILayout.FlexibleSpace();

                    if (overrides != null && overrides.TryGetValue(child.fullPath, out var overPath) && !string.IsNullOrEmpty(overPath))
                    {
                        var rawText = "→ " + CompactAssetPath(overPath);
                        var linkStyle = new GUIStyle(EditorStyles.miniLabel);
                        linkStyle.richText = true;
                        var content = new GUIContent($"<color=#9ccbff>{rawText}</color>", overPath);
						var linkRect = GUILayoutUtility.GetRect(content, linkStyle, GUILayout.ExpandWidth(false));
						linkRect.y += 3f;
						EditorGUIUtility.AddCursorRect(linkRect, MouseCursor.Link);
						GUI.Label(linkRect, content, linkStyle);
						if (Event.current.type == EventType.MouseDown && linkRect.Contains(Event.current.mousePosition))
                        {
                            var obj = AssetDatabase.LoadAssetAtPath<Object>(overPath);
                            if (obj != null)
                            {
                                EditorGUIUtility.PingObject(obj);
                                Selection.activeObject = obj;
                            }
                            Event.current.Use();
                        }
                        if (GUILayout.Button("Remove", GUILayout.Width(68)))
                        {
                            RemoveOverrideForCurrentMod(child.fullPath, overPath);
                        }
                    }
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(12 * indent);
                    var expanded = overridesExpanded.Contains(child.fullPath);
                    var newExpanded = EditorGUILayout.Foldout(expanded, child.name, true);
                    if (newExpanded != expanded)
                    {
                        if (newExpanded) overridesExpanded.Add(child.fullPath);
                        else overridesExpanded.Remove(child.fullPath);
                    }
                }
                if (overridesExpanded.Contains(child.fullPath))
                    DrawOverridesTree(child, indent + 1, overrides);
            }
        }
    }

	private static string CompactString(string text, int maxLength)
	{
		if (string.IsNullOrEmpty(text)) return "";
		var t = text.Trim();
		if (maxLength <= 1) return "…";
		if (t.Length <= maxLength) return t;
		return "…" + t.Substring(t.Length - (maxLength - 1));
	}

    private Dictionary<string, string> ComputeOverrideMap()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var settings = GetOrCreatePerModSettings();
            if (settings == null) return map;
            var group = settings.groups.FirstOrDefault(g => g != null && g.name == targetGroupName);
            if (group == null) return map;
            string modLabel = $"mod:{San(modName)}";
            foreach (var e in group.entries)
            {
                if (e == null) continue;
                if (!(e.labels != null && e.labels.Contains(modLabel))) continue;
                var path = AssetDatabase.GUIDToAssetPath(e.guid);
                if (string.IsNullOrEmpty(path)) continue;
                map[e.address ?? ""] = path.Replace('\\','/');
            }
        }
        catch { }
        return map;
    }

	private Dictionary<string, string> GetOverrideMapCached()
	{
		var now = EditorApplication.timeSinceStartup;
		var mod = San(modName);
		bool modChanged = cachedOverrideForMod == null || !string.Equals(cachedOverrideForMod, mod, StringComparison.Ordinal);
		bool stale = lastOverrideRefresh < 0 || (now - lastOverrideRefresh) > OverrideRefreshInterval;
		if (cachedOverrideMap == null || modChanged || stale)
		{
			cachedOverrideMap = ComputeOverrideMap();
			cachedOverrideForMod = mod;
			lastOverrideRefresh = now;
		}
		return cachedOverrideMap;
	}

	private void EnsureFilteredTreeUpToDate()
	{
		var curSearch = search ?? "";
		var curLabel = filterLabel ?? "";
		var curFolder = (filterFolder ?? "");
		if (cacheRoot != null && cacheVersion == publicVersion &&
			string.Equals(cacheSearch, curSearch, StringComparison.Ordinal) &&
			string.Equals(cacheLabel, curLabel, StringComparison.Ordinal) &&
			string.Equals(cacheFolder, curFolder, StringComparison.Ordinal))
		{
			return;
		}

		cacheSearch = curSearch;
		cacheLabel = curLabel;
		cacheFolder = curFolder;
		cacheVersion = publicVersion;

		cacheFiltered = FilterPublic(cacheSearch, cacheLabel, cacheFolder);
		cacheRoot = BuildAddressTree(cacheFiltered);
	}

    private static string CompactAssetPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return "";
        assetPath = assetPath.Replace('\\','/');
        var fileName = Path.GetFileName(assetPath);
        var parent = Path.GetFileName(Path.GetDirectoryName(assetPath));
        var shorty = string.IsNullOrEmpty(parent) ? fileName : (parent + "/" + fileName);
        if (shorty.Length > 42) shorty = "…" + shorty.Substring(shorty.Length - 42);
        return shorty;
    }

    private HashSet<string> GetOverrideFolderSet(Dictionary<string, string> overrideMap)
    {
        var set = new HashSet<string>();
        if (overrideMap == null) return set;
        foreach (var addr in overrideMap.Keys)
        {
            var path = (addr ?? string.Empty).Replace('\\','/');
            var parts = path.Split(new[]{'/'}, StringSplitOptions.RemoveEmptyEntries);
            var cur = "";
            for (int i = 0; i < parts.Length - 1; i++)
            {
                cur += parts[i] + "/";
                set.Add(cur);
            }
        }
        return set;
    }

    // ---------- Assign ----------
    private void AssignSelectedToAddress(PublicEntry target)
    {
        var settings = GetOrCreatePerModSettings();

        var group = EnsureGroup(settings, targetGroupName);

        var selected = GetSelectedGuids(true);
        if (selected.Count == 0) { Debug.LogWarning("Select one or more assets in the Project view."); return; }

        var existingAddresses = new HashSet<string>(
            settings.groups
                .Where(g => g != null)
                .SelectMany(g => g.entries ?? Enumerable.Empty<AddressableAssetEntry>())
                .Where(e => e != null)
                .Select(e => e.address ?? string.Empty)
        );
        int ok = 0, created = 0, moved = 0, mismatched = 0;

        Undo.RecordObject(settings, "Assign Mod Addresses");

        foreach (var guid in selected)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
            if (path.Contains("/EditorOnly/")) continue;

            var entry = settings.FindAssetEntry(guid);
            if (entry == null)
            {
                entry = settings.CreateOrMoveEntry(guid, group);
                created++;
            }

            // type check (best-effort)
            if (!string.IsNullOrEmpty(target.type))
            {
                var mainType = AssetDatabase.GetMainAssetTypeAtPath(path)?.Name ?? "";
                if (!string.IsNullOrEmpty(mainType) && !string.Equals(mainType, target.type, StringComparison.Ordinal))
                    mismatched++;
            }

            // set address exactly
            var desired = target.address.Trim();
            if (!existingAddresses.Contains(desired))
                entry.SetAddress(desired);
            else
                entry.SetAddress(desired); // allow duplicates intentionally for overrides; Addressables keeps one entry, mods override by order at runtime

            // move to mod group
            if (entry.parentGroup != group)
            {
                settings.CreateOrMoveEntry(entry.guid, group);
                moved++;
            }

            // tag entry for this mod, so we can build a per‑mod catalog later
            try
            {
                var modLabel = $"mod:{San(modName)}";
                var labels = settings.GetLabels();
                bool has = false; foreach (var l in labels) { if (l == modLabel) { has = true; break; } }
                if (!has) settings.AddLabel(modLabel);
                // Addressables API in this version doesn't expose HasLabel directly; use labels list
                var elabels = new HashSet<string>(entry.labels ?? Enumerable.Empty<string>());
                if (!elabels.Contains(modLabel)) entry.SetLabel(modLabel, true);
            }
            catch { }

            ok++;
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true, true);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ModSDK] Assigned: {ok}, created entries: {created}, moved: {moved}, type mismatches (warn): {mismatched}");
    }

	// Helper for drag-and-drop: assign a set of GUIDs to a specific public address entry
	private void AssignGuidsToAddress(PublicEntry target, List<string> guids)
	{
		if (target == null || guids == null || guids.Count == 0) return;
		var settings = GetOrCreatePerModSettings();
		var group = EnsureGroup(settings, targetGroupName);

		var existingAddresses = new HashSet<string>(
			settings.groups
				.Where(g => g != null)
				.SelectMany(g => g.entries ?? Enumerable.Empty<AddressableAssetEntry>())
				.Where(e => e != null)
				.Select(e => e.address ?? string.Empty)
		);

		Undo.RecordObject(settings, "Assign Mod Addresses (Drag&Drop)");
		int created = 0, moved = 0, mismatched = 0, ok = 0;

		foreach (var guid in guids)
		{
			var path = AssetDatabase.GUIDToAssetPath(guid);
			if (string.IsNullOrEmpty(path) || path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
			if (path.Contains("/EditorOnly/")) continue;

			var entry = settings.FindAssetEntry(guid);
			if (entry == null)
			{
				entry = settings.CreateOrMoveEntry(guid, group);
				created++;
			}

			// type check (best-effort)
			if (!string.IsNullOrEmpty(target.type))
			{
				var mainType = AssetDatabase.GetMainAssetTypeAtPath(path)?.Name ?? "";
				if (!string.IsNullOrEmpty(mainType) && !string.Equals(mainType, target.type, StringComparison.Ordinal))
					mismatched++;
			}

			var desired = target.address.Trim();
			if (!existingAddresses.Contains(desired)) entry.SetAddress(desired);
			else entry.SetAddress(desired);

			if (entry.parentGroup != group)
			{
				settings.CreateOrMoveEntry(entry.guid, group);
				moved++;
			}

			try
			{
				var modLabel = $"mod:{San(modName)}";
				var labels = settings.GetLabels();
				bool has = false; foreach (var l in labels) { if (l == modLabel) { has = true; break; } }
				if (!has) settings.AddLabel(modLabel);
				var elabels = new HashSet<string>(entry.labels ?? Enumerable.Empty<string>());
				if (!elabels.Contains(modLabel)) entry.SetLabel(modLabel, true);
			}
			catch { }

			ok++;
		}

		settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true, true);
		AssetDatabase.SaveAssets();
		Debug.Log($"[ModSDK] (Drag&Drop) Assigned: {ok}, created: {created}, moved: {moved}, type mismatches (warn): {mismatched}");
	}

	// ---------- Preview from metadata ----------
	private void BuildPreviewFromMeta(PublicEntry entry)
	{
		try
		{
			if (entry == null)
			{
				Debug.LogWarning("[ModSDK] Build preview failed: entry is null.");
				return;
			}
			// Enforce model/prefab preview only
			bool isModelOrPrefab =
				string.Equals(entry.type, "Model", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(entry.type, "GameObject", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(entry.role, "Model", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(entry.role, "Prefab", StringComparison.OrdinalIgnoreCase);
			if (!isModelOrPrefab)
			{
				Debug.LogWarning($"[ModSDK] Preview supported for Model/Prefab entries only. '{entry.address}' is type='{entry.type}', role='{entry.role}'.");
				return;
			}
			var meta = entry.meta ?? string.Empty;
			if (string.IsNullOrWhiteSpace(meta))
			{
				Debug.LogWarning($"[ModSDK] No meta for '{entry.address}'.");
				return;
			}

			// If we have a full PrefabStructure in structure, reconstruct hierarchy preview from it
			if (!string.IsNullOrEmpty(entry.structure))
			{
				BuildPreviewFromStructure(entry);
				return;
			}

			// Fallback: simple AABB preview from meta only
			if (!TryParseVec3(meta, "aabbCenter", out var aabbCenter))
			{
				Debug.LogWarning($"[ModSDK] Could not parse aabbCenter from meta for '{entry.address}'.");
				return;
			}
			if (!TryParseVec3(meta, "aabbSize", out var aabbSize))
			{
				Debug.LogWarning($"[ModSDK] Could not parse aabbSize from meta for '{entry.address}'.");
				return;
			}

			// pivotOffset is optional; if present, prefer -pivotOffset as cube localPosition; else use aabbCenter
			Vector3 cubeLocalPos = aabbCenter;
			if (TryParseVec3(meta, "pivotOffset", out var pivotOffset))
				cubeLocalPos = -pivotOffset;

			// Create a root at origin to represent the pivot
			var leaf = entry.address;
			var slashIdx = string.IsNullOrEmpty(leaf) ? -1 : leaf.LastIndexOf('/') ;
			if (slashIdx >= 0 && slashIdx + 1 < leaf.Length) leaf = leaf.Substring(slashIdx + 1);
			var root = new GameObject(San(leaf));
			root.transform.position = Vector3.zero;

			// Apply root scale if provided, so world-space preview size matches captured context
			if (TryParseVec3(meta, "rootScale", out var rootScale))
			{
				root.transform.localScale = new Vector3(
					Mathf.Max(0.0001f, rootScale.x),
					Mathf.Max(0.0001f, rootScale.y),
					Mathf.Max(0.0001f, rootScale.z)
				);
			}

			// Create a white cube child sized to the AABB and offset so the pivot is correct
			var cube = CreateBakedBoxVisual("AABB_Box", root.transform, cubeLocalPos, new Vector3(
				Mathf.Max(0.0001f, aabbSize.x),
				Mathf.Max(0.0001f, aabbSize.y),
				Mathf.Max(0.0001f, aabbSize.z)
			));

			Selection.activeGameObject = root;
			EditorGUIUtility.PingObject(root);
			Debug.Log($"[ModSDK] Built preview cube for '{entry.address}'.", root);
		}
		catch (Exception ex)
		{
			Debug.LogError($"[ModSDK] Failed to build preview: {ex.Message}");
		}
	}

	private void BuildPreviewFromStructure(PublicEntry entry)
	{
		PrefabStructure structure = null;
		try { structure = JsonUtility.FromJson<PrefabStructure>(entry.structure ?? string.Empty); }
		catch { }
		if (structure == null || structure.nodes == null || structure.nodes.Count == 0)
		{
			Debug.LogWarning($"[ModSDK] Invalid prefab structure for '{entry.address}'.");
			return;
		}

	var leaf2 = entry.address;
	var slashIdx2 = string.IsNullOrEmpty(leaf2) ? -1 : leaf2.LastIndexOf('/') ;
	if (slashIdx2 >= 0 && slashIdx2 + 1 < leaf2.Length) leaf2 = leaf2.Substring(slashIdx2 + 1);
	var root = new GameObject(San(leaf2));
		root.transform.position = Vector3.zero;
		if (structure.rootScale != Vector3.zero)
			root.transform.localScale = new Vector3(
				Mathf.Max(0.0001f, structure.rootScale.x),
				Mathf.Max(0.0001f, structure.rootScale.y),
				Mathf.Max(0.0001f, structure.rootScale.z)
			);

		// Build hierarchy using path strings
		var pathToGO = new Dictionary<string, GameObject>(StringComparer.Ordinal);
		foreach (var node in structure.nodes)
		{
			if (string.IsNullOrEmpty(node?.path)) continue;
			var go = EnsureHierarchyPath(root, node.path, pathToGO);
			var t = go.transform;
			// Apply local TRS captured
			t.localPosition = node.lp;
			t.localEulerAngles = node.lr;
			t.localScale = node.ls;

			// If node has bounds, add a single baked box whose mesh is offset to match pivot
			if (node.hasBounds)
			{
				var leafName = node.path;
				var slash = string.IsNullOrEmpty(leafName) ? -1 : leafName.LastIndexOf('/');
				if (slash >= 0 && slash + 1 < leafName.Length) leafName = leafName.Substring(slash + 1);
				if (string.IsNullOrEmpty(leafName)) leafName = "Bounds";
				// If the only segment equals the root name, attach to the root instead of creating a duplicate child
				bool singleSegment = node.path.IndexOf('/') < 0;
				if (singleSegment && string.Equals(San(node.path), root.name, StringComparison.Ordinal))
				{
					AttachBakedBoxVisualToExisting(root, -node.pivotOffset, new Vector3(
						Mathf.Max(0.0001f, node.bSize.x),
						Mathf.Max(0.0001f, node.bSize.y),
						Mathf.Max(0.0001f, node.bSize.z)
					));
				}
				else
				{
					CreateBakedBoxVisual(leafName, t, -node.pivotOffset, new Vector3(
						Mathf.Max(0.0001f, node.bSize.x),
						Mathf.Max(0.0001f, node.bSize.y),
						Mathf.Max(0.0001f, node.bSize.z)
					));
				}
			}

			// Recreate BoxColliders present on this node
			if (node.boxes != null && node.boxes.Count > 0)
			{
				foreach (var bc in node.boxes)
				{
					var colGo = new GameObject("BoxColliderVis");
					colGo.transform.SetParent(t, false);
					colGo.transform.localPosition = bc.center;
					colGo.transform.localRotation = Quaternion.identity;
					colGo.transform.localScale = bc.size;
					CreateBoxVisual("Collider", colGo.transform, Vector3.zero, Vector3.one);
					// Add an actual BoxCollider on the node to mirror trigger setting
					var colliderHost = t.gameObject;
					var collider = colliderHost.GetComponent<BoxCollider>();
					if (collider == null) collider = colliderHost.AddComponent<BoxCollider>();
					collider.center = bc.center;
					collider.size = bc.size;
					collider.isTrigger = bc.isTrigger;
				}
			}
		}

		Selection.activeGameObject = root;
		EditorGUIUtility.PingObject(root);
	}

	private static GameObject EnsureHierarchyPath(GameObject root, string path, Dictionary<string, GameObject> cache)
	{
		// Path format: Root/Child/Sub
		if (cache.TryGetValue(path, out var found)) return found;
		var parts = path.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
		Transform current = root.transform;
		var builtPath = root.name;
		// Skip duplicating the first segment if it matches the root name
		int start = 0;
		if (parts.Length > 0 && string.Equals(San(parts[0]), root.name, StringComparison.Ordinal)) start = 1;
		for (int i = start; i < parts.Length; i++)
		{
			var name = parts[i];
			Transform next = null;
			for (int c = 0; c < current.childCount; c++)
			{
				var ch = current.GetChild(c);
				if (ch.name == name) { next = ch; break; }
			}
			if (next == null)
			{
				var go = new GameObject(name);
				go.transform.SetParent(current, false);
				next = go.transform;
			}
			current = next;
			builtPath = builtPath + "/" + name;
			if (!cache.ContainsKey(builtPath)) cache[builtPath] = current.gameObject;
		}
		return current.gameObject;
	}

	private static bool TryParseVec3(string meta, string key, out Vector3 value)
	{
		value = Vector3.zero;
		if (string.IsNullOrEmpty(meta) || string.IsNullOrEmpty(key)) return false;
		try
		{
			var idx = meta.IndexOf(key, StringComparison.OrdinalIgnoreCase);
			if (idx < 0) return false;
			var open = meta.IndexOf('(', idx);
			if (open < 0) return false;
			var close = meta.IndexOf(')', open + 1);
			if (close < 0 || close <= open + 1) return false;
			var inner = meta.Substring(open + 1, close - open - 1);
			var parts = inner.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length != 3) return false;
			var ci = CultureInfo.InvariantCulture;
			float x = float.Parse(parts[0].Trim(), ci);
			float y = float.Parse(parts[1].Trim(), ci);
			float z = float.Parse(parts[2].Trim(), ci);
			value = new Vector3(x, y, z);
			return true;
		}
		catch { return false; }
	}

    private void RemoveOverrideForCurrentMod(string address, string assetPath)
    {
        try
        {
            var settings = GetOrCreatePerModSettings();
            if (settings == null) { Debug.LogWarning($"[ModSDK] Remove override failed: settings not found for '{address}'."); return; }

            var group = settings.groups.FirstOrDefault(g => g != null && g.name == targetGroupName);
            if (group == null) { Debug.LogWarning($"[ModSDK] Remove override failed: group '{targetGroupName}' not found."); return; }

            string modLabel = $"mod:{San(modName)}";
            // find entries in target group with matching address and this mod label
            var toRemove = group.entries
                .Where(e => e != null && string.Equals(e.address ?? string.Empty, address, StringComparison.Ordinal)
                    && e.labels != null && e.labels.Contains(modLabel))
                .ToList();

            if (toRemove.Count == 0)
            {
                Debug.LogWarning($"[ModSDK] No override entry found for '{address}' in this mod.");
                return;
            }

            Undo.RecordObject(settings, "Remove Mod Override");
            foreach (var e in toRemove)
            {
                try { settings.RemoveAssetEntry(e.guid); } catch {}
            }
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryRemoved, null, true, true);
            AssetDatabase.SaveAssets();

            // Invalidate override cache so UI updates quickly
            cachedOverrideMap = null;
            lastOverrideRefresh = -1;
            Repaint();
            Debug.Log($"[ModSDK] Removed override for '{address}' from mod '{San(modName)}'.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ModSDK] Failed to remove override for '{address}': {ex.Message}");
        }
    }

	// Create a named lit box (no physics collider) as a child of parent, with mesh vertices offset so the pivot is baked
	private static GameObject CreateBakedBoxVisual(string objectName, Transform parent, Vector3 localCenter, Vector3 size)
	{
		var go = new GameObject(objectName);
		go.transform.SetParent(parent, false);
		go.transform.localPosition = Vector3.zero;
		go.transform.localRotation = Quaternion.identity;
		go.transform.localScale = new Vector3(
			Mathf.Max(0.0001f, size.x),
			Mathf.Max(0.0001f, size.y),
			Mathf.Max(0.0001f, size.z)
		);

		var mf = go.AddComponent<MeshFilter>();
		var mr = go.AddComponent<MeshRenderer>();

		var src = GetUnitCubeMesh();
		var srcVerts = src.vertices;
		var bakedVerts = new Vector3[srcVerts.Length];
		float sx = Mathf.Max(0.0001f, size.x);
		float sy = Mathf.Max(0.0001f, size.y);
		float sz = Mathf.Max(0.0001f, size.z);
		var offset = new Vector3(
			localCenter.x / sx, 
			localCenter.y / sy,
			localCenter.z / sz
		);
		for (int i = 0; i < srcVerts.Length; i++) bakedVerts[i] = srcVerts[i] + offset;

		var mesh = new Mesh();
		mesh.name = src.name + "_BakedPivot";
		mesh.vertices = bakedVerts;
		mesh.triangles = src.triangles;
		mesh.normals = src.normals;
		mesh.RecalculateBounds();
		mf.sharedMesh = mesh;

		mr.shadowCastingMode = ShadowCastingMode.On;
		mr.receiveShadows = true;
		var mat = GetDefaultLitMaterial();
		if (mat != null) mr.sharedMaterial = mat;
		return go;
	}

	// Attach a baked box mesh to an existing GameObject without creating a child
	private static void AttachBakedBoxVisualToExisting(GameObject target, Vector3 localCenter, Vector3 size)
	{
		if (target == null) return;
		var mf = target.GetComponent<MeshFilter>(); if (mf == null) mf = target.AddComponent<MeshFilter>();
		var mr = target.GetComponent<MeshRenderer>(); if (mr == null) mr = target.AddComponent<MeshRenderer>();

		var src = GetUnitCubeMesh();
		var srcVerts = src.vertices;
		var bakedVerts = new Vector3[srcVerts.Length];
		for (int i = 0; i < srcVerts.Length; i++)
		{
			var v = srcVerts[i];
			bakedVerts[i] = new Vector3(v.x * Mathf.Max(0.0001f, size.x), v.y * Mathf.Max(0.0001f, size.y), v.z * Mathf.Max(0.0001f, size.z)) + localCenter;
		}
		var mesh = new Mesh();
		mesh.name = "BakedPivot_Attached";
		mesh.vertices = bakedVerts;
		mesh.triangles = src.triangles;
		mesh.normals = src.normals;
		mesh.RecalculateBounds();
		mf.sharedMesh = mesh;

		mr.shadowCastingMode = ShadowCastingMode.On;
		mr.receiveShadows = true;
		var mat = GetDefaultLitMaterial();
		if (mat != null) mr.sharedMaterial = mat;
	}

	// Create a named lit box (no physics collider) as a child of parent, centered at localPos and scaled to size
	private static GameObject CreateBoxVisual(string namePrefix, Transform parent, Vector3 localPos, Vector3 size)
	{
		var go = new GameObject($"{namePrefix}_Box");
		go.transform.SetParent(parent, false);
		go.transform.localPosition = localPos;
		go.transform.localRotation = Quaternion.identity;
		go.transform.localScale = size;

		var mf = go.AddComponent<MeshFilter>();
		var mr = go.AddComponent<MeshRenderer>();
		mf.sharedMesh = GetUnitCubeMesh();
		mr.shadowCastingMode = ShadowCastingMode.On;
		mr.receiveShadows = true;
		var mat = GetDefaultLitMaterial();
		if (mat != null) mr.sharedMaterial = mat;
		return go;
	}

	// Returns a cached unit cube mesh (1x1x1) centered at origin
	private static Mesh unitCubeMesh;
	private static Mesh GetUnitCubeMesh()
	{
		if (unitCubeMesh != null) return unitCubeMesh;
		var m = new Mesh();
		m.name = "UnitCube";
		var v = new Vector3[]
		{
			new Vector3(-0.5f,-0.5f,-0.5f), new Vector3( 0.5f,-0.5f,-0.5f), new Vector3( 0.5f, 0.5f,-0.5f), new Vector3(-0.5f, 0.5f,-0.5f), // back
			new Vector3(-0.5f,-0.5f, 0.5f), new Vector3( 0.5f,-0.5f, 0.5f), new Vector3( 0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)  // front
		};
		var t = new int[]
		{
			0,2,1, 0,3,2, // back
			4,5,6, 4,6,7, // front
			0,1,5, 0,5,4, // bottom
			2,3,7, 2,7,6, // top
			1,2,6, 1,6,5, // right
			3,0,4, 3,4,7  // left
		};
		m.SetVertices(v);
		m.SetTriangles(t, 0);
		m.RecalculateNormals();
		m.RecalculateBounds();
		unitCubeMesh = m;
		return m;
	}

	// Simple default lit material for previews
	private static Material defaultLitMaterial;
	private static Material GetDefaultLitMaterial()
	{
		if (defaultLitMaterial != null) return defaultLitMaterial;
		Shader shader = Shader.Find("Universal Render Pipeline/Lit");
		if (shader == null) shader = Shader.Find("HDRP/Lit");
		if (shader == null) shader = Shader.Find("Standard");
		if (shader == null) return null;
		defaultLitMaterial = new Material(shader);
		return defaultLitMaterial;
	}

    // Auto mode removed per updated UX

    // ---------- Validate ----------
    private bool ValidateMod(out List<string> issues)
    {
        issues = new List<string>();
        var settings = GetOrCreatePerModSettings();
        if (settings == null) { issues.Add("Per‑mod Addressables settings not found."); return false; }

        var group = settings.groups.FirstOrDefault(g => g != null && g.name == targetGroupName) ?? EnsureGroup(settings, targetGroupName);
        if (group == null) { issues.Add($"Group '{targetGroupName}' not found."); return false; }

        // Build a quick map of public addresses -> expected type
        var expected = publicCatalog.entries
            .GroupBy(e => e.address)
            .ToDictionary(g => g.Key, g => g.First().type ?? "");

        var taken = new HashSet<string>();
        string modLabel = $"mod:{San(modName)}";
        var entries = group.entries.Where(e => e != null && e.labels != null && e.labels.Contains(modLabel)).ToList();
        foreach (var e in entries)
        {
            var addr = e.address ?? "";
            if (string.IsNullOrWhiteSpace(addr))
                issues.Add($"Empty address: {AssetDatabase.GUIDToAssetPath(e.guid)}");

            if (addr.Contains("  ") || addr.Contains(" "))
                issues.Add($"Spaces in address '{addr}' (avoid spaces).");

            if (!taken.Add(addr))
                issues.Add($"Duplicate address in mod group: '{addr}'");

            // warn if overriding a non-public address (optional)
            if (expected.Count > 0 && !expected.ContainsKey(addr))
                issues.Add($"Address '{addr}' not found in public list (ok for NEW content, but won't override the base game).");

            // type check
            var tExpected = expected.ContainsKey(addr) ? expected[addr] : "";
            if (!string.IsNullOrEmpty(tExpected))
            {
                var path = AssetDatabase.GUIDToAssetPath(e.guid);
                var tActual = AssetDatabase.GetMainAssetTypeAtPath(path)?.Name ?? "";
                if (!string.Equals(tExpected, tActual, StringComparison.Ordinal))
                    issues.Add($"Type mismatch for '{addr}': expected {tExpected}, found {tActual}");
            }
        }

        return issues.Count == 0;
    }

    // ---------- Build ----------
    private void BuildMod()
    {
        if (!ValidateMod(out var problems))
        {
            if (!EditorUtility.DisplayDialog("Validation failed",
                $"Found {problems.Count} issue(s). Build anyway?", "Build Anyway", "Cancel"))
                return;
        }

        var settings = GetOrCreatePerModSettings();
        if (settings == null) { Debug.LogError("Addressables settings not found."); return; }

        // Ensure target group has Pack Separately / LZ4 / Local paths
        var group = EnsureGroup(settings, targetGroupName);

        // Point this group's LoadPath to mods/<ModName> so the generated catalog resolves bundles there
        var bundled = group.GetSchema<BundledAssetGroupSchema>() ?? group.AddSchema<BundledAssetGroupSchema>();
        // Keep the default load path for catalog; we only relocate bundles via copy. Catalog stays at platform root
        bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
        bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupSchemaModified, group, true, false);
        AssetDatabase.SaveAssets();

        // Always produce a per‑mod catalog (either to the provided output dir, or into StreamingAssets mods path)
        if (!string.IsNullOrEmpty(modOutputDir))
        {
            var perModOutDir = Path.Combine(modOutputDir, San(modName));
            BuildPerModCatalog(perModOutDir, group);
            return;
        }

        var platform = PlatformMappingService.GetPlatformPathSubFolder();
        var streamingAA = Path.Combine(Application.streamingAssetsPath, "aa", platform);
        Directory.CreateDirectory(streamingAA);
        var outRoot = Path.Combine(streamingAA, "mods", San(modName));
        BuildPerModCatalog(outRoot, group);
        return;
    }

    private void BuildPerModCatalog(string outDir, AddressableAssetGroup sourceGroup)
    {
        try
        {
            // Preserve WorkshopID from previous build (if present) before cleaning outDir
            string preservedWorkshopId = null;
            try
            {
                var prevInfo = Path.Combine(outDir, "modinfo.json");
                if (Directory.Exists(outDir) && File.Exists(prevInfo))
                {
                    var prevText = File.ReadAllText(prevInfo);
                    preservedWorkshopId = ExtractWorkshopId(prevText);
                }
            }
            catch { /* ignore */ }

            // Full clean: ensure destination is empty before building into it
            try
            {
                if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            }
            catch { /* ignore, recreate below */ }
            Directory.CreateDirectory(outDir);

			// Build uses an isolated copy of Addressables settings so working assignments remain intact
			var mainRoot = Path.Combine(GetWorkingRootAssetPath(), San(modName));
            Directory.CreateDirectory(mainRoot);
            var buildRoot = Path.Combine(mainRoot, "BuildTemp");
            try { if (Directory.Exists(buildRoot)) Directory.Delete(buildRoot, true); } catch { }
            Directory.CreateDirectory(buildRoot);
            var buildSettings = AddressableAssetSettings.Create(buildRoot, "AddressableAssetSettings", true, true);

            // Use a dedicated profile; point Build/Load to the absolute mod folder
            var profiles = buildSettings.profileSettings;
            var profileId = buildSettings.activeProfileId;
            if (string.IsNullOrEmpty(profileId)) profileId = profiles.AddProfile("Mod", null);
            buildSettings.activeProfileId = profileId;

            var modRoot = outDir.Replace('\\','/');
            if (string.IsNullOrEmpty(profiles.GetValueByName(profileId, AddressableAssetSettings.kLocalBuildPath)))
                profiles.CreateValue(AddressableAssetSettings.kLocalBuildPath, modRoot);
            if (string.IsNullOrEmpty(profiles.GetValueByName(profileId, AddressableAssetSettings.kLocalLoadPath)))
                profiles.CreateValue(AddressableAssetSettings.kLocalLoadPath, modRoot);
            profiles.SetValue(profileId, AddressableAssetSettings.kLocalBuildPath, modRoot);
            profiles.SetValue(profileId, AddressableAssetSettings.kLocalLoadPath, modRoot);

            // Create a group and mirror entries from sourceGroup that belong to this mod only
            var schemas = new List<AddressableAssetGroupSchema>();
            var bundled = ScriptableObject.CreateInstance<BundledAssetGroupSchema>();
            bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            schemas.Add(bundled);
            var update = ScriptableObject.CreateInstance<ContentUpdateGroupSchema>();
            update.StaticContent = false;
            schemas.Add(update);

            var modGroup = buildSettings.CreateGroup("ModContent", false, false, false, schemas,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            buildSettings.DefaultGroup = modGroup;

            string modLabel = $"mod:{San(modName)}";
            foreach (var e in sourceGroup.entries.ToList())
            {
                if (e == null) continue;
                if (!(e.labels != null && e.labels.Contains(modLabel))) continue;
                var entry = buildSettings.CreateOrMoveEntry(e.guid, modGroup);
                entry.SetAddress(e.address);
            }

            bundled.BuildPath.SetVariableByName(buildSettings, AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(buildSettings, AddressableAssetSettings.kLocalLoadPath);

            // Prune the Built In Data 'EditorSceneList' entry so Scenes In Build are not added to this per‑mod catalog
            try
            {
                foreach (var g in buildSettings.groups.ToList())
                {
                    if (g == null) continue;
                    var entries = g.entries != null ? g.entries.ToList() : new List<AddressableAssetEntry>();
                    foreach (var e in entries)
                    {
                        if (e == null) continue;
                        if (string.Equals(e.guid, "EditorSceneList", StringComparison.Ordinal))
                        {
                            buildSettings.RemoveAssetEntry(e.guid);
                        }
                    }
                }
            }
            catch { }

            // Ensure a Player data builder exists and is active (Packed Mode)
            var builderPath = Path.Combine(buildRoot, "BuildScriptPackedMode.asset");
            var packedBuilder = ScriptableObject.CreateInstance<BuildScriptPackedMode>();
            AssetDatabase.CreateAsset(packedBuilder, builderPath);
            buildSettings.AddDataBuilder(packedBuilder);
            buildSettings.ActivePlayerDataBuilderIndex = buildSettings.DataBuilders.IndexOf(packedBuilder);
            // PlayMode builder is irrelevant here but set to a sane default
            buildSettings.ActivePlayModeDataBuilderIndex = buildSettings.ActivePlayerDataBuilderIndex;

            AssetDatabase.SaveAssets();

            // Swap default settings to the temp settings for the build
            var previous = AddressableAssetSettingsDefaultObject.Settings;
            // Addressables (1.19) writes DefaultObject.asset under Assets/AddressableAssetsData on set; ensure dir exists and delete after
            var defaultObjDir = Path.Combine("Assets", "AddressableAssetsData");
            bool createdDefaultDir = false;
            if (!Directory.Exists(defaultObjDir)) { Directory.CreateDirectory(defaultObjDir); createdDefaultDir = true; AssetDatabase.Refresh(); }
            AddressableAssetSettingsDefaultObject.Settings = buildSettings;

            try
            {
                AddressableAssetSettings.BuildPlayerContent();
            }
            finally
            {
                // Restore
                AddressableAssetSettingsDefaultObject.Settings = previous;
                // Clean up the auto-created DefaultObject.asset and folder if we made it
                if (createdDefaultDir)
                {
                    try { AssetDatabase.DeleteAsset("Assets/AddressableAssetsData/DefaultObject.asset"); } catch { }
                    try { AssetDatabase.DeleteAsset("Assets/AddressableAssetsData"); } catch { }
                    AssetDatabase.Refresh();
                }
            }

            // Post-build: copy ONLY the per‑mod catalog from the temp build path → outDir
            var platform = PlatformMappingService.GetPlatformPathSubFolder();
            var tempBuildPath = ResolveProfilePath(buildSettings, AddressableAssetSettings.kLocalBuildPath, platform);
            // Clean old catalogs to avoid picking up a platform catalog with StreamingAssets paths
            foreach (var old in Directory.GetFiles(outDir, "catalog*.*", SearchOption.AllDirectories))
            {
                try { File.Delete(old); } catch { }
            }
            int copiedCatalogs = 0;
            if (!string.IsNullOrEmpty(tempBuildPath) && Directory.Exists(tempBuildPath))
            {
                foreach (var file in Directory.GetFiles(tempBuildPath, "catalog*.*", SearchOption.AllDirectories))
                {
                    var name = Path.GetFileName(file);
                    var dst = Path.Combine(outDir, name);
                    try { File.Copy(file, dst, true); copiedCatalogs++; } catch { }
                }
            }
            // Do not fall back to copying the main project catalog from Library; per‑mod catalogs must originate from the temp build
            if (copiedCatalogs == 0)
            {
                Debug.LogWarning("[ModSDK] No per‑mod catalog files were produced. Ensure your mod has addressable entries labeled for this mod.");
            }

            // Write a minimal modinfo.json if not present (preserving WorkshopID if we captured one) and include code target flags
            var infoPath = Path.Combine(outDir, "modinfo.json");
            if (!File.Exists(infoPath))
            {
                // Load flags (and optional preserved fields) from working modinfo.json if present
                bool buildIsClient = true, buildIsServer = false;
                LoadFlagsFromWorkingModInfo(out buildIsClient, out buildIsServer);
                var workshopLine = !string.IsNullOrEmpty(preservedWorkshopId)
                    ? $"  \"workshopID\": \"{Escape(preservedWorkshopId)}\",\n"
                    : string.Empty;
                var info = "{\n" +
                           $"  \"name\": \"{Escape(modName)}\",\n" +
                           $"  \"version\": \"{Escape(modVersion)}\",\n" +
                           $"  \"gameVersion\": \"{Escape(PlayerSettings.bundleVersion)}\",\n" +
                           $"  \"unity\": \"{Escape(Application.unityVersion)}\",\n" +
                           workshopLine +
                           $"  \"isClient\": {(buildIsClient ? "true" : "false")},\n" +
                           $"  \"isServer\": {(buildIsServer ? "true" : "false")},\n" +
                           $"  \"builtUtc\": \"{DateTime.UtcNow:O}\"\n" +
                           "}\n";
                File.WriteAllText(infoPath, info);
            }
            else
            {
                // If modinfo.json exists, upsert the isClient/isServer flags to reflect UI state
                try
                {
                    bool buildIsClient = modIsClient;
                    bool buildIsServer = modIsServer;
                    var text = File.ReadAllText(infoPath);
                    text = UpsertBooleanJson(text, "isClient", buildIsClient);
                    text = UpsertBooleanJson(text, "isServer", buildIsServer);
                    File.WriteAllText(infoPath, text);
                }
                catch { }
            }

            // Optional: include code payload and manifest (compile C# under code/ or copy prebuilt DLLs)
            WriteOrBuildCodePayload(outDir);

            // Quick check: report expected load path and whether a catalog is present
            var expectedLoadPath = bundled.LoadPath.GetValue(buildSettings);
            var catalogs = Directory.GetFiles(outDir, "catalog*.*", SearchOption.AllDirectories).Length;
            Debug.Log($"[ModSDK] Per-mod catalog built. Expected LoadPath='{expectedLoadPath}'. Catalog files in outDir: {catalogs} (copied {copiedCatalogs}). Folder: {outDir}");
            EditorUtility.RevealInFinder(outDir);

            // Tokenize absolute InternalIds to {ModRoot} so they resolve on player machines
            PostprocessCatalogTokenize(outDir);
            // Optional: clean the transient build settings folder to avoid confusion
            try { AssetDatabase.DeleteAsset(buildRoot); } catch { }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ModSDK] Per-mod build failed: {ex.Message}");
        }
    }

    // ---------- Helpers: per‑mod Addressables settings ----------
	private AddressableAssetSettings GetOrCreatePerModSettings()
    {
		try
        {
			var newRoot = Path.Combine(GetWorkingRootAssetPath(), San(modName));
			var legacyRoot = Path.Combine(LegacyRootAssetPath(), San(modName));
			var newAssetPath = Path.Combine(newRoot, "AddressableAssetSettings.asset");
			var legacyAssetPath = Path.Combine(legacyRoot, "AddressableAssetSettings.asset");

			// Prefer new location; if not found, try legacy; if neither, create under new
			var settings = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(newAssetPath);
			if (settings == null)
				settings = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(legacyAssetPath);

			if (settings == null)
			{
				Directory.CreateDirectory(newRoot);
				settings = AddressableAssetSettings.Create(newRoot, "AddressableAssetSettings", true, true);
				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();
			}
			return settings;
        }
        catch { return AddressableAssetSettingsDefaultObject.Settings; }
    }

	private AddressableAssetSettings GetOrCreatePerModSettingsFor(string mod)
	{
		try
		{
			var newRoot = Path.Combine(GetWorkingRootAssetPath(), San(mod));
			var legacyRoot = Path.Combine(LegacyRootAssetPath(), San(mod));
			var newAssetPath = Path.Combine(newRoot, "AddressableAssetSettings.asset");
			var legacyAssetPath = Path.Combine(legacyRoot, "AddressableAssetSettings.asset");

			var settings = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(newAssetPath);
			if (settings == null)
				settings = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(legacyAssetPath);

			if (settings == null)
			{
				Directory.CreateDirectory(newRoot);
				// Ensure a 'Code' source folder exists to guide modders
				var codeSrc = Path.Combine(newRoot, "Code");
				if (!Directory.Exists(codeSrc)) Directory.CreateDirectory(codeSrc);
				settings = AddressableAssetSettings.Create(newRoot, "AddressableAssetSettings", true, true);
				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();
			}
			if (settings == null)
			{
				Directory.CreateDirectory(newRoot);
				// Ensure a 'Code' source folder exists to guide modders
				var codeSrc = Path.Combine(newRoot, "Code");
				if (!Directory.Exists(codeSrc)) Directory.CreateDirectory(codeSrc);
				settings = AddressableAssetSettings.Create(newRoot, "AddressableAssetSettings", true, true);
				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();
			}
			return settings;
		}
		catch { return AddressableAssetSettingsDefaultObject.Settings; }
	}

	private bool TryFindPerModSettings(string mod, out AddressableAssetSettings settings, out string folderAssetPath)
	{
		settings = null;
		folderAssetPath = null;
		try
		{
			var newRoot = Path.Combine(GetWorkingRootAssetPath(), San(mod));
			var legacyRoot = Path.Combine(LegacyRootAssetPath(), San(mod));
			var newAssetPath = Path.Combine(newRoot, "AddressableAssetSettings.asset");
			var legacyAssetPath = Path.Combine(legacyRoot, "AddressableAssetSettings.asset");

			settings = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(newAssetPath);
			if (settings != null) { folderAssetPath = newRoot; return true; }
			settings = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(legacyAssetPath);
			if (settings != null) { folderAssetPath = legacyRoot; return true; }
			settings = null;
			return false;
		}
		catch { settings = null; folderAssetPath = null; return false; }
	}

	private string GetWorkingRootAssetPath()
	{
		var name = San(DefaultWorkingRootName);
		return Path.Combine("Assets", name);
	}

	private string LegacyRootAssetPath()
	{
		return Path.Combine("Assets", LegacyWorkingRootName);
	}

    private static void PostprocessCatalogTokenize(string outDir)
    {
        try
        {
            var norm = outDir.Replace('\\','/').TrimEnd('/');
            var back = outDir.Replace('/','\\').TrimEnd('\\');
            var escBack = back.Replace("\\", "\\\\"); // JSON-escaped backslashes
            int filesChanged = 0, totalReplacements = 0;
            foreach (var file in Directory.GetFiles(outDir, "catalog*.json", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                var before = text;
                text = text.Replace(norm, "{ModRoot}");
                text = text.Replace(back, "{ModRoot}");
                text = text.Replace(escBack, "{ModRoot}");
                if (!ReferenceEquals(before, text) && before != text)
                {
                    filesChanged++;
                    // crude replacement count
                    totalReplacements += Math.Max(0, (before.Length - text.Length) / Math.Max(1, norm.Length));
                    File.WriteAllText(file, text);
                }
            }
            Debug.Log($"[ModSDK] Catalog post-process: tokenized {filesChanged} file(s), replacements≈{totalReplacements}. Base='{outDir}' → '{{ModRoot}}'.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ModSDK] Catalog tokenization skipped/failed: {ex.Message}");
        }
    }

    // ---------- Optional code packaging (Harmony) ----------
	private async void WriteOrBuildCodePayload(string outDir)
    {
        try
        {
			// Determine source folder from selected mod automatically: Assets/My_Mods/<ModName>/Code (fallback: code)
			var working = Path.Combine(GetWorkingRootAssetPath(), San(modName));
			var srcFolder = Path.Combine(working, "Code");
			if (!Directory.Exists(srcFolder)) srcFolder = Path.Combine(working, "code");
			if (string.IsNullOrEmpty(srcFolder) || !Directory.Exists(srcFolder)) return;

            // Prefer compiling .cs into a DLL
            var csFiles = Directory.GetFiles(srcFolder, "*.cs", SearchOption.AllDirectories);
            var outCodeDir = Path.Combine(outDir, "code");
            Directory.CreateDirectory(outCodeDir);

            var builtDlls = new List<string>();
            if (csFiles != null && csFiles.Length > 0)
            {
                var targetName = San(modName) + ".dll";
                var targetPath = Path.Combine(outCodeDir, targetName);
                if (!await BuildCSharpToDllAsync(csFiles, targetPath))
                {
                    Debug.LogWarning($"[ModSDK] Code compile failed; will try copying prebuilt DLLs if any.");
                }
                else
                {
                    builtDlls.Add(targetName);
                }
            }

            // Also copy any prebuilt DLLs present (excluding 0Harmony)
            var dlls = Directory.GetFiles(srcFolder, "*.dll", SearchOption.TopDirectoryOnly);
            var copied = new List<string>();
            if (dlls != null)
            {
                foreach (var src in dlls)
                {
                    var name = Path.GetFileName(src);
                    if (string.Equals(name, "0Harmony.dll", StringComparison.OrdinalIgnoreCase)) continue;
                    // Skip the one we just compiled if srcFolder is under outDir/code
                    var dst = Path.Combine(outCodeDir, name);
                    try { File.Copy(src, dst, true); copied.Add(name); }
                    catch (Exception ex) { Debug.LogWarning($"[ModSDK] Copy DLL failed '{name}': {ex.Message}"); }
                }
            }
            var assembliesList = new List<string>();
            assembliesList.AddRange(builtDlls);
            assembliesList.AddRange(copied);
            if (assembliesList.Count == 0) return; // nothing to package

            // Build manifest JSON
            var harmonyId = San(modName);
            var entries = new List<string>();

            string JsonList(IEnumerable<string> items)
            {
                var list = items != null ? items.ToList() : new List<string>();
                for (int i = 0; i < list.Count; i++) list[i] = "\"" + Escape(list[i]) + "\"";
                return "[" + string.Join(", ", list) + "]";
            }

            var json = "{\n" +
                       $"  \"harmonyId\": \"{Escape(harmonyId)}\",\n" +
                       $"  \"assemblies\": {JsonList(assembliesList)},\n" +
                       $"  \"entryTypes\": {JsonList(entries)}\n" +
                       "}\n";

            var manifestPath = Path.Combine(outDir, "mod.code.json");
            File.WriteAllText(manifestPath, json);
            Debug.Log($"[ModSDK] Wrote code manifest with {assembliesList.Count} DLL(s): {manifestPath}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ModSDK] Code packaging skipped/failed: {ex.Message}");
        }
    }

    private static Task<bool> BuildCSharpToDllAsync(string[] csFiles, string outputPath)
    {
        var tcs = new TaskCompletionSource<bool>();
        try
        {
            if (csFiles == null || csFiles.Length == 0)
            {
                tcs.SetResult(false);
                return tcs.Task;
            }
            var asmName = Path.GetFileNameWithoutExtension(outputPath);
            // Ensure SDK helper is compiled into the mod DLL so STN.ModSDK.HarmonyTargets resolves
            var sourceList = (csFiles ?? Array.Empty<string>()).ToList();
            try
            {
                var helperRel = "Assets/_Internal/Utility/ModManager/HarmonyTargets.cs";
                var helperAbs = GetAbsoluteFilePath(helperRel);
                if (!string.IsNullOrEmpty(helperAbs) && File.Exists(helperAbs))
                {
                    bool alreadyIncluded = sourceList.Any(p => p.EndsWith("HarmonyTargets.cs", StringComparison.OrdinalIgnoreCase));
                    if (!alreadyIncluded) sourceList.Add(helperAbs);
                }
            }
            catch { }
            var builder = new AssemblyBuilder(outputPath, sourceList.ToArray());
            // Target runtime compatible with project's API compatibility level
            builder.compilerOptions = new ScriptCompilerOptions
            {
                ApiCompatibilityLevel = PlayerSettings.GetApiCompatibilityLevel(EditorUserBuildSettings.selectedBuildTargetGroup)
            };
            // Add default references: UnityEngine, UnityEditor (if needed), and 0Harmony if present in project
            var refs = new List<string>();
            TryAddRef(references: refs, asmName: "UnityEngine.dll");
#if UNITY_EDITOR
            TryAddRef(references: refs, asmName: "UnityEditor.dll");
#endif
            // Try find 0Harmony.dll in project (Assets/**)
            TryAddHarmonyRef(refs);
            builder.additionalReferences = refs.ToArray();
            Debug.Log($"[ModSDK] Using {builder.additionalReferences.Length} compiler reference(s): {string.Join(", ", builder.additionalReferences)}");

            builder.buildFinished += (path, messages) =>
            {
                var success = messages == null || !messages.Any(m => m.type == CompilerMessageType.Error);
                if (messages != null)
                {
                    foreach (var m in messages) Debug.Log((m.type == CompilerMessageType.Error ? "[ModSDK] (code) ERROR: " : "[ModSDK] (code) ") + m.message);
                }
                tcs.TrySetResult(success && File.Exists(outputPath));
            };
            if (!builder.Build())
            {
                Debug.LogWarning("[ModSDK] AssemblyBuilder failed to start.");
                tcs.TrySetResult(false);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ModSDK] BuildCSharpToDll failed: {ex.Message}");
            tcs.TrySetResult(false);
        }
        return tcs.Task;
    }

    private static void TryAddHarmonyRef(List<string> references)
    {
        try
        {
            // 1) Prefer exact file-name match anywhere under Assets
            var all = AssetDatabase.FindAssets("0Harmony");
            if (all != null && all.Length > 0)
            {
                foreach (var g in all)
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    if (string.IsNullOrEmpty(p) || !p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!p.EndsWith("/0Harmony.dll", StringComparison.OrdinalIgnoreCase) && !p.EndsWith("\\0Harmony.dll", StringComparison.OrdinalIgnoreCase)) continue;
                    var abs0 = GetAbsoluteFilePath(p);
                    if (!string.IsNullOrEmpty(abs0) && File.Exists(abs0)) { references.Add(abs0); return; }
                }
            }
        }
        catch { }
        // 2) Fallback: try common location under Assets/Plugins/Harmony/0Harmony.dll
        var fallback = GetAbsoluteFilePath("Assets/Plugins/Harmony/0Harmony.dll");
        if (!string.IsNullOrEmpty(fallback) && File.Exists(fallback)) { references.Add(fallback); return; }
        // 3) Last resort: scan disk under Assets for any 0Harmony.dll
        try
        {
            var any = Directory.GetFiles(Application.dataPath, "0Harmony.dll", SearchOption.AllDirectories).FirstOrDefault();
            if (!string.IsNullOrEmpty(any) && File.Exists(any)) references.Add(any);
        }
        catch { }
    }

    private static void TryAddRef(List<string> references, string asmName)
    {
        try
        {
            var unityDir = EditorApplication.applicationContentsPath;
            var monoLib = Path.Combine(unityDir, "MonoBleedingEdge/lib/mono/4.7.1");
            var file = Path.Combine(monoLib, asmName);
            if (File.Exists(file)) { references.Add(file); return; }
        }
        catch { }
    }

    // ---------- Helpers ----------
    private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string name)
    {
        var g = settings.groups.FirstOrDefault(x => x != null && x.name == name);
        if (g != null)
        {
            // enforce schema basics
            var bundled = g.GetSchema<BundledAssetGroupSchema>() ?? g.AddSchema<BundledAssetGroupSchema>();
            bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately; // per-asset bundles for mods
            // Use default: build to ServerData, load from StreamingAssets
            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            // Default load path will be overridden at build-time to mod-specific path
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);

            var update = g.GetSchema<ContentUpdateGroupSchema>() ?? g.AddSchema<ContentUpdateGroupSchema>();
            update.StaticContent = false;
            return g;
        }

        var schemas = new List<AddressableAssetGroupSchema>();
        var b = ScriptableObject.CreateInstance<BundledAssetGroupSchema>();
        b.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
        b.BundleMode  = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
        // Use default: build to ServerData, load from StreamingAssets
        b.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
        b.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
        schemas.Add(b);

        var u = ScriptableObject.CreateInstance<ContentUpdateGroupSchema>();
        u.StaticContent = false;
        schemas.Add(u);

        g = settings.CreateGroup(name, false, false, false, schemas,
            typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
        Debug.Log($"[ModSDK] Created group '{name}' (Pack Separately, LZ4).");
        return g;
    }

    private static List<string> GetSelectedGuids(bool recurse)
    {
        var set = new HashSet<string>();
        foreach (var o in Selection.objects)
        {
            var p = AssetDatabase.GetAssetPath(o);
            if (string.IsNullOrEmpty(p)) continue;

            if (Directory.Exists(p) && recurse)
                foreach (var g in AssetDatabase.FindAssets("", new[] { p })) set.Add(g);
            else
            {
                var g = AssetDatabase.AssetPathToGUID(p);
                if (!string.IsNullOrEmpty(g)) set.Add(g);
            }
        }
        return set.ToList();
    }

    private static string San(string s) => string.Concat((s ?? "mod").Select(ch =>
        char.IsLetterOrDigit(ch) || ch=='_' || ch=='-' || ch=='(' || ch==')' ? ch : '_'));

    private static string Escape(string s) => (s ?? "").Replace("\\","\\\\").Replace("\"","\\\"");

	private static string ExtractWorkshopId(string json)
	{
		if (string.IsNullOrEmpty(json)) return null;
		var keys = new [] { "workshopId", "workshopID", "id", "WorkshopID" };
		foreach (var key in keys)
		{
			var rx = new System.Text.RegularExpressions.Regex("\\\"" + System.Text.RegularExpressions.Regex.Escape(key) + "\\\"\\s*:\\s*\\\"?([0-9]+)\\\"?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
			var m = rx.Match(json);
			if (m.Success) return m.Groups[1].Value;
		}
		return null;
	}

    private static string GetAbsoluteFilePath(string assetOrRelativePath)
    {
        if (string.IsNullOrEmpty(assetOrRelativePath)) return null;
        if (Path.IsPathRooted(assetOrRelativePath)) return assetOrRelativePath;
        try
        {
            // If it's an Asset path, convert from project root
            if (assetOrRelativePath.Replace('\\','/').StartsWith("Assets/", StringComparison.Ordinal))
            {
                var projectRoot = Directory.GetParent(Application.dataPath).FullName;
                var combined = Path.Combine(projectRoot, assetOrRelativePath.Replace('/', Path.DirectorySeparatorChar));
                return combined;
            }
            // Otherwise resolve relative to project root
            return Path.GetFullPath(assetOrRelativePath);
        }
        catch { return null; }
    }

    private static string NormalizeDir(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        path = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string MakeRelative(string baseDir, string fullPath)
    {
        try
        {
            var baseUri = new Uri(NormalizeDir(baseDir));
            var fileUri = new Uri(NormalizePath(fullPath));
            var rel = Uri.UnescapeDataString(baseUri.MakeRelativeUri(fileUri).ToString().Replace('/', Path.DirectorySeparatorChar));
            return rel;
        }
        catch { return null; }
    }

    private static string ResolveProfilePath(AddressableAssetSettings settings, string variableName, string buildTargetSubfolder)
    {
        try
        {
            var raw = settings.profileSettings.GetValueByName(settings.activeProfileId, variableName);
            if (string.IsNullOrEmpty(raw)) return null;
            // Let Addressables evaluate profile variables like {Addressables.BuildPath} and [BuildTarget]
            var evaluated = settings.profileSettings.EvaluateString(settings.activeProfileId, raw);
            evaluated = evaluated.Replace("[BuildTarget]", buildTargetSubfolder);
            return Path.GetFullPath(evaluated);
        }
        catch { return null; }
    }

    private static void EnsureProfileVariable(AddressableAssetSettings settings, string name, string value)
    {
        var profiles = settings.profileSettings;
        if (profiles == null) return;
        var existing = profiles.GetValueByName(settings.activeProfileId, name);
        if (string.IsNullOrEmpty(existing)) profiles.CreateValue(name, value);
        profiles.SetValue(settings.activeProfileId, name, value);
    }

    private void AutoSelectFirstModIfAvailable()
    {
        try
        {
            var activeNew = Path.Combine(GetWorkingRootAssetPath(), San(modName), "AddressableAssetSettings.asset");
            var activeLegacy = Path.Combine(LegacyRootAssetPath(), San(modName), "AddressableAssetSettings.asset");
            bool hasActive = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(activeNew) != null ||
                             AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(activeLegacy) != null;
            if (hasActive) return;

            var mods = GetAvailableModNames();
            if (mods != null && mods.Count > 0)
            {
                modName = mods[0];
                cachedOverrideMap = null;
                lastOverrideRefresh = -1;
                // avoid noisy logs on editor load; UI will reflect selection immediately
            }
        }
        catch { }
    }

    // ---------- Mod Flags: load/save from working mod folder ----------
    private void EnsureModFlagsLoaded()
    {
        try
        {
            var current = San(modName);
            if (string.Equals(flagsLoadedForMod, current, StringComparison.Ordinal)) return;
            bool isC = true, isS = false;
            LoadFlagsFromWorkingModInfo(out isC, out isS);
            modIsClient = isC;
            modIsServer = isS;
            flagsLoadedForMod = current;
        }
        catch { }
    }

    private void LoadFlagsFromWorkingModInfo(out bool isClient, out bool isServer)
    {
        isClient = true; // back-compat default
        isServer = false;
        try
        {
            var working = Path.Combine(GetWorkingRootAssetPath(), San(modName));
            var infoPath = Path.Combine(working, "modinfo.json");
            if (!File.Exists(infoPath)) return;
            var json = File.ReadAllText(infoPath);
            // naive boolean extraction tolerant to whitespace/casing
            bool? ReadBool(string key)
            {
                try
                {
                    var rx = new Regex("\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
                    var m = rx.Match(json);
                    if (m.Success) return string.Equals(m.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
                    return null;
                }
                catch { return null; }
            }
            var c = ReadBool("isClient");
            var s = ReadBool("isServer");
            if (c.HasValue) isClient = c.Value;
            if (s.HasValue) isServer = s.Value;
        }
        catch { }
    }

    private void SaveModFlagsToWorkingModInfo()
    {
        try
        {
            var working = Path.Combine(GetWorkingRootAssetPath(), San(modName));
            var infoPath = Path.Combine(working, "modinfo.json");
            string json = "{\n}\n";
            try { if (File.Exists(infoPath)) json = File.ReadAllText(infoPath); } catch { }
            json = UpsertBooleanJson(json, "isClient", modIsClient);
            json = UpsertBooleanJson(json, "isServer", modIsServer);
            File.WriteAllText(infoPath, json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ModSDK] Failed to save mod flags: {ex.Message}");
        }
    }

    private static string UpsertBooleanJson(string json, string key, bool value)
    {
        try
        {
            if (string.IsNullOrEmpty(json)) json = "{\n}\n";
            var rx = new Regex("\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
            var replacement = $"\"{key}\": {(value ? "true" : "false")}";
            if (rx.IsMatch(json)) return rx.Replace(json, replacement);
            // Insert before closing brace
            var idx = json.LastIndexOf('}');
            if (idx < 0) return "{\n  " + replacement + "\n}\n";
            // Determine comma
            var prefix = json.Substring(0, idx).TrimEnd();
            var comma = prefix.EndsWith("{") ? "  " : ",\n  ";
            return json.Substring(0, idx) + comma + replacement + "\n}";
        }
        catch { return json; }
    }

	private List<string> GetAvailableModNames()
	{
		var list = new List<string>();
		try
		{
			void Scan(string assetRoot)
			{
				if (string.IsNullOrEmpty(assetRoot)) return;
				var abs = GetAbsoluteFilePath(assetRoot);
				if (string.IsNullOrEmpty(abs) || !Directory.Exists(abs)) return;
				foreach (var dir in Directory.GetDirectories(abs))
				{
					var name = Path.GetFileName(dir);
					var hasSettings = File.Exists(Path.Combine(dir, "AddressableAssetSettings.asset"));
					if (hasSettings) list.Add(San(name));
				}
			}

			// Prefer new root, then add legacy ones not already included
			Scan(GetWorkingRootAssetPath());
			var before = new HashSet<string>(list, StringComparer.Ordinal);
			var legacyRoot = LegacyRootAssetPath();
			var legacyAbs = GetAbsoluteFilePath(legacyRoot);
			if (!string.IsNullOrEmpty(legacyAbs) && Directory.Exists(legacyAbs))
			{
				foreach (var dir in Directory.GetDirectories(legacyAbs))
				{
					var name = San(Path.GetFileName(dir));
					var hasSettings = File.Exists(Path.Combine(dir, "AddressableAssetSettings.asset"));
					if (hasSettings && !before.Contains(name)) list.Add(name);
				}
			}
		}
		catch { }
		list.Sort(StringComparer.OrdinalIgnoreCase);
		return list;
	}

    // Super small JSON reader for the simple array we export (avoids extra deps)
    private List<PublicEntry> MiniJsonList(string json)
    {
        // Expect [{"address":"...","type":"...","labels":"...","role":"...","meta":"..."}]
        var list = new List<PublicEntry>();
        if (string.IsNullOrWhiteSpace(json)) return list;
        try
        {
            var sep = new[] {"{\"address\":\""};
            var parts = json.Split(sep, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                var addrEnd = p.IndexOf('"'); if (addrEnd < 0) continue;
                var address = p.Substring(0, addrEnd);

                string Grab(string key)
                {
                    var k = $"\"{key}\":\"";
                    var i = p.IndexOf(k, StringComparison.Ordinal);
                    if (i < 0) return "";
                    i += k.Length;
                    var j = p.IndexOf('"', i);
                    return j > i ? p.Substring(i, j - i) : "";
                }

                list.Add(new PublicEntry {
                    address = address,
                    type = Grab("type"),
                    labels = Grab("labels"),
                    role = Grab("role"),
                    meta = Grab("meta"),
                    structure = Grab("structure"),
                    notes = Grab("notes")
                });
            }
        }
        catch { /* fall back to empty */ }
        return list;
    }
}
#endif
