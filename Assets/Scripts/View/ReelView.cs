#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using SlotGame.Data;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.View
{
    /// <summary>
    /// リール 1 本のシンボルスクロールアニメーションを担う View。
    /// 循環バッファ方式（5 シンボル固定、Instantiate/Destroy なし）を採用。
    /// </summary>
    public class ReelView : MonoBehaviour
    {
        [SerializeField] private SymbolView symbolViewPrefab = null!;
        [SerializeField] private float      symbolHeight = 180f;
        [SerializeField] private float      scrollSpeed  = 2000f;   // px/sec

        // バッファ内のシンボル数（上下バッファ 1 + 表示 3 + バッファ合計 = 5）
        private const int BufferSize = 5;

        private ReelStripData?   _strip;
        private SymbolView[]?    _symbolViews;   // 循環バッファ
        private RectTransform[]? _symbolRects;   // キャッシュ済み RectTransform（毎フレーム GetComponent を避ける）
        private int              _stripIndex;    // 現在のストリップ先頭インデックス
        private bool             _isScrolling;
        private float            _scrollOffset;
        private RectTransform?   _rectTransform;

        public void Initialize(ReelStripData strip)
        {
            _strip = strip;
            EnsureRectTransform();
            ResizeViewport();
            _symbolViews = new SymbolView[BufferSize];

            _symbolRects = new RectTransform[BufferSize];
            for (int i = 0; i < BufferSize; i++)
            {
                var view = Instantiate(symbolViewPrefab, transform);
                var rt   = view.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(0, GetSymbolYPosition(i));
                _symbolViews[i] = view;
                _symbolRects[i] = rt;
            }

            _stripIndex = 0;
            RefreshAllSymbols();
        }

        /// <summary>スクロールを開始する。</summary>
        public void StartScrolling()
        {
            _isScrolling  = true;
            _scrollOffset = 0;
        }

        private void Update()
        {
            if (!_isScrolling || _symbolViews == null) return;

            _scrollOffset += scrollSpeed * Time.deltaTime;

            // 1 シンボル分スクロールしたら循環
            while (_scrollOffset >= symbolHeight)
            {
                _scrollOffset -= symbolHeight;
                AdvanceStrip();
            }

            UpdateSymbolPositions();
        }

        private void UpdateSymbolPositions()
        {
            if (_symbolRects == null) return;
            for (int i = 0; i < BufferSize; i++)
            {
                float baseY = GetSymbolYPosition(i);
                _symbolRects[i].anchoredPosition = new Vector2(0, baseY + _scrollOffset);
            }
        }

        /// <summary>停止位置に向けて減速し、バウンスアニメーションで停止する。</summary>
        public async UniTask DecelerateAndStop(int targetStopIndex, CancellationToken ct)
        {
            _isScrolling = false;

            // 停止位置にスナップ
            AlignToStopIndex(targetStopIndex);

            // バウンスアニメーション（リール全体）
            float bounceAmount = 30f;
            Tween tween = null;
            try
            {
                tween = DOTween.To(
                            () => _scrollOffset,
                            value =>
                            {
                                _scrollOffset = value;
                                UpdateSymbolPositions();
                            },
                            -bounceAmount,
                            0.1f)
                        .SetEase(Ease.OutQuad);

                await tween.ToUniTask(cancellationToken: ct);
            }
            finally
            {
                tween?.Kill();
            }

            tween = null;
            try
            {
                tween = DOTween.To(
                            () => _scrollOffset,
                            value =>
                            {
                                _scrollOffset = value;
                                UpdateSymbolPositions();
                            },
                            0f,
                            0.15f)
                        .SetEase(Ease.OutBounce);

                await tween.ToUniTask(cancellationToken: ct);
            }
            finally
            {
                tween?.Kill();
            }

            // 全シンボルを整列
            SnapAllToGrid();
        }

        /// <summary>即座に停止位置にスナップする（早期停止用）。</summary>
        public void SnapToPosition(int targetStopIndex)
        {
            _isScrolling = false;
            AlignToStopIndex(targetStopIndex);
            SnapAllToGrid();
        }

        /// <summary>現在表示中の 3 シンボル ID を返す（[0]=上段, [1]=中段, [2]=下段）。</summary>
        public int[] GetVisibleSymbolIds()
        {
            if (_symbolViews == null) return Array.Empty<int>();
            // _symbolViews[1]=上段, [2]=中段, [3]=下段
            return new[]
            {
                _symbolViews[1].SymbolId,
                _symbolViews[2].SymbolId,
                _symbolViews[3].SymbolId,
            };
        }

        /// <summary>指定行のシンボルで当選アニメーションを再生して完了を待機する。</summary>
        public async UniTask PlayWinAnimation(int row, CancellationToken ct)
        {
            // row: 0=上段, 1=中段, 2=下段 → _symbolViews インデックスは 1〜3
            if (_symbolViews == null || row + 1 >= _symbolViews.Length) return;
            await _symbolViews[row + 1].PlayWinPresentation(ct);
        }

        /// <summary>指定行の SymbolView を取得する。</summary>
        public SymbolView? GetSymbolView(int row)
        {
            if (_symbolViews == null || row + 1 >= _symbolViews.Length) return null;
            return _symbolViews[row + 1];
        }

        /// <summary>指定行のシンボルの世界座標を取得する（0=上, 1=中, 2=下）。</summary>
        public Vector3 GetSymbolWorldPosition(int row)
        {
            if (_symbolViews == null || row + 1 >= _symbolViews.Length) return transform.position;
            return _symbolViews[row + 1].transform.position;
        }

        public void HighlightRows(IReadOnlyCollection<int>? rows)
        {
            if (_symbolViews == null) return;

            for (int row = 0; row < 3; row++)
                _symbolViews[row + 1].SetHighlighted(rows != null && rows.Any(r => r == row));
        }

        public void ClearHighlights()
        {
            if (_symbolViews == null) return;

            for (int i = 1; i <= 3; i++)
            {
                _symbolViews[i].ResetHighlight();
            }
        }

        // ─── ヘルパー ─────────────────────────────────────────────────────

        private void AdvanceStrip()
        {
            if (_symbolViews == null || _symbolRects == null || _strip == null) return;

            // 先頭バッファを末尾に移動させて循環
            _stripIndex = (_stripIndex + 1) % _strip.strip.Count;
            var first     = _symbolViews[0];
            var firstRect = _symbolRects[0];
            for (int i = 0; i < BufferSize - 1; i++)
            {
                _symbolViews[i] = _symbolViews[i + 1];
                _symbolRects[i] = _symbolRects[i + 1];
            }
            _symbolViews[BufferSize - 1] = first;
            _symbolRects[BufferSize - 1] = firstRect;

            // 新しい末尾にストリップのシンボルを設定
            int newIdx = (_stripIndex + BufferSize - 1) % _strip.strip.Count;
            _symbolViews[BufferSize - 1].SetSymbol(_strip.strip[newIdx]);
            _symbolRects[BufferSize - 1].anchoredPosition = new Vector2(0, GetSymbolYPosition(BufferSize - 1));
        }

        private void AlignToStopIndex(int stopIndex)
        {
            if (_symbolViews == null || _strip == null) return;

            // 停止インデックスを中段（row=1 = _symbolViews[2]）に来るように調整
            int mid = stopIndex;
            int top = (mid - 1 + _strip.strip.Count) % _strip.strip.Count;
            int bot = (mid + 1) % _strip.strip.Count;
            int topBuf = (top - 1 + _strip.strip.Count) % _strip.strip.Count;
            int botBuf = (bot + 1) % _strip.strip.Count;

            _symbolViews[0].SetSymbol(_strip.strip[topBuf]);
            _symbolViews[1].SetSymbol(_strip.strip[top]);
            _symbolViews[2].SetSymbol(_strip.strip[mid]);
            _symbolViews[3].SetSymbol(_strip.strip[bot]);
            _symbolViews[4].SetSymbol(_strip.strip[botBuf]);

            // ストリップ位置を更新して次のスピン開始時のジャンプを防ぐ
            _stripIndex = topBuf;
        }

        private void SnapAllToGrid()
        {
            if (_symbolRects == null) return;

            for (int i = 0; i < BufferSize; i++)
            {
                _symbolRects[i].anchoredPosition = new Vector2(0, GetSymbolYPosition(i));
            }
            _scrollOffset = 0;
        }

        private void RefreshAllSymbols()
        {
            if (_symbolViews == null || _strip == null) return;

            for (int i = 0; i < BufferSize; i++)
            {
                int idx = (_stripIndex + i) % _strip.strip.Count;
                _symbolViews[i].SetSymbol(_strip.strip[idx]);
            }
        }

        private void EnsureRectTransform()
        {
            _rectTransform ??= GetComponent<RectTransform>();
        }

        private void ResizeViewport()
        {
            if (_rectTransform == null) return;

            float width = symbolHeight;
            if (symbolViewPrefab != null &&
                symbolViewPrefab.TryGetComponent<RectTransform>(out var symbolRect) &&
                symbolRect.rect.width > 0f)
            {
                width = symbolRect.rect.width;
            }

            _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, symbolHeight * 3f);
        }

        private float GetSymbolYPosition(int bufferIndex)
        {
            float centeredIndex = (float)(BufferSize - 1) * 0.5f;
            return (centeredIndex - bufferIndex) * symbolHeight;
        }
    }
}
