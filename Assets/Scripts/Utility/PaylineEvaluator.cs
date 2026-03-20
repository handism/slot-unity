using System.Collections.Generic;
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
        private const int ReelCount = 5;
        private const int RowCount  = 3;
        private const int MinMatch  = 3;

        /// <summary>
        /// スピン結果を評価して SpinResult を返す。
        /// </summary>
        /// <param name="symbolGrid">停止シンボル ID のグリッド [reel, row]</param>
        /// <param name="symbolDefs">全シンボル定義（symbolId でインデックス引きできるように並べること）</param>
        /// <param name="paylines">ペイライン定義</param>
        /// <param name="payouts">Scatter 配当・ボーナス報酬テーブル</param>
        /// <param name="betAmount">ベット額（コイン）</param>
        public static SpinResult Evaluate(
            int[,]          symbolGrid,
            SymbolData[]    symbolDefs,
            PaylineData     paylines,
            PayoutTableData payouts,
            int             betAmount)
        {
            var lineWins = EvaluatePaylines(symbolGrid, symbolDefs, paylines, betAmount);

            int scatterCount    = CountScatters(symbolGrid, symbolDefs);
            bool hasScatter     = scatterCount >= 3;
            long scatterWin     = CalcScatterWin(scatterCount, payouts, betAmount);
            bool hasBonusCondition = CheckBonusCondition(symbolGrid, symbolDefs);

            long totalWin = scatterWin;
            foreach (var w in lineWins) totalWin += w.WinAmount;

            return new SpinResult(
                StoppedSymbolIds:  symbolGrid,
                LineWins:          lineWins,
                HasScatter:        hasScatter,
                ScatterCount:      scatterCount,
                HasBonusCondition: hasBonusCondition,
                TotalWinAmount:    totalWin
            );
        }

        // ─── ペイライン判定 ───────────────────────────────────────────────

        private static IReadOnlyList<LineWin> EvaluatePaylines(
            int[,] grid, SymbolData[] defs, PaylineData paylines, int bet)
        {
            var wins = new List<LineWin>();
            for (int li = 0; li < paylines.lines.Length; li++)
            {
                var entry = paylines.lines[li];
                var win   = EvaluateLine(li, entry.rows, grid, defs, bet);
                if (win != null) wins.Add(win);
            }
            return wins;
        }

        private static LineWin? EvaluateLine(
            int lineIndex, int[] rows, int[,] grid, SymbolData[] defs, int bet)
        {
            // 左端のシンボルを確定（Wild の場合は後続シンボルで補完）
            int baseSymbolId = -1;
            for (int r = 0; r < ReelCount; r++)
            {
                int id   = grid[r, rows[r]];
                var sym  = FindSymbol(defs, id);
                if (sym == null) return null;

                if (sym.type != SymbolType.Wild)
                {
                    baseSymbolId = id;
                    break;
                }
            }

            // 全 Wild ライン: Dragon（最高配当シンボル = symbolId が最も大きい Normal）相当
            if (baseSymbolId < 0)
                baseSymbolId = FindHighestNormalSymbolId(defs);

            // 連続一致カウント（Wild は baseSymbol の代替として機能）
            int matchCount = 0;
            for (int r = 0; r < ReelCount; r++)
            {
                int id  = grid[r, rows[r]];
                var sym = FindSymbol(defs, id);
                if (sym == null) break;

                if (sym.type == SymbolType.Wild || id == baseSymbolId)
                    matchCount++;
                else
                    break;
            }

            if (matchCount < MinMatch) return null;

            var baseSym = FindSymbol(defs, baseSymbolId);
            if (baseSym == null || baseSym.payouts == null || matchCount - MinMatch >= baseSym.payouts.Length)
                return null;

            long winAmount = (long)baseSym.payouts[matchCount - MinMatch] * bet;
            return new LineWin(lineIndex, baseSymbolId, matchCount, winAmount);
        }

        // ─── Scatter 判定 ─────────────────────────────────────────────────

        private static int CountScatters(int[,] grid, SymbolData[] defs)
        {
            int count = 0;
            for (int r = 0; r < ReelCount; r++)
                for (int row = 0; row < RowCount; row++)
                {
                    var sym = FindSymbol(defs, grid[r, row]);
                    if (sym?.type == SymbolType.Scatter) count++;
                }
            return count;
        }

        private static long CalcScatterWin(int count, PayoutTableData payouts, int bet)
        {
            foreach (var sp in payouts.scatterPayouts)
                if (sp.scatterCount == count)
                    return (long)sp.multiplier * bet;
            return 0;
        }

        // ─── ボーナス条件判定 ─────────────────────────────────────────────

        /// <summary>リール 0/2/4（0-indexed）それぞれに Bonus タイプのシンボルが 1 つ以上あれば true。</summary>
        private static bool CheckBonusCondition(int[,] grid, SymbolData[] defs)
        {
            foreach (int reel in new[] { 0, 2, 4 })
            {
                bool found = false;
                for (int row = 0; row < RowCount; row++)
                {
                    var sym = FindSymbol(defs, grid[reel, row]);
                    if (sym?.type == SymbolType.Bonus) { found = true; break; }
                }
                if (!found) return false;
            }
            return true;
        }

        // ─── ヘルパー ─────────────────────────────────────────────────────

        private static SymbolData? FindSymbol(SymbolData[] defs, int id)
        {
            foreach (var d in defs)
                if (d.symbolId == id) return d;
            return null;
        }

        private static int FindHighestNormalSymbolId(SymbolData[] defs)
        {
            int bestId      = 0;
            int bestPayout  = -1;
            foreach (var d in defs)
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
