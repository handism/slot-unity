using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SlotGame.Data;
using SlotGame.Model;
using SlotGame.Utility;

namespace SlotGame.Tests.EditMode
{
    /// <summary>
    /// PaylineEvaluator のユニットテスト。
    /// テスト用の ScriptableObject は ScriptableObject.CreateInstance で生成する。
    /// </summary>
    public class PaylineEvaluatorTests
    {
        // シンボル ID 定数
        private const int Dragon  = 0;
        private const int Phoenix = 1;
        private const int Wild    = 8;
        private const int Scatter = 9;
        private const int Bonus   = 10;

        private SymbolData[]                     _defs;
        private IReadOnlyDictionary<int, SymbolData> _defDict;
        private PaylineData                      _paylines;
        private PayoutTableData                  _payouts;

        [SetUp]
        public void SetUp()
        {
            _defs     = BuildSymbolDefs();
            _defDict  = _defs.ToDictionary(d => d.symbolId);
            _paylines = BuildPaylineData();
            _payouts  = BuildPayoutTableData();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var d in _defs) UnityEngine.Object.DestroyImmediate(d);
            UnityEngine.Object.DestroyImmediate(_paylines);
            UnityEngine.Object.DestroyImmediate(_payouts);
        }

        // ─── ペイライン配当テスト ────────────────────────────────────────

        [Test]
        public void ThreeMatch_Dragon_ReturnsCorrectWin()
        {
            // ライン 0 (全て中段): Dragon Dragon Dragon Phoenix Phoenix
            var grid = MakeGrid(Dragon, Dragon, Dragon, Phoenix, Phoenix);
            var result = Evaluate(grid, bet: 10);

            Assert.AreEqual(1, result.LineWins.Count);
            Assert.AreEqual(Dragon, result.LineWins[0].SymbolId);
            Assert.AreEqual(3,      result.LineWins[0].MatchCount);
            Assert.AreEqual(500,    result.LineWins[0].WinAmount); // 50 × bet=10
        }

        [Test]
        public void FourMatch_Dragon_ReturnsCorrectWin()
        {
            var grid = MakeGrid(Dragon, Dragon, Dragon, Dragon, Phoenix);
            var result = Evaluate(grid, bet: 10);

            Assert.AreEqual(100 * 10, result.LineWins[0].WinAmount); // 100 × 10 = 1000
            Assert.AreEqual(4,        result.LineWins[0].MatchCount);
        }

        [Test]
        public void FiveMatch_Dragon_ReturnsCorrectWin()
        {
            var grid = MakeGrid(Dragon, Dragon, Dragon, Dragon, Dragon);
            var result = Evaluate(grid, bet: 10);

            Assert.AreEqual(500 * 10, result.LineWins[0].WinAmount); // 500 × 10 = 5000
            Assert.AreEqual(5,        result.LineWins[0].MatchCount);
        }

        [Test]
        public void NoMatch_TwoDragons_NoWin()
        {
            var grid = MakeGrid(Dragon, Dragon, Phoenix, Phoenix, Phoenix);
            var result = Evaluate(grid, bet: 10);

            Assert.AreEqual(0, result.LineWins.Count);
            Assert.AreEqual(0, result.TotalWinAmount);
        }

        // ─── Wild 置換テスト ─────────────────────────────────────────────

        [Test]
        public void Wild_DoesNotSubstitute_Scatter()
        {
            // Wild Wild Scatter Phoenix Phoenix
            // もし Wild が Scatter に置換されるなら Scatter 3 揃えになるはずだが、
            // 実際には Scatter はペイライン配当の対象外（かつ Wild は置換しない）であるべき。
            var grid = MakeGrid(Wild, Wild, Scatter, Phoenix, Phoenix);
            var result = Evaluate(grid, bet: 10);

            // Scatter x3 という LineWin が発生していないことを確認
            Assert.IsFalse(result.LineWins.Any(w => w.SymbolId == Scatter));
        }

