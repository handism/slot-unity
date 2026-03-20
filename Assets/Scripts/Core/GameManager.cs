using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SlotGame.Audio;
using SlotGame.Data;
using SlotGame.Model;
using SlotGame.Utility;
using SlotGame.View;
using UnityEngine;

namespace SlotGame.Core
{
    public enum GamePhase
    {
        Idle, Spinning, Evaluating, WinPresentation,
        BonusRound, FreeSpin, GameOver
    }

    /// <summary>ゲーム全体のステートマシン頂点。状態遷移のみを担う。</summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Managers")]
        [SerializeField] private SpinManager   spinManager;
        [SerializeField] private BonusManager  bonusManager;
        [SerializeField] private UIManager     uiManager;
        [SerializeField] private AudioManager  audioManager;

        [Header("Data Assets")]
        [SerializeField] private ReelStripData[]  reelStrips;   // 5本
        [SerializeField] private PaylineData       paylineData;
        [SerializeField] private PayoutTableData   payoutData;

        private GameState              _gameState;
        private SaveDataManager        _saveDataManager;
        private GamePhase              _currentPhase;
        private CancellationTokenSource _autoSpinCts;

        // ─── ライフサイクル ──────────────────────────────────────────────

        private void Awake()
        {
            // Boot シーンから渡されたデータで初期化
            if (GameContext.GameState != null)
            {
                Initialize(
                    GameContext.GameState,
                    GameContext.SaveDataManager,
                    GameContext.Random,
                    GameContext.SaveData
                );
            }
            else
            {
                // デバッグ用（Boot シーンを通さず起動した場合）
                _saveDataManager = new SaveDataManager();
                var save = _saveDataManager.Load();
                _gameState = new GameState(save.coins, save.betAmount);
                _gameState.RestoreStats(save.totalSpins, save.maxWin);

                var random = new SystemRandomGenerator();
                Initialize(_gameState, _saveDataManager, random, save);
            }
        }

        public void Initialize(
            GameState gameState,
            SaveDataManager saveDataManager,
            IRandomGenerator random,
            SaveData save)
        {
            _gameState       = gameState;
            _saveDataManager = saveDataManager;

            spinManager.Initialize(random);
            bonusManager.Initialize(random);

            audioManager.SetBGMVolume(save.bgmVolume);
            audioManager.SetSEVolume(save.seVolume);
            audioManager.PlayBGM(BGMType.Normal);
        }

