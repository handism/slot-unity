using System.Collections.Generic;

namespace SlotGame.Model
{
    /// <summary>シンボルの位置情報（[reel, row]）</summary>
    public sealed record SymbolPosition(int Reel, int Row);

    /// <summary>1 スピンの結果（イミュータブルな値オブジェクト）</summary>
    public sealed record SpinResult(
        int[,]                 StoppedSymbolIds,   // [reel, row] ※配列は参照型なので変更注意
        IReadOnlyList<LineWin> LineWins,
        bool                   HasScatter,
        int                    ScatterCount,
        IReadOnlyList<SymbolPosition> ScatterPositions,
        bool                   HasBonusCondition,
        IReadOnlyList<SymbolPosition> BonusPositions,
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