        [Test]
        public void Wild_PlusTwo_CountsAsThreeMatch()
        {
            // Wild Dragon Dragon Phoenix Phoenix → Dragon 3 揃え
            var grid = MakeGrid(Wild, Dragon, Dragon, Phoenix, Phoenix);
            var result = Evaluate(grid, bet: 10);

            Assert.AreEqual(1, result.LineWins.Count);
            Assert.AreEqual(3, result.LineWins[0].MatchCount);
            Assert.AreEqual(Dragon, result.LineWins[0].SymbolId);
        }

        [Test]
        public void Wild_Wild_Normal_CountsAsThreeMatch()
        {
            // Wild Wild Dragon Phoenix Phoenix → Dragon 3 揃え
            var grid = MakeGrid(Wild, Wild, Dragon, Phoenix, Phoenix);
            var result = Evaluate(grid, bet: 10);

            Assert.AreEqual(1, result.LineWins.Count);
            Assert.AreEqual(3, result.LineWins[0].MatchCount);
        }

        [Test]
        public void AllWild_ReturnsHighestSymbolPayout()
        {
            // 全 Wild → Dragon 5 揃え配当
            var grid = MakeGrid(Wild, Wild, Wild, Wild, Wild);
            var result = Evaluate(grid, bet: 10);

            Assert.AreEqual(1, result.LineWins.Count);
            Assert.AreEqual(5,          result.LineWins[0].MatchCount);
            Assert.AreEqual(500 * 10,   result.LineWins[0].WinAmount);
        }

        [Test]
        public void Wild_AtStart_ButDifferentNormals_NoWin()
        {
            // Wild Dragon Phoenix Dragon Phoenix → Wild は Dragon に変換 → Dragon Dragon Phoenix... で 2 揃えのみ
            var grid = MakeGrid(Wild, Dragon, Phoenix, Dragon, Phoenix);
            var result = Evaluate(grid, bet: 10);

            // Dragon Wild Dragon → Wild は Dragon として機能するが Phoenix で途切れる → 2 揃え → 配当なし
            Assert.AreEqual(0, result.LineWins.Count);
        }

        // ─── Scatter テスト ──────────────────────────────────────────────

        [Test]
        public void Scatter_ThreeOrMore_HasScatterTrue()
        {
            var grid = new int[5, 3];
            grid[0, 1] = Scatter;
            grid[1, 1] = Scatter;
            grid[2, 1] = Scatter;
            // 他は Dragon
            for (int r = 0; r < 5; r++)
                for (int row = 0; row < 3; row++)
                    if (grid[r, row] == 0) grid[r, row] = Dragon;

            var result = Evaluate(grid, bet: 10);
            Assert.IsTrue(result.HasScatter);
            Assert.AreEqual(3, result.ScatterCount);
        }

        [Test]
        public void Scatter_Two_HasScatterFalse()
        {
            var grid = new int[5, 3];
            grid[0, 1] = Scatter;
            grid[1, 1] = Scatter;
            for (int r = 0; r < 5; r++)
                for (int row = 0; row < 3; row++)
                    if (grid[r, row] == 0) grid[r, row] = Dragon;

            var result = Evaluate(grid, bet: 10);
            Assert.IsFalse(result.HasScatter);
        }

        [Test]
        public void Scatter_Two_StillHasPositions()
        {
            var grid = new int[5, 3];
            grid[0, 1] = Scatter;
            grid[1, 1] = Scatter;
            var result = Evaluate(grid, bet: 10);
            
            // 当選していなくても位置情報は保持されている（View側で HasScatter を見る必要があることの確認）
            Assert.AreEqual(2, result.ScatterPositions.Count);
        }

        [Test]
        public void Scatter_Five_ScatterCountFive()
        {
            var grid = new int[5, 3];
            for (int r = 0; r < 5; r++) grid[r, 1] = Scatter;
            for (int r = 0; r < 5; r++)
                for (int row = 0; row < 3; row++)
                    if (grid[r, row] == 0) grid[r, row] = Dragon;

            var result = Evaluate(grid, bet: 10);
            Assert.AreEqual(5, result.ScatterCount);
        }

