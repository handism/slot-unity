using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.View
{
    public class TutorialView : MonoBehaviour
    {
        private readonly string[] _steps = {
            "【1. ベットの変更】\n画面下部の＋/－ボタンで、1スピンあたりの賭けコイン（ベット額）を変更できます。",
            "【2. スピン】\n右下の「SPIN」ボタンを押すとリールが回転します。長押しや「AUTO」ボタンで自動スピンも可能です。",
            "【3. 設定 / ペイテーブル】\n画面右下の「i」ボタンや右上の歯車ボタンから、ルールの確認や音量設定が可能です。",
            "【4. 特殊シンボル】\n「SCATTER(魔法陣)」が3つでフリースピン、「BONUS(宝箱)」が3つ出るとボーナスラウンドに突入します。",
            "【5. さらに楽しく】\nフリースピン中は配当が2倍！宝箱を選んで大量コイン獲得を目指しましょう！"
        };
        private int _currentStep;
        private TMP_Text _messageText;
        private Button _nextButton;
        private Button _skipButton;
        private CanvasGroup _canvasGroup;

        public event System.Action OnComplete;

        public void Setup()
        {
            var rect = gameObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.8f);

            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // パネル作成
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(800, 450);
            panel.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.18f, 1f);

            // テキスト作成
            var textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(panel.transform, false);
            _messageText = textObj.GetComponent<TextMeshProUGUI>();
            _messageText.rectTransform.anchorMin = new Vector2(0.05f, 0.25f);
            _messageText.rectTransform.anchorMax = new Vector2(0.95f, 0.9f);
            _messageText.rectTransform.offsetMin = Vector2.zero;
            _messageText.rectTransform.offsetMax = Vector2.zero;
            _messageText.font = TMP_Settings.defaultFontAsset;
            _messageText.fontSize = 32;
            _messageText.alignment = TextAlignmentOptions.Center;
            _messageText.textWrappingMode = TextWrappingModes.Normal;
            _messageText.color = Color.white;

            // 次へボタン
            var nextObj = new GameObject("NextButton", typeof(RectTransform), typeof(Image), typeof(Button));
            nextObj.transform.SetParent(panel.transform, false);
            var nextRect = nextObj.GetComponent<RectTransform>();
            nextRect.anchorMin = new Vector2(0.55f, 0.05f);
            nextRect.anchorMax = new Vector2(0.9f, 0.2f);
            nextRect.offsetMin = Vector2.zero;
            nextRect.offsetMax = Vector2.zero;
            nextObj.GetComponent<Image>().color = new Color(0.1f, 0.4f, 0.6f);
            _nextButton = nextObj.GetComponent<Button>();
            _nextButton.onClick.AddListener(OnNextClicked);

            var nextTextObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            nextTextObj.transform.SetParent(nextObj.transform, false);
            var nextText = nextTextObj.GetComponent<TextMeshProUGUI>();
            SetStampRect(nextText.rectTransform);
            nextText.font = TMP_Settings.defaultFontAsset;
            nextText.text = "次へ";
            nextText.color = Color.white;
            nextText.alignment = TextAlignmentOptions.Center;
            nextText.fontSize = 28;

            // スキップボタン
            var skipObj = new GameObject("SkipButton", typeof(RectTransform), typeof(Image), typeof(Button));
            skipObj.transform.SetParent(panel.transform, false);
            var skipRect = skipObj.GetComponent<RectTransform>();
            skipRect.anchorMin = new Vector2(0.1f, 0.05f);
            skipRect.anchorMax = new Vector2(0.45f, 0.2f);
            skipRect.offsetMin = Vector2.zero;
            skipRect.offsetMax = Vector2.zero;
            skipObj.GetComponent<Image>().color = new Color(0.4f, 0.4f, 0.4f);
            _skipButton = skipObj.GetComponent<Button>();
            _skipButton.onClick.AddListener(OnSkipClicked);

            var skipTextObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            skipTextObj.transform.SetParent(skipObj.transform, false);
            var skipText = skipTextObj.GetComponent<TextMeshProUGUI>();
            SetStampRect(skipText.rectTransform);
            skipText.font = TMP_Settings.defaultFontAsset;
            skipText.text = "スキップ";
            skipText.color = Color.white;
            skipText.alignment = TextAlignmentOptions.Center;
            skipText.fontSize = 28;
            
            UpdateStep();
            gameObject.SetActive(false);
        }

        private void SetStampRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void OnNextClicked()
        {
            _currentStep++;
            if (_currentStep >= _steps.Length)
            {
                Finish();
            }
            else
            {
                UpdateStep();
            }
        }

        private void OnSkipClicked()
        {
            Finish();
        }

        private void UpdateStep()
        {
            _messageText.text = _steps[_currentStep];
            if (_currentStep == _steps.Length - 1)
            {
                _nextButton.GetComponentInChildren<TextMeshProUGUI>().text = "閉じる";
            }
            else
            {
                _nextButton.GetComponentInChildren<TextMeshProUGUI>().text = "次へ";
            }
        }

        private void Finish()
        {
            gameObject.SetActive(false);
            OnComplete?.Invoke();
        }

        public async UniTask ShowAsync(CancellationToken ct)
        {
            _currentStep = 0;
            UpdateStep();
            gameObject.SetActive(true);
            
            var tcs = new UniTaskCompletionSource();
            
            System.Action completeAction = null;
            completeAction = () => 
            {
                OnComplete -= completeAction;
                tcs.TrySetResult();
            };
            OnComplete += completeAction;

            using (ct.Register(() => 
            {
                OnComplete -= completeAction;
                tcs.TrySetCanceled();
            }))
            {
                await tcs.Task;
            }
        }
    }
}
