using System;

namespace SlotGame.Utility
{
    /// <summary>テスト用乱数実装。固定シードで決定論的に動作する。</summary>
    public class SeededRandomGenerator : IRandomGenerator
    {
        private readonly Random _random;

        public SeededRandomGenerator(int seed) => _random = new Random(seed);

        public int Next(int minValue, int maxValue) => _random.Next(minValue, maxValue);

        public float NextFloat() => (float)_random.NextDouble();
    }
}
