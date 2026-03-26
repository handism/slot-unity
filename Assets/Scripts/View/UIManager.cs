#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using SlotGame.Data;
using SlotGame.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.View
{
    public enum ModeVisualType
    {
        Normal,
        FreeSpin,
        BonusRound
    }

    /// <summary>各 View パネルを統括する UIManager。</summary>
    public class UIManager : MonoBehaviour
    {
        private const int MaxPaylinePoolSize = 50;
        private const int SequentialLineDelayMs = 500;

        [SerializeField] private MainHUDView     mainHUD = null!;
        [SerializeField] private FreeSpinHUDView freeSpinHUD = null!;
        [SerializeField] private WinPopupView    winPopup = null!;
        [SerializeField] private SettingsView    settingsView = null!;
        [SerializeField] private PaytableView    paytableView = null!;
        [SerializeField] private StatsView       statsView    = null!;
        [SerializeField] private PaylineView     paylinePrefab = null!;
        [SerializeField] private Transform       paylineParent = null!;
        [Header("Debug/Fallback")]
        [SerializeField] private PaylineData     paylineData = null!;

        private List<PaylineView> _activePaylines = new();
        private Queue<PaylineView> _paylinePool = new();

        private ReelView[]? _reelViews;
        private Canvas? _rootCanvas;
        private Image? _modeTintOverlay;
        private CanvasGroup? _modeAnnouncementGroup;
        private RectTransform? _modeAnnouncementRoot;
        private TMP_Text? _modeTitleText;
        private TMP_Text? _modeSubtitleText;
        private Camera? _mainCamera;
        private CanvasGroup? _hudCanvasGroup;

        private static readonly Color NormalTint     = new(0.05f, 0.08f, 0.14f, 0f);
        private static readonly Color FreeSpinTint   = new(0.08f, 0.36f, 0.52f, 0.3f);
        private static readonly Color BonusRoundTint = new(0.42f, 0.16f, 0.06f, 0.34f);

        private static readonly Color NormalCameraColor     = new(0.05f, 0.08f, 0.14f, 1f);
        private static readonly Color FreeSpinCameraColor   = new(0.04f, 0.18f, 0.24f, 1f);
        private static readonly Color BonusRoundCameraColor = new(0.19f, 0.09f, 0.03f, 1f);

        public event System.Action<float>? BgmVolumeChanged;
        public event System.Action<float>? SeVolumeChanged;
        public event System.Action? ResetCoinsRequested;
        public event System.Action? SettingsCloseRequested;
        public event System.Action? PaytableCloseRequested;
        public event System.Action? StatsCloseRequested;

        private void Awake()
        {
            if (settingsView != null)
            {
                settingsView.OnBGMVolumeChanged += volume => BgmVolumeChanged?.Invoke(volume);
                settingsView.OnSEVolumeChanged += volume => SeVolumeChanged?.Invoke(volume);
                settingsView.OnResetCoinsRequested += () => ResetCoinsRequested?.Invoke();
                settingsView.OnCloseRequested += () => SettingsCloseRequested?.Invoke();
            }

            if (paytableView != null)
            {
                paytableView.OnCloseRequested += () => PaytableCloseRequested?.Invoke();
            }

            if (statsView != null)
            {
                statsView.OnCloseRequested += () => StatsCloseRequested?.Invoke();
            }
        }

        private void Start()
        {
            CacheReelViews();
            EnsureModeVisuals();
        }

        public void UpdateCoins(long coins)    => mainHUD.SetCoins(coins);
        public void UpdateBet(int bet)         => mainHUD.SetBet(bet);
        public void UpdateWin(long amount)     => mainHUD.SetWin(amount);
        public void SetSpinButtonInteractable(bool interactable) => mainHUD.SetSpinInteractable(interactable);
        public void SetAutoButtonText(string text) => mainHUD.SetAutoButtonText(text);

        public async UniTask ShowWinAmount(long amount, WinLevel level)
            => await winPopup.Show(amount, level, this.GetCancellationTokenOnDestroy());

        public void SetupReels(IEnumerable<ReelView> reels)
        {
            _reelViews = reels.ToArray();
        }

        /// <summary>
        /// 当選ペイラインと特殊シンボルのハイライトを表示する。
        /// 複数ライン当選時は順番に表示してから全点灯させる。
        /// </summary>
        public async UniTask HighlightWinLinesAsync(SpinResult result, CancellationToken ct, PaylineData? overridePaylineData = null)
        {
            var currentPaylineData = overridePaylineData != null ? overridePaylineData : paylineData;

            if (_reelViews == null || _reelViews.Length == 0)
            {
                CacheReelViews();
            }
            if (_reelViews == null || _reelViews.Length == 0) return;

            ClearLineHighlights();

            try
            {
                // 1. 各当選ラインを順番に表示
                if (result.LineWins.Count > 1)
                {
                    foreach (var win in result.LineWins)
                    {
                        await ShowSingleLineWinAsync(win, currentPaylineData, ct);
                        await UniTask.Delay(SequentialLineDelayMs, cancellationToken: ct);
                        ClearLineHighlights();
                    }
                }
                else if (result.LineWins.Count == 1)
                {
                    await ShowSingleLineWinAsync(result.LineWins[0], currentPaylineData, ct);
                    await UniTask.Delay(SequentialLineDelayMs, cancellationToken: ct);
                }

                // 2. 全点灯（最終状態）
                ShowAllWins(result, currentPaylineData);
            }
            finally
            {
                // ここで ClearLineHighlights を呼ぶと最終状態も消えてしまうため、
                // キャンセル時のみクリアしたい場合は例外をキャッチして処理する。
                // ただし、GameManager 側でスピン開始時にクリアされるため、ここでは何もしないか、
                // ct.IsCancellationRequested をチェックする。
                if (ct.IsCancellationRequested)
                {
                    ClearLineHighlights();
                }
            }
        }

        private async UniTask ShowSingleLineWinAsync(LineWin win, PaylineData currentPaylineData, CancellationToken ct)
        {
            if (_reelViews == null) return;
            if (win.LineIndex < 0 || win.LineIndex >= currentPaylineData.lines.Length) return;

            var lineDef = currentPaylineData.lines[win.LineIndex];
            var points = new Vector3[win.MatchCount];
            for (int i = 0; i < win.MatchCount; i++)
            {
                if (i >= lineDef.rows.Length) break;
                points[i] = _reelViews[i].GetSymbolWorldPosition(lineDef.rows[i]);
            }

            // 非当選シンボルを暗くし、当選シンボルをパルスさせる
            for (int r = 0; r < _reelViews.Length; r++)
            {
                var highlightedRows = new HashSet<int>();
                if (r < win.MatchCount && r < lineDef.rows.Length)
                {
                    highlightedRows.Add(lineDef.rows[r]);
                    _reelViews[r].PlayWinAnimation(lineDef.rows[r], ct).Forget();
                    var symbol = _reelViews[r].GetSymbolView(lineDef.rows[r]);
                    symbol?.PlayPulseAnimation();
                }
                _reelViews[r].HighlightRows(highlightedRows);
            }

            var lineView = GetPaylineView();
            _activePaylines.Add(lineView);
            await lineView.AnimateDrawAsync(points, GetLineColor(win.LineIndex), ct);
        }

        private void ShowAllWins(SpinResult result, PaylineData currentPaylineData)
        {
            if (_reelViews == null) return;
            var destroyCt = this.GetCancellationTokenOnDestroy();
            var highlightedRowsByReel = new Dictionary<int, HashSet<int>>();

            // ペイライン描画
            foreach (var win in result.LineWins)
            {
                if (win.LineIndex < 0 || win.LineIndex >= currentPaylineData.lines.Length) continue;

                var lineDef = currentPaylineData.lines[win.LineIndex];
                var points = new Vector3[win.MatchCount];
                for (int i = 0; i < win.MatchCount; i++)
                {
                    if (i >= lineDef.rows.Length) break;
                    points[i] = _reelViews[i].GetSymbolWorldPosition(lineDef.rows[i]);
                    AddHighlight(highlightedRowsByReel, i, lineDef.rows[i]);
                }

                var lineView = GetPaylineView();
                lineView.DrawLine(points, GetLineColor(win.LineIndex));
                _activePaylines.Add(lineView);
            }

            // Scatter / Bonus ハイライト
            if (result.HasScatter)
            {
                foreach (var pos in result.ScatterPositions)
                    AddHighlight(highlightedRowsByReel, pos.Reel, pos.Row);
            }
            if (result.HasBonusCondition)
            {
                foreach (var pos in result.BonusPositions)
                    AddHighlight(highlightedRowsByReel, pos.Reel, pos.Row);
            }

            // 表示反映
            for (int i = 0; i < _reelViews.Length; i++)
            {
                highlightedRowsByReel.TryGetValue(i, out var rows);
                _reelViews[i].HighlightRows(rows);

                if (rows != null)
                {
                    foreach (int row in rows)
                    {
                        _reelViews[i].PlayWinAnimation(row, destroyCt).Forget();
                        var symbol = _reelViews[i].GetSymbolView(row);
                        symbol?.PlayPulseAnimation();
                    }
                }
            }
        }

        private void AddHighlight(Dictionary<int, HashSet<int>> dict, int reel, int row)
        {
            if (!dict.TryGetValue(reel, out var rows))
            {
                rows = new HashSet<int>();
                dict.Add(reel, rows);
            }
            rows.Add(row);
        }

        private PaylineView GetPaylineView()
        {
            if (_paylinePool.Count > 0)
            {
                var view = _paylinePool.Dequeue();
                view.gameObject.SetActive(true);
                return view;
            }

            return Instantiate(paylinePrefab, paylineParent != null ? paylineParent : transform);
        }

        private void ClearPaylines()
        {
            foreach (var pl in _activePaylines)
            {
                if (pl != null)
                {
                    pl.Clear();
                    pl.gameObject.SetActive(false);
                    if (_paylinePool.Count < MaxPaylinePoolSize)
                    {
                        _paylinePool.Enqueue(pl);
                    }
                    else
                    {
                        Destroy(pl.gameObject);
                    }
                }
            }
            _activePaylines.Clear();
        }

        private Color GetLineColor(int index)
        {
            float hue = (index * 0.15f) % 1f;
            return Color.HSVToRGB(hue, 0.8f, 1f);
        }

        public void ClearLineHighlights()
        {
            ClearPaylines();
            if (_reelViews == null || _reelViews.Length == 0)
            {
                CacheReelViews();
            }
            if (_reelViews == null) return;

            foreach (var reelView in _reelViews)
            {
                reelView.ClearHighlights();
            }
        }

        public void ShowFreeSpinHUD(int remaining, long totalWin)
        {
            if (freeSpinHUD == null) return;
            freeSpinHUD.gameObject.SetActive(true);
            freeSpinHUD.UpdateDisplay(remaining, totalWin);
        }

        public void HideFreeSpinHUD() { if (freeSpinHUD != null) freeSpinHUD.gameObject.SetActive(false); }

        public void ApplyModeVisual(ModeVisualType mode)
        {
            EnsureModeVisuals();

            if (_modeTintOverlay != null)
            {
                _modeTintOverlay.color = mode switch
                {
                    ModeVisualType.FreeSpin   => FreeSpinTint,
                    ModeVisualType.BonusRound => BonusRoundTint,
                    _                         => NormalTint,
                };
            }

            _mainCamera ??= Camera.main;
            if (_mainCamera != null)
            {
                _mainCamera.backgroundColor = mode switch
                {
                    ModeVisualType.FreeSpin   => FreeSpinCameraColor,
                    ModeVisualType.BonusRound => BonusRoundCameraColor,
                    _                         => NormalCameraColor,
                };
            }
        }

        public async UniTask ShowModeTransitionAsync(
            string title,
            string subtitle,
            ModeVisualType mode,
            CancellationToken cancellationToken)
        {
            EnsureModeVisuals();
            ApplyModeVisual(mode);

            if (_modeAnnouncementGroup == null || _modeTitleText == null || _modeSubtitleText == null)
            {
                return;
            }

            _modeTitleText.text = title;
            _modeSubtitleText.text = subtitle;
            _modeAnnouncementGroup.alpha = 0f;
            _modeAnnouncementGroup.gameObject.SetActive(true);

            try
            {
                await DOTween.To(
                        () => _modeAnnouncementGroup.alpha,
                        value => _modeAnnouncementGroup.alpha = value,
                        1f,
                        0.2f)
                    .SetEase(Ease.OutQuad)
                    .ToUniTask(cancellationToken: cancellationToken);
                await UniTask.Delay(900, cancellationToken: cancellationToken);
                await DOTween.To(
                        () => _modeAnnouncementGroup.alpha,
                        value => _modeAnnouncementGroup.alpha = value,
                        0f,
                        0.25f)
                    .SetEase(Ease.InQuad)
                    .ToUniTask(cancellationToken: cancellationToken);
            }
            finally
            {
                _modeAnnouncementGroup.alpha = 0f;
                _modeAnnouncementGroup.gameObject.SetActive(false);
            }
        }

        public void ShowSettings()
        {
            SetHudInteractable(false);
            settingsView.ShowAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        public void HideSettings() => HideSettingsAsync(this.GetCancellationTokenOnDestroy()).Forget();

        public void ShowPaytable()
        {
            SetHudInteractable(false);
            paytableView.ShowAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        public void HidePaytable() => HidePaytableAsync(this.GetCancellationTokenOnDestroy()).Forget();

        public void ShowStats()
        {
            if (statsView == null) return;
            SetHudInteractable(false);
            statsView.ShowAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        public void HideStats() => HideStatsAsync(this.GetCancellationTokenOnDestroy()).Forget();

        public void UpdateStats(in SlotGame.Model.SessionStats stats) => statsView?.UpdateDisplay(stats);

        private async UniTaskVoid HideSettingsAsync(CancellationToken ct)
        {
            await settingsView.HideAsync(ct);
            SetHudInteractable(true);
        }

        private async UniTaskVoid HidePaytableAsync(CancellationToken ct)
        {
            await paytableView.HideAsync(ct);
            SetHudInteractable(true);
        }

        private async UniTaskVoid HideStatsAsync(CancellationToken ct)
        {
            if (statsView != null) await statsView.HideAsync(ct);
            SetHudInteractable(true);
        }

        public void SetSettingsVolumes(float bgm, float se)
        {
            settingsView?.SetVolumes(bgm, se);
        }

        public void PopulatePaytable(SymbolData[] symbols, PayoutTableData payoutData)
        {
            paytableView?.Populate(symbols, payoutData);
        }

        private void CacheReelViews()
        {
            if (_reelViews != null && _reelViews.Length == 5) return;

            var views = FindObjectsByType<ReelView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (views.Length == 0) return;

            _reelViews = views
                .OrderBy(v => v.transform.position.x)
                .ToArray();
        }

        private void SetHudInteractable(bool interactable)
        {
            if (_hudCanvasGroup == null)
            {
                var hudCanvas = mainHUD != null ? mainHUD.GetComponentInParent<Canvas>() : null;
                if (hudCanvas != null)
                {
                    var cg = hudCanvas.GetComponent<CanvasGroup>();
                    _hudCanvasGroup = cg != null ? cg : hudCanvas.gameObject.AddComponent<CanvasGroup>();
                }
            }
            if (_hudCanvasGroup != null)
                _hudCanvasGroup.interactable = interactable;
        }

        private void EnsureModeVisuals()
        {
            if (_modeTintOverlay != null && _modeAnnouncementGroup != null)
            {
                return;
            }

            _rootCanvas ??= mainHUD != null ? mainHUD.GetComponentInParent<Canvas>() : FindFirstObjectByType<Canvas>();
            if (_rootCanvas == null)
            {
                return;
            }

            if (_modeTintOverlay == null)
            {
                var tintObject = new GameObject("ModeTintOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                tintObject.transform.SetParent(_rootCanvas.transform, false);
                tintObject.transform.SetAsFirstSibling();

                var rect = tintObject.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                _modeTintOverlay = tintObject.GetComponent<Image>();
                _modeTintOverlay.raycastTarget = false;
                _modeTintOverlay.color = NormalTint;
            }

            if (_modeAnnouncementGroup == null)
            {
                var overlayObject = new GameObject("ModeAnnouncementOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
                overlayObject.transform.SetParent(_rootCanvas.transform, false);
                overlayObject.transform.SetAsLastSibling();

                _modeAnnouncementRoot = overlayObject.GetComponent<RectTransform>();
                _modeAnnouncementRoot.anchorMin = Vector2.zero;
                _modeAnnouncementRoot.anchorMax = Vector2.one;
                _modeAnnouncementRoot.offsetMin = Vector2.zero;
                _modeAnnouncementRoot.offsetMax = Vector2.zero;

                var overlayImage = overlayObject.GetComponent<Image>();
                overlayImage.color = new Color(0f, 0f, 0f, 0.45f);
                overlayImage.raycastTarget = false;

                _modeAnnouncementGroup = overlayObject.GetComponent<CanvasGroup>();
                _modeAnnouncementGroup.alpha = 0f;
                _modeAnnouncementGroup.interactable = false;
                _modeAnnouncementGroup.blocksRaycasts = false;

                CreateAnnouncementTexts(overlayObject.transform);
                overlayObject.SetActive(false);
            }
        }

        private void CreateAnnouncementTexts(Transform parent)
        {
            var titleObject = CreateTextObject("ModeTitle", parent, 56, FontStyles.Bold);
            var subtitleObject = CreateTextObject("ModeSubtitle", parent, 28, FontStyles.Normal);

            var titleRect = titleObject.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, 28f);
            titleRect.sizeDelta = new Vector2(960f, 80f);

            var subtitleRect = subtitleObject.rectTransform;
            subtitleRect.anchorMin = new Vector2(0.5f, 0.5f);
            subtitleRect.anchorMax = new Vector2(0.5f, 0.5f);
            subtitleRect.anchoredPosition = new Vector2(0f, -28f);
            subtitleRect.sizeDelta = new Vector2(960f, 60f);

            titleObject.alignment = TextAlignmentOptions.Center;
            subtitleObject.alignment = TextAlignmentOptions.Center;
            titleObject.color = new Color(1f, 0.96f, 0.8f, 1f);
            subtitleObject.color = new Color(0.9f, 0.96f, 1f, 1f);

            _modeTitleText = titleObject;
            _modeSubtitleText = subtitleObject;
        }

        private static TMP_Text CreateTextObject(string name, Transform parent, float fontSize, FontStyles fontStyle)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            if (text.font == null)
            {
                Debug.LogError($"TMP default font asset is missing for {name}.");
                return text;
            }
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.text = string.Empty;

            return text;
        }
    }
}
