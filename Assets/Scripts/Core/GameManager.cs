using System;
using System.Linq;
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
        [SerializeField] private GameConfigData    gameConfig;

        private GameState              _gameState;
        private SaveDataManager        _saveDataManager;
        private GamePhase              _currentPhase;
        private CancellationTokenSource _autoSpinCts;
        private bool                   _isAutoSpinning;
        private float                  _bgmVolume = 0.8f;
        private float                  _seVolume  = 1f;
        private bool                   _hasLoggedSaveSkip;
        private int                    _autoSpinCount = 10;

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
                _saveDataManager = new SaveDataManager(gameConfig);
                var save = _saveDataManager.Load();
                _gameState = new GameState(
                    gameConfig.initialCoins,
                    gameConfig.maxCoins,
                    gameConfig.validBetAmounts,
                    save.coins,
                    save.betAmount
                );
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
            _saveDataManager = saveDataManager ?? new SaveDataManager(gameConfig);
            save ??= _saveDataManager.Load();
            _gameState = gameState ?? new GameState(
                gameConfig.initialCoins,
                gameConfig.maxCoins,
                gameConfig.validBetAmounts,
                save.coins,
                save.betAmount
            );
            random ??= new SystemRandomGenerator();

            spinManager.Initialize(random, reelStrips);
            bonusManager.Initialize(random, gameConfig);

            // UIManager に使用するリールを明示的にセット
            uiManager.SetupReels(spinManager.Reels.Select(r => r.GetComponent<ReelView>()));

            _bgmVolume = save.bgmVolume;
            _seVolume  = save.seVolume;

            if (save.totalSpins == 0) // Assume new game
            {
                _bgmVolume = gameConfig.defaultBgmVolume;
                _seVolume  = gameConfig.defaultSeVolume;
                _autoSpinCount = gameConfig.defaultAutoSpinCount;
            }

            audioManager.SetBGMVolume(_bgmVolume);
            audioManager.SetSEVolume(_seVolume);
            audioManager.PlayBGM(BGMType.Normal);
        }

        private void Start()
        {
            uiManager.UpdateCoins(_gameState.Coins);
            uiManager.UpdateBet(_gameState.BetAmount);
            uiManager.UpdateWin(0);
            uiManager.SetAutoButtonText(GetAutoSpinButtonText());
            uiManager.SetSettingsVolumes(_bgmVolume, _seVolume);
            uiManager.PopulatePaytable(CollectSymbolDefinitions());
            uiManager.ApplyModeVisual(ModeVisualType.Normal);
            uiManager.BgmVolumeChanged += HandleBgmVolumeChanged;
            uiManager.SeVolumeChanged += HandleSeVolumeChanged;
            uiManager.ResetCoinsRequested += HandleResetCoinsRequested;
            uiManager.SettingsCloseRequested += uiManager.HideSettings;
            uiManager.PaytableCloseRequested += uiManager.HidePaytable;
            spinManager.ReelStopped += HandleReelStopped;
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
            if (uiManager != null)
            {
                uiManager.BgmVolumeChanged -= HandleBgmVolumeChanged;
                uiManager.SeVolumeChanged -= HandleSeVolumeChanged;
                uiManager.ResetCoinsRequested -= HandleResetCoinsRequested;
                uiManager.SettingsCloseRequested -= uiManager.HideSettings;
                uiManager.PaytableCloseRequested -= uiManager.HidePaytable;
            }

            if (spinManager != null)
            {
                spinManager.ReelStopped -= HandleReelStopped;
            }

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
            if (_isAutoSpinning)
            {
                OnAutoSpinStopRequested();
                return;
            }

            if (_currentPhase != GamePhase.Idle) return;
            _autoSpinCount = count;
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

        public void OnSettingsButtonPressed()
        {
            uiManager.ShowSettings();
        }

        public void OnPaytableButtonPressed()
        {
            uiManager.ShowPaytable();
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
                uiManager.HideFreeSpinHUD();
                uiManager.ClearLineHighlights();
                uiManager.ApplyModeVisual(ModeVisualType.Normal);
                TransitionTo(GamePhase.Idle);
            }
        }

        private async UniTask RunAutoSpinAsync(int count, CancellationToken destroyToken)
        {
            _autoSpinCts?.Dispose();
            _autoSpinCts = CancellationTokenSource.CreateLinkedTokenSource(destroyToken);
            var ct = _autoSpinCts.Token;

            _isAutoSpinning = true;
            uiManager.SetAutoButtonText("ストップ");

            try
            {
                for (int i = 0; i < count; i++)
                {
                    // ストップがリクエストされていたら、次のスピンを開始せずに抜ける
                    if (ct.IsCancellationRequested) break;

                    // destroyToken を渡すことで、現在の 1 ゲームが完了するまでは中断されないようにする
                    bool shouldStopAutoSpin = await SpinOnceAsync(destroyToken);
                    
                    // ボーナス発動・フリースピン発動・コイン不足で自動停止
                    if (shouldStopAutoSpin || _currentPhase != GamePhase.Idle) break;
                }
            }
            catch (OperationCanceledException)
            {
                uiManager.HideFreeSpinHUD();
                uiManager.ClearLineHighlights();
                uiManager.ApplyModeVisual(ModeVisualType.Normal);
                TransitionTo(GamePhase.Idle);
            }
            finally
            {
                _isAutoSpinning = false;
                uiManager.SetAutoButtonText(GetAutoSpinButtonText());
                _autoSpinCts?.Dispose();
                _autoSpinCts = null;
            }
        }

        private async UniTask<bool> SpinOnceAsync(CancellationToken ct)
        {
            bool shouldStopAutoSpin = false;

            // コイン不足チェック
            if (!_gameState.DeductBet())
            {
                await HandleGameOver();
                return true;
            }

            uiManager.UpdateCoins(_gameState.Coins);
            uiManager.UpdateWin(0); // Reset win display
            uiManager.SetSpinButtonInteractable(false);
            audioManager.PlaySE(SEType.SpinStart);

            TransitionTo(GamePhase.Spinning);
            var result = await spinManager.ExecuteSpin(reelStrips, paylineData, payoutData, _gameState.BetAmount, ct, gameConfig);

            TransitionTo(GamePhase.Evaluating);

            // 通常スピンの配当を加算
            if (result.TotalWinAmount > 0 || result.HasBonusCondition || result.HasScatter)
            {
                if (result.TotalWinAmount > 0)
                {
                    _gameState.AddCoins(result.TotalWinAmount);
                    _gameState.RecordSpin(result.TotalWinAmount);
                    uiManager.UpdateCoins(_gameState.Coins);
                }
                else
                {
                    _gameState.RecordSpin(0);
                }

                TransitionTo(GamePhase.WinPresentation);
                if (result.TotalWinAmount > 0)
                {
                    uiManager.UpdateWin(result.TotalWinAmount);
                    PlayWinSe(result.TotalWinAmount);
                    await uiManager.ShowWinAmount(result.TotalWinAmount, CalcWinLevel(result.TotalWinAmount));
                }
                else if (result.HasScatter)
                {
                    audioManager.PlaySE(SEType.ScatterAppear);
                }

                uiManager.HighlightWinLines(result, paylineData);
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
                shouldStopAutoSpin = true;
                await HandleBonusRound(ct);
            }

            if (pendingFreeSpin)
            {
                shouldStopAutoSpin = true;
                await HandleFreeSpins(result.ScatterCount, ct);
            }

            uiManager.SetSpinButtonInteractable(true);
            TransitionTo(GamePhase.Idle);
            return shouldStopAutoSpin;
        }

        private async UniTask HandleBonusRound(CancellationToken ct)
        {
            await uiManager.ShowModeTransitionAsync(
                "BONUS ROUND",
                "PICK CHESTS",
                ModeVisualType.BonusRound,
                ct);

            TransitionTo(GamePhase.BonusRound);
            audioManager.PlaySE(SEType.BonusStart);
            await audioManager.FadeOutBGM(0.5f, ct);
            audioManager.PlayBGM(BGMType.BonusRound);
            uiManager.ApplyModeVisual(ModeVisualType.BonusRound);

            long win = await bonusManager.RunBonusRound(_gameState.BetAmount, payoutData, ct);
            _gameState.AddCoins(win);
            uiManager.UpdateCoins(_gameState.Coins);
            uiManager.UpdateWin(win);
            SaveGame();

            await audioManager.FadeOutBGM(0.5f, ct);
            audioManager.PlayBGM(BGMType.Normal);
            uiManager.ApplyModeVisual(ModeVisualType.Normal);
        }

        private async UniTask HandleFreeSpins(int scatterCount, CancellationToken ct)
        {
            int freeSpinCount = scatterCount switch
            {
                3 => 10,
                4 => 15,
                5 => 20,
                _ => 0
            };

            await uiManager.ShowModeTransitionAsync(
                "フリースピン",
                $"{freeSpinCount} フリースピン",
                ModeVisualType.FreeSpin,
                ct);

            TransitionTo(GamePhase.FreeSpin);
            audioManager.PlaySE(SEType.FreeSpinStart);
            await audioManager.FadeOutBGM(0.5f, ct);
            audioManager.PlayBGM(BGMType.FreeSpin);
            uiManager.ApplyModeVisual(ModeVisualType.FreeSpin);

            long cumulativeFreeSpinWin = 0;
            uiManager.ShowFreeSpinHUD(freeSpinCount, cumulativeFreeSpinWin);

            await bonusManager.RunFreeSpins(
                _gameState, freeSpinCount,
                reelStrips, paylineData, payoutData,
                async result =>
                {
                    cumulativeFreeSpinWin += result.TotalWinAmount * 2;
                    uiManager.UpdateCoins(_gameState.Coins);
                    uiManager.UpdateWin(result.TotalWinAmount * 2);
                    uiManager.ShowFreeSpinHUD(_gameState.FreeSpinsLeft, cumulativeFreeSpinWin);
                    if (result.TotalWinAmount > 0)
                    {
                        uiManager.HighlightWinLines(result, paylineData);
                        await uiManager.ShowWinAmount(result.TotalWinAmount * 2, CalcWinLevel(result.TotalWinAmount * 2));
                        await UniTask.Delay(TimeSpan.FromSeconds(1.5f), cancellationToken: ct);
                        uiManager.ClearLineHighlights();
                    }
                },
                ct);

            uiManager.HideFreeSpinHUD();
            SaveGame();

            await audioManager.FadeOutBGM(0.5f, ct);
            audioManager.PlayBGM(BGMType.Normal);
            uiManager.ApplyModeVisual(ModeVisualType.Normal);
        }

        private async UniTask HandleGameOver()
        {
            TransitionTo(GamePhase.GameOver);
            uiManager.ApplyModeVisual(ModeVisualType.Normal);
            // コインをデフォルト値にリセット
            _gameState.SetCoins(gameConfig.initialCoins);
            uiManager.UpdateCoins(_gameState.Coins);
            SaveGame();
            await UniTask.Delay(TimeSpan.FromSeconds(1f));
            TransitionTo(GamePhase.Idle);
        }

        // ─── ユーティリティ ──────────────────────────────────────────────

        private void TransitionTo(GamePhase next)
        {
            if (!CanTransitionTo(next))
            {
                Debug.LogWarning($"[GameManager] Invalid transition rejected: {_currentPhase} -> {next}");
                return;
            }

            _currentPhase = next;
        }

        private bool CanTransitionTo(GamePhase next)
        {
            if (_currentPhase == next)
            {
                return next == GamePhase.Idle;
            }

            return _currentPhase switch
            {
                GamePhase.Idle => next is GamePhase.Spinning or GamePhase.GameOver,
                GamePhase.Spinning => next == GamePhase.Evaluating,
                GamePhase.Evaluating => next is GamePhase.WinPresentation or GamePhase.BonusRound or GamePhase.FreeSpin or GamePhase.Idle,
                GamePhase.WinPresentation => next is GamePhase.BonusRound or GamePhase.FreeSpin or GamePhase.Idle,
                GamePhase.BonusRound => next is GamePhase.FreeSpin or GamePhase.Idle,
                GamePhase.FreeSpin => next == GamePhase.Idle,
                GamePhase.GameOver => next == GamePhase.Idle,
                _ => false
            };
        }

        private void SaveGame()
        {
            if (_saveDataManager == null || _gameState == null)
            {
                if (!_hasLoggedSaveSkip)
                {
                    Debug.LogWarning("[GameManager] Save skipped because initialization is not complete yet.");
                    _hasLoggedSaveSkip = true;
                }
                return;
            }

            _hasLoggedSaveSkip = false;
            _saveDataManager.Save(new SaveData
            {
                coins      = _gameState.Coins,
                betAmount  = _gameState.BetAmount,
                bgmVolume  = _bgmVolume,
                seVolume   = _seVolume,
                totalSpins = _gameState.TotalSpins,
                maxWin     = _gameState.MaxWin,
            });
        }

        private void HandleBgmVolumeChanged(float volume)
        {
            _bgmVolume = volume;
            audioManager.SetBGMVolume(volume);
            SaveGame();
        }

        private void HandleSeVolumeChanged(float volume)
        {
            _seVolume = volume;
            audioManager.SetSEVolume(volume);
            SaveGame();
        }

        private void HandleResetCoinsRequested()
        {
            _gameState.SetCoins(gameConfig.initialCoins);
            uiManager.UpdateCoins(_gameState.Coins);
            SaveGame();
        }

        private void HandleReelStopped(int reelIndex)
        {
            audioManager.PlaySE(SEType.ReelStop);
        }

        private SymbolData[] CollectSymbolDefinitions()
        {
            var symbols = new System.Collections.Generic.Dictionary<int, SymbolData>();
            foreach (var strip in reelStrips)
            {
                foreach (var symbol in strip.strip)
                {
                    symbols[symbol.symbolId] = symbol;
                }
            }

            var result = new SymbolData[symbols.Count];
            symbols.Values.CopyTo(result, 0);
            Array.Sort(result, (left, right) => left.symbolId.CompareTo(right.symbolId));
            return result;
        }

        private static WinLevel CalcWinLevel(long amount)
        {
            if (amount >= 5000) return WinLevel.Mega;
            if (amount >= 1000) return WinLevel.Big;
            return WinLevel.Small;
        }

        private void PlayWinSe(long amount)
        {
            var seType = CalcWinLevel(amount) switch
            {
                WinLevel.Mega => SEType.MegaWin,
                WinLevel.Big => SEType.BigWin,
                _ => SEType.SmallWin
            };
            audioManager.PlaySE(seType);
        }

        private string GetAutoSpinButtonText() => $"オート x{_autoSpinCount}";
    }
}
