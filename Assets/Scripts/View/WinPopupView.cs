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
        private Sequence    _currentSequence;
        private long        _countValue;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0;

            // ポップアップ背景 Image にダークネイビーグラデーションを適用
            var bgImage = GetComponent<UnityEngine.UI.Image>();
            if (bgImage != null)
            {
                var grad = bgImage.gameObject.AddComponent<UIGradient>();
                grad.SetColors(
                    new Color(0.08f, 0.12f, 0.25f, 0.95f),
                    new Color(0.03f, 0.05f, 0.15f, 0.98f)
                );
            }

            // テキストの初期スタイル設定
            ApplyInitialStyle(winAmountText);
            ApplyInitialStyle(winLevelText);
        }

        private void ApplyInitialStyle(TMP_Text text)
        {
            if (text == null) return;
            text.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            
            // アウトライン設定（マテリアルを強制更新）
            text.outlineWidth = 0.25f;
            text.outlineColor = Color.black;
            text.UpdateMeshPadding(); 
        }

        public async UniTask Show(long amount, WinLevel level, CancellationToken ct)
        {
            _currentSequence?.Kill();
            transform.DOKill();
            winAmountText.transform.DOKill();
            
            // 初期のカウント値を 0 にリセット
            _countValue = 0;
            winAmountText.text = "0";
            
            // 当選レベルに応じた文字列とスタイルの設定
            SetupDisplayByLevel(level);

            // シーケンス開始
            transform.localScale = Vector3.zero;
            _canvasGroup.alpha   = 1;
            _currentSequence = DOTween.Sequence();
            
            // 1. 豪華な登場アニメーション
            _ = _currentSequence.Append(transform.DOScale(1.5f, 0.4f).SetEase(Ease.OutBack, 3.5f));
            _ = _currentSequence.Append(transform.DOScale(1.0f, 0.15f).SetEase(Ease.OutSine));

            // 2. カウントアップ演出（金額を徐々に増やす）
            float countDuration = (level == WinLevel.Mega) ? 1.5f : (level == WinLevel.Big) ? 1.0f : 0.5f;
            _ = _currentSequence.Join(DOTween.To(() => _countValue, x => {
                _countValue = x;
                winAmountText.text = _countValue.ToString("N0");
            }, amount, countDuration).SetEase(Ease.OutCubic));

            // 3. 滞在中のループ演出（レベル別）
            if (level >= WinLevel.Big)
            {
                // BIG 以上は脈動と光のゆらぎ（Sequence の完了後に開始するようにし、Sequence 内には含めない）
                _ = _currentSequence.OnComplete(() => {
                    _ = transform.DOScale(1.15f, 0.4f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
                    
                    if (level == WinLevel.Mega)
                    {
                        // MEGA は回転シェイクとズーム
                        _ = transform.DOShakeRotation(1f, 8f, 15, 90f, false).SetLoops(-1);
                        _ = winAmountText.transform.DOScale(1.2f, 0.3f).SetLoops(-1, LoopType.Yoyo);
                    }
                });
            }

            // 指定された時間表示（MEGA の場合は少し長めに）
            float finalDuration = (level == WinLevel.Mega) ? displayDuration + 1.0f : displayDuration;
            await UniTask.Delay(TimeSpan.FromSeconds(finalDuration), cancellationToken: ct);

            // 4. フェードアウト
            _currentSequence?.Kill();
            transform.DOKill();
            winAmountText.transform.DOKill();
            await DOTween.To(() => _canvasGroup.alpha, v => _canvasGroup.alpha = v, 0f, 0.4f).ToUniTask(cancellationToken: ct);
            
            _canvasGroup.alpha = 0;
            _currentSequence = null;
        }

        private void SetupDisplayByLevel(WinLevel level)
        {
            Color top, bottom;
            string levelString;

            switch (level)
            {
                case WinLevel.Mega:
                    levelString = "MEGA WIN!";
                    // MEGA は情熱的なレッド〜ゴールドのグラデーション
                    top    = new Color(1f, 0.1f, 0f);    // 鮮烈な赤
                    bottom = new Color(1f, 0.85f, 0f);   // 輝くゴールド
                    break;
                case WinLevel.Big:
                    levelString = "BIG WIN!";
                    // BIG は煌びやかな黄色〜オレンジのグラデーション
                    top    = new Color(1f, 1f, 0.3f);    // 明るいレモン
                    bottom = new Color(1f, 0.5f, 0f);    // 深いオレンジ
                    break;
                case WinLevel.Small:
                default:
                    levelString = "WIN!";
                    // 通常 Win は柔らかい白〜シルバー
                    top    = Color.white;
                    bottom = new Color(0.7f, 0.9f, 1f);  // 透き通る水色/シルバー
                    break;
            }

            winLevelText.text = levelString;
            
            ApplyGradient(winLevelText, top, bottom);
            ApplyGradient(winAmountText, top, bottom);
        }

        private void ApplyGradient(TMP_Text text, Color top, Color bottom)
        {
            if (text == null) return;
            text.enableVertexGradient = true;
            // 左右で色を変えることで立体感を出す
            text.colorGradient = new VertexGradient(top, top, bottom, bottom);
        }
    }
}
