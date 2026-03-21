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

        public async UniTask ShowWinAmount(long amount, WinLevel level)
            => await winPopup.Show(amount, level, this.GetCancellationTokenOnDestroy());

        public void HighlightWinLines(IReadOnlyList<LineWin> wins)
        {
            CacheReelViews();
            if (_reelViews == null || _reelViews.Length == 0) return;

            var highlightedRowsByReel = new Dictionary<int, HashSet<int>>();
            foreach (var win in wins)
            {
                for (int reelIndex = 0; reelIndex < win.MatchCount && reelIndex < _reelViews.Length; reelIndex++)
                {
                    if (!highlightedRowsByReel.TryGetValue(reelIndex, out var rows))
                    {
                        rows = new HashSet<int>();
                        highlightedRowsByReel.Add(reelIndex, rows);
                    }

                    int row = win.LineIndex >= 0 && win.LineIndex < 25
                        ? ResolvePaylineRow(win.LineIndex, reelIndex)
                        : 1;
                    rows.Add(row);
                }
            }

            for (int reelIndex = 0; reelIndex < _reelViews.Length; reelIndex++)
            {
                highlightedRowsByReel.TryGetValue(reelIndex, out var rows);
                _reelViews[reelIndex].HighlightRows(rows);
            }
        }

        public void ClearLineHighlights()
        {
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

        public void ShowSettings()  => settingsView.gameObject.SetActive(true);
        public void HideSettings()  => settingsView.gameObject.SetActive(false);
        public void ShowPaytable()  => paytableView.gameObject.SetActive(true);
        public void HidePaytable()  => paytableView.gameObject.SetActive(false);

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

        private static int ResolvePaylineRow(int lineIndex, int reelIndex)
        {
            int[][] lineRows =
            {
                new[] { 1, 1, 1, 1, 1 },
                new[] { 0, 0, 0, 0, 0 },
                new[] { 2, 2, 2, 2, 2 },
                new[] { 0, 1, 2, 1, 0 },
                new[] { 2, 1, 0, 1, 2 },
                new[] { 1, 0, 0, 0, 1 },
                new[] { 1, 2, 2, 2, 1 },
                new[] { 0, 0, 1, 2, 2 },
                new[] { 2, 2, 1, 0, 0 },
                new[] { 0, 1, 1, 1, 2 },
                new[] { 2, 1, 1, 1, 0 },
                new[] { 1, 1, 0, 1, 1 },
                new[] { 1, 1, 2, 1, 1 },
                new[] { 0, 0, 2, 0, 0 },
                new[] { 2, 2, 0, 2, 2 },
                new[] { 1, 0, 1, 2, 1 },
                new[] { 1, 2, 1, 0, 1 },
                new[] { 0, 2, 0, 2, 0 },
                new[] { 2, 0, 2, 0, 2 },
                new[] { 0, 1, 0, 1, 0 },
                new[] { 2, 1, 2, 1, 2 },
                new[] { 0, 2, 2, 2, 0 },
                new[] { 2, 0, 0, 0, 2 },
                new[] { 1, 1, 0, 0, 0 },
                new[] { 1, 1, 2, 2, 2 },
            };

            if (lineIndex < 0 || lineIndex >= lineRows.Length) return 1;
            return lineRows[lineIndex][reelIndex];
        }
    }
}
