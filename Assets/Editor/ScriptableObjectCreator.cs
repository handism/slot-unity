using System.Collections.Generic;
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

        [MenuItem("SlotGame/Create All ScriptableObject Assets")]
        public static void CreateAllAssets()
        {
            EnsureFolders();

            // SymbolData → PaylineData → PayoutTableData の順に作成（参照なし）
            CreateSymbolAssets();
            CreatePaylineAsset();
            CreatePayoutTableAsset();
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
                (0,  "Dragon",  SymbolType.Normal,  50,  100, 500),
                (1,  "Phoenix", SymbolType.Normal,  40,   80, 400),
                (2,  "Crystal", SymbolType.Normal,  30,   60, 300),
                (3,  "Sword",   SymbolType.Normal,  20,   40, 200),
                (4,  "Ace",     SymbolType.Normal,  10,   20, 100),
                (5,  "King",    SymbolType.Normal,   8,   15,  80),
                (6,  "Queen",   SymbolType.Normal,   6,   12,  60),
                (7,  "Jack",    SymbolType.Normal,   4,    8,  40),
                (8,  "Wild",    SymbolType.Wild,     0,    0,   0),
                (9,  "Scatter", SymbolType.Scatter,  0,    0,   0),
                (10, "Bonus",   SymbolType.Bonus,    0,    0,   0),
            };

            foreach (var d in defs)
            {
                string path = $"{BasePath}/Symbols/{d.name}.asset";
                if (AssetDatabase.LoadAssetAtPath<SymbolData>(path) != null)
                    continue;

                var asset = ScriptableObject.CreateInstance<SymbolData>();
                asset.symbolId   = d.id;
                asset.symbolName = d.name;
                asset.type       = d.type;
                asset.payouts    = new[] { d.p3, d.p4, d.p5 };
                // sprite / winAnim は Art アセット整備後に Unity Editor で設定する
                AssetDatabase.CreateAsset(asset, path);
            }
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
            if (AssetDatabase.LoadAssetAtPath<PayoutTableData>(path) != null)
                return;

            var asset = ScriptableObject.CreateInstance<PayoutTableData>();

            // requirements.md より: Scatter 3個→×2, 4個→×10, 5個→×50
            asset.scatterPayouts = new[]
            {
                new ScatterPayout { scatterCount = 3, multiplier =  2 },
                new ScatterPayout { scatterCount = 4, multiplier = 10 },
                new ScatterPayout { scatterCount = 5, multiplier = 50 },
            };

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

            // 各リールのシンボル出現数（計 60）
            // Dragon×2, Phoenix×3, Crystal×4, Sword×5,
            // Ace×8, King×8, Queen×8, Jack×8,
            // Wild×3, Scatter×2, Bonus×1  → 合計 52
            // + 低配当各1追加（Ace+2, King+2, Queen+2, Jack+2 = +8）→ 60
            var baseCounts = new (SymbolData sym, int count)[]
            {
                (jack,     10),
                (queen,    10),
                (king,     10),
                (ace,      10),
                (sword,     5),
                (crystal,   4),
                (phoenix,   3),
                (wild,      3),
                (dragon,    2),
                (scatter,   2),
                (bonus,     1),
            };

            for (int reelIdx = 0; reelIdx < 5; reelIdx++)
            {
                string path = $"{BasePath}/Reels/Reel{reelIdx}.asset";
                if (AssetDatabase.LoadAssetAtPath<ReelStripData>(path) != null)
                    continue;

                var asset = ScriptableObject.CreateInstance<ReelStripData>();
                asset.reelIndex = reelIdx;
                asset.strip     = BuildStrip(baseCounts, reelIdx);
                AssetDatabase.CreateAsset(asset, path);
            }
        }

        /// <summary>
        /// 60 シンボルのリールストリップを組み立てる。
        /// 素数ステップ（7）を使ったインターリーブでシンボルを均等分散させる。
        /// reelOffset でリールごとに配置を少しずらし、単調なパターンを防ぐ。
        /// </summary>
        private static List<SymbolData> BuildStrip(
            (SymbolData sym, int count)[] counts, int reelOffset)
        {
            const int totalSlots = 60;
            const int step       = 7; // gcd(7, 60) = 1 → 全スロットを一巡する

            // フラットリストを作成（出現数分のシンボル）
            var flat = new List<SymbolData>(totalSlots);
            foreach (var (sym, count) in counts)
                for (int i = 0; i < count; i++)
                    flat.Add(sym);

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