        private void Start()
        {
            uiManager.UpdateCoins(_gameState.Coins);
            uiManager.UpdateBet(_gameState.BetAmount);
            TransitionTo(GamePhase.Idle);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) SaveGame();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) SaveGame();
        }

        private void OnDestroy()
        {
            _autoSpinCts?.Dispose();
        }

        // ─── UI イベント（Inspector から Button.OnClick() に登録） ─────

        public void OnSpinButtonPressed()
        {
            if (_currentPhase == GamePhase.Spinning)
            {
                spinManager.RequestSkip();
                return;
            }
            if (_currentPhase != GamePhase.Idle) return;
            RunSpinAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        public void OnAutoSpinButtonPressed(int count)
        {
            if (_currentPhase != GamePhase.Idle) return;
            RunAutoSpinAsync(count, this.GetCancellationTokenOnDestroy()).Forget();
        }

        public void OnAutoSpinStopRequested()
        {
            _autoSpinCts?.Cancel();
        }

        public void OnBetChanged(int newBet)
        {
            if (_gameState.SetBetAmount(newBet))
            {
                uiManager.UpdateBet(_gameState.BetAmount);
                SaveGame();
            }
        }

        // ─── スピンフロー ────────────────────────────────────────────────

        private async UniTask RunSpinAsync(CancellationToken destroyToken)
        {
            try
            {
                await SpinOnceAsync(destroyToken);
            }
            catch (OperationCanceledException)
            {
                TransitionTo(GamePhase.Idle);
            }
        }

        private async UniTask RunAutoSpinAsync(int count, CancellationToken destroyToken)
        {
            _autoSpinCts?.Dispose();
            _autoSpinCts = CancellationTokenSource.CreateLinkedTokenSource(destroyToken);
            var ct = _autoSpinCts.Token;

            try
            {
                for (int i = 0; i < count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    await SpinOnceAsync(ct);
                    // ボーナス発動・フリースピン発動・コイン不足で自動停止
                    if (_currentPhase != GamePhase.Idle) break;
                }
            }
            catch (OperationCanceledException)
            {
                TransitionTo(GamePhase.Idle);
            }
        }

        private async UniTask SpinOnceAsync(CancellationToken ct)
        {
            // コイン不足チェック
            if (!_gameState.DeductBet())
            {
                await HandleGameOver();
                return;
            }

            uiManager.UpdateCoins(_gameState.Coins);
            uiManager.SetSpinButtonInteractable(false);
            audioManager.PlaySE(SEType.SpinStart);

            TransitionTo(GamePhase.Spinning);
            var result = await spinManager.ExecuteSpin(reelStrips, paylineData, payoutData, _gameState.BetAmount, ct);

            TransitionTo(GamePhase.Evaluating);

            // 通常スピンの配当を加算
            if (result.TotalWinAmount > 0)
            {
                _gameState.AddCoins(result.TotalWinAmount);
                _gameState.RecordSpin(result.TotalWinAmount);
                uiManager.UpdateCoins(_gameState.Coins);

                TransitionTo(GamePhase.WinPresentation);
                await uiManager.ShowWinAmount(result.TotalWinAmount, CalcWinLevel(result.TotalWinAmount));
                uiManager.HighlightWinLines(result.LineWins);
                await UniTask.Delay(TimeSpan.FromSeconds(1.5f), cancellationToken: ct);
                uiManager.ClearLineHighlights();
            }
            else
            {
                _gameState.RecordSpin(0);
            }

            SaveGame();

            // ボーナス判定
            bool pendingFreeSpin = result.HasScatter;
            if (result.HasBonusCondition)
            {
                await HandleBonusRound(ct);
            }

            if (pendingFreeSpin)
            {
                await HandleFreeSpins(result.ScatterCount, ct);
            }

            uiManager.SetSpinButtonInteractable(true);
            TransitionTo(GamePhase.Idle);
        }

        private async UniTask HandleBonusRound(CancellationToken ct)
        {
            TransitionTo(GamePhase.BonusRound);
            audioManager.PlaySE(SEType.BonusStart);
            await audioManager.FadeOutBGM(0.5f, ct);
            audioManager.PlayBGM(BGMType.BonusRound);

            long win = await bonusManager.RunBonusRound(_gameState.BetAmount, payoutData, ct);
            _gameState.AddCoins(win);
            uiManager.UpdateCoins(_gameState.Coins);
            SaveGame();

            await audioManager.FadeOutBGM(0.5f, ct);
            audioManager.PlayBGM(BGMType.Normal);
        }

        private async UniTask HandleFreeSpins(int scatterCount, CancellationToken ct)
        {
            TransitionTo(GamePhase.FreeSpin);
            audioManager.PlaySE(SEType.FreeSpinStart);
            await audioManager.FadeOutBGM(0.5f, ct);
            audioManager.PlayBGM(BGMType.FreeSpin);

            int freeSpinCount = scatterCount switch
            {
                3 => 10,
                4 => 15,
                5 => 20,
                _ => 0
            };

            long cumulativeFreeSpinWin = 0;
            uiManager.ShowFreeSpinHUD(_gameState.FreeSpinsLeft, cumulativeFreeSpinWin);

            await bonusManager.RunFreeSpins(
                _gameState, freeSpinCount,
                reelStrips, paylineData, payoutData,
                async result =>
                {
                    cumulativeFreeSpinWin += result.TotalWinAmount * 2;
                    uiManager.UpdateCoins(_gameState.Coins);
                    uiManager.ShowFreeSpinHUD(_gameState.FreeSpinsLeft, cumulativeFreeSpinWin);
                    if (result.TotalWinAmount > 0)
                        await uiManager.ShowWinAmount(result.TotalWinAmount * 2, CalcWinLevel(result.TotalWinAmount * 2));
                },
                ct);

            uiManager.HideFreeSpinHUD();
            SaveGame();

            await audioManager.FadeOutBGM(0.5f, ct);
            audioManager.PlayBGM(BGMType.Normal);
        }

        private async UniTask HandleGameOver()
        {
            TransitionTo(GamePhase.GameOver);
            // コインをデフォルト値にリセット
            _gameState.SetCoins(1000);
            uiManager.UpdateCoins(_gameState.Coins);
            SaveGame();
            await UniTask.Delay(TimeSpan.FromSeconds(1f));
            TransitionTo(GamePhase.Idle);
        }

        // ─── ユーティリティ ──────────────────────────────────────────────

        private void TransitionTo(GamePhase next)
        {
            Debug.Log($"[GameManager] {_currentPhase} → {next}");
            _currentPhase = next;
        }

        private void SaveGame()
        {
            _saveDataManager.Save(new SaveData
            {
                coins      = _gameState.Coins,
                betAmount  = _gameState.BetAmount,
                totalSpins = _gameState.TotalSpins,
                maxWin     = _gameState.MaxWin,
            });
        }

        private static WinLevel CalcWinLevel(long amount)
        {
            if (amount >= 5000) return WinLevel.Mega;
            if (amount >= 1000) return WinLevel.Big;
            return WinLevel.Small;
        }
    }
}
