using UnityEngine;
using SlotGame.Model;

namespace SlotGame.Data
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "SlotGame/GameConfig")]
    public class GameConfigData : ScriptableObject
    {
        [Header("Balance")]
        public long initialCoins = 1000;
        public long maxCoins = 9_999_999;
        public int[] validBetAmounts = { 10, 20, 50, 100 };

        [Header("Slot Rules")]
        public int reelCount = 5;
        public int rowCount = 3;
        public int minMatch = 3;
        public int[] bonusTriggerReels = { 0, 2, 4 };

        [Header("Free Spins")]
        public int maxFreeSpinAddition = 20;

        [Header("Auto Spin")]
        public int defaultAutoSpinCount = 10;

        [Header("Audio Defaults")]
        public float defaultBgmVolume = 0.8f;
        public float defaultSeVolume = 1.0f;

        [Header("Security")]
        [SerializeField] private string checksumSalt = "SALTY_SLOT_2026";
        public string ChecksumSalt => checksumSalt;

        public SlotConfig ToModelConfig(int freeSpinMultiplier = 2)
        {
            return new SlotConfig(
                initialCoins,
                maxCoins,
                validBetAmounts,
                reelCount,
                rowCount,
                minMatch,
                bonusTriggerReels,
                freeSpinMultiplier,
                maxFreeSpinAddition,
                defaultAutoSpinCount,
                defaultBgmVolume,
                defaultSeVolume,
                checksumSalt
            );
        }
    }
}
