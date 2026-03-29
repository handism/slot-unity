#nullable enable
using System.Collections.Generic;
using System.Text;
using SlotGame.Data;
using SlotGame.Model;

namespace SlotGame.Utility
{
    /// <summary>
    /// ペイライン配当判定（Unity 非依存の静的クラス）。
    /// Edit Mode ユニットテストで直接テスト可能。
    /// </summary>
    public static class PaylineEvaluator
    {
        /// <summary>
        /// スピン結果を評価して SpinResult を返す。
        /// </summary>
        /// <param name="symbolGrid">停止シンボル ID のグリッド [reel, row]</param>
        /// <param name="symbolDefs">全シンボル定義（symbolId をキーとした辞書）</param>
        /// <param name="paylines">ペイライン定義</param>
        /// <param name="payouts">Scatter 配当・ボーナス報酬テーブル</param>
        /// <param name="betAmount">ベット額（コイン）</param>
        public static SpinResult Evaluate(
            int[,]                               symbolGrid,
            IReadOnlyDictionary<int, SymbolData> symbolDefs,
            PaylineData                          paylines,
            PayoutTableData                      payouts,
            int                                  betAmount,
            int                                  reelCount = 5,
            int                                  rowCount = 3,
            int                                  minMatch = 3,
            int[]?                               bonusReels = null)
        {

            var lineWins = EvaluatePaylines(symbolGrid, symbolDefs, paylines, betAmount, reelCount, minMatch);

            var scatterPositions = GetScatterPositions(symbolGrid, symbolDefs, reelCount, rowCount);
            int scatterCount     = scatterPositions.Count;
            bool hasScatter      = scatterCount >= 3;
            long scatterWin      = CalcScatterWin(scatterCount, payouts, betAmount);

            var bonusPositions    = GetBonusPositions(symbolGrid, symbolDefs, reelCount, rowCount);
            bool hasBonusCondition = CheckBonusConditionFromPositions(bonusPositions, bonusReels);

            long totalWin = scatterWin;
            foreach (var w in lineWins) totalWin += w.WinAmount;

            return new SpinResult(
                StoppedSymbolIds:  symbolGrid,
                LineWins:          lineWins,
                HasScatter:        hasScatter,
                ScatterCount:      scatterCount,
                ScatterPositions:  scatterPositions,
                HasBonusCondition: hasBonusCondition,
                BonusPositions:    bonusPositions,
                TotalWinAmount:    totalWin
            );
        }

        /// <summary>
        /// 当たりの内訳をコンソールに出力する（デバッグ用）。
        /// </summary>
        public static void LogSpinResult(SpinResult result, IReadOnlyDictionary<int, SymbolData> defs, PayoutTableData payouts, int betAmount)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Spin Result Breakdown]");
            
            // グリッド表示
            sb.AppendLine("--- Symbol Grid ---");
            int reelCount = result.StoppedSymbolIds.GetLength(0);
            int rowCount  = result.StoppedSymbolIds.GetLength(1);

            for (int row = 0; row < rowCount; row++)
            {
                sb.Append($"Row {row}: ");
                for (int r = 0; r < reelCount; r++)
                {
                    int id = result.StoppedSymbolIds[r, row];
                    var sym = FindSymbol(defs, id);
                    sb.Append($"[{sym?.symbolName ?? id.ToString()}] ");
                }
                sb.AppendLine();
            }

            // 配当ライン
            if (result.LineWins.Count > 0)
            {
                sb.AppendLine("--- Line Wins ---");
                foreach (var win in result.LineWins)
                {
                    var sym = FindSymbol(defs, win.SymbolId);
                    sb.AppendLine($"- Line {win.LineIndex:D2}: {sym?.symbolName ?? win.SymbolId.ToString()} x{win.MatchCount} => {win.WinAmount} coins");
                }
            }

            // スキャター / ボーナス
            if (result.HasScatter || result.HasBonusCondition)
            {
                sb.AppendLine("--- Special Hits ---");
                if (result.HasScatter)
                {
                    long scatterWin = CalcScatterWin(result.ScatterCount, payouts, betAmount);
                    sb.AppendLine($"- Scatter Hit: {result.ScatterCount} symbols => {scatterWin} coins");
                }
                if (result.HasBonusCondition)
                    sb.AppendLine("- Bonus Triggered!");
            }

            sb.AppendLine($"Total Win Amount: {result.TotalWinAmount}");
            sb.AppendLine("-----------------------");

