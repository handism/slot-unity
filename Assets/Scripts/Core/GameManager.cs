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
        private static readonly int[] DefaultBetAmounts = { 10, 20, 50, 100 };

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
        private bool                   _isInitialized;
        private SlotConfig             _config;
        private bool                   _isPaytableOpen;

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
                var config = ResolveSlotConfig();
                _saveDataManager = new SaveDataManager(config);
                var save = _saveDataManager.Load();
                _gameState = new GameState(
                    config.InitialCoins,
                    config.MaxCoins,
                    config.ValidBetAmounts,
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
            if (spinManager == null || bonusManager == null || uiManager == null || audioManager == null)
            {
                Debug.LogError("[GameManager] Manager references are not fully set in Inspector.");
                return;
            }

            if (reelStrips == null || reelStrips.Length == 0 || paylineData == null || payoutData == null)
            {
                Debug.LogError("[GameManager] Data references are not fully set in Inspector.");
                return;
            }

            var config = ResolveSlotConfig();
            _config = config;

            _saveDataManager = saveDataManager ?? new SaveDataManager(config);
            save ??= _saveDataManager.Load();
            _gameState = gameState ?? new GameState(
                config.InitialCoins,
                config.MaxCoins,
                config.ValidBetAmounts,
                save.coins,
                save.betAmount
            );
            random ??= new SystemRandomGenerator();

            spinManager.Initialize(random, reelStrips);
            bonusManager.Initialize(random, config);

            // UIManager に使用するリールを明示的にセット
            uiManager.SetupReels(spinManager.Reels.Select(r => r.GetComponent<ReelView>()));

            _bgmVolume = save.bgmVolume;
            _seVolume  = save.seVolume;
            _autoSpinCount = config.DefaultAutoSpinCount;

            // セーブデータが完全に新規（一度も保存されていない）場合のみ、Config のデフォルト値を優先する。
            // チェックサムがない、または totalSpins が 0 かつ default 値と一致する場合は新規とみなす。
            if (string.IsNullOrEmpty(save.checksum) && save.totalSpins == 0)
            {
                _bgmVolume = config.DefaultBgmVolume;
                _seVolume  = config.DefaultSeVolume;
            }

            audioManager.SetBGMVolume(_bgmVolume);
            audioManager.SetSEVolume(_seVolume);
            audioManager.PlayBGM(BGMType.Normal);
            _isInitialized = true;
        }

        private void Start()
        {
            if (!_isInitialized || _gameState == null)
            {
                Debug.LogError("[GameManager] Initialization did not complete. Start was skipped.");
                return;
            }

            uiManager.UpdateCoins(_gameState.Coins);
            uiManager.UpdateBet(_gameState.BetAmount);
            uiManager.UpdateWin(0);
            uiManager.SetAutoButtonText(GetAutoSpinButtonText());
            uiManager.SetSettingsVolumes(_bgmVolume, _seVolume);
            uiManager.PopulatePaytable(CollectSymbolDefinitions(), payoutData);
            uiManager.SetGameDescriptionText(
                "【ゲームルール】\n" +
                "・25ペイラインの固定ラインスロットです。\n" +
                "・左端から3つ以上の同一シンボルが並ぶと配当獲得！\n" +
                "・WILDは全シンボルの代用となります（Scatter/Bonus除く）。\n\n" +
                "【フリースピン】\n" +
                "・SCATTER（魔法陣）が3つ以上出現で発動！\n" +
                "・フリースピン中の配当は2倍にアップします。\n\n" +
                "【ボーナスラウンド】\n" +
                "・リール1・3・5すべてにBONUS（宝箱）が出現で発動！\n" +
                "・宝箱を選んでランダムなコイン報酬を獲得できます。"
            );
            uiManager.ApplyModeVisual(ModeVisualType.Normal);
            uiManager.BgmVolumeChanged += HandleBgmVolumeChanged;
            uiManager.SeVolumeChanged += HandleSeVolumeChanged;
            uiManager.ResetCoinsRequested += HandleResetCoinsRequested;
            uiManager.SettingsCloseRequested += uiManager.HideSettings;
            uiManager.PaytableCloseRequested += () => { uiManager.HidePaytable(); _isPaytableOpen = false; };
            uiManager.StatsCloseRequested    += uiManager.HideStats;
            uiManager.GameDescriptionCloseRequested += uiManager.HideGameDescription;
            uiManager.AutoSpinRequested      += OnAutoSpinButtonPressed;
            uiManager.AutoSpinStopRequested  += OnAutoSpinStopRequested;
            uiManager.TurboToggled           += OnTurboToggled;
            spinManager.ReelStopped += HandleReelStopped;
            TransitionTo(GamePhase.Idle);
        }

        private SlotConfig ResolveSlotConfig()
        {
            int fsMultiplier = payoutData != null ? payoutData.freeSpinMultiplier : 2;

            if (gameConfig != null)
                return gameConfig.ToModelConfig(fsMultiplier);

            Debug.LogWarning("[GameManager] gameConfig is not set in Inspector. Falling back to default config.");
            return new SlotConfig(
                1000,
                9_999_999,
                DefaultBetAmounts,
                5,
                3,
                3,
                new[] { 0, 2, 4 },
                fsMultiplier,
                20,
                10,
                0.8f,
                1.0f,
                "SALTY_SLOT_2026",
                0.8f,
                0.1f);
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
                uiManager.StatsCloseRequested    -= uiManager.HideStats;
                uiManager.GameDescriptionCloseRequested -= uiManager.HideGameDescription;
                uiManager.AutoSpinRequested      -= OnAutoSpinButtonPressed;
                uiManager.AutoSpinStopRequested  -= OnAutoSpinStopRequested;
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

        public void OnTurboToggled(bool enabled)
        {
            _gameState.SetTurbo(enabled);
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
            _isPaytableOpen = true;
            uiManager.ShowPaytable();
        }

        public void OnStatsButtonPressed()
        {
            uiManager.ShowStats();
        }

        public void ToggleMute()
        {
            audioManager.ToggleMute();
            // Mute状態のときはスライダーを0に見せかけるか、AudioManager側で制御。
            // ここではセーブデータの更新のみ行う
            SaveGame();
        }

        public void IncreaseBet()
        {
            if (_currentPhase != GamePhase.Idle) return;
            int currentIndex = Array.IndexOf(_config.ValidBetAmounts, _gameState.BetAmount);
            if (currentIndex >= 0 && currentIndex < _config.ValidBetAmounts.Length - 1)
            {
                OnBetChanged(_config.ValidBetAmounts[currentIndex + 1]);
            }
        }

        public void DecreaseBet()
        {
            if (_currentPhase != GamePhase.Idle) return;
            int currentIndex = Array.IndexOf(_config.ValidBetAmounts, _gameState.BetAmount);
            if (currentIndex > 0)
            {
                OnBetChanged(_config.ValidBetAmounts[currentIndex - 1]);
            }
        }

        public void ToggleAutoSpin()
        {
            if (_isAutoSpinning)
            {
                OnAutoSpinStopRequested();
            }
            else
            {
                if (_currentPhase != GamePhase.Idle) return;
                OnAutoSpinButtonPressed(_autoSpinCount);
            }
        }

        public void ToggleTurbo()
        {
            bool newState = !_gameState.IsTurbo;
            OnTurboToggled(newState);
            uiManager.SetTurbo(newState);
        }

        public void TogglePaytable()
        {
            _isPaytableOpen = !_isPaytableOpen;
            if (_isPaytableOpen) uiManager.ShowPaytable();
            else uiManager.HidePaytable();
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
                uiManager.SetSpinButtonMode(false);
                uiManager.SetSpinButtonInteractable(true);
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
            uiManager.SetAutoSpinCountInteractable(false);

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
                uiManager.SetSpinButtonMode(false);
                uiManager.SetSpinButtonInteractable(true);
                uiManager.SetAutoSpinCountInteractable(true);
                TransitionTo(GamePhase.Idle);
            }
            finally
            {
                _isAutoSpinning = false;
                uiManager.SetAutoButtonText(GetAutoSpinButtonText());
                uiManager.SetAutoSpinCountInteractable(true);
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
            uiManager.SetSpinButtonMode(true); // "STOP" に変更
            uiManager.SetAutoSpinCountInteractable(false);
            audioManager.PlaySE(SEType.SpinStart);

            TransitionTo(GamePhase.Spinning);
            var result = await spinManager.ExecuteSpin(
                reelStrips, paylineData, payoutData, _gameState.BetAmount, ct,
                _config.ReelCount, _config.RowCount, _config.MinMatch,
                _config.BonusTriggerReels); // ボーナストリガーリール

            uiManager.SetSpinButtonMode(false); // "SPIN" に戻す
            uiManager.SetSpinButtonInteractable(false); // 評価中は操作不可
            uiManager.SetAutoSpinCountInteractable(false);
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
                    var winLevel = CalcWinLevel(result.TotalWinAmount);
                    PlayWinSe(result.TotalWinAmount);

                    // BigWin以上はBGMをフェードアウトしてファンファーレSEを際立たせる
                    if (winLevel >= WinLevel.Big)
                    {
                        await audioManager.FadeOutBGM(0.3f, ct);
                    }

                    await uiManager.ShowWinAndHighlightAsync(result.TotalWinAmount, winLevel, result, ct, paylineData);

                    // BigWin演出後はNormal BGMへクロスフェードで復帰
                    if (winLevel >= WinLevel.Big)
                    {
                        await audioManager.CrossFadeBGM(BGMType.Normal, 0.5f, ct);
                    }
                }
                else
                {
                    if (result.HasScatter)
                    {
                        audioManager.PlaySE(SEType.ScatterAppear);
                    }
                    // Scatter または BonusCondition がある場合のハイライト（配当なしでも表示）
                    await uiManager.HighlightWinLinesAsync(result, ct, paylineData);
                }

                await UniTask.Delay(TimeSpan.FromSeconds(0.5f), cancellationToken: ct);
                uiManager.ClearLineHighlights();
            }
            else
            {
                _gameState.RecordSpin(0);
            }

            SaveGame();
            uiManager.UpdateStats(_gameState.GetSessionStats());

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
            if (!_isAutoSpinning) uiManager.SetAutoSpinCountInteractable(true);
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
            await audioManager.CrossFadeBGM(BGMType.BonusRound, 0.5f, ct);
            uiManager.ApplyModeVisual(ModeVisualType.BonusRound);

            long win = await bonusManager.RunBonusRound(_gameState.BetAmount, payoutData, ct);
            _gameState.AddCoins(win);
            uiManager.UpdateCoins(_gameState.Coins);
            uiManager.UpdateWin(win);
            SaveGame();
            uiManager.UpdateStats(_gameState.GetSessionStats());

            await audioManager.CrossFadeBGM(BGMType.Normal, 0.5f, ct);
            uiManager.ApplyModeVisual(ModeVisualType.Normal);
        }

        private async UniTask HandleFreeSpins(int scatterCount, CancellationToken ct)
        {
            _gameState.RecordFreeSpinTrigger();

            int freeSpinCount = PaylineEvaluator.CalculateFreeSpinCount(scatterCount, payoutData);

            await uiManager.ShowModeTransitionAsync(
                "フリースピン",
                $"{freeSpinCount} フリースピン",
                ModeVisualType.FreeSpin,
                ct);

            TransitionTo(GamePhase.FreeSpin);
            audioManager.PlaySE(SEType.FreeSpinStart);
            await audioManager.CrossFadeBGM(BGMType.FreeSpin, 0.5f, ct);
            uiManager.ApplyModeVisual(ModeVisualType.FreeSpin);

            long cumulativeFreeSpinWin = 0;
            uiManager.ShowFreeSpinHUD(freeSpinCount, cumulativeFreeSpinWin);

            await bonusManager.RunFreeSpins(
                _gameState, freeSpinCount,
                reelStrips, paylineData, payoutData,
                async result =>
                {
                    int multiplier = payoutData != null ? payoutData.freeSpinMultiplier : 2;
                    long win = result.TotalWinAmount * multiplier;
                    cumulativeFreeSpinWin += win;
                    uiManager.UpdateCoins(_gameState.Coins);
                    uiManager.UpdateWin(win);
                    uiManager.ShowFreeSpinHUD(_gameState.FreeSpinsLeft, cumulativeFreeSpinWin);
                    if (result.TotalWinAmount > 0)
                    {
                        await uiManager.ShowWinAndHighlightAsync(win, CalcWinLevel(win), result, ct, paylineData);
                        await UniTask.Delay(TimeSpan.FromSeconds(0.5f), cancellationToken: ct);
                        uiManager.ClearLineHighlights();
                    }
                },
                ct);

            uiManager.HideFreeSpinHUD();
            SaveGame();
            uiManager.UpdateStats(_gameState.GetSessionStats());

            await audioManager.CrossFadeBGM(BGMType.Normal, 0.5f, ct);
            uiManager.ApplyModeVisual(ModeVisualType.Normal);
        }

        private async UniTask HandleGameOver()
        {
            TransitionTo(GamePhase.GameOver);
            uiManager.ApplyModeVisual(ModeVisualType.Normal);
            // コインをデフォルト値にリセット
            _gameState.SetCoins(_config.InitialCoins);
            uiManager.UpdateCoins(_gameState.Coins);
            SaveGame();
            await UniTask.Delay(1000);
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
            _gameState.SetCoins(_config.InitialCoins);
            uiManager.UpdateCoins(_gameState.Coins);

            // 音量もデフォルトにリセット
            _bgmVolume = _config.DefaultBgmVolume;
            _seVolume = _config.DefaultSeVolume;
            audioManager.SetBGMVolume(_bgmVolume);
            audioManager.SetSEVolume(_seVolume);
            uiManager.SetSettingsVolumes(_bgmVolume, _seVolume);

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
