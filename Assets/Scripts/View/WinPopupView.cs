using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace SlotGame.View
{
    /// <summary>当選額を中央ポップアップで表示する View。</summary>
    public class WinPopupView : MonoBehaviour
    {
        [SerializeField] private TMP_Text winAmountText;
        [SerializeField] private TMP_Text winLevelText;
        [SerializeField] private float    displayDuration = 2f;

        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0;
        }

        public async UniTask Show(long amount, WinLevel level, CancellationToken ct)
        {
            winAmountText.text = amount.ToString("N0");
            winLevelText.text  = level switch
            {
                WinLevel.Mega  => "MEGA WIN!",
                WinLevel.Big   => "BIG WIN!",
                WinLevel.Small => "WIN!",
                _ => ""
            };

            // スケールアニメーションで表示
            transform.localScale = Vector3.zero;
            _canvasGroup.alpha   = 1;

            // より派手なスケールアニメーションとシェイクの追加（イージングのオーバーシュートを強くする）
            await transform.DOScale(1.2f, 0.35f).SetEase(Ease.OutBack, 2.5f).ToUniTask(cancellationToken: ct);
            _ = transform.DOShakeRotation(0.5f, new Vector3(0, 0, 8), 15, 90f, false).SetEase(Ease.OutQuad);
            await transform.DOScale(1.0f, 0.15f).SetEase(Ease.InOutSine).ToUniTask(cancellationToken: ct);
            await UniTask.Delay(TimeSpan.FromSeconds(displayDuration), cancellationToken: ct);

            // フェードアウト
            await DOTween.To(
                    () => _canvasGroup.alpha,
                    value => _canvasGroup.alpha = value,
                    0f,
                    0.3f)
                .ToUniTask(cancellationToken: ct);
            _canvasGroup.alpha = 0;
        }
    }
}
