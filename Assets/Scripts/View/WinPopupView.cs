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

            await transform.DOScale(1.1f, 0.25f).SetEase(Ease.OutBack).ToUniTask(cancellationToken: ct);
            await transform.DOScale(1.0f, 0.1f).ToUniTask(cancellationToken: ct);
            await UniTask.Delay(TimeSpan.FromSeconds(displayDuration), cancellationToken: ct);

            // フェードアウト
            await _canvasGroup.DOFade(0f, 0.3f).ToUniTask(cancellationToken: ct);
            _canvasGroup.alpha = 0;
        }
    }
}
