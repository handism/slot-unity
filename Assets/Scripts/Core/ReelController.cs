using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using SlotGame.Data;
using SlotGame.View;
using UnityEngine;

namespace SlotGame.Core
{
    /// <summary>個別リールの回転・停止を制御する。</summary>
    [RequireComponent(typeof(ReelView))]
    public class ReelController : MonoBehaviour
    {
        [SerializeField] private ReelStripData reelStrip;

        public int ReelIndex { get; private set; }
        public bool IsSpinning => _isSpinning;

        private ReelView _view;
        private bool     _isSpinning;
        private bool     _skipRequested;
        private int      _targetStopIndex;

        private void Awake()
        {
            EnsureView();
            ReelIndex   = reelStrip != null ? reelStrip.reelIndex : 0;
        }

        public void Initialize(ReelStripData strip)
        {
            EnsureView();
            reelStrip = strip;
            ReelIndex = strip.reelIndex;
            _view.Initialize(strip);
        }

        /// <summary>高速スクロールを開始する。</summary>
        public void StartSpin()
        {
            _isSpinning    = true;
            _skipRequested = false;
            _view.StartScrolling();
        }

        /// <summary>
        /// 指定停止インデックスで停止する（中段シンボルのストリップインデックス）。
        /// キャンセル時はそのまま例外を上位に伝播させる。
        /// </summary>
        public async UniTask StopSpin(int targetStopIndex, CancellationToken ct)
        {
            _targetStopIndex = targetStopIndex;

            if (_skipRequested)
            {
                _view.SnapToPosition(targetStopIndex);
            }
            else
            {
                await _view.DecelerateAndStop(targetStopIndex, ct);
            }

            _isSpinning = false;
        }

        /// <summary>スピン中に呼ぶと全リールを即座にスナップ位置に停止させる。</summary>
        public void RequestSkip()
        {
            _skipRequested = true;
            if (_isSpinning)
                _view.SnapToPosition(_targetStopIndex);
        }

        /// <summary>現在表示中の 3 シンボル ID を返す（[0]=上段, [1]=中段, [2]=下段）。</summary>
        public int[] GetVisibleSymbolIds() => _view.GetVisibleSymbolIds();

        private void EnsureView()
        {
            if (_view == null)
                _view = GetComponent<ReelView>();
        }
    }
}
