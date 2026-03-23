using DG.Tweening;
using SlotGame.Audio;
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
        private AudioManager _audioManager;

        private void Awake()
        {
            _audioManager = FindFirstObjectByType<AudioManager>();

            ConfigureNumericText(coinText, 22f);
            ConfigureNumericText(winText, 22f);

            if (autoButtonText == null && autoSpinButton != null)
                autoButtonText = autoSpinButton.GetComponentInChildren<TMP_Text>();

            for (int i = 0; i < betButtons.Length; i++)
            {
                int bet = betValues[i];
                var btn = betButtons[i];
                btn.onClick.AddListener(() => {
                    btn.transform.DOPunchScale(Vector3.one * 0.1f, 0.15f, 10, 1).SetUpdate(true);
                    PlayButtonClickSe();
                    OnBetButtonClicked(bet);
                });
            }

            if (spinButton != null)
                spinButton.onClick.AddListener(() =>
                {
                    spinButton.transform.DOPunchScale(Vector3.one * 0.1f, 0.15f, 10, 1).SetUpdate(true);
                    PlayButtonClickSe();
                });
            
            if (autoSpinButton != null)
                autoSpinButton.onClick.AddListener(() =>
                {
                    autoSpinButton.transform.DOPunchScale(Vector3.one * 0.1f, 0.15f, 10, 1).SetUpdate(true);
                    PlayButtonClickSe();
                });
        }

        public void SetCoins(long coins)
        {
            DOTween.To(() => _displayedCoins, v =>
            {
                _displayedCoins = v;
                coinText.text = v.ToString("N0");
            }, coins, 0.5f).SetEase(Ease.OutQuad);
        }

        public void SetBet(int bet)
        {
            for (int i = 0; i < betButtons.Length; i++)
            {
                bool isSelected = betValues[i] == bet;
                var button = betButtons[i];
                var image = button != null ? button.GetComponent<Image>() : null;
                var label = button != null ? button.GetComponentInChildren<TMP_Text>() : null;
                if (image != null)
                {
                    var baseColor = isSelected
                        ? new Color(0.95f, 0.71f, 0.24f, 0.96f)
                        : new Color(0.16f, 0.23f, 0.37f, 0.92f);
                    image.color = baseColor;

                    var colors = button.colors;
                    colors.normalColor = baseColor;
                    colors.highlightedColor = isSelected
                        ? new Color(1f, 0.78f, 0.34f, 1f)
                        : new Color(0.22f, 0.3f, 0.46f, 1f);
                    colors.pressedColor = isSelected
                        ? new Color(0.82f, 0.58f, 0.16f, 1f)
                        : new Color(0.1f, 0.16f, 0.28f, 1f);
                    colors.selectedColor = colors.highlightedColor;
                    colors.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.45f);
                    button.colors = colors;
                }

                if (label != null)
                {
                    label.color = isSelected
                        ? new Color(0.11f, 0.09f, 0.06f, 1f)
                        : new Color(0.92f, 0.96f, 1f, 0.96f);
                }
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

        private void PlayButtonClickSe()
        {
            _audioManager ??= FindFirstObjectByType<AudioManager>();
            _audioManager?.PlaySE(SEType.ButtonClick);
        }

        private static void ConfigureNumericText(TMP_Text text, float minFontSize)
        {
            if (text == null) return;

            text.enableAutoSizing = true;
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = minFontSize;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            text.characterSpacing = 0f;
            text.margin = new Vector4(0f, 0f, 4f, 0f);
        }
    }
}
