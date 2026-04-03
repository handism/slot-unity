namespace SlotGame.Core
{
    using SlotGame.Model;
    using SlotGame.Utility;

    /// <summary>
    /// <see cref="GameContextInitializer"/> への読み取り専用プロキシ。
    /// Boot シーンを経由しなかった場合（テスト・直接起動等）は各プロパティが null を返す。
    /// </summary>
    public static class GameContext
    {
        public static GameState        GameState       => GameContextInitializer.Instance?.GameState;
        public static SaveDataManager  SaveDataManager => GameContextInitializer.Instance?.SaveDataManager;
        public static IRandomGenerator Random          => GameContextInitializer.Instance?.Random;
        public static SaveData         SaveData        => GameContextInitializer.Instance?.SaveData;
    }
}
