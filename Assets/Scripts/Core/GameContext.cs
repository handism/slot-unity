namespace SlotGame.Core
{
    using SlotGame.Model;
    using SlotGame.Utility;

    /// <summary>
    /// シーン間でデータを引き継ぐための静的コンテナ。
    /// Boot シーンで生成された Model インスタンスを Main シーンへ渡す。
    /// </summary>
    public static class GameContext
    {
        public static GameState        GameState       { get; set; }
        public static SaveDataManager SaveDataManager { get; set; }
        public static IRandomGenerator Random          { get; set; }
        public static SaveData         SaveData        { get; set; }
    }
}
