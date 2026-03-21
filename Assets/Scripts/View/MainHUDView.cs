using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.View
{
    /// <summary>メイン HUD（コイン残高・ベット選択・スピンボタン）の表示担当 View。</summary>
    public class MainHUDView : MonoBehaviour
    {
        [SerializeField] private TMP_Text coinText;
        [SerializeField] private TMP_Text winText;
        [SerializeField] private Button   spinButton;
        [SerializeField] private Button   autoSpinButton;
        [SerializeField] private TMP_Text autoButtonText;

        // ベット選択ボタン群（Inspector でボタンと値を紐付け）
        [SerializeField] private Button[] betButtons;
        [SerializeField] private int[]    betValues;   // { 10, 20, 50, 100 }

        private long _displayedCoins;
        private long _displayedWin;

        private void Awake()
        {
            if (autoButtonText == null && autoSpinButton != null)
                autoButtonText = autoSpinButton.GetComponentInChildren<TMP_Text>();

            for (int i = 0; i < betButtons.Length; i++)
            {
                int bet = betValues[i];
                var btn = betButtons[i];
                btn.onClick.AddListener(() => {
                    btn.transform.DOPunchScale(Vector3.one * 0.1f, 0.15f, 10, 1).SetUpdate(true);
                    OnBetButtonClicked(bet);
                });
            }

            if (spinButton != null)
                spinButton.onClick.AddListener(() => spinButton.transform.DOPunchScale(Vector3.one * 0.1f, 0.15f, 10, 1).SetUpdate(true));
            
            if (autoSpinButton != null)
                autoSpinButton.onClick.AddListener(() => autoSpinButton.transform.DOPunchScale(Vector3.one * 0.1f, 0.15f, 10, 1).SetUpdate(true));
        }

        public void SetCoins(long coins)
        {
            DOTween.To(() => _displayedCoins, v =>
            {
                _displayedCoins = v;
                coinText.SetText("{0}", v);
            }, coins, 0.5f).SetEase(Ease.OutQuad);
        }

        public void SetBet(int bet)
        {
            // ベット選択ボタンのハイライト更新
            for (int i = 0; i < betButtons.Length; i++)
            {
                var colors = betButtons[i].colors;
                colors.normalColor = (betValues[i] == bet) ? Color.yellow : Color.white;
                betButtons[i].colors = colors;
            }
        }

        public void SetSpinInteractable(bool interactable)
        {
            spinButton.interactable = interactable;
        }

        public void SetAutoButtonText(string text)
        {
            if (autoButtonText != null)
                autoButtonText.text = text;
        }

        public void SetWin(long amount)
        {
            if (amount <= 0)
            {
                winText.text = "------";
                _displayedWin = 0;
                return;
            }

            DOTween.To(() => _displayedWin, v =>
            {
                _displayedWin = v;
                winText.text = v.ToString("N0");
            }, amount, 1.0f).SetEase(Ease.OutCubic);
        }

        private void OnBetButtonClicked(int bet)
        {
            // GameManager の OnBetChanged に委譲（Inspector でイベントを登録する）
        }
    }
}
