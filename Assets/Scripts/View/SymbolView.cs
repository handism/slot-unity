using System.Threading;
using Cysharp.Threading.Tasks;
using SlotGame.Data;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.View
{
    /// <summary>シンボル 1 個の表示を担う View（オブジェクトプール管理対象）。</summary>
    [RequireComponent(typeof(Image))]
    public class SymbolView : MonoBehaviour
    {
        private Image     _image;
        private Animator  _animator;
        private int       _symbolId;

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
        }

        public void SetSymbolId(int id) => _symbolId = id;

        /// <summary>当選アニメーションを再生して完了を待機する。</summary>
        public async UniTask PlayWinAnim(AnimationClip clip, CancellationToken ct)
        {
            if (_animator == null || clip == null) return;
            _animator.runtimeAnimatorController = null;
            _animator.Play(clip.name);
            await UniTask.Delay((int)(clip.length * 1000), cancellationToken: ct);
        }
    }
}