        [Test]
        public void Scatter_Three_WinAmountIsMultiplierTimesBet()
        {
            var grid = new int[5, 3];
            grid[0, 1] = Scatter;
            grid[1, 1] = Scatter;
            grid[2, 1] = Scatter;
            for (int r = 0; r < 5; r++)
                for (int row = 0; row < 3; row++)
                    if (grid[r, row] == 0) grid[r, row] = Dragon;

            var result = Evaluate(grid, bet: 10);
            // Scatter 3個 → 倍率 ×2 → 2 × 10 = 20
            Assert.AreEqual(20, result.TotalWinAmount - SumLineWins(result));
        }

        // ─── ボーナス条件テスト ──────────────────────────────────────────

        [Test]
        public void BonusCondition_BonusOnReels0_2_4_ReturnsTrue()
        {
            var grid = new int[5, 3];
            grid[0, 1] = Bonus;
            grid[2, 1] = Bonus;
            grid[4, 1] = Bonus;
            for (int r = 0; r < 5; r++)
                for (int row = 0; row < 3; row++)
                    if (grid[r, row] == 0) grid[r, row] = Dragon;

            var result = Evaluate(grid, bet: 10);
            Assert.IsTrue(result.HasBonusCondition);
        }

        [Test]
        public void BonusCondition_MissingReel2_ReturnsFalse()
        {
            var grid = new int[5, 3];
            grid[0, 1] = Bonus;
            grid[4, 1] = Bonus;
            for (int r = 0; r < 5; r++)
                for (int row = 0; row < 3; row++)
                    if (grid[r, row] == 0) grid[r, row] = Dragon;

            var result = Evaluate(grid, bet: 10);
            Assert.IsFalse(result.HasBonusCondition);
        }

        // ─── 複数ライン同時当選テスト ────────────────────────────────────

        [Test]
        public void MultipleLines_TotalWinIsSumOfAllLineWins()
        {
            // ライン 0 (中段): Dragon×5, ライン 1 (上段): Phoenix×5, ライン 2 (下段): Dragonx5
            var grid = new int[5, 3];
            for (int r = 0; r < 5; r++)
            {
                grid[r, 0] = Phoenix; // 上段
                grid[r, 1] = Dragon;  // 中段
                grid[r, 2] = Dragon;  // 下段
            }

            var result = Evaluate(grid, bet: 10);

            long expectedLine0Win = 500 * 10; // Dragon 5-match on mid
            long expectedLine1Win = 400 * 10; // Phoenix 5-match on top
            long expectedLine2Win = 500 * 10; // Dragon 5-match on bot

            // 3つのペイライン（上段・中段・下段）が当選していることを確認
            Assert.AreEqual(3, result.LineWins.Count);

            // 各ペイラインの当選内容が正しいか個別にチェック
            Assert.IsTrue(result.LineWins.Any(w => w.LineIndex == 0 && w.SymbolId == Dragon && w.WinAmount == expectedLine0Win));
            Assert.IsTrue(result.LineWins.Any(w => w.LineIndex == 1 && w.SymbolId == Phoenix && w.WinAmount == expectedLine1Win));
            Assert.IsTrue(result.LineWins.Any(w => w.LineIndex == 2 && w.SymbolId == Dragon && w.WinAmount == expectedLine2Win));
            
            // 合計当選額が各ラインの合計と一致するか確認
            Assert.AreEqual(expectedLine0Win + expectedLine1Win + expectedLine2Win, result.TotalWinAmount);
        }

        // ─── ヘルパー ────────────────────────────────────────────────────

        private SpinResult Evaluate(int[,] grid, int bet = 10)
            => PaylineEvaluator.Evaluate(grid, _defDict, _paylines, _payouts, bet, null);

        private static long SumLineWins(SpinResult result)
        {
            long sum = 0;
            foreach (var w in result.LineWins) sum += w.WinAmount;
            return sum;
        }

