namespace SlotGame.Model
{
    /// <summary>セッション中の統計スナップショット（インメモリのみ・永続化なし）。</summary>
    public readonly struct SessionStats
    {
        /// <summary>このセッションのスピン総数。</summary>
        public long TotalSpins { get; }

        /// <summary>当選スピン数。</summary>
        public long Wins { get; }

        /// <summary>当選率（0〜100 %）。</summary>
        public float WinRate { get; }

        /// <summary>1スピンで獲得した最大コイン数。</summary>
        public long LargestWin { get; }

        /// <summary>フリースピンが発動した回数。</summary>
        public int FreeSpinTriggers { get; }

        /// <summary>セッション開始時からの損益（負数 = 損失）。</summary>
        public long NetProfit { get; }

        public SessionStats(
            long totalSpins,
            long wins,
            float winRate,
            long largestWin,
            int freeSpinTriggers,
            long netProfit)
        {
            TotalSpins       = totalSpins;
            Wins             = wins;
            WinRate          = winRate;
            LargestWin       = largestWin;
            FreeSpinTriggers = freeSpinTriggers;
            NetProfit        = netProfit;
        }
    }
}
