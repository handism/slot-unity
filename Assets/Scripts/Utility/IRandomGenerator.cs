namespace SlotGame.Utility
{
    /// <summary>乱数生成インターフェース。テスト時に決定論的な実装を差し込めるようにする。</summary>
    public interface IRandomGenerator
    {
        /// <summary>minValue 以上 maxValue 未満の整数を返す。</summary>
        int Next(int minValue, int maxValue);

        /// <summary>0.0f 以上 1.0f 未満の浮動小数点数を返す。</summary>
        float NextFloat();
    }
}
