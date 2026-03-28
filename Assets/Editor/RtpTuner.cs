#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SlotGame.Core;
using SlotGame.Data;
using SlotGame.Utility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlotGame.Utility.Editor
{
    /// <summary>
    /// RTP 調整・検証ツール。
    /// - Tune RTP : アセット再生成 → RTP シミュレーション を一括実行
    /// - RTP Calculator : シミュレーションのみ実行（アセット再生成なし）
    /// </summary>
    public static class RtpTuner
    {
        private const int MaxFreeSpinAddition = 20; // BonusManager.MaxFreeSpinAddition と同値

        // ─── メニュー ────────────────────────────────────────────────────

        [MenuItem("SlotGame/Tune RTP (Recreate Assets & Calculate)")]
        public static void TuneRtp()
        {
            if (!EditorUtility.DisplayDialog(
                "Tune RTP",
                "This will update Symbol payouts and recreate Reel/Payline/PayoutTable assets. Sprite references are preserved. Are you sure?",
                "Yes, Tune RTP",
                "Cancel"))
                return;

            DeleteAssets();
            SlotGame.Editor.ScriptableObjectCreator.CreateAllAssets();
            AssignReelStripsToScene();
            Calculate();
        }

        [MenuItem("SlotGame/RTP Calculator")]
        public static void Calculate()
        {
            var strips   = LoadAssets<ReelStripData>("t:ReelStripData");
            var paylines = LoadAsset<PaylineData>("t:PaylineData");
            var payouts  = LoadAsset<PayoutTableData>("t:PayoutTableData");
            var config   = LoadAsset<GameConfigData>("t:GameConfigData");

            if (strips == null || strips.Length < 5 || paylines == null || payouts == null)
            {
                Debug.LogError("[RTP] アセットが見つかりません。先に SlotGame > Create All ScriptableObject Assets を実行してください。");
                return;
            }

            const int Iterations = 100_000;
            const int BetAmount  = 10;

            long totalBet            = 0;
            long totalNormalReturn   = 0;
            long totalFreeSpinReturn = 0;
            long totalBonusReturn    = 0;
            int  freeSpinTriggers    = 0;
            int  bonusRoundTriggers  = 0;
            int  totalFreeSpinSpins  = 0;

            var random     = new SeededRandomGenerator(12345);
            var allDefs    = CollectDefs(strips);
            var bonusReels = config != null ? config.bonusTriggerReels : new[] { 0, 2, 4 };

            var sb = new StringBuilder();
            sb.AppendLine("spin,bet,normalWin,freeSpinWin,bonusWin,totalWin,rtp_cumulative");

            for (int i = 0; i < Iterations; i++)
            {
                totalBet += BetAmount;

                var grid   = RollGrid(strips, random);
                var result = PaylineEvaluator.Evaluate(grid, allDefs, paylines, payouts, BetAmount, bonusReels: bonusReels);

                long spinNormalWin   = result.TotalWinAmount;
                long spinFreeSpinWin = 0;
                long spinBonusWin    = 0;

                if (result.HasScatter)
                {
                    freeSpinTriggers++;
                    int fsCount = PaylineEvaluator.CalculateFreeSpinCount(result.ScatterCount, payouts);
                    spinFreeSpinWin = SimulateFreeSpins(
                        fsCount, strips, allDefs, paylines, payouts,
                        BetAmount, random, ref totalFreeSpinSpins, bonusReels);
                }

                if (result.HasBonusCondition)
                {
                    bonusRoundTriggers++;
                    spinBonusWin = SimulateBonusRound(BetAmount, payouts, random);
                }

                totalNormalReturn   += spinNormalWin;
                totalFreeSpinReturn += spinFreeSpinWin;
                totalBonusReturn    += spinBonusWin;

                if ((i + 1) % 10_000 == 0)
                {
                    long  totalReturn = totalNormalReturn + totalFreeSpinReturn + totalBonusReturn;
                    float rtp         = totalBet > 0 ? (float)totalReturn / totalBet * 100f : 0f;
                    sb.AppendLine($"{i + 1},{totalBet},{totalNormalReturn},{totalFreeSpinReturn},{totalBonusReturn},{totalReturn},{rtp:F2}%");
                }
            }

            long  grandTotal  = totalNormalReturn + totalFreeSpinReturn + totalBonusReturn;
            float normalRtp   = totalBet > 0 ? (float)totalNormalReturn   / totalBet * 100f : 0f;
            float freeSpinRtp = totalBet > 0 ? (float)totalFreeSpinReturn / totalBet * 100f : 0f;
            float bonusRtp    = totalBet > 0 ? (float)totalBonusReturn    / totalBet * 100f : 0f;
            float finalRtp    = totalBet > 0 ? (float)grandTotal          / totalBet * 100f : 0f;

            Debug.Log(
                $"[RTP] {Iterations:N0} スピン完了\n" +
                $"  通常RTP: {normalRtp:F2}% | FS RTP: {freeSpinRtp:F2}% | ボーナスRTP: {bonusRtp:F2}% | 合計RTP: {finalRtp:F2}%\n" +
                $"  FS発動: {freeSpinTriggers}回 ({(float)freeSpinTriggers / Iterations * 100f:F2}%) | " +
                $"ボーナス発動: {bonusRoundTriggers}回 | FS総回数: {totalFreeSpinSpins}回");

            string outPath = Path.Combine(Application.dataPath, "../RTP_Result.csv");
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log($"[RTP] CSV を出力しました: {outPath}");
        }

        // ─── Tune RTP ヘルパー ───────────────────────────────────────────

        private static void DeleteAssets()
        {
            DeleteDirectory("Assets/ScriptableObjects/Paylines");
            DeleteDirectory("Assets/ScriptableObjects/PayoutTable");
            AssetDatabase.Refresh();
        }

        private static void AssignReelStripsToScene()
        {
            var gameManager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            if (gameManager == null)
            {
                Debug.LogWarning("[RtpTuner] シーン上に GameManager が見つかりません。Reel Strips の自動アサインをスキップします。");
                return;
            }

            var so   = new SerializedObject(gameManager);
            var prop = so.FindProperty("reelStrips");
            prop.arraySize = 5;
            for (int i = 0; i < 5; i++)
            {
                var strip = AssetDatabase.LoadAssetAtPath<ReelStripData>(
                    $"Assets/ScriptableObjects/Reels/Reel{i}.asset");
                prop.GetArrayElementAtIndex(i).objectReferenceValue = strip;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(gameManager.gameObject.scene);
            Debug.Log("[RtpTuner] GameManager.reelStrips を自動アサインしました。シーンを保存してください。");
        }

        private static void DeleteDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path)) return;

            if (!AssetDatabase.DeleteAsset(path))
            {
                foreach (var guid in AssetDatabase.FindAssets("", new[] { path }))
                    AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
                AssetDatabase.DeleteAsset(path);
            }
            AssetDatabase.Refresh();
        }

        // ─── シミュレーション ────────────────────────────────────────────

        private static int[,] RollGrid(ReelStripData[] strips, IRandomGenerator rng)
        {
            var grid = new int[5, 3];
            for (int r = 0; r < 5; r++)
            {
                int stopIdx = rng.Next(0, strips[r].strip.Count);
                for (int row = 0; row < 3; row++)
                {
                    int idx = (stopIdx - 1 + row + strips[r].strip.Count) % strips[r].strip.Count;
                    grid[r, row] = strips[r].strip[idx].symbolId;
                }
            }
            return grid;
        }

        private static long SimulateFreeSpins(
            int initialCount,
            ReelStripData[] strips, IReadOnlyDictionary<int, SymbolData> defs,
            PaylineData paylines, PayoutTableData payouts,
            int bet, IRandomGenerator rng,
            ref int totalFreeSpinsOut,
            int[] bonusReels)
        {
            long freeSpinWin = 0;
            int  remaining   = initialCount;
            int  multiplier  = payouts != null ? payouts.freeSpinMultiplier : 2;

            while (remaining > 0)
            {
                remaining--;
                totalFreeSpinsOut++;

                var grid   = RollGrid(strips, rng);
                var result = PaylineEvaluator.Evaluate(grid, defs, paylines, payouts, bet, bonusReels: bonusReels);

                freeSpinWin += result.TotalWinAmount * multiplier;

                if (result.HasScatter)
                {
                    int additional = PaylineEvaluator.CalculateFreeSpinCount(result.ScatterCount, payouts);
                    remaining += Math.Min(additional, MaxFreeSpinAddition);
                }
            }

            return freeSpinWin;
        }

        private static long SimulateBonusRound(int bet, PayoutTableData payouts, IRandomGenerator rng)
        {
            long totalWin   = 0;
            int  totalWeight = 0;
            foreach (var entry in payouts.bonusRewards)
                totalWeight += entry.weight;

            if (totalWeight <= 0) return 0;

            for (int i = 0; i < 3; i++)
            {
                int roll       = rng.Next(0, totalWeight);
                int cumulative = 0;
                foreach (var entry in payouts.bonusRewards)
                {
                    cumulative += entry.weight;
                    if (roll < cumulative)
                    {
                        totalWin += (long)entry.multiplier * bet;
                        break;
                    }
                }
            }
            return totalWin;
        }

        // ─── アセットロード ──────────────────────────────────────────────

        private static T[] LoadAssets<T>(string filter) where T : UnityEngine.Object
        {
            var guids = AssetDatabase.FindAssets(filter);
            var list  = new List<T>();
            foreach (var guid in guids)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            return list.ToArray();
        }

        private static T LoadAsset<T>(string filter) where T : UnityEngine.Object
        {
            var arr = LoadAssets<T>(filter);
            return arr.Length > 0 ? arr[0] : null;
        }

        private static Dictionary<int, SymbolData> CollectDefs(ReelStripData[] strips)
        {
            var dict = new Dictionary<int, SymbolData>();
            foreach (var s in strips)
                foreach (var sym in s.strip)
                    if (!dict.ContainsKey(sym.symbolId))
                        dict[sym.symbolId] = sym;
            return dict;
        }
    }
}
#endif
