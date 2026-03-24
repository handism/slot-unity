using Cysharp.Threading.Tasks;
using DG.Tweening;
using SlotGame.Audio;
using SlotGame.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.View
{
    /// <summary>セッション統計パネルの View。</summary>
    public class StatsView : MonoBehaviour
    {
        [SerializeField] private TMP_Text totalSpinsText;
        [SerializeField] private TMP_Text winsText;
        [SerializeField] private TMP_Text winRateText;
        [SerializeField] private TMP_Text largestWinText;
        [SerializeField] private TMP_Text freeSpinTriggersText;
        [SerializeField] private TMP_Text netProfitText;
        [SerializeField] private Button   closeButton;

        private CanvasGroup  _canvasGroup;
        private AudioManager _audioManager;

        public event System.Action OnCloseRequested;

        private void Awake()
        {
            _audioManager = FindFirstObjectByType<AudioManager>();
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;

            closeButton.onClick.AddListener(() =>
            {
                PlayButtonClickSe();
                OnCloseRequested?.Invoke();
            });
        }

        /// <summary>統計値を画面に反映する。</summary>
        public void UpdateDisplay(in SessionStats stats)
        {
            if (totalSpinsText != null)
                totalSpinsText.text = stats.TotalSpins.ToString();

            if (winsText != null)
                winsText.text = stats.Wins.ToString();

            if (winRateText != null)
                winRateText.text = $"{stats.WinRate:F1}%";

            if (largestWinText != null)
                largestWinText.text = stats.LargestWin.ToString();

            if (freeSpinTriggersText != null)
                freeSpinTriggersText.text = stats.FreeSpinTriggers.ToString();

            if (netProfitText != null)
            {
                string sign = stats.NetProfit >= 0 ? "+" : "";
                netProfitText.text = $"{sign}{stats.NetProfit}";
                netProfitText.color = stats.NetProfit >= 0
                    ? new Color(0.2f, 1f, 0.4f)
                    : new Color(1f, 0.35f, 0.35f);
            }
        }

        public async UniTask ShowAsync(System.Threading.CancellationToken ct = default)
        {
            gameObject.SetActive(true);
            transform.localScale = Vector3.one * 0.9f;
            _canvasGroup.alpha = 0f;

            await UniTask.WhenAll(
                DOTween.To(() => _canvasGroup.alpha, x => _canvasGroup.alpha = x, 1f, 0.2f)
                    .SetEase(Ease.OutQuad).ToUniTask(cancellationToken: ct),
                transform.DOScale(1f, 0.2f)
                    .SetEase(Ease.OutBack).ToUniTask(cancellationToken: ct)
            );
        }

        public async UniTask HideAsync(System.Threading.CancellationToken ct = default)
        {
            await UniTask.WhenAll(
                DOTween.To(() => _canvasGroup.alpha, x => _canvasGroup.alpha = x, 0f, 0.15f)
                    .SetEase(Ease.InQuad).ToUniTask(cancellationToken: ct),
                transform.DOScale(0.9f, 0.15f)
                    .SetEase(Ease.InBack).ToUniTask(cancellationToken: ct)
            );
            gameObject.SetActive(false);
        }

        private void PlayButtonClickSe()
        {
            _audioManager ??= FindFirstObjectByType<AudioManager>();
            _audioManager?.PlaySE(SEType.ButtonClick);
        }
    }
}
