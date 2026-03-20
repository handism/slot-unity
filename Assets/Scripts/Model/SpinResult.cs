using System.Collections.Generic;

namespace SlotGame.Model
{
    /// <summary>1 スピンの結果（イミュータブルな値オブジェクト）</summary>
    public sealed record SpinResult(
        int[,]                 StoppedSymbolIds,   // [reel, row] ※配列は参照型なので変更注意
        IReadOnlyList<LineWin> LineWins,
        bool                   HasScatter,
        int                    ScatterCount,
        bool                   HasBonusCondition,
        long                   TotalWinAmount
    );

    /// <summary>ペイライン 1 本の当選情報</summary>
    public sealed record LineWin(
        int  LineIndex,
        int  SymbolId,
        int  MatchCount,   // 3/4/5
        long WinAmount
    );
}
