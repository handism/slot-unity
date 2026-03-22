using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using SlotGame.Data;
using SlotGame.Model;
using UnityEngine;

namespace SlotGame.View
{
    /// <summary>各 View パネルを統括する UIManager。</summary>
    public class UIManager : MonoBehaviour
    {
        [SerializeField] private MainHUDView     mainHUD;
        [SerializeField] private FreeSpinHUDView freeSpinHUD;
        [SerializeField] private WinPopupView    winPopup;
        [SerializeField] private SettingsView    settingsView;
        [SerializeField] private PaytableView    paytableView;
        [SerializeField] private PaylineView     paylinePrefab;
        [SerializeField] private Transform       paylineParent;
        [SerializeField] private PaylineData     paylineData;

        private List<PaylineView> _activePaylines = new();

        private ReelView[] _reelViews;

        public event System.Action<float> BgmVolumeChanged;
        public event System.Action<float> SeVolumeChanged;
        public event System.Action ResetCoinsRequested;
        public event System.Action SettingsCloseRequested;
        public event System.Action PaytableCloseRequested;

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
        }

        private void Start()
        {
            CacheReelViews();
        }

        public void UpdateCoins(long coins)    => mainHUD.SetCoins(coins);
        public void UpdateBet(int bet)         => mainHUD.SetBet(bet);
        public void SetSpinButtonInteractable(bool interactable) => mainHUD.SetSpinInteractable(interactable);
        public void SetAutoButtonText(string text) => mainHUD.SetAutoButtonText(text);

        public async UniTask ShowWinAmount(long amount, WinLevel level)
            => await winPopup.Show(amount, level, this.GetCancellationTokenOnDestroy());

        public void HighlightWinLines(SpinResult result)
        {
            CacheReelViews();
            if (_reelViews == null || _reelViews.Length == 0) return;

            var highlightedRowsByReel = new Dictionary<int, HashSet<int>>();
            var ct = this.GetCancellationTokenOnDestroy();

            // --- ペイライン描画 ---
            ClearPaylines();

            if (paylineData != null && paylinePrefab != null)
            {
                foreach (var win in result.LineWins)
                {
                    if (win.LineIndex < 0 || win.LineIndex >= paylineData.lines.Length) continue;

                    var lineDef = paylineData.lines[win.LineIndex];
                    var points = new Vector3[win.MatchCount];
                    for (int i = 0; i < win.MatchCount; i++)
                    {
                        int row = lineDef.rows[i];
                        points[i] = _reelViews[i].GetSymbolWorldPosition(row);
                    }

                    var lineView = Instantiate(paylinePrefab, paylineParent != null ? paylineParent : transform);
                    lineView.DrawLine(points, GetLineColor(win.LineIndex));
                    _activePaylines.Add(lineView);

                    // シンボルハイライト追加
                    for (int i = 0; i < win.MatchCount; i++)
                    {
                        AddHighlight(highlightedRowsByReel, i, lineDef.rows[i]);
                    }
                }
            }

            // --- Scatter / Bonus ハイライト追加 ---
            foreach (var pos in result.ScatterPositions)
            {
                AddHighlight(highlightedRowsByReel, pos.Reel, pos.Row);
            }
            foreach (var pos in result.BonusPositions)
            {
                AddHighlight(highlightedRowsByReel, pos.Reel, pos.Row);
            }

            // --- 表示反映 & アニメーション開始 ---
            for (int reelIndex = 0; reelIndex < _reelViews.Length; reelIndex++)
            {
                highlightedRowsByReel.TryGetValue(reelIndex, out var rows);
                _reelViews[reelIndex].HighlightRows(rows);

                if (rows != null)
                {
                    foreach (int row in rows)
                    {
                        _reelViews[reelIndex].PlayWinAnimation(row, ct).Forget();
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

        private void ClearPaylines()
        {
            foreach (var pl in _activePaylines)
            {
                if (pl != null) Destroy(pl.gameObject);
            }
            _activePaylines.Clear();
        }

        private Color GetLineColor(int index)
        {
            // インデックスごとに異なる色を割り当てる（視認性向上）
            float hue = (index * 0.15f) % 1f;
            return Color.HSVToRGB(hue, 0.8f, 1f);
        }

        public void ClearLineHighlights()
        {
            ClearPaylines();
            CacheReelViews();
            if (_reelViews == null) return;

            foreach (var reelView in _reelViews)
            {
                reelView.ClearHighlights();
            }
        }

        public void ShowFreeSpinHUD(int remaining, long totalWin)
        {
            freeSpinHUD.gameObject.SetActive(true);
            freeSpinHUD.UpdateDisplay(remaining, totalWin);
        }

        public void HideFreeSpinHUD() => freeSpinHUD.gameObject.SetActive(false);

        public void ShowSettings()  => settingsView.ShowAsync(this.GetCancellationTokenOnDestroy()).Forget();
        public void HideSettings()  => settingsView.HideAsync(this.GetCancellationTokenOnDestroy()).Forget();
        public void ShowPaytable()  => paytableView.ShowAsync(this.GetCancellationTokenOnDestroy()).Forget();
        public void HidePaytable()  => paytableView.HideAsync(this.GetCancellationTokenOnDestroy()).Forget();

        public void SetSettingsVolumes(float bgm, float se)
        {
            settingsView?.SetVolumes(bgm, se);
        }

        public void PopulatePaytable(SymbolData[] symbols)
        {
            paytableView?.Populate(symbols);
        }

        private void CacheReelViews()
        {
            if (_reelViews != null && _reelViews.Length > 0) return;

            _reelViews = FindObjectsByType<ReelView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            System.Array.Sort(_reelViews, CompareReelsByXPosition);
        }

        private static int CompareReelsByXPosition(ReelView left, ReelView right)
        {
            return left.transform.position.x.CompareTo(right.transform.position.x);
        }
    }
}
