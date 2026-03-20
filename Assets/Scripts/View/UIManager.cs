using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
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

        public void UpdateCoins(long coins)    => mainHUD.SetCoins(coins);
        public void UpdateBet(int bet)         => mainHUD.SetBet(bet);
        public void SetSpinButtonInteractable(bool interactable) => mainHUD.SetSpinInteractable(interactable);

        public async UniTask ShowWinAmount(long amount, WinLevel level)
            => await winPopup.Show(amount, level, this.GetCancellationTokenOnDestroy());

        public void HighlightWinLines(IReadOnlyList<LineWin> wins)
        {
            // 実装: ペイライン強調表示（ReelController や LineHighlighter に委譲）
            // ここでは省略（フェーズ 4 の UI 実装で対応）
        }

        public void ClearLineHighlights()
        {
            // 実装: 強調表示を解除
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
    }
}
