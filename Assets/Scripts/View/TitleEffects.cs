using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace SlotGame.View
{
    /// <summary>
    /// タイトルシーンのUI演出（ブレス演出、光彩、アニメーション）を担当するクラス。
    /// </summary>
    public class TitleEffects : MonoBehaviour
    {
        [Header("Logo Animation")]
        [SerializeField] private RectTransform logoTransform;
        [SerializeField] private float breathingSpeed = 2f;
        [SerializeField] private float breathingAmount = 0.05f;

        [Header("Start Button Animation")]
        [SerializeField] private RectTransform startButtonTransform;
        [SerializeField] private CanvasGroup startButtonCanvasGroup;
        [SerializeField] private float pulseSpeed = 1.5f;
        [SerializeField] private float pulseMinAlpha = 0.6f;

        [Header("Background Effects")]
        [SerializeField] private Image overlayFade;

        private CancellationTokenSource cts;

        private void Start()
        {
            cts = new CancellationTokenSource();
            StartAnimations(cts.Token).Forget();
        }

        private void OnDestroy()
        {
            cts?.Cancel();
            cts?.Dispose();
        }

        private async UniTaskVoid StartAnimations(CancellationToken token)
        {
            float elapsed = 0f;

            while (!token.IsCancellationRequested)
            {
                elapsed += Time.deltaTime;

                // ロゴのブレス演出 (Scale)
                if (logoTransform != null)
                {
                    float scale = 1f + Mathf.Sin(elapsed * breathingSpeed) * breathingAmount;
                    logoTransform.localScale = new Vector3(scale, scale, 1f);
                }

                // スタートボタンの明滅演出 (Alpha)
                if (startButtonCanvasGroup != null)
                {
                    float alpha = pulseMinAlpha + (1f - pulseMinAlpha) * (0.5f + 0.5f * Mathf.Sin(elapsed * pulseSpeed));
                    startButtonCanvasGroup.alpha = alpha;
                }

                // スタートボタンの微かな拡大縮小
                if (startButtonTransform != null)
                {
                    float btnScale = 1f + Mathf.Sin(elapsed * pulseSpeed) * 0.02f;
                    startButtonTransform.localScale = new Vector3(btnScale, btnScale, 1f);
                }

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        /// <summary>
        /// シーン遷移時のフェードアウト演出（例）
        /// </summary>
        public async UniTask FadeOutAsync()
        {
            if (overlayFade == null) return;

            overlayFade.gameObject.SetActive(true);
            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                overlayFade.color = new Color(0, 0, 0, t);
                await UniTask.Yield();
            }
        }
    }
}
