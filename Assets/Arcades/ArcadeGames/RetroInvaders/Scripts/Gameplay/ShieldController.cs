using System.Collections.Generic;
using UnityEngine;

namespace RetroInvaders {
    public sealed class ShieldController : MonoBehaviour {
        [SerializeField] private GameConfig config;
        [SerializeField] private PlayArea playArea;
        [SerializeField] private ShieldBlock blockPrefab;
        [SerializeField] private Material shieldMaterial;
        [SerializeField] private Transform blockRoot;

        private static readonly string[] Pattern = {
            "        ########        ",
            "      ############      ",
            "    ################    ",
            "   ##################   ",
            "  ####################  ",
            " ###################### ",
            "########################",
            "########################",
            "########################",
            "########################",
            "#########      #########",
            "########        ########",
            "#######          #######",
            "######            ######",
            "#####              #####",
            "####                ####"
        };

        private static readonly int[][] UpwardErosionMasks = {
            new[] { 0, 0, -1, 0, 1, 0, 0, -1, -1, -1, 0, -2, 1, -2 },
            new[] { 0, 0, -1, 0, 0, -1, 1, -1, -1, -2, 0, -2, 0, -3 },
            new[] { 0, 0, 1, 0, 0, -1, -1, -1, 1, -1, 0, -2, 1, -2 }
        };

        private static readonly int[][] DownwardErosionMasks = {
            new[] { 0, 0, -1, 0, 1, 0, 0, 1, -1, 1, 0, 2, 1, 2 },
            new[] { 0, 0, -1, 0, 0, 1, 1, 1, 0, 2, -1, 2, 0, 3 },
            new[] { 0, 0, 1, 0, 0, 1, -1, 1, 1, 1, 0, 2, -1, 2 }
        };

        private readonly List<ShieldBlock> blocks = new List<ShieldBlock>(1536);
        private ShieldBlock[] blockGrid;
        private int builtShieldCount = -1;
        private int builtPatternColumns = -1;
        private int builtPatternRows = -1;
        private Vector2 builtBlockSize = Vector2.zero;
        private float builtCoverageFraction = -1.0f;

        public void Configure(GameConfig gameConfig, PlayArea area, ShieldBlock prefab, Material material) {
            config = gameConfig;
            playArea = area;
            blockPrefab = prefab;
            shieldMaterial = material;
        }

        public void ResetShields() {
            if (config != null && config.ShieldCount <= 0) {
                ClearShields();
                return;
            }

            if (!CanBuild()) {
                return;
            }

            GenerateIfNeeded();

            for (int i = 0; i < blocks.Count; i++) {
                if (blocks[i] != null) {
                    blocks[i].ResetBlock();
                }
            }
        }

        public void ClearShields() {
            for (int i = 0; i < blocks.Count; i++) {
                if (blocks[i] != null) {
                    blocks[i].ClearBlock();
                }
            }
        }

        public void NotifyBlockHit(ShieldBlock block, HitInfo hit) {
            if (block == null || block.ShieldIndex < 0 || block.Row < 0 || block.Column < 0) {
                return;
            }

            bool upward = hit.Direction.y >= 0.0f;
            int[][] masks = upward ? UpwardErosionMasks : DownwardErosionMasks;
            int[] mask = masks[GetMaskIndex(block.ShieldIndex, block.Row, block.Column, masks.Length)];
            ApplyErosionMask(block.ShieldIndex, block.Row, block.Column, mask);
        }

        private void OnValidate() {
            if (blockRoot == null) {
                blockRoot = transform;
            }
        }

        private bool CanBuild() {
            return config != null && playArea != null && blockPrefab != null && config.ShieldCount > 0;
        }

        private void GenerateIfNeeded() {
            int patternColumns = GetPatternColumns();
            Vector2 blockSize = config.ShieldBlockSize;

            if (builtShieldCount == config.ShieldCount
                && builtPatternColumns == patternColumns
                && builtPatternRows == Pattern.Length
                && builtBlockSize == blockSize
                && Mathf.Approximately(builtCoverageFraction, config.ShieldCoverageFraction)
                && blocks.Count > 0) {
                return;
            }

            ClearGeneratedBlocks();
            EnsureBlockRoot();

            int columnOffset = (Pattern[0].Length - patternColumns) / 2;
            Vector3 blockScale = new Vector3(blockSize.x, blockSize.y, 0.18f);

            for (int shieldIndex = 0; shieldIndex < config.ShieldCount; shieldIndex++) {
                Vector3 shieldCenter = GetShieldCenter(shieldIndex, config.ShieldCount, patternColumns, blockSize);

                for (int row = 0; row < Pattern.Length; row++) {
                    for (int column = 0; column < patternColumns; column++) {
                        int patternColumn = column + columnOffset;
                        if (Pattern[row][patternColumn] != '#') {
                            continue;
                        }

                        ShieldBlock block = Instantiate(blockPrefab, blockRoot);
                        block.name = "Shield_" + shieldIndex.ToString("00") + "_Block_" + row.ToString("00") + "_" + column.ToString("00");
                        block.transform.localPosition = shieldCenter + GetBlockOffset(row, column, patternColumns, blockSize);
                        block.transform.localRotation = Quaternion.identity;
                        block.Configure(this, shieldIndex, row, column, config.ShieldBlocksHealth, blockScale, shieldMaterial);
                        blocks.Add(block);
                        SetGridBlock(shieldIndex, row, column, block);
                    }
                }
            }

            builtShieldCount = config.ShieldCount;
            builtPatternColumns = patternColumns;
            builtPatternRows = Pattern.Length;
            builtBlockSize = blockSize;
            builtCoverageFraction = config.ShieldCoverageFraction;
        }

