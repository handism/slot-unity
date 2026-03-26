using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace SlotGame.View
{
    /// <summary>
    /// 当選したペイラインを視覚的に表示する View。
    /// LineRenderer を使用してリール間のシンボルを接続する。
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class PaylineView : MonoBehaviour
    {
        private LineRenderer _lineRenderer;
        private Tween        _glowTween;
        private float        _baseWidth;

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            _lineRenderer.enabled = false;
            _baseWidth = _lineRenderer.startWidth;
            if (_baseWidth <= 0) _baseWidth = 10f;
        }

        /// <summary>
        /// 指定された座標リストを結ぶラインを描画し、光らせる。
        /// </summary>
        public void DrawLine(Vector3[] points, Color color)
        {
            _lineRenderer.positionCount = points.Length;
            _lineRenderer.SetPositions(points);
            _lineRenderer.startColor = color;
            _lineRenderer.endColor   = color;
            _lineRenderer.enabled    = true;

            StartGlowAnimation();
        }

        /// <summary>
        /// 指定された座標リストを結ぶラインをフロー演出（左から右へ描画）しながら表示する。
        /// </summary>
        public async UniTask AnimateDrawAsync(Vector3[] points, Color color, CancellationToken ct)
        {
            _lineRenderer.enabled = true;
            _lineRenderer.startColor = color;
            _lineRenderer.endColor = color;
            _lineRenderer.positionCount = 0;

            for (int i = 0; i < points.Length; i++)
            {
                _lineRenderer.positionCount = i + 1;
                _lineRenderer.SetPosition(i, points[i]);
                // 各ポイント間を少し待機してフロー感を出す
                await UniTask.Delay(100, cancellationToken: ct);
            }

            StartGlowAnimation();
        }

        public void Clear()
        {
            StopGlow();
            _lineRenderer.enabled = false;
        }

        private void StartGlowAnimation()
        {
            StopGlow();
            _glowTween = DOTween.To(
                () => _baseWidth,
                w => {
                    _lineRenderer.startWidth = w;
                    _lineRenderer.endWidth = w;
                },
                _baseWidth * 1.5f,
                0.5f
            ).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        }

        private void StopGlow()
        {
            if (_glowTween != null && _glowTween.IsActive())
            {
                _glowTween.Kill();
            }
            _glowTween = null;
            _lineRenderer.startWidth = _baseWidth;
            _lineRenderer.endWidth = _baseWidth;
        }

        private void OnDestroy()
        {
            StopGlow();
        }
    }
}
