using UnityEngine;

namespace SlotGame.Data
{
    public enum SymbolType
    {
        Normal,   // 通常シンボル（Seven〜Lemon）
        Wild,     // ワイルド: 通常シンボルの代替として機能
        Scatter,  // スキャター: ペイラインに依存しない全体判定、フリースピン発動
        Bonus,    // ボーナストリガー: リール 0/2/4 全てに出現でボーナスラウンド発動
        Filler    // 空白シンボル: ペイラインを遮断するだけで配当なし（RTP 調整用）
    }

    [CreateAssetMenu(fileName = "SymbolData", menuName = "SlotGame/Symbol Data")]
    public class SymbolData : ScriptableObject
    {
        public int          symbolId;
        public string       symbolName;
        public Sprite       sprite;
        public SymbolType   type;
        public int[]        payouts;      // [0]=3揃え倍率, [1]=4揃え, [2]=5揃え（Normal のみ有効）
        /// <summary>
        /// 当選時の専用アニメーション（Animator 用）。
        /// 設定されている場合は SymbolView で再生され、未設定の場合は共通のパルス演出が適用されます。
        /// </summary>
        public AnimationClip winAnim;
    }
}
