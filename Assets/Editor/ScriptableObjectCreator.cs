using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using SlotGame.Data;

namespace SlotGame.Editor
{
    /// <summary>
    /// SlotGame/Create All ScriptableObject Assets メニューから全アセットを一括生成する。
    /// 既存アセットはスキップするため、何度実行しても安全。
    /// </summary>
    public static class ScriptableObjectCreator
    {
        private const string BasePath = "Assets/ScriptableObjects";
        private const string SpriteBasePath = "Assets/Art/Sprites/Generated";

        [MenuItem("SlotGame/Create All ScriptableObject Assets")]
        public static void CreateAllAssets()
        {
            EnsureFolders();

            // SymbolData → PaylineData → PayoutTableData → GameConfigData の順に作成（参照なし）
            CreateSymbolAssets();
            CreatePlaceholderSpritesAndAssign();
            CreatePaylineAsset();
            CreatePayoutTableAsset();
            CreateGameConfigAsset();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ReelStripData は SymbolData への参照が必要なため後で作成
            CreateReelStripAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ScriptableObjectCreator] All ScriptableObject assets created successfully!");
        }

        // ---------------------------------------------------------------
        // フォルダ
        // ---------------------------------------------------------------

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "ScriptableObjects");
            EnsureFolder("Assets", "Art");
            EnsureFolder("Assets/Art", "Sprites");
            EnsureFolder("Assets/Art/Sprites", "Generated");
            EnsureFolder(BasePath, "Symbols");
            EnsureFolder(BasePath, "Reels");
            EnsureFolder(BasePath, "Paylines");
            EnsureFolder(BasePath, "PayoutTable");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        // ---------------------------------------------------------------
        // SymbolData × 11
        // ---------------------------------------------------------------

        private static void CreateSymbolAssets()
        {
            // (symbolId, name, type, payout3, payout4, payout5)
            // Normal 以外は payouts を 0 で登録（PaylineEvaluator では参照されない）
            var defs = new (int id, string name, SymbolType type, int p3, int p4, int p5)[]
            {
                (0,  "Dragon",  SymbolType.Normal,  10,   25, 125),
                (1,  "Phoenix", SymbolType.Normal,   7,   15,  75),
                (2,  "Crystal", SymbolType.Normal,   5,   10,  50),
                (3,  "Sword",   SymbolType.Normal,   3,    6,  30),
                (4,  "Ace",     SymbolType.Normal,   2,    4,  12),
                (5,  "King",    SymbolType.Normal,   1,    3,  10),
                (6,  "Queen",   SymbolType.Normal,   1,    2,   8),
                (7,  "Jack",    SymbolType.Normal,   1,    2,   5),
                (8,  "Wild",    SymbolType.Wild,     0,    0,   0),
                (9,  "Scatter", SymbolType.Scatter,  0,    0,   0),
                (10, "Bonus",   SymbolType.Bonus,    0,    0,   0),
                (11, "Blank",   SymbolType.Filler,   0,    0,   0),
            };

            foreach (var d in defs)
            {
                string path = $"{BasePath}/Symbols/{d.name}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<SymbolData>(path);
                if (existing != null)
                {
                    // 既存アセットはスプライト参照を保持しつつ payouts のみ更新する
                    existing.payouts = new[] { d.p3, d.p4, d.p5 };
                    EditorUtility.SetDirty(existing);
                    continue;
                }

                var asset = ScriptableObject.CreateInstance<SymbolData>();
                asset.symbolId = d.id;
                asset.symbolName = d.name;
                asset.type = d.type;
                asset.payouts = new[] { d.p3, d.p4, d.p5 };
                // sprite / winAnim は Art アセット整備後に Unity Editor で設定する
                AssetDatabase.CreateAsset(asset, path);
            }
        }

        private static void CreatePlaceholderSpritesAndAssign()
        {
            var defs = new (string name, Color fill, Color accent)[]
            {
                ("Dragon",  new Color32(180,  54,  54, 255), new Color32(255, 219,  88, 255)),
                ("Phoenix", new Color32(214,  95,  32, 255), new Color32(255, 210, 102, 255)),
                ("Crystal", new Color32( 64, 156, 214, 255), new Color32(195, 243, 255, 255)),
                ("Sword",   new Color32(107, 122, 138, 255), new Color32(232, 236, 240, 255)),
                ("Ace",     new Color32( 52, 119, 188, 255), new Color32(240, 247, 255, 255)),
                ("King",    new Color32(122,  74, 172, 255), new Color32(255, 230, 140, 255)),
                ("Queen",   new Color32(185,  74, 134, 255), new Color32(255, 221, 242, 255)),
                ("Jack",    new Color32( 65, 156, 106, 255), new Color32(217, 255, 226, 255)),
                ("Wild",    new Color32(255, 180,  37, 255), new Color32(120,  43,   0, 255)),
                ("Scatter", new Color32( 98, 204, 189, 255), new Color32( 11,  77,  90, 255)),
                ("Bonus",   new Color32(156, 101,  31, 255), new Color32(255, 231, 163, 255)),
                ("Blank",   new Color32( 20,  20,  30, 255), new Color32( 35,  35,  50, 255)),
            };

            foreach (var def in defs)
            {
                string spritePath = $"{SpriteBasePath}/{def.name}.png";
                if (!File.Exists(spritePath))
                    CreatePlaceholderSpriteFile(spritePath, def.fill, def.accent);
            }

            AssetDatabase.Refresh();

            foreach (var def in defs)
            {
                string symbolPath = $"{BasePath}/Symbols/{def.name}.asset";
                var symbol = AssetDatabase.LoadAssetAtPath<SymbolData>(symbolPath);
                if (symbol == null || symbol.sprite != null)
                    continue;

                string spritePath = $"{SpriteBasePath}/{def.name}.png";
                var importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.mipmapEnabled = false;
                    importer.filterMode = FilterMode.Point;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite == null)
                    continue;

                symbol.sprite = sprite;
                EditorUtility.SetDirty(symbol);
            }
        }

        private static void CreatePlaceholderSpriteFile(string path, Color fill, Color accent)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;

            var bg = new Color(fill.r * 0.35f, fill.g * 0.35f, fill.b * 0.35f, 1f);
            var border = new Color(accent.r * 0.75f, accent.g * 0.75f, accent.b * 0.75f, 1f);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var color = bg;

                    bool outerBorder = x < 12 || x >= size - 12 || y < 12 || y >= size - 12;
                    bool innerPanel = x >= 28 && x < size - 28 && y >= 28 && y < size - 28;
                    bool horizontalBand = y >= 108 && y < 148;
                    bool verticalBand = x >= 108 && x < 148;
                    bool diamond = Mathf.Abs(x - 128) + Mathf.Abs(y - 128) < 70;

                    if (outerBorder)
                        color = border;
                    else if (innerPanel)
                        color = fill;

                    if (horizontalBand || verticalBand)
                        color = accent;

                    if (diamond)
                        color = Color.Lerp(color, Color.white, 0.3f);

                    pixels[y * size + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            var bytes = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            File.WriteAllBytes(path, bytes);
        }

        // ---------------------------------------------------------------
        // PaylineData（25 ライン）
        // ---------------------------------------------------------------

        private static void CreatePaylineAsset()
        {
            string path = $"{BasePath}/Paylines/PaylineData.asset";
            if (AssetDatabase.LoadAssetAtPath<PaylineData>(path) != null)
                return;

            var asset = ScriptableObject.CreateInstance<PaylineData>();

            // requirements.md の 25 ペイライン定義（0=Top, 1=Mid, 2=Bot）
            asset.lines = new PaylineEntry[]
            {
                new() { rows = new[] {1,1,1,1,1} }, // 01: 中段水平
                new() { rows = new[] {0,0,0,0,0} }, // 02: 上段水平
                new() { rows = new[] {2,2,2,2,2} }, // 03: 下段水平
                new() { rows = new[] {0,1,2,1,0} }, // 04: V字
                new() { rows = new[] {2,1,0,1,2} }, // 05: 逆V字
                new() { rows = new[] {1,0,0,0,1} }, // 06
                new() { rows = new[] {1,2,2,2,1} }, // 07
                new() { rows = new[] {0,0,1,2,2} }, // 08
                new() { rows = new[] {2,2,1,0,0} }, // 09
                new() { rows = new[] {0,1,1,1,2} }, // 10
                new() { rows = new[] {2,1,1,1,0} }, // 11
                new() { rows = new[] {1,1,0,1,1} }, // 12
                new() { rows = new[] {1,1,2,1,1} }, // 13
                new() { rows = new[] {0,0,2,0,0} }, // 14
                new() { rows = new[] {2,2,0,2,2} }, // 15
                new() { rows = new[] {1,0,1,2,1} }, // 16
                new() { rows = new[] {1,2,1,0,1} }, // 17
                new() { rows = new[] {0,2,0,2,0} }, // 18
                new() { rows = new[] {2,0,2,0,2} }, // 19
                new() { rows = new[] {0,1,0,1,0} }, // 20
                new() { rows = new[] {2,1,2,1,2} }, // 21
                new() { rows = new[] {0,2,2,2,0} }, // 22
                new() { rows = new[] {2,0,0,0,2} }, // 23
                new() { rows = new[] {1,1,0,0,0} }, // 24
                new() { rows = new[] {1,1,2,2,2} }, // 25
            };

            AssetDatabase.CreateAsset(asset, path);
        }

        // ---------------------------------------------------------------
        // PayoutTableData
        // ---------------------------------------------------------------

        private static void CreatePayoutTableAsset()
        {
            string path = $"{BasePath}/PayoutTable/PayoutTableData.asset";
            var existing = AssetDatabase.LoadAssetAtPath<PayoutTableData>(path);
            var asset = existing;
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<PayoutTableData>();
            }

            // requirements.md より: Scatter 3個→×2, 4個→×10, 5個→×50
            asset.scatterPayouts = new[]
            {
                new ScatterPayout { scatterCount = 3, multiplier =  2 },
                new ScatterPayout { scatterCount = 4, multiplier = 10 },
                new ScatterPayout { scatterCount = 5, multiplier = 50 },
            };

            // フリースピン回数: 3個→10回, 4個→15回, 5個→20回
            asset.freeSpinRewards = new[]
            {
                new FreeSpinReward { scatterCount = 3, spinCount = 10 },
                new FreeSpinReward { scatterCount = 4, spinCount = 15 },
                new FreeSpinReward { scatterCount = 5, spinCount = 20 },
            };
            asset.freeSpinMultiplier = 2;

            // ボーナスラウンド報酬（PLAN.md 暫定値）
            asset.bonusRewards = new[]
            {
                new BonusRewardEntry { multiplier =   5, weight = 40 },
                new BonusRewardEntry { multiplier =  10, weight = 25 },
                new BonusRewardEntry { multiplier =  20, weight = 15 },
                new BonusRewardEntry { multiplier =  30, weight = 10 },
                new BonusRewardEntry { multiplier =  50, weight =  7 },
                new BonusRewardEntry { multiplier = 100, weight =  3 },
            };

            if (existing == null)
            {
                AssetDatabase.CreateAsset(asset, path);
            }
            else
            {
                EditorUtility.SetDirty(existing);
            }
        }

        // ---------------------------------------------------------------
        // GameConfigData
        // ---------------------------------------------------------------

        private static void CreateGameConfigAsset()
        {
            string path = $"{BasePath}/GameConfig.asset";
            var existing = AssetDatabase.LoadAssetAtPath<GameConfigData>(path);
            if (existing != null)
            {
                existing.bonusTriggerReels = new[] { 0, 2, 4 };
                EditorUtility.SetDirty(existing);
                return;
            }
            var asset = ScriptableObject.CreateInstance<GameConfigData>();
            asset.bonusTriggerReels = new[] { 0, 2, 4 };
            AssetDatabase.CreateAsset(asset, path);
        }

        // ---------------------------------------------------------------
        // ReelStripData × 5
        // ---------------------------------------------------------------

        private static void CreateReelStripAssets()
        {
            // SymbolData を名前でロード
            SymbolData Load(string name) =>
                AssetDatabase.LoadAssetAtPath<SymbolData>($"{BasePath}/Symbols/{name}.asset");

            var dragon  = Load("Dragon");
            var phoenix = Load("Phoenix");
            var crystal = Load("Crystal");
            var sword   = Load("Sword");
            var ace     = Load("Ace");
            var king    = Load("King");
            var queen   = Load("Queen");
            var jack    = Load("Jack");
            var wild    = Load("Wild");
            var scatter = Load("Scatter");
            var bonus   = Load("Bonus");
            var blank   = Load("Blank");

            // 各リールのシンボル出現数（計 88）
            // 通常シンボル 62 スロット分、残り 26 を Blank（Filler）で埋める。
            // Blank はペイラインを遮断するだけで配当なし。
            // これにより全シンボルの有効出現率が約 2/3 に下がり、RTP を ~95% に抑える。
            var baseCounts = new (SymbolData sym, int count)[]
            {
                (jack,      9),
                (queen,    10),
                (king,     10),
                (ace,      10),
                (sword,     5),
                (crystal,   4),
                (phoenix,   3),
                (wild,      3),
                (dragon,    2),
                (scatter,   3),
                (bonus,     3),
                (blank,    26), // Filler: ペイライン遮断用（配当なし）
            };

            for (int reelIdx = 0; reelIdx < 5; reelIdx++)
            {
                string path = $"{BasePath}/Reels/Reel{reelIdx}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<ReelStripData>(path);
                if (existing != null)
                {
                    // 既存アセットは GUID を保持しつつ strip 内容のみ更新する
                    existing.strip = BuildStrip(baseCounts, reelIdx);
                    EditorUtility.SetDirty(existing);
                    continue;
                }

                var asset = ScriptableObject.CreateInstance<ReelStripData>();
                asset.reelIndex = reelIdx;
                asset.strip = BuildStrip(baseCounts, reelIdx);
                AssetDatabase.CreateAsset(asset, path);
            }
        }

        /// <summary>
        /// 88 シンボルのリールストリップを組み立てる（うち 26 は Blank Filler）。
        /// 素数ステップ（7）を使ったインターリーブでシンボルを均等分散させる。
        /// reelOffset でリールごとに配置を少しずらし、単調なパターンを防ぐ。
        /// </summary>
        private static List<SymbolData> BuildStrip(
            (SymbolData sym, int count)[] counts, int reelOffset)
        {
            const int totalSlots = 88; // 62 通常シンボル + 26 Blank Filler
            const int step = 7; // gcd(7, 88) = 1 → 全スロットを一巡する

            // フラットリストを作成（出現数分のシンボル）
            var flat = new List<SymbolData>(totalSlots);
            foreach (var (sym, count) in counts)
                for (int i = 0; i < count; i++)
                    flat.Add(sym);

            // シンボル総数とスロット数の不一致は無限ループを引き起こすため事前チェック
            if (flat.Count != totalSlots)
            {
                Debug.LogError($"[BuildStrip] シンボル合計 {flat.Count} が totalSlots {totalSlots} と一致しません。baseCounts を確認してください。");
                return new List<SymbolData>();
            }

            // インターリーブ配置
            var strip = new SymbolData[totalSlots];
            int pos = reelOffset % totalSlots;

            foreach (var sym in flat)
            {
                // 空きスロットを探す
                while (strip[pos] != null)
                    pos = (pos + 1) % totalSlots;

                strip[pos] = sym;
                pos = (pos + step) % totalSlots;
            }

            return new List<SymbolData>(strip);
        }
    }
}
