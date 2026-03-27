using Cysharp.Threading.Tasks;
using DG.Tweening;
using SlotGame.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.View
{
    /// <summary>ゲーム説明モーダルを表示する View。</summary>
    public class GameDescriptionView : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text descriptionText;

        private CanvasGroup _canvasGroup;
        private AudioManager _audioManager;

        public event System.Action OnCloseRequested;

        private void Awake()
        {
            _audioManager = FindFirstObjectByType<AudioManager>();
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0;

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() =>
                {
                    PlayButtonClickSe();
                    OnCloseRequested?.Invoke();
                });
            }
        }

        public void SetDescription(string text)
        {
            if (descriptionText != null) descriptionText.text = text;
        }

        public async UniTask ShowAsync(System.Threading.CancellationToken ct = default)
        {
            gameObject.SetActive(true);
            transform.localScale = Vector3.one * 0.9f;
            _canvasGroup.alpha = 0f;

            await UniTask.WhenAll(
                DOTween.To(() => _canvasGroup.alpha, x => _canvasGroup.alpha = x, 1f, 0.2f).SetEase(Ease.OutQuad).ToUniTask(cancellationToken: ct),
                transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack).ToUniTask(cancellationToken: ct)
            );
        }

        public async UniTask HideAsync(System.Threading.CancellationToken ct = default)
        {
            await UniTask.WhenAll(
                DOTween.To(() => _canvasGroup.alpha, x => _canvasGroup.alpha = x, 0f, 0.15f).SetEase(Ease.InQuad).ToUniTask(cancellationToken: ct),
                transform.DOScale(0.9f, 0.15f).SetEase(Ease.InBack).ToUniTask(cancellationToken: ct)
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
