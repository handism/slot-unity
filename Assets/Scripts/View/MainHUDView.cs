#nullable enable
using DG.Tweening;
using SlotGame.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace SlotGame.View
{
    /// <summary>メイン HUD（コイン残高・ベット選択・スピンボタン）の表示担当 View。</summary>
    public class MainHUDView : MonoBehaviour
    {
        [SerializeField] private TMP_Text coinText = null!;
        [SerializeField] private TMP_Text winText = null!;
        [SerializeField] private Button   spinButton = null!;
        [SerializeField] private Button   autoSpinButton = null!;
        [SerializeField] private Button   turboButton = null!;
        [SerializeField] private int[]    autoSpinCounts = { 10, 25, 50, 100 };
        [SerializeField] private TMP_Text spinButtonText = null!;
        [SerializeField] private TMP_Text autoButtonText = null!;

        // ベット選択ボタン群（Inspector でボタンと値を紐付け）
        [SerializeField] private Button[] betButtons = null!;
        [SerializeField] private int[]    betValues = null!;   // { 10, 20, 50, 100 }

        private long _displayedCoins;
        private long _displayedWin;
        private AudioManager? _audioManager;
        private List<Button> _autoSpinCountButtons = new();
        private GameObject? _autoSpinPopup;
        private bool _popupOpen;

        public event System.Action<int>? OnAutoSpinRequested;
        public event System.Action?      OnAutoSpinStopRequested;
        public event System.Action<bool>? OnTurboToggled;

        private bool _isTurbo;
        private bool _isAutoRunning;
        private int  _lastStateChangeFrame;

        // ロングプレス（長押し）検知用
        private float _pointerDownTime;
        private bool  _isPointerDown;
        private bool  _longPressTriggered;
        private const float LongPressThreshold = 0.5f;

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
                SetupAutoSpinButtonEvents();
                BuildAutoSpinPopup();
            }

            if (turboButton != null)
            {
                turboButton.onClick.AddListener(() =>
                {
                    _isTurbo = !_isTurbo;
                    turboButton.transform.DOPunchScale(Vector3.one * 0.1f, 0.15f, 10, 1).SetUpdate(true);
                    PlayButtonClickSe();
                    UpdateTurboVisual();
                    OnTurboToggled?.Invoke(_isTurbo);
                });
            }
        }

        private void Update()
        {
            if (_isPointerDown && !_longPressTriggered && !_isAutoRunning)
            {
                if (Time.unscaledTime - _pointerDownTime > LongPressThreshold)
                {
                    _longPressTriggered = true;
                    OpenAutoSpinPopup();
                    autoSpinButton.transform.DOPunchScale(Vector3.one * 0.1f, 0.15f, 10, 1).SetUpdate(true);
                    PlayButtonClickSe();
                }
            }
        }

        private void SetupAutoSpinButtonEvents()
        {
            var trigger = autoSpinButton.gameObject.AddComponent<EventTrigger>();

            // PointerDown
            var pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            pointerDown.callback.AddListener((data) =>
            {
                _isPointerDown = true;
                _pointerDownTime = Time.unscaledTime;
                _longPressTriggered = false;
            });
            trigger.triggers.Add(pointerDown);

            // PointerUp
            var pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            pointerUp.callback.AddListener((data) =>
            {
                if (_isPointerDown && !_longPressTriggered)
                {
                    OnAutoSpinButtonClick();
                }
                _isPointerDown = false;
            });
            trigger.triggers.Add(pointerUp);

            // PointerExit (枠外に外れたらキャンセル)
            var pointerExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            pointerExit.callback.AddListener((data) =>
            {
                _isPointerDown = false;
            });
            trigger.triggers.Add(pointerExit);
        }

        private void OnAutoSpinButtonClick()
        {
            autoSpinButton.transform.DOPunchScale(Vector3.one * 0.1f, 0.15f, 10, 1).SetUpdate(true);
            PlayButtonClickSe();

            if (_isAutoRunning && Time.frameCount > _lastStateChangeFrame)
            {
                OnAutoSpinStopRequested?.Invoke();
            }
            else if (_popupOpen)
            {
                CloseAutoSpinPopup();
            }
            else if (!_isAutoRunning)
            {
                // 長押しされなかった場合はデフォルト（10回など）で開始
                OnAutoSpinRequested?.Invoke(-1); // -1 は GameManager 側でデフォルト値として扱う
            }
        }

        private void BuildAutoSpinPopup()
        {
            if (autoSpinButton == null || autoSpinCounts == null) return;

            // autoSpinButton の RectTransform を基準にポップアップを作成
            var autoRect = autoSpinButton.GetComponent<RectTransform>();

            var popupGo = new GameObject("AutoSpinPopup", typeof(RectTransform));
            popupGo.transform.SetParent(autoSpinButton.transform.parent, false);
            popupGo.transform.SetAsLastSibling();

            var popupRect = popupGo.GetComponent<RectTransform>();
            // autoSpinButton と同じアンカー・ピボット・X位置に合わせる
            popupRect.anchorMin = autoRect.anchorMin;
            popupRect.anchorMax = autoRect.anchorMax;
            popupRect.pivot     = new Vector2(autoRect.pivot.x, 0f);

            const float btnH = 52f;
            const float gap  = 4f;
            float totalH = autoSpinCounts.Length * btnH + (autoSpinCounts.Length - 1) * gap;
            popupRect.sizeDelta = new Vector2(autoRect.sizeDelta.x, totalH);
            // autoSpinButton の上端から gap 分上に配置
            popupRect.anchoredPosition = new Vector2(
                autoRect.anchoredPosition.x,
                autoRect.anchoredPosition.y + autoRect.sizeDelta.y + gap
            );

            // 背景
            var bg = popupGo.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.08f, 0.14f, 0.92f);
            bg.raycastTarget = true;

            for (int i = 0; i < autoSpinCounts.Length; i++)
            {
                int count = autoSpinCounts[i];
                var btnGo = Instantiate(autoSpinButton.gameObject, popupGo.transform);
                btnGo.name = $"AutoSpin_{count}";
                
                // 元のボタンの EventTrigger は不要（かつ誤作動の元）なので即座に削除
                var oldTrigger = btnGo.GetComponent<EventTrigger>();
                if (oldTrigger != null) DestroyImmediate(oldTrigger);

                var btnRect = btnGo.GetComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0f, 0f);
                btnRect.anchorMax = new Vector2(1f, 0f);
                btnRect.pivot     = new Vector2(0.5f, 0f);
                // 下から順に積む（index 0 が一番下）
                btnRect.anchoredPosition = new Vector2(0f, i * (btnH + gap));
                btnRect.sizeDelta = new Vector2(0f, btnH);

                var txt = btnGo.GetComponentInChildren<TMP_Text>();
                if (txt != null) { txt.text = count.ToString(); txt.fontSize = 16f; }

                var btn = btnGo.GetComponent<Button>();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    btn.transform.DOPunchScale(Vector3.one * 0.1f, 0.15f, 10, 1).SetUpdate(true);
                    PlayButtonClickSe();
                    CloseAutoSpinPopup();
                    OnAutoSpinRequested?.Invoke(count);
                });

                _autoSpinCountButtons.Add(btn);
            }

            _autoSpinPopup = popupGo;
            popupGo.SetActive(false);
        }

        private void OpenAutoSpinPopup()
        {
            if (_autoSpinPopup == null) return;
            _popupOpen = true;
            _autoSpinPopup.SetActive(true);
            _autoSpinPopup.transform.localScale = new Vector3(1f, 0f, 1f);
            _autoSpinPopup.transform.DOScaleY(1f, 0.15f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        private void CloseAutoSpinPopup()
        {
            if (_autoSpinPopup == null) return;
            _popupOpen = false;
            _autoSpinPopup.transform.DOScaleY(0f, 0.1f).SetEase(Ease.InQuad).SetUpdate(true)
                .OnComplete(() => _autoSpinPopup.SetActive(false));
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

                if (image != null && button != null)
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
            
            bool nextIsRunning = text == "ストップ" || text.Contains("ストップ");
            if (nextIsRunning && !_isAutoRunning)
            {
                _lastStateChangeFrame = Time.frameCount;
            }
            _isAutoRunning = nextIsRunning;
        }

        public void SetAutoSpinCountInteractable(bool interactable)
        {
            foreach (var btn in _autoSpinCountButtons)
                if (btn != null) btn.interactable = interactable;

            if (!interactable) CloseAutoSpinPopup();
        }

        public void SetTurbo(bool enabled)
        {
            _isTurbo = enabled;
            UpdateTurboVisual();
        }

        private void UpdateTurboVisual()
        {
            if (turboButton == null) return;
            var txt = turboButton.GetComponentInChildren<TMP_Text>();
            if (txt != null)
            {
                txt.text = _isTurbo ? "TURBO: ON" : "TURBO: OFF";
                txt.color = _isTurbo ? Color.yellow : Color.white;
            }

            var image = turboButton.GetComponent<Image>();
            if (image != null)
            {
                var grad = image.GetComponent<UIGradient>() ?? image.gameObject.AddComponent<UIGradient>();
                if (_isTurbo)
                {
                    grad.SetColors(new Color(1f, 0.8f, 0.2f), new Color(0.6f, 0.4f, 0f));
                }
                else
                {
                    grad.SetColors(new Color(0.4f, 0.4f, 0.4f), new Color(0.2f, 0.2f, 0.2f));
                }
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
