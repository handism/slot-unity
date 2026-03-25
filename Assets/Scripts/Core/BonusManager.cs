using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SlotGame.Data;
using SlotGame.Model;
using SlotGame.Utility;
using SlotGame.View;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlotGame.Core
{
    /// <summary>フリースピン・ボーナスラウンドのフローを管理する Presenter。</summary>
    public class BonusManager : MonoBehaviour
    {
        [SerializeField] private SpinManager spinManager;

        private IRandomGenerator _random;
        private SlotConfig       _config;

        public void Initialize(IRandomGenerator random, SlotConfig config)
        {
            _random = random;
            _config = config;
        }

        /// <summary>
        /// フリースピンを実行する。
        /// 各スピン結果を onSpin コールバックで通知する。
        /// Scatter 再トリガーで追加スピンを付与する（上限 +20）。
        /// </summary>
        public async UniTask RunFreeSpins(
            GameState                        state,
            int                              count,
            ReelStripData[]                  strips,
            PaylineData                      paylines,
            PayoutTableData                  payouts,
            Func<SpinResult, UniTask>        onSpin,
            CancellationToken                ct)
        {
            state.AddFreeSpins(count);

            while (state.IsFreeSpin)
            {
                ct.ThrowIfCancellationRequested();

                state.ConsumeFreeSpin();
                var result = await spinManager.ExecuteSpin(
                    strips, paylines, payouts, state.BetAmount, ct,
                    _config?.ReelCount ?? 5,
                    _config?.RowCount ?? 3,
                    _config?.MinMatch ?? 3,
                    _config?.BonusTriggerReels ?? new[] { 0, 2, 4 });

                // フリースピン中は配当を指定倍率（デフォルト×2）で計算
                int multiplier = payouts != null ? payouts.freeSpinMultiplier : 2;
                long freeSpinWin = result.TotalWinAmount * multiplier;
                state.AddCoins(freeSpinWin);
                state.RecordSpin(freeSpinWin);

                // 再トリガー: Scatter 個数に応じて追加
                if (result.HasScatter)
                {
                    int extra = PaylineEvaluator.CalculateFreeSpinCount(result.ScatterCount, payouts);
                    if (_config != null)
                        extra = Math.Min(extra, _config.MaxFreeSpinAddition);
                    state.AddFreeSpins(extra);
                }

                await onSpin(result);
            }
        }

        /// <summary>
        /// ボーナスラウンド（宝箱選択ミニゲーム）を実行して獲得コインを返す。
        /// </summary>
        public async UniTask<long> RunBonusRound(
            int              betAmount,
            PayoutTableData  payouts,
            CancellationToken ct)
        {
            // BonusRound シーンを Additive ロード
            var op = SceneManager.LoadSceneAsync("BonusRound", LoadSceneMode.Additive);
            await op.ToUniTask(cancellationToken: ct);

            // BonusRoundView を探して宝箱選択完了を待機
            var view = FindFirstObjectByType<BonusRoundView>();
            if (view == null)
            {
                Debug.LogError("BonusRoundView not found in BonusRound scene.");
                await SceneManager.UnloadSceneAsync("BonusRound").ToUniTask(cancellationToken: ct);
                return 0;
            }

            // 9個の報酬を抽選
            int[] rewards = new int[9];
            for (int i = 0; i < 9; i++)
                rewards[i] = DrawBonusReward(payouts);

            var selectedMultipliers = await view.WaitForSelection(rewards, ct);
            long totalWin = 0;
            foreach (int mul in selectedMultipliers)
                totalWin += (long)mul * betAmount;

            await SceneManager.UnloadSceneAsync("BonusRound").ToUniTask(cancellationToken: ct);
            return totalWin;
        }

        /// <summary>ボーナス報酬テーブルから重み付き抽選で倍率を決定する。</summary>
        public int DrawBonusReward(PayoutTableData payouts)
        {
            int totalWeight = 0;
            foreach (var entry in payouts.bonusRewards)
                totalWeight += entry.weight;

            int roll = _random.Next(0, totalWeight);
            int cum  = 0;
            foreach (var entry in payouts.bonusRewards)
            {
                cum += entry.weight;
                if (roll < cum) return entry.multiplier;
            }
            return payouts.bonusRewards[^1].multiplier;
        }

    }
}
