using System;

namespace SlotGame.Utility
{
    /// <summary>プロダクション用乱数実装。System.Random をラップする。</summary>
    public class SystemRandomGenerator : IRandomGenerator
    {
        private readonly Random _random = new();

        public int Next(int minValue, int maxValue) => _random.Next(minValue, maxValue);

        public float NextFloat() => (float)_random.NextDouble();
    }
}
