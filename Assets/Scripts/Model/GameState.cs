using System;

namespace SlotGame.Model
{
    /// <summary>ゲーム全体の状態を保持するモデル（ピュア C#、Unity 非依存）</summary>
    public class GameState
    {
        public long InitialCoins { get; }
        public long MaxCoins { get; }
        public int[] ValidBetAmounts { get; }

        public long Coins { get; private set; }
        public int BetAmount { get; private set; }
        public int FreeSpinsLeft { get; private set; }
        public bool HasCompletedTutorial { get; private set; }
        public bool IsFreeSpin => FreeSpinsLeft > 0;
        public bool IsTurbo { get; private set; }
        public long TotalSpins { get; private set; }
        public long TotalWins { get; private set; }
        public long MaxWin { get; private set; }
        public int  TotalFreeSpinTriggers { get; private set; }

        // ─── セッション統計（インメモリのみ・永続化なし） ───────────────────
        private long _sessionStartCoins;
        private long _sessionTotalSpins;
        private long _sessionWins;
        private long _sessionLargestWin;
        private int  _sessionFreeSpinTriggers;

        public GameState(
            long initialCoins,
            long maxCoins,
            int[] validBetAmounts,
            long currentCoins,
            int currentBetAmount,
            bool hasCompletedTutorial = false
        )
        {
            InitialCoins = initialCoins;
            MaxCoins = maxCoins;
            ValidBetAmounts = validBetAmounts;
            Coins = Math.Clamp(currentCoins, 0, MaxCoins);
            BetAmount = currentBetAmount;
            HasCompletedTutorial = hasCompletedTutorial;
            _sessionStartCoins = Coins;
        }

        /// <summary>ベット額を消費する。残高不足の場合は false を返してコインを変更しない。</summary>
        public bool DeductBet()
        {
            if (Coins < BetAmount)
                return false;
            Coins -= BetAmount;
            return true;
        }

        /// <summary>コインを加算する。上限（9,999,999）でクランプする。</summary>
        public void AddCoins(long amount)
        {
            if (amount <= 0)
                return;
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
            if (Array.IndexOf(ValidBetAmounts, bet) < 0)
                return false;
            BetAmount = bet;
            return true;
        }

        /// <summary>フリースピンを追加する。</summary>
        public void AddFreeSpins(int count)
        {
            if (count <= 0)
                return;
            FreeSpinsLeft += count;
        }

        /// <summary>フリースピンを 1 回消費する。0 未満にはならない。</summary>
        public void ConsumeFreeSpin()
        {
            if (FreeSpinsLeft <= 0)
                return;
            FreeSpinsLeft--;
        }

        /// <summary>ターボモードを切り替える。</summary>
        public void SetTurbo(bool enabled)
        {
            IsTurbo = enabled;
        }

        /// <summary>スピン結果を記録する（ライフタイム統計 + セッション統計）。</summary>
        public void RecordSpin(long winAmount)
        {
            TotalSpins++;
            if (winAmount > MaxWin)
                MaxWin = winAmount;

            _sessionTotalSpins++;
            if (winAmount > 0)
            {
                TotalWins++;
                _sessionWins++;
                if (winAmount > _sessionLargestWin)
                    _sessionLargestWin = winAmount;
            }
        }

        /// <summary>フリースピンが発動した回数をライフタイム統計・セッション統計に記録する。</summary>
        public void RecordFreeSpinTrigger()
        {
            TotalFreeSpinTriggers++;
            _sessionFreeSpinTriggers++;
        }

        /// <summary>現在のセッション統計のスナップショットを返す。</summary>
        public SessionStats GetSessionStats()
        {
            float winRate = _sessionTotalSpins > 0
                ? (float)_sessionWins / _sessionTotalSpins * 100f
                : 0f;

            return new SessionStats(
                _sessionTotalSpins,
                _sessionWins,
                winRate,
                _sessionLargestWin,
                _sessionFreeSpinTriggers,
                Coins - _sessionStartCoins);
        }

        /// <summary>統計をセーブデータから復元する。</summary>
        public void RestoreStats(long totalSpins, long totalWins, long maxWin, int totalFreeSpinTriggers)
        {
            TotalSpins = totalSpins;
            TotalWins  = totalWins;
            MaxWin     = maxWin;
            TotalFreeSpinTriggers = totalFreeSpinTriggers;
        }

        /// <summary>ライフタイム統計のスナップショットを返す（統計パネル表示用）。</summary>
        public SessionStats GetLifetimeStats()
        {
            float winRate = TotalSpins > 0
                ? (float)TotalWins / TotalSpins * 100f
                : 0f;

            return new SessionStats(
                TotalSpins,
                TotalWins,
                winRate,
                MaxWin,
                TotalFreeSpinTriggers,
                Coins - _sessionStartCoins);
        }

        /// <summary>チュートリアルを完了状態にする。</summary>
        public void CompleteTutorial()
        {
            HasCompletedTutorial = true;
        }
    }
}
