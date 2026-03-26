using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using SlotGame.Data;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.View
{
    /// <summary>シンボル 1 個の表示を担う View（オブジェクトプール管理対象）。</summary>
    [RequireComponent(typeof(Image))]
    public class SymbolView : MonoBehaviour
    {
        private Image         _image;
        private Animator      _animator;
        private int           _symbolId;
        private AnimationClip _winAnim;
        private Tween         _pulseTween;

        private void Awake()
        {
            _image    = GetComponent<Image>();
            _animator = GetComponent<Animator>();
        }

        public int SymbolId => _symbolId;

        public void SetSymbol(SymbolData data)
        {
            _symbolId    = data.symbolId;
            _image.sprite = data.sprite;
            _image.enabled = true;
            _winAnim     = data.winAnim;
        }

        public void SetSymbolId(int id) => _symbolId = id;

        public void SetHighlighted(bool highlighted)
        {
            if (_image == null)
            {
                _image = GetComponent<Image>();
            }

            _image.color = highlighted ? Color.white : new Color(1f, 1f, 1f, 0.3f);
        }

        public void ResetHighlight()
        {
            if (_image == null)
            {
                _image = GetComponent<Image>();
            }

            _image.color = Color.white;
            StopPulseAnimation();
        }

        /// <summary>パルスアニメーション（拡縮繰り返し）を開始する。</summary>
        public void PlayPulseAnimation()
        {
            StopPulseAnimation();
            _pulseTween = transform.DOScale(1.2f, 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        /// <summary>パルスアニメーションを停止する。</summary>
        public void StopPulseAnimation()
        {
            if (_pulseTween != null && _pulseTween.IsActive())
            {
                _pulseTween.Kill();
            }
            _pulseTween = null;
            transform.localScale = Vector3.one;
        }

        /// <summary>アイドル状態のアニメーションを再生する（当選演出の停止用）。</summary>
        public void PlayIdleAnimation()
        {
            StopPulseAnimation();
            if (_animator != null)
            {
                // Animator をデフォルト状態に戻す（必要に応じて）
                _animator.Play("Idle", 0, 0f);
            }
        }

        /// <summary>当選アニメーションを再生して完了を待機する。</summary>
        public async UniTask PlayWinAnim(CancellationToken ct)
        {
            if (_animator == null || _winAnim == null) return;
            _animator.Play(_winAnim.name);
            await UniTask.Delay((int)(_winAnim.length * 1000), cancellationToken: ct);
        }

        /// <summary>当選アニメーションを再生して完了を待機する（引数指定版）。</summary>
        public async UniTask PlayWinAnim(AnimationClip clip, CancellationToken ct)
        {
            if (_animator == null || clip == null) return;
            _animator.Play(clip.name);
            await UniTask.Delay((int)(clip.length * 1000), cancellationToken: ct);
        }

        private void OnDestroy()
        {
            StopPulseAnimation();
        }
    }
}
