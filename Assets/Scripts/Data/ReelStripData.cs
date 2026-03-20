using System.Collections.Generic;
using UnityEngine;

namespace SlotGame.Data
{
    [CreateAssetMenu(fileName = "ReelStripData", menuName = "SlotGame/Reel Strip Data")]
    public class ReelStripData : ScriptableObject
    {
        public int              reelIndex;
        public List<SymbolData> strip;   // 出目順に並んだシンボルリスト（重複あり）
    }
}