            UnityEngine.Debug.Log(sb.ToString());
        }

        // ─── ペイライン判定 ───────────────────────────────────────────────

        private static IReadOnlyList<LineWin> EvaluatePaylines(
            int[,] grid, IReadOnlyDictionary<int, SymbolData> defs, PaylineData paylines, int bet, int reelCount, int minMatch)
        {
            var wins = new List<LineWin>();
            for (int li = 0; li < paylines.lines.Length; li++)
            {
                var entry = paylines.lines[li];
                var win   = EvaluateLine(li, entry.rows, grid, defs, bet, reelCount, minMatch);
                if (win != null) wins.Add(win);
            }
            return wins;
        }

        private static LineWin? EvaluateLine(
            int lineIndex, int[] rows, int[,] grid, IReadOnlyDictionary<int, SymbolData> defs, int bet, int reelCount, int minMatch)
        {
            // 左端のシンボルを確定（Wild の場合は後続シンボルで補完）
            int baseSymbolId = -1;
            for (int r = 0; r < reelCount; r++)
            {
                int id   = grid[r, rows[r]];
                var sym  = FindSymbol(defs, id);
                if (sym == null) return null;

                if (sym.type == SymbolType.Normal)
                {
                    baseSymbolId = id;
                    break;
                }
            }

            // 全 Wild ライン: Seven（最高配当シンボル = symbolId が最も大きい Normal）相当
            if (baseSymbolId < 0)
                baseSymbolId = FindHighestNormalSymbolId(defs);

            // 連続一致カウント（Wild は baseSymbol の代替として機能）
            int matchCount = 0;
            for (int r = 0; r < reelCount; r++)
            {
                int id  = grid[r, rows[r]];
                var sym = FindSymbol(defs, id);
                if (sym == null) break;

                if (sym.type == SymbolType.Wild || id == baseSymbolId)
                    matchCount++;
                else
                    break;
            }

            if (matchCount < minMatch) return null;

            var baseSym = FindSymbol(defs, baseSymbolId);
            if (baseSym == null || baseSym.payouts == null || matchCount - minMatch >= baseSym.payouts.Length)
                return null;

            long winAmount = (long)baseSym.payouts[matchCount - minMatch] * bet;
            return new LineWin(lineIndex, baseSymbolId, matchCount, winAmount);
        }

        // ─── Scatter 判定 ─────────────────────────────────────────────────

        private static IReadOnlyList<SymbolPosition> GetScatterPositions(int[,] grid, IReadOnlyDictionary<int, SymbolData> defs, int reelCount, int rowCount)
        {
            var positions = new List<SymbolPosition>();
            for (int r = 0; r < reelCount; r++)
                for (int row = 0; row < rowCount; row++)
                {
                    var sym = FindSymbol(defs, grid[r, row]);
                    if (sym?.type == SymbolType.Scatter)
                        positions.Add(new SymbolPosition(r, row));
                }
            return positions;
        }

        private static long CalcScatterWin(int count, PayoutTableData payouts, int bet)
        {
            foreach (var sp in payouts.scatterPayouts)
                if (sp.scatterCount == count)
                    return (long)sp.multiplier * bet;
            return 0;
        }

        /// <summary>Scatter 個数に基づき、付与されるフリースピン回数を計算する。</summary>
        public static int CalculateFreeSpinCount(int scatterCount, PayoutTableData? payouts)
        {
            if (payouts != null && payouts.freeSpinRewards != null)
            {
                foreach (var reward in payouts.freeSpinRewards)
                {
                    if (reward.scatterCount == scatterCount)
                        return reward.spinCount;
                }
            }

            // Fallback (3:10, 4:15, 5+:20)
            return scatterCount switch
            {
                3 => 10,
                4 => 15,
                >= 5 => 20,
                _ => 0
            };
        }

        // ─── ボーナス条件判定 ─────────────────────────────────────────────

        private static IReadOnlyList<SymbolPosition> GetBonusPositions(int[,] grid, IReadOnlyDictionary<int, SymbolData> defs, int reelCount, int rowCount)
        {
            var positions = new List<SymbolPosition>();
            for (int r = 0; r < reelCount; r++)
                for (int row = 0; row < rowCount; row++)
                {
                    var sym = FindSymbol(defs, grid[r, row]);
                    if (sym?.type == SymbolType.Bonus)
                        positions.Add(new SymbolPosition(r, row));
                }
            return positions;
        }

        /// <summary>指定されたリールインデックス（デフォルト: 0/2/4）それぞれに Bonus タイプのシンボルが 1 つ以上あれば true。</summary>
        private static bool CheckBonusConditionFromPositions(
            IReadOnlyList<SymbolPosition> positions,
            int[]? bonusReels = null)
        {
            bonusReels ??= new[] { 0, 2, 4 };
            var flags = new bool[bonusReels.Length];

            foreach (var pos in positions)
            {
                for (int i = 0; i < bonusReels.Length; i++)
                {
                    if (pos.Reel == bonusReels[i])
                    {
                        flags[i] = true;
                        break;
                    }
                }
            }

            foreach (bool f in flags) if (!f) return false;
            return bonusReels.Length > 0;
        }

        // ─── ヘルパー ─────────────────────────────────────────────────────

        private static SymbolData? FindSymbol(IReadOnlyDictionary<int, SymbolData> defs, int id)
        {
            return defs.TryGetValue(id, out var data) ? data : null;
        }

        private static int FindHighestNormalSymbolId(IReadOnlyDictionary<int, SymbolData> defs)
        {
            int bestId      = 0;
            int bestPayout  = -1;
            foreach (var d in defs.Values)
            {
                if (d.type != SymbolType.Normal) continue;
                if (d.payouts == null || d.payouts.Length < 3) continue;
                if (d.payouts[2] > bestPayout)
                {
                    bestPayout = d.payouts[2];
                    bestId     = d.symbolId;
                }
            }
            return bestId;
        }
    }
}
