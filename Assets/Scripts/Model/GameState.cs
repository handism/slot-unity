using System;

namespace SlotGame.Model
{
    /// <summary>ゲーム全体の状態を保持するモデル（ピュア C#、Unity 非依存）</summary>
    public class GameState
    {
        public long InitialCoins     { get; }
        public long MaxCoins         { get; }
        public int[] ValidBetAmounts { get; }

        public long Coins         { get; private set; }
        public int  BetAmount     { get; private set; }
        public int  FreeSpinsLeft { get; private set; }
        public bool IsFreeSpin    => FreeSpinsLeft > 0;
        public long TotalSpins    { get; private set; }
        public long MaxWin        { get; private set; }

        public GameState(long initialCoins, long maxCoins, int[] validBetAmounts, long currentCoins, int currentBetAmount)
        {
            InitialCoins = initialCoins;
            MaxCoins = maxCoins;
            ValidBetAmounts = validBetAmounts;
            Coins = Math.Clamp(currentCoins, 0, MaxCoins);
            BetAmount = currentBetAmount;
        }

        /// <summary>ベット額を消費する。残高不足の場合は false を返してコインを変更しない。</summary>
        public bool DeductBet()
        {
            if (Coins < BetAmount) return false;
            Coins -= BetAmount;
            return true;
        }

        /// <summary>コインを加算する。上限（9,999,999）でクランプする。</summary>
        public void AddCoins(long amount)
        {
            if (amount <= 0) return;
            Coins = Math.Min(Coins + amount, MaxCoins);
        }

        /// <summary>コインを直接セットする（セーブデータ読み込み時）。</summary>
        public void SetCoins(long coins)
        {
            Coins = Math.Clamp(coins, 0, MaxCoins);
        }

        /// <summary>ベット額を変更する。有効な選択肢以外は無視する。</summary>
        public bool SetBetAmount(int bet)
        {
            if (Array.IndexOf(ValidBetAmounts, bet) < 0) return false;
            BetAmount = bet;
            return true;
        }

        /// <summary>フリースピンを追加する。</summary>
        public void AddFreeSpins(int count)
        {
            if (count <= 0) return;
            FreeSpinsLeft += count;
        }

        /// <summary>フリースピンを 1 回消費する。0 未満にはならない。</summary>
        public void ConsumeFreeSpin()
        {
            if (FreeSpinsLeft <= 0) return;
            FreeSpinsLeft--;
        }

        /// <summary>スピン結果を記録する（統計用）。</summary>
        public void RecordSpin(long winAmount)
        {
            TotalSpins++;
            if (winAmount > MaxWin) MaxWin = winAmount;
        }

        /// <summary>統計・フリースピンを含む全状態をセーブデータから復元する。</summary>
        public void RestoreStats(long totalSpins, long maxWin)
        {
            TotalSpins = totalSpins;
            MaxWin     = maxWin;
        }
    }
}
