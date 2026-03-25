using System;
using UnityEngine;

namespace SlotGame.Data
{
    [Serializable]
    public struct ScatterPayout
    {
        public int scatterCount;   // Scatter 出現個数（3/4/5）
        public int multiplier;     // ベット額に掛ける倍率
    }

    [Serializable]
    public struct BonusRewardEntry
    {
        public int multiplier;   // ベット額に掛ける倍率（×5〜×100）
        public int weight;       // 抽選重み（大きいほど出やすい）
    }

    [Serializable]
    public struct FreeSpinReward
    {
        public int scatterCount;   // Scatter 個数（3/4/5）
        public int spinCount;      // 付与されるスピン回数
    }

    [CreateAssetMenu(fileName = "PayoutTableData", menuName = "SlotGame/Payout Table Data")]
    public class PayoutTableData : ScriptableObject
    {
        public ScatterPayout[]    scatterPayouts;   // Scatter 個数ごとの配当
        public FreeSpinReward[]   freeSpinRewards;  // Scatter 個数ごとのフリースピン回数
        public int                freeSpinMultiplier = 2; // フリースピン中の配当倍率
        public BonusRewardEntry[] bonusRewards;     // ボーナスラウンド報酬の重み付きテーブル
    }
}
