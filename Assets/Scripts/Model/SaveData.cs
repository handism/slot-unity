using System;

namespace SlotGame.Model
{
    [Serializable]
    public class SaveData
    {
        public long   coins       = 1000;
        public int    betAmount   = 10;
        public float  bgmVolume   = 0.8f;
        public float  seVolume    = 1.0f;
        public long   totalSpins  = 0;
        public long   totalWins   = 0;
        public long   maxWin      = 0;
        public int    totalFreeSpinTriggers = 0;
        public string saveVersion = "1.0";
        public string checksum    = "";
        public bool   hasCompletedTutorial = false;
        public bool   isTurbo = false;
    }
}
