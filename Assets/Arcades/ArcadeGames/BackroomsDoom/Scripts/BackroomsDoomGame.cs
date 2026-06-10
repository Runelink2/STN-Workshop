using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum BackroomsDoomSound {
    Shot,
    DryFire,
    Hit,
    Kill,
    Pickup,
    Hurt,
    Door,
    EntityMoan
}

public sealed class BackroomsDoomGame : MonoBehaviour {
    [Header("Software Renderer")]
    [SerializeField] private int renderWidth = 320;
    [SerializeField] private int renderHeight = 200;
    [SerializeField] private float horizontalFov = 82.0f;
    [SerializeField] private bool showHud = true;

    [Header("Arcade Output")]
    [SerializeField] private Camera outputCamera;
    [SerializeField] private bool showStandaloneGui = true;

    [Header("Player")]
    [SerializeField] private int playerMaxHealth = 100;
    [SerializeField] private int startingAmmo = 24;
    [SerializeField] private int maxAmmo = 99;
    [SerializeField] private float walkSpeed = 3.0f;
    [SerializeField] private float sprintSpeed = 4.8f;
    [SerializeField] private float turnSpeed = 115.0f;
    [SerializeField] private float mouseSensitivity = 2.1f;
    [SerializeField] private float interactRange = 2.2f;

    [Header("Rules")]
    [SerializeField] private int healthPickupAmount = 25;
    [SerializeField] private int ammoPickupAmount = 20;
    [SerializeField] private int hazardDamage = 10;
    [SerializeField] private float hazardInterval = 0.6f;

    [Header("Horror Mood")]
    [SerializeField] private float worldDarkness = 0.82f;
    [SerializeField] private float lightFlickerStrength = 0.18f;
    [SerializeField] private float dreadRange = 7.0f;
    [SerializeField] private float closeMoanInterval = 3.25f;

    private const float EyeHeight = 0.52f;
    private const float WallHeight = 1.0f;
    private const float NearClip = 0.035f;
    private const float PlayerRadius = 0.22f;
    private const float MaxTraceRange = 18.0f;
    private const int StatusBarHeight = 32;
    private const int SampleRate = 22050;
    private const int ShadeLevelCount = 18;
    private const int PlanePixelStep = 2;

    // The ASCII grid remains only as authoring input. At startup it is converted
    // into Doom-like linedefs, door specials, sectors, and things.
    private static readonly string[] MapRows = {
        "##########################",
        "#P..L....#....L...#......#",
        "#........#........#..E...#",
        "#..####..D..####..D......#",
        "#..#..#..#..#..#..#..L...#",
        "#..#L.#..A..#..#.........#",
        "#..D..#######..####D######",
        "#..#..............#......#",
        "#..#..E...L.......#..H...#",
        "#..#######..####..#......#",
        "#..~~....D.....#..D..E...#",
        "#..~~..L.#.....#..#......#",
        "#####D####..E..#..########",
        "#........#.....#.........#",
        "#..L.....D..L..D....L....#",
        "#........#.....#.........#",
        "#..E..A..#..####..E......#",
        "#........#.....#.....H...#",
        "#....L...D.....D....L....#",
        "##########################"
    };

    private enum LineKind {
        Wall,
        Door
    }

    private enum ThingKind {
        Enemy,
        Health,
        Ammo,
        Puff,
        Furniture,
        StopSign
    }

    private enum ThingState {
        Alive,
        Pain,
        Dying
    }

    private sealed class TextureData {
        public readonly int Width;
        public readonly int Height;
        public readonly Color32[] Pixels;
        private Color32[] shadedPixels;

        public TextureData(int width, int height) {
            Width = width;
            Height = height;
            Pixels = new Color32[width * height];
        }

        public void SetPixel(int x, int y, Color32 color) {
            Pixels[y * Width + x] = color;
        }

        public Color32 Sample(float u, float v) {
            int x = Mathf.FloorToInt(u * Width) % Width;
            int y = Mathf.FloorToInt(v * Height) % Height;
            if (x < 0) {
                x += Width;
            }
            if (y < 0) {
                y += Height;
            }
            return Pixels[y * Width + x];
        }

        public Color32 Sample32(float u, float v) {
            int x = FastFloorToInt(u * 32.0f) & 31;
            int y = FastFloorToInt(v * 32.0f) & 31;
            return Pixels[(y << 5) + x];
        }

        public int Sample32Index(float u, float v) {
            int x = FastFloorToInt(u * 32.0f) & 31;
            int y = FastFloorToInt(v * 32.0f) & 31;
            return (y << 5) + x;
        }

        public Color32 Shaded32At(int pixelIndex, int shadeLevel) {
            return shadedPixels[(shadeLevel << 10) + pixelIndex];
        }

        public Color32 Sample32Shaded(float u, float v, int shadeLevel) {
            int x = FastFloorToInt(u * 32.0f) & 31;
            int y = FastFloorToInt(v * 32.0f) & 31;
            return shadedPixels[(shadeLevel << 10) + (y << 5) + x];
        }

        public Color32 SampleShaded(float u, float v, int shadeLevel) {
            int x = FastFloorToInt(u * Width) % Width;
            int y = FastFloorToInt(v * Height) % Height;
            if (x < 0) {
                x += Width;
            }
            if (y < 0) {
                y += Height;
            }
            return shadedPixels[shadeLevel * Pixels.Length + y * Width + x];
        }

        public void BuildShadeTable() {
            int pixelCount = Pixels.Length;
            shadedPixels = new Color32[(ShadeLevelCount + 1) * pixelCount];
            for (int level = 0; level <= ShadeLevelCount; level++) {
                int offset = level * pixelCount;
                for (int i = 0; i < pixelCount; i++) {
                    shadedPixels[offset + i] = ApplyShadeLevel(Pixels[i], level);
                }
            }
        }

        private static int FastFloorToInt(float value) {
            int integer = (int)value;
            return value < integer ? integer - 1 : integer;
        }
    }

    private sealed class Sector {
        public float floorHeight;
        public float ceilingHeight;
        public float light;
        public TextureData floorTexture;
        public TextureData ceilingTexture;
    }

    private sealed class LineDef {
        public Vector2 a;
        public Vector2 b;
        public LineKind kind;
        public int frontSector;
        public int backSector;
        public int doorIndex = -1;
        public float light = 0.7f;
        public TextureData texture;
    }

    private sealed class Door {
        public LineDef line;
        public bool targetOpen;
        public float open;
        public float speed = 1.7f;
    }

    private sealed class Thing {
        public ThingKind kind;
        public ThingState state;
        public float x;
        public float y;
        public float z;
        public float radius;
        public float width;
        public float height;
        public float angle;
        public float life;
        public float cooldown;
        public int health;
        public int variant;
        public float viewDepth;
    }

    private Texture2D screenTexture;
    private Color32[] frame;
    private Color32[] uploadFrame;
    private float[] columnDepth;
    private float[] columnCameraX;
    private float[] floorCeilingRowDistance;
    private float[] floorCeilingRegularShadeBase;
    private float[] floorCeilingLightShadeBase;
    private bool[] floorCeilingRowIsFloor;
    private int[] frameRowOffsets;
    private float[] dreadVignetteEdge;
    private readonly List<LineDef> lines = new List<LineDef>(256);
    private readonly List<Door> doors = new List<Door>(16);
    private readonly List<Sector> sectors = new List<Sector>(4);
    private readonly List<Thing> things = new List<Thing>(32);
    private readonly List<Thing> visibleThings = new List<Thing>(32);

    private TextureData wallTexture;
    private TextureData doorTexture;
    private TextureData floorTexture;
    private TextureData ceilingTexture;
    private TextureData hazardTexture;
    private TextureData enemyTexture;
    private TextureData enemyPainTexture;
    private TextureData corpseTexture;
    private TextureData healthTexture;
    private TextureData ammoTexture;
    private TextureData puffTexture;
    private TextureData[] furnitureTextures;
    private TextureData stopSignTexture;
    private TextureData weaponTexture;
    private TextureData weaponFlashTexture;

    private char[,] map;
    private float[,] lightMap;
    private char[] mapCells;
    private float[] lightCells;
    private int mapWidth;
    private int mapHeight;
    private float playerX;
    private float playerY;
    private float playerAngle;
    private int health;
    private int ammo;
    private bool dead;
    private float nextHazardTick;
    private float fireCooldown;
    private float weaponKick;
    private float muzzleFlashTimer;
    private float damageFlashTimer;
    private float messageTimer;
    private string messageText = "";
    private float nearestLivingEnemyDistance = 999.0f;
    private float worldLightMultiplier = 0.64f;
    private float dreadIntensity;
    private float nextMoanTime;
    private AudioSource audioSource;
    private AudioSource ambienceSource;
    private AudioClip[] soundClips;
    private float focalLength;
    private int horizonY;
    private int sceneHeight;
    private bool arcadeOutputMode;
    private Canvas outputCanvas;
    private RawImage outputImage;

    public bool InputActive {
        get { return !dead && (arcadeOutputMode || Cursor.lockState == CursorLockMode.Locked); }
    }

    private void Start() {
        ConfigureUnityHost();
        CreateRenderer();
        CreateSoftwareAssets();
        CreateSounds();
        RestartRun();

        Debug.Log("Backrooms Doom software renderer ready. The copied Doom layout now runs as a fluorescent liminal horror maze.");
    }