        /// <summary>中段（row=1）に 5 シンボルを並べたグリッドを生成する。</summary>
        private static int[,] MakeGrid(int r0, int r1, int r2, int r3, int r4)
        {
            var grid = new int[5, 3];
            int[] mid = { r0, r1, r2, r3, r4 };
            for (int r = 0; r < 5; r++)
            {
                grid[r, 0] = Phoenix; // 上段（ライン 0 以外で参照）
                grid[r, 1] = mid[r];  // 中段（ライン 0）
                grid[r, 2] = Phoenix; // 下段
            }
            return grid;
        }

        // ─── テストデータ生成 ────────────────────────────────────────────

        private static SymbolData[] BuildSymbolDefs()
        {
            SymbolData Make(int id, string name, SymbolType type, int[] payouts)
            {
                var d = UnityEngine.ScriptableObject.CreateInstance<SymbolData>();
                d.symbolId   = id;
                d.symbolName = name;
                d.type       = type;
                d.payouts    = payouts;
                return d;
            }

            return new[]
            {
                Make(Dragon,  "Dragon",  SymbolType.Normal,  new[] { 50, 100, 500 }),
                Make(Phoenix, "Phoenix", SymbolType.Normal,  new[] { 40, 80,  400 }),
                Make(2,       "Crystal", SymbolType.Normal,  new[] { 30, 60,  300 }),
                Make(3,       "Sword",   SymbolType.Normal,  new[] { 20, 40,  200 }),
                Make(4,       "Ace",     SymbolType.Normal,  new[] { 10, 20,  100 }),
                Make(5,       "King",    SymbolType.Normal,  new[] { 10, 20,  100 }),
                Make(6,       "Queen",   SymbolType.Normal,  new[] {  5, 10,   50 }),
                Make(7,       "Jack",    SymbolType.Normal,  new[] {  5, 10,   50 }),
                Make(Wild,    "Wild",    SymbolType.Wild,    new int[0]),
                Make(Scatter, "Scatter", SymbolType.Scatter, new int[0]),
                Make(Bonus,   "Bonus",   SymbolType.Bonus,   new int[0]),
            };
        }

        private static PaylineData BuildPaylineData()
        {
            var pd = UnityEngine.ScriptableObject.CreateInstance<PaylineData>();
            // ライン 0: 中段 (1,1,1,1,1), ライン 1: 上段 (0,0,0,0,0)
            // 残りのライン 2〜24 はダミー（中段と同じ）
            var lines = new List<PaylineEntry>();
            lines.Add(new PaylineEntry { rows = new[] { 1, 1, 1, 1, 1 } }); // 中段
            lines.Add(new PaylineEntry { rows = new[] { 0, 0, 0, 0, 0 } }); // 上段
            lines.Add(new PaylineEntry { rows = new[] { 2, 2, 2, 2, 2 } }); // 下段
            for (int i = 3; i < 25; i++)
                lines.Add(new PaylineEntry { rows = new[] { 1, 1, 1, 1, 1 } }); // ダミー（中段）
            pd.lines = lines.ToArray();
            return pd;
        }

        private static PayoutTableData BuildPayoutTableData()
        {
            var pd = UnityEngine.ScriptableObject.CreateInstance<PayoutTableData>();
            pd.scatterPayouts = new[]
            {
                new ScatterPayout { scatterCount = 3, multiplier = 2  },
                new ScatterPayout { scatterCount = 4, multiplier = 10 },
                new ScatterPayout { scatterCount = 5, multiplier = 50 },
            };
            pd.bonusRewards = new[]
            {
                new BonusRewardEntry { multiplier = 5,   weight = 40 },
                new BonusRewardEntry { multiplier = 10,  weight = 25 },
                new BonusRewardEntry { multiplier = 20,  weight = 15 },
                new BonusRewardEntry { multiplier = 30,  weight = 10 },
                new BonusRewardEntry { multiplier = 50,  weight = 7  },
                new BonusRewardEntry { multiplier = 100, weight = 3  },
            };
            return pd;
        }
    }
}
