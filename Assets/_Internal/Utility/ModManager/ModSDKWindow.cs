#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using Object = UnityEngine.Object;
using STN.ModSDK;

public class ModSDKWindow : EditorWindow
{
    // ---------- UI state ----------
    [Serializable] public class PublicEntry { public string address; public string type; public string labels; public string role; public string meta; }
    [Serializable] public class PublicCatalog { public List<PublicEntry> entries = new List<PublicEntry>(); }
    [Serializable] public class PublicRowsWrapper { public PublicEntry[] rows; }

    private const string DefaultPublicJsonPath = "Assets/_Internal/Json/public_metadata.json";
    private PublicCatalog publicCatalog = new PublicCatalog();
    private string publicJsonPath = DefaultPublicJsonPath;

    private string modName = "MyMod";
    private string modVersion = "1.0.0";
    private string targetGroupName = "Mod_PublicAssets";
    private string search = "";
    private string modOutputDir = ""; // absolute folder where per-mod catalog/bundles are written
    private string filterLabel = "";
    private string filterFolder = ""; // prefix match on address path like "Weapons/357Magnum"
    private string selectedAddress = null;
    private HashSet<string> treeExpanded = new HashSet<string>();

    private Vector2 scrollAssign;
    private Vector2 scrollValidate;

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
    }

    // ---------- GUI ----------
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Mod SDK", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        // Config
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            modName    = EditorGUILayout.TextField("Mod Name", modName);
            modVersion = EditorGUILayout.TextField("Mod Version", modVersion);
            EditorGUILayout.LabelField("Public Metadata (JSON)", publicJsonPath);
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
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Load Mod (Temp)…", "Pick an existing per‑mod settings folder under _ModSDK_Temp"), GUILayout.Width(160)))
                {
                    var baseTemp = Path.Combine(Application.dataPath, "_ModSDK_Temp");
                    Directory.CreateDirectory(baseTemp);
                    var picked = EditorUtility.OpenFolderPanel("Select Mod Settings Folder", baseTemp, "");
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
                if (GUILayout.Button(new GUIContent("Create Mod", "Create per‑mod Addressables settings in _ModSDK_Temp/<ModName>"), GUILayout.Width(120)))
                {
                    var newName = San(string.IsNullOrEmpty(modName) ? "MyMod" : modName);
                    modName = newName;
                    var settings = GetOrCreatePerModSettings();
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
                        var modTempRoot = Path.Combine("Assets", "_ModSDK_Temp", newName).Replace('\\','/');
                        EnsureProfileVariable(settings, AddressableAssetSettings.kLocalBuildPath, modTempRoot);
                        EnsureProfileVariable(settings, AddressableAssetSettings.kLocalLoadPath, modTempRoot);
                        bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                        bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
                        settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupSchemaModified, group, true, false);
                        AssetDatabase.SaveAssets();
                        Debug.Log($"[ModSDK] Mod context ready: '{newName}' → Assets/_ModSDK_Temp/{newName}");
                    }
                }
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reload")) LoadPublicAddresses();
                if (GUILayout.Button("Reveal File"))
                {
                    var abs = GetAbsoluteFilePath(publicJsonPath);
                    if (!string.IsNullOrEmpty(abs))
                    {
                        if (File.Exists(abs)) EditorUtility.RevealInFinder(abs);
                        else EditorUtility.RevealInFinder(Path.GetDirectoryName(abs));
                    }
                }
            }
        }

        // Gate the rest of the UI until a mod context is selected/available
        var perModSettingsPath = Path.Combine("Assets", "_ModSDK_Temp", San(modName), "AddressableAssetSettings.asset");
        bool hasModContext = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(perModSettingsPath) != null;
        if (!hasModContext)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox("Select or create a mod above to continue.", MessageType.Info);
            return;
        }

        // ASSIGN
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("1) Assign addresses to selected assets", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            search = EditorGUILayout.TextField("Search addresses", search);
            using (new EditorGUILayout.HorizontalScope())
            {
                filterLabel = EditorGUILayout.TextField(new GUIContent("Label contains", "Filter entries whose labels contains this text"), filterLabel);
                filterFolder = EditorGUILayout.TextField(new GUIContent("Folder prefix", "Filter entries whose address starts with this folder path/prefix"), filterFolder);
            }
            var filtered = FilterPublic(search, filterLabel, filterFolder);

            EditorGUILayout.LabelField($"Matching: {filtered.Count} entries", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Expand All", GUILayout.Width(100)))
                    treeExpanded = new HashSet<string>(GetAllFolderPaths(filtered));
                if (GUILayout.Button("Collapse All", GUILayout.Width(100)))
                    treeExpanded.Clear();
                GUILayout.FlexibleSpace();
            }

            scrollAssign = EditorGUILayout.BeginScrollView(scrollAssign, GUILayout.MinHeight(220), GUILayout.MaxHeight(360));
            var root = BuildAddressTree(filtered);
            DrawAddressTree(root, 0);
            EditorGUILayout.EndScrollView();

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

            EditorGUILayout.HelpBox("Tip: select your asset(s) in Project, filter then browse like folders and click an address to select it.", MessageType.None);
        }

        // VALIDATE
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("2) Validate mod", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (GUILayout.Button("Run Validation"))
            {
                if (ValidateMod(out var issues))
                {
                    EditorUtility.DisplayDialog("Validation", "Validation passed.", "OK");
                }
                else
                {
                    var msg = issues != null && issues.Count > 0 ? ("Found " + issues.Count + " issue(s). See Console for details.") : "Validation failed.";
                    Debug.LogWarning(issues != null && issues.Count > 0 ? "[ModSDK] Validation issues:\n - " + string.Join("\n - ", issues) : "[ModSDK] Validation failed.");
                    EditorUtility.DisplayDialog("Validation failed", msg, "OK");
                }
            }
            scrollValidate = EditorGUILayout.BeginScrollView(scrollValidate, GUILayout.MinHeight(80), GUILayout.MaxHeight(180));
            EditorGUILayout.EndScrollView();
            EditorGUILayout.HelpBox("Checks: duplicates, spaces in addresses, type mismatches vs. public list, non-public overrides.", MessageType.None);
        }

        // BUILD
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("3) Build mod package", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (GUILayout.Button("Build Mod"))
                BuildMod();
            EditorGUILayout.HelpBox("If Mod Output Folder is set, the SDK builds a separate catalog + bundles into that folder (per‑mod). If not set, it falls back to copying bundles under StreamingAssets/aa/<Platform>/mods/<ModName>/ and uses the platform catalog.", MessageType.Info);
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
            (e.labels  ?? "").ToLowerInvariant().Contains(q)).ToList();
    }

    private List<PublicEntry> FilterPublic(string text, string labelContains, string folderPrefix)
    {
        var q = (text ?? "").Trim().ToLowerInvariant();
        var labelQ = (labelContains ?? "").Trim().ToLowerInvariant();
        var folder = (folderPrefix ?? "").Replace('\\','/').Trim();
        var list = publicCatalog.entries;
        IEnumerable<PublicEntry> res = list;
        if (!string.IsNullOrEmpty(q))
            res = res.Where(e => (e.address ?? "").ToLowerInvariant().Contains(q) || (e.type ?? "").ToLowerInvariant().Contains(q) || (e.role ?? "").ToLowerInvariant().Contains(q) || (e.labels ?? "").ToLowerInvariant().Contains(q));
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

    private void DrawAddressTree(TreeNode node, int indent)
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
                    var label = new GUIContent(child.name + (string.IsNullOrEmpty(child.entry?.type) ? "" : $"  [{child.entry.type}]"));
                    var style = isSel ? EditorStyles.boldLabel : EditorStyles.label;
                    if (GUILayout.Button(label, style, GUILayout.ExpandWidth(true)))
                        selectedAddress = child.fullPath;
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(12 * indent);
                    var expanded = treeExpanded.Contains(child.fullPath);
                    var newExpanded = EditorGUILayout.Foldout(expanded, child.name, true);
                    if (newExpanded != expanded)
                    {
                        if (newExpanded) treeExpanded.Add(child.fullPath);
                        else treeExpanded.Remove(child.fullPath);
                    }
                }
                if (treeExpanded.Contains(child.fullPath))
                    DrawAddressTree(child, indent + 1);
            }
        }
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

        if (issues.Count == 0) Debug.Log("[ModSDK] Validation passed.");
        else Debug.LogWarning($"[ModSDK] Validation found {issues.Count} issue(s):\n - " + string.Join("\n - ", issues));

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
            // Full clean: ensure destination is empty before building into it
            try
            {
                if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            }
            catch { /* ignore, recreate below */ }
            Directory.CreateDirectory(outDir);

            // Use or create the per‑mod Addressables settings in temp (so Assign and Build share the same settings)
            var tempRoot = Path.Combine("Assets", "_ModSDK_Temp", San(modName));
            Directory.CreateDirectory(tempRoot);
            var tempSettings = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(Path.Combine(tempRoot, "AddressableAssetSettings.asset"));
            if (tempSettings == null) tempSettings = AddressableAssetSettings.Create(tempRoot, "AddressableAssetSettings", true, true);

            // Use a dedicated profile; point Build/Load to the absolute mod folder
            var profiles = tempSettings.profileSettings;
            var profileId = tempSettings.activeProfileId;
            if (string.IsNullOrEmpty(profileId)) profileId = profiles.AddProfile("Mod", null);
            tempSettings.activeProfileId = profileId;

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

            var modGroup = tempSettings.CreateGroup("ModContent", false, false, false, schemas,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            tempSettings.DefaultGroup = modGroup;

            string modLabel = $"mod:{San(modName)}";
            foreach (var e in sourceGroup.entries.ToList())
            {
                if (e == null) continue;
                if (!(e.labels != null && e.labels.Contains(modLabel))) continue;
                var entry = tempSettings.CreateOrMoveEntry(e.guid, modGroup);
                entry.SetAddress(e.address);
            }

            bundled.BuildPath.SetVariableByName(tempSettings, AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(tempSettings, AddressableAssetSettings.kLocalLoadPath);

            // Ensure a Player data builder exists and is active (Packed Mode)
            var builderPath = Path.Combine(tempRoot, "BuildScriptPackedMode.asset");
            var packedBuilder = ScriptableObject.CreateInstance<BuildScriptPackedMode>();
            AssetDatabase.CreateAsset(packedBuilder, builderPath);
            tempSettings.AddDataBuilder(packedBuilder);
            tempSettings.ActivePlayerDataBuilderIndex = tempSettings.DataBuilders.IndexOf(packedBuilder);
            // PlayMode builder is irrelevant here but set to a sane default
            tempSettings.ActivePlayModeDataBuilderIndex = tempSettings.ActivePlayerDataBuilderIndex;

            AssetDatabase.SaveAssets();

            // Swap default settings to the temp settings for the build
            var previous = AddressableAssetSettingsDefaultObject.Settings;
            // Addressables (1.19) writes DefaultObject.asset under Assets/AddressableAssetsData on set; ensure dir exists and delete after
            var defaultObjDir = Path.Combine("Assets", "AddressableAssetsData");
            bool createdDefaultDir = false;
            if (!Directory.Exists(defaultObjDir)) { Directory.CreateDirectory(defaultObjDir); createdDefaultDir = true; AssetDatabase.Refresh(); }
            AddressableAssetSettingsDefaultObject.Settings = tempSettings;

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
            var tempBuildPath = ResolveProfilePath(tempSettings, AddressableAssetSettings.kLocalBuildPath, platform);
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
            // Also try the Library aa cache used by Addressables for this build target
            var libraryAA = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "com.unity.addressables", "aa", platform);
            if (Directory.Exists(libraryAA))
            {
                foreach (var file in Directory.GetFiles(libraryAA, "catalog*.*", SearchOption.AllDirectories))
                {
                    var name = Path.GetFileName(file);
                    var dst = Path.Combine(outDir, name);
                    try { if (!File.Exists(dst)) { File.Copy(file, dst, true); copiedCatalogs++; } } catch { }
                }
            }

            // Write a minimal modinfo.json if not present
            var infoPath = Path.Combine(outDir, "modinfo.json");
            if (!File.Exists(infoPath))
            {
                var info = "{\n" +
                           $"  \"name\": \"{Escape(modName)}\",\n" +
                           $"  \"version\": \"{Escape(modVersion)}\",\n" +
                           $"  \"gameVersion\": \"{Escape(PlayerSettings.bundleVersion)}\",\n" +
                           $"  \"unity\": \"{Escape(Application.unityVersion)}\",\n" +
                           $"  \"builtUtc\": \"{DateTime.UtcNow:O}\"\n" +
                           "}\n";
                File.WriteAllText(infoPath, info);
            }

            // Quick check: report expected load path and whether a catalog is present
            var expectedLoadPath = bundled.LoadPath.GetValue(tempSettings);
            var catalogs = Directory.GetFiles(outDir, "catalog*.*", SearchOption.AllDirectories).Length;
            Debug.Log($"[ModSDK] Per-mod catalog built. Expected LoadPath='{expectedLoadPath}'. Catalog files in outDir: {catalogs} (copied {copiedCatalogs}). Folder: {outDir}");
            EditorUtility.RevealInFinder(outDir);

            // Tokenize absolute InternalIds to {ModRoot} so they resolve on player machines
            PostprocessCatalogTokenize(outDir);
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
            var root = Path.Combine("Assets", "_ModSDK_Temp", San(modName));
            Directory.CreateDirectory(root);
            var assetPath = Path.Combine(root, "AddressableAssetSettings.asset");
            var settings = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(assetPath);
            if (settings == null)
            {
                // Persist the settings asset so the folder is not empty and can be reloaded later
                settings = AddressableAssetSettings.Create(root, "AddressableAssetSettings", true, true);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            return settings;
        }
        catch { return AddressableAssetSettingsDefaultObject.Settings; }
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
        char.IsLetterOrDigit(ch) || ch=='_' || ch=='-' ? ch : '_'));

    private static string Escape(string s) => (s ?? "").Replace("\\","\\\\").Replace("\"","\\\"");

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
                    meta = Grab("meta")
                });
            }
        }
        catch { /* fall back to empty */ }
        return list;
    }
}
#endif
