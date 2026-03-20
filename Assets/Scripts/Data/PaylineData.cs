using System;
using UnityEngine;

namespace SlotGame.Data
{
    /// <summary>ペイライン 1 本分の定義（5 リール × 行インデックス）</summary>
    [Serializable]
    public struct PaylineEntry
    {
        /// <summary>要素数 5。rows[reelIndex] = 行インデックス（0=Top, 1=Mid, 2=Bot）</summary>
        public int[] rows;
    }

    [CreateAssetMenu(fileName = "PaylineData", menuName = "SlotGame/Payline Data")]
    public class PaylineData : ScriptableObject
    {
        /// <summary>25 ペイライン定義</summary>
        public PaylineEntry[] lines;
    }
}
