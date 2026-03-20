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

        // ベット選択ボタン群（Inspector でボタンと値を紐付け）
        [SerializeField] private Button[] betButtons;
        [SerializeField] private int[]    betValues;   // { 10, 20, 50, 100 }

        private long _displayedCoins;

        private void Awake()
        {
            for (int i = 0; i < betButtons.Length; i++)
            {
                int bet = betValues[i];
                betButtons[i].onClick.AddListener(() => OnBetButtonClicked(bet));
            }
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

        public void SetWin(long amount)
        {
            winText.text = amount > 0 ? amount.ToString("N0") : "------";
        }

        private void OnBetButtonClicked(int bet)
        {
            // GameManager の OnBetChanged に委譲（Inspector でイベントを登録する）
        }
    }
}