        private Vector3 GetShieldCenter(int shieldIndex, int shieldCount, int patternColumns, Vector2 blockSize) {
            float bunkerWidth = patternColumns * blockSize.x;
            float totalBunkerWidth = bunkerWidth * shieldCount;
            float targetWidth = playArea.Width * config.ShieldCoverageFraction;
            float gap = shieldCount > 1 ? Mathf.Max(blockSize.x * 4.0f, (targetWidth - totalBunkerWidth) / (shieldCount - 1)) : 0.0f;
            float groupWidth = totalBunkerWidth + gap * (shieldCount - 1);
            float x = playArea.Center.x - groupWidth * 0.5f + bunkerWidth * 0.5f + shieldIndex * (bunkerWidth + gap);
            return new Vector3(x, config.ShieldY, 0.0f);
        }

        private static Vector3 GetBlockOffset(int row, int column, int patternColumns, Vector2 blockSize) {
            float x = (column - (patternColumns - 1) * 0.5f) * blockSize.x;
            float y = ((Pattern.Length - 1) * 0.5f - row) * blockSize.y;
            return new Vector3(x, y, 0.0f);
        }

        private int GetPatternColumns() {
            return Mathf.Clamp(config.ShieldWidthPattern, 16, Pattern[0].Length);
        }

        private void EnsureBlockRoot() {
            if (blockRoot != null) {
                return;
            }

            GameObject root = new GameObject("ShieldBlocks");
            root.transform.SetParent(transform, false);
            blockRoot = root.transform;
        }

        private void ClearGeneratedBlocks() {
            for (int i = 0; i < blocks.Count; i++) {
                if (blocks[i] == null) {
                    continue;
                }

                GameObject blockObject = blocks[i].gameObject;
                if (Application.isPlaying) {
                    Destroy(blockObject);
                } else {
                    DestroyImmediate(blockObject);
                }
            }

            blocks.Clear();
            blockGrid = null;
        }

        private void SetGridBlock(int shieldIndex, int row, int column, ShieldBlock block) {
            EnsureGrid();
            int index = GetGridIndex(shieldIndex, row, column);
            if (index >= 0 && index < blockGrid.Length) {
                blockGrid[index] = block;
            }
        }

        private void ApplyErosionMask(int shieldIndex, int centerRow, int centerColumn, int[] mask) {
            if (mask == null) {
                return;
            }

            for (int i = 0; i < mask.Length - 1; i += 2) {
                ClearBlockAt(shieldIndex, centerRow + mask[i + 1], centerColumn + mask[i]);
            }
        }

        private void ClearBlockAt(int shieldIndex, int row, int column) {
            ShieldBlock block = GetGridBlock(shieldIndex, row, column);
            if (block != null && block.gameObject.activeSelf) {
                block.ClearBlock();
            }
        }

        private ShieldBlock GetGridBlock(int shieldIndex, int row, int column) {
            if (blockGrid == null || shieldIndex < 0 || row < 0 || column < 0 || row >= builtPatternRows || column >= builtPatternColumns) {
                return null;
            }

            int index = GetGridIndex(shieldIndex, row, column);
            return index >= 0 && index < blockGrid.Length ? blockGrid[index] : null;
        }

        private int GetGridIndex(int shieldIndex, int row, int column) {
            if (builtPatternColumns <= 0 || builtPatternRows <= 0) {
                return -1;
            }

            return (shieldIndex * builtPatternRows + row) * builtPatternColumns + column;
        }

        private void EnsureGrid() {
            int patternColumns = GetPatternColumns();
            int patternRows = Pattern.Length;
            int gridSize = config.ShieldCount * patternRows * patternColumns;

            if (blockGrid == null || blockGrid.Length != gridSize) {
                blockGrid = new ShieldBlock[gridSize];
                builtPatternRows = patternRows;
                builtPatternColumns = patternColumns;
            }
        }

        private static int GetMaskIndex(int shieldIndex, int row, int column, int maskCount) {
            if (maskCount <= 1) {
                return 0;
            }

            int hash = shieldIndex * 73856093 ^ row * 19349663 ^ column * 83492791;
            return (hash & 0x7fffffff) % maskCount;
        }
    }
}
