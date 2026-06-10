using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RetroInvaders {
    public static class RetroInvadersSceneBuilder {
        private const string RootPath = "Assets/Arcades/ArcadeGames/RetroInvaders";
        private const string MaterialsPath = RootPath + "/Materials";
        private const string PrefabsPath = RootPath + "/Prefabs";
        private const string ScenesPath = RootPath + "/Scenes";
        private const string ScriptableObjectsPath = RootPath + "/ScriptableObjects";
        private const string ConfigPath = ScriptableObjectsPath + "/RetroInvadersConfig.asset";
        private const string ScenePath = ScenesPath + "/RetroInvaders.unity";
        private const string PlayerPrefabPath = PrefabsPath + "/Player.prefab";
        private const string ProjectilePrefabPath = PrefabsPath + "/Projectile.prefab";
        private const string InvaderPrefabPath = PrefabsPath + "/Invader.prefab";
        private const string InvaderVariantPrefix = "Invader_Row_";
        private const string ShieldBlockPrefabPath = PrefabsPath + "/ShieldBlock.prefab";
        private const string UfoPrefabPath = PrefabsPath + "/UFO.prefab";
        private const string VfxFlashPrefabPath = PrefabsPath + "/VfxFlash.prefab";

        private static readonly string[][] InvaderPatterns = {
            new[] {
                "    ####    ",
                "   ######   ",
                "  ## AA ##  ",
                " ########## ",
                "## #### ####",
                "   #    #   ",
                "  #      #  ",
                "            "
            },
            new[] {
                "  #      #  ",
                "   #    #   ",
                "  ########  ",
                " ## AA AA ##",
                "############",
                "# ######## #",
                "# #      # #",
                "  ##    ##  "
            },
            new[] {
                "   ######   ",
                " ########## ",
                "### AA AA###",
                "############",
                "  ## ## ##  ",
                " ##  ##  ## ",
                "#          #",
                " ##      ## "
            },
            new[] {
                "  ##    ##  ",
                " ########## ",
                "###  AA  ###",
                "############",
                " ## #### ## ",
                "  ########  ",
                "   # ## #   ",
                "  ##    ##  "
            },
            new[] {
                "   ##  ##   ",
                "  ########  ",
                " ####AA#### ",
                "############",
                "  ########  ",
                " ## #  # ## ",
                " #        # ",
                "  ##    ##  "
            }
        };

        private static readonly Color[] InvaderBodyColors = {
            new Color(1.0f, 0.18f, 0.24f),
            new Color(1.0f, 0.58f, 0.1f),
            new Color(0.92f, 0.18f, 0.95f),
            new Color(0.16f, 0.9f, 1.0f),
            new Color(0.16f, 0.95f, 0.45f)
        };

        private static readonly Color[] InvaderAccentColors = {
            new Color(1.0f, 0.92f, 0.35f),
            new Color(1.0f, 0.96f, 0.42f),
            new Color(0.35f, 1.0f, 0.95f),
            new Color(0.95f, 0.35f, 1.0f),
            new Color(0.8f, 1.0f, 0.45f)
        };

        private static readonly string[] PlayerPattern = {
            "      A      ",
            "      A      ",
            "    AAAAA    ",
            "   HHHHHHH   ",
            " HHHHHHHHHHH ",
            "HHHHHHHHHHHHH",
            "  HHH   HHH  "
        };

        private static readonly string[] UfoPattern = {
            "      AAAA      ",
            "    AAAAAAAA    ",
            "  RRRRRRRRRRRR  ",
            "RRRRRRRRRRRRRRRR",
            "  RR  AA  RR    ",
            "    R      R    "
        };

        [MenuItem("Tools/Retro Invaders/Build Game Scene")]
        public static void BuildGameScene() {
            EnsureFolders();

            GameConfig config = EnsureConfig();
            Material borderMaterial = EnsureMaterial("MI_PlayAreaBorder", new Color(0.0f, 0.85f, 1.0f));
            Material floorMaterial = EnsureMaterial("MI_PlayfieldScreen", new Color(0.01f, 0.012f, 0.018f));
            Material playerHullMaterial = EnsureMaterial("MI_PlayerHull", new Color(0.08f, 0.95f, 0.38f));
            Material playerAccentMaterial = EnsureMaterial("MI_PlayerAccent", new Color(0.85f, 1.0f, 0.45f));
            Material playerProjectileMaterial = EnsureMaterial("MI_PlayerProjectile", new Color(0.98f, 1.0f, 0.55f));
            Material invaderProjectileMaterial = EnsureMaterial("MI_InvaderProjectile", new Color(1.0f, 0.08f, 0.24f));
            Material shieldMaterial = EnsureMaterial("MI_ShieldBlock", new Color(0.1f, 0.92f, 0.55f));
            Material ufoBodyMaterial = EnsureMaterial("MI_UfoBody", new Color(1.0f, 0.25f, 0.16f));
            Material ufoAccentMaterial = EnsureMaterial("MI_UfoAccent", new Color(1.0f, 0.95f, 0.35f));
            Material playerMuzzleMaterial = EnsureMaterial("MI_VfxPlayerMuzzle", new Color(1.0f, 1.0f, 0.25f));
            Material invaderMuzzleMaterial = EnsureMaterial("MI_VfxInvaderMuzzle", new Color(1.0f, 0.12f, 0.24f));
            Material invaderExplosionMaterial = EnsureMaterial("MI_VfxInvaderExplosion", new Color(1.0f, 0.35f, 1.0f));
            Material ufoExplosionMaterial = EnsureMaterial("MI_VfxUfoExplosion", new Color(1.0f, 0.62f, 0.04f));
            Material playerHitMaterial = EnsureMaterial("MI_VfxPlayerHit", new Color(0.32f, 1.0f, 0.72f));
            PlayerShip playerPrefab = BuildPlayerPrefab(config, playerHullMaterial, playerAccentMaterial);
            Projectile projectilePrefab = BuildProjectilePrefab(config, playerProjectileMaterial);
            Invader[] invaderPrefabs = BuildInvaderPrefabs(config);
            ShieldBlock shieldBlockPrefab = BuildShieldBlockPrefab(config, shieldMaterial);
            UfoShip ufoPrefab = BuildUfoPrefab(config, ufoBodyMaterial, ufoAccentMaterial);
            VfxFlash vfxFlashPrefab = BuildVfxFlashPrefab(playerMuzzleMaterial);

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildScene(
                config,
                borderMaterial,
                floorMaterial,
                playerPrefab,
                projectilePrefab,
                invaderPrefabs,
                shieldBlockPrefab,
                ufoPrefab,
                vfxFlashPrefab,
                playerProjectileMaterial,
                invaderProjectileMaterial,
                shieldMaterial,
                playerMuzzleMaterial,
                invaderMuzzleMaterial,
                invaderExplosionMaterial,
                ufoExplosionMaterial,
                playerHitMaterial
            );
            EditorSceneManager.SaveScene(scene, ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Retro Invaders scaffold scene built at " + ScenePath);
        }

        private static void EnsureFolders() {
            EnsureFolder("Assets", "Arcades");
            EnsureFolder("Assets/Arcades", "ArcadeGames");
            EnsureFolder("Assets/Arcades/ArcadeGames", "RetroInvaders");
            EnsureFolder(RootPath, "Scripts");
            EnsureFolder(RootPath + "/Scripts", "Core");
            EnsureFolder(RootPath + "/Scripts", "Gameplay");
            EnsureFolder(RootPath + "/Scripts", "UI");
            EnsureFolder(RootPath + "/Scripts", "VFX");
            EnsureFolder(RootPath + "/Scripts", "Audio");
            EnsureFolder(RootPath + "/Scripts", "Editor");
            EnsureFolder(RootPath, "Materials");
            EnsureFolder(RootPath, "Prefabs");
            EnsureFolder(RootPath, "Scenes");
            EnsureFolder(RootPath, "ScriptableObjects");
        }

        private static void EnsureFolder(string parent, string child) {
            string path = parent + "/" + child;

            if (!AssetDatabase.IsValidFolder(path)) {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static GameConfig EnsureConfig() {
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);

            if (config == null) {
                config = ScriptableObject.CreateInstance<GameConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            EditorUtility.SetDirty(config);
            return config;
        }

        private static Material EnsureMaterial(string name, Color color) {
            string path = MaterialsPath + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null) {
                Shader shader = Shader.Find("Unlit/Color");

                if (shader == null) {
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static PlayerShip BuildPlayerPrefab(GameConfig config, Material hullMaterial, Material accentMaterial) {
            GameObject root = new GameObject("Player");

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;

            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1.04f, 0.56f, 0.45f);

            Damageable damageable = root.AddComponent<Damageable>();
            damageable.Configure(Team.Player, 1, false);

            PlayerShip ship = root.AddComponent<PlayerShip>();
            root.AddComponent<PlayerController>();
            root.AddComponent<PlayerWeapon>();

            Transform visualRoot = new GameObject("Visual").transform;
            visualRoot.SetParent(root.transform, false);
            CreatePixelMask(
                visualRoot,
                "PlayerPixel",
                PlayerPattern,
                GetSpritePixelSize(config),
                0.22f,
                hullMaterial,
                accentMaterial,
                1.0f
            );

            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root.transform, false);
            muzzle.localPosition = new Vector3(0.0f, 0.36f, 0.0f);
            ship.SetMuzzle(muzzle);
            ship.SetDamageable(damageable);

            return SaveGeneratedPrefab<PlayerShip>(root, PlayerPrefabPath);
        }

        private static Projectile BuildProjectilePrefab(GameConfig config, Material playerProjectileMaterial) {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "Projectile";
            root.transform.localScale = GetReadableProjectileScale(config);

            Renderer renderer = root.GetComponent<Renderer>();
            if (renderer != null) {
                renderer.sharedMaterial = playerProjectileMaterial;
                PixelMeshUtility.ConfigureRendererForPixelArt(renderer);
            }

            BoxCollider trigger = root.GetComponent<BoxCollider>();
            if (trigger != null) {
                trigger.isTrigger = true;
            }

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;

            root.AddComponent<Projectile>();

            return SaveGeneratedPrefab<Projectile>(root, ProjectilePrefabPath);
        }

        private static Invader[] BuildInvaderPrefabs(GameConfig config) {
            int rowCount = config != null ? Mathf.Max(1, config.InvaderRows) : InvaderPatterns.Length;
            Invader[] prefabs = new Invader[rowCount];
            DeleteGeneratedInvaderVariantPrefabs();

            for (int row = 0; row < rowCount; row++) {
                Material bodyMaterial = EnsureMaterial(GetInvaderBodyMaterialName(row), GetWrappedColor(InvaderBodyColors, row));
                Material accentMaterial = EnsureMaterial(GetInvaderAccentMaterialName(row), GetWrappedColor(InvaderAccentColors, row));
                prefabs[row] = BuildInvaderPrefab(
                    config,
                    InvaderPatterns[row % InvaderPatterns.Length],
                    bodyMaterial,
                    accentMaterial,
                    GetInvaderPrefabPath(row)
                );
            }

            return prefabs;
        }

        private static Invader BuildInvaderPrefab(GameConfig config, string[] pattern, Material bodyMaterial, Material accentMaterial, string path) {
            GameObject root = new GameObject("Invader");

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;

            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(0.9f, 0.58f, 0.42f);

            Damageable damageable = root.AddComponent<Damageable>();
            damageable.Configure(Team.Invader, config.InvaderHealth, false);

            Invader invader = root.AddComponent<Invader>();

            Transform visualRoot = new GameObject("Visual").transform;
            visualRoot.SetParent(root.transform, false);

            CreatePixelMask(
                visualRoot,
                "InvaderPixel",
                pattern,
                GetSpritePixelSize(config),
                0.22f,
                bodyMaterial,
                accentMaterial,
                1.0f
            );

            invader.ConfigurePrefab(damageable, visualRoot, new Vector2(0.45f, 0.29f));

            return SaveGeneratedPrefab<Invader>(root, path);
        }

        private static ShieldBlock BuildShieldBlockPrefab(GameConfig config, Material shieldMaterial) {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "ShieldBlock";

            Renderer renderer = root.GetComponent<Renderer>();
            if (renderer != null) {
                renderer.sharedMaterial = shieldMaterial;
                PixelMeshUtility.ConfigureRendererForPixelArt(renderer);
            }

            BoxCollider trigger = root.GetComponent<BoxCollider>();
            if (trigger != null) {
                trigger.isTrigger = true;
            }

            Damageable damageable = root.AddComponent<Damageable>();
            damageable.Configure(Team.Neutral, config.ShieldBlocksHealth, true);

            ShieldBlock block = root.AddComponent<ShieldBlock>();
            block.Configure(config.ShieldBlocksHealth, new Vector3(config.ShieldBlockSize.x, config.ShieldBlockSize.y, 0.18f), shieldMaterial);

            return SaveGeneratedPrefab<ShieldBlock>(root, ShieldBlockPrefabPath);
        }

        private static UfoShip BuildUfoPrefab(GameConfig config, Material bodyMaterial, Material accentMaterial) {
            GameObject root = new GameObject("UFO");

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;

            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1.18f, 0.42f, 0.42f);

            Damageable damageable = root.AddComponent<Damageable>();
            damageable.Configure(Team.Invader, 1, false);

            UfoShip ufo = root.AddComponent<UfoShip>();

            Transform visualRoot = new GameObject("Visual").transform;
            visualRoot.SetParent(root.transform, false);

            CreatePixelMask(
                visualRoot,
                "UfoPixel",
                UfoPattern,
                GetSpritePixelSize(config),
                0.24f,
                bodyMaterial,
                accentMaterial,
                1.0f
            );

            ufo.ConfigurePrefab(damageable, visualRoot, new Vector2(0.6f, 0.24f));

            return SaveGeneratedPrefab<UfoShip>(root, UfoPrefabPath);
        }

        private static VfxFlash BuildVfxFlashPrefab(Material material) {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = "VfxFlash";
            root.transform.localScale = new Vector3(0.35f, 0.35f, 0.08f);

            Renderer renderer = root.GetComponent<Renderer>();
            if (renderer != null) {
                renderer.sharedMaterial = material;
                PixelMeshUtility.ConfigureRendererForPixelArt(renderer);
            }

            Collider collider = root.GetComponent<Collider>();
            if (collider != null) {
                Object.DestroyImmediate(collider);
            }

            root.AddComponent<VfxFlash>();

            return SaveGeneratedPrefab<VfxFlash>(root, VfxFlashPrefabPath);
        }

        private static void BuildScene(
            GameConfig config,
            Material borderMaterial,
            Material floorMaterial,
            PlayerShip playerPrefab,
            Projectile projectilePrefab,
            Invader[] invaderPrefabs,
            ShieldBlock shieldBlockPrefab,
            UfoShip ufoPrefab,
            VfxFlash vfxFlashPrefab,
            Material playerProjectileMaterial,
            Material invaderProjectileMaterial,
            Material shieldMaterial,
            Material playerMuzzleMaterial,
            Material invaderMuzzleMaterial,
            Material invaderExplosionMaterial,
            Material ufoExplosionMaterial,
            Material playerHitMaterial
        ) {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.65f);

            GameObject root = new GameObject("RetroInvaders_GameRoot");
            PlayArea playArea = CreatePlayArea(root.transform, config, borderMaterial, floorMaterial);
            Camera camera = CreateCamera(config);
            ScreenShake screenShake = CreateScreenShake(camera, config);
            CreateLight(root.transform);
            Transform playerRoot = CreatePlayerRoot(root.transform);
            AudioManager audioManager = CreateAudioManager(root.transform, config);
            VfxSpawner vfxSpawner = CreateVfxSpawner(root.transform, config, vfxFlashPrefab, playerMuzzleMaterial, invaderMuzzleMaterial, invaderExplosionMaterial, ufoExplosionMaterial, playerHitMaterial);
            ProjectilePool projectilePool = CreateProjectilePool(root.transform, config, playArea, projectilePrefab, playerProjectileMaterial, invaderProjectileMaterial);
            InvaderFleetController invaderFleet = CreateInvaderFleet(root.transform, config, playArea, invaderPrefabs, projectilePool);
            ShieldController shieldController = CreateShieldController(root.transform, config, playArea, shieldBlockPrefab, shieldMaterial);
            UfoController ufoController = CreateUfoController(root.transform, config, playArea, ufoPrefab);
            GameSession session = CreateGameSession(root.transform, config, playArea, playerPrefab, playerRoot, projectilePool, invaderFleet, shieldController, ufoController);
            invaderFleet.Configure(session, config, playArea, invaderPrefabs, projectilePool);
            ufoController.Configure(session, config, playArea, ufoPrefab);
            CreateEventSystem(root.transform);
            ScorePopupController scorePopupController = CreateHud(root.transform, session, camera);
            session.SetScorePopupController(scorePopupController);
            session.SetFeedbackSystems(audioManager, vfxSpawner, screenShake);
        }

        private static PlayArea CreatePlayArea(Transform root, GameConfig config, Material borderMaterial, Material floorMaterial) {
            GameObject playAreaObject = new GameObject("PlayArea");
            playAreaObject.transform.SetParent(root);

            PlayArea playArea = playAreaObject.AddComponent<PlayArea>();
            playArea.Configure(config);

            GameObject borderRoot = new GameObject("Border");
            borderRoot.transform.SetParent(playAreaObject.transform);

            Vector2 center = config.PlayfieldCenter;
            Vector2 size = config.PlayfieldSize;
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            float thickness = 0.08f;

            CreateCube("Screen Plane", playAreaObject.transform, new Vector3(center.x, center.y, 0.2f), new Vector3(size.x, size.y, 0.04f), floorMaterial);
            CreateCube("Top", borderRoot.transform, new Vector3(center.x, center.y + halfHeight, 0.0f), new Vector3(size.x, thickness, 0.12f), borderMaterial);
            CreateCube("Bottom", borderRoot.transform, new Vector3(center.x, center.y - halfHeight, 0.0f), new Vector3(size.x, thickness, 0.12f), borderMaterial);
            CreateCube("Left", borderRoot.transform, new Vector3(center.x - halfWidth, center.y, 0.0f), new Vector3(thickness, size.y, 0.12f), borderMaterial);
            CreateCube("Right", borderRoot.transform, new Vector3(center.x + halfWidth, center.y, 0.0f), new Vector3(thickness, size.y, 0.12f), borderMaterial);

            return playArea;
        }

        private static Camera CreateCamera(GameConfig config) {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(config.PlayfieldCenter.x, config.PlayfieldCenter.y, -10.0f);
            cameraObject.transform.rotation = Quaternion.identity;

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            float targetAspect = 16.0f / 9.0f;
            float heightSize = config.PlayfieldSize.y * 0.5f;
            float widthSize = config.PlayfieldSize.x / targetAspect * 0.5f;
            camera.orthographicSize = Mathf.Max(heightSize, widthSize) + 0.7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.005f, 0.006f, 0.01f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 50.0f;
            return camera;
        }

        private static ScreenShake CreateScreenShake(Camera camera, GameConfig config) {
            if (camera == null) {
                return null;
            }

            ScreenShake shake = camera.gameObject.AddComponent<ScreenShake>();
            shake.Configure(config);
            return shake;
        }

        private static void CreateLight(Transform root) {
            GameObject lightObject = new GameObject("Directional Light");
            lightObject.transform.SetParent(root);
            lightObject.transform.rotation = Quaternion.Euler(35.0f, -25.0f, 0.0f);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.65f;
        }

        private static Transform CreatePlayerRoot(Transform root) {
            GameObject playerRoot = new GameObject("PlayerRoot");
            playerRoot.transform.SetParent(root);
            return playerRoot.transform;
        }

        private static AudioManager CreateAudioManager(Transform root, GameConfig config) {
            GameObject audioObject = new GameObject("AudioManager");
            audioObject.transform.SetParent(root);
            audioObject.AddComponent<AudioSource>();

            AudioManager audioManager = audioObject.AddComponent<AudioManager>();
            audioManager.Configure(config);
            return audioManager;
        }

        private static VfxSpawner CreateVfxSpawner(
            Transform root,
            GameConfig config,
            VfxFlash flashPrefab,
            Material playerMuzzleMaterial,
            Material invaderMuzzleMaterial,
            Material invaderExplosionMaterial,
            Material ufoExplosionMaterial,
            Material playerHitMaterial
        ) {
            GameObject vfxObject = new GameObject("VfxSpawner");
            vfxObject.transform.SetParent(root);

            VfxSpawner spawner = vfxObject.AddComponent<VfxSpawner>();
            spawner.Configure(
                config,
                flashPrefab,
                playerMuzzleMaterial,
                invaderMuzzleMaterial,
                invaderExplosionMaterial,
                ufoExplosionMaterial,
                playerHitMaterial
            );
            return spawner;
        }

        private static ProjectilePool CreateProjectilePool(
            Transform root,
            GameConfig config,
            PlayArea playArea,
            Projectile projectilePrefab,
            Material playerProjectileMaterial,
            Material invaderProjectileMaterial
        ) {
            GameObject poolObject = new GameObject("ProjectilePool");
            poolObject.transform.SetParent(root);

            ProjectilePool pool = poolObject.AddComponent<ProjectilePool>();
            pool.Configure(
                projectilePrefab,
                playArea,
                playerProjectileMaterial,
                invaderProjectileMaterial,
                config.ProjectileLifetime,
                config.ProjectilePoolInitialSize,
                config.ProjectilePoolMaxSize
            );
            return pool;
        }

        private static GameSession CreateGameSession(
            Transform root,
            GameConfig config,
            PlayArea playArea,
            PlayerShip playerPrefab,
            Transform playerRoot,
            ProjectilePool projectilePool,
            InvaderFleetController invaderFleet,
            ShieldController shieldController,
            UfoController ufoController
        ) {
            GameObject sessionObject = new GameObject("GameSession");
            sessionObject.transform.SetParent(root);

            GameSession session = sessionObject.AddComponent<GameSession>();
            session.Configure(config, playArea, playerPrefab, playerRoot, projectilePool, invaderFleet, shieldController, ufoController);
            return session;
        }

        private static InvaderFleetController CreateInvaderFleet(
            Transform root,
            GameConfig config,
            PlayArea playArea,
            Invader[] invaderPrefabs,
            ProjectilePool projectilePool
        ) {
            GameObject fleetObject = new GameObject("InvaderFleet");
            fleetObject.transform.SetParent(root);

            InvaderFleetController fleet = fleetObject.AddComponent<InvaderFleetController>();
            fleet.Configure(null, config, playArea, invaderPrefabs, projectilePool);
            return fleet;
        }

        private static ShieldController CreateShieldController(
            Transform root,
            GameConfig config,
            PlayArea playArea,
            ShieldBlock shieldBlockPrefab,
            Material shieldMaterial
        ) {
            GameObject shieldObject = new GameObject("ShieldController");
            shieldObject.transform.SetParent(root);

            ShieldController controller = shieldObject.AddComponent<ShieldController>();
            controller.Configure(config, playArea, shieldBlockPrefab, shieldMaterial);
            return controller;
        }

        private static UfoController CreateUfoController(Transform root, GameConfig config, PlayArea playArea, UfoShip ufoPrefab) {
            GameObject ufoObject = new GameObject("UfoController");
            ufoObject.transform.SetParent(root);

            UfoController controller = ufoObject.AddComponent<UfoController>();
            controller.Configure(null, config, playArea, ufoPrefab);
            return controller;
        }

        private static void CreateEventSystem(Transform root) {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.transform.SetParent(root);
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private static ScorePopupController CreateHud(Transform root, GameSession session, Camera worldCamera) {
            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            GameObject canvasObject = new GameObject("HUD Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(root, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920.0f, 1080.0f);
            scaler.matchWidthOrHeight = 0.5f;

            Text score = CreateText(canvasObject.transform, "ScoreText", font, 30, TextAnchor.UpperLeft, new Vector2(0.0f, 1.0f), new Vector2(0.0f, 1.0f), new Vector2(36.0f, -30.0f), new Vector2(430.0f, 46.0f));
            Text highScore = CreateText(canvasObject.transform, "HighScoreText", font, 30, TextAnchor.UpperCenter, new Vector2(0.5f, 1.0f), new Vector2(0.5f, 1.0f), new Vector2(0.0f, -30.0f), new Vector2(460.0f, 46.0f));
            Text lives = CreateText(canvasObject.transform, "LivesText", font, 30, TextAnchor.UpperRight, new Vector2(1.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(-36.0f, -30.0f), new Vector2(320.0f, 46.0f));
            Text wave = CreateText(canvasObject.transform, "WaveText", font, 26, TextAnchor.UpperRight, new Vector2(1.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(-36.0f, -74.0f), new Vector2(320.0f, 40.0f));

            GameObject panel = CreateMessagePanel(canvasObject.transform);
            Text title = CreateText(panel.transform, "TitleText", font, 64, TextAnchor.MiddleCenter, new Vector2(0.0f, 0.68f), new Vector2(1.0f, 1.0f), Vector2.zero, Vector2.zero);
            Text body = CreateText(panel.transform, "BodyText", font, 25, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.68f), Vector2.zero, Vector2.zero);
            Text prompt = CreateText(panel.transform, "PromptText", font, 30, TextAnchor.MiddleCenter, new Vector2(0.0f, 0.1f), new Vector2(1.0f, 0.28f), Vector2.zero, Vector2.zero);
            Text footer = CreateText(panel.transform, "FooterText", font, 18, TextAnchor.MiddleCenter, new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.1f), Vector2.zero, Vector2.zero);
            footer.color = new Color(0.62f, 0.86f, 0.9f, 1.0f);

            HUDController hud = canvasObject.AddComponent<HUDController>();
            hud.Configure(session, score, highScore, lives, wave, panel, title, body, prompt, footer);

            ScorePopupController scorePopupController = CreateScorePopupController(canvasObject.transform, session != null ? session.Config : null, worldCamera, font);
            return scorePopupController;
        }

        private static ScorePopupController CreateScorePopupController(Transform canvasRoot, GameConfig config, Camera worldCamera, Font font) {
            GameObject popupRootObject = new GameObject("Score Popups", typeof(RectTransform));
            popupRootObject.transform.SetParent(canvasRoot, false);

            RectTransform popupRoot = popupRootObject.GetComponent<RectTransform>();
            popupRoot.anchorMin = Vector2.zero;
            popupRoot.anchorMax = Vector2.one;
            popupRoot.pivot = new Vector2(0.5f, 0.5f);
            popupRoot.anchoredPosition = Vector2.zero;
            popupRoot.sizeDelta = Vector2.zero;

            Text templateText = CreateText(popupRoot, "ScorePopupTemplate", font, 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150.0f, 44.0f));
            templateText.color = new Color(1.0f, 0.95f, 0.35f, 1.0f);
            ScorePopup template = templateText.gameObject.AddComponent<ScorePopup>();
            template.Hide();

            ScorePopupController controller = popupRootObject.AddComponent<ScorePopupController>();
            controller.Configure(config, worldCamera, popupRoot, template);
            return controller;
        }

        private static GameObject CreateMessagePanel(Transform parent) {
            GameObject panel = new GameObject("Center Message Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(900.0f, 390.0f);

            Image image = panel.GetComponent<Image>();
            image.color = new Color(0.0f, 0.0f, 0.0f, 0.68f);
            image.raycastTarget = false;
            return panel;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            Font font,
            int fontSize,
            TextAnchor alignment,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta
        ) {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.86f, 0.98f, 1.0f, 1.0f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            Outline outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.0f, 0.0f, 0.0f, 0.9f);
            outline.effectDistance = new Vector2(2.0f, -2.0f);
            return text;
        }

        private static Vector3 GetReadableProjectileScale(GameConfig config) {
            Vector3 scale = config != null ? config.ProjectileVisualScale : new Vector3(0.18f, 0.48f, 0.18f);
            scale.x = Mathf.Max(0.18f, scale.x);
            scale.y = Mathf.Max(0.48f, scale.y);
            scale.z = Mathf.Max(0.18f, scale.z);
            return scale;
        }

        private static string GetInvaderPrefabPath(int row) {
            if (row <= 0) {
                return InvaderPrefabPath;
            }

            return PrefabsPath + "/" + InvaderVariantPrefix + (row + 1).ToString("00") + ".prefab";
        }

        private static string GetInvaderBodyMaterialName(int row) {
            return row <= 0 ? "MI_InvaderBody" : "MI_InvaderBody_Row_" + (row + 1).ToString("00");
        }

        private static string GetInvaderAccentMaterialName(int row) {
            return row <= 0 ? "MI_InvaderAccent" : "MI_InvaderAccent_Row_" + (row + 1).ToString("00");
        }

        private static Color GetWrappedColor(Color[] colors, int index) {
            if (colors == null || colors.Length == 0) {
                return Color.white;
            }

            return colors[Mathf.Abs(index) % colors.Length];
        }

        private static void DeleteGeneratedInvaderVariantPrefabs() {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsPath });

            for (int i = 0; i < guids.Length; i++) {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                int slashIndex = path.LastIndexOf('/');
                string fileName = slashIndex >= 0 ? path.Substring(slashIndex + 1) : path;

                if (fileName.StartsWith(InvaderVariantPrefix) && fileName.EndsWith(".prefab")) {
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

        private static Vector2 GetSpritePixelSize(GameConfig config) {
            Vector2 size = config != null ? config.SpritePixelSize : new Vector2(0.07f, 0.06f);
            size.x = Mathf.Max(0.02f, size.x);
            size.y = Mathf.Max(0.02f, size.y);
            return size;
        }

        private static void CreatePixelMask(
            Transform parent,
            string prefix,
            string[] pattern,
            Vector2 pixelSize,
            float depth,
            Material primaryMaterial,
            Material accentMaterial,
            float fill
        ) {
            if (pattern == null || pattern.Length == 0) {
                return;
            }

            int width = pattern[0].Length;
            int created = 0;
            float xOrigin = (width - 1) * -0.5f;
            float yOrigin = (pattern.Length - 1) * 0.5f;
            Vector3 pixelScale = new Vector3(pixelSize.x * fill, pixelSize.y * fill, depth);

            for (int row = 0; row < pattern.Length; row++) {
                string line = pattern[row];
                for (int column = 0; column < width && column < line.Length; column++) {
                    char key = line[column];
                    Material material = GetPixelMaterial(key, primaryMaterial, accentMaterial);
                    if (material == null) {
                        continue;
                    }

                    Vector3 position = new Vector3((xOrigin + column) * pixelSize.x, (yOrigin - row) * pixelSize.y, 0.0f);
                    CreatePrefabPart(parent, prefix + "_" + created.ToString("00"), position, pixelScale, material);
                    created++;
                }
            }
        }

        private static Material GetPixelMaterial(char key, Material primaryMaterial, Material accentMaterial) {
            switch (key) {
                case '#':
                case 'H':
                case 'R':
                    return primaryMaterial;
                case 'A':
                    return accentMaterial;
                default:
                    return null;
            }
        }

        private static T SaveGeneratedPrefab<T>(GameObject root, string path) where T : Component {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) {
                AssetDatabase.DeleteAsset(path);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab != null ? prefab.GetComponent<T>() : null;
        }

        private static GameObject CreatePrefabPart(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material) {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null) {
                renderer.sharedMaterial = material;
                PixelMeshUtility.ConfigureRendererForPixelArt(renderer);
            }

            Collider collider = part.GetComponent<Collider>();
            if (collider != null) {
                Object.DestroyImmediate(collider);
            }

            return part;
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 position, Vector3 scale, Material material) {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent);
            cube.transform.position = position;
            cube.transform.localScale = scale;

            Renderer renderer = cube.GetComponent<Renderer>();
            if (renderer != null) {
                renderer.sharedMaterial = material;
                PixelMeshUtility.ConfigureRendererForPixelArt(renderer);
            }

            Collider collider = cube.GetComponent<Collider>();
            if (collider != null) {
                Object.DestroyImmediate(collider);
            }

            return cube;
        }

    }
}
