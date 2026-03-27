using DG.Tweening;
using SlotGame.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace SlotGame.View
{
    /// <summary>メイン HUD（コイン残高・ベット選択・スピンボタン）の表示担当 View。</summary>
    public class MainHUDView : MonoBehaviour
    {
        [SerializeField] private TMP_Text coinText;
        [SerializeField] private TMP_Text winText;
        [SerializeField] private Button   spinButton;
        [SerializeField] private Button   autoSpinButton;
        [SerializeField] private int[]    autoSpinCounts = { 10, 25, 50, 100 };
        [SerializeField] private TMP_Text spinButtonText;
        [SerializeField] private TMP_Text autoButtonText;

        // ベット選択ボタン群（Inspector でボタンと値を紐付け）
        [SerializeField] private Button[] betButtons;
        [SerializeField] private int[]    betValues;   // { 10, 20, 50, 100 }

        private long _displayedCoins;
        private long _displayedWin;
        private AudioManager _audioManager;
        private List<Button> _autoSpinCountButtons = new();

        public event System.Action<int>? OnAutoSpinRequested;
        public event System.Action?      OnAutoSpinStopRequested;

        private void Awake()
        {
            _audioManager = FindFirstObjectByType<AudioManager>();

            ConfigureNumericText(coinText, 22f);
            ConfigureNumericText(winText, 22f);

            if (spinButtonText == null && spinButton != null)
                spinButtonText = spinButton.GetComponentInChildren<TMP_Text>();

            if (autoButtonText == null && autoSpinButton != null)
                autoButtonText = autoSpinButton.GetComponentInChildren<TMP_Text>();

            // スピンボタンにブルー系グラデーションを適用
            var spinImg = spinButton != null ? spinButton.GetComponent<Image>() : null;
            if (spinImg != null)
            {
                var grad = spinImg.gameObject.AddComponent<UIGradient>();
                grad.SetColors(
                    new Color(0.3f, 0.5f, 0.9f, 1f),
                    new Color(0.05f, 0.15f, 0.4f, 1f)
                );
            }

            // オートスピンボタンにも同系統のグラデーションを適用
            var autoImg = autoSpinButton != null ? autoSpinButton.GetComponent<Image>() : null;
            if (autoImg != null)
            {
                var grad = autoImg.gameObject.AddComponent<UIGradient>();
                grad.SetColors(
                    new Color(0.25f, 0.4f, 0.75f, 1f),
                    new Color(0.04f, 0.12f, 0.32f, 1f)
                );
            }

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
            {
                autoSpinButton.onClick.AddListener(() =>
                {
                    autoSpinButton.transform.DOPunchScale(Vector3.one * 0.1f, 0.15f, 10, 1).SetUpdate(true);
                    PlayButtonClickSe();
                    OnAutoSpinStopRequested?.Invoke();
                });

                // 回数選択ボタンを動的に生成
                CreateAutoSpinSelectors();
            }
        }

        private void CreateAutoSpinSelectors()
        {
            if (autoSpinButton == null || autoSpinCounts == null) return;

            var parent = autoSpinButton.transform.parent;
            // 既存のボタンのレイアウトを崩さないよう調整
            var horizontalLayout = parent.GetComponent<HorizontalLayoutGroup>();
            
            foreach (var count in autoSpinCounts)
            {
                var btnGo = Instantiate(autoSpinButton.gameObject, parent);
                btnGo.name = $"AutoSpin_{count}";
                var btn = btnGo.GetComponent<Button>();
                var txt = btnGo.GetComponentInChildren<TMP_Text>();
                if (txt != null)
                {
                    txt.text = count.ToString();
                    txt.fontSize = 14f; // 小さく調整
                }

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    btn.transform.DOPunchScale(Vector3.one * 0.1f, 0.15f, 10, 1).SetUpdate(true);
                    PlayButtonClickSe();
                    OnAutoSpinRequested?.Invoke(count);
                });

                btnGo.transform.localScale = Vector3.one * 0.9f;
                _autoSpinCountButtons.Add(btn);
            }
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
                    image.color = Color.white;
                    var grad = image.GetComponent<UIGradient>() ?? image.gameObject.AddComponent<UIGradient>();
                    if (isSelected)
                    {
                        grad.SetColors(
                            new Color(1f,   0.85f, 0.4f,  0.96f),
                            new Color(0.7f, 0.45f, 0.1f,  0.96f)
                        );
                    }
                    else
                    {
                        grad.SetColors(
                            new Color(0.22f, 0.32f, 0.5f,  0.92f),
                            new Color(0.1f,  0.15f, 0.28f, 0.92f)
                        );
                    }

                    var colors = button.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = isSelected
                        ? new Color(1f, 0.92f, 0.55f, 1f)
                        : new Color(0.3f, 0.4f, 0.6f, 1f);
                    colors.pressedColor = isSelected
                        ? new Color(0.85f, 0.6f, 0.2f, 1f)
                        : new Color(0.08f, 0.12f, 0.22f, 1f);
                    colors.selectedColor    = colors.highlightedColor;
                    colors.disabledColor    = new Color(1f, 1f, 1f, 0.35f);
                    button.colors = colors;
                }

                if (label != null)
                {
                    label.color = isSelected
                        ? new Color(0.11f, 0.09f, 0.06f, 1f)
                        : new Color(0.92f, 0.96f, 1f,    0.96f);
                }
            }
        }

        public void SetSpinInteractable(bool interactable)
        {
            if (spinButton != null) spinButton.interactable = interactable;
        }

        public void SetSpinButtonMode(bool isStopMode)
        {
            if (spinButtonText == null || spinButton == null) return;
            spinButtonText.text = isStopMode ? "STOP" : "SPIN";
            
            var grad = spinButton.GetComponent<UIGradient>();
            if (grad != null)
            {
                if (isStopMode)
                {
                    grad.SetColors(new Color(1f, 0.4f, 0.3f, 1f), new Color(0.6f, 0.1f, 0.05f, 1f));
                }
                else
                {
                    grad.SetColors(new Color(0.3f, 0.5f, 0.9f, 1f), new Color(0.05f, 0.15f, 0.4f, 1f));
                }
            }
        }

        public void SetAutoButtonText(string text)
        {
            if (autoButtonText != null)
                autoButtonText.text = text;
        }

        public void SetAutoSpinCountInteractable(bool interactable)
        {
            foreach (var btn in _autoSpinCountButtons)
            {
                if (btn != null) btn.interactable = interactable;
            }
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
            // GameManager の OnBetChanged に委譲
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
