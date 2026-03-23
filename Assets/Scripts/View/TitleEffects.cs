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
        [SerializeField] private RectTransform[] backgroundGlows;
        [SerializeField] private float glowRotationSpeed = 10f;

        [Header("Floating Symbols")]
        [SerializeField] private RectTransform[] floatingSymbols;
        [SerializeField] private float floatAmount = 20f;
        [SerializeField] private float floatSpeed = 1f;

        private Vector2[] initialSymbolPositions;
        private CancellationTokenSource cts;

        private void Start()
        {
            if (floatingSymbols != null)
            {
                initialSymbolPositions = new Vector2[floatingSymbols.Length];
                for (int i = 0; i < floatingSymbols.Length; i++)
                {
                    if (floatingSymbols[i] != null)
                        initialSymbolPositions[i] = floatingSymbols[i].anchoredPosition;
                }
            }

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

                // 背景グローの回転
                if (backgroundGlows != null)
                {
                    for (int i = 0; i < backgroundGlows.Length; i++)
                    {
                        if (backgroundGlows[i] != null)
                        {
                            float direction = (i % 2 == 0) ? 1f : -1f;
                            backgroundGlows[i].Rotate(Vector3.forward, direction * glowRotationSpeed * Time.deltaTime);
                        }
                    }
                }

                // 装飾シンボルの浮遊
                if (floatingSymbols != null && initialSymbolPositions != null)
                {
                    for (int i = 0; i < floatingSymbols.Length; i++)
                    {
                        if (floatingSymbols[i] != null && i < initialSymbolPositions.Length)
                        {
                            float offset = i * 1.5f;
                            float y = Mathf.Sin((elapsed + offset) * floatSpeed) * floatAmount;
                            floatingSymbols[i].anchoredPosition = initialSymbolPositions[i] + new Vector2(0f, y);
                        }
                    }
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
