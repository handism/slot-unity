using NUnit.Framework;
using SlotGame.Model;

namespace SlotGame.Tests.EditMode
{
    public class GameStateTests
    {
        private static GameState CreateState(long coins = 1000, int betAmount = 10)
        {
            return new GameState(1000, 9_999_999L, new[] { 10, 20, 50, 100 }, coins, betAmount);
        }

        [Test]
        public void DeductBet_SufficientCoins_ReturnsTrueAndDeductsAmount()
        {
            var state = CreateState(coins: 1000, betAmount: 10);
            bool result = state.DeductBet();
            Assert.IsTrue(result);
            Assert.AreEqual(990, state.Coins);
        }

        [Test]
        public void DeductBet_InsufficientCoins_ReturnsFalseAndCoinsUnchanged()
        {
            var state = CreateState(coins: 5, betAmount: 10);
            bool result = state.DeductBet();
            Assert.IsFalse(result);
            Assert.AreEqual(5, state.Coins);
        }

        [Test]
        public void DeductBet_ExactlyEnough_ReturnsTrueAndCoinsZero()
        {
            var state = CreateState(coins: 10, betAmount: 10);
            bool result = state.DeductBet();
            Assert.IsTrue(result);
            Assert.AreEqual(0, state.Coins);
        }

        [Test]
        public void AddCoins_Normal_IncreasesCoins()
        {
            var state = CreateState(coins: 500);
            state.AddCoins(200);
            Assert.AreEqual(700, state.Coins);
        }

        [Test]
        public void AddCoins_ExceedsMax_ClampsToMax()
        {
            var state = CreateState(coins: 9_999_990);
            state.AddCoins(100);
            Assert.AreEqual(state.MaxCoins, state.Coins);
        }

        [Test]
        public void AddCoins_ZeroOrNegative_NoChange()
        {
            var state = CreateState(coins: 500);
            state.AddCoins(0);
            state.AddCoins(-10);
            Assert.AreEqual(500, state.Coins);
        }

        [Test]
        public void FreeSpinsLeft_NeverGoesBelowZero()
        {
            var state = CreateState();
            state.ConsumeFreeSpin();
            Assert.AreEqual(0, state.FreeSpinsLeft);
        }

        [Test]
        public void AddFreeSpins_ThenConsume_DecreasesCorrectly()
        {
            var state = CreateState();
            state.AddFreeSpins(10);
            state.ConsumeFreeSpin();
            Assert.AreEqual(9, state.FreeSpinsLeft);
        }

        [Test]
        public void IsFreeSpin_WhenFreeSpinsLeft_ReturnsTrue()
        {
            var state = CreateState();
            state.AddFreeSpins(1);
            Assert.IsTrue(state.IsFreeSpin);
            state.ConsumeFreeSpin();
            Assert.IsFalse(state.IsFreeSpin);
        }

        [Test]
        public void RecordSpin_IncrementsTotalSpins()
        {
            var state = CreateState();
            state.RecordSpin(0);
            state.RecordSpin(100);
            Assert.AreEqual(2, state.TotalSpins);
        }

        [Test]
        public void RecordSpin_UpdatesMaxWinOnlyIfLarger()
        {
            var state = CreateState();
            state.RecordSpin(100);
            Assert.AreEqual(100, state.MaxWin);
            state.RecordSpin(50);
            Assert.AreEqual(100, state.MaxWin);
            state.RecordSpin(200);
            Assert.AreEqual(200, state.MaxWin);
        }

        [Test]
        public void SetBetAmount_ValidAmount_ReturnsTrue()
        {
            var state = CreateState();
            bool result = state.SetBetAmount(50);
            Assert.IsTrue(result);
            Assert.AreEqual(50, state.BetAmount);
        }

        [Test]
        public void SetBetAmount_InvalidAmount_ReturnsFalseAndUnchanged()
        {
            var state = CreateState(betAmount: 10);
            bool result = state.SetBetAmount(999);
            Assert.IsFalse(result);
            Assert.AreEqual(10, state.BetAmount);
        }

        [Test]
        public void SetTurbo_ChangesIsTurboState()
        {
            var state = CreateState();
            Assert.IsFalse(state.IsTurbo);
            state.SetTurbo(true);
            Assert.IsTrue(state.IsTurbo);
            state.SetTurbo(false);
            Assert.IsFalse(state.IsTurbo);
        }

        [Test]
        public void RecordSpin_WinAmount_IncrementsTotalWins()
        {
            var state = CreateState();
            state.RecordSpin(0);
            Assert.AreEqual(0, state.TotalWins);
            state.RecordSpin(100);
            Assert.AreEqual(1, state.TotalWins);
            state.RecordSpin(0);
            Assert.AreEqual(1, state.TotalWins);
            state.RecordSpin(50);
            Assert.AreEqual(2, state.TotalWins);
        }

        [Test]
        public void RecordFreeSpinTrigger_IncrementsTotalFreeSpinTriggers()
        {
            var state = CreateState();
            Assert.AreEqual(0, state.TotalFreeSpinTriggers);
            state.RecordFreeSpinTrigger();
            state.RecordFreeSpinTrigger();
            Assert.AreEqual(2, state.TotalFreeSpinTriggers);
        }

        [Test]
        public void RestoreStats_SetsAllLifetimeFields()
        {
            var state = CreateState();
            state.RestoreStats(totalSpins: 100, totalWins: 40, maxWin: 500, totalFreeSpinTriggers: 3);
            Assert.AreEqual(100, state.TotalSpins);
            Assert.AreEqual(40,  state.TotalWins);
            Assert.AreEqual(500, state.MaxWin);
            Assert.AreEqual(3,   state.TotalFreeSpinTriggers);
        }

        [Test]
        public void GetLifetimeStats_ReturnsLifetimeValues()
        {
            var state = CreateState(coins: 1000);
            state.RestoreStats(totalSpins: 10, totalWins: 4, maxWin: 200, totalFreeSpinTriggers: 2);
            state.AddCoins(500);

            var stats = state.GetLifetimeStats();

            Assert.AreEqual(10,    stats.TotalSpins);
            Assert.AreEqual(4,     stats.Wins);
            Assert.AreEqual(40f,   stats.WinRate, 0.01f);
            Assert.AreEqual(200,   stats.LargestWin);
            Assert.AreEqual(2,     stats.FreeSpinTriggers);
            Assert.AreEqual(500,   stats.NetProfit); // セッション損益
        }
    }
}
