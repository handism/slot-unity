namespace SlotGame.Model
{
    /// <summary>
    /// ゲーム設定の純粋なデータ構造（ピュア C#）。
    /// ScriptableObject である GameConfigData から値を抽出して渡すために使用。
    /// </summary>
    public record SlotConfig(
        long InitialCoins,
        long MaxCoins,
        int[] ValidBetAmounts,
        int ReelCount,
        int RowCount,
        int MinMatch,
        int MaxFreeSpinAddition,
        int DefaultAutoSpinCount,
        float DefaultBgmVolume,
        float DefaultSeVolume,
        string ChecksumSalt
    );
}
