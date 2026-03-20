using TMPro;
using UnityEngine;

namespace SlotGame.View
{
    /// <summary>フリースピン中の残り回数・累計獲得コインを表示する View。</summary>
    public class FreeSpinHUDView : MonoBehaviour
    {
        [SerializeField] private TMP_Text remainingText;
        [SerializeField] private TMP_Text totalWinText;

        public void UpdateDisplay(int remaining, long totalWin)
        {
            remainingText.text = $"FREE SPINS: {remaining}";
            totalWinText.text  = $"TOTAL WIN: {totalWin:N0}";
        }
    }
}
