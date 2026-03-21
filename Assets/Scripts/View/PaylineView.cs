using System.Collections.Generic;
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
        private Coroutine    _glowCoroutine;

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            _lineRenderer.enabled = false;
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

            // 発光表現（簡易的に太さを変えるなどのアニメーション）
            StopGlow();
            _glowCoroutine = StartCoroutine(GlowAnimation());
        }

        public void Clear()
        {
            StopGlow();
            _lineRenderer.enabled = false;
        }

        private void StopGlow()
        {
            if (_glowCoroutine != null)
            {
                StopCoroutine(_glowCoroutine);
                _glowCoroutine = null;
            }
        }

        private System.Collections.IEnumerator GlowAnimation()
        {
            float baseWidth = _lineRenderer.startWidth;
            if (baseWidth <= 0) baseWidth = 10f; // デフォルト太さ

            while (true)
            {
                float time = Time.time * 5f;
                float width = baseWidth * (1f + 0.3f * Mathf.Sin(time));
                _lineRenderer.startWidth = width;
                _lineRenderer.endWidth   = width;
                yield return null;
            }
        }
    }
}
