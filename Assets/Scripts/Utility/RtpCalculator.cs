#if UNITY_EDITOR
using System.IO;
using System.Text;
using SlotGame.Data;
using SlotGame.Utility;
using UnityEditor;
using UnityEngine;

namespace SlotGame.Utility.Editor
{
    /// <summary>
    /// RTP（理論還元率）をシミュレーションで検証する Editor ツール。
    /// メニュー: SlotGame / RTP Calculator
    /// </summary>
    public static class RtpCalculator
    {
        [MenuItem("SlotGame/RTP Calculator")]
        public static void Calculate()
        {
            // 各 ScriptableObject アセットを検索してロード
            var strips   = LoadAssets<ReelStripData>("t:ReelStripData");
            var paylines = LoadAsset<PaylineData>("t:PaylineData");
            var payouts  = LoadAsset<PayoutTableData>("t:PayoutTableData");

            if (strips == null || strips.Length < 5 || paylines == null || payouts == null)
            {
                Debug.LogError("[RTP] アセットが見つかりません。");
                return;
            }

            const int Iterations = 100_000;
            const int BetAmount  = 10;

            long totalBet     = 0;
            long totalReturn  = 0;
            var  random       = new SeededRandomGenerator(12345);
            var  allDefs      = CollectDefs(strips);

            var sb = new StringBuilder();
            sb.AppendLine("spin,bet,win,rtp_cumulative");

            for (int i = 0; i < Iterations; i++)
            {
                var grid = new int[5, 3];
                for (int r = 0; r < 5; r++)
                {
                    int stopIdx = random.Next(0, strips[r].strip.Count);
                    for (int row = 0; row < 3; row++)
                    {
                        int idx = (stopIdx - 1 + row + strips[r].strip.Count) % strips[r].strip.Count;
                        grid[r, row] = strips[r].strip[idx].symbolId;
                    }
                }

                var result = PaylineEvaluator.Evaluate(grid, allDefs, paylines, payouts, BetAmount);
                totalBet    += BetAmount;
                totalReturn += result.TotalWinAmount;

                if ((i + 1) % 10000 == 0)
                {
                    float rtp = totalBet > 0 ? (float)totalReturn / totalBet * 100f : 0f;
                    sb.AppendLine($"{i + 1},{totalBet},{totalReturn},{rtp:F2}%");
                }
            }

            float finalRtp = totalBet > 0 ? (float)totalReturn / totalBet * 100f : 0f;
            Debug.Log($"[RTP] {Iterations:N0} スピン完了 | 投入: {totalBet:N0} | 払出: {totalReturn:N0} | RTP: {finalRtp:F2}%");

            string outPath = Path.Combine(Application.dataPath, "../RTP_Result.csv");
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log($"[RTP] CSV を出力しました: {outPath}");
        }

        private static T[] LoadAssets<T>(string filter) where T : Object
        {
            var guids = AssetDatabase.FindAssets(filter);
            var list  = new System.Collections.Generic.List<T>();
            foreach (var guid in guids)
            {
                var path  = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) list.Add(asset);
            }
            return list.ToArray();
        }

        private static T LoadAsset<T>(string filter) where T : Object
        {
            var arr = LoadAssets<T>(filter);
            return arr.Length > 0 ? arr[0] : null;
        }

        private static Data.SymbolData[] CollectDefs(ReelStripData[] strips)
        {
            var set = new System.Collections.Generic.HashSet<Data.SymbolData>();
            foreach (var s in strips)
                foreach (var sym in s.strip)
                    set.Add(sym);
            var arr = new Data.SymbolData[set.Count];
            set.CopyTo(arr);
            return arr;
        }
    }
}
#endif
