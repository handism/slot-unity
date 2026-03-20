using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SlotGame.Data;
using SlotGame.Model;
using SlotGame.Utility;
using UnityEngine;

namespace SlotGame.Core
{
    /// <summary>全リールの回転・停止を調整し SpinResult を返す Presenter。</summary>
    public class SpinManager : MonoBehaviour
    {
        [SerializeField] private ReelController[] reels;   // 5 個

        private IRandomGenerator _random;
        private bool             _skipRequested;
        private SlotGame.Data.SymbolData[] _cachedSymbolDefs;

        public void Initialize(IRandomGenerator random) => _random = random;

        /// <summary>
        /// スピンを 1 回実行して結果を返す。
        /// キャンセル時は OperationCanceledException を上位に伝播させる。
        /// </summary>
        public async UniTask<SpinResult> ExecuteSpin(
            ReelStripData[]  strips,
            PaylineData      paylines,
            PayoutTableData  payouts,
            int              betAmount,
            CancellationToken ct)
        {
            _skipRequested = false;

            // 停止位置をリールごとに乱数決定
            var stopIndices = new int[reels.Length];
            for (int i = 0; i < reels.Length; i++)
                stopIndices[i] = _random.Next(0, strips[i].strip.Count);

            // 全リール同時にスクロール開始
            foreach (var reel in reels) reel.StartSpin();

            // 最低スピン時間（2 秒）
            await UniTask.Delay(TimeSpan.FromSeconds(2f), cancellationToken: ct);

            // 早期停止リクエストがあれば全リールを即スナップ
            if (_skipRequested)
            {
                for (int i = 0; i < reels.Length; i++)
                {
                    reels[i].RequestSkip();
                    await reels[i].StopSpin(stopIndices[i], ct);
                }
            }
            else
            {
                // 0.3 秒間隔で順次停止
                for (int i = 0; i < reels.Length; i++)
                {
                    if (i > 0)
                        await UniTask.Delay(TimeSpan.FromSeconds(0.3f), cancellationToken: ct);
                    await reels[i].StopSpin(stopIndices[i], ct);
                }
            }

            // グリッド取得
            var grid = new int[reels.Length, 3];
            for (int r = 0; r < reels.Length; r++)
            {
                var ids = reels[r].GetVisibleSymbolIds();
                for (int row = 0; row < 3; row++)
                    grid[r, row] = ids[row];
            }

            // シンボル定義の配列を集約（SymbolData は各ストリップに含まれる）
            if (_cachedSymbolDefs == null)
                _cachedSymbolDefs = CollectSymbolDefs(strips);

            return PaylineEvaluator.Evaluate(grid, _cachedSymbolDefs, paylines, payouts, betAmount);
        }

        /// <summary>スピン中に呼ぶとリールが即座に停止位置へスナップする。</summary>
        public void RequestSkip() => _skipRequested = true;

        private static SlotGame.Data.SymbolData[] CollectSymbolDefs(ReelStripData[] strips)
        {
            var set = new System.Collections.Generic.HashSet<SlotGame.Data.SymbolData>();
            foreach (var strip in strips)
                foreach (var sym in strip.strip)
                    set.Add(sym);
            var arr = new SlotGame.Data.SymbolData[set.Count];
            set.CopyTo(arr);
            return arr;
        }
    }
}