    private void Update() {
        if (dead) {
            if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)) {
                if (arcadeOutputMode) {
                    RestartRun();
                } else {
                    SceneManager.LoadScene(SceneManager.GetActiveScene().path);
                }
            }
        } else {
            UpdateCursorLock();
            UpdatePlayer();
            UpdateInteraction();
            UpdateWeapon();
            UpdateDoors();
            UpdateThings();
            UpdatePickups();
            UpdateHazards();
            UpdateHorrorMood();
        }

        fireCooldown = Mathf.Max(0.0f, fireCooldown - Time.deltaTime);
        weaponKick = Mathf.MoveTowards(weaponKick, 0.0f, Time.deltaTime * 0.65f);
        muzzleFlashTimer = Mathf.Max(0.0f, muzzleFlashTimer - Time.deltaTime);
        damageFlashTimer = Mathf.Max(0.0f, damageFlashTimer - Time.deltaTime);
        messageTimer = Mathf.Max(0.0f, messageTimer - Time.deltaTime);

        RenderFrame();
        UploadFrame();
    }

    private void OnGUI() {
        if (arcadeOutputMode || !showStandaloneGui || screenTexture == null || Event.current.type != EventType.Repaint) {
            return;
        }

        GUI.depth = 1000;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0.0f, 0.0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float targetAspect = renderWidth / (float)renderHeight;
        float screenAspect = Screen.width / (float)Screen.height;
        Rect rect;
        if (screenAspect > targetAspect) {
            float width = Screen.height * targetAspect;
            rect = new Rect((Screen.width - width) * 0.5f, 0.0f, width, Screen.height);
        } else {
            float height = Screen.width / targetAspect;
            rect = new Rect(0.0f, (Screen.height - height) * 0.5f, Screen.width, height);
        }
        GUI.DrawTexture(rect, screenTexture, ScaleMode.StretchToFill, false);
    }

    private void ConfigureUnityHost() {
        Application.targetFrameRate = 60;
        QualitySettings.pixelLightCount = 0;
        RenderSettings.fog = false;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;

        Camera mainCamera = ResolveOutputCamera();
        if (mainCamera != null) {
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = Color.black;
            mainCamera.orthographic = true;
            arcadeOutputMode = mainCamera.targetTexture != null;
            mainCamera.cullingMask = 0;
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.0f;
        audioSource.playOnAwake = false;

        ambienceSource = gameObject.AddComponent<AudioSource>();
        ambienceSource.spatialBlend = 0.0f;
        ambienceSource.playOnAwake = false;
        ambienceSource.loop = true;
        ambienceSource.volume = 0.14f;
    }

    private Camera ResolveOutputCamera() {
        if (outputCamera != null) {
            return outputCamera;
        }

        outputCamera = GetComponent<Camera>();
        if (outputCamera == null) {
            outputCamera = GetComponentInChildren<Camera>();
        }
        if (outputCamera == null) {
            outputCamera = GetComponentInParent<Camera>();
        }
        if (outputCamera == null) {
            outputCamera = Camera.main;
        }
        return outputCamera;
    }

    private void CreateRenderer() {
        renderWidth = Mathf.Clamp(renderWidth, 160, 640);
        renderHeight = Mathf.Clamp(renderHeight, 100, 400);
        sceneHeight = Mathf.Max(80, renderHeight - StatusBarHeight);
        horizonY = sceneHeight / 2;
        focalLength = (renderWidth * 0.5f) / Mathf.Tan(horizontalFov * 0.5f * Mathf.Deg2Rad);

        frame = new Color32[renderWidth * renderHeight];
        uploadFrame = new Color32[renderWidth * renderHeight];
        columnDepth = new float[renderWidth];
        BuildRendererLookups();
        screenTexture = new Texture2D(renderWidth, renderHeight, TextureFormat.RGBA32, false);
        screenTexture.filterMode = FilterMode.Point;
        screenTexture.wrapMode = TextureWrapMode.Clamp;
        CreateCameraOutputSurface();
    }

    private void CreateCameraOutputSurface() {
        Camera targetCamera = ResolveOutputCamera();
        if (targetCamera == null || targetCamera.targetTexture == null) {
            return;
        }

        arcadeOutputMode = true;

        if (outputCanvas != null) {
            Destroy(outputCanvas.gameObject);
        }

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer < 0) {
            uiLayer = gameObject.layer;
        }

        GameObject canvasObject = new GameObject("BackroomsDoom Arcade Output", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.layer = uiLayer;
        canvasObject.transform.SetParent(transform, false);
        outputCanvas = canvasObject.GetComponent<Canvas>();
        outputCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        outputCanvas.worldCamera = targetCamera;
        outputCanvas.planeDistance = 1.0f;
        outputCanvas.sortingOrder = 0;

        RenderTexture targetTexture = targetCamera.targetTexture;
        Vector2 referenceResolution = new Vector2(targetTexture.width, targetTexture.height);
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        StretchToParent(canvasRect);

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
        backgroundObject.layer = uiLayer;
        backgroundObject.transform.SetParent(canvasObject.transform, false);
        Image background = backgroundObject.GetComponent<Image>();
        background.color = Color.black;
        StretchToParent(backgroundObject.GetComponent<RectTransform>());

        GameObject imageObject = new GameObject("Frame", typeof(RectTransform), typeof(RawImage));
        imageObject.layer = uiLayer;
        imageObject.transform.SetParent(canvasObject.transform, false);
        outputImage = imageObject.GetComponent<RawImage>();
        outputImage.texture = screenTexture;
        outputImage.color = Color.white;

        Rect frameRect = GetAspectFitRect(referenceResolution.x, referenceResolution.y, renderWidth / (float)renderHeight);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = new Vector2(frameRect.center.x - referenceResolution.x * 0.5f, frameRect.center.y - referenceResolution.y * 0.5f);
        imageRect.sizeDelta = frameRect.size;

        targetCamera.cullingMask = 1 << uiLayer;
    }

    private static Rect GetAspectFitRect(float width, float height, float targetAspect) {
        float screenAspect = width / height;
        if (screenAspect > targetAspect) {
            float fittedWidth = height * targetAspect;
            return new Rect((width - fittedWidth) * 0.5f, 0.0f, fittedWidth, height);
        }

        float fittedHeight = width / targetAspect;
        return new Rect(0.0f, (height - fittedHeight) * 0.5f, width, fittedHeight);
    }

    private static void StretchToParent(RectTransform rectTransform) {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    private void BuildRendererLookups() {
        columnCameraX = new float[renderWidth];
        float halfWidth = renderWidth * 0.5f;
        for (int x = 0; x < renderWidth; x++) {
            columnCameraX[x] = (x + 0.5f - halfWidth) / focalLength;
        }

        floorCeilingRowDistance = new float[sceneHeight];
        floorCeilingRegularShadeBase = new float[sceneHeight];
        floorCeilingLightShadeBase = new float[sceneHeight];
        floorCeilingRowIsFloor = new bool[sceneHeight];
        frameRowOffsets = new int[renderHeight];
        dreadVignetteEdge = new float[renderWidth * sceneHeight];
        for (int y = 0; y < renderHeight; y++) {
            frameRowOffsets[y] = y * renderWidth;
        }
        float centerX = renderWidth * 0.5f;
        float centerY = sceneHeight * 0.5f;
        float maxDistance = Mathf.Sqrt(centerX * centerX + centerY * centerY);
        for (int y = 0; y < sceneHeight; y++) {
            bool floor = y >= horizonY;
            int distanceFromHorizon = floor ? y - horizonY + 1 : horizonY - y;
            float planeHeight = floor ? EyeHeight : WallHeight - EyeHeight;
            float rowDistance = planeHeight * focalLength / Mathf.Max(1.0f, distanceFromHorizon);
            floorCeilingRowIsFloor[y] = floor;
            floorCeilingRowDistance[y] = rowDistance;
            floorCeilingRegularShadeBase[y] = 1.0f / (1.0f + rowDistance * 0.68f * 0.095f);
            floorCeilingLightShadeBase[y] = 1.0f / (1.0f + rowDistance * 0.12f * 0.095f);

            float dy = y - centerY;
            int row = frameRowOffsets[y];
            for (int x = 0; x < renderWidth; x++) {
                float dx = x - centerX;
                float edge = Mathf.Sqrt(dx * dx + dy * dy) / maxDistance;
                dreadVignetteEdge[row + x] = Mathf.Clamp01((edge - 0.38f) / 0.62f);
            }
        }
    }

    private void UploadFrame() {
        for (int y = 0; y < renderHeight; y++) {
            Array.Copy(frame, y * renderWidth, uploadFrame, (renderHeight - 1 - y) * renderWidth, renderWidth);
        }
        screenTexture.SetPixels32(uploadFrame);
        screenTexture.Apply(false);
    }

    private void CreateSoftwareAssets() {
        wallTexture = CreateBrickTexture();
        doorTexture = CreateDoorTexture();
        floorTexture = CreateFloorTexture();
        ceilingTexture = CreateCeilingTexture();
        hazardTexture = CreateHazardTexture();
        enemyTexture = CreateEnemyTexture(false);
        enemyPainTexture = CreateEnemyTexture(true);
        corpseTexture = CreateCorpseTexture();
        healthTexture = CreateHealthPickupTexture();
        ammoTexture = CreateAmmoPickupTexture();
        puffTexture = CreatePuffTexture();
        furnitureTextures = new[] {
            CreateFurnitureTexture(0),
            CreateFurnitureTexture(1),
            CreateFurnitureTexture(2)
        };
        stopSignTexture = CreateStopSignTexture();
        weaponTexture = CreateWeaponTexture(false);
        weaponFlashTexture = CreateWeaponTexture(true);
        BuildTextureShadeTables();

        Sector main = new Sector {
            floorHeight = 0.0f,
            ceilingHeight = WallHeight,
            light = 0.78f,
            floorTexture = floorTexture,
            ceilingTexture = ceilingTexture
        };
        sectors.Add(main);
    }

    private void BuildTextureShadeTables() {
        wallTexture.BuildShadeTable();
        doorTexture.BuildShadeTable();
        floorTexture.BuildShadeTable();
        ceilingTexture.BuildShadeTable();
        hazardTexture.BuildShadeTable();
        enemyTexture.BuildShadeTable();
        enemyPainTexture.BuildShadeTable();
        corpseTexture.BuildShadeTable();
        healthTexture.BuildShadeTable();
        ammoTexture.BuildShadeTable();
        puffTexture.BuildShadeTable();
        stopSignTexture.BuildShadeTable();
        for (int i = 0; i < furnitureTextures.Length; i++) {
            furnitureTextures[i].BuildShadeTable();
        }
    }

    private void RestartRun() {
        health = playerMaxHealth;
        ammo = startingAmmo;
        dead = false;
        nextHazardTick = 0.0f;
        fireCooldown = 0.0f;
        weaponKick = 0.0f;
        muzzleFlashTimer = 0.0f;
        damageFlashTimer = 0.0f;
        messageTimer = 0.0f;
        messageText = "";
        nearestLivingEnemyDistance = 999.0f;
        worldLightMultiplier = 0.64f;
        dreadIntensity = 0.0f;
        nextMoanTime = Time.time + closeMoanInterval;

        ClearRunState();
        BuildMap();
        RenderFrame();
        UploadFrame();
    }

    private void ClearRunState() {
        lines.Clear();
        doors.Clear();
        things.Clear();
        visibleThings.Clear();
    }

    private void BuildMap() {
        mapHeight = MapRows.Length;
        mapWidth = MapRows[0].Length;
        map = new char[mapWidth, mapHeight];
        lightMap = new float[mapWidth, mapHeight];
        mapCells = new char[mapWidth * mapHeight];
        lightCells = new float[mapWidth * mapHeight];
        List<Vector2> lightSources = new List<Vector2>(8);

        for (int r = 0; r < mapHeight; r++) {
            if (MapRows[r].Length != mapWidth) {
                Debug.LogError("Map row " + r + " has wrong length " + MapRows[r].Length + " (expected " + mapWidth + ").");
                return;
            }

            for (int c = 0; c < mapWidth; c++) {
                char cell = MapRows[r][c];
                map[c, r] = cell;
                if (cell == 'P') {
                    playerX = c + 0.5f;
                    playerY = r + 0.5f;
                    playerAngle = 0.0f;
                    map[c, r] = '.';
                } else if (cell == 'E') {
                    SpawnThing(ThingKind.Enemy, c + 0.5f, r + 0.5f);
                    map[c, r] = '.';
                } else if (cell == 'H') {
                    SpawnThing(ThingKind.Health, c + 0.5f, r + 0.5f);
                    map[c, r] = '.';
                } else if (cell == 'A') {
                    SpawnThing(ThingKind.Ammo, c + 0.5f, r + 0.5f);
                    map[c, r] = '.';
                } else if (cell == 'L') {
                    lightSources.Add(new Vector2(c + 0.5f, r + 0.5f));
                    map[c, r] = '.';
                }
            }
        }

        ScatterStopSigns();
        ScatterFurniture();
        BuildLightMap(lightSources);
        RebuildFlatCellCaches();
        BuildWallLinedefs();
        BuildDoorLinedefs();
    }

    private void RebuildFlatCellCaches() {
        for (int r = 0; r < mapHeight; r++) {
            int row = r * mapWidth;
            for (int c = 0; c < mapWidth; c++) {
                int index = row + c;
                mapCells[index] = map[c, r];
                lightCells[index] = lightMap[c, r];
            }
        }
    }

    private void BuildLightMap(List<Vector2> lightSources) {
        for (int r = 0; r < mapHeight; r++) {
            for (int c = 0; c < mapWidth; c++) {
                float light = map[c, r] == '~' ? 0.86f : 0.58f;
                Vector2 cellCenter = new Vector2(c + 0.5f, r + 0.5f);
                for (int i = 0; i < lightSources.Count; i++) {
                    float distance = Vector2.Distance(cellCenter, lightSources[i]);
                    light = Mathf.Max(light, Mathf.Clamp01(1.12f - distance * 0.11f));
                }
                lightMap[c, r] = Mathf.Clamp(light, 0.34f, 1.16f);
            }
        }
    }

    private void BuildWallLinedefs() {
        for (int r = 0; r < mapHeight; r++) {
            for (int c = 0; c < mapWidth; c++) {
                if (map[c, r] != '#') {
                    continue;
                }

                if (IsOpenCell(c, r - 1)) {
                    AddWallLine(new Vector2(c, r), new Vector2(c + 1, r), CellLight(c, r - 1));
                }
                if (IsOpenCell(c + 1, r)) {
                    AddWallLine(new Vector2(c + 1, r), new Vector2(c + 1, r + 1), CellLight(c + 1, r));
                }
                if (IsOpenCell(c, r + 1)) {
                    AddWallLine(new Vector2(c + 1, r + 1), new Vector2(c, r + 1), CellLight(c, r + 1));
                }
                if (IsOpenCell(c - 1, r)) {
                    AddWallLine(new Vector2(c, r + 1), new Vector2(c, r), CellLight(c - 1, r));
                }
            }
        }
    }

    private void BuildDoorLinedefs() {
        for (int r = 0; r < mapHeight; r++) {
            for (int c = 0; c < mapWidth; c++) {
                if (map[c, r] != 'D') {
                    continue;
                }

                bool verticalDoor = IsWallCell(c, r - 1) && IsWallCell(c, r + 1) && IsOpenCell(c - 1, r) && IsOpenCell(c + 1, r);
                bool horizontalDoor = IsWallCell(c - 1, r) && IsWallCell(c + 1, r) && IsOpenCell(c, r - 1) && IsOpenCell(c, r + 1);
                if (!verticalDoor && !horizontalDoor) {
                    Debug.LogWarning("BackroomsDoom ignored invalid door marker at " + c + ", " + r + ".");
                    continue;
                }

                Vector2 a = verticalDoor ? new Vector2(c + 0.5f, r) : new Vector2(c, r + 0.5f);
                Vector2 b = verticalDoor ? new Vector2(c + 0.5f, r + 1) : new Vector2(c + 1, r + 0.5f);
                LineDef line = new LineDef {
                    a = a,
                    b = b,
                    kind = LineKind.Door,
                    texture = doorTexture,
                    frontSector = 0,
                    backSector = 0,
                    light = CellLight(c, r),
                    doorIndex = doors.Count
                };
                Door door = new Door {
                    line = line
                };
                lines.Add(line);
                doors.Add(door);
            }
        }
    }

    private void AddWallLine(Vector2 a, Vector2 b, float light) {
        lines.Add(new LineDef {
            a = a,
            b = b,
            kind = LineKind.Wall,
            texture = wallTexture,
            frontSector = 0,
            backSector = -1,
            light = light
        });
    }

    private void SpawnThing(ThingKind kind, float x, float y) {
        Thing thing = new Thing {
            kind = kind,
            state = ThingState.Alive,
            x = x,
            y = y,
            z = 0.0f
        };

        if (kind == ThingKind.Enemy) {
            thing.health = 36;
            thing.radius = 0.22f;
            thing.width = 0.42f;
            thing.height = 1.12f;
            thing.cooldown = UnityEngine.Random.Range(0.0f, 1.0f);
        } else if (kind == ThingKind.Health) {
            thing.radius = 0.2f;
            thing.width = 0.35f;
            thing.height = 0.35f;
        } else if (kind == ThingKind.Ammo) {
            thing.radius = 0.2f;
            thing.width = 0.35f;
            thing.height = 0.35f;
        }

        things.Add(thing);
    }

    private void ScatterFurniture() {
        for (int r = 1; r < mapHeight - 1; r++) {
            for (int c = 1; c < mapWidth - 1; c++) {
                if (map[c, r] != '.') {
                    continue;
                }

                int roll = Mathf.Abs(Hash(c, r, 147)) % 100;
                if (roll >= 7) {
                    continue;
                }

                bool nearWall = IsWallCell(c - 1, r) || IsWallCell(c + 1, r) || IsWallCell(c, r - 1) || IsWallCell(c, r + 1);
                if (!nearWall && roll > 1) {
                    continue;
                }

                float x = c + 0.5f;
                float y = r + 0.5f;
                if (Vector2.Distance(new Vector2(x, y), PlayerPosition()) < 2.7f || TooCloseToExistingThing(x, y, 1.15f)) {
                    continue;
                }

                int variant = Mathf.Abs(Hash(c, r, 173)) % furnitureTextures.Length;
                SpawnFurniture(x, y, variant);
            }
        }
    }

    private void ScatterStopSigns() {
        for (int r = 1; r < mapHeight - 1; r++) {
            for (int c = 1; c < mapWidth - 1; c++) {
                if (map[c, r] != '.') {
                    continue;
                }

                bool trueDeadEnd = OpenNeighborCount(c, r) == 1;
                if (!trueDeadEnd && !IsDeadEndPocket(c, r)) {
                    continue;
                }

                int roll = Mathf.Abs(Hash(c, r, 911)) % 100;
                if ((trueDeadEnd && roll >= 72) || (!trueDeadEnd && roll >= 28)) {
                    continue;
                }

                float x = c + 0.5f;
                float y = r + 0.5f;
                if (Vector2.Distance(new Vector2(x, y), PlayerPosition()) < 3.0f || TooCloseToExistingThing(x, y, 1.25f)) {
                    continue;
                }

                SpawnStopSign(x, y);
            }
        }
    }

    private int OpenNeighborCount(int c, int r) {
        int count = 0;
        if (IsOpenCell(c - 1, r)) {
            count++;
        }
        if (IsOpenCell(c + 1, r)) {
            count++;
        }
        if (IsOpenCell(c, r - 1)) {
            count++;
        }
        if (IsOpenCell(c, r + 1)) {
            count++;
        }
        return count;
    }

    private bool IsDeadEndPocket(int c, int r) {
        int wallCount = 0;
        if (IsWallCell(c - 1, r)) {
            wallCount++;
        }
        if (IsWallCell(c + 1, r)) {
            wallCount++;
        }
        if (IsWallCell(c, r - 1)) {
            wallCount++;
        }
        if (IsWallCell(c, r + 1)) {
            wallCount++;
        }
        if (wallCount < 2) {
            return false;
        }

        return (IsWallCell(c - 1, r) && IsOpenCell(c + 1, r)) ||
            (IsWallCell(c + 1, r) && IsOpenCell(c - 1, r)) ||
            (IsWallCell(c, r - 1) && IsOpenCell(c, r + 1)) ||
            (IsWallCell(c, r + 1) && IsOpenCell(c, r - 1));
    }

    private bool TooCloseToExistingThing(float x, float y, float minDistance) {
        Vector2 position = new Vector2(x, y);
        for (int i = 0; i < things.Count; i++) {
            if (Vector2.Distance(position, new Vector2(things[i].x, things[i].y)) < minDistance) {
                return true;
            }
        }
        return false;
    }

    private void SpawnFurniture(float x, float y, int variant) {
        Thing thing = new Thing {
            kind = ThingKind.Furniture,
            state = ThingState.Alive,
            x = x,
            y = y,
            z = 0.0f,
            radius = 0.0f,
            variant = variant
        };

        if (variant == 0) {
            thing.width = 0.46f;
            thing.height = 0.55f;
        } else if (variant == 1) {
            thing.width = 0.78f;
            thing.height = 0.48f;
        } else {
            thing.width = 0.42f;
            thing.height = 0.82f;
        }

        things.Add(thing);
    }

    private void SpawnStopSign(float x, float y) {
        things.Add(new Thing {
            kind = ThingKind.StopSign,
            state = ThingState.Alive,
            x = x,
            y = y,
            z = 0.0f,
            radius = 0.0f,
            width = 0.42f,
            height = 0.78f
        });
    }

    private void SpawnPuff(float x, float y) {
        things.Add(new Thing {
            kind = ThingKind.Puff,
            state = ThingState.Alive,
            x = x,
            y = y,
            z = 0.25f,
            radius = 0.0f,
            width = 0.28f,
            height = 0.28f,
            life = 0.16f
        });
    }

    private void UpdateCursorLock() {
        if (arcadeOutputMode) {
            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked) {
            if (Input.GetMouseButtonDown(0)) {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        } else if (Input.GetKeyDown(KeyCode.Escape)) {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void UpdatePlayer() {
        if (!InputActive) {
            return;
        }

        float deltaTime = Time.deltaTime;
        float turn = 0.0f;
        if (Input.GetKey(KeyCode.LeftArrow)) {
            turn -= 1.0f;
        }
        if (Input.GetKey(KeyCode.RightArrow)) {
            turn += 1.0f;
        }
        playerAngle += turn * turnSpeed * Mathf.Deg2Rad * deltaTime;
        playerAngle += Input.GetAxis("Mouse X") * mouseSensitivity * Mathf.Deg2Rad;
        playerAngle = NormalizeAngle(playerAngle);

        float forwardInput = 0.0f;
        float strafeInput = 0.0f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) {
            forwardInput += 1.0f;
        }
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) {
            forwardInput -= 1.0f;
        }
        if (Input.GetKey(KeyCode.D)) {
            strafeInput += 1.0f;
        }
        if (Input.GetKey(KeyCode.A)) {
            strafeInput -= 1.0f;
        }

        Vector2 forward = ForwardVector();
        Vector2 right = RightVector();
        Vector2 move = forward * forwardInput + right * strafeInput;
        if (move.sqrMagnitude > 1.0f) {
            move.Normalize();
        }

        float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;
        TryMove(move * speed * deltaTime);
    }

    private void UpdateInteraction() {
        if (!InputActive || !Input.GetKeyDown(KeyCode.E)) {
            return;
        }

        Door nearestDoor = null;
        float nearestDistance = interactRange;
        Vector2 origin = PlayerPosition();
        Vector2 direction = ForwardVector();
        for (int i = 0; i < doors.Count; i++) {
            float distance;
            float along;
            if (!RaySegmentIntersection(origin, direction, doors[i].line.a, doors[i].line.b, out distance, out along)) {
                continue;
            }
            if (distance < nearestDistance) {
                nearestDistance = distance;
                nearestDoor = doors[i];
            }
        }

        if (nearestDoor != null) {
            nearestDoor.targetOpen = !nearestDoor.targetOpen;
            PlaySound(BackroomsDoomSound.Door);
            messageText = nearestDoor.targetOpen ? "WALL OPENS" : "WALL SEALS";
            messageTimer = 0.8f;
        }
    }

    private void UpdateWeapon() {
        if (!InputActive || !IsFirePressed() || fireCooldown > 0.0f) {
            return;
        }

        fireCooldown = 0.28f;
        if (!TryConsumeAmmo()) {
            PlaySound(BackroomsDoomSound.DryFire);
            messageText = "EMPTY";
            messageTimer = 0.7f;
            return;
        }

        PlaySound(BackroomsDoomSound.Shot);
        weaponKick = 0.1f;
        muzzleFlashTimer = 0.055f;

        Vector2 origin = PlayerPosition();
        Vector2 direction = ForwardVector();
        float nearestWall = TraceBlockingLine(origin, direction, MaxTraceRange);
        Thing hitEnemy = null;
        float hitDistance = nearestWall;

        for (int i = 0; i < things.Count; i++) {
            Thing thing = things[i];
            if (thing.kind != ThingKind.Enemy || thing.state == ThingState.Dying) {
                continue;
            }

            Vector2 toThing = new Vector2(thing.x - playerX, thing.y - playerY);
            float forwardDistance = Vector2.Dot(toThing, direction);
            if (forwardDistance <= 0.25f || forwardDistance >= hitDistance) {
                continue;
            }

            float lateral = Mathf.Abs(Cross(direction, toThing));
            if (lateral > thing.radius) {
                continue;
            }

            float impactDistance = forwardDistance - Mathf.Sqrt(Mathf.Max(0.0f, thing.radius * thing.radius - lateral * lateral));
            if (impactDistance < hitDistance && HasLineOfSight(origin, new Vector2(thing.x, thing.y))) {
                hitDistance = impactDistance;
                hitEnemy = thing;
            }
        }

        if (hitEnemy != null) {
            hitEnemy.health -= 12;
            hitEnemy.state = ThingState.Pain;
            hitEnemy.life = 0.11f;
            Vector2 puffPoint = origin + direction * hitDistance;
            SpawnPuff(puffPoint.x, puffPoint.y);
            if (hitEnemy.health <= 0) {
                hitEnemy.state = ThingState.Dying;
                hitEnemy.life = 1.6f;
                hitEnemy.cooldown = 0.0f;
                PlaySound(BackroomsDoomSound.Kill);
                MaybeDropPickup(hitEnemy.x, hitEnemy.y);
            } else {
                PlaySound(BackroomsDoomSound.Hit);
            }
        } else if (nearestWall < MaxTraceRange) {
            Vector2 puffPoint = origin + direction * Mathf.Max(0.1f, nearestWall - 0.02f);
            SpawnPuff(puffPoint.x, puffPoint.y);
        }
    }

    private static bool IsFirePressed() {
        return Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftControl);
    }

    private void UpdateDoors() {
        float deltaTime = Time.deltaTime;
        for (int i = 0; i < doors.Count; i++) {
            Door door = doors[i];
            float target = door.targetOpen ? 1.0f : 0.0f;
            door.open = Mathf.MoveTowards(door.open, target, door.speed * deltaTime);
        }
    }

    private void UpdateThings() {
        float deltaTime = Time.deltaTime;
        Vector2 player = PlayerPosition();

        for (int i = things.Count - 1; i >= 0; i--) {
            Thing thing = things[i];

            if (thing.kind == ThingKind.Puff) {
                thing.life -= deltaTime;
                thing.width += deltaTime * 0.9f;
                thing.height += deltaTime * 0.9f;
                if (thing.life <= 0.0f) {
                    things.RemoveAt(i);
                }
                continue;
            }

            if (thing.kind != ThingKind.Enemy) {
                continue;
            }

            if (thing.state == ThingState.Pain) {
                thing.life -= deltaTime;
                if (thing.life <= 0.0f) {
                    thing.state = ThingState.Alive;
                }
            }

            if (thing.state == ThingState.Dying) {
                thing.life -= deltaTime;
                thing.height = Mathf.MoveTowards(thing.height, 0.28f, deltaTime * 0.72f);
                thing.width = Mathf.MoveTowards(thing.width, 0.68f, deltaTime * 0.38f);
                if (thing.life <= -0.8f) {
                    things.RemoveAt(i);
                }
                continue;
            }

            Vector2 position = new Vector2(thing.x, thing.y);
            Vector2 toPlayer = player - position;
            float distance = toPlayer.magnitude;
            if (distance > 0.001f) {
                thing.angle = Mathf.Atan2(toPlayer.y, toPlayer.x);
            }

            thing.cooldown = Mathf.Max(0.0f, thing.cooldown - deltaTime);
            if (distance < 0.78f && thing.cooldown <= 0.0f) {
                thing.cooldown = 0.9f;
                DamagePlayer(12);
                continue;
            }

            if (distance < 8.5f && HasLineOfSight(position, player)) {
                Vector2 direction = toPlayer.normalized;
                float chaseSpeed = 0.62f * deltaTime;
                TryMoveThing(thing, direction * chaseSpeed);
            }
        }
    }

    private void UpdatePickups() {
        for (int i = things.Count - 1; i >= 0; i--) {
            Thing thing = things[i];
            if (thing.kind != ThingKind.Health && thing.kind != ThingKind.Ammo) {
                continue;
            }

            float distance = Vector2.Distance(PlayerPosition(), new Vector2(thing.x, thing.y));
            if (distance > 0.42f) {
                continue;
            }

            if (thing.kind == ThingKind.Health) {
                if (health >= playerMaxHealth) {
                    continue;
                }
                health = Mathf.Min(playerMaxHealth, health + healthPickupAmount);
                messageText = "ALMOND WATER";
            } else {
                if (ammo >= maxAmmo) {
                    continue;
                }
                ammo = Mathf.Min(maxAmmo, ammo + ammoPickupAmount);
                messageText = "BATTERIES";
            }

            messageTimer = 0.9f;
            PlaySound(BackroomsDoomSound.Pickup);
            things.RemoveAt(i);
        }
    }

    private void UpdateHazards() {
        if (Time.time < nextHazardTick) {
            return;
        }

        int c = Mathf.FloorToInt(playerX);
        int r = Mathf.FloorToInt(playerY);
        if (InBounds(c, r) && map[c, r] == '~') {
            nextHazardTick = Time.time + hazardInterval;
            DamagePlayer(hazardDamage);
        }
    }

    private void UpdateHorrorMood() {
        nearestLivingEnemyDistance = 999.0f;
        int livingEnemies = 0;
        Vector2 player = PlayerPosition();

        for (int i = 0; i < things.Count; i++) {
            Thing thing = things[i];
            if (thing.kind != ThingKind.Enemy || thing.state == ThingState.Dying) {
                continue;
            }

            livingEnemies++;
            float distance = Vector2.Distance(player, new Vector2(thing.x, thing.y));
            if (distance < nearestLivingEnemyDistance) {
                nearestLivingEnemyDistance = distance;
            }
        }

        float targetDread = nearestLivingEnemyDistance < dreadRange
            ? 1.0f - nearestLivingEnemyDistance / dreadRange
            : 0.0f;
        float dreadSpeed = targetDread > dreadIntensity ? 1.9f : 0.8f;
        dreadIntensity = Mathf.MoveTowards(dreadIntensity, targetDread, Time.deltaTime * dreadSpeed);

        float flicker = 1.0f
            + Mathf.Sin(Time.time * 5.7f) * lightFlickerStrength * 0.35f
            + Mathf.Sin(Time.time * 13.3f) * lightFlickerStrength * 0.22f
            - dreadIntensity * 0.13f;
        worldLightMultiplier = Mathf.Clamp(worldDarkness * flicker, 0.36f, 1.0f);

        if (ambienceSource != null) {
            ambienceSource.volume = Mathf.Lerp(0.12f, 0.28f, dreadIntensity);
            ambienceSource.pitch = Mathf.Lerp(0.92f, 1.04f, dreadIntensity)
                + Mathf.Sin(Time.time * 1.7f) * 0.015f;
        }

        if (livingEnemies == 0 || Time.time < nextMoanTime) {
            return;
        }

        if (nearestLivingEnemyDistance < 10.0f) {
            PlaySound(BackroomsDoomSound.EntityMoan);
            nextMoanTime = Time.time + (nearestLivingEnemyDistance < dreadRange
                ? closeMoanInterval
                : UnityEngine.Random.Range(7.0f, 12.0f));
        }
    }

    private void TryMove(Vector2 delta) {
        float nextX = playerX + delta.x;
        if (!WouldCollide(nextX, playerY, PlayerRadius)) {
            playerX = nextX;
        }

        float nextY = playerY + delta.y;
        if (!WouldCollide(playerX, nextY, PlayerRadius)) {
            playerY = nextY;
        }
    }

    private void TryMoveThing(Thing thing, Vector2 delta) {
        float nextX = thing.x + delta.x;
        if (!WouldCollide(nextX, thing.y, thing.radius)) {
            thing.x = nextX;
        }

        float nextY = thing.y + delta.y;
        if (!WouldCollide(thing.x, nextY, thing.radius)) {
            thing.y = nextY;
        }
    }

    private bool WouldCollide(float x, float y, float radius) {
        if (IsWallAt(x - radius, y - radius) || IsWallAt(x + radius, y - radius) ||
            IsWallAt(x - radius, y + radius) || IsWallAt(x + radius, y + radius)) {
            return true;
        }

        Vector2 point = new Vector2(x, y);
        for (int i = 0; i < doors.Count; i++) {
            Door door = doors[i];
            if (door.open >= 0.86f) {
                continue;
            }
            if (DistancePointToSegment(point, door.line.a, door.line.b) < radius + 0.035f) {
                return true;
            }
        }

        return false;
    }

    private bool IsWallAt(float x, float y) {
        int c = Mathf.FloorToInt(x);
        int r = Mathf.FloorToInt(y);
        return IsWallCell(c, r);
    }

    private bool IsWallCell(int c, int r) {
        if (!InBounds(c, r)) {
            return true;
        }
        return map[c, r] == '#';
    }

    private bool IsOpenCell(int c, int r) {
        if (!InBounds(c, r)) {
            return false;
        }
        return map[c, r] != '#';
    }

    private bool InBounds(int c, int r) {
        return c >= 0 && r >= 0 && c < mapWidth && r < mapHeight;
    }

    private float CellLight(int c, int r) {
        if (!InBounds(c, r)) {
            return 0.5f;
        }
        return lightMap[c, r];
    }

    private void MaybeDropPickup(float x, float y) {
        float roll = Mathf.Repeat(Mathf.Sin((x * 12.9898f + y * 78.233f + Time.time) * 43758.5453f), 1.0f);
        if (roll < 0.22f) {
            SpawnThing(ThingKind.Health, x, y);
        } else if (roll < 0.44f) {
            SpawnThing(ThingKind.Ammo, x, y);
        }
    }

    private bool HasLineOfSight(Vector2 from, Vector2 to) {
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance <= 0.001f) {
            return true;
        }
        float blocker = TraceBlockingLine(from, delta / distance, distance);
        return blocker >= distance - 0.05f;
    }

    private float TraceBlockingLine(Vector2 origin, Vector2 direction, float maxDistance) {
        float nearest = maxDistance;
        for (int i = 0; i < lines.Count; i++) {
            LineDef line = lines[i];
            if (line.kind == LineKind.Door && doors[line.doorIndex].open >= 0.86f) {
                continue;
            }

            float distance;
            float along;
            if (RaySegmentIntersection(origin, direction, line.a, line.b, out distance, out along)) {
                if (distance < nearest) {
                    nearest = distance;
                }
            }
        }
        return nearest;
    }

    private void RenderFrame() {
        if (frame == null) {
            return;
        }

        DrawFloorAndCeiling();
        for (int x = 0; x < renderWidth; x++) {
            columnDepth[x] = 9999.0f;
        }

        for (int i = 0; i < lines.Count; i++) {
            DrawLine(lines[i]);
        }

        DrawThings();
        DrawWeapon();

        if (damageFlashTimer > 0.0f) {
            OverlayColor(C32(190, 0, 0), Mathf.Clamp01(damageFlashTimer * 1.25f) * 0.45f, 0, sceneHeight);
        }

        if (showHud) {
            DrawStatusBar();
        }

        if (!InputActive && !dead) {
            DrawPanelText("BACKROOMS DOOM", renderWidth / 2, sceneHeight / 2 - 18, 2, C32(246, 218, 94));
            DrawPanelText("CLICK TO ENTER", renderWidth / 2, sceneHeight / 2 + 2, 1, C32(230, 220, 166));
            DrawPanelText("WASD MOVE  E USE  MOUSE FIRE", renderWidth / 2, sceneHeight / 2 + 14, 1, C32(154, 140, 92));
        }

        if (dead) {
            OverlayColor(C32(70, 52, 8), 0.62f, 0, sceneHeight);
            DrawPanelText("YOU NOCLIPPED", renderWidth / 2, sceneHeight / 2 - 12, 2, C32(246, 218, 94));
            DrawPanelText("PRESS R", renderWidth / 2, sceneHeight / 2 + 18, 1, C32(230, 220, 166));
        } else if (messageTimer > 0.0f && messageText.Length > 0) {
            DrawPanelText(messageText, renderWidth / 2, sceneHeight - 44, 1, C32(246, 218, 94));
        }

        ApplyDreadFrameOverlay();
    }

    private void ApplyDreadFrameOverlay() {
        if (dreadVignetteEdge == null) {
            return;
        }

        Color32 tint = C32(34, 29, 8);
        float baseDarkness = Mathf.Clamp01(0.07f + (1.0f - worldLightMultiplier) * 0.24f);
        float pulse = (Mathf.Sin(Time.time * 8.0f) + Mathf.Sin(Time.time * 17.0f)) * 0.5f;
        float dread = Mathf.Clamp01(dreadIntensity);
        float dreadAlpha = Mathf.Clamp01(dread * 0.14f + pulse * dread * 0.02f);
        float scanlineAlpha = Mathf.Clamp01(0.025f + dread * 0.02f);

        for (int y = 0; y < sceneHeight; y++) {
            int row = frameRowOffsets[y];
            bool scanline = (y & 3) == 0;
            for (int x = 0; x < renderWidth; x++) {
                int index = row + x;
                Color32 color = frame[index];
                color = Blend(color, tint, baseDarkness * dreadVignetteEdge[index]);
                if (dreadAlpha > 0.001f) {
                    color = Blend(color, tint, dreadAlpha);
                }
                if (scanline) {
                    color = Blend(color, tint, scanlineAlpha);
                }
                frame[index] = color;
            }
        }
    }

    private void DrawFloorAndCeiling() {
        Vector2 forward = ForwardVector();
        Vector2 right = RightVector();
        float localWorldLightMultiplier = worldLightMultiplier;
        float playerLocalX = playerX;
        float playerLocalY = playerY;
        float inverseFocalLength = 1.0f / focalLength;
        int localRenderWidth = renderWidth;
        int localMapWidth = mapWidth;
        int localMapHeight = mapHeight;
        Color32[] localFrame = frame;
        char[] localMapCells = mapCells;
        float[] localLightCells = lightCells;

        for (int y = 0; y < sceneHeight;) {
            bool floor = floorCeilingRowIsFloor[y];
            int rowSpan = PlanePixelStep;
            if (y + rowSpan > sceneHeight) {
                rowSpan = sceneHeight - y;
            }
            if (rowSpan > 1 && floorCeilingRowIsFloor[y + 1] != floor) {
                rowSpan = 1;
            }

            float rowDistance = floorCeilingRowDistance[y];
            float lateral = rowDistance * columnCameraX[0];
            float worldX = playerLocalX + forward.x * rowDistance + right.x * lateral;
            float worldY = playerLocalY + forward.y * rowDistance + right.y * lateral;
            float worldStepX = right.x * rowDistance * inverseFocalLength;
            float worldStepY = right.y * rowDistance * inverseFocalLength;
            float blockStepX = worldStepX * PlanePixelStep;
            float blockStepY = worldStepY * PlanePixelStep;
            float regularShadeBase = floorCeilingRegularShadeBase[y] * localWorldLightMultiplier;
            float lightShadeBase = floorCeilingLightShadeBase[y] * localWorldLightMultiplier;
            int frameRow = frameRowOffsets[y];
            int nextFrameRow = rowSpan > 1 ? frameRowOffsets[y + 1] : 0;
            float textureU = worldX * 0.5f;
            float textureV = worldY * 0.5f;
            float textureStepU = blockStepX * 0.5f;
            float textureStepV = blockStepY * 0.5f;

            if (floor) {
                for (int x = 0; x < localRenderWidth; x += PlanePixelStep) {
                    int c = FastFloorToInt(worldX);
                    int r = FastFloorToInt(worldY);
                    bool inBounds = c >= 0 && r >= 0 && c < localMapWidth && r < localMapHeight;
                    int cellIndex = inBounds ? r * localMapWidth + c : 0;
                    TextureData texture = inBounds && localMapCells[cellIndex] == '~' ? hazardTexture : floorTexture;
                    float light = inBounds ? localLightCells[cellIndex] : 0.22f;
                    int textureIndex = texture.Sample32Index(textureU, textureV);
                    Color32 shaded = texture.Shaded32At(textureIndex, QuantizeShadeLevel(light * regularShadeBase));
                    localFrame[frameRow + x] = shaded;
                    if (x + 1 < localRenderWidth) {
                        localFrame[frameRow + x + 1] = shaded;
                    }
                    if (rowSpan > 1) {
                        localFrame[nextFrameRow + x] = shaded;
                        if (x + 1 < localRenderWidth) {
                            localFrame[nextFrameRow + x + 1] = shaded;
                        }
                    }
                    worldX += blockStepX;
                    worldY += blockStepY;
                    textureU += textureStepU;
                    textureV += textureStepV;
                }
            } else {
                TextureData texture = ceilingTexture;
                for (int x = 0; x < localRenderWidth; x += PlanePixelStep) {
                    int c = FastFloorToInt(worldX);
                    int r = FastFloorToInt(worldY);
                    bool inBounds = c >= 0 && r >= 0 && c < localMapWidth && r < localMapHeight;
                    int cellIndex = inBounds ? r * localMapWidth + c : 0;
                    float light = inBounds ? localLightCells[cellIndex] : 0.22f;
                    int textureIndex = texture.Sample32Index(textureU, textureV);
                    Color32 color = texture.Pixels[textureIndex];
                    bool ceilingLight = color.r >= 218 && color.g >= 210;
                    float shadeFactor = ceilingLight ? (light > 1.75f ? light : 1.75f) * lightShadeBase : light * regularShadeBase;
                    Color32 shaded = texture.Shaded32At(textureIndex, QuantizeShadeLevel(shadeFactor));
                    localFrame[frameRow + x] = shaded;
                    if (x + 1 < localRenderWidth) {
                        localFrame[frameRow + x + 1] = shaded;
                    }
                    if (rowSpan > 1) {
                        localFrame[nextFrameRow + x] = shaded;
                        if (x + 1 < localRenderWidth) {
                            localFrame[nextFrameRow + x + 1] = shaded;
                        }
                    }
                    worldX += blockStepX;
                    worldY += blockStepY;
                    textureU += textureStepU;
                    textureV += textureStepV;
                }
            }

            y += rowSpan;
        }
    }

    private void DrawLine(LineDef line) {
        if (line.kind == LineKind.Door && doors[line.doorIndex].open >= 0.985f) {
            return;
        }

        Vector2 forward = ForwardVector();
        Vector2 right = RightVector();
        Vector2 player = new Vector2(playerX, playerY);
        Vector2 relA = line.a - player;
        Vector2 relB = line.b - player;
        float ax = Vector2.Dot(relA, right);
        float az = Vector2.Dot(relA, forward);
        float bx = Vector2.Dot(relB, right);
        float bz = Vector2.Dot(relB, forward);
        float au = 0.0f;
        float bu = Vector2.Distance(line.a, line.b);

        if (az <= NearClip && bz <= NearClip) {
            return;
        }

        if (az <= NearClip) {
            float t = (NearClip - az) / (bz - az);
            ax = Mathf.Lerp(ax, bx, t);
            az = NearClip;
            au = Mathf.Lerp(au, bu, t);
        } else if (bz <= NearClip) {
            float t = (NearClip - bz) / (az - bz);
            bx = Mathf.Lerp(bx, ax, t);
            bz = NearClip;
            bu = Mathf.Lerp(bu, au, t);
        }

        float sxA = renderWidth * 0.5f + ax * focalLength / az;
        float sxB = renderWidth * 0.5f + bx * focalLength / bz;
        if (Mathf.Abs(sxA - sxB) < 0.0001f) {
            return;
        }

        if (sxA > sxB) {
            Swap(ref sxA, ref sxB);
            Swap(ref az, ref bz);
            Swap(ref au, ref bu);
        }

        int xStart = Mathf.Max(0, Mathf.CeilToInt(sxA));
        int xEnd = Mathf.Min(renderWidth - 1, Mathf.FloorToInt(sxB));
        if (xEnd < xStart) {
            return;
        }

        float invZA = 1.0f / az;
        float invZB = 1.0f / bz;
        float uOverZA = au * invZA;
        float uOverZB = bu * invZB;
        float topHeight = WallHeight;
        if (line.kind == LineKind.Door) {
            topHeight = Mathf.Max(0.02f, WallHeight * (1.0f - doors[line.doorIndex].open));
        }

        for (int x = xStart; x <= xEnd; x++) {
            float t = (x + 0.5f - sxA) / (sxB - sxA);
            float invZ = Mathf.Lerp(invZA, invZB, t);
            if (invZ <= 0.0f) {
                continue;
            }

            float depth = 1.0f / invZ;
            if (depth >= columnDepth[x]) {
                continue;
            }

            float u = Mathf.Lerp(uOverZA, uOverZB, t) * depth;
            float yTop = horizonY - (topHeight - EyeHeight) * focalLength / depth;
            float yBottom = horizonY + EyeHeight * focalLength / depth;
            int yStart = Mathf.Max(0, Mathf.CeilToInt(yTop));
            int yEnd = Mathf.Min(sceneHeight - 1, Mathf.FloorToInt(yBottom));
            if (yEnd < yStart) {
                continue;
            }

            float shadeFactor = (line.light * worldLightMultiplier) / (1.0f + depth * 0.095f);
            int shadeLevel = QuantizeShadeLevel(shadeFactor);
            float invWallSpan = 1.0f / Mathf.Max(1.0f, yBottom - yTop);
            float v = (yStart + 0.5f - yTop) * invWallSpan;
            int frameIndex = frameRowOffsets[yStart] + x;
            for (int y = yStart; y <= yEnd; y++) {
                frame[frameIndex] = line.texture.Sample32Shaded(u, v, shadeLevel);
                v += invWallSpan;
                frameIndex += renderWidth;
            }
            columnDepth[x] = depth;
        }
    }

    private void DrawThings() {
        visibleThings.Clear();
        Vector2 forward = ForwardVector();
        Vector2 right = RightVector();
        Vector2 player = PlayerPosition();

        for (int i = 0; i < things.Count; i++) {
            Thing thing = things[i];
            Vector2 rel = new Vector2(thing.x, thing.y) - player;
            float depth = Vector2.Dot(rel, forward);
            if (depth <= NearClip) {
                continue;
            }

            float side = Vector2.Dot(rel, right);
            float projectedX = renderWidth * 0.5f + side * focalLength / depth;
            float halfWidth = thing.width * focalLength / depth * 0.5f;
            if (projectedX + halfWidth < 0.0f || projectedX - halfWidth >= renderWidth) {
                continue;
            }

            thing.viewDepth = depth;
            visibleThings.Add(thing);
        }

        visibleThings.Sort((a, b) => b.viewDepth.CompareTo(a.viewDepth));
        for (int i = 0; i < visibleThings.Count; i++) {
            DrawThing(visibleThings[i]);
        }
    }

    private void DrawThing(Thing thing) {
        TextureData texture = TextureForThing(thing);
        Vector2 forward = ForwardVector();
        Vector2 right = RightVector();
        Vector2 rel = new Vector2(thing.x - playerX, thing.y - playerY);
        float depth = Vector2.Dot(rel, forward);
        if (depth <= NearClip) {
            return;
        }

        float side = Vector2.Dot(rel, right);
        float screenCenterX = renderWidth * 0.5f + side * focalLength / depth;
        float bottomY = horizonY + (EyeHeight - thing.z) * focalLength / depth;
        float topY = horizonY - (thing.z + thing.height - EyeHeight) * focalLength / depth;
        float spriteHeight = Mathf.Max(1.0f, bottomY - topY);
        float spriteWidth = Mathf.Max(1.0f, thing.width * focalLength / depth);
        int xStart = Mathf.Max(0, Mathf.CeilToInt(screenCenterX - spriteWidth * 0.5f));
        int xEnd = Mathf.Min(renderWidth - 1, Mathf.FloorToInt(screenCenterX + spriteWidth * 0.5f));
        int yStart = Mathf.Max(0, Mathf.CeilToInt(topY));
        int yEnd = Mathf.Min(sceneHeight - 1, Mathf.FloorToInt(bottomY));
        if (xEnd < xStart || yEnd < yStart) {
            return;
        }

        float leftX = screenCenterX - spriteWidth * 0.5f;
        float invSpriteWidth = 1.0f / spriteWidth;
        float invSpriteHeight = 1.0f / spriteHeight;
        int shadeLevel = QuantizeShadeLevel((CellLight(FastFloorToInt(thing.x), FastFloorToInt(thing.y)) * worldLightMultiplier) / (1.0f + depth * 0.095f));

        for (int x = xStart; x <= xEnd; x++) {
            if (depth >= columnDepth[x]) {
                continue;
            }
            float u = (x + 0.5f - leftX) * invSpriteWidth;
            float v = (yStart + 0.5f - topY) * invSpriteHeight;
            int frameIndex = frameRowOffsets[yStart] + x;
            for (int y = yStart; y <= yEnd; y++) {
                Color32 color = texture.SampleShaded(u, v, shadeLevel);
                if (color.a < 8) {
                    v += invSpriteHeight;
                    frameIndex += renderWidth;
                    continue;
                }
                frame[frameIndex] = color;
                v += invSpriteHeight;
                frameIndex += renderWidth;
            }
        }
    }

    private TextureData TextureForThing(Thing thing) {
        if (thing.kind == ThingKind.Health) {
            return healthTexture;
        }
        if (thing.kind == ThingKind.Ammo) {
            return ammoTexture;
        }
        if (thing.kind == ThingKind.Puff) {
            return puffTexture;
        }
        if (thing.kind == ThingKind.Furniture) {
            int variant = Mathf.Clamp(thing.variant, 0, furnitureTextures.Length - 1);
            return furnitureTextures[variant];
        }
        if (thing.kind == ThingKind.StopSign) {
            return stopSignTexture;
        }
        if (thing.state == ThingState.Dying) {
            return thing.life > 1.0f ? enemyPainTexture : corpseTexture;
        }
        if (thing.state == ThingState.Pain) {
            return enemyPainTexture;
        }
        return enemyTexture;
    }

    private void DrawWeapon() {
        TextureData texture = muzzleFlashTimer > 0.0f ? weaponFlashTexture : weaponTexture;
        int width = 120;
        int height = 92;
        int x = (renderWidth - width) / 2;
        int y = sceneHeight - height + 16 + Mathf.RoundToInt(weaponKick * 72.0f);
        DrawImage(texture, x, y, width, height, true);
    }

    private void DrawStatusBar() {
        int top = renderHeight - StatusBarHeight;
        FillRect(0, top, renderWidth, StatusBarHeight, C32(31, 28, 20));
        FillRect(0, top, renderWidth, 2, C32(119, 104, 58));
        FillRect(0, top + 2, renderWidth, 1, C32(13, 12, 10));

        DrawFace(top);
        DrawText("CELLS", 10, top + 5, 1, C32(189, 174, 121));
        DrawText(ammo.ToString(), 10, top + 15, 2, ammo == 0 ? C32(196, 42, 25) : C32(238, 222, 150));
        DrawText("VITALS", renderWidth - 102, top + 5, 1, C32(189, 174, 121));
        DrawText(health + "%", renderWidth - 102, top + 15, 2, health <= 25 ? C32(196, 42, 25) : C32(238, 222, 150));
    }

    private void DrawFace(int statusTop) {
        int cx = renderWidth / 2;
        int y = statusTop + 4;
        Color32 frameDark = C32(15, 13, 10);
        Color32 frameMid = C32(86, 77, 48);
        Color32 frameLight = C32(153, 137, 77);
        Color32 backing = dead ? C32(45, 28, 12) : C32(39, 35, 23);
        Color32 armor = dead ? C32(55, 45, 31) : C32(95, 84, 52);
        Color32 skin = dead ? C32(93, 73, 53) : health <= 25 ? C32(181, 106, 75) : C32(189, 140, 89);
        Color32 skinLight = dead ? C32(122, 96, 68) : health <= 25 ? C32(214, 134, 92) : C32(222, 175, 105);
        Color32 shade = dead ? C32(55, 39, 23) : C32(106, 71, 39);

        FillRect(cx - 27, y, 54, 26, frameDark);
        FillRect(cx - 25, y + 1, 50, 23, frameMid);
        FillRect(cx - 24, y + 2, 48, 21, backing);
        FillRect(cx - 25, y + 1, 50, 1, frameLight);
        FillRect(cx - 25, y + 23, 50, 2, C32(11, 11, 12));

        FillRect(cx - 18, y + 19, 36, 4, armor);
        FillRect(cx - 12, y + 16, 24, 5, C32(54, 48, 44));
        FillRect(cx - 14, y + 5, 28, 5, shade);
        FillRect(cx - 12, y + 8, 24, 12, skin);
        FillRect(cx - 9, y + 7, 18, 3, skinLight);
        FillRect(cx - 13, y + 10, 2, 7, shade);
        FillRect(cx + 11, y + 10, 2, 7, shade);

        if (health <= 25 && !dead) {
            FillRect(cx - 13, y + 12, 3, 2, C32(130, 0, 0));
            FillRect(cx + 10, y + 15, 3, 2, C32(130, 0, 0));
        }

        FillRect(cx - 8, y + 12, 4, 2, C32(15, 15, 16));
        FillRect(cx + 4, y + 12, 4, 2, C32(15, 15, 16));
        if (dead) {
            FillRect(cx - 9, y + 18, 18, 2, C32(34, 0, 0));
        } else if (health <= 25) {
            FillRect(cx - 7, y + 18, 14, 2, C32(72, 14, 10));
            FillRect(cx - 5, y + 19, 10, 1, C32(18, 8, 7));
        } else {
            FillRect(cx - 7, y + 17, 14, 2, C32(45, 18, 12));
        }
    }

    private void DrawImage(TextureData texture, int x, int y, int width, int height, bool alpha) {
        for (int py = 0; py < height; py++) {
            int destY = y + py;
            if (destY < 0 || destY >= renderHeight) {
                continue;
            }

            float v = (py + 0.5f) / height;
            for (int px = 0; px < width; px++) {
                int destX = x + px;
                if (destX < 0 || destX >= renderWidth) {
                    continue;
                }

                Color32 color = texture.Sample((px + 0.5f) / width, v);
                if (alpha && color.a < 8) {
                    continue;
                }
                frame[destY * renderWidth + destX] = color;
            }
        }
    }

    private void FillRect(int x, int y, int width, int height, Color32 color) {
        int x0 = Mathf.Max(0, x);
        int y0 = Mathf.Max(0, y);
        int x1 = Mathf.Min(renderWidth, x + width);
        int y1 = Mathf.Min(renderHeight, y + height);
        for (int py = y0; py < y1; py++) {
            int row = py * renderWidth;
            for (int px = x0; px < x1; px++) {
                frame[row + px] = color;
            }
        }
    }

    private void OverlayColor(Color32 color, float alpha, int yStart, int yEnd) {
        byte inv = (byte)Mathf.RoundToInt((1.0f - alpha) * 255.0f);
        byte src = (byte)Mathf.RoundToInt(alpha * 255.0f);
        yStart = Mathf.Clamp(yStart, 0, renderHeight);
        yEnd = Mathf.Clamp(yEnd, 0, renderHeight);
        for (int y = yStart; y < yEnd; y++) {
            int row = y * renderWidth;
            for (int x = 0; x < renderWidth; x++) {
                Color32 dst = frame[row + x];
                frame[row + x] = new Color32(
                    (byte)((dst.r * inv + color.r * src) / 255),
                    (byte)((dst.g * inv + color.g * src) / 255),
                    (byte)((dst.b * inv + color.b * src) / 255),
                    255
                );
            }
        }
    }

    private static Color32 Blend(Color32 dst, Color32 src, float alpha) {
        alpha = Mathf.Clamp01(alpha);
        byte inv = (byte)Mathf.RoundToInt((1.0f - alpha) * 255.0f);
        byte srcAlpha = (byte)Mathf.RoundToInt(alpha * 255.0f);
        return new Color32(
            (byte)((dst.r * inv + src.r * srcAlpha) / 255),
            (byte)((dst.g * inv + src.g * srcAlpha) / 255),
            (byte)((dst.b * inv + src.b * srcAlpha) / 255),
            255
        );
    }

    private void DrawPanelText(string text, int centerX, int y, int scale, Color32 color) {
        int width = MeasureText(text, scale);
        DrawText(text, centerX - width / 2, y, scale, C32(0, 0, 0));
        DrawText(text, centerX - width / 2 - scale, y - scale, scale, color);
    }

    private int MeasureText(string text, int scale) {
        return text.Length * 6 * scale;
    }

    private void DrawText(string text, int x, int y, int scale, Color32 color) {
        int cursor = x;
        for (int i = 0; i < text.Length; i++) {
            DrawGlyph(char.ToUpperInvariant(text[i]), cursor, y, scale, color);
            cursor += 6 * scale;
        }
    }

    private void DrawGlyph(char ch, int x, int y, int scale, Color32 color) {
        string[] glyph = Glyph(ch);
        for (int gy = 0; gy < glyph.Length; gy++) {
            string row = glyph[gy];
            for (int gx = 0; gx < row.Length; gx++) {
                if (row[gx] == ' ') {
                    continue;
                }
                FillRect(x + gx * scale, y + gy * scale, scale, scale, color);
            }
        }
    }

    private static string[] Glyph(char ch) {
        switch (ch) {
            case 'A': return new[] { " ### ", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" };
            case 'B': return new[] { "#### ", "#   #", "#   #", "#### ", "#   #", "#   #", "#### " };
            case 'C': return new[] { " ### ", "#   #", "#    ", "#    ", "#    ", "#   #", " ### " };
            case 'D': return new[] { "#### ", "#   #", "#   #", "#   #", "#   #", "#   #", "#### " };
            case 'E': return new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#####" };
            case 'F': return new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#    " };
            case 'G': return new[] { " ### ", "#   #", "#    ", "# ###", "#   #", "#   #", " ### " };
            case 'H': return new[] { "#   #", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" };
            case 'I': return new[] { "#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "#####" };
            case 'J': return new[] { "#####", "   # ", "   # ", "   # ", "   # ", "#  # ", " ##  " };
            case 'K': return new[] { "#   #", "#  # ", "# #  ", "##   ", "# #  ", "#  # ", "#   #" };
            case 'L': return new[] { "#    ", "#    ", "#    ", "#    ", "#    ", "#    ", "#####" };
            case 'M': return new[] { "#   #", "## ##", "# # #", "#   #", "#   #", "#   #", "#   #" };
            case 'N': return new[] { "#   #", "##  #", "# # #", "#  ##", "#   #", "#   #", "#   #" };
            case 'O': return new[] { " ### ", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " };
            case 'P': return new[] { "#### ", "#   #", "#   #", "#### ", "#    ", "#    ", "#    " };
            case 'Q': return new[] { " ### ", "#   #", "#   #", "#   #", "# # #", "#  # ", " ## #" };
            case 'R': return new[] { "#### ", "#   #", "#   #", "#### ", "# #  ", "#  # ", "#   #" };
            case 'S': return new[] { " ####", "#    ", "#    ", " ### ", "    #", "    #", "#### " };
            case 'T': return new[] { "#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  " };
            case 'U': return new[] { "#   #", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " };
            case 'V': return new[] { "#   #", "#   #", "#   #", "#   #", "#   #", " # # ", "  #  " };
            case 'W': return new[] { "#   #", "#   #", "#   #", "# # #", "# # #", "## ##", "#   #" };
            case 'X': return new[] { "#   #", "#   #", " # # ", "  #  ", " # # ", "#   #", "#   #" };
            case 'Y': return new[] { "#   #", "#   #", " # # ", "  #  ", "  #  ", "  #  ", "  #  " };
            case 'Z': return new[] { "#####", "    #", "   # ", "  #  ", " #   ", "#    ", "#####" };
            case '0': return new[] { " ### ", "#   #", "#  ##", "# # #", "##  #", "#   #", " ### " };
            case '1': return new[] { "  #  ", " ##  ", "# #  ", "  #  ", "  #  ", "  #  ", "#####" };
            case '2': return new[] { " ### ", "#   #", "    #", "   # ", "  #  ", " #   ", "#####" };
            case '3': return new[] { "#### ", "    #", "    #", " ### ", "    #", "    #", "#### " };
            case '4': return new[] { "#   #", "#   #", "#   #", "#####", "    #", "    #", "    #" };
            case '5': return new[] { "#####", "#    ", "#    ", "#### ", "    #", "    #", "#### " };
            case '6': return new[] { " ### ", "#    ", "#    ", "#### ", "#   #", "#   #", " ### " };
            case '7': return new[] { "#####", "    #", "   # ", "  #  ", " #   ", " #   ", " #   " };
            case '8': return new[] { " ### ", "#   #", "#   #", " ### ", "#   #", "#   #", " ### " };
            case '9': return new[] { " ### ", "#   #", "#   #", " ####", "    #", "    #", " ### " };
            case '%': return new[] { "#   #", "   # ", "  #  ", " #   ", "#    ", "#   #", "     " };
            case '-': return new[] { "     ", "     ", "     ", " ### ", "     ", "     ", "     " };
            case '.': return new[] { "     ", "     ", "     ", "     ", "     ", " ##  ", " ##  " };
            default: return new[] { "     ", "     ", "     ", "     ", "     ", "     ", "     " };
        }
    }

    private static TextureData CreateBrickTexture() {
        TextureData texture = new TextureData(32, 32);
        for (int y = 0; y < texture.Height; y++) {
            for (int x = 0; x < texture.Width; x++) {
                int noise = Mathf.Abs(Hash(x, y, 2)) % 34;
                bool wallpaperSeam = x == 0 || x == 31;
                bool baseboard = y >= 28;
                bool stained = Mathf.Abs(Hash(x / 4, y / 4, 45)) % 59 == 0;
                bool weave = ((x * 5 + y * 7) % 17) < 2;
                if (baseboard) {
                    texture.SetPixel(x, y, C32(164 + noise / 4, 146 + noise / 5, 74 + noise / 6));
                } else if (wallpaperSeam) {
                    texture.SetPixel(x, y, C32(149, 132, 63));
                } else if (weave) {
                    texture.SetPixel(x, y, C32(202 + noise / 5, 184 + noise / 6, 92 + noise / 8));
                } else if (stained) {
                    texture.SetPixel(x, y, C32(169 + noise / 6, 151 + noise / 7, 76 + noise / 9));
                } else {
                    texture.SetPixel(x, y, C32(184 + noise / 2, 166 + noise / 3, 82 + noise / 5));
                }
            }
        }
        return texture;
    }

    private static TextureData CreateDoorTexture() {
        TextureData texture = new TextureData(32, 32);
        for (int y = 0; y < texture.Height; y++) {
            for (int x = 0; x < texture.Width; x++) {
                int noise = Mathf.Abs(Hash(x, y, 5)) % 18;
                Color32 color = y % 7 == 0 ? C32(102, 95, 56) : C32(148 + noise, 132 + noise / 2, 64);
                if (x % 11 == 0) {
                    color = C32(76, 72, 45);
                }
                if (y >= 12 && y <= 17) {
                    color = ((x + y) & 7) < 4 ? C32(235, 226, 142) : C32(38, 35, 24);
                }
                texture.SetPixel(x, y, color);
            }
        }
        return texture;
    }

    private static TextureData CreateFloorTexture() {
        TextureData texture = new TextureData(32, 32);
        for (int y = 0; y < texture.Height; y++) {
            for (int x = 0; x < texture.Width; x++) {
                int noise = Mathf.Abs(Hash(x, y, 8)) % 24;
                bool fiber = ((x * 3 + y * 5 + noise) % 13) < 4;
                bool stain = Mathf.PerlinNoise(x * 0.13f + 11.0f, y * 0.13f + 3.0f) > 0.84f;
                if (stain) {
                    texture.SetPixel(x, y, C32(143 + noise / 5, 123 + noise / 6, 70 + noise / 8));
                } else if (fiber) {
                    texture.SetPixel(x, y, C32(167 + noise / 2, 144 + noise / 3, 80));
                } else {
                    texture.SetPixel(x, y, C32(184 + noise, 158 + noise / 2, 88 + noise / 4));
                }
            }
        }
        return texture;
    }

    private static TextureData CreateCeilingTexture() {
        TextureData texture = new TextureData(32, 32);
        for (int y = 0; y < texture.Height; y++) {
            for (int x = 0; x < texture.Width; x++) {
                bool grid = x % 8 == 0 || y % 8 == 0;
                bool mainLight = x >= 10 && x <= 23 && y >= 10 && y <= 18;
                bool sideLight = x >= 2 && x <= 8 && y >= 20 && y <= 28;
                bool glow = (x >= 8 && x <= 25 && y >= 8 && y <= 20) || (x >= 1 && x <= 10 && y >= 18 && y <= 30);
                if (grid) {
                    texture.SetPixel(x, y, C32(86, 78, 47));
                } else if (mainLight || sideLight) {
                    int buzz = Mathf.Abs(Hash(x, y, 31)) % 10;
                    texture.SetPixel(x, y, C32(245 + buzz, 241 + buzz, 196 + buzz / 2));
                } else if (glow) {
                    int noise = Mathf.Abs(Hash(x, y, 23)) % 12;
                    texture.SetPixel(x, y, C32(178 + noise, 166 + noise, 103 + noise / 2));
                } else {
                    int noise = Mathf.Abs(Hash(x, y, 12)) % 15;
                    texture.SetPixel(x, y, C32(142 + noise, 132 + noise, 82 + noise / 2));
                }
            }
        }
        return texture;
    }

    private static TextureData CreateHazardTexture() {
        TextureData texture = new TextureData(32, 32);
        for (int y = 0; y < texture.Height; y++) {
            for (int x = 0; x < texture.Width; x++) {
                int noise = Mathf.Abs(Hash(x, y, 21)) % 66;
                bool shine = ((x + y * 2 + noise) % 17) < 3;
                texture.SetPixel(x, y, shine ? C32(186 + noise / 3, 168 + noise / 4, 88) : C32(80 + noise / 4, 66 + noise / 5, 38));
            }
        }
        return texture;
    }

    private static TextureData CreateEnemyTexture(bool pain) {
        TextureData texture = new TextureData(32, 48);
        ClearTransparent(texture);
        Color32 voidBlack = pain ? C32(36, 9, 8) : C32(8, 10, 8);
        Color32 shadow = pain ? C32(82, 16, 12) : C32(19, 22, 17);
        Color32 edge = pain ? C32(142, 34, 22) : C32(43, 45, 33);
        Color32 eye = pain ? C32(255, 78, 28) : C32(236, 219, 94);
        Color32 smear = C32(126, 13, 8);

        FillTextureEllipse(texture, 16, 7, 6, 5, shadow);
        FillTextureEllipse(texture, 16, 8, 4, 6, voidBlack);
        FillTextureRect(texture, 12, 9, 3, 1, eye);
        FillTextureRect(texture, 18, 9, 3, 1, eye);
        FillTextureRect(texture, 13, 13, 7, 1, edge);

        FillTextureRect(texture, 12, 15, 8, 21, shadow);
        FillTextureRect(texture, 14, 16, 4, 22, voidBlack);
        FillTextureRect(texture, 11, 18, 2, 17, edge);
        FillTextureRect(texture, 20, 19, 2, 15, edge);

        DrawTextureLine(texture, 11, 18, 4, 39, 3, shadow);
        DrawTextureLine(texture, 21, 18, 27, 39, 3, shadow);
        FillTextureRect(texture, 3, 38, 4, 2, voidBlack);
        FillTextureRect(texture, 25, 38, 4, 2, voidBlack);

        DrawTextureLine(texture, 14, 35, 10, 47, 3, shadow);
        DrawTextureLine(texture, 18, 35, 22, 47, 3, shadow);
        FillTextureRect(texture, 8, 45, 5, 2, voidBlack);
        FillTextureRect(texture, 20, 45, 5, 2, voidBlack);

        FillTextureRect(texture, 15, 3, 2, 41, C32(52, 53, 38));
        for (int y = 4; y < 46; y += 5) {
            int sway = Mathf.Abs(Hash(y, 0, 71)) % 3 - 1;
            FillTextureRect(texture, 15 + sway, y, 1, 2, C32(76, 71, 43));
        }
        if (pain) {
            FillTextureRect(texture, 10, 18, 3, 7, smear);
            FillTextureRect(texture, 20, 21, 3, 8, smear);
            DrawTextureLine(texture, 8, 31, 24, 37, 2, C32(195, 32, 18));
        }
        return texture;
    }

    private static TextureData CreateCorpseTexture() {
        TextureData texture = new TextureData(40, 22);
        ClearTransparent(texture);

        Color32 black = C32(7, 8, 6);
        Color32 shadow = C32(24, 25, 17);
        Color32 edge = C32(63, 55, 30);
        Color32 smear = C32(112, 15, 9);

        FillTextureEllipse(texture, 20, 12, 17, 7, shadow);
        FillTextureEllipse(texture, 21, 13, 12, 5, black);
        DrawTextureLine(texture, 6, 8, 34, 16, 3, shadow);
        DrawTextureLine(texture, 12, 5, 28, 20, 2, edge);
        FillTextureRect(texture, 9, 13, 22, 3, smear);
        FillTextureRect(texture, 14, 16, 13, 2, C32(143, 23, 13));
        FillTextureRect(texture, 5, 17, 30, 2, C32(52, 43, 24));
        return texture;
    }

    private static TextureData CreateHealthPickupTexture() {
        TextureData texture = new TextureData(16, 16);
        ClearTransparent(texture);
        FillTextureRect(texture, 6, 1, 4, 2, C32(42, 88, 102));
        FillTextureRect(texture, 5, 3, 6, 10, C32(181, 219, 213));
        FillTextureRect(texture, 6, 4, 4, 8, C32(93, 154, 158));
        FillTextureRect(texture, 7, 6, 2, 4, C32(237, 222, 134));
        FillTextureRect(texture, 4, 13, 8, 2, C32(32, 38, 34));
        return texture;
    }

    private static TextureData CreateAmmoPickupTexture() {
        TextureData texture = new TextureData(16, 16);
        ClearTransparent(texture);
        FillTextureRect(texture, 3, 5, 10, 7, C32(186, 158, 52));
        FillTextureRect(texture, 4, 4, 8, 2, C32(238, 219, 115));
        FillTextureRect(texture, 5, 7, 6, 2, C32(57, 48, 22));
        FillTextureRect(texture, 5, 10, 2, 1, C32(232, 231, 177));
        FillTextureRect(texture, 9, 10, 2, 1, C32(232, 231, 177));
        return texture;
    }

    private static TextureData CreatePuffTexture() {
        TextureData texture = new TextureData(16, 16);
        ClearTransparent(texture);
        for (int y = 0; y < 16; y++) {
            for (int x = 0; x < 16; x++) {
                float dx = x - 7.5f;
                float dy = y - 7.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < 7.0f) {
                    byte alpha = (byte)Mathf.Clamp((7.0f - d) * 32.0f, 0.0f, 220.0f);
                    texture.SetPixel(x, y, new Color32(214, 196, 122, alpha));
                }
            }
        }
        return texture;
    }

    private static TextureData CreateFurnitureTexture(int variant) {
        TextureData texture = new TextureData(32, 32);
        ClearTransparent(texture);

        Color32 shadow = C32(36, 33, 24);
        Color32 dark = C32(78, 70, 48);
        Color32 mid = C32(137, 121, 77);
        Color32 light = C32(188, 171, 105);
        Color32 metal = C32(156, 158, 148);
        Color32 metalDark = C32(88, 91, 88);

        if (variant == 0) {
            FillTextureEllipse(texture, 16, 27, 10, 3, shadow);
            FillTextureRect(texture, 11, 12, 10, 10, dark);
            FillTextureRect(texture, 12, 13, 8, 8, mid);
            FillTextureRect(texture, 13, 14, 6, 2, light);
            FillTextureRect(texture, 10, 20, 12, 3, dark);
            FillTextureRect(texture, 14, 22, 4, 5, metalDark);
            DrawTextureLine(texture, 14, 25, 9, 30, 2, metalDark);
            DrawTextureLine(texture, 18, 25, 23, 30, 2, metalDark);
            FillTextureRect(texture, 8, 29, 5, 2, shadow);
            FillTextureRect(texture, 20, 29, 5, 2, shadow);
        } else if (variant == 1) {
            FillTextureEllipse(texture, 16, 28, 14, 3, shadow);
            FillTextureRect(texture, 5, 12, 22, 5, dark);
            FillTextureRect(texture, 6, 13, 20, 3, light);
            FillTextureRect(texture, 7, 17, 3, 10, metalDark);
            FillTextureRect(texture, 22, 17, 3, 10, metalDark);
            FillTextureRect(texture, 8, 18, 2, 8, metal);
            FillTextureRect(texture, 23, 18, 2, 8, metal);
            FillTextureRect(texture, 11, 10, 10, 2, C32(211, 199, 132));
        } else {
            FillTextureEllipse(texture, 16, 29, 9, 2, shadow);
            FillTextureRect(texture, 10, 5, 12, 23, metalDark);
            FillTextureRect(texture, 11, 6, 10, 21, metal);
            FillTextureRect(texture, 12, 8, 8, 4, C32(193, 190, 160));
            FillTextureRect(texture, 12, 14, 8, 4, C32(174, 171, 145));
            FillTextureRect(texture, 12, 20, 8, 4, C32(156, 153, 130));
            FillTextureRect(texture, 18, 10, 1, 1, dark);
            FillTextureRect(texture, 18, 16, 1, 1, dark);
            FillTextureRect(texture, 18, 22, 1, 1, dark);
        }

        return texture;
    }

    private static TextureData CreateStopSignTexture() {
        TextureData texture = new TextureData(32, 32);
        ClearTransparent(texture);

        Color32 shadow = C32(38, 31, 22);
        Color32 pole = C32(154, 153, 139);
        Color32 poleDark = C32(80, 82, 79);
        Color32 red = C32(166, 23, 20);
        Color32 redLight = C32(215, 46, 36);
        Color32 border = C32(235, 222, 194);

        FillTextureEllipse(texture, 16, 30, 8, 2, shadow);
        FillTextureRect(texture, 15, 17, 3, 12, poleDark);
        FillTextureRect(texture, 16, 17, 1, 12, pole);

        FillTextureRect(texture, 12, 2, 8, 2, border);
        FillTextureRect(texture, 9, 4, 14, 2, border);
        FillTextureRect(texture, 7, 6, 18, 2, border);
        FillTextureRect(texture, 5, 8, 22, 10, border);
        FillTextureRect(texture, 7, 18, 18, 2, border);
        FillTextureRect(texture, 9, 20, 14, 2, border);
        FillTextureRect(texture, 12, 22, 8, 2, border);

        FillTextureRect(texture, 13, 4, 6, 1, red);
        FillTextureRect(texture, 10, 6, 12, 2, red);
        FillTextureRect(texture, 8, 8, 16, 2, red);
        FillTextureRect(texture, 7, 10, 18, 7, red);
        FillTextureRect(texture, 8, 17, 16, 1, red);
        FillTextureRect(texture, 10, 18, 12, 2, red);
        FillTextureRect(texture, 13, 20, 6, 1, red);

        FillTextureRect(texture, 10, 7, 6, 1, redLight);
        FillTextureRect(texture, 8, 9, 4, 2, redLight);

        FillTextureRect(texture, 7, 11, 3, 1, border);
        FillTextureRect(texture, 7, 12, 1, 1, border);
        FillTextureRect(texture, 7, 13, 3, 1, border);
        FillTextureRect(texture, 9, 14, 1, 1, border);
        FillTextureRect(texture, 7, 15, 3, 1, border);

        FillTextureRect(texture, 11, 11, 3, 1, border);
        FillTextureRect(texture, 12, 12, 1, 4, border);

        FillTextureRect(texture, 15, 11, 3, 1, border);
        FillTextureRect(texture, 15, 12, 1, 3, border);
        FillTextureRect(texture, 17, 12, 1, 3, border);
        FillTextureRect(texture, 15, 15, 3, 1, border);

        FillTextureRect(texture, 19, 11, 3, 1, border);
        FillTextureRect(texture, 19, 12, 1, 4, border);
        FillTextureRect(texture, 21, 12, 1, 2, border);
        FillTextureRect(texture, 19, 14, 3, 1, border);

        return texture;
    }

    private static TextureData CreateWeaponTexture(bool flash) {
        TextureData texture = new TextureData(128, 96);
        ClearTransparent(texture);

        Color32 black = C32(12, 11, 12);
        Color32 nearBlack = C32(24, 23, 25);
        Color32 metalDark = C32(43, 43, 47);
        Color32 metal = C32(77, 77, 84);
        Color32 metalLight = C32(122, 121, 128);
        Color32 warmMetal = C32(91, 82, 70);
        Color32 leather = C32(63, 39, 29);
        Color32 leatherLight = C32(104, 66, 42);
        Color32 skin = C32(172, 116, 77);
        Color32 skinLight = C32(205, 151, 103);
        Color32 skinShade = C32(105, 65, 48);

        FillTextureEllipse(texture, 64, 94, 31, 6, C32(12, 10, 9));

        FillTextureTrapezoid(texture, 76, 95, 36, 56, 30, 53, C32(54, 42, 36));
        FillTextureTrapezoid(texture, 76, 95, 72, 92, 75, 98, C32(54, 42, 36));
        FillTextureEllipse(texture, 48, 76, 11, 8, skinShade);
        FillTextureEllipse(texture, 48, 73, 10, 7, skin);
        FillTextureRect(texture, 42, 72, 5, 4, skinLight);
        FillTextureRect(texture, 49, 72, 5, 4, skinLight);
        FillTextureEllipse(texture, 80, 76, 11, 8, skinShade);
        FillTextureEllipse(texture, 80, 73, 10, 7, skin);
        FillTextureRect(texture, 74, 72, 5, 4, skinLight);
        FillTextureRect(texture, 81, 72, 5, 4, skinLight);

        FillTextureTrapezoid(texture, 58, 95, 55, 74, 47, 81, black);
        FillTextureTrapezoid(texture, 61, 95, 57, 72, 51, 77, leather);
        FillTextureTrapezoid(texture, 64, 88, 61, 70, 56, 72, leatherLight);
        FillTextureRect(texture, 62, 66, 8, 5, black);

        FillTextureTrapezoid(texture, 43, 74, 42, 86, 35, 93, black);
        FillTextureTrapezoid(texture, 46, 70, 45, 83, 40, 88, metalDark);
        FillTextureTrapezoid(texture, 48, 62, 47, 81, 45, 83, metal);
        FillTextureRect(texture, 50, 51, 28, 5, metalLight);
        FillTextureRect(texture, 46, 61, 38, 4, nearBlack);
        FillTextureRect(texture, 56, 67, 17, 4, warmMetal);

        FillTextureTrapezoid(texture, 24, 49, 51, 77, 46, 82, black);
        FillTextureTrapezoid(texture, 27, 47, 54, 74, 50, 78, metal);
        FillTextureRect(texture, 57, 28, 14, 4, metalLight);
        FillTextureRect(texture, 53, 36, 22, 4, metalDark);
        FillTextureRect(texture, 55, 45, 18, 3, nearBlack);

        FillTextureRect(texture, 58, 5, 12, 23, black);
        FillTextureRect(texture, 60, 7, 8, 21, metalDark);
        FillTextureRect(texture, 61, 7, 3, 17, metalLight);
        FillTextureRect(texture, 56, 2, 16, 6, C32(92, 91, 97));
        FillTextureRect(texture, 60, 3, 8, 3, C32(20, 19, 20));
        FillTextureRect(texture, 63, 17, 3, 10, nearBlack);

        DrawTextureLine(texture, 41, 72, 87, 72, 2, C32(27, 26, 28));
        DrawTextureLine(texture, 49, 39, 80, 39, 1, C32(146, 142, 137));

        for (int y = 22; y < 72; y++) {
            for (int x = 40; x < 89; x++) {
                if (texture.Pixels[y * texture.Width + x].a == 0) {
                    continue;
                }

                int noise = Mathf.Abs(Hash(x, y, 38)) % 15;
                if (noise < 2) {
                    Color32 current = texture.Pixels[y * texture.Width + x];
                    texture.SetPixel(x, y, C32(current.r + 10, current.g + 10, current.b + 10));
                }
            }
        }

        if (flash) {
            FillTextureEllipse(texture, 64, 3, 18, 8, C32(255, 218, 78));
            FillTextureEllipse(texture, 64, 5, 11, 6, C32(255, 118, 28));
            FillTextureEllipse(texture, 64, 6, 6, 3, C32(255, 244, 174));
            DrawTextureLine(texture, 64, 2, 64, 19, 2, C32(255, 196, 50));
            DrawTextureLine(texture, 48, 7, 80, 7, 2, C32(255, 175, 35));
        }
        return texture;
    }

    private static void ClearTransparent(TextureData texture) {
        for (int i = 0; i < texture.Pixels.Length; i++) {
            texture.Pixels[i] = new Color32(0, 0, 0, 0);
        }
    }

    private static void FillTextureRect(TextureData texture, int x, int y, int width, int height, Color32 color) {
        for (int py = y; py < y + height; py++) {
            if (py < 0 || py >= texture.Height) {
                continue;
            }
            for (int px = x; px < x + width; px++) {
                if (px < 0 || px >= texture.Width) {
                    continue;
                }
                texture.SetPixel(px, py, color);
            }
        }
    }

    private static void FillTextureTrapezoid(TextureData texture, int yTop, int yBottom, int leftTop, int rightTop, int leftBottom, int rightBottom, Color32 color) {
        if (yBottom <= yTop) {
            return;
        }

        for (int y = yTop; y <= yBottom; y++) {
            float t = (y - yTop) / (float)(yBottom - yTop);
            int left = Mathf.RoundToInt(Mathf.Lerp(leftTop, leftBottom, t));
            int right = Mathf.RoundToInt(Mathf.Lerp(rightTop, rightBottom, t));
            FillTextureRect(texture, left, y, right - left + 1, 1, color);
        }
    }

    private static void FillTextureEllipse(TextureData texture, int centerX, int centerY, int radiusX, int radiusY, Color32 color) {
        int xMin = centerX - radiusX;
        int xMax = centerX + radiusX;
        int yMin = centerY - radiusY;
        int yMax = centerY + radiusY;
        float rx = Mathf.Max(1, radiusX);
        float ry = Mathf.Max(1, radiusY);

        for (int y = yMin; y <= yMax; y++) {
            if (y < 0 || y >= texture.Height) {
                continue;
            }

            for (int x = xMin; x <= xMax; x++) {
                if (x < 0 || x >= texture.Width) {
                    continue;
                }

                float nx = (x - centerX) / rx;
                float ny = (y - centerY) / ry;
                if (nx * nx + ny * ny <= 1.0f) {
                    texture.SetPixel(x, y, color);
                }
            }
        }
    }

    private static void DrawTextureLine(TextureData texture, int x0, int y0, int x1, int y1, int thickness, Color32 color) {
        int dx = Mathf.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        int radius = Mathf.Max(0, thickness / 2);

        while (true) {
            FillTextureRect(texture, x0 - radius, y0 - radius, thickness, thickness, color);
            if (x0 == x1 && y0 == y1) {
                break;
            }

            int e2 = 2 * err;
            if (e2 >= dy) {
                err += dy;
                x0 += sx;
            }
            if (e2 <= dx) {
                err += dx;
                y0 += sy;
            }
        }
    }

    private static int QuantizeShadeLevel(float factor) {
        if (factor < 0.06f) {
            factor = 0.06f;
        } else if (factor > 1.0f) {
            factor = 1.0f;
        }

        int level = (int)(factor * ShadeLevelCount);
        if (level < 1) {
            return 1;
        }
        if (level > ShadeLevelCount) {
            return ShadeLevelCount;
        }
        return level;
    }

    private static Color32 ApplyShadeLevel(Color32 source, int level) {
        if (level < 1) {
            level = 1;
        } else if (level > ShadeLevelCount) {
            level = ShadeLevelCount;
        }

        float factor = level * (1.0f / ShadeLevelCount);
        return new Color32(
            ClampToByte(source.r * factor * 1.04f),
            ClampToByte(source.g * factor),
            ClampToByte(source.b * factor * 0.82f),
            source.a
        );
    }

    private static byte ClampToByte(float value) {
        if (value <= 0.0f) {
            return 0;
        }
        if (value >= 255.0f) {
            return 255;
        }
        return (byte)value;
    }

    private static int FastFloorToInt(float value) {
        int integer = (int)value;
        return value < integer ? integer - 1 : integer;
    }

    private Vector2 PlayerPosition() {
        return new Vector2(playerX, playerY);
    }

    private Vector2 ForwardVector() {
        return new Vector2(Mathf.Cos(playerAngle), Mathf.Sin(playerAngle));
    }

    private Vector2 RightVector() {
        return new Vector2(-Mathf.Sin(playerAngle), Mathf.Cos(playerAngle));
    }

    private static bool RaySegmentIntersection(Vector2 origin, Vector2 direction, Vector2 a, Vector2 b, out float distance, out float along) {
        Vector2 segment = b - a;
        float denominator = Cross(direction, segment);
        if (Mathf.Abs(denominator) < 0.00001f) {
            distance = 0.0f;
            along = 0.0f;
            return false;
        }

        Vector2 toLine = a - origin;
        distance = Cross(toLine, segment) / denominator;
        along = Cross(toLine, direction) / denominator;
        return distance >= 0.0f && along >= 0.0f && along <= 1.0f;
    }

    private static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b) {
        Vector2 segment = b - a;
        float t = Vector2.Dot(point - a, segment) / Mathf.Max(0.0001f, segment.sqrMagnitude);
        t = Mathf.Clamp01(t);
        return Vector2.Distance(point, a + segment * t);
    }

    private static float Cross(Vector2 a, Vector2 b) {
        return a.x * b.y - a.y * b.x;
    }

    private static float NormalizeAngle(float angle) {
        while (angle < -Mathf.PI) {
            angle += Mathf.PI * 2.0f;
        }
        while (angle > Mathf.PI) {
            angle -= Mathf.PI * 2.0f;
        }
        return angle;
    }

    private static void Swap(ref float a, ref float b) {
        float temp = a;
        a = b;
        b = temp;
    }

    private static int Hash(int x, int y, int seed) {
        unchecked {
            int h = x * 374761393 + y * 668265263 + seed * 224682251;
            h = (h ^ (h >> 13)) * 1274126177;
            return h ^ (h >> 16);
        }
    }

    private static Color32 C32(int r, int g, int b) {
        return new Color32(
            (byte)Mathf.Clamp(r, 0, 255),
            (byte)Mathf.Clamp(g, 0, 255),
            (byte)Mathf.Clamp(b, 0, 255),
            255
        );
    }

    public bool TryConsumeAmmo() {
        if (ammo <= 0) {
            return false;
        }
        ammo--;
        return true;
    }

    public void NotifyEnemyHit(bool killed) {
        PlaySound(killed ? BackroomsDoomSound.Kill : BackroomsDoomSound.Hit);
    }

    public void NotifyEnemyDied(Vector3 position) {
        MaybeDropPickup(position.x, position.z);
    }

    public void DamagePlayer(int amount) {
        if (dead) {
            return;
        }

        health = Mathf.Max(0, health - amount);
        damageFlashTimer = 0.5f;
        PlaySound(BackroomsDoomSound.Hurt);
        if (health == 0) {
            dead = true;
            if (!arcadeOutputMode) {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    public void PlaySound(BackroomsDoomSound sound) {
        if (audioSource != null && soundClips != null && (int)sound >= 0 && (int)sound < soundClips.Length) {
            audioSource.PlayOneShot(soundClips[(int)sound]);
        }
    }

    private void CreateSounds() {
        soundClips = new[] {
            CreateShotClip(),
            CreateDryFireClip(),
            CreateHitClip(),
            CreateKillClip(),
            CreatePickupClip(),
            CreateHurtClip(),
            CreateDoorClip(),
            CreateEntityMoanClip()
        };

        if (ambienceSource != null) {
            ambienceSource.clip = CreateFluorescentHumClip();
            ambienceSource.Play();
        }
    }

    private static AudioClip MakeClip(string name, float[] samples) {
        AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateShotClip() {
        int count = (int)(SampleRate * 0.16f);
        float[] samples = new float[count];
        System.Random noise = new System.Random(42);
        for (int i = 0; i < count; i++) {
            float p = i / (float)count;
            float ts = i / (float)SampleRate;
            float env = Mathf.Pow(1.0f - p, 2.4f);
            float white = (float)noise.NextDouble() * 2.0f - 1.0f;
            float body = Mathf.Sign(Mathf.Sin(2.0f * Mathf.PI * 95.0f * ts));
            samples[i] = Mathf.Clamp((white * 0.75f + body * 0.35f) * env * 0.8f, -1.0f, 1.0f);
        }
        return MakeClip("Shot", samples);
    }

    private static AudioClip CreateDryFireClip() {
        int count = (int)(SampleRate * 0.04f);
        float[] samples = new float[count];
        for (int i = 0; i < count; i++) {
            float p = i / (float)count;
            float ts = i / (float)SampleRate;
            samples[i] = Mathf.Sign(Mathf.Sin(2.0f * Mathf.PI * 1800.0f * ts)) * Mathf.Pow(1.0f - p, 1.5f) * 0.3f;
        }
        return MakeClip("DryFire", samples);
    }

    private static AudioClip CreateHitClip() {
        int count = (int)(SampleRate * 0.08f);
        float[] samples = new float[count];
        System.Random noise = new System.Random(77);
        float previous = 0.0f;
        for (int i = 0; i < count; i++) {
            float p = i / (float)count;
            float white = (float)noise.NextDouble() * 2.0f - 1.0f;
            previous = previous * 0.6f + white * 0.4f;
            samples[i] = previous * Mathf.Pow(1.0f - p, 2.0f) * 0.7f;
        }
        return MakeClip("Hit", samples);
    }

    private static AudioClip CreateKillClip() {
        int count = (int)(SampleRate * 0.4f);
        float[] samples = new float[count];
        float phase = 0.0f;
        for (int i = 0; i < count; i++) {
            float p = i / (float)count;
            float freq = Mathf.Lerp(180.0f, 50.0f, p);
            phase += 2.0f * Mathf.PI * freq / SampleRate;
            samples[i] = Mathf.Sign(Mathf.Sin(phase)) * (1.0f - p) * 0.4f;
        }
        return MakeClip("Kill", samples);
    }

    private static AudioClip CreatePickupClip() {
        int count = (int)(SampleRate * 0.2f);
        float[] samples = new float[count];
        float phase = 0.0f;
        for (int i = 0; i < count; i++) {
            float p = i / (float)count;
            float freq = Mathf.Lerp(450.0f, 950.0f, p);
            phase += 2.0f * Mathf.PI * freq / SampleRate;
            samples[i] = Mathf.Sin(phase) * Mathf.Sin(Mathf.PI * p) * 0.5f;
        }
        return MakeClip("Pickup", samples);
    }

    private static AudioClip CreateHurtClip() {
        int count = (int)(SampleRate * 0.22f);
        float[] samples = new float[count];
        System.Random noise = new System.Random(99);
        for (int i = 0; i < count; i++) {
            float p = i / (float)count;
            float ts = i / (float)SampleRate;
            float env = Mathf.Pow(1.0f - p, 1.3f);
            float tone = Mathf.Sign(Mathf.Sin(2.0f * Mathf.PI * 120.0f * ts));
            float white = (float)noise.NextDouble() * 2.0f - 1.0f;
            samples[i] = (tone * 0.45f + white * 0.2f) * env * 0.6f;
        }
        return MakeClip("Hurt", samples);
    }

    private static AudioClip CreateDoorClip() {
        int count = (int)(SampleRate * 0.25f);
        float[] samples = new float[count];
        System.Random noise = new System.Random(55);
        float phase = 0.0f;
        for (int i = 0; i < count; i++) {
            float p = i / (float)count;
            float freq = Mathf.Lerp(260.0f, 180.0f, p);
            phase += 2.0f * Mathf.PI * freq / SampleRate;
            float white = (float)noise.NextDouble() * 2.0f - 1.0f;
            samples[i] = (Mathf.Sign(Mathf.Sin(phase)) * 0.16f + white * 0.08f) * Mathf.Sin(Mathf.PI * p);
        }
        return MakeClip("Door", samples);
    }

    private static AudioClip CreateEntityMoanClip() {
        int count = (int)(SampleRate * 1.2f);
        float[] samples = new float[count];
        System.Random noise = new System.Random(136);
        float phaseA = 0.0f;
        float phaseB = 0.0f;
        float filtered = 0.0f;
        for (int i = 0; i < count; i++) {
            float p = i / (float)count;
            float env = Mathf.Sin(Mathf.PI * p);
            float freqA = Mathf.Lerp(82.0f, 44.0f, p);
            float freqB = 124.0f + Mathf.Sin(p * Mathf.PI * 6.0f) * 9.0f;
            phaseA += 2.0f * Mathf.PI * freqA / SampleRate;
            phaseB += 2.0f * Mathf.PI * freqB / SampleRate;
            filtered = filtered * 0.92f + ((float)noise.NextDouble() * 2.0f - 1.0f) * 0.08f;
            float tone = Mathf.Sin(phaseA) * 0.42f + Mathf.Sin(phaseB) * 0.18f + filtered * 0.35f;
            samples[i] = Mathf.Clamp(tone * env * 0.58f, -1.0f, 1.0f);
        }
        return MakeClip("EntityMoan", samples);
    }

    private static AudioClip CreateFluorescentHumClip() {
        int count = (int)(SampleRate * 1.0f);
        float[] samples = new float[count];
        System.Random noise = new System.Random(408);
        float phaseA = 0.0f;
        float phaseB = 0.0f;
        float hiss = 0.0f;
        for (int i = 0; i < count; i++) {
            float p = i / (float)count;
            phaseA += 2.0f * Mathf.PI * 60.0f / SampleRate;
            phaseB += 2.0f * Mathf.PI * 120.0f / SampleRate;
            hiss = hiss * 0.86f + ((float)noise.NextDouble() * 2.0f - 1.0f) * 0.14f;
            float wobble = 0.8f + Mathf.Sin(p * Mathf.PI * 2.0f) * 0.2f;
            samples[i] = Mathf.Clamp((Mathf.Sin(phaseA) * 0.12f + Mathf.Sin(phaseB) * 0.05f + hiss * 0.035f) * wobble, -1.0f, 1.0f);
        }
        return MakeClip("FluorescentHum", samples);
    }
}
